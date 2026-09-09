/*
RFC-010 slice 2 fixture: ported from technitium-content-filter's
patches/dns-server/pr1-repro (RfcRepro.App/BravoApp.cs), unchanged.
*/

using System;
using System.Threading.Tasks;
using DnsServerCore.ApplicationCommon;
using RfcRepro.Helper;

namespace RfcRepro.App
{
    // Deliberately broken TYPE LOAD (not construction): the base type lives in
    // RfcRepro.Helper.dll, which is omitted from the installed app zip. Resolving this class's
    // layout (its base type) fails with a TypeLoadException the moment the CLR tries to load the
    // type -- during Assembly.GetTypes()/ExportedTypes enumeration, not when anyone constructs it
    // or JITs one of its method bodies.
    public class BravoApp : MissingBase, IDnsApplication
    {
        public string? Description => "BravoApp - deliberately broken type load";

        public Task InitializeAsync(IDnsServer dnsServer, string? config)
        {
            return Task.CompletedTask;
        }

        public void Dispose()
        {
        }
    }
}
