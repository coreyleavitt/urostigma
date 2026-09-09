/*
RFC-010 slice 5, quirk 1 (partial-load surfacing): WriteAppAsJson is the one
helper shared by ListInstalledAppsAsync, InstallAppAsync/DownloadAndInstallAppAsync,
and UpdateAppAsync/DownloadAndUpdateAppAsync, so it is where a "loadWarnings"
array must appear whenever DnsApplication.LoadWarnings is non-empty. Round-3
seam correction from the RFC: this was a private instance method with one
instance dependency (the store version, DnsWebService._currentVersion) --
"make it internal" alone exposes nothing, since constructing the enclosing
DnsWebService is not something a unit test should have to do. It becomes
internal static with the version lifted to a parameter.

These tests call it directly -- no HttpContext, no DnsWebService -- and parse
the real Utf8JsonWriter output back with JsonDocument, the same style
DnsAppApiDispatcherTests already uses for constructing DnsApplication against
a fake IDnsServer.
*/

using DnsServerCore.ApplicationCommon;
using DnsServerCore.Dns.Applications;
using DnsServerCore.Tests.Fixtures;
using NSubstitute;
using System;
using System.Text.Json;
using Xunit;

namespace DnsServerCore.Tests
{
    public class WebServiceAppsApiWriteAppAsJsonTests : IClassFixture<PoisonedAppFixture>, IClassFixture<SingleApiHandlerAppFixture>
    {
        readonly PoisonedAppFixture _partialLoadFixture;
        readonly SingleApiHandlerAppFixture _healthyFixture;

        public WebServiceAppsApiWriteAppAsJsonTests(PoisonedAppFixture partialLoadFixture, SingleApiHandlerAppFixture healthyFixture)
        {
            _partialLoadFixture = partialLoadFixture;
            _healthyFixture = healthyFixture;
        }

        static IDnsServer FakeDnsServer(string applicationFolder)
        {
            IDnsServer dnsServer = Substitute.For<IDnsServer>();
            dnsServer.ApplicationFolder.Returns(applicationFolder);
            return dnsServer;
        }

        static JsonElement WriteAndParse(DnsApplication application)
        {
            using System.IO.MemoryStream stream = new System.IO.MemoryStream();

            using (Utf8JsonWriter jsonWriter = new Utf8JsonWriter(stream))
            {
                DnsWebService.WebServiceAppsApi.WriteAppAsJson(jsonWriter, application, new Version(13, 0));
            }

            using JsonDocument document = JsonDocument.Parse(stream.ToArray());
            return document.RootElement.Clone();
        }

        [Fact]
        public void WriteAppAsJson_PartialLoadApp_IncludesLoadWarnings()
        {
            using DnsApplication application = new DnsApplication(FakeDnsServer(_partialLoadFixture.PoisonedApplicationFolder), "RfcRepro.App");

            JsonElement json = WriteAndParse(application);

            Assert.True(json.TryGetProperty("loadWarnings", out JsonElement loadWarnings));
            Assert.Equal(JsonValueKind.Array, loadWarnings.ValueKind);
            Assert.Equal(1, loadWarnings.GetArrayLength());

            JsonElement warning = loadWarnings[0];
            Assert.Equal(JsonValueKind.Null, warning.GetProperty("typeName").ValueKind);
            Assert.False(string.IsNullOrWhiteSpace(warning.GetProperty("message").GetString()));
        }

        [Fact]
        public void WriteAppAsJson_HealthyApp_OmitsLoadWarnings_AndShapeIsUnchanged()
        {
            using DnsApplication application = new DnsApplication(FakeDnsServer(_healthyFixture.ApplicationFolder), "RfcApiHandler.Single");

            JsonElement json = WriteAndParse(application);

            Assert.False(json.TryGetProperty("loadWarnings", out _));

            //regression: the existing shape is untouched
            Assert.Equal(application.Name, json.GetProperty("name").GetString());
            Assert.True(json.TryGetProperty("dnsApps", out JsonElement dnsApps));
            Assert.Equal(JsonValueKind.Array, dnsApps.ValueKind);
        }
    }
}
