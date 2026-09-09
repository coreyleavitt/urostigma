# RFC-010: Fork sovereignty — patches become commits, builds trust this repo

- **Status:** Accepted (architect rounds 1–2 applied 2026-08-13, round 3
  applied 2026-09-09; ledgers in `010-fork-sovereignty.handoff.md`)
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
  unit/service-level. This non-goal defers *characterization*, not
  artifact **boot liveness**: "the published artifact starts and answers
  a query" is this RFC's own definition of done for its producer slices
  (1 and 10), and cannot be cited to skip those proofs — RFC-012's
  baseline circularly depends on this repo's image being alive.
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

**The actual upstream base (corrected, round 3).** `develop` is not the
v15.4.0 tag: it sits on upstream master past the release — the v15.4.0
commit plus fifteen unreleased upstream commits (forwarder-selection
rework, DoH receive timeout, a `LogManager.DeleteLogFile`
path-validation fix) beneath this program's doc commits. Content-filter's
Dockerfiles meanwhile clone upstream at the v15.4.0 *tag*, so the two
recipes already build materially different servers today — before any
quirk fix lands. The TechnitiumLibrary pin `dns-server-v15.4.0` still
serves: the delta uses no library API the tag lacks (verified). In the
tag scheme below, `<upstream-version>` means the latest upstream release
tag that is an ancestor of the tagged commit.

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
modified upstream files keep Technitium's header. This prevents
misattribution of new work; the §5(a) modification record is the git
history itself (obligations attach on conveyance, which RFC-020 gates) —
the header convention does not claim to be it.

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
The attribute lands in a new attribute-only source file, not a
`.csproj` edit — the same zero-merge-surface principle as the HintPath
non-goal. Plain `InternalsVisibleTo` with no `PublicKey=` suffices:
nothing in the tree is strong-named (verified — no `SignAssembly`
anywhere). The test
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
porting must happen before slice 13 deletes `patches/`.

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

The dispatch preserves 0003's deliberate security property — *a request
that will be denied is never buffered into memory* (the patch checks
permission against a body-less probe before `ReadBoundedBodyAsync` ever
runs) — **structurally, behind one entry point**, so the property lives
in the tested unit rather than in adapter discipline (round 3: a
two-call surface with the adapter reading the body between calls made
the property untestable — the adapter needs a constructed
`DnsWebService` — and made Invoke-without-Authorize representable):

`DispatchAsync(apps, requestMetadata, permissionCheck, bodyReader, ct)
→ outcome`, where:

- `apps` is the app table as `IReadOnlyDictionary<string,
  DnsApplication>` (the shape `DnsApplicationManager.Applications`
  already exposes; a lookup delegate wrapping a dictionary is
  indirection that buys nothing);
- `requestMetadata` is the request *without body*;
- `permissionCheck` is one delegate over `AuthManager` (the only heavy
  collaborator: its constructor does real config-file I/O). The other
  permission authority — the resolved handler's own `GetRequiredAccess`
  on the body-less probe — is app code the dispatcher invokes itself,
  and a throw from it classifies as `HandlerThrew` (a fifth failure
  path 0003 leaves unguarded);
- `bodyReader` is a delegate (the adapter passes
  `ct => ReadBoundedBodyAsync(...)`) invoked **only after** a permitted
  verdict. The denied-never-reads-body test injects a recording thunk
  and asserts zero invocations — dispatcher-pure, no `DnsWebService`
  construction.

