# RFC-010 Fork sovereignty — handoff

- **Stage:** 2 architect   •   **Round:** 1 of 2 complete (2026-08-13)
- **Resume:** `/architect docs/rfcs/010-fork-sovereignty.md round 2`

## Slices (11 after round-1 re-slicing)
- [ ] 1 Buildable ground: TechnitiumLibrary pin script, found DnsServerCore.Tests
- [ ] 2 Poisoned-app fixture (from pr1-repro); fold 0001; delete stale branch
- [ ] 3 Fold 0002 + discovery test (corrected copyright header)
- [ ] 4 DnsAppApiDispatcher extraction; fold 0003 into it; dispatch tests
- [ ] 5 Quirk 1: zero-registration error + loadWarnings partial visibility
- [ ] 6 Quirk 2: RFC 6761 defer-to-blocking (3 tests incl. no-recursion invariant)
- [ ] 7 CI: build + test workflow (verification by execution)
- [ ] 8 CI: image publish (Dockerfile.sovereign, -sovereign.<n> tags, gated)
- [ ] 9 Sync policy doc + UPSTREAM-HISTORY preservation
- [ ] 10 Cutover A: compile-reference consumers (COPY --from + HintPaths)
- [ ] 11 Cutover B: runtime consumers (fixture C# change) + scripts; delete patches/

## Open forks (awaiting Corey)
- none — all round-1 findings carried confident recommendations and were applied

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
- Auth: GHCR package Actions-access grant to content-filter (no rotating
  secret); classic PAT for humans; host-daemon docker login for
  Testcontainers jobs; no pull_request_target
- Cutover split: compile-reference consumers get COPY --from (NuGet
  packaging deferred to RFC-013); runtime consumers are a C# fixture
  change; build-dns-server.sh + docker-compose.example.yaml in scope;
  UPSTREAM.md history preserved as docs/UPSTREAM-HISTORY.md
- Prior state verified: patches NOT on develop; 0001 exists only on stale
  branch origin/fix/app-type-discovery (delete after slice 2)
- Windows projects out of CI; linux/amd64 only; Apps-permission
  coarseness deferred to issue #24

## Review ledger
| id | sev | finding (lens) | status | resolution |
|----|-----|----------------|--------|------------|
| 1 | crit | TechnitiumLibrary unprovisioned — nothing builds (breadth/feas) | fixed | CI section rewritten; slice 1 |
| 2 | crit | BaseTechnitiumFixture builds image at test time, missed by cutover (breadth/depth/feas) | fixed | Cutover (b); slice 11 |
| 3 | crit | Dispatch untestable (private nested, Kestrel-bound); open question punted (depth/design/feas) | fixed | Extraction decided; slice 4 |
| 4 | crit | RFC 6761 naive fix leaks RFC-1918/localhost to recursion (depth) | fixed | Invariant + 3 tests; slice 6 |
| 5 | crit | Partial-load visibility gap reintroduced by 0001 (design) | fixed | loadWarnings design; slice 5 |
| 6 | high | 4/6 Dockerfiles are compile-reference consumers; FROM doesn't fit (breadth/depth/feas) | fixed | Cutover (a); slice 10 |
| 7 | high | Repo Dockerfile doesn't build; CI image step unspecified (breadth/feas) | fixed | Dockerfile.sovereign |
| 8 | high | Tagging open question gates slices 7/9 (breadth/design) | fixed | Decided -sovereign.<n> |
| 9 | high | No rollback/immutability story (breadth) | fixed | Immutable tags; repin |
| 10 | high | Upstream 0001 objection unengaged (depth) | fixed | Rationale paragraph |
| 11 | high | Existing LocallyServedDnsZones flag interaction unspecified (depth/design) | fixed | Composition + test |
| 12 | med | 0001 prior state misread — exists on stale branch only (breadth; verified directly) | fixed | Prior-state section |
| 13 | med | Poisoned fixture ambiguity — ctor-throw is false RED (feas) | fixed | Fixture spec; slice 2 |
| 14 | med | pr1-repro + UPSTREAM.md deleted without preservation (breadth/feas) | fixed | Cutover (d) |
| 15 | med | Copyright header misattribution on new files (breadth) | fixed | Header convention |
| 16 | med | GHCR auth: fine-grained PAT gaps; host-daemon login; public-repo secrets (depth/breadth) | fixed | Build auth rewritten |
| 17 | med | Merge-not-rebase rationale wrong (depth) | fixed | Corrected + revision label |
| 18 | med | Sync triggers vague (design) | fixed | Mechanism + 1-week SLA |
| 19 | med | Test stack unpinned; no mocking convention; InternalsVisibleTo unmentioned (design/feas) | fixed | Founding section |
| 20 | med | build-dns-server.sh + compose example missed (breadth) | fixed | Cutover (c) |
| 21 | med | Windows CI scope, multi-arch, publish gating, CI-ordering rationale unstated (breadth/feas) | fixed | Non-goals + CI |
| 22 | med | 0003 permission coarseness untracked (design) | fixed | Issue #24 + non-goal |
| 23 | low | Envelope no-new-plumbing note; bulk-apply trap; compose repoint (design/feas/breadth) | fixed | In place |
