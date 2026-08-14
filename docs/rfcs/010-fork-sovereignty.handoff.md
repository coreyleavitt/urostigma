# RFC-010 Fork sovereignty — handoff

- **Stage:** 2 architect   •   **Round:** 0 of 2 complete
- **Resume:** `/architect docs/rfcs/010-fork-sovereignty.md round 1`

## Slices
- [ ] 1 Found DnsServerCore.Tests; fold 0001 + isolation tests
- [ ] 2 Fold 0002 (IDnsApplicationApiHandler) + discovery test
- [ ] 3 Fold 0003 (/api/apps/call) + dispatch tests
- [ ] 4 Quirk: install/load silent success → status:"error"
- [ ] 5 Quirk: RFC 6761 hook behind config
- [ ] 6 CI: build + test workflow
- [ ] 7 CI: image publish to private GHCR
- [ ] 8 Sync policy doc (docs/UPSTREAM-SYNC.md)
- [ ] 9 Consumer cutover in content-filter (Dockerfiles, delete patches/)

## Open forks (awaiting Corey)
- none yet — the RFC's Open Questions go to the architect rounds first

## Key decisions (this session, 2026-08-13)
- Registry: unified number line continues at 010; RFC lives in the repo it
  specifies; master registry in this repo (docs/rfcs/README.md)
- Folded commits use real authorship; patch identity not preserved
- Dockerfile.patched-server retires; consumers pull the GHCR image
- Work lands on develop (default branch), merge-not-rebase for upstream

## Review ledger (stage 4)
| id | sev | finding | status | proof / reason |
|----|-----|---------|--------|----------------|