Internally the dispatch is still two-phase (authorize on the probe,
then read body and invoke), but the ordering is unrepresentable at the
boundary rather than adapter convention. The handler resolved at
authorize time is the one invoked — never re-resolved from the live
`ConcurrentDictionary`, so an app updated between phases cannot swap in
a handler that was never authorized (an uninstall in the window
classifies as `HandlerThrew`, matching 0003's existing race window).

The outcome is a five-value discriminated result — `NotFound`,
`Ambiguous`, `AccessDenied`, `HandlerThrew`, `Success` — with two
mappings stated: an app that exists but implements no API handler is
`NotFound` (0003 treated it as a distinct error), and the
`GetRequiredAccess` throw above is `HandlerThrew`. The thin HTTP
adapter in `WebServiceAppsApi.CallAppApiAsync` maps outcomes to writes
on the raw-passthrough route: raw HTTP statuses for the four failures
(the patch's raw-write discipline — never the 200-rewriting JSON
envelope), the handler's own response streamed for `Success`;
`WebServiceAppsApi` itself becomes `internal` for adapter-level tests.
**Stated functional change:** 0003 as authored is internally
inconsistent — ambiguous-handler and handler-throw write raw 500 text
while not-found and access-denied throw `DnsWebServiceException` into
the generic exception handler; the fold unifies all failure modes
through the one outcome classification. Slice 4's tests:
found/missing/ambiguous/no-handler/denied/invoked/handler-throw, plus
the denied-never-invokes-bodyReader property. Patch 0003 is folded
*into this shape* — fold-verbatim-then-refactor was considered and
rejected as scheduled rework.

### Quirk fixes (issue #5)

1. **Install/load error visibility — total and partial.** Two distinct
   failure shapes, both currently silent. This slice is independent of
   the dispatcher work: `/api/apps/install|update` are separate routes
   from `/api/apps/call`.
   - *Zero types register* (the sweep throws, or nothing implements the
     interfaces): today the sweep catches, logs, and returns success —
     the fix *adds* a zero-registration check (new logic, not preserved
     behavior). Placement matters: the check lives in
     `DnsApplicationManager.LoadApplicationAsync` *after* construction —
     dispose the freshly built `DnsApplication` first, then throw; an
     in-constructor throw would skip `Dispose` and leak the collectible
     `AssemblyLoadContext` on every failed load attempt. No new envelope
     plumbing exists or is needed: install/update are verified not to be
     on the raw-passthrough route list, so the existing
     `WebServiceApiMiddleware` / `WebServiceExceptionHandler` pair turns
     the throw into `status:"error"` + `errorMessage`. Scope honesty:
     that envelope exists only for install/update — server-*startup*
     loading has no HTTP response; there the same throw lands in
     `LoadAllApplicationsAsync`'s existing per-app catch and becomes a
     real logged error instead of a silent empty load. **Recovery must
     not dead-end:** a startup-failed app is absent from the loaded
     table, where today `/api/apps/update` refuses ("does not exist")
     and `/api/apps/uninstall` silently no-ops (it deletes the folder
     only for loaded names) — leaving install-over as the sole API
     recovery, and install-over wipes `dnsApp.config`, which for this
     deployment *is* the filtering policy. The fix therefore also makes
     uninstall delete the on-disk app folder when the name is not in the
     loaded table, so a poisoned app can be removed without destroying
     config semantics elsewhere. Test: uninstall of a never-loaded app
     removes the folder.
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
     (list/install/update all flow through it). Round-3 seam
     correction: it is a private *instance* method on a private nested
     class, with one instance dependency (the store version) — "make it
     internal" alone exposes nothing. It becomes `internal static` with
     the version lifted to a parameter, so tests call it without
     constructing the enclosing service; the enclosing class's
     `internal` visibility change lands with whichever of slices 4/5
     goes first (they share `WebServiceAppsApi.cs` — one sentence of
     coordination, not a dependency). Reload needs no survival story:
     update
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
     forward-special-use set and an `internal static
     IsForwardSpecialUseName(qname)` predicate (`static` because the set
     is constant and an instance predicate would drag `DnsServer`
     construction into the classification tests). The predicate is
     **suffix-inclusive** — true iff qname equals a member or ends with
     `"." + member`, OrdinalIgnoreCase, mirroring the existing
     NXDOMAIN-path suffix match — because both mechanisms answer
     *subtrees*, not apexes: an equality-only predicate would defer
     `local` but not `printer.local`, leaving the founding quirk in
     place for exactly the mDNS-spillover traffic that motivated it,
     while every test still passed. The predicate's *coverage* spans
     both of `Query`'s mechanisms' names, but the gate itself lives at
     the `AuthoritativeQueryAsync` consumption point — `Query` needs no
     changes. Reverse zones never defer.
   - New narrow setting `specialUseNamesDeferToBlocking` (default
     `false`), only effective when `LocallyServedDnsZones` is `true`
     (when that is `false` nothing is intercepted and this setting is
     moot — interaction stated and tested).
   - **Insertion point: explicit caller intent, not endpoint
     inference.** Round 3 killed the `remoteEP`-nullability gate: three
     caller classes reach `AuthoritativeQueryAsync` — real client
     endpoints; `null` (`ResolverDnsCache`, where deferral would NRE on
     the bypass list and misapply client-allow logic); and the `ANY_0`
     sentinel, used *both* by internal probes (the cache-refresh path,
     which the recursive pipeline deliberately exempts from blocking,
     and `DirectQueryAsync`) *and* for genuine clients (the DoH real-IP
     fallback) — and the sentinel is substituted for `null` before the
     app-handler block anyway, so nullability cannot express
     "client-originated" in either direction. The defer is gated by an
     explicit `deferEligible` input set `true` only by the top-level
     client query path; every internal caller passes `false` and keeps
     today's behavior unconditionally.
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
   - **The blocked response is terminal.**
     `ProcessAuthoritativeQueryAsync` post-processes authoritative
     returns — CNAME/ANAME chasing and forced recursion on NS-first
     authority — which could rewrite an app's CNAME-shaped block
     response into exactly the recursion the invariant forbids (the
     recursive path returns blocked responses verbatim, un-chased). The
     deferred path's blocked result bypasses that reprocess loop,
     returned directly keyed on its `Blocked` tag. And the call site
     stamps `DnsServerResponseType.Authoritative` on the *synthetic*
     branch only — blocked responses keep the `Blocked` tag
     `ProcessBlockedQueryAsync` already sets, so dashboard counters and
     query-log classification stay correct.
   - **The setting's surface.** A DnsServer setting is not just a field.
     The native pattern persists into `dns.config` — a positional binary
     format with one linear version byte that **upstream owns and bumps
     with its own settings**: a fork-persisted field claiming version 6
     collides with upstream's next layout claiming the same 6, a
     positional-misparse hazard against the operator's real config on
     first post-merge load. So fork-owned settings persist in a
     fork-owned sidecar (`sovereign.config`, JSON, in the config
     folder), read/written alongside `dns.config` — zero touch on the
     upstream serialization region, the same merge-surface philosophy
     as the HintPath and InternalsVisibleTo decisions; a missing
     sidecar means defaults, so fresh installs work. Exposure is
     API-only: `WebServiceSettingsApi` get/set (two small hunks —
     accepted merge surface, joining the conflict-ownership list). No
     cluster propagation (single-operator deployment — stated, not
     forgotten) and no web-console UI (this surface is transitional;
     RFC-015/019 replace the console).
   - **Testability.** The ordering decision extracts into a small
     `internal static` unit — (defer/eligibility flags, subset
     membership, the synthetic response, and **two delegates mirroring
     the real pass**: `isAllowed` and `processBlockedQuery`) → response.
     The unit performs the tri-state collapse itself; a single
     pre-collapsed thunk would make the vetoed-with-null-response test
     vacuous (its inputs indistinguishable from "allowed"), pushing the
     load-bearing invariant into untested adapter composition. Two
     delegates for two genuine async I/O collaborators is the right
     seam — unlike the dictionary-wrapping delegate round 2 removed.
     The tests construct no `DnsServer`; `AuthoritativeQueryAsync`
     becomes a thin call site, the same shallow-adapter/deep-decision
     pattern the dispatcher extraction uses.
   - Tests (four, plus pinned properties): blocked special-use name gets
     the blocking response — asserted to reach the caller
     *un-reprocessed* with its `Blocked` tag intact; unblocked
     special-use name still gets the synthetic answer with no recursion
     attempted; vetoed-with-null-block-response still gets the synthetic
     answer with no recursion; `LocallyServedDnsZones=false` behaves
     identically with the new setting on or off.

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
is named now so slices 9–13 and the sync policy agree on one file) and
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
and **requires the test job to have passed on the same commit**. Slice
10 also repoints this repo's own `docker-compose.yml` (which still names
upstream's Docker Hub image) at the published image — after the run
proof in that slice's definition of done.

