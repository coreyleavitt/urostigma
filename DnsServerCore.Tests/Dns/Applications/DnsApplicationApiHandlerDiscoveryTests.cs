/*
RFC-010 slice 3: folds 0002 (IDnsApplicationApiHandler, corrected copyright
header) and 0003's DnsApplication.cs discovery hunk -- the handler list, the
`is IDnsApplicationApiHandler` check in the constructor type sweep, and the
DnsApplicationApiHandlerAmbiguous property. Exercises DnsApplication's own
production constructor sweep against a fake IDnsServer, same pattern as
DnsApplicationIsolationTests (slice 2).
*/

using DnsServerCore.ApplicationCommon;
using DnsServerCore.Dns.Applications;
using DnsServerCore.Tests.Fixtures;
using NSubstitute;
using Xunit;

namespace DnsServerCore.Tests.Dns.Applications
{
    public class DnsApplicationApiHandlerDiscoveryTests :
        IClassFixture<SingleApiHandlerAppFixture>,
        IClassFixture<AmbiguousApiHandlerAppFixture>
    {
        readonly SingleApiHandlerAppFixture _singleFixture;
        readonly AmbiguousApiHandlerAppFixture _ambiguousFixture;

        public DnsApplicationApiHandlerDiscoveryTests(SingleApiHandlerAppFixture singleFixture, AmbiguousApiHandlerAppFixture ambiguousFixture)
        {
            _singleFixture = singleFixture;
            _ambiguousFixture = ambiguousFixture;
        }

        [Fact]
        public void Constructor_TypeSweep_DiscoversIDnsApplicationApiHandler()
        {
            IDnsServer dnsServer = Substitute.For<IDnsServer>();
            dnsServer.ApplicationFolder.Returns(_singleFixture.ApplicationFolder);

            using DnsApplication app = new DnsApplication(dnsServer, "RfcApiHandler.Single");

            Assert.NotNull(app.DnsApplicationApiHandler);
            Assert.IsAssignableFrom<IDnsApplicationApiHandler>(app.DnsApplicationApiHandler);
            Assert.False(app.DnsApplicationApiHandlerAmbiguous,
                "Exactly one IDnsApplicationApiHandler implementor is registered; the ambiguity property must not report ambiguity.");
        }

        [Fact]
        public void Constructor_TypeSweep_TwoHandlerImplementors_SurfacesAmbiguity_WithoutThrowing()
        {
            IDnsServer dnsServer = Substitute.For<IDnsServer>();
            dnsServer.ApplicationFolder.Returns(_ambiguousFixture.ApplicationFolder);

            //discovery itself must not throw when an app registers more than one
            //IDnsApplicationApiHandler implementor
            using DnsApplication app = new DnsApplication(dnsServer, "RfcApiHandler.Ambiguous");

            Assert.True(app.DnsApplicationApiHandlerAmbiguous,
                "Two IDnsApplicationApiHandler implementors are registered; the ambiguity property must report it.");
            Assert.Null(app.DnsApplicationApiHandler);

            //both handler-implementing app types still register as ordinary DNS apps --
            //ambiguity is a dispatch-time concern the sweep merely surfaces, not a load failure
            Assert.True(app.DnsApplications.ContainsKey("RfcApiHandler.Ambiguous.HandlerOne"));
            Assert.True(app.DnsApplications.ContainsKey("RfcApiHandler.Ambiguous.HandlerTwo"));
        }
    }
}
