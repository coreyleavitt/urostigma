/*
RFC-010 slice 2: RED before patch 0001, GREEN after. Loads the poisoned-app
fixture (Fixtures\PoisonedApp, staged by PoisonedAppFixture) through
DnsApplication's own production constructor sweep against a fake IDnsServer
-- no full server, no Kestrel, no separately-built AssemblyLoadContext.
Assertions key off registration outcomes (which types registered, which were
skipped), never exception types: the unpatched sweep drops the whole
assembly (ExportedTypes throws and aborts the enclosing foreach), the patched
sweep isolates the one unloadable type (GetTypes() reports partial results
via ReflectionTypeLoadException).
*/

using DnsServerCore.ApplicationCommon;
using DnsServerCore.Dns.Applications;
using DnsServerCore.Tests.Fixtures;
using NSubstitute;
using Xunit;

namespace DnsServerCore.Tests.Dns.Applications
{
    public class DnsApplicationIsolationTests : IClassFixture<PoisonedAppFixture>
    {
        readonly PoisonedAppFixture _fixture;

        public DnsApplicationIsolationTests(PoisonedAppFixture fixture)
        {
            _fixture = fixture;
        }

        [Fact]
        public void Constructor_TypeSweep_RegistersLoadableTypes_SkipsTypeWithMissingBase()
        {
            IDnsServer dnsServer = Substitute.For<IDnsServer>();
            dnsServer.ApplicationFolder.Returns(_fixture.PoisonedApplicationFolder);

            using DnsApplication app = new DnsApplication(dnsServer, "RfcRepro.App");

            Assert.True(app.DnsApplications.ContainsKey("RfcRepro.App.AlphaApp"),
                "AlphaApp has no broken dependency and must register.");

            Assert.True(app.DnsApplications.ContainsKey("RfcRepro.App.CharlieApp"),
                "CharlieApp has no broken dependency and must register, even though it is " +
                "declared after BravoApp: one bad type must not abort discovery of the rest " +
                "of the assembly.");

            Assert.False(app.DnsApplications.ContainsKey("RfcRepro.App.BravoApp"),
                "BravoApp's base type lives in RfcRepro.Helper.dll, which the fixture deletes " +
                "from the staged application folder; BravoApp cannot load and must be skipped.");

            Assert.Equal(2, app.DnsApplications.Count);
        }
    }
}
