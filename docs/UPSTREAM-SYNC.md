# Upstream sync policy

This repo tracks `TechnitiumSoftware/DnsServer` as a read-only merge
source. There is no upstreaming program: the fork owns its diverged
files outright (see "Conflict ownership" below), and upstream is a
release feed to pull from, not a target to submit patches to. This
document is the standing procedure for keeping the fork current against
that feed.

## Triggers

Watch `TechnitiumSoftware/DnsServer` releases (GitHub release
notifications on the upstream repo).

- A release whose notes mention a CVE or security fix starts a sync
  within one week of the release.
- A feature release is merged only when something in it is actually
  wanted. There is no obligation to stay current with upstream feature
  work for its own sake.

## Procedure

Sync by `git merge` from the upstream remote. Never rebase.

Rationale: published images carry `image.revision` SHAs, and the
provenance record cites commits by SHA. A rebase rewrites history and
orphans every SHA anyone has already written down — in a provenance
citation, in an image label, in an incident note. `git merge` preserves
the commits those records point at.

## Paired pin

Every upstream merge bumps `build/technitiumlibrary.version` to the
TechnitiumLibrary release tag matching the upstream release just merged,
in the same pull request as the merge. The DnsServer merge and the
TechnitiumLibrary pin are one fact, never independent variables — do not
land one without the other, and do not let the pin drift ahead of or
behind the merge across separate PRs.

## Conflict ownership

The fork has diverged from upstream in a known, specific set of files.
Merge conflicts in these files are ours to resolve, and resolving them
means preserving the fork's behavior while folding in upstream's
change, not picking one side wholesale:

- `DnsServerCore/Dns/Applications/DnsApplication.cs`
- `DnsServerCore/Dns/Applications/DnsApplicationManager.cs`
- `DnsServerCore/WebServiceAppsApi.cs`
- `DnsServerCore/DnsWebService.cs`
- `DnsServerCore/Dns/SpecialZoneManager.cs`
- `DnsServerCore/DnsServer.cs`
- `DnsServerCore/WebServiceSettingsApi.cs`

The following files are fork-owned and upstream has no equivalent, so
they never participate in a merge conflict: `SovereignConfig.cs`,
`SpecialUseDeferralDecision.cs`, `DnsAppApiDispatcher.cs`,
`InternalsVisibleTo.cs`, the `DnsServerCore.Tests` project, everything
under `build/`, `Dockerfile.sovereign`, and this repo's GitHub Actions
workflows.

This list is a snapshot, not a ceiling. As the fork's own development
diverges further from upstream, keep this document current — add a file
here the first time a merge conflicts in it, whether or not that was
anticipated.

## Provenance clause

After a merge session lands, any engine (syconium) work touching a
subsystem the merge changed starts from the spec or wire corpus for that
subsystem, not from memory of the merge diff. This is syconium's
CONTRIBUTING rule 4, restated here because the upstream merge is where
the temptation to shortcut it actually arises.

## SDK re-survey

On each merge, re-survey the `DnsServerCore.ApplicationCommon` SDK
surface for breaking drift against the plugin that consumes it. This is
the RFC-006 handoff's obligation, carried forward into this policy: a
merge is the point where upstream can silently change the SDK contract
the plugin depends on, so the survey happens every time, not only when
something is suspected to have moved.
