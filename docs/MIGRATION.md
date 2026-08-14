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

## Tracks (restructured 2026-08-13)

The roadmap runs as **two parallel tracks meeting at two gates** — not a
single phase line. The engine is the goal; engine work starts
immediately and never waits on host plumbing it doesn't need. (The
earlier phase framing was history-shaped — organized around the fork
pivot — and manufactured false dependencies; this supersedes it.)

**Done — identity & structure (2026-08-13).** Fork renamed `urostigma`
under `ficus/`, `syconium` scaffolded, engine issues transferred with
cross-repo epic links intact, RFC line allocated.

### Engine track (syconium, Apache) — starts now

- **E1 — Adopt ContentFilter.Core** (RFC-013, syconium#5/#6): the Core is
  owned, already-Apache code; adoption, solution shape, and engine CI
  have **no fork dependency**. Only the host version contract waits on
  H1's image.
- **E2 — Synthesis builder** (RFC-016 engine side, syconium#2): built
  from RFC-007 and the plugin-side tests — owned spec, owned code,
  wire-bytes assertions. **No fork dependency to build**; promotion into
  the host waits on G2.
- **E3 — Harness construction** (RFC-012, syconium#1): corpus format,
  FsCheck generators, capture/replay/diff tooling — developable against
  any server image, stock upstream included. Only **baseline capture**
  (G1) needs the fork's build.
- **E4 — Cache and identity engine work** (RFC-017/018 engine sides):
  these genuinely wait on H2's characterization; not artificial.

### Host track (urostigma, GPL)

- **H1 — Sovereignty** (RFC-010, epic #1): buildable fork, founding
  tests, CI + image, sync policy, quirk fixes, consumer cutover.
- **H2 — Map & characterize** (RFC-011, epic #6): subsystem map, swap
  order, recursion boundary, `CacheZoneManager` characterization incl.
  admin/stats observables and persistence.
- **H3 — Native seam** (RFC-014, epic #10): the filter-agnostic Ostiole
  contract, dark-launch machinery, kill-switch/soak/perf standing rules.

### Gates (where the tracks must meet)

- **G1 — Baseline:** the harness (E3) captures the promotion baseline
  against the fork's CI image (H1). H1's cutover slices ensure production
  and baseline are the same build — one build, one truth.
- **G2 — First swap:** synthesis (E2) dark-launches through the seam
  (H3) and promotes per RFC-016; cache (RFC-017) and identity (RFC-018)
  then repeat the pattern: characterize → implement from spec →
  dark-launch → divergence log dry + perf budget → delete the C# path,
  kill-switch surviving the named soak.

**Steady state.** The host is a thin GPL shell around an Apache engine.
Upstream merges decay from code source to intelligence feed per subsystem
as swaps land. Publication is gated by #23 (RFC-020), deliberately
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
9. Console rebuild untracked → **syconium#7** (Syconium.Console);
   RFC-019 stub.
10. Promotion gates checked correctness only → perf clause added as the
    third epic #10 standing rule; each swap's RFC names its budget.

## RFC scaffold (2026-08-13)

The full number line is allocated and stubbed: registry at
[docs/rfcs/README.md](rfcs/README.md); RFC-010 (fork sovereignty) drafted
with nine slices and awaiting its two architect rounds; 011/014/020
stubbed here; 012/013/015–019 stubbed in syconium.

## Sequence

Phase 1 → 2 (harness needs the CI image) → 3–6 (re-scoped by #9's
decision; swap 1 promotes through phase 3's machinery). syconium#5 blocks
#11/#12. syconium#6 blocks the phase-2 harness wiring. #23 blocks
publication only.

Immediate next step: draft RFC-010 (fork sovereignty, epic #1), then two
architect rounds, then the slice grind.