**Tagging:** two mechanisms, no allocation race.
- The moving `develop` tag publishes per green `develop` commit for the
  operator's own compose convenience, under a non-cancelling
  `concurrency` group — Actions does not serialize runs per branch by
  default, and two rapid pushes finishing out of order would otherwise
  silently revert the moving tag to an older image. (Semantics honesty:
  a non-cancelling group keeps only the *latest* pending run, discarding
  intermediates — that is latest-wins, exactly the property needed, not
  full serialization.)
- The immutable `<upstream-version>-sovereign.<n>` (e.g.
  `15.4.0-sovereign.1`) is cut by pushing a **git tag of the same name**;
  the tag push triggers the publish, `<n>` is chosen by the human
  cutting the tag, and the git tag doubles as the ref content-filter
  clones (below) — the image version and the source pin are one fact.
  A tag-push run cannot see a develop run's test results (`needs:`
  works only within one workflow run, and cross-run checks
  interrogation is exactly the fragile machinery this RFC keeps
  rejecting), so **the tag workflow contains its own build+test jobs
  and the publish job `needs:` them** — duplicate execution is the cost
  of a self-contained gate. The image's consumers are the RFC-012
  harness and the operator's deployment — no consumer-facing tag
  ceremony beyond that (scope decision in Consumer cutover below).
