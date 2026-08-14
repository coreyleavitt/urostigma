# RFC-011: Subsystem map, characterization targets, swap order, recursion boundary

- **Status:** Stub — draft after RFC-010 lands
- **Depends on:** 010 (a buildable, tested fork to map)
- **Enables:** 014, and re-scopes 016–018 before any leaves Stub
- **Issues:** epic #6 (#7 map, #9 decisions); pairs with syconium RFC-012

Scope: the subsystem map and dependency graph of `DnsServerCore`'s query
pipeline end to end (listener → parse → zones/cache/blocking/apps →
recursion → response); the god-class inventory (`DnsServer.cs` and
friends); where state lives (zone tree, cache, stats, session); which
subsystems are separable today vs entangled. Output is a doc in this repo
naming each subsystem, its interface surface, its entanglements, and its
non-wire observables (admin APIs, stats — gap 4) — the input for the two
decisions this RFC must record: swap order, and the standing recursion
boundary question (does recursion/DNSSEC validation ever move to F#, stay
C# indefinitely, or delegate to an external resolver?). Cache persistence
behavior (gap 5) is characterized here for RFC-017 to consume.
