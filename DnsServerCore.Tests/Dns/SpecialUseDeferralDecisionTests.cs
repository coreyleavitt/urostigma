/*
RFC-010 slice 8 (quirk 2c): the ordering change. Special-use names (localhost,
home.arpa, resolver.arpa, service.arpa, test, invalid, local, onion, and their
subdomains) currently answer straight out of SpecialZoneManager before the
blocking pass ever runs. specialUseNamesDeferToBlocking flips that ordering --
but naively "skip the shortcut and let blocking run" would leak the recursion
outcome for a vetoed-with-no-response app decision, which is exactly the
RFC-1918/localhost exposure this quirk exists to close.

SpecialUseDeferralDecision is the extracted, DnsServer-free unit that performs
the tri-state collapse: not-deferring, deferring-and-allowed, and
deferring-and-vetoed-with-a-response all resolve immediately; only
deferring-and-vetoed-with-NO-response is the dangerous leg, and it still
resolves to the synthetic answer, never to a recursion outcome (recursion is
not even representable here -- the unit takes no recursion input at all).

These tests construct no DnsServer: isAllowedAsync/processBlockedQueryAsync
are plain test-controlled delegates, and syntheticResponse/blockedResponse are
distinct sentinel DnsDatagram instances distinguished by reference equality.
*/

using DnsServerCore.Dns;
using System;
using System.Threading.Tasks;
using TechnitiumLibrary.Net.Dns;
using Xunit;

namespace DnsServerCore.Tests.Dns
{
    public class SpecialUseDeferralDecisionTests
    {
        #region helpers

        static DnsDatagram Sentinel()
        {
            return new DnsDatagram(0, true, DnsOpcode.StandardQuery, true, false, true, true, false, false, DnsResponseCode.NoError, []);
        }

        static Func<Task<bool>> IsAllowed(bool allowed, out Func<bool> wasCalled)
        {
            bool called = false;
            wasCalled = () => called;

            return () =>
            {
                called = true;
                return Task.FromResult(allowed);
            };
        }

        static Func<Task<DnsDatagram>> ProcessBlockedQuery(DnsDatagram response, out Func<bool> wasCalled)
        {
            bool called = false;
            wasCalled = () => called;

            return () =>
            {
                called = true;
                return Task.FromResult(response);
            };
        }

        #endregion

        [Fact]
        public async Task DecideAsync_DeferringAndVetoedWithBlockResponse_ReturnsBlockResponse()
        {
            DnsDatagram syntheticResponse = Sentinel();
            DnsDatagram blockResponse = Sentinel();

            Func<Task<bool>> isAllowedAsync = IsAllowed(false, out Func<bool> isAllowedCalled);
            Func<Task<DnsDatagram>> processBlockedQueryAsync = ProcessBlockedQuery(blockResponse, out _);

            DnsDatagram result = await SpecialUseDeferralDecision.DecideAsync(
                specialUseNamesDeferToBlocking: true,
                deferEligible: true,
                locallyServedDnsZones: true,
                isForwardSpecialUseName: true,
                syntheticResponse: syntheticResponse,
                isAllowedAsync: isAllowedAsync,
                processBlockedQueryAsync: processBlockedQueryAsync);

            Assert.Same(blockResponse, result);
            Assert.True(isAllowedCalled());
        }

        [Fact]
        public async Task DecideAsync_DeferringAndAllowed_ReturnsSyntheticResponse_NeverConsultsBlockedQuery()
        {
            DnsDatagram syntheticResponse = Sentinel();

            Func<Task<bool>> isAllowedAsync = IsAllowed(true, out Func<bool> isAllowedCalled);
            Func<Task<DnsDatagram>> processBlockedQueryAsync = ProcessBlockedQuery(Sentinel(), out Func<bool> processBlockedQueryCalled);

            DnsDatagram result = await SpecialUseDeferralDecision.DecideAsync(
                specialUseNamesDeferToBlocking: true,
                deferEligible: true,
                locallyServedDnsZones: true,
                isForwardSpecialUseName: true,
                syntheticResponse: syntheticResponse,
                isAllowedAsync: isAllowedAsync,
                processBlockedQueryAsync: processBlockedQueryAsync);

            Assert.Same(syntheticResponse, result);
            Assert.True(isAllowedCalled());
            Assert.False(processBlockedQueryCalled()); //no recursion path exists to leak into either -- this delegate is the closest observable stand-in
        }

        [Fact]
        public async Task DecideAsync_DeferringAndVetoedWithNullBlockResponse_ReturnsSyntheticResponse()
        {
            //the dangerous leg: IsAllowedAsync returned false (an app vetoed) but
            //ProcessBlockedQueryAsync produced no block response -- the recursive path would fall
            //through here today; this unit must not, since recursion is not representable at all
            DnsDatagram syntheticResponse = Sentinel();

            Func<Task<bool>> isAllowedAsync = IsAllowed(false, out Func<bool> isAllowedCalled);
            Func<Task<DnsDatagram>> processBlockedQueryAsync = ProcessBlockedQuery(null!, out Func<bool> processBlockedQueryCalled); //an app vetoed without producing a block response

            DnsDatagram result = await SpecialUseDeferralDecision.DecideAsync(
                specialUseNamesDeferToBlocking: true,
                deferEligible: true,
                locallyServedDnsZones: true,
                isForwardSpecialUseName: true,
                syntheticResponse: syntheticResponse,
                isAllowedAsync: isAllowedAsync,
                processBlockedQueryAsync: processBlockedQueryAsync);

            Assert.Same(syntheticResponse, result);
            Assert.True(isAllowedCalled());
            Assert.True(processBlockedQueryCalled());
        }

        [Theory]
        //LocallyServedDnsZones=false: special-zone interception itself is off, so deferral is moot
        //regardless of the setting -- identical behavior whether specialUseNamesDeferToBlocking is
        //on or off (the interaction Design states explicitly)
        [InlineData(false, false, true, true)]
        [InlineData(false, true, true, true)]
        //pinned: setting off keeps today's behavior unconditionally, even when otherwise eligible
        [InlineData(true, false, true, false)]
        //pinned: deferEligible off (every internal caller) keeps today's behavior unconditionally
        [InlineData(true, true, false, false)]
        //reverse zones (and anything outside the eight forward names) are never eligible to defer
        [InlineData(true, true, true, false)]
        public async Task DecideAsync_NotDeferring_ReturnsSyntheticResponse_WithoutConsultingIsAllowed(bool specialUseNamesDeferToBlocking, bool deferEligible, bool locallyServedDnsZones, bool isForwardSpecialUseName)
        {
            DnsDatagram syntheticResponse = Sentinel();

            Func<Task<bool>> isAllowedAsync = IsAllowed(true, out Func<bool> isAllowedCalled);
            Func<Task<DnsDatagram>> processBlockedQueryAsync = ProcessBlockedQuery(Sentinel(), out Func<bool> processBlockedQueryCalled);

            DnsDatagram result = await SpecialUseDeferralDecision.DecideAsync(
                specialUseNamesDeferToBlocking: specialUseNamesDeferToBlocking,
                deferEligible: deferEligible,
                locallyServedDnsZones: locallyServedDnsZones,
                isForwardSpecialUseName: isForwardSpecialUseName,
                syntheticResponse: syntheticResponse,
                isAllowedAsync: isAllowedAsync,
                processBlockedQueryAsync: processBlockedQueryAsync);

            Assert.Same(syntheticResponse, result);
            Assert.False(isAllowedCalled());
            Assert.False(processBlockedQueryCalled());
        }
    }
}