- **`Dockerfile.sovereign` stays classic-builder compatible** — no
  BuildKit-only syntax (`RUN --mount` etc.) — because content-filter's
  fixture builds it through Testcontainers' Docker-API builder, which
  has no BuildKit secret support. It needs none: TechnitiumLibrary is
  public and the fork source arrives via build context or a pre-baked
  clone.

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
  landing while slices 1–10 are mid-flight triggers the merge
  immediately, ahead of slice 11 writing the policy down.
- **Procedure:** `git merge` from the upstream remote, never rebase. The
  honest rationale: published images carry `image.revision` SHAs and the
  provenance record cites commits — a rebase orphans every SHA anyone has
  written down. (Consumers pull image tags, not commits; the audit trail
  is what needs history stability.)
- **Paired pin:** every upstream merge bumps the TechnitiumLibrary pin to
  the matching release tag in the same PR — the two versions are one
  fact, never independent variables.
- **Conflict ownership:** we own the resolution in files we have diverged
  in — the divergence is knowable now, not "growing": `DnsApplication.cs`,
  `DnsApplicationManager.cs`, `WebServiceAppsApi.cs`, `DnsWebService.cs`,
  `SpecialZoneManager.cs`, `DnsServer.cs`, `WebServiceSettingsApi.cs`;
  the policy doc keeps the authoritative list as it grows further.
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
  and the `Dockerfile.patched-server` filename; it is rewritten to build
  `Dockerfile.sovereign` from a urostigma tree at the pin — a real,
  scoped C# change (two methods), preserving its built-not-pulled
  freshness property. **Round-3 auth correction:** the fixture runs at
  *test runtime* (inside the integration container, or on a developer
  machine) where BuildKit secrets do not exist and CI passes no
  credentials — "clone at test time via secret mount" was a build-time
  answer to a runtime question, and Testcontainers' Docker-API builder
  has no BuildKit support anyway. Two-tier, reusing the fixture's
  existing search pattern: `Dockerfile.integration-test` and
  `Dockerfile.perf-comparison` bake a urostigma clone at the pin into
  their images at *build* time (via secret mount — the legitimate
  successor of the dropped `COPY patches/dns-server/`); the fixture
  prefers that baked tree as its build context, falling back to a host
  clone with ambient developer credentials outside CI. The fixture
  itself stays credential-free.
