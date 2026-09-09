# RFC-010 Fork sovereignty — handoff

- **Stage:** 3 tdd — slice grind in progress (started 2026-09-09)
- **Rounds:** 3 complete (1–2 on 2026-08-13; 3 on 2026-09-09, Fable team)
- **Resume:** `/loop implement the next unimplemented RFC slice with /tdd, following the standing rules; after each slice report one progress line (e.g. "slice 4/13 done, 9 remaining"); stop when every slice is implemented`
  (run from ficus/urostigma; RFC is docs/rfcs/010-fork-sovereignty.md)

## Slices (13 after round-3 re-slicing)
- [x] 1 Buildable ground + liveness proof — commits 2c130890 + 4c473245; liveness: published artifact booted in stock aspnet:10.0 container, dig answered NOERROR on example.com; 3 tests green (AuthZoneManager.GetParentZone via InternalsVisibleTo)
- [x] 2 Poisoned-app fixture + fold 0001 — commit 70ef70d6; RED proven on unpatched sweep (healthy AlphaApp dropped with whole assembly), GREEN after fold (2 of 3 types register); suite 4/4; stale branch fix/app-type-discovery deleted from origin
- [x] 3 Fold 0002 + 0003 discovery hunk — commits 1ee7dd0e (interface, fork header) + 0916ee16 (sweep wiring + tests); suite 6/6; ambiguity shape: `DnsApplicationApiHandler` null + `DnsApplicationApiHandlerAmbiguous` true on >1, discovery never throws
- [x] 4 DnsAppApiDispatcher + 0003 fold — commits 34a0efd5 + 40760c84; 7 dispatcher-pure tests incl. denied-never-reads-body; suite 13/13; WebServiceAppsApi now internal; adapter maps DnsAppApiAccess (dispatcher stays free of PermissionFlag/AuthManager); Outcome carries Exception for HandlerThrew logging; fixture publisher serialized under static lock (xunit parallel race fix)
- [x] 5 Quirk 1 — commits 07d45fd4 + 9b07b08f + 8cd73f0b; 5 behaviors RED→GREEN; suite 18/18; AppLoadWarning(Message, TypeName=null); WriteAppAsJson internal static with Version param; loadWarnings surfaces on list/install/update alike (RFC-literal, lines 293–296 — the round-1 "install/update only" note was superseded; verified correct call); TestDnsServerFactory fixture available (~150ms, no sockets)
- [x] 6 Quirk 2a — commit 1d229685; eight-name set confirmed exact in SpecialZoneManager; suffix-inclusive predicate landed; suite 34/34; call-site recon confirms deferEligible must be an explicit param (remoteEP null until ANY_0 coercion at 3913, after the special branch)
- [ ] 7 Quirk 2b: the setting exists — sovereign.config sidecar + WebServiceSettingsApi get/set (in progress: delegated agent)
- [ ] 8 Quirk 2c: ordering change (two-delegate unit, deferEligible, terminal blocked + tag)
- [ ] 9 CI: build + test (Apps build-only pass; pin+OS+SDK cache keys)
- [ ] 10 CI: image publish (tag workflow self-gated; docker run + dig/HTTP probe before compose repoint)
- [ ] 11 Sync policy doc + UPSTREAM-HISTORY; delete FUNDING.yml
- [ ] 12 Cutover 1 — repoint: six Dockerfiles + workflow/script --secret threading + pin ARG; CI green
- [ ] 13 Cutover 2 — baked-clone COPY successor; fixture two-tier rewrite; delete patched-server + patches/; operator redeploy

## Grind environment notes (2026-09-09)
- dotnet SDK 10 user-installed at `~/.dotnet` (not on default PATH — export `PATH="$HOME/.dotnet:$PATH"`); set `TMPDIR` under /home for big downloads (/tmp is a nearly-full tmpfs).
- TechnitiumLibrary cloned+built at `~/projects/dotnet/ficus/TechnitiumLibrary` (pin dns-server-v15.4.0); per-project OutputPath lands all five DLLs in `bin/`.
- Root `.gitignore` had `[Bb]uild/` swallowing `build/` — un-ignored via `!/build/` at EOF; watch for the same on other root dirs.
- aspnet:10.0 image runs as root → container port 53 binds without extra caps (slice-10 precedent).

