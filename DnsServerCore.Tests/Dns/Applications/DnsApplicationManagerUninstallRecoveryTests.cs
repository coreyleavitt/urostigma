/*
RFC-010 slice 5, quirk 1 (recovery): UninstallApplication only ever deletes an
app's on-disk folder when the name is found in the loaded table
(_applications.TryRemove). A never-loaded app -- one that failed at server
startup (LoadAllApplicationsAsync's per-app catch swallows the throw into a log
entry) or mid an update that replaced its files but then failed to load -- has
no entry there, so uninstall silently no-ops today, leaving install-over as the
only recovery path. Install-over wipes dnsApp.config, which for a real
deployment is the app's persisted policy, so it is not an acceptable recovery.

The fix: UninstallApplication deletes the on-disk folder directly when the name
is absent from the loaded table, so a poisoned/never-loaded app can be removed
without touching config semantics elsewhere.

This test simulates the "never loaded" state directly (a folder placed under
the server's apps directory that DnsApplicationManager never loaded), rather
than reusing the zero-registration fixture's install path -- InstallApplicationAsync
already deletes its own folder on any load failure (see
DnsApplicationManagerZeroRegistrationTests), so it cannot exercise this
recovery gap. The gap is specific to folders that end up on disk without ever
entering the loaded table by some other route (startup, or an update that left
files in place).
*/

using System;
using System.IO;
using System.Threading.Tasks;
using DnsServerCore.Dns;
using DnsServerCore.Tests.Fixtures;
using Xunit;

namespace DnsServerCore.Tests.Dns.Applications
{
    public class DnsApplicationManagerUninstallRecoveryTests
    {
        [Fact]
        public async Task UninstallApplication_NeverLoadedApp_DeletesItsFolder()
        {
            string configFolder = TestDnsServerFactory.CreateTempConfigFolderPath();

            try
            {
                await using DnsServer dnsServer = TestDnsServerFactory.Create(configFolder);

                string appsFolder = Path.Combine(configFolder, "apps");
                string neverLoadedAppFolder = Path.Combine(appsFolder, "NeverLoadedApp");
                Directory.CreateDirectory(neverLoadedAppFolder);
                File.WriteAllText(Path.Combine(neverLoadedAppFolder, "dnsApp.config"), "some persisted policy");

                Assert.False(dnsServer.DnsApplicationManager.Applications.ContainsKey("NeverLoadedApp"));
                Assert.True(Directory.Exists(neverLoadedAppFolder));

                dnsServer.DnsApplicationManager.UninstallApplication("NeverLoadedApp");

                Assert.False(Directory.Exists(neverLoadedAppFolder));
            }
            finally
            {
                if (Directory.Exists(configFolder))
                    Directory.Delete(configFolder, true);
            }
        }
    }
}
