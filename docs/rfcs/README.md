# RFC registry (master)

One number line for the whole ficus program. Each RFC lives in the repo
whose behavior it specifies; engine RFCs double as provenance artifacts
(the spec wall — see syconium's CONTRIBUTING.md). This registry is the
master index; 001–009 predate it and live in technitium-content-filter.

Statuses: Stub → Draft → Accepted (two architect rounds applied) →
Implemented.

**Division of labor:** RFCs own design (the how, the why, the slices);
issues own status (open/closed, discussion, blockers); MIGRATION.md owns
the map. Every roadmap issue carries a "Design home" comment pointing at
its RFC. While an RFC is a Stub, its issues' text is authoritative (the
stubs were derived from them); once it goes Draft, design changes happen
only in the RFC and, on conflict, the RFC wins.

| RFC | Repo | Title | Status | Depends on |
|---|---|---|---|---|
| 001–009 | content-filter | See [that repo's index](https://github.com/coreyleavitt/technitium-content-filter/blob/main/docs/rfcs/README.md) | mixed | — |
| [010](010-fork-sovereignty.md) | urostigma | Fork sovereignty: patches become commits, builds trust this repo | Accepted | — |
| [011](011-subsystem-map-and-swap-order.md) | urostigma | Subsystem map, characterization targets, swap order, recursion boundary | Stub | 010 |
| [012](https://github.com/coreyleavitt/syconium/blob/main/docs/rfcs/012-wire-characterization-harness.md) | syconium | Wire-level characterization harness | Stub | none to build; 010 for baseline capture |
| [013](https://github.com/coreyleavitt/syconium/blob/main/docs/rfcs/013-engine-adoption.md) | syconium | Engine adoption: ContentFilter.Core, solution shape, CI, version contract | Stub | none to adopt; 010 for the version contract |
| [014](014-native-pipeline-seam.md) | urostigma | Native query-pipeline seam and dark-launch | Stub | 011, 012, 013 |
| [015](https://github.com/coreyleavitt/syconium/blob/main/docs/rfcs/015-admin-api-surfaces.md) | syconium | Admin API surfaces: Syconium.Api, host mounting | Stub | 013, 014 |
| [016](https://github.com/coreyleavitt/syconium/blob/main/docs/rfcs/016-response-synthesis.md) | syconium | Swap 1: response synthesis | Stub | 007, 013 to build; 012, 014 to promote |
| [017](https://github.com/coreyleavitt/syconium/blob/main/docs/rfcs/017-cache.md) | syconium | Swap 2: the cache | Stub | 011, 012, 014 |
| [018](https://github.com/coreyleavitt/syconium/blob/main/docs/rfcs/018-identity-transport.md) | syconium | Swap 3: client identity and transport termination | Stub | 011, 014 |
| [019](https://github.com/coreyleavitt/syconium/blob/main/docs/rfcs/019-console.md) | syconium | Syconium.Console: standalone Blazor WASM admin console | Stub | 015 |
| [020](020-publication-gate.md) | urostigma | Publication gate: de-branding and GPL compliance | Stub | — (gates publication only) |

Sequencing runs as two parallel tracks (see
[../MIGRATION.md](../MIGRATION.md), "Tracks"): the engine track (013
adoption, 016 building, 012 tooling) starts immediately with no fork
dependencies; the host track runs 010 → 011 → 014; they meet at the
baseline-capture and first-swap gates. 011's swap-order and
recursion-boundary decision re-scopes 017–018 before either leaves Stub.
020 is deliberately unscheduled. The issues are the operational truth.