- Private-clone auth: the PAT via BuildKit secret mounts per Build auth
  above (never `ARG`/`ENV`). The **invoking surfaces** need the
  `--secret` threading too, and slice 12 names them: the `docker build`
  calls in `.github/workflows/ci.yaml` (three), `perf-comparison.yaml`,
  and `release.yaml` (two), plus `build.sh` and `build-dns-server.sh`.
  Creating the classic PAT and storing it as a content-filter secret is
  a named manual prerequisite of the cutover, not an assumed ambient
  fact.
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
would poison RFC-012's characterization baseline — and note the recipes
*already* differ today, since content-filter clones the v15.4.0 tag
while this repo sits past it (see "The actual upstream base"). The fix
is two small slices — a mechanical repoint and one scoped fixture edit —
deliberately minimal (see the scope decision in Consumer cutover). Slices 1–11 unblock RFCs
011/012/013; nothing in the engine program waits on slices 12–13. The
folded 0002/0003 surfaces are transitional — RFC-015 replaces them — but
the operator's deployment uses them today, so the fork carries them
until then. Slice-boundary notes: round 2 split 3–4 along the code's
actual discovery/dispatch boundary (patch 0002 is the interface file
*alone* — all sweep-discovery wiring lives in 0003's `DnsApplication.cs`
hunk) and split quirk 2's classification seam from its ordering change;
round 3 split the setting's existence (7) from the ordering change (8) —
the settings surface is real plumbing an implementer should not
discover mid-ordering-change — and split the cutover into its
mechanical repoint (12) and its fixture-rewrite-plus-deletion (13).