## Owner scope decision (2026-08-13, mid-round-1)
No external consumers exist and none are wanted as a constraint
("we dont have any real consumers yet except for me and I dont care").
Consumer-durability machinery removed and recorded as rejected in the
RFC; rounds 2–3 honored this. Roadmap runs as parallel engine/host
tracks (MIGRATION.md "Tracks").

## Open forks (awaiting Corey)
- none — all three rounds' findings carried confident recommendations, applied

## Key decisions (round 3, 2026-09-09)
- **True upstream base stated:** develop = v15.4.0 + 15 unreleased
  upstream commits (never published/booted anywhere); content-filter
  clones the tag, so the recipes already differ today;
  `<upstream-version>` defined as latest release tag ancestor;
  TechnitiumLibrary pin still valid (verified). Upstream shipped
  nothing since v15.4.0 — retroactive sync clause not triggered.
- **Liveness proofs added, no resequencing:** slice 1 gains
  publish+boot+answer-one-query; slice 10's definition of done runs the
  pulled image (dig + HTTP probe) before the compose repoint; Non-goals
  now distinguish artifact boot liveness (in scope) from wire
  characterization (RFC-012); slice 13 ends with the named operator
  redeploy step.
- **Settings persistence = fork-owned sidecar** (`sovereign.config`,
  JSON, missing = defaults) — NOT a dns.config version bump: upstream
  owns the single linear version byte and will claim 6 itself
  (positional-misparse hazard on merge). API-only exposure via
  WebServiceSettingsApi get/set; explicitly no cluster propagation, no
  console UI. Own slice (7) so the plumbing isn't discovered
  mid-ordering-change.
- **Dispatcher: single entry point** `DispatchAsync(apps, metadata,
  permissionCheck, bodyReader, ct)` — internal two-phase; bodyReader
  delegate invoked only post-permit (denied-never-reads-body test is
  dispatcher-pure); handler captured at authorize, never re-resolved;
  GetRequiredAccess throw → HandlerThrew; no-handler → NotFound;
  adapter writes raw HTTP statuses (never the 200-rewriting envelope).
- **Ordering unit takes two delegates** (isAllowed, processBlockedQuery)
  and collapses the tri-state itself — a single thunk made the
  vetoed-null test vacuous. Defer gated by explicit `deferEligible`
  caller intent (remoteEP nullability fails both directions: ANY_0
  sentinel serves internal probes AND DoH-fallback clients; sentinel
  substituted pre-gate). Blocked response is terminal (bypasses
  CNAME-chase/forced-recursion reprocess loop) and keeps its Blocked
  tag; Authoritative stamp on synthetic branch only.
- **IsForwardSpecialUseName is suffix-inclusive and static** — both
  mechanisms answer subtrees; equality-only would exempt printer.local
  etc. while all tests passed. Subdomain + reverse-zone classification
  tests added.
- **Quirk 1 recovery:** zero-registration check in LoadApplicationAsync
  (dispose-then-throw — in-ctor throw leaks the collectible ALC);
  uninstall deletes the folder of a never-loaded app (else the only
  recovery, install-over, wipes dnsApp.config = the filtering policy).
  WriteAppAsJson → internal static with version parameter; slice 4/5
  visibility coordination noted.
- **Fixture auth is runtime, not build-time:** integration/perf
  Dockerfiles bake a urostigma clone at build time (secret mount,
  successor of the dropped COPY); fixture prefers baked tree, falls
  back to host clone with ambient dev creds; fixture stays
  credential-free. Dockerfile.sovereign must stay classic-builder
  compatible (Testcontainers has no BuildKit).
- **Tag-push publish runs its own build+test jobs** (needs: can't see
  another run; cross-run checks interrogation rejected); non-cancelling
  concurrency = latest-wins, not serialization (stated). Invoking
  surfaces for --secret named: ci.yaml ×3, perf-comparison.yaml,
  release.yaml ×2, build.sh, build-dns-server.sh; PAT creation a named
  prerequisite.
- Conflict-ownership list completed now (DnsApplicationManager.cs,
  SpecialZoneManager.cs, DnsServer.cs, WebServiceSettingsApi.cs added);
  InternalsVisibleTo via attribute-only file; GPLv3 §5 claim corrected
  (git history is the modification record); slices 11→13.

## Key decisions (rounds 1–2, 2026-08-13)
- TechnitiumLibrary: pinned sibling clone via checked-in script
  (build/technitiumlibrary.version); never a submodule
