# Concept world performance preflight — PC profiling acceptance

Executor GPT-6 in ChatGPT, 2026-10-08. Issue #12.
Branch gpt6/concept-perf-audit-preflight, based on current-unity-fix c61a6de.
Status: **INSTRUMENTATION SOURCE ONLY; NO UNITY OR REAL FPS MEASURED.**

## Existing configuration observed

- Concept world chunk size: **32 meters**, **64 cells per side**, **0.5m** sample grid.
- Visible load radius: **4 chunks = 128m** from focus. Unload radius: **5 = 160m**. Data-only prefetch radius: **6 = 192m**. Collider radius: **1 = 32m**.
- Concept streaming budgets: max chunk loads/frame **4**, unloads/frame **6**, urgent generation stages/frame **2**, background stages/frame **1**, cache limit **160 chunk records**.
- Current concept finite preset: **96 × 96 = 9,216 playable chunks** (does NOT mean all instantiate; generated/staged per visible region). Square bound for initial visible radius = 81 chunk coordinates; circular mode fewer. The actual count depends on finite-map boundary and readiness.
- These are read-only configured values, NOT proven runtime limits or measured performance.

## Implemented draft tooling

Assets/_Game/Editor/ConceptWorldPerformanceAudit.cs:
- Menu: Little Castle > World > Concept Performance > Validate Concept Units prints configured radii in METERS and per-frame settings without changing them.
- Menu: Start CSV Capture **before entering Play Mode**; capture uses EditorApplication.update every ~0.5 sec while PlayMode is running. Editor SessionState preserves capture toggle/file path across PlayMode domain reload.
- Writes Logs/GPT6-ConceptPerf-UTC.csv with editor time, PlayMode time, Time.unscaledDeltaTime observed at sample (NOT trustworthy standalone FPS), macro road/river/bridge counts, missing/active/desired chunks, urgent/background generation, colliders, cache entries, loads/unloads, recent mesh/spawn/phase timings, managed memory and Profiler total allocated memory. Stop CSV Capture to flush.
- No changes to WorldStreamer, macro generation, scene, Main* or concept settings. No GameObjects or runtime worlds created by the sampler.
- Report a BLOCKED if no WorldStreamer is present, not a zero-runtime-cost PASS.
- Editor-side periodic CSV file writing can influence CPU timings; corroborate with Unity Profiler and Player build.

Assets/_Game/Tests/Editor/ConceptWorldPerformanceTests.cs checks only static concept config and expected finite budget constraints, not true CPU/frame GPU performance.
Stable .meta GUIDs added.

## Desktop acceptance protocol (future Codex working task)

1. Finish/review #7 baseline and import/routing/water/landscape draft branches individually in dependency order. Do not auto-merge. Use new dated EditMode/PlayMode reports.
2. Run concept scene under Unity 6000.5.5f1 in a normal graphical Editor, CPU and GPU profilers and Development Player build as appropriate. Validate no shader compilation/first-use stalls hidden by batchmode.
3. Start CSV capture, press Play and record three repeatable separate passes with same seed and map preset:
   - Fresh process/first bootstrap: exact stopwatch from match creation to MATCH READY; log macro plan computation, sync allocation and number of detailed chunks/GameObjects instantiated before gameplay.
   - First visit: move camera across several new 32m chunks; record main thread worst spikes, generation stages, mesh builds and collider creation, memory/peak.
   - Return visit: same route backward; cache hit pressure, regeneration and GameObject cleanup; verify no leaked meshes, colliders or water.
4. Repeat at least seeds 12345, 54321 and -10101 and 2/8/16 participants; at least a small and large finite map where presets actually allow those player counts. If current profile only exposes concept_preview, **create reviewed separate presets** instead of silently claiming multiple sizes were tested.
5. For each run record Date/Time, exact head SHA and merged PR SHAs, Unity version, GPU/CPU/RAM, resolution, graphics quality/vsync/FPS cap, seed, player count, map preset, warm/cold cache, total play seconds, mean/p95/p99 main thread ms, max frame spike, GameObject count, active colliders, peak memory, first-frame READY count. No unverified numerical PASS thresholds invented.
6. Use the CSV only as auxiliary evidence; explicit Unity Profiler/Memory Profiler and in-game screenshot/recording are required to characterize GPU/visual quality. Headless -nographics CANNOT certify GPU performance.
7. Optimize only after capturing baseline. No world-sized expensive startup materialization, no indiscriminate budget/density raising and no renderer migration without new decision.
8. Preserve docs/reports/verification-2026-10-08 XML as historical baseline. New outputs belong in Logs/ locally or clearly named new dated reports; attach summarized results, do not commit gigabytes of profiler raw traces without approval.

## Formal acceptance status

- Profile settings audit / static test source: PREPARED, Unity not run.
- Measured macro bootstrap/startup timings: NOT RUN.
- Initial world detailed chunk count: NOT MEASURED.
- 2/8/16 actual sessions and different map sizes: NOT RUN.
- First visit vs return + memory measurements: NOT RUN.
- GUI GPU + CPU profiling and asset LOD/streaming: NOT RUN.
- Actual frame limits validated: NOT RUN.

Codex must post separate PASS/FAIL/BLOCKED for each line and actionable ticket/patch for every FAIL; do not close Issue #12 based only on code review.
