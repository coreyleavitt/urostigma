# RFC registry (master)

One number line for the whole ficus program. Each RFC lives in the repo
whose behavior it specifies; engine RFCs double as provenance artifacts
(the spec wall — see syconium's CONTRIBUTING.md). This registry is the
master index; 001–009 predate it and live in technitium-content-filter.

Statuses: Stub → Draft → Accepted (two architect rounds applied) →
Implemented.

| RFC | Repo | Title | Status | Depends on |
|---|---|---|---|---|
| 001–009 | content-filter | See [that repo's index](https://github.com/coreyleavitt/technitium-content-filter/blob/main/docs/rfcs/README.md) | mixed | — |
| [010](010-fork-sovereignty.md) | urostigma | Fork sovereignty: patches become commits, builds trust this repo | Draft | — |
| [011](011-subsystem-map-and-swap-order.md) | urostigma | Subsystem map, characterization targets, swap order, recursion boundary | Stub | 010 |
| [012](https://github.com/coreyleavitt/syconium/blob/main/docs/rfcs/012-wire-characterization-harness.md) | syconium | Wire-level characterization harness | Stub | 010 |
| [013](https://github.com/coreyleavitt/syconium/blob/main/docs/rfcs/013-engine-adoption.md) | syconium | Engine adoption: ContentFilter.Core, solution shape, CI, version contract | Stub | 010 |
| [014](014-native-pipeline-seam.md) | urostigma | Native query-pipeline seam and dark-launch | Stub | 011, 012, 013 |
| [015](https://github.com/coreyleavitt/syconium/blob/main/docs/rfcs/015-admin-api-surfaces.md) | syconium | Admin API surfaces: Syconium.Api, host mounting | Stub | 013, 014 |
| [016](https://github.com/coreyleavitt/syconium/blob/main/docs/rfcs/016-response-synthesis.md) | syconium | Swap 1: response synthesis | Stub | 007, 012, 014 |
| [017](https://github.com/coreyleavitt/syconium/blob/main/docs/rfcs/017-cache.md) | syconium | Swap 2: the cache | Stub | 011, 012, 014 |
| [018](https://github.com/coreyleavitt/syconium/blob/main/docs/rfcs/018-identity-transport.md) | syconium | Swap 3: client identity and transport termination | Stub | 011, 014 |
| [019](https://github.com/coreyleavitt/syconium/blob/main/docs/rfcs/019-console.md) | syconium | Syconium.Console: standalone Blazor WASM admin console | Stub | 015 |
| [020](020-publication-gate.md) | urostigma | Publication gate: de-branding and GPL compliance | Stub | — (gates publication only) |

Sequencing: 010 first (nothing else has a trustworthy build without it).
011/012/013 can proceed in parallel after it. 011's swap-order and
recursion-boundary decision re-scopes 016–018 before any of them leaves
Stub. 020 is deliberately unscheduled. The phase view lives in
[../MIGRATION.md](../MIGRATION.md); the issues are the operational truth.