- Dispatch seam extracted now; fold 0003 into the extracted shape
- Defer-to-blocking subset = all eight forward special-use names, owned
  by SpecialZoneManager; four ordering tests incl. tri-state
- LoadWarnings: readonly IReadOnlyList built in ctor; type name only on
  ctor-throw branch; envelope scope = install/update only
- Slices split along real code boundaries (0002 is interface-only;
  discovery wiring is 0003's DnsApplication.cs hunk)
- Cutover: Dockerfile.patched-server deleted outright; sovereign git
  tag doubles as image version and clone pin; BuildKit secret mounts
- Image: Dockerfile.sovereign; revision + source labels; publish gated
  on tests; GITHUB_TOKEN packages:write in-repo; classic PAT only for
  content-filter cross-repo
- Upstream's 0001 objection engaged in-text; partial-load with visible
  warnings chosen over total-abort
- Prior state: patches NOT on develop; 0001 only on stale branch
  fix/app-type-discovery (deleted after slice 2)
- Windows projects out of CI; linux/amd64 only; Apps-permission
  coarseness deferred to issue #24

## Review ledger — round 3 (Fable team)
| id | sev | finding (lens) | status | resolution |
|----|-----|----------------|--------|------------|
| 46 | high | IsForwardSpecialUseName match semantics unspecified; exact-match defeats the quirk for subdomains (depth/design) | fixed | Suffix-inclusive contract; slice 6 tests |
| 47 | high | Setting has no persistence/API producer — dark mechanism; dns.config version byte is upstream-owned (liveness/breadth/feas/depth) | fixed | sovereign.config sidecar; slice 7 |
| 48 | high | Fixture clone is runtime; BuildKit secrets are build-time; CI green unreachable (breadth/feas) | fixed | Baked-clone two-tier; slice 13 |
| 49 | high | Liveness inversion: runnable-server proof deferred to slice 9; develop never published/booted (liveness) | fixed | Slice-1 publish+boot proof; slice-10 run proof |
| 50 | high | remoteEP non-null ≠ client-originated (ANY_0 sentinel both ways; substituted pre-gate) (design/feas/depth) | fixed | deferEligible caller intent |
| 51 | med | Single blocking thunk makes tri-state test vacuous (design) | fixed | Two-delegate unit |
| 52 | med | Denied-never-reads-body test unwritable: body read assigned to untestable adapter (depth/feas/design) | fixed | Single-entry dispatcher owns bodyReader |
| 53 | med | Invoke callable without Authorize; inter-phase handler carrier unspecified (design/depth) | fixed | One entry point; handler captured at authorize |
| 54 | med | Zero-registration throw dead-ends recovery (uninstall no-ops; install-over wipes config) + ALC leak (depth) | fixed | Dispose-then-throw in LoadApplicationAsync; uninstall-unloaded |
| 55 | med | Blocked response re-enters reprocess loop → CNAME-chase recursion (depth) | fixed | Terminal blocked + tag; test assertions |
| 56 | med | Tag-push workflow can't see develop run's test gate (feas) | fixed | Self-contained build+test in tag workflow |
| 57 | med | True upstream base misstated; recipes already differ; upstream-version undefined (breadth) | fixed | Base paragraph + definition |
| 58 | med | Workflow files + build.sh missing from cutover list; slice 11 blast radius → split (feas/breadth) | fixed | Slices 12/13 |
| 59 | med | Settings plumbing blast radius → slice 7 split from ordering (feas) | fixed | Slices 7/8 |
| 60 | med | WriteAppAsJson seam doesn't exist as specified (private instance on private nested) (design/feas) | fixed | internal static + version param |
| 61 | low | GetRequiredAccess throw unclassified in authorize phase (design) | fixed | → HandlerThrew |
| 62 | low | No-handler outcome unmapped; adapter write targets unnamed (depth) | fixed | → NotFound; raw statuses stated |
| 63 | low | Tag restamping would misclassify blocked as Authoritative (depth) | fixed | Synthetic-branch-only stamp |
| 64 | low | Live-table re-resolution between phases (depth) | fixed | Captured handler |
| 65 | low | Queued-concurrency semantics; classic-builder constraint; InternalsVisibleTo mechanism; §5 wording; slice-4/5 visibility overlap; PAT prerequisite; operator redeploy step (all) | fixed | In place |

## Review ledger — rounds 1–2
(22 + 23 rows, all fixed — see git history of this file at commit
6e5dc077 for the full tables; nothing is open.)
