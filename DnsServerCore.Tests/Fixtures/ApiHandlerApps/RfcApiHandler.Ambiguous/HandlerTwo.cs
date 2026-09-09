/*
RFC-010 slice 3 fixture: second of two IDnsApplicationApiHandler implementors
registered by the same app -- see HandlerOne.cs.
*/

using System;
using System.Threading;
using System.Threading.Tasks;
using DnsServerCore.ApplicationCommon;

namespace RfcApiHandler.Ambiguous
{
    public class HandlerTwo : IDnsApplication, IDnsApplicationApiHandler
    {
        public string? Description => "HandlerTwo - second of two IDnsApplicationApiHandler implementors";

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
