/*
RFC-010 slice 3 fixture: first of two IDnsApplicationApiHandler implementors
registered by the same app, to prove the constructor type sweep surfaces the
ambiguity condition (rather than throwing) when an app registers more than
one handler.
*/

using System;
using System.Threading;
using System.Threading.Tasks;
using DnsServerCore.ApplicationCommon;

namespace RfcApiHandler.Ambiguous
{
    public class HandlerOne : IDnsApplication, IDnsApplicationApiHandler
    {
        public string? Description => "HandlerOne - first of two IDnsApplicationApiHandler implementors";

        public Task InitializeAsync(IDnsServer dnsServer, string? config)
        {
            return Task.CompletedTask;
        }

        public Task<DnsAppApiResponse> HandleApiRequestAsync(DnsAppApiRequest request, CancellationToken cancellationToken)
        {
            return Task.FromResult(new DnsAppApiResponse(200, "text/plain", Array.Empty<byte>()));
        }

        public void Dispose()
        {
        }
    }
}
