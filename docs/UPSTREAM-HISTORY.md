# Upstream submission history

This document preserves `patches/dns-server/UPSTREAM.md` from the
`technitium-content-filter` repository, copied here verbatim before that
repo's `patches/dns-server/` directory is deleted as part of RFC-010's
consumer cutover (the patch series it describes is retired once
content-filter builds from this repo's published image instead of
reapplying patches to a fresh upstream clone).

That file is the only record of PR #2092
(`TechnitiumSoftware/DnsServer#2092`), the per-type isolation fix this
fork submitted upstream — RFC-010's Context section records upstream's
decline of that PR ("not a bug") as the event that pivoted the program
from upstreaming to owning the divergence outright; see
`docs/rfcs/010-fork-sovereignty.md` for that account. The record below
documents the submission itself, plus the second, never-posted PR draft
for the generic per-app API port, exactly as it stood in
content-filter at the time of copying. Nothing below has been rewritten;
only this preamble is new.

**Outcome (primary-source record, verified 2026-09-09 via the GitHub
API):** PR #2092 was closed unmerged on 2026-08-11 by the maintainer
(ShreyasZare) with one comment: "Thanks for the PR. The current
implementation is intended for the app to fail completely instead of
having parts of it loaded. This is not a bug. Having a few interfaces
loaded will cause the app to be used by the DNS server and may cause
errors per DNS query being processed due to the root cause". RFC-010's
"On upstream's objection to 0001" section engages this argument on the
merits and records why the fork chooses partial-load anyway.

---

# Upstream PR drafts

This file holds the text for the two pull requests the patch series in this
directory is meant to become, in sequence. **Nothing here has been posted.**
Both are drafts, kept next to the patches they describe so they stay in sync
as the series is rebased across pin bumps.

Sequencing: PR #1 first, standalone, as soon as it's convenient. PR #2 only
after this project has shipped and run its own explain endpoint on the port
for a while -- it is the "working consumer" argument PR #2 leans on.

---

## PR #1 -- per-type isolation in the app type-sweep loop

**SUBMITTED 2026-08-10: https://github.com/TechnitiumSoftware/DnsServer/pull/2092**
(branch `fix/app-type-discovery` on the `coreyleavitt/DnsServer` fork, based on
upstream master, which at submission time was identical to the v15.4.0 pin — no
rebase was needed). Corresponds to `0001-per-type-isolation-in-type-sweep.patch`.
If upstream merges it, drop 0001 from the local series at the next pin bump
(submission checklist item 3). Text below is what was posted.

### Title

```
Fix: one unloadable type in a DNS App assembly prevents every type in it from registering
```

### Body

```markdown
## The bug

`DnsApplication`'s constructor discovers app classes like this
(`DnsApplication.cs`, trimmed):

```csharp
try
{
    foreach (Type classType in appAssembly.ExportedTypes)
    {
        // register classType if it implements IDnsApplication
    }
}
catch (Exception ex)
{
    _dnsServer.WriteLog(ex);
}
```

`ExportedTypes` calls `GetExportedTypes()`, which resolves every type in the
assembly before returning anything. If a single type fails to resolve (for
example, its base type lives in an assembly that isn't present), the whole
call throws and the loop body never runs. The catch logs that one exception
and the app ends up installed with no classes registered at all. The install
API still reports `"status": "ok"`, so nothing looks wrong anywhere except a
single loader exception in the log, and that exception says nothing about
the healthy types that were never attempted.

Since apps are sideloaded zips that carry their own dependency DLLs, a
missing or mispackaged DLL is all it takes to hit this. And if the assembly
contains an `IDnsRequestBlockingHandler`, blocking is now off while the
server keeps resolving normally, which is a hard failure mode to notice.

## The fix

Use `Assembly.GetTypes()` and handle its partial-failure mode: on
`ReflectionTypeLoadException`, the exception's `Types` array still contains
every type that did load (with `null` in the slots for the ones that
didn't). The loop now logs each entry of `LoaderExceptions` and carries on
with the types that loaded. Any other exception is logged and aborts that
assembly, same as before.

`GetTypes()` also returns non-exported types, unlike `ExportedTypes`, so the
loop gains a `classType.IsVisible` check to keep the original exported-only
behavior. For an assembly where every type loads cleanly, the registration
outcome is unchanged.

## Testing

Repro: an app assembly with three public classes implementing
`IDnsApplication` (AlphaApp, BravoApp, CharlieApp), where BravoApp's base
type lives in a second helper assembly that is deliberately left out of the
app zip.

On a stock v15.4.0 container, installing that zip reports `"status": "ok"`
but `/api/apps/list` shows `"dnsApps": []` -- none of the three classes
registered -- and the log contains one `FileNotFoundException` thrown from
inside `GetExportedTypes()`. With this change, AlphaApp and CharlieApp both
register, BravoApp is skipped, and its loader exception is logged.

Also verified the change builds standalone on an otherwise clean v15.4.0
tree (DnsServerCore build and DnsServerApp publish).
```

