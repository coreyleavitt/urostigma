# RFC-010: Fork sovereignty — patches become commits, builds trust this repo

- **Status:** Accepted (architect rounds 1 and 2 applied 2026-08-13;
  ledgers in `010-fork-sovereignty.handoff.md`)
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
| 0001 per-type isolation | `DnsServerCore/Dns/Applications/DnsApplication.cs` | The type sweep survives per-type load failure. Unpatched, `ExportedTypes` throws (`FileNotFoundException` for a missing dependency) and the whole assembly is dropped; patched, the sweep uses `Assembly.GetTypes()`, whose `ReflectionTypeLoadException` carries partial results via `ex.Types` — loadable types still register, unloadable ones are skipped |
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
none exists in the repo today, and the quirk-2 hooks are `internal`.
Plain `InternalsVisibleTo` with no `PublicKey=` suffices: nothing in the
tree is strong-named (verified — no `SignAssembly` anywhere). The test
`.csproj` carries its own `TechnitiumLibrary` `<Reference>`/`HintPath`
entries mirroring `DnsServerCore`'s: plain file references do not
reliably flow transitively through a `ProjectReference` at compile time,
and the test project is a new file, so the entries add zero merge
surface. Shared fixtures live in a `Fixtures/` folder inside the test
project; promote to a separate infrastructure project only when a second
test project exists to share them (don't pre-build the abstraction).

**The poisoned-app fixture is slice 2's hard part, not the production
diff.** The mechanism, verified: `DnsApplicationAssemblyLoadContext`
discovers an app by enumerating `*.deps.json` in the application folder
and resolves dependencies through `AssemblyDependencyResolver` — a bare
DLL with no `.deps.json` loads *nothing*, silently. A type that merely
throws in its *constructor* was already isolated before the patch, so a
constructor-throw fake produces a test that passes on unpatched code and
proves nothing. The fixture is therefore publish-shaped output staged
into a temp application folder — the app DLL, its `.deps.json`, and
`dnsApp.config` — with one referenced helper DLL deliberately deleted
after publish, loaded through `DnsApplication`'s *own production load
path* against a fake `IDnsServer` (a small interface; no full server or
Kestrel needed — the sweep runs synchronously in the constructor). A
separately-built test `AssemblyLoadContext` would bypass the production
path and test nothing. Assertions key off registration outcomes (which
types registered, which were skipped), never exception types — the
unpatched and patched sweeps throw *different* exceptions (see the patch
inventory). `patches/dns-server/pr1-repro/` in content-filter already
contains exactly this scenario (AlphaApp/BravoApp/CharlieApp plus a
deliberately-missing helper assembly); slice 2 ports it into
`DnsServerCore.Tests/Fixtures/`, adding the `dotnet publish`
fixture-build step no existing automation provides and rewriting the
repro `.csproj` HintPaths that assume the old container layout — this
porting must happen before slice 11 deletes `patches/`.

### Dispatch extraction (decided, not open)

`WebServiceAppsApi` is a private sealed class nested in the monolithic
`DnsWebService`, whose constructor does filesystem I/O and whose pipeline
is a real Kestrel host — patch 0003's `CallAppApiAsync` as written is
untestable at any granularity below a running server. The RFC decides the
seam now rather than mid-slice: a `static internal DnsAppApiDispatcher`
with zero `HttpContext` or `DnsWebService` involvement. Static, not
instantiated: each dispatch is independent, there is no per-instance
state to hold, and RFC-015 replaces this whole surface — the cheaper of
two equally testable shapes is less code to delete.

The dispatch runs in **two phases**, preserving 0003's deliberate
security property — *a request that will be denied is never buffered
into memory* (the patch checks permission against a body-less probe
before `ReadBoundedBodyAsync` ever runs; a one-shot "parsed request in,
result out" signature would silently regress this):

1. **Authorize** — inputs: the app table as
   `IReadOnlyDictionary<string, DnsApplication>` (the shape
   `DnsApplicationManager.Applications` already exposes; a lookup
   delegate wrapping a dictionary is indirection that buys nothing),
   the request metadata *without body*, and the two distinct permission
   authorities 0003 actually consults — the resolved handler's own
   `GetRequiredAccess` on the body-less probe, and one permission-check
   delegate over `AuthManager` (the only collaborator that earns a
   delegate: its constructor does real config-file I/O). Produces a
   verdict.
2. **Invoke** — only on a permitted verdict does the adapter read the
   bounded body and pass the full request through the dispatcher to the
   handler.

The result is a five-outcome discriminated value — `NotFound`,
`Ambiguous`, `AccessDenied`, `HandlerThrew`, `Success` — which the thin
HTTP adapter in `WebServiceAppsApi.CallAppApiAsync` maps to writes;
`WebServiceAppsApi` itself becomes `internal` for adapter-level tests.
**Stated functional change:** 0003 as authored is internally
inconsistent — ambiguous-handler and handler-throw write raw 500 text
while not-found and access-denied throw `DnsWebServiceException` into
the generic exception handler; the fold unifies all four failure modes
through the one outcome classification. Slice 4's tests include the
property that a denied request never invokes the body reader. Patch
0003 is folded *into this shape* — fold-verbatim-then-refactor was
considered and rejected as scheduled rework.

### Quirk fixes (issue #5)

1. **Install/load error visibility — total and partial.** Two distinct
   failure shapes, both currently silent. This slice is independent of
   the dispatcher work: `/api/apps/install|update` are separate routes
   from `/api/apps/call`.
   - *Zero types register* (the sweep throws, or nothing implements the
     interfaces): today the sweep catches, logs, and returns success —
     the fix *adds* a zero-registration check after the sweep that
     throws (new logic, not preserved behavior). No new envelope
     plumbing exists or is needed: install/update are verified not to be
     on the raw-passthrough route list, so the existing
     `WebServiceApiMiddleware` / `WebServiceExceptionHandler` pair turns
     the throw into `status:"error"` + `errorMessage`. Scope honesty:
     that envelope exists only for install/update — server-*startup*
     loading has no HTTP response; there the same throw lands in
     `DnsApplicationManager.LoadAllApplicationsAsync`'s existing per-app
     catch and becomes a real logged error instead of a silent empty
     load, which is the correct behavior for that path.
   - *Partial load* (post-0001: some types register, some are skipped):
     `DnsApplication` exposes a `readonly IReadOnlyList<AppLoadWarning>`
     built once during the constructor sweep — matching the class's
     established build-then-freeze convention (no mutable collection
     escapes), with `AppLoadWarning` a small record. Content honesty:
     on the assembly-load-failure branch the failed type's *name is not
     recoverable* — `ex.Types` holds `null` at the failed positions and
     the loader exception names the missing dependency assembly, not the
     type that referenced it — so those warnings carry the
     loader-exception summary only; a type name appears only on the
     constructor-throw branch, where `classType.FullName` is in scope.
     No metadata-reader engineering to recover names. The apps API
     includes a `"loadWarnings"` array whenever non-empty — on
     `status:"ok"` too — via the one shared `WriteAppAsJson` helper
     (list/install/update all flow through it; it becomes `internal` so
     tests call it directly). Reload needs no survival story: update
     constructs a fresh `DnsApplication`, so warnings are recomputed
     each load. This closes the gap 0001 would otherwise reintroduce:
     2-of-3 types loading (blocking handler's sibling failed) is exactly
     the "blocking quietly off" scenario the pivot was about. Test:
     2-of-3 partial load surfaces the warning in the response; healthy
     path regression test asserts no warnings.

2. **Special-use names defer-to-blocking.** This is a
   pipeline-*ordering* change composing with an existing flag — and the
   forward-name subset it applies to is **not an addressable set in
   today's code**, so the design creates one. Today
   `DnsServer.AuthoritativeQueryAsync` returns `SpecialZoneManager`'s
   synthetic answer before the blocking handlers ever run, gated by the
   existing `LocallyServedDnsZones` bool (default `true`) — flipping
   that off kills all special-zone interception and sends the queries to
   real recursion. Design:
   - **The subset, concretely.** `SpecialZoneManager` holds two disjoint
     mechanisms: `_locallyServedZones` (RFC 6303-family *reverse* zones
     interleaved, comment-distinguished only, with four *forward* names —
     `localhost`, `home.arpa`, `resolver.arpa`, `service.arpa` —
     answered through real zone lookups) and `_nonExistentZones`
     (`test`, `invalid`, `local`, `onion` — answered by suffix-match
     NXDOMAIN synthesis in a structurally separate path in `Query`).
     All eight forward names are the same blocking-quietly-pre-empted
     risk this quirk closes, so the subset is **all eight** (the setting
     is named for special-use names, not one RFC), owned where the
     knowledge already lives: `SpecialZoneManager` gains a single named
     forward-special-use set and an `internal
     IsForwardSpecialUseName(qname)` predicate, and the defer gate
     applies at *both* of `Query`'s return shapes — the zone lookup and
     the NXDOMAIN synthesis. Reverse zones never defer.
   - New narrow setting `specialUseNamesDeferToBlocking` (default
     `false`), only effective when `LocallyServedDnsZones` is `true`
     (when that is `false` nothing is intercepted and this setting is
     moot — interaction stated and tested).
   - **Insertion point.** `AuthoritativeQueryAsync` is also invoked with
     `remoteEP == null` by the resolver's internal cache path
     (`ResolverDnsCache`), where deferral would be semantically wrong
     (client-allow logic applied to the server's own lookups) and
     NRE-prone (`IsAllowedAsync` dereferences `remoteEP.Address` when a
     blocking-bypass list is configured). The defer runs only for
     client-originated queries (`remoteEP` non-null); the internal path
     keeps today's behavior unconditionally.
   - **The blocking pass, named.** It is two private methods —
     `IsAllowedAsync` then `ProcessBlockedQueryAsync` — invoked today
     only from `ProcessRecursiveQueryAsync`; making them reachable from
     the special-zone branch is new composition on the same class, not a
     flag flip. The pass is *tri-state*: `IsAllowedAsync` can return
     false while `ProcessBlockedQueryAsync` still returns null (an app
     vetoes without producing a block response). Blocked-with-response →
     the blocking response. Anything else — allowed, or
     vetoed-with-no-response — → **the special-zone synthetic answer,
     never recursion.** This invariant is the load-bearing line: a naive
     "skip the shortcut" leaks RFC-1918 reverse lookups and `localhost`
     queries to upstream resolvers, and so would letting the
     null-block-response case fall through the way the recursive path
     does today.
   - **Testability.** The ordering decision extracts into a small
     `internal static` unit — (defer flag, subset membership, the
     synthetic response, a blocking-pass thunk) → response — so the
     tests below construct no `DnsServer`; `AuthoritativeQueryAsync`
     becomes a thin call site, the same shallow-adapter/deep-decision
     pattern the dispatcher extraction uses.
   - Tests (four): blocked special-use name gets the blocking response;
     unblocked special-use name still gets the synthetic answer with no
     recursion attempted; vetoed-with-null-block-response still gets the
     synthetic answer with no recursion; `LocallyServedDnsZones=false`
     behaves identically with the new setting on or off.

### CI (issue #3)

**TechnitiumLibrary provisioning comes first — without it nothing
builds.** Every job (and every implementer) needs a sibling clone of
`TechnitiumSoftware/TechnitiumLibrary` at a **pinned tag paired with the
current upstream base** (today: the tag matching DnsServer v15.4.0, as
content-filter's Dockerfiles already pin), with
`TechnitiumLibrary.ByteTree`, `.Net`, and `.Security.OTP` built Release
before this solution restores. A checked-in script
(`build/get-technitiumlibrary.sh`) reads the pin from
`build/technitiumlibrary.version` (one line, the upstream tag — the path
is named now so slices 8–11 and the sync policy agree on one file) and
serves both CI and local setup; the pin bumps only alongside upstream
merges (sync policy below). No submodule — that would be fine for CI but
the `HintPath`s expect a sibling path and editing every `.csproj`
creates permanent merge surface for zero gain.

Workflow shape on push/PR: checkout → provision TechnitiumLibrary (pin) →
build `DnsServerApp.csproj`, which transitively covers the Linux project
set (`DnsServerCore`, `.ApplicationCommon`, `.HttpApi`) — never
`dotnet build DnsServer.sln`, which drags in the Windows-only projects
and fails on Linux runners → build (not test) the `Apps/*.csproj`
plugin projects, so an in-tree `ApplicationCommon` change that would
break every plugin fails CI instead of waiting for the manual SDK
re-survey → run `DnsServerCore.Tests`. NuGet and TechnitiumLibrary
build output are cached keyed on the pin **plus runner OS and .NET SDK
major version** — prebuilt Release DLLs surviving an SDK image bump
would surface as runtime weirdness, not a build failure.

**Image build:** the repo's existing `Dockerfile` does not build — it
copies a host-side `publish/` directory. CI uses a new, self-contained
multi-stage `Dockerfile.sovereign` (SDK stage: provision
TechnitiumLibrary + `dotnet publish DnsServerApp` → runtime stage),
inheriting its logic from content-filter's `Dockerfile.patched-server`,
which retires. Upstream's `Dockerfile` is left untouched (no merge
surface). The image is labeled with `org.opencontainers.image.revision`
(the git SHA) and `org.opencontainers.image.source` pointing at
`coreyleavitt/urostigma` — not upstream's repo, which is what the
inherited label claims today — so every published image is traceable to
its commit and its actual source. The publish job runs only on `develop`
and **requires the test job to have passed on the same commit**. Slice 9
also repoints this repo's own `docker-compose.yml` (which still names
upstream's Docker Hub image) at the published image.

**Tagging:** two mechanisms, no allocation race.
- The moving `develop` tag publishes per green `develop` commit for the
  operator's own compose convenience, under a queued (not cancelling)
  `concurrency` group — Actions does not serialize runs per branch by
  default, and two rapid pushes finishing out of order would otherwise
  silently revert the moving tag to an older image.
- The immutable `<upstream-version>-sovereign.<n>` (e.g.
  `15.4.0-sovereign.1`) is cut by pushing a **git tag of the same name**;
  the tag push triggers the publish, `<n>` is chosen by the human
  cutting the tag, and the git tag doubles as the ref content-filter
  clones (below) — the image version and the source pin are one fact.
  The image's consumers are the RFC-012 harness and the operator's
  deployment — no consumer-facing tag ceremony beyond that (scope
  decision in Consumer cutover below).

**Build auth:** this repo's publish job needs no PAT — the default
`GITHUB_TOKEN` with an explicit `permissions: packages: write` block
pushes to its own GHCR namespace. The classic PAT (`repo` read +
`read:packages`) exists solely for content-filter's cross-repo needs —
the private clone in its Docker builds and any image pull; stored as a
secret in content-filter and used only on
`push`/`workflow_dispatch`/same-repo-PR triggers, never
`pull_request_target` (content-filter is public). Fine-grained PATs
rejected — GHCR support for them has documented gaps. In the Docker
builds themselves the PAT is threaded through BuildKit secret mounts
(`RUN --mount=type=secret,id=gh_token git clone https://x-access-token:$(cat /run/secrets/gh_token)@github.com/...`,
with `docker build --secret id=gh_token,...`), never a build
`ARG`/`ENV` — a token in a clone URL persists in image layer history.

### Upstream sync policy (issue #4)

A written policy at `docs/UPSTREAM-SYNC.md`:

- **Triggers, mechanically:** watch `TechnitiumSoftware/DnsServer`
  releases (GitHub release notifications on the upstream repo). A release
  whose notes mention a CVE or security fix starts a sync within one
  week; feature releases are merged only when something in them is
  wanted. The policy binds retroactively: a qualifying security release
  landing while slices 1–9 are mid-flight triggers the merge immediately,
  ahead of slice 10 writing the policy down.
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
change that ends patch application — with one honest C# edit an earlier
draft miscounted as zero:

- `Dockerfile.patched-server` **retires outright** — deleted, not
  repointed. `Dockerfile.sovereign` in this repo now serves its exact
  purpose, and keeping a parallel copy in content-filter recreates the
  silent-drift problem this RFC's Motivation names. ("Retires" in the CI
  section and "modified" here were contradictory in the earlier draft;
  this resolves it: deleted.)
- The six remaining Dockerfiles change their clone source to
  `coreyleavitt/urostigma` at the sovereign git tag (one checked-in
  reference in content-filter — a build `ARG` fed from one file — bumped
  deliberately; rollback = repoint it; an unpinned clone of `develop`
  would make every fork commit a latent content-filter CI break) and
  drop the `git apply` loop. The two that `COPY patches/dns-server/`
  wholesale into their build context (`Dockerfile.integration-test`,
  `Dockerfile.perf-comparison`) exist to ship `Dockerfile.patched-server`
  to the fixture — that `COPY` goes away with it.
- `BaseTechnitiumFixture` hardcodes the `patches/dns-server` directory
  and the `Dockerfile.patched-server` filename; it is rewritten to clone
  this repo at the pin and build `Dockerfile.sovereign` — a real, scoped
  C# change (two methods), preserving its built-not-pulled freshness
  property.
- Private-clone auth: the PAT via BuildKit secret mounts per Build auth
  above (never `ARG`/`ENV`).
- `build-dns-server.sh` is rewritten the same way (its `PATCHES_DIR`
  machinery goes away), and
  `docs/getting-started/explain-api-provisioning.md` — a live operator
  doc — stops telling operators to "patch" the server.

Rejected as consumer-population-zero engineering (recorded so round 2
doesn't re-litigate): `COPY --from` DLL extraction, NuGet packaging of
`ApplicationCommon`, fixture image-pulling with tag-bump discipline,
rollback runbooks, and the GHCR Actions-access grant. Revisit at RFC-013's
version contract if the engine ever gains real consumers.

**History preservation** — before `patches/dns-server/` is deleted:
`UPSTREAM.md` (the only record of PR #2092's submission and decline) is
copied to this repo as `docs/UPSTREAM-HISTORY.md`; `pr1-repro/` has
already been ported into test fixtures by slice 2.
`docker-compose.example.yaml` updates its comments/source in passing and
keeps working since the image build survives (via `Dockerfile.sovereign`
per the bullets above).

## Slices

Sequencing note: content-filter appears here only because its Dockerfiles
are the last thing still building upstream+patches — once quirk fixes
land, that recipe produces a *different server* than this repo, which
would poison RFC-012's characterization baseline. The fix is one slice of
clone-source changes plus one scoped fixture edit, deliberately minimal
(see the scope decision in Consumer cutover). Slices 1–10 unblock RFCs
011/012/013; nothing in the engine program waits on slice 11. The folded
0002/0003 surfaces are transitional — RFC-015 replaces them — but the
operator's deployment uses them today, so the fork carries them until
then. Round-2 note on slice boundaries: slices 3–4 split along the
code's actual discovery/dispatch boundary (patch 0002 is the interface
file *alone* — all sweep-discovery wiring lives in 0003's
`DnsApplication.cs` hunk, so a 1:1 patch-to-slice fold would leave slice
3's test with nothing to discover), and quirk 2 splits into its
classification seam and its ordering change — both are places a single
slice would have had no clean RED.

Each slice is a `/tdd` unit where behavior changes; slices 8–9 are
infrastructure verified by execution (a dry run of the same commands,
then a green Actions run, then a scratch `docker pull` with no ambient
credentials), not by xUnit. CI lands after the behavior slices
deliberately: local `dotnet test` is the safety net during 1–7; 8–9
automate what is already green.

1. **Buildable ground.** `build/get-technitiumlibrary.sh` +
   `build/technitiumlibrary.version`; the Linux projects build locally
   from a fresh clone by following one documented step (via
   `DnsServerApp.csproj`, never the `.sln`); found `DnsServerCore.Tests`
   (pinned packages, plain `InternalsVisibleTo`, its own
   TechnitiumLibrary `Reference` entries, `Fixtures/` folder) with one
   smoke test green. (Deliberately mixes execution-verified provisioning
   with one TDD unit.)
2. **Poisoned-app fixture; fold 0001.** Port `pr1-repro` into
   `Fixtures/` with a `dotnet publish` fixture-build step
   (publish-shaped: DLL + `.deps.json` + `dnsApp.config`, helper DLL
   deleted); write the isolation test through `DnsApplication`'s own
   production load path and watch it fail on the unpatched sweep,
   asserting registration outcomes (a constructor-throw fake is *not*
   acceptable RED — it passes unpatched); fold 0001; green. Delete stale
   branch `fix/app-type-discovery`.
3. **Fold 0002 + 0003's discovery hunk.** 0002 is the interface file
   alone (corrected copyright header); the sweep wiring — handler list,
   `is IDnsApplicationApiHandler` check, ambiguity property — lives in
   0003's `DnsApplication.cs` hunk and folds here with it. Tests: a fake
   handler is discovered by the sweep; two handlers surface the
   ambiguity property.
4. **`DnsAppApiDispatcher`; fold the rest of 0003.** The two-phase
   static dispatcher per Design, route registration, thin adapter.
   Tests: app found/missing/ambiguous, permission denied, handler
   invoked, handler-throw classified — plus the property that a denied
   request never invokes the body reader; `WebServiceAppsApi` becomes
   `internal`.
5. **Quirk 1: install/load visibility** (independent of slice 4).
   Zero-registration check added after the sweep → `status:"error"` via
   the existing exception envelope (no new plumbing); partial load →
   `IReadOnlyList<AppLoadWarning>` surfaced through `WriteAppAsJson`
   (made `internal`); 2-of-3 test with the poisoned fixture;
   healthy-path regression.
6. **Quirk 2a: the subset seam.** `SpecialZoneManager`'s named
   forward-special-use set + `IsForwardSpecialUseName` predicate, with
   the defer gate reachable at both `Query` return shapes;
   classification tests (forward names in, reverse zones out).
7. **Quirk 2b: the ordering change** per Design — extracted ordering
   unit, client-originated-only insertion, blocking pass made reachable:
   the four tests including both no-recursion invariants and the
   `LocallyServedDnsZones` interaction.
8. **CI: build + test workflow** with TechnitiumLibrary provisioning,
   the Apps build-only pass, and pin+OS+SDK cache keys; verification by
   execution.
9. **CI: image publish.** `Dockerfile.sovereign` with `revision` and
   `source` labels; moving `develop` tag under a queued concurrency
   group, gated on tests; git-tag-triggered immutable `-sovereign.<n>`;
   `GITHUB_TOKEN` with `packages: write`; repoint this repo's
   `docker-compose.yml`; verify with a scratch pull using the documented
   auth path.
10. **Sync policy doc** (`docs/UPSTREAM-SYNC.md`) per the design, plus
    `docs/UPSTREAM-HISTORY.md` preserving the PR #2092 record; delete
    the inherited `.github/FUNDING.yml` (upstream's sponsorship pointer)
    in passing.
11. **Cutover, in content-filter,** per the Consumer cutover design and
    in this order: six Dockerfiles repoint to the sovereign git tag with
    secret-mount auth; the two whole-tree `COPY`s drop;
    `BaseTechnitiumFixture` rewritten (clone at pin, build
    `Dockerfile.sovereign`); `Dockerfile.patched-server` deleted;
    `build-dns-server.sh`, `docker-compose.example.yaml`, and the
    provisioning doc updated; **then** delete `patches/dns-server/`
    (history preserved per Design); full content-filter CI green.

## Open questions

None. Round 1 resolved all three original open questions (dispatch seam:
extract now; RFC 6761 surface: narrow flag with ordering semantics and
the no-recursion invariant; tagging: `<upstream>-sovereign.<n>`). Round
2 carried no forks either — every finding had a clear best fix, applied
above (ledger in the handoff doc).
