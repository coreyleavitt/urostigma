/*
RFC-010 slice 3 fixture: a healthy DNS app that implements
IDnsApplicationApiHandler exactly once. Used to prove the constructor type
sweep discovers a fake app's IDnsApplicationApiHandler and surfaces it on
DnsApplication.DnsApplicationApiHandler without flagging ambiguity.
*/

using System;
using System.Threading;
using System.Threading.Tasks;
using DnsServerCore.ApplicationCommon;

namespace RfcApiHandler.Single
{
    public class SingleHandlerApp : IDnsApplication, IDnsApplicationApiHandler
    {
        public string? Description => "SingleHandlerApp - registers one IDnsApplicationApiHandler";

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
