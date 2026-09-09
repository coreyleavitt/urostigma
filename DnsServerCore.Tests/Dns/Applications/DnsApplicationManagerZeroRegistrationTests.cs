/*
RFC-010 slice 5, quirk 1 (zero-registration): today DnsApplicationManager's
private LoadApplicationAsync builds a DnsApplication, awaits its
InitializeAsync, and adds it to the loaded table without ever checking whether
the sweep actually registered anything -- an app whose assembly registers zero
IDnsApplication implementors (the fixture here: RfcZero.App, see
Fixtures\ZeroRegistrationApp) loads "successfully" and sits in the table doing
nothing. The fix adds a check right after construction: if zero types
registered, dispose the freshly built DnsApplication first, then throw, so the
install/update API surfaces status:"error" through the existing exception
envelope instead of silently succeeding.

Driven through DnsApplicationManager.InstallApplicationAsync -- the real
production entry point install/update quirk 1 is about -- rather than calling
the private LoadApplicationAsync directly (see TestDnsServerFactory for why a
real DnsServer is the smallest harness that reaches it).
*/

using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Runtime.Loader;
using System.Threading.Tasks;
using DnsServerCore.Dns;
using DnsServerCore.Dns.Applications;
using DnsServerCore.Tests.Fixtures;
using Xunit;

namespace DnsServerCore.Tests.Dns.Applications
{
    public class DnsApplicationManagerZeroRegistrationTests : IClassFixture<ZeroRegistrationAppFixture>
    {
        readonly ZeroRegistrationAppFixture _fixture;

        public DnsApplicationManagerZeroRegistrationTests(ZeroRegistrationAppFixture fixture)
        {
            _fixture = fixture;
        }

        static string BuildAppZip(string applicationFolder)
        {
            string zipPath = Path.Combine(Path.GetTempPath(), "urostigma-zero-reg-zip-" + Guid.NewGuid().ToString("N") + ".zip");
            ZipFile.CreateFromDirectory(applicationFolder, zipPath, CompressionLevel.NoCompression, false);
            return zipPath;
        }

        [Fact]
        public async Task InstallApplicationAsync_ZeroRegistrationApp_Throws_NotAdded_AndAssemblyLoadContextIsCollected()
        {
            string configFolder = TestDnsServerFactory.CreateTempConfigFolderPath();
            string zipPath = BuildAppZip(_fixture.ApplicationFolder);

            try
            {
                await using DnsServer dnsServer = TestDnsServerFactory.Create(configFolder);

                int collectibleAlcCountBefore = AssemblyLoadContext.All.Count(alc => alc.IsCollectible);

                await using (FileStream zipStream = new FileStream(zipPath, FileMode.Open, FileAccess.Read))
                {
                    DnsServerException ex = await Assert.ThrowsAsync<DnsServerException>(
                        () => dnsServer.DnsApplicationManager.InstallApplicationAsync("ZeroRegApp", zipStream));

                    Assert.Contains("ZeroRegApp", ex.Message, StringComparison.Ordinal);
                }

                //not left behind in the loaded table
                Assert.False(dnsServer.DnsApplicationManager.Applications.ContainsKey("ZeroRegApp"));

                //the collectible AssemblyLoadContext created for the failed load must not leak:
                //dispose-then-throw means DnsApplication.Dispose() ran (which unloads its ALC)
                //before LoadApplicationAsync threw. AssemblyLoadContext.Unload() only requests
                //collection, so give the GC a few passes to actually reclaim it.
                int collectibleAlcCountAfter = -1;

                for (int i = 0; i < 10; i++)
                {
                    GC.Collect();
                    GC.WaitForPendingFinalizers();

                    collectibleAlcCountAfter = AssemblyLoadContext.All.Count(alc => alc.IsCollectible);
                    if (collectibleAlcCountAfter == collectibleAlcCountBefore)
                        break;
                }

                Assert.Equal(collectibleAlcCountBefore, collectibleAlcCountAfter);
            }
            finally
            {
                if (File.Exists(zipPath))
                    File.Delete(zipPath);

                if (Directory.Exists(configFolder))
                    Directory.Delete(configFolder, true);
            }
        }
    }
}
