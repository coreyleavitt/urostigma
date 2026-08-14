# urostigma

GPLv3 sovereign fork of Technitium DNS Server; host for the syconium F#
engine (Apache 2.0, separate repo — the license boundary is the repo
boundary). Program plan: `docs/MIGRATION.md`. RFC master registry:
`docs/rfcs/README.md`. Issues in this repo are the operational truth.

Rules that matter here:
- Never copy or translate code from this repo into syconium — provenance
  rules in syconium's CONTRIBUTING.md.
- No renaming of .NET projects/namespaces — the upstream merge surface
  stays intact.
- Upstream (TechnitiumSoftware/DnsServer) is read-only merge source;
  policy in `docs/UPSTREAM-SYNC.md` once RFC-010 lands.

## Compact Instructions
When compacting, preserve in the summary: the active RFC and its
handoff-doc path, the current stage/round, slices done vs remaining, open
forks awaiting me, and the exact resume command. After compacting, re-read
the handoff doc and MEMORY.md before continuing.
