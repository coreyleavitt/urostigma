/*
RFC-010 slice 4: DnsAppApiDispatcher is a static, dispatcher-pure unit -- no
HttpContext, no DnsWebService, no Kestrel -- so these tests construct no web
service at all. Real DnsApplication instances (via IClassFixture-published
apps, the same pattern slice 3 established) stand in for the live
DnsApplicationManager.Applications table; permissionCheck and bodyReader are
plain delegates the test controls directly, which is what makes the
denied-never-reads-body security property (Outcome.AccessDenied AND
bodyReader never invoked) testable at this level at all.
*/

using DnsServerCore.ApplicationCommon;
using DnsServerCore.Dns.Applications;
using DnsServerCore.Tests.Fixtures;
using NSubstitute;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace DnsServerCore.Tests.Dns.Applications
{
    public class DnsAppApiDispatcherTests :
        IClassFixture<SingleApiHandlerAppFixture>,
        IClassFixture<AmbiguousApiHandlerAppFixture>,
        IClassFixture<PoisonedAppFixture>,
        IClassFixture<ScriptedApiHandlerAppFixture>
    {
        readonly SingleApiHandlerAppFixture _singleFixture;
        readonly AmbiguousApiHandlerAppFixture _ambiguousFixture;
        readonly PoisonedAppFixture _noHandlerFixture;
        readonly ScriptedApiHandlerAppFixture _scriptedFixture;

        public DnsAppApiDispatcherTests(SingleApiHandlerAppFixture singleFixture, AmbiguousApiHandlerAppFixture ambiguousFixture, PoisonedAppFixture noHandlerFixture, ScriptedApiHandlerAppFixture scriptedFixture)
        {
            _singleFixture = singleFixture;
            _ambiguousFixture = ambiguousFixture;
            _noHandlerFixture = noHandlerFixture;
            _scriptedFixture = scriptedFixture;
        }

        #region helpers

        static IDnsServer FakeDnsServer(string applicationFolder)
        {
            IDnsServer dnsServer = Substitute.For<IDnsServer>();
            dnsServer.ApplicationFolder.Returns(applicationFolder);
            return dnsServer;
        }

        static DnsAppApiDispatcher.RequestMetadata Metadata(string appName, string path = "", string method = "GET")
        {
            return new DnsAppApiDispatcher.RequestMetadata(appName, method, path, new Dictionary<string, string>(), "", "testuser");
        }

        static Func<CancellationToken, Task<byte[]>> RecordingBodyReader(byte[] body, out Func<bool> wasInvoked)
        {
            bool invoked = false;
            wasInvoked = () => invoked;

            return ct =>
            {
                invoked = true;
                return Task.FromResult(body);
            };
        }

        #endregion

        [Fact]
        public async Task DispatchAsync_AppMissing_ReturnsNotFound()
        {
            Dictionary<string, DnsApplication> apps = new Dictionary<string, DnsApplication>();

            DnsAppApiDispatcher.Outcome outcome = await DnsAppApiDispatcher.DispatchAsync(
                apps,
                Metadata("no-such-app"),
                access => true,
                ct => Task.FromResult(Array.Empty<byte>()),
                CancellationToken.None);

            Assert.Equal(DnsAppApiDispatcher.OutcomeKind.NotFound, outcome.Kind);
        }

        [Fact]
        public async Task DispatchAsync_AppPresentWithNoHandler_ReturnsNotFound()
        {
            using DnsApplication application = new DnsApplication(FakeDnsServer(_noHandlerFixture.PoisonedApplicationFolder), "RfcRepro.App");

            Assert.Null(application.DnsApplicationApiHandler);
            Assert.False(application.DnsApplicationApiHandlerAmbiguous);

            Dictionary<string, DnsApplication> apps = new Dictionary<string, DnsApplication> { { "RfcRepro.App", application } };

            DnsAppApiDispatcher.Outcome outcome = await DnsAppApiDispatcher.DispatchAsync(
                apps,
                Metadata("RfcRepro.App"),
                access => true,
                ct => Task.FromResult(Array.Empty<byte>()),
                CancellationToken.None);

            Assert.Equal(DnsAppApiDispatcher.OutcomeKind.NotFound, outcome.Kind);
        }

        [Fact]
        public async Task DispatchAsync_AmbiguousHandlers_ReturnsAmbiguous()
        {
            using DnsApplication application = new DnsApplication(FakeDnsServer(_ambiguousFixture.ApplicationFolder), "RfcApiHandler.Ambiguous");

            Assert.True(application.DnsApplicationApiHandlerAmbiguous);

            Dictionary<string, DnsApplication> apps = new Dictionary<string, DnsApplication> { { "RfcApiHandler.Ambiguous", application } };

            DnsAppApiDispatcher.Outcome outcome = await DnsAppApiDispatcher.DispatchAsync(
                apps,
                Metadata("RfcApiHandler.Ambiguous"),
                access => true,
                ct => Task.FromResult(Array.Empty<byte>()),
                CancellationToken.None);

            Assert.Equal(DnsAppApiDispatcher.OutcomeKind.Ambiguous, outcome.Kind);
        }

        [Fact]
        public async Task DispatchAsync_PermissionDenied_ReturnsAccessDenied_AndNeverInvokesBodyReader()
        {
            using DnsApplication application = new DnsApplication(FakeDnsServer(_singleFixture.ApplicationFolder), "RfcApiHandler.Single");

            Dictionary<string, DnsApplication> apps = new Dictionary<string, DnsApplication> { { "RfcApiHandler.Single", application } };

            Func<CancellationToken, Task<byte[]>> bodyReader = RecordingBodyReader(new byte[] { 1, 2, 3 }, out Func<bool> wasInvoked);

            DnsAppApiDispatcher.Outcome outcome = await DnsAppApiDispatcher.DispatchAsync(
                apps,
                Metadata("RfcApiHandler.Single"),
                access => false,
                bodyReader,
                CancellationToken.None);

            Assert.Equal(DnsAppApiDispatcher.OutcomeKind.AccessDenied, outcome.Kind);
            Assert.False(wasInvoked(), "A denied request must never buffer the body: bodyReader must not be invoked.");
        }

        [Fact]
        public async Task DispatchAsync_Permitted_InvokesHandlerWithBody_AndReturnsSuccessWithItsResponse()
        {
            using DnsApplication application = new DnsApplication(FakeDnsServer(_scriptedFixture.ApplicationFolder), "RfcApiHandler.Scripted");

            Dictionary<string, DnsApplication> apps = new Dictionary<string, DnsApplication> { { "RfcApiHandler.Scripted", application } };

            byte[] requestBody = new byte[] { 9, 8, 7, 6 };
            Func<CancellationToken, Task<byte[]>> bodyReader = RecordingBodyReader(requestBody, out Func<bool> wasInvoked);

            //ScriptedHandlerApp echoes the request body back verbatim for any path other than the
            //scripted throw paths -- see ScriptedHandlerApp for the path vocabulary
            DnsAppApiDispatcher.Outcome outcome = await DnsAppApiDispatcher.DispatchAsync(
                apps,
                Metadata("RfcApiHandler.Scripted", path: "echo"),
                access => true,
                bodyReader,
                CancellationToken.None);

            Assert.True(wasInvoked());
            Assert.Equal(DnsAppApiDispatcher.OutcomeKind.Success, outcome.Kind);
            Assert.NotNull(outcome.Response);
            Assert.Equal(200, outcome.Response.StatusCode);
            Assert.Equal(requestBody, outcome.Response.Body);
        }

        [Fact]
        public async Task DispatchAsync_HandlerThrowsDuringInvoke_ReturnsHandlerThrew()
        {
            using DnsApplication application = new DnsApplication(FakeDnsServer(_scriptedFixture.ApplicationFolder), "RfcApiHandler.Scripted");

            Dictionary<string, DnsApplication> apps = new Dictionary<string, DnsApplication> { { "RfcApiHandler.Scripted", application } };

            //ScriptedHandlerApp.HandleApiRequestAsync throws for exactly this path -- see
            //ScriptedHandlerApp for the path vocabulary
            DnsAppApiDispatcher.Outcome outcome = await DnsAppApiDispatcher.DispatchAsync(
                apps,
                Metadata("RfcApiHandler.Scripted", path: "throw-handle"),
                access => true,
                ct => Task.FromResult(Array.Empty<byte>()),
                CancellationToken.None);

            Assert.Equal(DnsAppApiDispatcher.OutcomeKind.HandlerThrew, outcome.Kind);
            Assert.NotNull(outcome.Exception);
        }

        [Fact]
        public async Task DispatchAsync_GetRequiredAccessThrows_ReturnsHandlerThrew_AndNeverInvokesBodyReader()
        {
            using DnsApplication application = new DnsApplication(FakeDnsServer(_scriptedFixture.ApplicationFolder), "RfcApiHandler.Scripted");

            Dictionary<string, DnsApplication> apps = new Dictionary<string, DnsApplication> { { "RfcApiHandler.Scripted", application } };

            Func<CancellationToken, Task<byte[]>> bodyReader = RecordingBodyReader(Array.Empty<byte>(), out Func<bool> wasInvoked);

            //ScriptedHandlerApp.GetRequiredAccess throws for exactly this path -- see
            //ScriptedHandlerApp for the path vocabulary
            DnsAppApiDispatcher.Outcome outcome = await DnsAppApiDispatcher.DispatchAsync(
                apps,
                Metadata("RfcApiHandler.Scripted", path: "throw-authorize"),
                access => true,
                bodyReader,
                CancellationToken.None);

            Assert.Equal(DnsAppApiDispatcher.OutcomeKind.HandlerThrew, outcome.Kind);
            Assert.NotNull(outcome.Exception);
            Assert.False(wasInvoked(), "GetRequiredAccess throwing must classify as HandlerThrew without ever reading the body.");
        }
    }
}
