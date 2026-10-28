# [Sol 6 High][P1] Bound macro terrain-probe memory and cost without changing terrain output

Executor gpt-6-sol/high, manual selection. Base integration/desktop-acceptance-2026-10-08@66e9a41, PR#20. Follow-up #12/#5. Read AGENTS and ADR-0002.

Evidence: actual3072m macro plan70.698s; observed suite managed peak1,098,055,680 bytes (includes prior runs, not a cold per-seed figure). 1024m plans17–25s. Diagnose expensive probe/chunk sampling before optimization; no blanket Task.Run of ScriptableObjects/AnimationCurves.

## Exclusive files/classes
Assets/_Game/Scripts/World/Generation/WorldTerrainProbe.cs; new Tests/Editor/WorldTerrainProbeCostTests.cs and Editor/MacroProbeCostAudit.cs (+meta); docs/reports/macro-probe-cost.md. Pipeline/stages/routing/settings/MacroWorldPlan/streamer are read-only.
Public contract frozen: Sample/getHeight/class/slope outputs, seed semantics and constructor remain compatible. Internal cache lifetime/bounds/counters may change. If pure sampler snapshot or pipeline change is necessary, propose reassignment/ADR first, do not edit it unilaterally.

Instrument calls, unique chunks, cache hits/rebuilds, peak retained samples, per-stage work and cold elapsed/memory. Try bounded deterministic cache or safe equivalent sampling only with bitwise/tolerance-equivalent output proof. No full detailed-world generation, silent lower-resolution topology, hidden prewarm, changed source selection or unbounded lifetime caches.
Tests: same samples before/after at chunk seams/negative boundaries, eviction/regeneration, stable bridge/river/road output, cache cap. Compare cold process seed12345 sizes1024/3072 using same settings, then full suite; GC allocation API returning0 is unavailable, not zero cost. Report controlled before/after wall-time/peak memory and limitations. Parallel A/B/C only while probe public outputs/API stay frozen; D profiles combined result after integration. Foreign edits forbidden without reassignment.
