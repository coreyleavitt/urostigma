/*
Urostigma DNS Server
Copyright (C) 2026  Corey Leavitt

This program is free software: you can redistribute it and/or modify
it under the terms of the GNU General Public License as published by
the Free Software Foundation, either version 3 of the License, or
(at your option) any later version.

This program is distributed in the hope that it will be useful,
but WITHOUT ANY WARRANTY; without even the implied warranty of
MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
GNU General Public License for more details.

You should have received a copy of the GNU General Public License
along with this program.  If not, see <http://www.gnu.org/licenses/>.

*/

using System;
using System.Threading.Tasks;
using TechnitiumLibrary.Net.Dns;

namespace DnsServerCore.Dns
{
    /// <summary>
    /// The RFC-010 quirk-2c ordering decision: whether a special-use-name query
    /// (<see cref="ZoneManagers.SpecialZoneManager.IsForwardSpecialUseName"/>) is answered by
    /// <c>SpecialZoneManager</c>'s synthetic response or deferred to the blocking pass first.
    /// Static and <c>DnsServer</c>-free -- the two collaborators the real blocking pass needs
    /// (<c>IsAllowedAsync</c>, <c>ProcessBlockedQueryAsync</c>) are delegates, so this unit is
    /// testable without constructing a <c>DnsServer</c>.
    /// </summary>
    /// <remarks>
    /// The load-bearing invariant: a special-use query never reaches recursion. This method
    /// performs the tri-state collapse itself rather than exposing <paramref name="isAllowedAsync"/>
    /// and <paramref name="processBlockedQueryAsync"/>'s outcomes to the caller pre-collapsed --
    /// a single pre-collapsed thunk would make the vetoed-with-null-response leg (allowed=false,
    /// block response=null) indistinguishable from "allowed", silently hiding the one case a naive
    /// "skip the shortcut" implementation gets wrong: an app that vetoes without producing a block
    /// response must still get the synthetic answer, never whatever <c>ProcessRecursiveQueryAsync</c>
    /// would have done with the same tri-state. Recursion is not merely avoided here -- it has no
    /// representation in this method's inputs at all.
    /// </remarks>
    internal static class SpecialUseDeferralDecision
    {
        #region public

        /// <summary>
        /// Decides the response for a special-use-name query already known to have a synthetic
        /// answer from <c>SpecialZoneManager</c>.
        /// </summary>
        /// <param name="specialUseNamesDeferToBlocking"><c>DnsServer.SpecialUseNamesDeferToBlocking</c>.</param>
        /// <param name="deferEligible">
        /// True only for the top-level client query path (the UDP/TCP/QUIC/DoH listener entry
        /// points, including the DoH real-IP fallback -- those are still genuine clients). Every
        /// internal caller (CNAME/ANAME chase re-queries, cache refresh, <c>ResolverDnsCache</c>,
        /// <c>DirectQueryAsync</c>) passes false and keeps today's behavior unconditionally.
        /// </param>
        /// <param name="locallyServedDnsZones">
        /// <c>DnsServer.LocallyServedDnsZones</c>. When false, special-zone interception itself is
        /// off, so deferral is moot regardless of the other flags -- stated and tested as an
        /// explicit input rather than left to be true-by-construction at the one call site, so this
        /// interaction is provable at the unit level.
        /// </param>
        /// <param name="isForwardSpecialUseName">
        /// <c>SpecialZoneManager.IsForwardSpecialUseName(qname)</c> for the query's question name.
        /// Reverse zones (and any other <c>SpecialZoneManager</c> answer outside the eight forward
        /// names) are never eligible to defer.
        /// </param>
        /// <param name="syntheticResponse"><c>SpecialZoneManager.Query</c>'s synthetic answer.</param>
        /// <param name="isAllowedAsync">Mirrors <c>DnsServer.IsAllowedAsync</c>.</param>
        /// <param name="processBlockedQueryAsync">
        /// Mirrors <c>DnsServer.ProcessBlockedQueryAsync</c>, which is itself tri-state: it can
        /// return null even when <paramref name="isAllowedAsync"/> returned false (an app vetoes
        /// without producing a block response).
        /// </param>
        public static async Task<DnsDatagram> DecideAsync(bool specialUseNamesDeferToBlocking, bool deferEligible, bool locallyServedDnsZones, bool isForwardSpecialUseName, DnsDatagram syntheticResponse, Func<Task<bool>> isAllowedAsync, Func<Task<DnsDatagram>> processBlockedQueryAsync)
        {
            bool defers = specialUseNamesDeferToBlocking && deferEligible && locallyServedDnsZones && isForwardSpecialUseName;
            if (!defers)
                return syntheticResponse;

            bool allowed = await isAllowedAsync();
            if (allowed)
                return syntheticResponse; //allowed: still the special-zone answer, not recursion

            DnsDatagram blockedResponse = await processBlockedQueryAsync();
            if (blockedResponse is not null)
                return blockedResponse; //blocked-with-response: the blocking response, terminal

            return syntheticResponse; //vetoed-with-no-response: the dangerous leg -- synthetic, never recursion
        }

        #endregion
    }
}
