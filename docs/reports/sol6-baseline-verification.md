# GPT-6 / High — Issue #7 baseline verification and Codex handoff

Date: 2026-10-08
Task: https://github.com/DimaFi/Little-Castle/issues/7
Queue: https://github.com/DimaFi/Little-Castle/issues/13
Base: `current-unity-fix` at `c61a6de1af90c35a843b39325bdc007c1297ff42`
Working branch: `gpt6/issue-7-baseline-2026-10-08`
Executor: GPT-6 in ChatGPT, assigned by the user to the existing Sol 6 / High tasks.
State: **source-level fixes proposed; actual Unity verification PENDING**.

## Immutable original baseline (not new results)

From `docs/reports/verification-2026-10-08/{EditMode,PlayMode}.xml`,
recorded on Unity 6000.5.5f1:

- Compilation passed; 16 concept bridge/landform tests passed.
- Full EditMode: **76/78**. Failures:
  - `NightLightingRuntimeTests.NightLightEmitter_FadesInAndOutWithoutInstantPop`: immediately after `AddComponent<NightLightEmitter>()`, a light is still enabled (`Expected: False; But was: True`, line 28).
  - `WorldGenerationIntegrationTests.Streamer_LoadsNearestFirstUnloadsCleansMeshesAndKeepsChunksStationary`: expected one chunk immediately after calling only `ProcessLoads`; actual zero.
- PlayMode: **0/1**. `WorldStreamerPlayModeTests.TestScene_StreamsAcrossFramesAndPreservesRuntimeDelta` failed because no original runtime mesh was observed destroyed after moving focus.

Historical XML is untouched. No new Unity test run has been performed in this ChatGPT environment.

## Findings and source-level changes

1. **Night Light EditMode setup**:
   - `NightLightEmitter.Awake()` already resolves the `Light` and calls `ForceOffImmediate()`, but an EditMode test cannot assume Unity PlayMode lifecycle callbacks run when `AddComponent` is invoked.
   - Modified the EditMode test to invoke the actual private `Awake` method explicitly and then test disabled startup and fade transitions; this tests the implementation instead of a lifecycle assumption. The production light implementation was not modified.
   - Still verify real startup behavior in PlayMode/scene; do not infer it from a reflected method call.

2. **First-load budget in cooperative generation**:
   - `WorldStreamer.ProcessLoads()` presents a chunk only if generated data exists. Missing data queues a cooperative `ProcessGeneration()`, deliberately avoiding synchronous full chunk generation.
   - Old test expected synchronous presentation and hardcoded exactly five visible chunks/9 cache entries, although streaming settings and load radius have evolved.
   - Updated the test to assert initial no synchronous materialization, assert queued urgent generation, and pump `ProcessUnloads -> ProcessLoads -> ProcessGeneration` while checking the per-frame load budget. The test now waits for a bounded number of steps, dynamically uses configured unload/cache limits, and still verifies nearest-first and ownership cleanup.
   - Updated the runtime-delta sub-check to wait for unload/reload beyond the configured unload radius rather than iterating a fixed twelve calls without advancing cooperative generation.

3. **PlayMode focus movement / mesh release**:
   - The test previously set the focus transform, then checked `MissingDesiredChunkCount` **before** a streamer `Update` refreshed desired chunks. If the old view had zero missing chunks, its waiting loop immediately exited; the movement and `Destroy` were not observed.
   - The test now calls the existing public `RefreshStreamingNow()` after each focus movement to establish new desired chunks before awaiting frames. This preserves delayed `Destroy` and production streaming budgets.
   - Actual mesh release remains to be observed in Unity; a passing result has not yet been established.

These are intentionally narrow **test/lifecycle-contract** adjustments, not a claim that no production bug exists. If a real runtime failure remains after these changes, investigate `WorldStreamer.UnloadChunk`, `StreamedChunkView.ReleaseOwnedResources`, cache ownership, and Unity deferred destruction before altering production behavior.

## Files modified in this branch

- `Assets/_Game/Tests/Editor/NightLightingRuntimeTests.cs`
- `Assets/_Game/Tests/Editor/WorldGenerationIntegrationTests.cs`
- `Assets/_Game/Tests/PlayMode/WorldStreamerPlayModeTests.cs`
- `docs/reports/sol6-baseline-verification.md` (this report)

No `Main*` settings, scene, or runtime light/streaming budgets changed.

## Test & validation record

- Source inspection: complete for the reported failing paths, including original NUnit XML.
- GitHub commits: see issue comment and PR; each update is an isolated commit.
- C# compile: **NOT RUN**.
- Unity EditMode: **NOT RUN**.
- Unity PlayMode: **NOT RUN**.
- Blender / visual scene review: **NOT RUN** (not needed for source-level tests).
- Regression of 16 previously green tests: **PENDING**.
- Headless Unity graphics results must not be misrepresented as GPU performance / visual approval.

## Exact next validation on PC

After a clean, safe fast-forward checkout of this branch (or deliberate review/merge of its PR), run Unity **6000.5.5f1**, using a PowerShell shell and the actual Unity Editor executable path:

```powershell
$Unity = "<path-to-Unity-6000.5.5f1>/Editor/Unity.exe"
$Project = (Resolve-Path "./").Path

& $Unity -batchmode -nographics -projectPath $Project -runTests -testPlatform EditMode -testResults "$Project/Logs/GPT6-Issue7-EditMode.xml" -logFile "$Project/Logs/GPT6-Issue7-EditMode.log"
& $Unity -batchmode -nographics -projectPath $Project -runTests -testPlatform PlayMode -testResults "$Project/Logs/GPT6-Issue7-PlayMode.xml" -logFile "$Project/Logs/GPT6-Issue7-PlayMode.log"
```

Then confirm: compilation; full EditMode + PlayMode; 16 concept tests; actual `Awake` behavior on enabled runtime prefab; all old meshes released after crossing configured unload radius and after a subsequent rendered frame; pinned/unpinned and cache limits; removed runtime spawn does not return on regeneration. Record NUnit totals and failures in a **new** dated report (do not overwrite original evidence).

## Codex / next agent instructions

- Read `AGENTS.md`, `docs/handoffs/sol6-high-mobile-2026-10-08.md`, Issue #7, this report and this branch's PR **before** editing overlapping files.
- Treat the task as **in-progress**, not PASS. Review the branch against any newer changes; do not reset, force-push or silently merge the separate gameplay PR #6.
- If EditMode fails, inspect whether test setup and cooperative generation simulation need further correction. Preserve meaningful asserts; never bypass a genuine runtime issue by weakening acceptance.
- Do not start bridge routing/prefab/water issue dependencies until the baseline gate is verified or a documented decision explicitly changes the ordering.
