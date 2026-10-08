# GPT-6 → Codex: desktop integration / verification WORK ORDER

## CURRENT STATUS UPDATE — 2026-10-09 (read before the original plan)

**Do not restart the work order below as though nothing was tested.** Actual Codex desktop integration is published in [game PR #20](https://github.com/DimaFi/Little-Castle/pull/20) (head at inspection `deaf52c`), with real Unity 6000.5.5f1 evidence: final integrated **120/120 EditMode + 3/3 PlayMode**, real Bridge Stone A v002 prefab import, and a single per-chunk streamed `RiverWaterPresenter`. These results belong ONLY to #20's tested branch, not new cloud drafts. The #20 acceptance matrix still reports **strict routing FAIL (3 of 4 tested map seed/size combinations), full water/landscape scene FAIL and real 2/8/16 multiplayer/performance GPU matrix incomplete**. This document's original baseline-first plan below is now historical context; review PR #20 and its reports before doing any new check or merge.

**New independent landscape [PR #26](https://github.com/DimaFi/Little-Castle/pull/26)** (branch `gpt6/landscape-measurement-preflight`, head `5fdd77f`) adds source-only, not-yet-Unity-tested diagnostic seed preview images/JSON, connected buildable-region metrics, negative-chunk/scale tests, and **concept-only** variable-width riverbed classification keyed to each river segment's real width. Main/legacy surface settings unaffected. Start from #20's actually tested integration branch; review/cherry-pick #26, compile and run new tests, then export diagnostic PNGs and compare to real-game camera materials. Do not assume logical `SurfaceKind` directly changes rendering: it must be consumed by a reviewed terrain material/shader pipeline.

The original #17 preflight utility must **not** introduce a second global river presenter: a duplicate provisional water renderer prepared by GPT-6 was reverted after discovering #20. Use #20's runtime water owner and preserve its tested release/reload behavior. New changes in #26 are unrelated to that presenter.

Keep source PRs as drafts until actual validation. Required next work: solve strict route failures and minimum-crossing/connectivity diagnostics on 2/8/16/player/map-size matrix, real procedural concept scene water/bridge/bank/landscape visual acceptance, measurements and shader/material binding, not repetitive baseline testing.


**Prepared:** 2026-10-08 by GPT-6 (user assigned the former Sol 6 / High queue to this assistant).
**This is the NEXT WORKING TASK FOR CODEX, not a proposal or a request for another planning pass.**
**Repository:** DimaFi/Little-Castle. Working base: current-unity-fix at c61a6de1af90c35a843b39325bdc007c1297ff42.
**Art repository:** DimaFi/Little-Castle_Assets, base main at 0e23f677e940ffdb8620abdd381f5bb1b23b0f9e.
**Goal:** Take all GPT-6 draft branches through actual local Unity/Blender/Git LFS verification, fix discovered integration defects, finish remaining real water/landscape work, measure runtime, and produce reproducible evidence. Do not close issues prematurely.

## DO NOT ASSUME A SUCCESSFUL BUILD

All PRs below are **DRAFT / UNMERGED / NOT VERIFIED IN UNITY** as of handoff. No full hydrated art checkout in the ChatGPT execution container. Historical Unity 6000.5.5f1 report is **76/78 EditMode, 0/1 PlayMode**; prior 16 newly added tests passed only on that earlier version. Never copy those totals into a new report.

### Ordered PRs and frozen snapshot SHAs (refresh live state before checkout)

1. Baseline game #7: https://github.com/DimaFi/Little-Castle/pull/14, branch gpt6/issue-7-baseline-2026-10-08, head 9051dbfd6f48d9b2b1cad7beedd9f2f5a886d8ff; report docs/reports/sol6-baseline-verification.md. Three tests reconciled with asynchronous/cooperative lifecycle; do not assume they pass.
2. Routing game #8: https://github.com/DimaFi/Little-Castle/pull/15, branch gpt6/bridge-aware-routing-draft, head 591bcdbe0964a94f20f3abd7ce312b8a0ef2b836; report docs/architecture/bridge-aware-routing.md. Optional retries + guided crossing anchors + connectivity validator/test sources. Verify compilation and real seeds; full valid crossings not guaranteed in impossible terrain.
3. Art #1: https://github.com/DimaFi/Little-Castle_Assets/pull/2, branch art-portability-audit, head f508fb3cab4c3a7ed628d404c2418f80a864209b; report docs/PORTABLE_RELEASE_AUDIT.md. Verifier + Python tests. Must actually hydrate all LFS payloads; 278 tracked source/release binary paths counted in Git tree, NOT verified bytes.
4. Bridge concept import game #9: https://github.com/DimaFi/Little-Castle/pull/16, branch gpt6/bridge-v002-import-preflight, head e37fcdde9992c97e7ee50c9bab2438faaa9de6f0; report docs/reports/bridge-v002-unity-integration.md. Source importer, runtime near collision, 4 EditMode test sources. **No actual generated FBX/prefab assets committed yet.**
5. Water/socket preflight game #10: https://github.com/DimaFi/Little-Castle/pull/17, branch gpt6/bridge-water-contract-preflight, head 5207539f3460ec8d4338f9eadbc369c23bef92c6; report docs/reports/fixed-crossing-presentation.md. World-space socket/waterline math and tests; **actual streamed global water mesh/scene not implemented**.
6. Performance preflight game #12: https://github.com/DimaFi/Little-Castle/pull/18, branch gpt6/concept-perf-audit-preflight, head c8e5d9d015696f207728fd7fb942ba0df377bbcf; report docs/reports/concept-world-performance.md. Editor CSV sampler and config tests; **NO true FPS/CPU/GPU benchmark yet**.

Game #11 landscape remains source/visual integration TO DO. Existing stylized sampler and six tests exist at base with historical pass, but there was no new GPT-6 landscape code PR in this batch; do not infer it is visually finished.

## Mandatory safe integration rules

- Read game AGENTS.md, existing docs/handoffs/sol6-high-mobile-2026-10-08.md, each PR diff/report and current GitHub Issue #13 before touching files.
- On a PC with local modifications: inspect git status, back up work, choose a clean clone or git worktree. Never reset --hard, force-push, blindly checkout over edited Unity assets, merge all PRs at once, change base to main, or silently merge unrelated PR #6.
- Review exact updated refs/SHAs. For each PR, work sequentially in dependency order. Bring into a new integration branch by reviewed merge/cherry-pick as appropriate, run tests, fix before the next PR; do not blindly mark all drafts ready.
- Keep Unity root and art repo independent. Do not link with symlinks/junctions. Only copy the reviewed art release to the Unity project.
- Keep Main* assets untouched; concept settings explicitly opt in. Renderer currently Built-in, no URP/HDRP migration without new decision.
- Keep deterministic seed, finite map, negative borders, no arbitrary resource injection/fairness mirroring, no watermills and no full-map detailed object materialization.
- Never rewrite immutable Releases/**, fixed bridge v002 dimensions/axes/ID, or overwrite original NUnit XML dated 2026-10-08.
- Record every new change as focused commits, PR/Issue comments and dated reports with real outcomes.

## Stage A — restore actual baseline before integration

Review PR #14. Run Unity 6000.5.5f1 compile and full EditMode/PlayMode. Investigate any failure, especially NightLightEmitter Awake/EditMode versus runtime lifecycle; WorldStreamer cooperative initial generation vs formerly synchronous test; mesh release after focus movement/deferred Destroy. Preserve meaningful regressions, do not weaken tests just to make them green. Require zero unexpected compile errors and a fully documented test matrix. Re-run 16 previous concept tests.

### Example Unity batch commands (PowerShell from actual Unity project root)

    $Unity = "C:/Program Files/Unity/Hub/Editor/6000.5.5f1/Editor/Unity.exe"
    $Project = (Resolve-Path ".").Path
    New-Item -ItemType Directory -Force "$Project/Logs" | Out-Null
    & $Unity -batchmode -nographics -projectPath $Project -runTests -testPlatform EditMode -testResults "$Project/Logs/GPT6-Desktop-EditMode.xml" -quit -logFile "$Project/Logs/GPT6-Desktop-EditMode.log"
    & $Unity -batchmode -nographics -projectPath $Project -runTests -testPlatform PlayMode -testResults "$Project/Logs/GPT6-Desktop-PlayMode.xml" -quit -logFile "$Project/Logs/GPT6-Desktop-PlayMode.log"

Confirm actual executable path; do not assume the example path is installed. For GPU/visual acceptance use a graphical Editor/Player, not -nographics.

## Stage B — route connections and finite session acceptance (#8)

Review PR #15 after baseline. Compile, test new BridgeAwareRoutingTests plus all existing tests. Create/test opt-in separate concept routing settings (never silently Main*). Run reproducible seeds 12345, 54321, -10101, 777 and at least 2/8/16 players and every compatible map-size preset. Test negative chunks and boundaries, actual bridge requiredSpan 10.8 m, clear 2.86 m, local road +Z, river +X, base water -0.85 m. Validate all realized road/river intersections have a real matching bridge, and every site belongs to a route. Measure orphan/unbridged/missing graph edges/connected components. Bounded retry failures must yield explicit FAIL, not endless loops, ghost roads or fabricated crossings. Address any corner/vertex double-crossing or guided-anchor false positive found by real Unity tests. Preserve stable IDs and runtime delta.

## Stage C — actual art LFS → Unity concept prefab (#9)

Art repo in a genuinely new checkout with git lfs installed:

    git clone https://github.com/DimaFi/Little-Castle_Assets.git Little-Castle_Assets_validation
    cd Little-Castle_Assets_validation
    git lfs install
    git lfs pull
    python Tools/verify_portable_releases.py --json
    python -m unittest discover -s Tools -p "test_portable_releases.py" -v
    python Tools/sync_asset_book.py
    python Scripts/asset_book.py validate-catalog

Apply reviewed art #2 changes to checkout BEFORE invoking new verifier. Record hashes/output. Open source v002 and actual FBX/UV/textures in Blender 4.4.3. Only then review/import game PR #16. Set environment variable LITTLE_CASTLE_BRIDGE_RELEASE to hydrated Releases/Bridge_Stone_A/v002 absolute path and invoke Editor method LittleCastle.Editor.BridgeStoneAssetIntegration.ImportFromEnvironment. Follow its own report for command. Fix actual FBX/Unity compile/import issues; absolutely no cube/primitive substitute or weakened SHA checks. Commit reviewed generated Unity prefab, textures/meshes, material, metadata and reviewed Git LFS rules. All BridgeStonePrefabTests must **RUN**, not skip. Check 3 real structural/dressing LOD tiers, 286 collider triangles, near/far collision, flag attachments, 1x scale, base Y and yaw, stable site ID, no bridge-local duplicate water/terrain, no material clones, no main catalog change.

## Stage D — finish global river water/road approaches (#10)

Review PR #17 for math only. Confirm there is still no global RiverWaterPresenter in the CURRENT updated code (earlier base did not contain one). Build **one global river-water owner** integrated to streamed chunks (do not make an entire finite-world mesh); use immutable bridge water worldY = baseElevation - 0.85; protect bridge channel to avoid overlapping local water and avoid dry gaps at boundaries. No duplicate terrain-reference mesh. Ensure road approaches connect authored south/north sockets at ±5.4m, with valid smooth grades/river width, no phantom perpendicular river. Stable ownership by bridge ID, correct negative-border/yaw/chunk seams, day/night and close/far screenshot checks. Streaming unload/reload must not leave duplicate water, meshes or bank decor. Record unresolved shoreline/foliage issues explicitly. Do NOT claim #10 finished from pure utility tests.

## Stage E — landscape visual acceptance and actual implementation (#11)

After D hands off concept scene ownership, inspect existing StylizedLandformSampler, StylizedLandformSettings, LayeredTerrainStage, TerrainSurfaceStage and docs/architecture/stylized-landforms.md. Existing source already implements large meadow regions, rounded hills, optional highland/scarp terraces and smooth masks; do not overwrite seed semantics or rebuild legacy-off branch.

- Sample multiple seeds/map spans including ±negative coordinates; measure finite heights, edge seams, gradient/slope distributions, connected buildable ground and scarp/highland masks.
- Generate the playable concept scene from true RTS camera distance, with approved actual houses, trees, roads, river and stone bridge. Take overview, normal gameplay height, close and far shots, day/night. Compare against the user's Tiny Glade-like soft visual direction, but do not pretend a heightfield alone can form real overhangs/rock meshes.
- Prioritize readable path/river valley, broad calm buildable plains, rounded low hills and localized stone escarpments. Fix concrete observed shader/material/mask issues rather than indiscriminately increasing noise/density.
- Implement missing material surface/rock outcrop presentation with separately approved authored assets, preserving built-in rendering. Keep player construction user-selected, no auto-finished player village or artificial resource equality.
- Extend StylizedLandformTests and report actual metrics/screenshots in docs/reports/concept-landscape-review.md. Return FAIL if visual acceptance remains unmet.

## Stage F — measured performance last (#12)

After stages A–E, review PR #18. Start Editor CSV capture before PlayMode and save Logs/GPT6-ConceptPerf-*.csv, then use actual Unity Profiler, Memory Profiler and graphical player/build. Existing concept profile: 32m chunks ×64 cells (0.5 m), load 4=128m, unload 5=160m, prefetch 6=192m, collider 1=32m, cache max 160. These are **settings**, not performance measurements.

Run cold bootstrap to MATCH READY, first visit to newly loaded area, and return trip; record p50/p95/p99 CPU main frame ms, max hitch, GPU frame data (graphical mode), allocations, cache/mesh/collider count, peak memory, draw calls, number of pre-MATCH READY detailed chunks/GameObjects, and seed/map/players/system hardware. Repeat at 2/8/16 players, varied seeds/map sizes; distinguish prewarm/shader compile from steady-state. Do not increase radius/resource density or force full-map generation to mask problems. Report explicit before/after metrics for every optimization, keep source constraints.

## Required final report and deliverables

Create docs/reports/desktop-acceptance-YYYY-MM-DD.md on reviewed integration branch, plus fresh logs/artifact outputs (do NOT overwrite original docs/reports/verification-2026-10-08). For each PR/issue/criterion record:
- exact checked repository, branch, base/head SHA, hardware/Unity/Blender/Python/Git LFS versions;
- actual compile status and NUnit counts (passed/failed/ignored), XML/log relative paths;
- source/art hash integrity, authored 286 collider/LOD triangle counts and materials;
- test seeds, map dimensions, 2/8/16 real player configuration, road/bridge diagnostics and retry failures;
- water and terrain gameplay screenshots (paths), visual gaps/issues;
- streaming/cache/collider/CPU/GPU/alloc/peak-memory numbers and profiling method;
- **PASS, FAIL or BLOCKED**, with explanation, severity, owning file and direct proposed fix/PR;
- list of code fixes made by Codex with commits + retest evidence; unresolved blockers; next executable task.

Do not close Issue #7/#8/#9/#10/#11/#12 or art #1, do not mark PR Ready or merge until its explicit acceptance is demonstrated. A partially working game is better than fabricated PASS labels. If blocked by tooling/licensing/assets, record exact blocker and continue all independent checks.

This is a working implementation+verification assignment. Codex should execute and repair rather than merely re-plan.
