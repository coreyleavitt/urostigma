# RFC-010 Fork sovereignty — handoff

- **Stage:** 2 architect complete — RFC **Accepted**, ready for stage 3
- **Rounds:** 2 of 2 complete (both 2026-08-13)
- **Resume:** `/loop implement the next unimplemented RFC slice with /tdd, following the standing rules; after each slice report one progress line (e.g. "slice 4/11 done, 7 remaining"); stop when every slice is implemented`
  (run from ficus/urostigma; RFC is docs/rfcs/010-fork-sovereignty.md)

## Slices (11 after round-2 re-slicing)
- [ ] 1 Buildable ground: pin script + `build/technitiumlibrary.version`, found DnsServerCore.Tests (own HintPath References)
- [ ] 2 Poisoned-app fixture (publish-shaped, production load path); fold 0001; delete stale branch
- [ ] 3 Fold 0002 + 0003's DnsApplication.cs discovery hunk; discovery + ambiguity tests
- [ ] 4 DnsAppApiDispatcher (two-phase, static); fold rest of 0003; denied-never-reads-body test
- [ ] 5 Quirk 1: zero-registration throw + IReadOnlyList\<AppLoadWarning\> (independent of 4)
- [ ] 6 Quirk 2a: SpecialZoneManager forward-special-use set + predicate (both Query return shapes)
- [ ] 7 Quirk 2b: ordering change via extracted unit (4 tests incl. tri-state + no-recursion)
- [ ] 8 CI: build + test (Apps build-only pass; pin+OS+SDK cache keys)
- [ ] 9 CI: image publish (queued concurrency; git-tag-triggered -sovereign.\<n\>; GITHUB_TOKEN packages:write)
- [ ] 10 Sync policy doc + UPSTREAM-HISTORY; delete FUNDING.yml
- [ ] 11 Cutover in content-filter (fixture rewrite; Dockerfile.patched-server deleted; secret mounts; then delete patches/)

## Owner scope decision (2026-08-13, mid-round-1)
No external consumers exist and none are wanted as a constraint
("we dont have any real consumers yet except for me and I dont care").
Consumer-durability machinery removed and recorded as rejected in the
RFC (COPY --from, NuGet packaging, fixture image-pulling, rollback
runbooks, GHCR Actions-access grants) — round 2 honored this; the one
fixture C# change that round 1 had identified and the slimming
accidentally dropped was restored as a scoped edit, not machinery.
Roadmap runs as parallel engine/host tracks (MIGRATION.md "Tracks").

## Open forks (awaiting Corey)
- none — both rounds' findings all carried confident recommendations, applied

## Key decisions (round 2, 2026-08-13)
- Defer-to-blocking subset = **all eight forward special-use names**
  (localhost, home.arpa, resolver.arpa, service.arpa, test, invalid,
  local, onion) — no addressable set existed in code; SpecialZoneManager
  owns it via a named set + internal IsForwardSpecialUseName predicate,
  gated at both Query return shapes (zone lookup + NXDOMAIN synthesis)
- Defer runs only for client-originated queries (remoteEP non-null) —
  ResolverDnsCache calls AuthoritativeQueryAsync with null remoteEP
  (NRE + wrong semantics otherwise); blocking pass is tri-state
  (veto-with-null-block-response → synthetic answer, never recursion);
  ordering decision extracted to an internal static unit (no DnsServer
  construction in tests); 4 tests
- Dispatcher: **static**, two-phase (authorize on body-less probe →
  invoke with body only if permitted — preserves 0003's
  denied-never-buffered property); app table passed as
  IReadOnlyDictionary (no lookup delegate); one delegate only
  (AuthManager permission check); five-outcome result; fold normalizes
  0003's inconsistent error shapes (stated functional change)
- LoadWarnings: readonly IReadOnlyList\<AppLoadWarning\> built in ctor
  (build-then-freeze convention); type name only on ctor-throw branch —
  unrecoverable on load-failure branch (ex.Types nulls), warning carries
  loader-exception summary; zero-registration throw is added logic;
  envelope scope = install/update only (startup path logs)
