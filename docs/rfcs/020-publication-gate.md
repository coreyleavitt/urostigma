# RFC-020: Publication gate — de-branding and GPL compliance

- **Status:** Stub — deliberately unscheduled; gates publication, not
  development
- **Depends on:** nothing in the build sequence; blocks only going public
- **Issues:** #23

Scope: the checklist that must pass before any repo or image becomes
public. GPLv3 corresponding-source obligations attach on distribution;
Technitium's trademarks are not covered by the code license. Contents:
strip upstream branding (logo, README identity, sponsor sections,
web-console naming) while retaining all copyright and license notices;
prominent statement-of-changes per GPLv3 §5; corresponding source
available for every published image; NOTICE hygiene for the consumed
Apache engine; a public identity decision (what the published project is
called and how it credits its Technitium ancestry honestly). Everything
stays private until this passes.