Each slice is a `/tdd` unit where behavior changes; slices 9–10 are
infrastructure verified by execution (a dry run of the same commands,
then a green Actions run, then a scratch `docker pull` with no ambient
credentials), not by xUnit. CI lands after the behavior slices
deliberately: local `dotnet test` is the safety net during 1–8; 9–10
automate what is already green. The load-bearing property — a runnable
server from this tree — goes live at slice 1 and is re-proven at each
producer boundary (slice 10's image run), not discovered at the end.

1. **Buildable ground + liveness proof.** `build/get-technitiumlibrary.sh`
   + `build/technitiumlibrary.version`; the Linux projects build locally
   from a fresh clone by following one documented step (via
   `DnsServerApp.csproj`, never the `.sln`); found `DnsServerCore.Tests`
   (pinned packages, `InternalsVisibleTo` via a new attribute-only
   file, its own TechnitiumLibrary `Reference` entries, `Fixtures/`
   folder) with one smoke test green. Then the proof that closes the
   liveness inversion: `dotnet publish DnsServerApp -c Release`
   succeeds and the published output *boots and answers one query
   locally* — this tree (which sits past v15.4.0, and has never been
   published anywhere) serves DNS from slice 1, not slice 10.
   (Deliberately mixes execution-verified provisioning with one TDD
   unit.)
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
4. **`DnsAppApiDispatcher`; fold the rest of 0003.** The single-entry
   static dispatcher per Design (internal two-phase, bodyReader
   delegate), route registration, thin adapter mapping outcomes to raw
   writes. Tests: app found/missing/ambiguous/no-handler, permission
   denied, handler invoked, handler-throw classified — plus the
   property that a denied request never invokes the bodyReader;
   `WebServiceAppsApi` becomes `internal`.
5. **Quirk 1: install/load visibility** (independent of slice 4's
   routes; shares `WebServiceAppsApi.cs` — the class-visibility change
   lands with whichever goes first). Zero-registration check in
   `LoadApplicationAsync` (dispose-then-throw) → `status:"error"` via
   the existing exception envelope; uninstall deletes the folder of a
   never-loaded app (recovery test); partial load →
   `IReadOnlyList<AppLoadWarning>` surfaced through `WriteAppAsJson`
   (made `internal static`, version parameter); 2-of-3 test with the
   poisoned fixture; healthy-path regression.
6. **Quirk 2a: the subset seam.** `SpecialZoneManager`'s named
   forward-special-use set + `internal static IsForwardSpecialUseName`,
   suffix-inclusive per Design; classification tests: the eight apexes
   in, a subdomain (`tracker.local`) in, a reverse zone
   (`1.0.0.127.in-addr.arpa`) out.
7. **Quirk 2b: the setting exists.** `specialUseNamesDeferToBlocking`
   persisted in the fork-owned `sovereign.config` sidecar (missing file
   = defaults), exposed through `WebServiceSettingsApi` get/set;
   explicitly no cluster propagation, no console UI. Tests: persistence
   round-trip; get/set through the settings API.
8. **Quirk 2c: the ordering change** per Design — extracted ordering
   unit (two delegates, tri-state collapse inside), `deferEligible`
   caller-intent wiring, blocking pass made reachable, terminal blocked
   response: the four tests including both no-recursion invariants, the
   un-reprocessed/`Blocked`-tag assertions, and the
   `LocallyServedDnsZones` interaction.
9. **CI: build + test workflow** with TechnitiumLibrary provisioning,
   the Apps build-only pass, and pin+OS+SDK cache keys; verification by
   execution.
10. **CI: image publish.** `Dockerfile.sovereign` (classic-builder
    compatible) with `revision` and `source` labels; moving `develop`
    tag under the non-cancelling concurrency group, gated on tests;
    git-tag-triggered immutable `-sovereign.<n>` whose workflow runs
    its own build+test gate; `GITHUB_TOKEN` with `packages: write`.
    Definition of done runs the artifact, not just the registry:
    scratch `docker pull` with the documented auth path, then
    `docker run` — one `dig` through the mapped port and one HTTP probe
    of the web service — **before** repointing this repo's
    `docker-compose.yml`.
11. **Sync policy doc** (`docs/UPSTREAM-SYNC.md`) per the design, plus
    `docs/UPSTREAM-HISTORY.md` preserving the PR #2092 record; delete
    the inherited `.github/FUNDING.yml` (upstream's sponsorship pointer)
    in passing.
12. **Cutover part 1 — repoint, in content-filter.** PAT created and
    stored (named prerequisite); six Dockerfiles clone the sovereign
    git tag via secret mounts; `--secret` threading added at every
    invoking surface (`ci.yaml` ×3, `perf-comparison.yaml`,
    `release.yaml` ×2, `build.sh`, `build-dns-server.sh`); the pin ARG
    file checked in. `Dockerfile.patched-server`, the fixture, and
    `patches/` untouched. Content-filter CI green.
13. **Cutover part 2 — fixture and deletion.** The two whole-tree
    `COPY`s become build-time urostigma clones baked into the
    integration/perf images; `BaseTechnitiumFixture` rewritten
    (two-tier: baked tree preferred, host clone fallback; builds
    `Dockerfile.sovereign`); `Dockerfile.patched-server` deleted;
    `docker-compose.example.yaml` and the provisioning doc updated;
    **then** delete `patches/dns-server/` (history preserved per
    Design); full content-filter CI green. Closing step: the operator
    rebuilds and `compose up`s — the live deployment now runs the
    fork's build, completing the title property by a named action, not
    an unstated one.

## Open questions

None. Round 1 resolved all three original open questions (dispatch seam:
extract now; RFC 6761 surface: narrow flag with ordering semantics and
the no-recursion invariant; tagging: `<upstream>-sovereign.<n>`). Rounds
2 and 3 carried no forks either — every finding had a clear best fix,
applied above (ledgers in the handoff doc).
