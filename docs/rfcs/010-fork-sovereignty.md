# RFC-010: Fork sovereignty — patches become commits, builds trust this repo

- **Status:** Draft (round-1 review applied 2026-08-13; ledger in
  `010-fork-sovereignty.handoff.md`)
- **Depends on:** nothing — this is the program's foundation
- **Enables:** everything downstream (011–020); content-filter's patch-application machinery retires
- **Issues:** epic #1 (#2 fold patches, #3 CI, #4 sync policy, #5 founding quirks); establishes the registry decision recorded on epic #1

## Summary

Make this fork a real workspace instead of a patch target. The three-patch
series in `technitium-content-filter/patches/dns-server/` becomes real
commits here with tests; the two founding quirks that motivated the pivot
get fixed in-tree; CI builds, tests, and publishes the server image from
this repo; a written upstream-sync policy replaces the pin-bump/patch-rebase
discipline; and content-filter's Dockerfiles stop cloning upstream and applying
patches — they clone this repo at a pin instead, and the patch series is
deleted.

Context: upstream declined the type-sweep fix
(TechnitiumSoftware/DnsServer#2092, "not a bug") and the program pivoted
from upstreaming to owning. Upstream remains a read-only source of releases
to merge, on our schedule.

## Motivation

Every content-filter build currently reconstructs the patched server from
scratch: six Dockerfiles (`Dockerfile.build`, `.test`, `.integration-test`,
`.benchmarks`, `.coyote`, `.perf-comparison`) clone upstream and either
apply the patch series or build `DnsServerCore.ApplicationCommon` from the
patched source, and `BaseTechnitiumFixture.cs` builds the runnable
patched-server image at test-run time via Testcontainers. That was correct
discipline while the patches were upstream candidates; now that they are
permanent, it is pure overhead with real failure modes: the patches carry
placeholder authorship (`RFC-006 Spike <spike@localhost>`), drift silently
against upstream churn, and no build anywhere trusts this fork.

The fork also cannot be built or tested standalone today: it ships zero
test projects (upstream has none), and every project references
`TechnitiumLibrary` DLLs via `HintPath` into an unpinned sibling clone
that this development tree does not even contain. Sovereignty starts with
a repo that builds itself.

## Non-goals

- No engine code, no F#, no syconium consumption — that starts at RFC-013.
- No renaming of .NET projects, namespaces, or assemblies — the merge
  surface with upstream stays intact (MIGRATION.md, "The shape").
- No upstream merge is performed here; this RFC only writes the policy.
- No wire-level characterization harness — that is RFC-012; tests here are
  unit/service-level.
- No publication: repo and image stay private (RFC-020 gates that).
- **TechnitiumLibrary stays upstream's.** It is pinned and consumed, never
  forked or patched by this RFC; if divergence is ever needed it gets its
  own decision. `HintPath`s in the `.csproj` files are left untouched —
  editing them would put a permanent merge conflict on every project file.
- **linux/amd64 only.** Multi-arch is explicitly out of scope (matches
  current practice everywhere in the program); revisit if a consumer
  materializes on arm64.
- **Windows-only projects are out of CI scope**: `DnsServerWindowsService`,
  `DnsServerSystemTrayApp`, `DnsServerWindowsSetup` are not built by CI
  (runners are Linux; nothing consumes them). Revisit at RFC-020 if
  publication wants Windows artifacts.
- The `Apps`-section permission coarseness in patch 0003 (`View` grants
  `config/get` across every installed app) is folded as-is and tracked as
  follow-up issue #24, not fixed here.

## Design

### Branch, history, and prior state

Work lands on `develop` (this repo's default; issue #2's "master" reads as
the default branch). One stale branch exists from the upstream attempt:
`origin/fix/app-type-discovery` carries patch 0001's change (commit
`cba7bff4`) without the provenance-citing message this RFC requires — it
is not merged; slice 2 folds 0001 fresh with a proper message and the
stale branch is deleted afterward.

