/*
RFC-010 slice 2 fixture: ported from technitium-content-filter's
patches/dns-server/pr1-repro (RfcRepro.App/CharlieApp.cs), unchanged.
*/

using System;
using System.Threading.Tasks;
using DnsServerCore.ApplicationCommon;

namespace RfcRepro.App
{
    // Enumeration-order marker: comes after BravoApp. If BravoApp's failure aborts the whole
    // foreach (ExportedTypes lazily-throws-mid-enumeration hypothesis from the PR draft),
    // CharlieApp should never register. If GetTypes() throws for the WHOLE array up front
    // (materialize-then-throw hypothesis), CharlieApp -- and AlphaApp -- register only under the
    // patched code, never under stock.
    public class CharlieApp : IDnsApplication
    {
        public string? Description => "CharlieApp - healthy, loads third";

        public Task InitializeAsync(IDnsServer dnsServer, string? config)
        {
            return Task.CompletedTask;
        }

        public void Dispose()
        {
        }
    }
}
