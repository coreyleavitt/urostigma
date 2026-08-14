# RFC-010: Fork sovereignty — patches become commits, builds trust this repo

- **Status:** Draft (pre-architect)
- **Depends on:** nothing — this is the program's foundation
- **Enables:** everything downstream (011–020); content-filter's patch-application machinery retires
- **Issues:** epic #1 (#2 fold patches, #3 CI, #4 sync policy, #5 founding quirks); establishes the registry decision recorded on epic #1

## Summary

Make this fork a real workspace instead of a patch target. The three-patch
series in `technitium-content-filter/patches/dns-server/` becomes real
commits here with tests; the two founding quirks that motivated the pivot
get fixed in-tree; CI builds, tests, and publishes the server image from
this repo; a written upstream-sync policy replaces the pin-bump/patch-rebase
discipline; and the six content-filter Dockerfiles stop running `git apply`
loops and consume the published image.

Context: upstream declined the type-sweep fix
(TechnitiumSoftware/DnsServer#2092, "not a bug") and the program pivoted
from upstreaming to owning. Upstream remains a read-only source of releases
to merge, on our schedule.

## Motivation

Every content-filter build currently reconstructs the patched server from
scratch: six Dockerfiles (`Dockerfile.build`, `.test`, `.integration-test`,
`.benchmarks`, `.coyote`, `.perf-comparison`) copy `patches/dns-server/`
and loop `git apply` over a fresh upstream clone. That was correct
discipline while the patches were upstream candidates; now that they are
permanent, it is pure overhead with real failure modes: the patches carry
placeholder authorship (`RFC-006 Spike <spike@localhost>`), drift silently
against upstream churn, and no build anywhere trusts this fork.

The fork also ships zero test projects (upstream has none), so the folded
patches' behavior is currently proven only by content-filter's plugin-side
tests — the wrong repo for pinning server behavior we now own.

## Non-goals

- No engine code, no F#, no syconium consumption — that starts at RFC-013.
- No renaming of .NET projects, namespaces, or assemblies — the merge
  surface with upstream stays intact (MIGRATION.md, "The shape").
- No upstream merge is performed here; this RFC only writes the policy.
- No wire-level characterization harness — that is RFC-012; tests here are
  unit/service-level.
- No publication: repo and image stay private (RFC-020 gates that).

## Design

### Branch and history

Work lands on `develop` (this repo's default; issue #2's "master" reads as
the default branch). The folded commits are new commits authored by Corey
Leavitt — the patch files' placeholder identity is not preserved; each
commit message cites the patch it folds and RFC-006 for provenance of
intent. Patches are folded in series order (0003 depends on 0002's
interface and touches 0001's file).

### Patch inventory

| Patch | Touches | Behavior |
|---|---|---|
| 0001 per-type isolation | `DnsServerCore/Dns/Applications/DnsApplication.cs` | A throwing type in the app type-sweep no longer aborts the whole sweep; other types still register |
| 0002 `IDnsApplicationApiHandler` | `DnsServerCore.ApplicationCommon/IDnsApplicationApiHandler.cs` (new) | The generic app-API port RFC-006 built the explain endpoint on |
| 0003 `/api/apps/call` | `DnsApplication.cs`, `DnsWebService.cs`, `WebServiceAppsApi.cs` | Dispatch route from the web service to a named app's handler |

### Founding test project

`DnsServerCore.Tests` (xUnit, first test project in the fork) is founded by
slice 1 and grows with every slice. It pins folded-patch behavior at the
unit/service level; full wire coverage arrives with RFC-012 and is not
duplicated here.

### Quirk fixes (issue #5)

1. **Silent install success.** An app install whose type sweep throws and
   registers zero dnsApps currently answers `status:"ok"` with the only
   evidence in a log file. It must answer `status:"error"` with the loader
   exception in the envelope. Test-first against the install/load path.
2. **RFC 6761 / Locally Served Zones interception.** Special-use names are
   answered before blocking ever sees them. Add a server-config hook so
   special-use-domain handling can defer to the blocking pipeline for our
   deployment; default preserves upstream behavior. Test-first: with the
   flag set, a locally-served name reaches the blocking handler.

### CI (issue #3)

GitHub Actions in this repo: restore, build, run `DnsServerCore.Tests` on
every push/PR; on `develop`, additionally build and push the server image
to `ghcr.io/coreyleavitt/urostigma` (private). The image build uses this
repo's existing `Dockerfile` — no patch-application step exists to remove
on this side; the removal happens in content-filter (below).
`Dockerfile.patched-server` in content-filter retires rather than moving.

**Build auth:** content-filter CI and local builds pull the private GHCR
image with a fine-grained PAT (`read:packages`) stored as a secret /
`docker login`. A deploy key is not needed once consumption is
image-based rather than clone-based — that is the simplification issue #2
anticipated.

### Upstream sync policy (issue #4)

A written policy at `docs/UPSTREAM-SYNC.md`:

- **Triggers:** security fixes always, promptly; features on demand only.
- **Procedure:** `git merge` from the upstream remote (never rebase — our
  commits are permanent and published to consumers).
- **Conflict ownership:** we own the resolution in files we have diverged
  in: `DnsApplication.cs`, `WebServiceAppsApi.cs`, `DnsWebService.cs`, and
  the list grows as quirk fixes land; the sync policy doc keeps the
  authoritative list.
- **Provenance clause (gap 8):** after a merge session, engine work on any
  subsystem the merge touched starts from the spec or wire corpus
  (syconium CONTRIBUTING rule 4), never from memory of the diff.
- **SDK re-survey:** the RFC-006 handoff's obligation — on each merge,
  re-survey the `ApplicationCommon` SDK surface for breaking drift against
  the plugin — carries into the policy.

### Consumer cutover

With the image published, the six content-filter Dockerfiles replace their
clone-and-patch preamble with `FROM ghcr.io/coreyleavitt/urostigma:<tag>`
(or a pull in compose), `patches/dns-server/` is deleted, and `UPSTREAM.md`
is rewritten to describe the new relationship. The integration fixture
consumes the same image CI publishes — one build, every consumer.

## Slices

Each slice is a `/tdd` unit: test-first where behavior changes, green and
refactored before the next begins.

1. **Found `DnsServerCore.Tests`; fold patch 0001** with tests proving
   per-type isolation (one poisoned type registers nothing; siblings load).
2. **Fold patch 0002** (`IDnsApplicationApiHandler`): interface lands; test
   proves a fake handler is discovered by the type sweep.
3. **Fold patch 0003** (`/api/apps/call`): dispatch unit tests — named app
   found/missing, handler invoked, error envelope on handler throw.
4. **Quirk: install/load silent success** answers `status:"error"` with the
   loader exception in the envelope; regression test for the healthy path.
5. **Quirk: RFC 6761 hook** behind server config, default-off; tests for
   both flag states.
6. **CI: build + test workflow** on push/PR (no image yet).
7. **CI: image publish** to private GHCR on `develop`, with the PAT-based
   pull documented for consumers.
8. **Sync policy doc** (`docs/UPSTREAM-SYNC.md`) per the design above —
   reviewed as a doc slice, no code.
9. **Consumer cutover in content-filter:** six Dockerfiles consume the
   image; `patches/dns-server/` deleted; `UPSTREAM.md` rewritten; that
   repo's CI green against the published image.

## Open questions (for the architect rounds)

- Slice 3's test seam: `DnsWebService` is monolithic — is a thin extraction
  of the dispatch logic acceptable now, or do we test through the running
  service only and accept coarser assertions until RFC-012?
- RFC 6761 hook placement: server-level config vs. per-app opt-in; and does
  default-off match issue #5's intent for *our* deployment (which wants it
  on)?
- Image tagging scheme: track upstream version (`15.4.0-sovereign.1`) vs.
  independent semver from this repo.
