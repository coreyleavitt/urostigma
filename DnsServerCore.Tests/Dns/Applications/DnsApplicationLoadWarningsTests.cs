/*
RFC-010 slice 5, quirk 1 (partial load): the constructor sweep in DnsApplication
must not stay silent when some types in an app assembly fail to load or fail to
construct -- it records a warning per failure in a build-then-freeze
IReadOnlyList<AppLoadWarning>, matching the class's existing convention for its
other collections (dictionaries assigned once in the constructor, exposed
read-only). Reuses the poisoned-app fixture from slice 2
(Fixtures\PoisonedApp\RfcRepro.App): AlphaApp and CharlieApp load cleanly,
BravoApp's base type lives in the deleted RfcRepro.Helper.dll and fails to load
via ReflectionTypeLoadException -- the "assembly-load-failure" branch, where
content honesty means the failed type's name is not recoverable (ex.Types holds
null at BravoApp's position; the loader exception names the missing dependency
assembly, not BravoApp itself).
*/

using DnsServerCore.ApplicationCommon;
using DnsServerCore.Dns.Applications;
using DnsServerCore.Tests.Fixtures;
using NSubstitute;
using Xunit;

namespace DnsServerCore.Tests.Dns.Applications
{
    public class DnsApplicationLoadWarningsTests : IClassFixture<PoisonedAppFixture>
    {
        readonly PoisonedAppFixture _fixture;

        public DnsApplicationLoadWarningsTests(PoisonedAppFixture fixture)
        {
            _fixture = fixture;
        }

        [Fact]
        public void Constructor_TypeSweep_2Of3PartialLoad_ProducesOneWarning_WithNoRecoverableTypeName()
        {
            IDnsServer dnsServer = Substitute.For<IDnsServer>();
            dnsServer.ApplicationFolder.Returns(_fixture.PoisonedApplicationFolder);

            using DnsApplication app = new DnsApplication(dnsServer, "RfcRepro.App");

            //two of three types registered; the missing one produced exactly one warning
            Assert.Equal(2, app.DnsApplications.Count);
            Assert.Single(app.LoadWarnings);

            AppLoadWarning warning = app.LoadWarnings[0];

            //content honesty: BravoApp's own name is not recoverable on this branch --
            //ex.Types holds null at its position, and the loader exception names the
            //missing dependency assembly (RfcRepro.Helper), not the type that referenced it
            Assert.Null(warning.TypeName);
            Assert.False(string.IsNullOrWhiteSpace(warning.Message));
        }
    }
}
