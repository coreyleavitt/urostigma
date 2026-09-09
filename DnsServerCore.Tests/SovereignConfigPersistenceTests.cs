/*
RFC-010 slice 7 (quirk 2b): the setting exists. specialUseNamesDeferToBlocking is a
fork-owned setting that must survive a restart without touching dns.config's
upstream-owned binary format (the file's single linear version byte belongs to
upstream, and a fork-persisted field claiming the next version number collides
with whatever upstream lands there next). It persists instead in a fork-owned
JSON sidecar, sovereign.config, in the same config folder, loaded/saved
independently of dns.config.

These tests exercise DnsServer.LoadSovereignConfig()/SaveSovereignConfig() and the
SpecialUseNamesDeferToBlocking property directly -- the same seam
DnsServer.LoadConfigFile() and the settings API save path call through -- without
going through the heavier dns.config load/save flow that LoadConfigFile() also
performs.
*/

using System;
using System.IO;
using System.Threading.Tasks;
using DnsServerCore.Dns;
using DnsServerCore.Tests.Fixtures;
using Xunit;

namespace DnsServerCore.Tests
{
    public class SovereignConfigPersistenceTests
    {
        [Fact]
        public async Task SpecialUseNamesDeferToBlocking_RoundTrips_AcrossInstances()
        {
            string configFolder = TestDnsServerFactory.CreateTempConfigFolderPath();

            try
            {
                await using (DnsServer dnsServer1 = TestDnsServerFactory.Create(configFolder))
                {
                    dnsServer1.SpecialUseNamesDeferToBlocking = true;
                    dnsServer1.SaveSovereignConfig();
                }

                await using DnsServer dnsServer2 = TestDnsServerFactory.Create(configFolder);
                dnsServer2.LoadSovereignConfig();

                Assert.True(dnsServer2.SpecialUseNamesDeferToBlocking);
            }
            finally
            {
                if (Directory.Exists(configFolder))
                    Directory.Delete(configFolder, true);
            }
        }

        [Fact]
        public async Task SpecialUseNamesDeferToBlocking_MissingSidecar_DefaultsFalse()
        {
            string configFolder = TestDnsServerFactory.CreateTempConfigFolderPath();

            try
            {
                await using DnsServer dnsServer = TestDnsServerFactory.Create(configFolder);

                dnsServer.LoadSovereignConfig();

                Assert.False(dnsServer.SpecialUseNamesDeferToBlocking);
            }
            finally
            {
                if (Directory.Exists(configFolder))
                    Directory.Delete(configFolder, true);
            }
        }

        [Theory]
        [InlineData("")]
        [InlineData("{ not valid json")]
        [InlineData("null")]
        public async Task SpecialUseNamesDeferToBlocking_CorruptOrEmptySidecar_DefaultsFalseNoCrash(string sidecarContents)
        {
            string configFolder = TestDnsServerFactory.CreateTempConfigFolderPath();

            try
            {
                Directory.CreateDirectory(configFolder);
                File.WriteAllText(Path.Combine(configFolder, "sovereign.config"), sidecarContents);

                await using DnsServer dnsServer = TestDnsServerFactory.Create(configFolder);

                dnsServer.LoadSovereignConfig();

                Assert.False(dnsServer.SpecialUseNamesDeferToBlocking);
            }
            finally
            {
                if (Directory.Exists(configFolder))
                    Directory.Delete(configFolder, true);
            }
        }
    }
}