---

## PR #2 -- generic per-app API port (`IDnsApplicationApiHandler`)

Skeleton only -- fill in before posting once the port has shipped and run in
production for a while. Corresponds to `0002-add-idnsapplicationapihandler-port.patch`
and `0003-api-apps-call-dispatch-route.patch`.

### Title

```
Add a generic per-app HTTP API port (IDnsApplicationApiHandler) via /api/apps/call
```

### Body outline

```markdown
## Motivation

Several DNS App capabilities already carry ad hoc, fixed-shape query surfaces
bolted onto the console API rather than a real request/response channel:
`IDnsQueryLogs` is one method with a fixed 12-parameter signature and a fixed
`DnsLogPage` return type, built for pluggable log *storage* backends behind
one console form -- it cannot answer anything outside that one shape. Advanced
Blocking's own statistics surface is similarly bespoke. Neither generalizes:
adding a new query shape today means adding a new fixed method to
`ApplicationCommon` and a new console route to match, every time.

[PROJECT] hit the same wall building a diagnostic "explain what this query
would do" endpoint and needed an actual request/response channel: an
arbitrary path space, a request body, a real HTTP verb -- none of which any
existing capability interface can express. Precedent for widening
`IDnsServer`'s own comms surface for exactly this kind of need exists already
(`DirectQueryAsync`, #1913, added for BlockPageApp's synthetic-query needs).
This PR generalizes the same instinct into a capability interface any app can
implement, rather than adding another bespoke fixed-shape method.

## Design

One new interface in `DnsServerCore.ApplicationCommon`, discovered by the
existing per-assembly capability sweep (the same mechanism that finds
`IDnsRequestBlockingHandler`, `IDnsQueryLogs`, etc.):

```csharp
public interface IDnsApplicationApiHandler
{
    DnsAppApiAccess GetRequiredAccess(DnsAppApiRequest request) => DnsAppApiAccess.View;

