# Ficus migration plan

*2026-08-13. Point-in-time synthesis; the issues are the operational truth.
Rendered version: claude.ai artifact "Ficus Migration Plan".*

Replace the engine of a working DNS server with an independently owned
Apache 2.0 F# core, one gated subsystem swap at a time — inside a GPLv3
host fork that keeps absorbing upstream security releases until each
subsystem's replacement lands.

## The shape

| Repo | License | Role |
|---|---|---|
| `urostigma` (this repo) | GPLv3, private | Sovereign fork of Technitium DNS Server. Owns the roadmap epics. Shrinks toward a shell: listeners, web console, config, installers. Merges upstream releases on our schedule, read-only. |
| `syconium` | Apache 2.0, private | The F# engine, spec-first, provenance-walled: response synthesis, cache, identity resolution, and the wire-level characterization harness that gates every swap. |
| `technitium-content-filter` | Apache 2.0 | The origin program: `ContentFilter.Core` (verified free of Technitium references), the plugin shell, RFCs 001–009, and the patch series Phase 1 retires. |

License flow is one-way by design: Apache code may be consumed by the GPL
host; nothing flows back. Distributed builds of the combined server are
GPLv3 forever; the engine remains independently Apache in its own repos.
Provenance rules live in syconium's CONTRIBUTING.md. Upstream
(TechnitiumSoftware/DnsServer) is a read-only merge source; upstreaming is
dead.

## Phases

**0 — Identity & structure (done 2026-08-13).** Fork renamed `urostigma`
under `ficus/`, `syconium` scaffolded (Apache LICENSE, NOTICE, provenance
rules), engine issues transferred (old #8/#15/#19/#21 → syconium #1–#4)
with cross-repo epic links intact.

**1 — Sovereignty (epic #1, next).** #2 fold the patch series into
commits and retire `git apply` from the content-filter Dockerfiles; #3 CI
building/testing/publishing the server image; #4 upstream sync policy;
#5 founding quirks (silent install success, RFC 6761 hook).
*Exit:* content-filter builds against an urostigma-built image with zero
patch files; sync policy written; quirks fixed with tests.

**2 — Map & pin (epic #6).** #7 subsystem map + dependency graph;
syconium#1 wire-level characterization harness (the promotion gate and
provenance evidence corpus); #9 swap order + the recursion boundary —
re-scopes everything downstream.
*Exit:* map merged; harness runs against the CI image; decisions recorded.

**3 — Engine adoption & the seam (epic #10).** syconium#5 adopt
ContentFilter.Core; #11 consumption mechanism + version contract;
#12 native query-pipeline seam, dark-launched to parity; #13 explain/
control surfaces server-native.
*Exit:* engine answers in-process behind the seam with harness-shown
parity against the app path.

**4 — Swap 1: response synthesis (epic #14).** syconium#2 rules from
RFC-007 and the plugin-side tests; #16 unify all synthesized responses
through the one F# builder, delete C# duplicates.

**5 — Swap 2: the cache (epic #17).** #18 characterize
`CacheZoneManager` (incl. admin/stats observables and the persistence
decision); syconium#3 property-tested, differential-gated F# cache.

**6 — Swap 3: identity & transport (epic #20).** syconium#4 identity
tiers (exact IP, CIDR, DoT client IDs); #22 termination scope, DoT first,
DoQ deferred — decided from the Phase 2 map.

Swap discipline (4–6): characterize → implement engine-side from spec →
dark-launch → promote when the divergence log is dry → delete the C# path.
Every swap ships behind a per-subsystem kill-switch flag until a soak
period named in its RFC expires (epic #10 standing rule).

**7 — Steady state.** The host is a thin GPL shell around an Apache
engine. Upstream merges decay from code source to intelligence feed per
subsystem as swaps land. Publication is gated by #23, deliberately
unscheduled.

## Standing machinery

- **Per-epic pipeline:** RFC → two architect rounds → TDD slice grind →
  code review (rfc-flow). RFC numbering continues at 010; each RFC lives
  in the repo whose behavior it specifies; the master registry lives in
  this repo's docs.
- **Upstream sync:** per #4, including the post-merge provenance clause.
- **Provenance wall:** syconium CONTRIBUTING rules apply to every engine
  line, forever.

## Web stack (decided 2026-08-13)

- **API:** Giraffe handlers in F#, shipped from syconium as `Syconium.Api`
  (Apache) and mounted by this host into its existing Kestrel pipeline —
  the host contributes routing and auth wiring only. Giraffe chosen for
  maturity and a small abandonment blast radius (thin functional layer
  over ASP.NET Core; handlers migrate mechanically to minimal APIs if
  ever needed). Typed contracts via shared F# DTO types.
- **Console:** standalone Blazor WebAssembly (`Syconium.Console`): C#
  components over F# logic libraries compiled into the same bundle,
  served by the host as static files, talking exclusively to the admin
  API. WASM over Interactive Server deliberately: admin actions restart
  the web service, and a server-circuit UI dies mid-action on exactly the
  restarts it triggers; a WASM console survives them in the browser.
  Trimming on, AOT off unless proven needed, no mixed render modes.
  Rejected alternatives: Bolero (thin-team dependency lagging Blazor's
  release cadence; moot once C# components are acceptable), Fable/React
  (Node toolchain, unwanted ecosystem), htmx (fine, but the component
  SPA model was preferred).
- **Repo rule:** repo count stays pinned to license count (two, end
  state). The frontend is a project in syconium's solution, not a repo.
  C# is welcome in syconium — the wall is provenance, not language. The
  Technitium console (GPL) keeps serving residual host features until
  swaps retire them; its `www/` is never ported into the new console.
- **Syconium solution shape:** `Syconium.Core`, `Syconium.Synthesis`,
  `Syconium.Cache`, `Syconium.Identity`, `Syconium.Api`,
  `Syconium.Console`, `Syconium.Harness`, `tests/`.

## Gap review (2026-08-13) — all filed

1. ContentFilter.Core move had no issue → **syconium#5**, sub-issue of
   epic #10.
2. No RFC home/registry → decided in RFC-010 (comment on epic #1).
3. No engine CI or version contract → **syconium#6**; pin + harness
   wiring owned by #11 (comment).
4. Harness is wire-only; swaps move admin/stats behavior → scope
   amendments on syconium#1 and #18.
5. Cache persistence undecided → added to #18 scope; decision recorded
   in the swap-2 RFC.
6. No kill-switch convention → standing rule on epic #10.
7. Publication readiness unowned → **#23** (publication gate).
8. Sync policy silent on provenance exposure → clause added to #4.

## Sequence

Phase 1 → 2 (harness needs the CI image) → 3–6 (re-scoped by #9's
decision; swap 1 promotes through phase 3's machinery). syconium#5 blocks
#11/#12. syconium#6 blocks the phase-2 harness wiring. #23 blocks
publication only.

Immediate next step: draft RFC-010 (fork sovereignty, epic #1), then two
architect rounds, then the slice grind.
