/*
RFC-010 slice 2 fixture: ported from technitium-content-filter's
patches/dns-server/pr1-repro (RfcRepro.App/AlphaApp.cs), unchanged.
*/

using System;
using System.Threading.Tasks;
using DnsServerCore.ApplicationCommon;

namespace RfcRepro.App
{
    // Enumeration-order marker: comes before BravoApp alphabetically/declaration-wise.
    public class AlphaApp : IDnsApplication
    {
        public string? Description => "AlphaApp - healthy, loads first";

        public Task InitializeAsync(IDnsServer dnsServer, string? config)
        {
            return Task.CompletedTask;
        }

        public void Dispose()
        {
        }
    }
}
