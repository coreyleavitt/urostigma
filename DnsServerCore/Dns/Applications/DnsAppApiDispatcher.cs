/*
Urostigma DNS Server
Copyright (C) 2026  Corey Leavitt

This program is free software: you can redistribute it and/or modify
it under the terms of the GNU General Public License as published by
the Free Software Foundation, either version 3 of the License, or
(at your option) any later version.

This program is distributed in the hope that it will be useful,
but WITHOUT ANY WARRANTY; without even the implied warranty of
MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
GNU General Public License for more details.

You should have received a copy of the GNU General Public License
along with this program.  If not, see <http://www.gnu.org/licenses/>.

*/

using DnsServerCore.ApplicationCommon;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace DnsServerCore.Dns.Applications
{
    /// <summary>
    /// Dispatches <c>/api/apps/call</c> requests to the named app's
    /// <see cref="IDnsApplicationApiHandler"/>. Static and dispatcher-pure: no
    /// <see cref="Microsoft.AspNetCore.Http.HttpContext"/> and no <c>DnsWebService</c> involvement, so
    /// it is testable without a running web host. Each dispatch is independent -- there is no
    /// per-instance state worth holding, so the type is not instantiated.
    /// </summary>
    /// <remarks>
    /// RFC-010 slice 4: folds the remainder of patch 0003's <c>CallAppApiAsync</c> into this shape.
    /// Dispatch is internally two-phase -- authorize against a body-less probe request, then read the
    /// body and invoke -- but that ordering is unrepresentable at the boundary, not adapter
    /// convention: <paramref name="bodyReader"/>-typed parameters are invoked only after a permitted
    /// verdict, so "a denied request never buffers the body" (0003's deliberate security property) is
    /// provable from this method's own tests. The handler resolved while authorizing is the same
    /// instance invoked -- it is never re-resolved from <paramref name="apps"/> between phases, so an
    /// app that is updated or uninstalled in the window between authorize and invoke cannot swap in a
    /// handler that was never authorized.
    /// </remarks>
    internal static class DnsAppApiDispatcher
    {
        #region public

        /// <summary>
        /// Dispatches one <c>/api/apps/call</c> request. Never throws: every failure mode -- app not
        /// found, an app with no (or an ambiguous) <see cref="IDnsApplicationApiHandler"/>, permission
        /// denial, and an exception from either <see cref="IDnsApplicationApiHandler.GetRequiredAccess"/>
        /// or <see cref="IDnsApplicationApiHandler.HandleApiRequestAsync"/> -- is classified into
        /// <see cref="Outcome"/> instead.
        /// </summary>
        /// <param name="apps">
        /// The live app table, e.g. <c>DnsApplicationManager.Applications</c>. Looked up once, by
        /// <see cref="RequestMetadata.AppName"/>, at authorize time.
        /// </param>
        /// <param name="requestMetadata">The request, without its body.</param>
        /// <param name="permissionCheck">
        /// Checks the caller's permission for the access level the resolved handler's
        /// <see cref="IDnsApplicationApiHandler.GetRequiredAccess"/> declares. The one delegate over
        /// <c>AuthManager</c> -- the dispatcher itself never references it, keeping this method
        /// constructible without the config-file I/O <c>AuthManager</c>'s constructor performs.
        /// </param>
        /// <param name="bodyReader">
        /// Reads the request body. Invoked at most once, and only after <paramref name="permissionCheck"/>
        /// has permitted the request -- never on a path that returns <see cref="OutcomeKind.AccessDenied"/>,
        /// <see cref="OutcomeKind.NotFound"/>, <see cref="OutcomeKind.Ambiguous"/>, or an
        /// <see cref="OutcomeKind.HandlerThrew"/> that came from <c>GetRequiredAccess</c>.
        /// </param>
        /// <param name="cancellationToken">Propagated to the handler's own async work.</param>
        public static async Task<Outcome> DispatchAsync(IReadOnlyDictionary<string, DnsApplication> apps, RequestMetadata requestMetadata, Func<DnsAppApiAccess, bool> permissionCheck, Func<CancellationToken, Task<byte[]>> bodyReader, CancellationToken cancellationToken)
        {
            if (!apps.TryGetValue(requestMetadata.AppName, out DnsApplication application))
                return Outcome.NotFound();

            if (application.DnsApplicationApiHandlerAmbiguous)
                return Outcome.Ambiguous();

            IDnsApplicationApiHandler apiHandler = application.DnsApplicationApiHandler;
            if (apiHandler is null)
                return Outcome.NotFound();

            //authorize against a body-less probe: GetRequiredAccess is called before bodyReader ever
            //runs, so a request that will be denied is never buffered into memory
            DnsAppApiRequest probeRequest = new DnsAppApiRequest(requestMetadata.Method, requestMetadata.Path, requestMetadata.Query, requestMetadata.ContentType, Array.Empty<byte>(), requestMetadata.Username);

            DnsAppApiAccess requiredAccess;

            try
            {
                requiredAccess = apiHandler.GetRequiredAccess(probeRequest);
            }
            catch (Exception ex)
            {
                //a fifth failure path 0003 left unguarded: GetRequiredAccess is app code, and a throw
                //from it happens strictly before bodyReader could ever run
                return Outcome.HandlerThrew(ex);
            }

            if (!permissionCheck(requiredAccess))
                return Outcome.AccessDenied();

            //only a permitted request ever reaches here: bodyReader is invoked at most once, and
            //never on a path that returns AccessDenied above
            byte[] body = await bodyReader(cancellationToken);

            DnsAppApiRequest request = new DnsAppApiRequest(requestMetadata.Method, requestMetadata.Path, requestMetadata.Query, requestMetadata.ContentType, body, requestMetadata.Username);

            //the handler resolved above is the one invoked -- never re-resolved from apps between
            //authorize and invoke, so an app updated or uninstalled in this window cannot swap in a
            //handler that was never authorized
            DnsAppApiResponse response;

            try
            {
                response = await apiHandler.HandleApiRequestAsync(request, cancellationToken);
            }
            catch (Exception ex)
            {
                return Outcome.HandlerThrew(ex);
            }

            return Outcome.Success(response);
        }

        #endregion

        #region types

        public enum OutcomeKind
        {
            NotFound,
            Ambiguous,
            AccessDenied,
            HandlerThrew,
            Success
        }

        /// <summary>The result of one <see cref="DispatchAsync"/> call.</summary>
        public sealed class Outcome
        {
            #region variables

            readonly OutcomeKind _kind;
            readonly DnsAppApiResponse _response;
            readonly Exception _exception;

            #endregion

            #region constructor

            private Outcome(OutcomeKind kind, DnsAppApiResponse response, Exception exception)
            {
                _kind = kind;
                _response = response;
                _exception = exception;
            }

            public static Outcome NotFound()
            {
                return new Outcome(OutcomeKind.NotFound, null, null);
            }

            public static Outcome Ambiguous()
            {
                return new Outcome(OutcomeKind.Ambiguous, null, null);
            }

            public static Outcome AccessDenied()
            {
                return new Outcome(OutcomeKind.AccessDenied, null, null);
            }

            public static Outcome HandlerThrew(Exception exception)
            {
                return new Outcome(OutcomeKind.HandlerThrew, null, exception);
            }

            public static Outcome Success(DnsAppApiResponse response)
            {
                return new Outcome(OutcomeKind.Success, response, null);
            }

            #endregion

            #region properties

            public OutcomeKind Kind
            { get { return _kind; } }

            /// <summary>The handler's response. Only set when <see cref="Kind"/> is <see cref="OutcomeKind.Success"/>.</summary>
            public DnsAppApiResponse Response
            { get { return _response; } }

            /// <summary>
            /// The exception the handler threw. Only set when <see cref="Kind"/> is
            /// <see cref="OutcomeKind.HandlerThrew"/>; carried so the adapter can log the real fault
            /// even though the HTTP response it writes is a generic raw 500.
            /// </summary>
            public Exception Exception
            { get { return _exception; } }

            #endregion
        }

        /// <summary>A <c>/api/apps/call</c> request, without its body.</summary>
        public sealed class RequestMetadata
        {
            #region variables

            readonly string _appName;
            readonly string _method;
            readonly string _path;
            readonly IReadOnlyDictionary<string, string> _query;
            readonly string _contentType;
            readonly string _username;

            #endregion

            #region constructor

            public RequestMetadata(string appName, string method, string path, IReadOnlyDictionary<string, string> query, string contentType, string username)
            {
                _appName = appName;
                _method = method;
                _path = path;
                _query = query;
                _contentType = contentType;
                _username = username;
            }

            #endregion

            #region properties

            /// <summary>The <c>name</c> query parameter: which app's table entry to dispatch to.</summary>
            public string AppName
            { get { return _appName; } }

            public string Method
            { get { return _method; } }

            /// <summary>The app-relative path: the <c>path</c> query parameter, with no leading slash.</summary>
            public string Path
            { get { return _path; } }

            public IReadOnlyDictionary<string, string> Query
            { get { return _query; } }

            public string ContentType
            { get { return _contentType; } }

            /// <summary>The username of the console session that authenticated this request.</summary>
            public string Username
            { get { return _username; } }

            #endregion
        }

        #endregion
    }
}