- Slices re-cut: 3/4 split along discovery/dispatch (0002 is interface
  only; wiring is in 0003's DnsApplication.cs hunk); quirk 2 split into
  seam (6) + ordering (7); cutover resequenced (fixture rewrite before
  patches/ deletion); 10 → 11 slices
- Cutover corrections: Dockerfile.patched-server deleted outright
  ("zero C# changes" was false — BaseTechnitiumFixture hardcodes the
  patches path + filename; rewritten to clone fork at pin, build
  Dockerfile.sovereign); integration-test/perf-comparison whole-tree
  COPYs drop; content-filter pins the fork via the sovereign git tag;
  PAT threaded via BuildKit secret mounts, never ARG/ENV;
  explain-api-provisioning.md updated
- CI: pin file named build/technitiumlibrary.version; build via
  DnsServerApp.csproj (never the .sln); Apps/* build-only pass added;
  cache keys include runner OS + SDK major; publish via GITHUB_TOKEN
  packages:write; moving develop tag under queued concurrency group;
  immutable -sovereign.\<n\> cut by pushing the same-named git tag
  (doubles as content-filter's clone pin — one fact, no allocation
  race); image.source label → urostigma
- Fixture mechanics corrected: production DnsApplicationAssemblyLoadContext
  discovers via *.deps.json (fixture must be publish-shaped or load
  silently no-ops); unpatched throw is FileNotFoundException from
  ExportedTypes (ReflectionTypeLoadException only post-patch) — assert
  registration outcomes, never exception types
- Sync policy retroactively binding during slices 1–9
- Verified clean in round 2: no strong-naming (plain InternalsVisibleTo
  works); sequential patch apply clean against develop (empirical);
  dotnet publish DnsServerApp transitively covers the four projects;
  install/update already envelope-covered; TechnitiumLibrary tag
  dns-server-v15.4.0 exists upstream

## Key decisions (round 1, 2026-08-13)
- TechnitiumLibrary: pinned sibling clone via checked-in script; NOT a
  submodule (HintPath edits would create permanent merge surface); pin
  bumps paired with upstream merges
- Dispatch seam: extract internal DnsAppApiDispatcher now; fold 0003 into
  the extracted shape (no fold-verbatim-then-refactor)
- Quirk 1 expanded: loadWarnings array for partial loads (closes the gap
  0001 reintroduces); status:"error" reserved for zero-registration; no
  new envelope plumbing
- RFC 6761: narrow flag specialUseNamesDeferToBlocking, ordering change
  with the invariant "unblocked special-use names still get synthetic
  answers, never recursion"; composes with existing LocallyServedDnsZones
- Upstream's 0001 objection engaged in-text: partial-load-with-visible-
  warning chosen over total-abort, rationale recorded
- Image: new multi-stage Dockerfile.sovereign (upstream Dockerfile
  untouched); immutable <upstream>-sovereign.<n> tags; revision label;
  publish gated on tests; rollback = repin
- Auth: classic PAT for content-filter's cross-repo clone/pull; no
  pull_request_target
- Prior state verified: patches NOT on develop; 0001 exists only on stale
  branch origin/fix/app-type-discovery (delete after slice 2)
- Windows projects out of CI; linux/amd64 only; Apps-permission
  coarseness deferred to issue #24

## Review ledger — round 2
| id | sev | finding (lens) | status | resolution |
|----|-----|----------------|--------|------------|
| 24 | crit | Slice-3 test impossible: 0002 is interface-only, discovery wiring in 0003 (feas) | fixed | Slices 3/4 re-cut |
| 25 | crit | Slice-10 self-contradiction: fixture hardcodes patched-server path; "zero C# changes" false (breadth/feas) | fixed | Cutover rewritten; slice 11 resequenced |
| 26 | high | "RFC 6761 forward-name subset" not a set in code; two disjoint mechanisms, two return sites (depth/design/feas/peer) | fixed | All-eight subset owned by SpecialZoneManager; slice 6 |
| 27 | high | AuthoritativeQueryAsync called with null remoteEP from ResolverDnsCache — NRE + wrong semantics (peer/depth) | fixed | Client-originated-only insertion |
| 28 | high | Blocking pass is two private methods, tri-state; null block-response falls through to recursion today (depth/peer) | fixed | Tri-state named; 4th test |
| 29 | high | One-shot dispatcher signature drops 0003's denied-never-buffered property (design/depth/feas) | fixed | Two-phase authorize/invoke |
| 30 | high | Content-filter's urostigma clone has no pin; develop-HEAD clones non-reproducible (breadth) | fixed | Sovereign git tag = clone pin |
| 31 | high | Private-clone secret injection unspecified; ARG/ENV leaks token into layers (breadth) | fixed | BuildKit secret mounts |
| 32 | high | Test csproj HintPath transitivity unreliable (feas) | fixed | Own Reference entries; slice 1 |
| 33 | high | LoadWarnings type name unrecoverable on load-failure branch (peer) | fixed | Content-honesty spec |
| 34 | high | Error-shape inconsistency in 0003 (raw 500 vs exception envelope) — fold changes behavior silently (depth/feas) | fixed | Stated functional change |
| 35 | med | Dispatcher shape: static not instanced; dictionary not lookup delegate; two permission authorities (design/depth) | fixed | Design rewritten |
| 36 | med | Quirk-2 tests need full DnsServer as written (design) | fixed | Extracted ordering unit |
| 37 | med | Fixture misdescribed: production ALC + .deps.json required; wrong exception type cited (depth) | fixed | Fixture spec rewritten |
| 38 | med | Quirk-1 framing: throw is added logic; startup path has no envelope; WriteAppAsJson private (feas/peer) | fixed | Design + slice 5 |
| 39 | med | Apps/* never built by CI — silent ApplicationCommon drift (peer) | fixed | Build-only pass; slice 8 |
| 40 | med | Moving-tag reorder race; -sovereign.\<n\> allocation unspecified (peer) | fixed | Queued concurrency; git-tag trigger |
| 41 | med | GHCR push permission for urostigma's own job unstated (breadth) | fixed | GITHUB_TOKEN packages:write |
| 42 | med | Pin file path unnamed; sln build fails on Linux (feas) | fixed | build/technitiumlibrary.version; csproj-not-sln |
| 43 | med | Upstream security release mid-implementation unhandled (breadth) | fixed | Retroactive clause |
| 44 | med | explain-api-provisioning.md still says "patched" (breadth) | fixed | Slice 11 doc list |
| 45 | low | image.source label points at upstream; FUNDING.yml stale; cache key omits OS/SDK (breadth/peer/depth) | fixed | Labels, slice 10, cache keys |

## Review ledger — round 1
| id | sev | finding (lens) | status | resolution |
|----|-----|----------------|--------|------------|
| 1 | crit | TechnitiumLibrary unprovisioned — nothing builds (breadth/feas) | fixed | CI section rewritten; slice 1 |
| 2 | crit | BaseTechnitiumFixture builds image at test time, missed by cutover (breadth/depth/feas) | fixed | Cutover (b); re-fixed properly in round 2 (#25) |
| 3 | crit | Dispatch untestable (private nested, Kestrel-bound); open question punted (depth/design/feas) | fixed | Extraction decided; slice 4 |
| 4 | crit | RFC 6761 naive fix leaks RFC-1918/localhost to recursion (depth) | fixed | Invariant + tests; slices 6-7 |
| 5 | crit | Partial-load visibility gap reintroduced by 0001 (design) | fixed | loadWarnings design; slice 5 |
| 6 | high | 4/6 Dockerfiles are compile-reference consumers; FROM doesn't fit (breadth/depth/feas) | fixed | Cutover (a); slice 11 |
| 7 | high | Repo Dockerfile doesn't build; CI image step unspecified (breadth/feas) | fixed | Dockerfile.sovereign |
| 8 | high | Tagging open question gates slices (breadth/design) | fixed | Decided -sovereign.\<n\> |
| 9 | high | No rollback/immutability story (breadth) | fixed | Immutable tags; repin |
| 10 | high | Upstream 0001 objection unengaged (depth) | fixed | Rationale paragraph |
| 11 | high | Existing LocallyServedDnsZones flag interaction unspecified (depth/design) | fixed | Composition + test |
| 12 | med | 0001 prior state misread — exists on stale branch only (breadth; verified directly) | fixed | Prior-state section |
| 13 | med | Poisoned fixture ambiguity — ctor-throw is false RED (feas) | fixed | Fixture spec; slice 2 |
| 14 | med | pr1-repro + UPSTREAM.md deleted without preservation (breadth/feas) | fixed | Cutover (d) |
| 15 | med | Copyright header misattribution on new files (breadth) | fixed | Header convention |
| 16 | med | GHCR auth gaps (depth/breadth) | fixed | Build auth rewritten |
| 17 | med | Merge-not-rebase rationale wrong (depth) | fixed | Corrected + revision label |
| 18 | med | Sync triggers vague (design) | fixed | Mechanism + 1-week SLA |
| 19 | med | Test stack unpinned; no mocking convention; InternalsVisibleTo unmentioned (design/feas) | fixed | Founding section |
| 20 | med | build-dns-server.sh + compose example missed (breadth) | fixed | Cutover (c) |
| 21 | med | Windows CI scope, multi-arch, publish gating unstated (breadth/feas) | fixed | Non-goals + CI |
| 22 | med | 0003 permission coarseness untracked (design) | fixed | Issue #24 + non-goal |
| 23 | low | Envelope no-new-plumbing note; bulk-apply trap; compose repoint (design/feas/breadth) | fixed | In place |
