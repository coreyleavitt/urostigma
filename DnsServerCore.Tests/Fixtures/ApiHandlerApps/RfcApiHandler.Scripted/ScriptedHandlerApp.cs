/*
RFC-010 slice 4 fixture: an IDnsApplicationApiHandler implementor whose
GetRequiredAccess/HandleApiRequestAsync behavior is selected by
DnsAppApiRequest.Path -- the dispatcher-pure DnsAppApiDispatcher tests need a
handler that can be made to throw from either method, or to answer
successfully with the exact body it was invoked with, and they need it
per-test rather than fixed at fixture-construction time. Path vocabulary:

  "throw-authorize" -> GetRequiredAccess throws
  "throw-handle"     -> HandleApiRequestAsync throws
  "modify"/"modify/*" -> GetRequiredAccess requires DnsAppApiAccess.Modify
  anything else       -> GetRequiredAccess requires the default (View);
                         HandleApiRequestAsync echoes the request body back
                         verbatim, so a test can prove the exact bytes the
                         dispatcher's bodyReader produced reached the handler.
*/

using System;
using System.Threading;
using System.Threading.Tasks;
using DnsServerCore.ApplicationCommon;

namespace RfcApiHandler.Scripted
{
    public class ScriptedHandlerApp : IDnsApplication, IDnsApplicationApiHandler
    {
        public string? Description => "ScriptedHandlerApp - path-driven IDnsApplicationApiHandler for dispatcher tests";

        public Task InitializeAsync(IDnsServer dnsServer, string? config)
        {
            return Task.CompletedTask;
        }

        public DnsAppApiAccess GetRequiredAccess(DnsAppApiRequest request)
        {
            if (string.Equals(request.Path, "throw-authorize", StringComparison.Ordinal))
                throw new InvalidOperationException("ScriptedHandlerApp: scripted GetRequiredAccess throw.");

            if (request.Path.StartsWith("modify", StringComparison.Ordinal))
                return DnsAppApiAccess.Modify;

            return DnsAppApiAccess.View;
        }

        public Task<DnsAppApiResponse> HandleApiRequestAsync(DnsAppApiRequest request, CancellationToken cancellationToken)
        {
            if (string.Equals(request.Path, "throw-handle", StringComparison.Ordinal))
                throw new InvalidOperationException("ScriptedHandlerApp: scripted HandleApiRequestAsync throw.");

            //echo the body back verbatim so callers can prove exactly what reached the handler
            return Task.FromResult(new DnsAppApiResponse(200, "application/octet-stream", request.Body));
        }

        public void Dispose()
        {
        }
    }
}
