# RFC-014: Native query-pipeline seam and dark-launch

- **Status:** Stub — draft after 011/012/013
- **Depends on:** 011 (the map names the seam point), 012 (the gate),
  013 (an engine to host)
- **Enables:** 015–018 (every swap promotes through this machinery)
- **Issues:** epic #10 (#12 seam/dark-launch; #11 consumption, partly 013's)

Scope: host the engine at a native query-pipeline seam — evaluate before
recursion, synthesize verdict responses in-process, no ALC hop. The
existing content-filter plugin stays deployable throughout; the native
path is dark-launched behind the RFC-012 harness until parity, then
becomes the default for our deployment. This RFC defines the seam
interface (candidate name: Ostiole — the opening into the syconium), which
is filter-agnostic by decision (2026-08-13): its contract speaks
pipeline-neutral types (query context, verdict, synthesis instruction)
with no ContentFilter types in its signature. The filter is the seam's
first consumer, not its definition — in-process, so the interface costs
nanoseconds, unlike the ALC hop this epic retires. The seam thereby
doubles as the future in-process plugin surface without building a plugin
framework for one consumer; `IDnsApplication` remains the third-party/
out-of-process story. This RFC also defines the
dark-launch mechanics (both paths run, divergence logged, where that log
lives and how it is monitored), and the epic #10 standing rules as
enforceable design: per-subsystem kill-switch flags surviving until a
named soak period ends, and the perf clause (each swap's RFC names a perf
budget measured before the C# path is deleted). The IDnsApplication app
surface stays functional until the app path and native path agree,
harness-gated.
