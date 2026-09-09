/*
RFC-010 slice 8 (quirk 2c): call-site wiring for SpecialUseDeferralDecision.
DnsServer.AuthoritativeQueryAsync's special-zone branch is a thin adapter around the
decision unit (SpecialUseDeferralDecisionTests.cs covers the decision itself, with no
DnsServer involved). This test proves the wiring end-to-end through a real, never-started
DnsServer (TestDnsServerFactory, ~150ms, no sockets): a blocked special-use name reaches
AuthoritativeQueryAsync's caller as the real blocking response -- not SpecialZoneManager's
synthetic answer -- with its Blocked tag intact rather than overwritten to Authoritative.

What this does NOT cover: the "un-reprocessed" half of the terminal-blocked-response
property (that the blocked response bypasses ProcessAuthoritativeQueryAsync's CNAME/ANAME
reprocess loop) lives in ProcessAuthoritativeQueryAsync, which is private and reachable
only through the request pipeline (ProcessRequestAsync and below), which needs bound
sockets/HTTP context that TestDnsServerFactory deliberately does not start. That half is
verified by build + inspection only (DnsServer.cs's ProcessAuthoritativeQueryAsync checks
response.Tag is DnsServerResponseType.Blocked and returns immediately, before entering the
reprocess loop) -- documented honestly rather than faked with a test that cannot reach it.
*/

using DnsServerCore.ApplicationCommon;
using DnsServerCore.Dns;
using DnsServerCore.Tests.Fixtures;
using System.IO;
using System.Net;
using System.Threading.Tasks;
using TechnitiumLibrary.Net.Dns;
using TechnitiumLibrary.Net.Dns.ResourceRecords;
using Xunit;

namespace DnsServerCore.Tests.Dns
{
    public class SpecialUseDeferralWiringTests
    {
        [Fact]
        public async Task AuthoritativeQueryAsync_BlockedSpecialUseName_ReturnsBlockedResponse_NotSyntheticAnswer()
        {
            string configFolder = TestDnsServerFactory.CreateTempConfigFolderPath();

            try
            {
                await using DnsServer dnsServer = TestDnsServerFactory.Create(configFolder);

                dnsServer.SpecialUseNamesDeferToBlocking = true; //quirk 2c setting under test
                dnsServer.EnableBlocking = true;
                Assert.True(dnsServer.BlockedZoneManager.BlockZone("localhost")); //"localhost" is one of the eight forward special-use names

                DnsQuestionRecord question = new DnsQuestionRecord("localhost", DnsResourceRecordType.A, DnsClass.IN);
                DnsDatagram request = new DnsDatagram(1, false, DnsOpcode.StandardQuery, false, false, true, false, false, false, DnsResponseCode.NoError, [question]);
                IPEndPoint remoteEP = new IPEndPoint(IPAddress.Parse("203.0.113.5"), 53123); //a real client endpoint, not the ANY_0 sentinel

                DnsDatagram response = await dnsServer.AuthoritativeQueryAsync(request, DnsTransportProtocol.Udp, true, true, true, remoteEP); //deferEligible: true

                Assert.NotNull(response);
                Assert.Equal(DnsServerResponseType.Blocked, response.Tag); //not overwritten to Authoritative -- that stamp applies to the synthetic branch only
                Assert.DoesNotContain(response.Answer, record => (record.Type == DnsResourceRecordType.A) && (record.RDATA is DnsARecordData aRecord) && aRecord.Address.Equals(IPAddress.Loopback)); //never SpecialZoneManager's synthetic 127.0.0.1 answer
            }
            finally
            {
                if (Directory.Exists(configFolder))
                    Directory.Delete(configFolder, true);
            }
        }

        [Fact]
        public async Task AuthoritativeQueryAsync_BlockedSpecialUseName_SettingDisabled_ReturnsSyntheticAnswer_NotBlockedResponse()
        {
            //same setup as above except SpecialUseNamesDeferToBlocking stays false (today's default):
            //proves the wiring, not a harness artifact -- the blocking pass must NOT run at all
            string configFolder = TestDnsServerFactory.CreateTempConfigFolderPath();

            try
            {
                await using DnsServer dnsServer = TestDnsServerFactory.Create(configFolder);

                dnsServer.EnableBlocking = true;
                Assert.True(dnsServer.BlockedZoneManager.BlockZone("localhost"));

                DnsQuestionRecord question = new DnsQuestionRecord("localhost", DnsResourceRecordType.A, DnsClass.IN);
                DnsDatagram request = new DnsDatagram(1, false, DnsOpcode.StandardQuery, false, false, true, false, false, false, DnsResponseCode.NoError, [question]);
                IPEndPoint remoteEP = new IPEndPoint(IPAddress.Parse("203.0.113.5"), 53123);

                DnsDatagram response = await dnsServer.AuthoritativeQueryAsync(request, DnsTransportProtocol.Udp, true, true, true, remoteEP); //deferEligible: true, but the setting is off

                Assert.NotNull(response);
                Assert.Equal(DnsServerResponseType.Authoritative, response.Tag);
                Assert.Contains(response.Answer, record => (record.Type == DnsResourceRecordType.A) && (record.RDATA is DnsARecordData aRecord) && aRecord.Address.Equals(IPAddress.Loopback)); //SpecialZoneManager's synthetic answer, blocking pass never consulted
            }
            finally
            {
                if (Directory.Exists(configFolder))
                    Directory.Delete(configFolder, true);
            }
        }
    }
}