    Task<DnsAppApiResponse> HandleApiRequestAsync(DnsAppApiRequest request, CancellationToken cancellationToken);
}
```

`DnsAppApiRequest` carries the real HTTP method, the app-relative path, the
already-parsed query string, the request's `Content-Type`, a bounded byte
array for the body (the console enforces a size cap while streaming the
body in, so a chunked request without `Content-Length` cannot bypass it),
and the authenticated username. `DnsAppApiResponse` carries a status code, a
content type, and a body -- the app owns its own response verbatim.

A new route, registered like every other console route:
`/api/apps/call?name=<app>&path=<subpath>`, `MapGetAndPost` so the real verb
passes through as `DnsAppApiRequest.Method`.

### Two departures from the existing capability-interface idiom, both deliberate

1. **The input is DTO-shaped, not a fixed parameter list.** Every existing
   inbound capability (`IDnsQueryLogs` above is the clearest example) takes
   discrete scalar parameters. An open-ended path space cannot be expressed
   that way. `ApplicationCommon` already has DTO precedent on the *output*
   side (`DnsLogPage`/`DnsLogEntry`); this just applies the same shape to the
   input.
2. **`HandleApiRequestAsync` takes a `CancellationToken`.** No existing
   inbound capability interface does, and neither does the console's own
   route-handler signature convention. This is the first capability that
   runs arbitrary app code inside an HTTP request's lifetime, so the
   dispatch threads `HttpContext.RequestAborted` through it -- never
   `CancellationToken.None` -- so a disconnecting caller can actually stop
   queued work rather than leaving it to run to completion regardless.

### The envelope bypass

`WebServiceApiMiddleware` wraps every `/api/*` response not on its
raw-passthrough list (`/api/logs/download`, `/api/zones/export`, and a
handful of others) in the console's standard JSON envelope and forces HTTP
200 regardless of the handler's actual status. A per-app API response needs
its status code, content type, and body to reach the wire untouched, so
`/api/apps/call` joins that existing passthrough list -- the same mechanism
`/api/logs/download` already uses, not a new bypass. This PR proposes no
change to the envelope convention itself; every existing `/api/*` client
keeps working exactly as it does today.

### Permission model

`GetRequiredAccess(request)` lets a handler declare `View` or `Modify` per
request path (default `View`, via a default interface method so no existing
implementor needs to change). The console checks the declared access against
the authenticated user's `Apps` section permission before dispatching.
Known coarseness, flagged rather than hidden: `Apps` section `View` currently
grants `config/get` visibility across *every* installed app, not just the
one being called -- so a per-app API consumer's token can also read other
apps' configs via the existing config-get route. If per-app (rather than
per-section) permission granularity is wanted, this is the natural place to
add it; happy to take a maintainer's preference on whether that belongs in
this PR or a follow-up. One related mechanism-location note: permission
rejection has to run inside the dispatch itself (it needs the resolved
handler's declared access first), not in the earlier session/token
middleware -- but the outcome is unchanged, a rejection still lands as the
console's standard 200 + `{"status":"invalid-token"|"error"}` envelope, the
same as every other permission failure today.

### Multi-implementor handling

An app is expected to register at most one `IDnsApplicationApiHandler`
implementor -- one API namespace per app, with path routing inside the
handler covering anything a second class would otherwise express. If an app
registers more than one, the load sweep logs a warning and the dispatch route
answers a raw HTTP 500 for that app (raw, not enveloped, since the envelope
path hardcodes 200 and a 500 is structurally impossible to express through
it).

## Evidence this works

A full consumer ships in production on top of this patch series today: a
read-only "explain what this query would do" diagnostic endpoint, authenticated
via a dedicated least-privilege token, exercising the full dispatch path --
routing, auth, permission enforcement, request/response bodies, and the
raw-passthrough envelope bypass -- against real traffic. Happy to point at
specifics if useful for review.

## What this PR does not touch

No change to the `/api/*` envelope convention for any existing route. No
change to any existing capability interface's shape. `RFC-009`-shaped write
consumers (config mutation through this same port) are not part of this PR;
`GetRequiredAccess`'s `Modify` case exists so that future capability can
arrive additively, without another interface change.

## Testing

[Describe the test coverage carried over from the patch series: unit tests
against the handler dispatch in isolation, and an integration suite exercising
the route through the console's real auth/session/permission machinery inside
a real container. Fill in exact suite names/counts before posting.]
```

---

## Submission checklist

Work through this before actually opening either PR (not before -- these are
reminders for future-me, not a task list to complete now):

1. **Rebase the patch series against the current pinned `DnsServer`/
   `TechnitiumLibrary` tags.** The series is rebased at every pin bump as a
   matter of course (see the RFC's own "Mechanics" section) -- do this
   rebase again, deliberately, immediately before opening either PR, even if
   the last scheduled rebase was recent. `git apply --check` clean against a
   fresh clone of the target tag is the bar.
2. **Re-verify PR #1 in isolation.** Apply *only* `0001-per-type-isolation-
   in-type-sweep.patch` to a clean checkout (not the full series) and confirm
   it still builds and the repro from the PR body still demonstrates the
   before/after behavior. PR #1 should never accidentally depend on PR #2/#3.
3. **Re-verify PR #2's dependency on PR #1.** `0002`/`0003` do not depend on
   `0001` functionally, but if `0001` has already merged upstream by the time
   PR #2 goes up, drop `0001` from PR #2's base and rebase `0002`/`0003`
   directly onto upstream's own fix instead of carrying a duplicate.
4. **Fill in PR #2's testing section** with the real suite names/counts at
   submission time (they will have grown since this draft was written).
5. **Re-confirm the "working consumer" claim is still true** -- i.e. this
   project is still running the explain endpoint against this patch series
   in production -- before citing it as evidence in PR #2. If the project has
   since moved off the fork (upstream declined, or a different transport
   replaced it), that line needs to be replaced with whatever evidence still
   applies, not left in citing a consumer that no longer exists.
6. **Read the target repository's current CONTRIBUTING guidance and recent
   merged PRs** for any process/style expectations that may have changed
   since this draft was written (issue templates, required checks, PR
   description conventions, DCO/CLA requirements).
7. **Do not post PR #2 before PR #1 has had a chance to land or receive
   feedback.** The sequencing (standalone bugfix first, credibility-builder,
   before pitching new surface) is deliberate -- posting both at once defeats
   the purpose.
8. **Fill in `[PROJECT]` in PR #2's motivation section** with whatever
   framing is appropriate for a public, upstream-facing description at
   submission time -- keep it generic (a DNS filtering app needing a real
   request/response channel), not a specific project pitch.
