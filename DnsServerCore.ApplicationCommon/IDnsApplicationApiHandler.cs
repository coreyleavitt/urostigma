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

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace DnsServerCore.ApplicationCommon
{
    /// <summary>
    /// Specifies the permission level the DNS Server's web console must require of the authenticated
    /// user before dispatching a request to an <see cref="IDnsApplicationApiHandler"/>. Declared in
    /// <c>DnsServerCore.ApplicationCommon</c> rather than as the console's own permission flag type
    /// because that type lives in <c>DnsServerCore</c>, which this assembly cannot reference; the
    /// console dispatch maps this value to its own <c>PermissionFlag</c>.
    /// </summary>
    public enum DnsAppApiAccess
    {
        /// <summary>The request only reads app or server state.</summary>
        View = 0,

        /// <summary>The request mutates app or server state.</summary>
        Modify = 1
    }

    /// <summary>
    /// Lets a DNS App expose its own HTTP-shaped request/response API through the DNS Server's
    /// existing web console, routed via <c>/api/apps/call?name=&lt;app&gt;&amp;path=&lt;subpath&gt;</c>.
    /// The console authenticates the caller and dispatches the request to this handler; it never
    /// inspects or transforms the request or response bodies. An app should register at most one
    /// implementor of this interface: an app is one API namespace, and path routing inside the
    /// handler is expected to express anything a second implementing class would. If an app
    /// registers more than one implementor, the console logs a warning at load time and the dispatch
    /// route answers HTTP 500 for that app until the ambiguity is fixed.
    /// </summary>
    public interface IDnsApplicationApiHandler
    {
        /// <summary>
        /// Declares the permission level the console must enforce for the given request before
        /// dispatching it to <see cref="HandleApiRequestAsync"/>. The default is
        /// <see cref="DnsAppApiAccess.View"/>; override to return <see cref="DnsAppApiAccess.Modify"/>
        /// for request paths that mutate state. Implemented as a default interface method so
        /// existing implementors keep compiling unchanged if this enum grows.
        /// </summary>
        /// <param name="request">
        /// The request the console is about to dispatch. Its <see cref="DnsAppApiRequest.Body"/> is
        /// not yet populated when this method is called -- the console checks permission before
        /// reading the request body, so a request that will be denied is never read into memory.
        /// </param>
        /// <returns>The permission level required for this request.</returns>
        DnsAppApiAccess GetRequiredAccess(DnsAppApiRequest request)
        {
            return DnsAppApiAccess.View;
        }

        /// <summary>
        /// Handles one HTTP-shaped API request routed to this app by the DNS Server's web console.
        /// Must not throw: an exception that escapes this method is answered by the console's
        /// dispatch route as a raw HTTP 500 with no response body, bypassing this app's own
        /// status-code contract entirely. Implementors are expected to catch their own faults and
        /// return a <see cref="DnsAppApiResponse"/> that describes them instead.
        /// </summary>
        /// <param name="request">The request to handle.</param>
        /// <param name="cancellationToken">
        /// Signalled when the calling HTTP connection is aborted by the client. This is the first
        /// inbound DNS App capability that carries a cancellation token: dispatching runs arbitrary
        /// app code inside an HTTP request, and a disconnecting caller should be able to stop queued
        /// work. The console threads its own <c>HttpContext.RequestAborted</c> here, never
        /// <see cref="CancellationToken.None"/>.
        /// </param>
        /// <returns>The response to write back to the caller verbatim.</returns>
        Task<DnsAppApiResponse> HandleApiRequestAsync(DnsAppApiRequest request, CancellationToken cancellationToken);
    }

    /// <summary>
    /// An HTTP-shaped request dispatched to an app's <see cref="IDnsApplicationApiHandler"/> by the
    /// DNS Server's web console. The console parses HTTP syntax exactly once; apps never re-parse it.
    /// </summary>
    public class DnsAppApiRequest
    {
        #region variables

        readonly string _method;
        readonly string _path;
        readonly IReadOnlyDictionary<string, string> _query;
        readonly string _contentType;
        readonly byte[] _body;
        readonly string _username;

        #endregion

        #region constructor

        public DnsAppApiRequest(string method, string path, IReadOnlyDictionary<string, string> query, string contentType, byte[] body, string username)
        {
            _method = method;
            _path = path;
            _query = query;
            _contentType = contentType;
            _body = body;
            _username = username;
        }

        #endregion

        #region properties

        /// <summary>The real HTTP verb (e.g. "GET", "POST") the caller used.</summary>
        public string Method
        { get { return _method; } }

        /// <summary>The app-relative path: the <c>path</c> query parameter of <c>/api/apps/call</c>, with no leading slash.</summary>
        public string Path
        { get { return _path; } }

        /// <summary>The request's query string parameters, parsed once by the console.</summary>
        public IReadOnlyDictionary<string, string> Query
        { get { return _query; } }

        /// <summary>
        /// The request's <c>Content-Type</c> header value, verbatim. Empty (never <c>null</c>) when
        /// the caller sent no <c>Content-Type</c> header. A single field rather than a general headers
        /// dictionary: it is the only request header a body-processing handler structurally needs to
        /// content-negotiate or validate its input media type, and the additive-evolution rule (new
        /// members, never removed ones) covers any future case a handler turns out to need.
        /// </summary>
        public string ContentType
        { get { return _contentType; } }

        /// <summary>
        /// The raw request body, buffered into memory by the console up to its configured size cap.
        /// The cap is enforced while the body is read, so a chunked request with no
        /// <c>Content-Length</c> header cannot bypass it. Never <c>null</c>; a zero-length array when
        /// the request carried no body, or when permission has not yet been checked (see
        /// <see cref="IDnsApplicationApiHandler.GetRequiredAccess"/>).
        /// </summary>
        public byte[] Body
        { get { return _body; } }

        /// <summary>The username of the console session that authenticated this request.</summary>
        public string Username
        { get { return _username; } }

        #endregion
    }

    /// <summary>
    /// The response an <see cref="IDnsApplicationApiHandler"/> returns for the console to write back
    /// to the caller verbatim: <see cref="StatusCode"/>, <see cref="ContentType"/> and
    /// <see cref="Body"/> all reach the wire untouched -- no envelope, no status-code rewriting.
    /// </summary>
    public class DnsAppApiResponse
    {
        #region variables

        readonly int _statusCode;
        readonly string _contentType;
        readonly byte[] _body;

        #endregion

        #region constructor

        public DnsAppApiResponse(int statusCode, string contentType, byte[] body)
        {
            _statusCode = statusCode;
            _contentType = contentType;
            _body = body;
        }

        #endregion

        #region properties

        /// <summary>The HTTP status code to write to the response, verbatim.</summary>
        public int StatusCode
        { get { return _statusCode; } }

        /// <summary>The <c>Content-Type</c> header value to write to the response.</summary>
        public string ContentType
        { get { return _contentType; } }

        /// <summary>The response body to write, verbatim. Never <c>null</c>; use a zero-length array for an empty body.</summary>
        public byte[] Body
        { get { return _body; } }

        #endregion
    }
}