The folded commits are new commits authored by Corey Leavitt — the patch
files' placeholder identity is not preserved; each commit message cites
the patch it folds and RFC-006 for provenance of intent. Patches fold
strictly in series order 0001 → 0002 → 0003 (0003 depends on 0002's
interface and touches 0001's file). Note for implementers: a bulk
`git apply --check patches/*.patch` falsely fails on overlapping hunks in
`DnsApplication.cs`; sequential application is clean (verified).

**In-file copyright headers:** new files entering the tree (e.g.
`IDnsApplicationApiHandler.cs`, which the patch wrongly stamps with
Technitium's header) carry this fork owner's copyright under GPLv3;
modified upstream files keep Technitium's header. This implements GPLv3
§5's state-changes norm rather than misattributing new work.

### Patch inventory

| Patch | Touches | Behavior |
|---|---|---|
| 0001 per-type isolation | `DnsServerCore/Dns/Applications/DnsApplication.cs` | `ReflectionTypeLoadException` during the type sweep no longer aborts the whole assembly; loadable types still register (unloadable ones are skipped via `ex.Types`) |
| 0002 `IDnsApplicationApiHandler` | `DnsServerCore.ApplicationCommon/IDnsApplicationApiHandler.cs` (new) | The generic app-API port RFC-006 built the explain endpoint on |
| 0003 `/api/apps/call` | `DnsApplication.cs`, `DnsWebService.cs`, `WebServiceAppsApi.cs` | Dispatch route from the web service to a named app's handler — folded into the extracted dispatcher shape below, not verbatim |

**On upstream's objection to 0001.** The maintainer's decline was a
substantive failure-isolation argument, not process: partial registration
can leave an app running in a configuration its author never tested, with
per-query errors instead of one clean total failure. This fork chooses
partial-load anyway, with eyes open, because: (a) the observed failure
mode that motivated the pivot was the opposite — a *total* silent abort
that turned one unresolvable type into blocking-off-with-no-signal; (b)
this deployment's apps are operated by their author, not installed from a
marketplace, so "untested partial configuration" is discoverable rather
than latent; and (c) quirk fix 1 below makes partial loads *loud* at
install time, which addresses the "hard to notice" half of upstream's
concern directly. Total-abort-with-visible-error was considered and
rejected: it converts a missing optional dependency into a full blocking
outage, which is the worse failure for a DNS-filtering deployment.

### Founding test project

`DnsServerCore.Tests` (first test project in the fork) is founded by slice
1 and grows with every slice. Package pins match the sibling repo so the
program's test stack does not drift: xUnit 2.9.3, NSubstitute 5.3.0,
FsCheck.Xunit 3.1.0 (FsCheck only when property tests arrive; don't
reference it speculatively). Slice 1 adds `InternalsVisibleTo` from
`DnsServerCore` (and `.ApplicationCommon` if needed) to the test project —
none exists in the repo today, and slice 6's hook lives on an `internal`
method. Shared fixtures live in a `Fixtures/` folder inside the test
project; promote to a separate infrastructure project only when a second
test project exists to share them (don't pre-build the abstraction).

**The poisoned-app fixture is slice 2's hard part, not the production
diff.** 0001 fixes assembly-level *load* failure
(`ReflectionTypeLoadException` from `Assembly.GetTypes()` when a type's
dependency is unresolvable) — a type that merely throws in its
*constructor* was already isolated before the patch, so a
constructor-throw fake produces a test that passes on unpatched code and
proves nothing. The fixture must be a compiled companion app assembly
loaded through an `AssemblyLoadContext` that withholds one dependency at
test time. `patches/dns-server/pr1-repro/` in content-filter already
contains exactly this scenario (AlphaApp/BravoApp/CharlieApp plus a
deliberately-missing helper assembly); slice 2 ports it into
`DnsServerCore.Tests/Fixtures/` — this porting must happen before
slice 11 deletes `patches/`.

### Dispatch extraction (decided, not open)

`WebServiceAppsApi` is a private sealed class nested in the monolithic
`DnsWebService`, whose constructor does filesystem I/O and whose pipeline
is a real Kestrel host — patch 0003's `CallAppApiAsync` as written is
untestable at any granularity below a running server. The RFC decides the
seam now rather than mid-slice: extract an `internal
DnsAppApiDispatcher` owning the decision chain — resolve app by name →
ambiguity check → permission check → invoke handler → classify errors
into the response envelope — taking plain inputs (an app-lookup delegate,
a permission-check delegate, parsed request data) and returning a plain
result, with zero `HttpContext` or `DnsWebService` involvement.
`WebServiceAppsApi.CallAppApiAsync` becomes a thin HTTP adapter, and
`WebServiceAppsApi` itself becomes `internal` for adapter-level tests.
Patch 0003 is folded *into this shape* — fold-verbatim-then-refactor was
considered and rejected as scheduled rework.

### Quirk fixes (issue #5)

1. **Install/load error visibility — total and partial.** Two distinct
   failure shapes, both currently silent:
   - *Zero types register* (the sweep throws, or nothing implements the
     interfaces): the install/update/load response must be
     `status:"error"` with the loader exception. No new envelope plumbing
     exists or is needed — the existing `WebServiceApiMiddleware` /
     `WebServiceExceptionHandler` pair already produces
     `status:"error"` + `errorMessage` for any escaping exception; the fix
     is that the loader *throws* in this case. Install/update must not
     join the raw-passthrough route list.
   - *Partial load* (post-0001: some types register, some are skipped):
     `DnsApplication` exposes a `LoadWarnings` collection (type name +
     exception summary); the apps API includes a `"loadWarnings"` array in
     the envelope whenever non-empty — on `status:"ok"` too. This closes
     the gap 0001 would otherwise reintroduce: 2-of-3 types loading
     (blocking handler's sibling failed) is exactly the "blocking quietly
     off" scenario the pivot was about. Test: 2-of-3 partial load
     surfaces the warning in the response; healthy path regression test
     asserts no warnings.

2. **RFC 6761 / Locally Served Zones defer-to-blocking.** This is a
   pipeline-*ordering* change, not an on/off switch, and it composes with
   an existing flag. Today `DnsServer.AuthoritativeQueryAsync` returns
   `SpecialZoneManager.Query`'s synthetic answer before the blocking
   handlers ever run, gated by the existing `LocallyServedDnsZones` bool
   (default `true`), which covers RFC 6303 reverse zones, RFC 8375
   `home.arpa`, and RFC 6761 forward names together — flipping it off
   kills all three and sends the queries to real recursion. Design:
   - New narrow setting `specialUseNamesDeferToBlocking` (default
     `false`), applying only to the RFC 6761 *forward-name* subset of the
     special zones, and only effective when `LocallyServedDnsZones` is
     `true` (when that is `false` nothing is intercepted and this setting
     is moot — interaction stated and tested).
   - When set: for names in the subset, run the blocking-handler pass
     *before* returning the special-zone answer. Blocked → the blocking
     response. Not blocked → **still return the special-zone synthetic
     answer, never fall through to recursion.** This invariant is the
     load-bearing line: a naive "skip the shortcut" implementation would
     leak RFC-1918 reverse lookups and `localhost` queries to upstream
     resolvers.
   - Tests (three, not two): blocked special-use name gets the blocking
     response; unblocked special-use name still gets the synthetic answer
     with no recursion attempted; `LocallyServedDnsZones=false` behaves
     identically with the new setting on or off.

### CI (issue #3)

**TechnitiumLibrary provisioning comes first — without it nothing
builds.** Every job (and every implementer) needs a sibling clone of
`TechnitiumSoftware/TechnitiumLibrary` at a **pinned tag paired with the
current upstream base** (today: the tag matching DnsServer v15.4.0, as
content-filter's Dockerfiles already pin), with
`TechnitiumLibrary.ByteTree`, `.Net`, and `.Security.OTP` built Release
before this solution restores. A checked-in script
(`build/get-technitiumlibrary.sh`, reading the pin from one file) serves
both CI and local setup; the pin bumps only alongside upstream merges
(sync policy below). No submodule — that would be fine for CI but the
`HintPath`s expect a sibling path and editing every `.csproj` creates
permanent merge surface for zero gain.

Workflow shape on push/PR: checkout → provision TechnitiumLibrary (pin) →
restore/build the Linux project set (`DnsServerApp`, `DnsServerCore`,
`DnsServerCore.ApplicationCommon`, `DnsServerCore.HttpApi`) → run
`DnsServerCore.Tests`. NuGet and TechnitiumLibrary build output are
cached keyed on the pin.

**Image build:** the repo's existing `Dockerfile` does not build — it
copies a host-side `publish/` directory. CI uses a new, self-contained
multi-stage `Dockerfile.sovereign` (SDK stage: provision
TechnitiumLibrary + `dotnet publish DnsServerApp` → runtime stage),
inheriting its logic from content-filter's `Dockerfile.patched-server`,
which retires. Upstream's `Dockerfile` is left untouched (no merge
surface). The image is labeled with `org.opencontainers.image.revision`
(the git SHA) so every published image is traceable to its commit. The
publish job runs only on `develop` and **requires the test job to have
passed on the same commit**. Slice 9 also repoints this repo's own
`docker-compose.yml` (which still names upstream's Docker Hub image) at
the published image.

**Tagging:** `<upstream-version>-sovereign.<n>` (e.g.
`15.4.0-sovereign.1`), immutable, plus a moving `develop` tag for the
operator's own compose convenience. The image's consumers are the
RFC-012 harness and the operator's deployment — no consumer-facing tag
ceremony beyond that (scope decision in Consumer cutover below).

**Build auth:** one classic PAT (`repo` read + `read:packages`) covers
both the private clone in content-filter's Docker builds and any image
pull; stored as a secret in content-filter and used only on
`push`/`workflow_dispatch`/same-repo-PR triggers, never
`pull_request_target` (content-filter is public). Fine-grained PATs
rejected — GHCR support for them has documented gaps.

### Upstream sync policy (issue #4)

A written policy at `docs/UPSTREAM-SYNC.md`:

- **Triggers, mechanically:** watch `TechnitiumSoftware/DnsServer`
  releases (GitHub release notifications on the upstream repo). A release
  whose notes mention a CVE or security fix starts a sync within one
  week; feature releases are merged only when something in them is
  wanted.
- **Procedure:** `git merge` from the upstream remote, never rebase. The
  honest rationale: published images carry `image.revision` SHAs and the
  provenance record cites commits — a rebase orphans every SHA anyone has
  written down. (Consumers pull image tags, not commits; the audit trail
  is what needs history stability.)
- **Paired pin:** every upstream merge bumps the TechnitiumLibrary pin to
  the matching release tag in the same PR — the two versions are one
  fact, never independent variables.
- **Conflict ownership:** we own the resolution in files we have diverged
  in: `DnsApplication.cs`, `WebServiceAppsApi.cs`, `DnsWebService.cs`,
  growing as quirk fixes land; the policy doc keeps the authoritative
  list.
- **Provenance clause (gap 8):** after a merge session, engine work on any
  subsystem the merge touched starts from the spec or wire corpus
  (syconium CONTRIBUTING rule 4), never from memory of the diff.
- **SDK re-survey:** the RFC-006 handoff's obligation — on each merge,
  re-survey the `ApplicationCommon` SDK surface for breaking drift against
  the plugin — carries into the policy.

### Consumer cutover — slimmed to the actual consumer population

**Scope decision (2026-08-13, owner):** there are no external consumers —
content-filter is this operator's own deployment, and continuity ceremony
for it is explicitly not wanted. The cutover is therefore the minimal
change that ends patch application: every content-filter Dockerfile that
today clones upstream and applies `patches/dns-server/` (the six
Dockerfiles plus `Dockerfile.patched-server`) instead clones
`coreyleavitt/urostigma` at the pin and drops the `git apply` loop. Auth
for the private clone is the deploy-key/token-in-build-context story
issue #2 already named. Nothing else changes: `HintPath`s still resolve
against source builds, and `BaseTechnitiumFixture` keeps building its
server image at test-run time — now from the fork — preserving its
built-not-pulled freshness property with zero C# changes.

Rejected as consumer-population-zero engineering (recorded so round 2
doesn't re-litigate): `COPY --from` DLL extraction, NuGet packaging of
`ApplicationCommon`, fixture image-pulling with tag-bump discipline,
rollback runbooks, and the GHCR Actions-access grant. Revisit at RFC-013's
version contract if the engine ever gains real consumers.

**History preservation** — before `patches/dns-server/` is deleted:
`UPSTREAM.md` (the only record of PR #2092's submission and decline) is
copied to this repo as `docs/UPSTREAM-HISTORY.md`; `pr1-repro/` has
already been ported into test fixtures by slice 2. `build-dns-server.sh`
and `docker-compose.example.yaml` update their comments/source in
passing; both keep working since the image build survives.

## Slices

Sequencing note: content-filter appears here only because its Dockerfiles
are the last thing still building upstream+patches — once quirk fixes
land, that recipe produces a *different server* than this repo, which
would poison RFC-012's characterization baseline. The fix is one slice of
one-line clone-source changes, deliberately minimal (see the scope
decision in Consumer cutover). Slices 1–9 unblock RFCs 011/012/013;
nothing in the engine program waits on slice 10. The folded 0002/0003
surfaces are transitional — RFC-015 replaces them — but the operator's
deployment uses them today, so the fork carries them until then.

Each slice is a `/tdd` unit where behavior changes; slices 7–8 are
infrastructure verified by execution (a dry run of the same commands,
then a green Actions run, then a scratch `docker pull` with no ambient
credentials), not by xUnit. CI lands after the behavior slices
deliberately: local `dotnet test` is the safety net during 1–6; 7–8
automate what is already green.

1. **Buildable ground.** `build/get-technitiumlibrary.sh` with the pin
   file; solution builds locally from a fresh clone by following one
   documented step; found `DnsServerCore.Tests` (pinned packages,
   `InternalsVisibleTo`, `Fixtures/` folder) with one smoke test green.
2. **Poisoned-app fixture; fold 0001.** Port `pr1-repro` into
   `Fixtures/`; build the withheld-dependency `AssemblyLoadContext`
   harness; write the isolation test and watch it fail on the unpatched
   sweep (a constructor-throw fake is *not* acceptable RED — it passes
   unpatched); fold 0001; green. Delete stale branch
   `fix/app-type-discovery`.
3. **Fold 0002** (`IDnsApplicationApiHandler`, with corrected copyright
   header): test proves a fake handler implementing the interface is
   discovered by the sweep.
4. **`DnsAppApiDispatcher` extraction; fold 0003 into it.** Dispatch
   tests: app found/missing/ambiguous, permission denied, handler
   invoked, handler-throw classified into the error envelope;
   `WebServiceAppsApi` becomes `internal`, adapter kept thin.
5. **Quirk 1: install/load visibility.** Zero-registration →
   `status:"error"` via existing exception envelope (no new plumbing);
   partial load → `loadWarnings` array (2-of-3 test with the poisoned
   fixture); healthy-path regression.
6. **Quirk 2: RFC 6761 defer-to-blocking** per the design: the three
   tests including the no-recursion invariant and the
   `LocallyServedDnsZones` interaction.
7. **CI: build + test workflow** with TechnitiumLibrary provisioning and
   caching; verification by execution.
8. **CI: image publish.** `Dockerfile.sovereign`, immutable
   `-sovereign.<n>` tag, `image.revision` label, publish gated on tests;
   repoint this repo's `docker-compose.yml`; verify with a scratch pull
   using the documented auth path.
9. **Sync policy doc** (`docs/UPSTREAM-SYNC.md`) per the design, plus
   `docs/UPSTREAM-HISTORY.md` preserving the PR #2092 record.
10. **Cutover, in content-filter:** the seven Dockerfiles (six + 
    `Dockerfile.patched-server`) clone `coreyleavitt/urostigma` at the
    pin instead of upstream+patches, `git apply` loops deleted, clone
    auth via the PAT/deploy key; **then** delete `patches/dns-server/`
    (history preserved per Design); full content-filter CI green.

## Open questions

None. Round 1 resolved all three original open questions (dispatch seam:
extract now; RFC 6761 surface: narrow flag with ordering semantics and
the no-recursion invariant; tagging: `<upstream>-sovereign.<n>`).
