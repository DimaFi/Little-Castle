# Q15 — Minimal saved-profile map regression audit (Codex handoff)

**Date:** 2026-10-09. Source contributor: GPT-6 ChatGPT.
**Issue:** [#44](https://github.com/DimaFi/Little-Castle/issues/44).
**Branch:** `gpt6/q15-concept-map-regression-audit-2026-10-09`.
**Base:** `codex/concept-world-pass-2026-10-09` at
`e6a02df6f57461df9cd7cf853e8e9d62b776d71a`.
**Status:** isolated Editor tool + source tests authored;
**Unity compilation, NUnit, source sampling, Player or FPS tests NOT RUN** here.

## Reason and strict ownership

The current concept preview has a verified 1024 m streamed
`concept_slice` and a configured 3072 m `concept_preview`, but a
single prior desktop test seed with one fixed bridge is not evidence
of balanced, accessible 2–16-player matches. Q15 provides an explicit
small regression checkpoint and leaves the expensive matrix under
root/Codex desktop ownership.

**Changed Q15 files only:**
- NEW `Assets/_Game/Editor/ConceptMapRegressionAudit.cs` (+ GUID .meta);
- NEW `Assets/_Game/Tests/Editor/ConceptMapRegressionTests.cs`
  (+ GUID .meta);
- THIS report.

**No edits to** Main, existing saved ScriptableObject assets, scene,
macro/river/road algorithms, resource/fairness settings, Q08 rock/scarp
parallel work, existing tests, Unity assembly definitions or
`WorldStreamer`. This tool reads what is actually in
`Assets/_Game/Settings/World/ConceptWorld_v001/`.

## Manual bounded diagnostic — actually executes when explicitly invoked

Unity menu:

`Little Castle > World > Diagnostics > Q15 Small Map Regression`

Batch command (separate from tests):

```text
-batchmode -nographics -projectPath <root> -executeMethod LittleCastle.Editor.ConceptMapRegressionAudit.RunSmall -logFile <Logs/Q15-audit.log> -quit
```

It writes JSON to ignored project-root
`Logs/Q15-ConceptMap-YYYYMMDD-HHmmss.json` (not tracked
`Assets`/scene/settings), including actual UTC, saved asset paths,
asset GUID, world/profile IDs, generation version, chunk size and
map-preset admission constraints.

The command deliberately performs **one bounded workflow**:

1. Load the **saved** `ConceptWorldDefinition.asset`,
   `GenerationSettings`, `MacroPlannerSettings.Rivers`,
   `WorldMapRules` and real stages through `AssetDatabase`.
   Throw if missing, wrong path or empty pipeline. No clone or
   automatic edits of river chance/flags, no Main fallback.
2. For **exactly four seeds** `12345`, `54321`, `-10101`, `777`,
   run only `RiverNetworkPlanner.BuildRivers` with one fresh
   `WorldTerrainProbe` per seed, using the saved `concept_slice`
   playable Rect plus **saved** `PlanningHalo`.
   This is an actual 1024 m source-policy scan, **not** a full
   road/bridge/fairness/map generation per seed.
3. Record for every seed river count, sorted stable IDs, duplicate-ID
   check, `RiverPlannerDiagnostics` including ordinary/fallback
   trace/eligibility counters, CPU wall milliseconds and any exception.
   A seed with zero rivers is recorded `NO_RIVER_SOURCES_ACCEPTED`,
   not silently marked successful. A source could exist outside the
   playable area because the halo is wider: do not claim in-map
   river/bridge crossing without an actual geometric check.
4. Run **one** small `MacroWorldPlanner.GenerateForBounds` using
   original saved settings with `Rect(-128,-128,256,256)` (metres)
   and the saved halo, on seed `-10101`, with a real
   terrain-only probe. Catch/report a strict-routing exception;
   never hide a failed road/bridge routing as successful.
5. If the small macro builds, run the *existing*
   `WorldRouteConnectivityValidator.Validate` and record
   feature/connection/realized path/river/bridge counts, source
   stable IDs (as deterministic sorted decimal text), components,
   missing realized routes, orphan/unbridged crossings, errors and
   whether strict-routing was attempted/satisfied. This is
   **graph/geometry data validation, NOT unit NavMesh connectivity**.
6. Dispose/release each probe's cache with `Clear()` even on an
   exception. No generated chunk renderer, GameObject instantiation,
   streaming into active scene or long-term entire-map index.

### Explicitly NOT part of default Q15

- **3072m or 6144m** source/whole macro run;
- 2/8/16-player full start fairness and comparable supplies;
- real physical exit-count measurements, walkability around starts,
  reachable resources or 16-player collision-free unit paths;
- complete chunk mesh/normal/water/bridge seam validation;
- live WorldStreamer eviction/revisit with persisted runtime deltas;
- GPU/Player draw calls/FPS/1%-low, actual 60 FPS approval;
- any asset approval, visuals or full production release.

These report fields are `NOT_RUN` or
`NOT_AN_ACCEPTANCE_RESULT`, **never true/zero-as-pass**. Saved
`concept_preview` accepts up to 16 players in its *settings*
but this does **not** mean 16-player fairness or PvP travel is proven.

The small macro has an **expanded planning halo** even though its
requested playable area is 256 m; root must compare actual work
counts and timestamps before considering it a cheap CI job.
Only the menu/explicit `-executeMethod` invokes the real four-source
and single-small-macro evaluation. Ordinary NUnit tests do not.

## EditMode source tests (lightweight)

`LittleCastle.Tests.ConceptMapRegressionTests` contains 12 authored
test methods:

- four-seed roster and negative seed/small macro input limits;
- saved concept asset identity and source/reference paths (no hidden
  synthetic flags or asset copies);
- actual saved `concept_slice` 2-player vs
  `concept_preview` 16-player *admission* settings;
- finite `WorldSessionMapFactory` playable/visual bounds
  and out-of-bounds rejection on saved preset;
- stable decimal ID canonical ordering across enumeration order,
  explicit duplicate count;
- synthetic macro with logical road but no realized geometry must
  return failed route/geometry, NOT accepted NavMesh;
- empty macro does not become "multiplayer ready";
- sample height equality across a negative/positive X chunk boundary,
  and a mismatched boundary rejects;
- all-sample terrain equality for a repeated chunk coordinate;
- synthetic generated tree removal survives after regeneration
  **only while the same runtime delta is retained**;
- missing saved asset inputs and null analysis input reject safely;
- report fields visibly distinguish unmeasured exit/seam/eviction
  and PvP fairness rather than filling defaults with a pass.

The tests invoke the Editor-only utility by reflection because the
existing `LittleCastle.Tests.EditMode.asmdef` references
`LittleCastle.Runtime` but **not**
`LittleCastle.Editor`. Q15 deliberately does not change shared
assembly definitions.

Run in Unity 6000.5.5f1:

```text
-batchmode -nographics -projectPath <root> -runTests -testPlatform EditMode -testFilter LittleCastle.Tests.ConceptMapRegressionTests -testResults <Logs/Q15-EditMode.xml> -logFile <Logs/Q15-EditMode.log>
```

Do not add `-quit` on `-runTests`. Inspect NUnit XML and Unity
compile/import logs, not exit code alone. Then run existing focused
`ConceptWorldProfileTests`, `WorldStartFairnessTests`,
`FiniteSessionMapAndFogTests`, `WorldRouteEndpointTests`,
`RiverSourceFallbackTests`, and the full EditMode/PlayMode suites.

**No saved-profile mutation:** after tests and menu run, verify
`git diff -- Assets/_Game/Settings/World` is empty. Logs remain
ignored/untracked; don't upload unbounded per-seed art or geometry.

## Extended root-only acceptance matrix, separate explicit run

**Not implemented or executed in this Q15 task**, by design. For
root/Codex desktop follow-up, record one matrix row for each selected
seed/preset/party-size combination:

| Dimension | Requirement |
|---|---|
| Seeds | include 12345, 54321, -10101, 777 and additional failure/revisit examples |
| Finite map | real 1024/3072/6144 m config, with playable vs planning halo clearly separated |
| Player count | 2/8/16 only when saved preset allows it |
| River/source | accepted ordinary vs fallback, no-river failure explicit, real in-playable crossing |
| Graph | actual realized connected components and missing desired edges |
| Paths | geometric road connection endpoints, bridge crossing contact and unit-navigation follow-up |
| Starts | all players placed, fair supply range, independent exit count, no trapped center start |
| IDs | stable macro object IDs and chunk spawn IDs after full regenerate |
| Seam | exact neighbor heights plus mesh normals/water banks/bridge contact |
| Eviction | remove tree/resource/runtime building, evict and revisit, ensure delta survives |
| Cost | CPU stage breakdown, generation stalls, resident caches, Player 1% low frame times |
| Visual | PlayMode near/far/day/night captured against same seed and camera |

Large map tests must be explicitly budgeted and run after
root integration of upstream PRs; a synthetic/source-only test cannot
certify target-device performance or PvP balance.
The current Q15 four river seeds plus one small macro are *regression
signals*, not a release gate for all seeds.

## Honest verification status

| Check | Status |
|---|---|
| Existing Q15 issue, saved profile and actual API inspected | DONE |
| Editor diagnostic, NUnit tests, `.meta`, report authored | DONE |
| Exact Q15 GitHub diff / readback | PENDING at report creation |
| Unity C# compilation / focused EditMode XML | **NOT RUN** |
| Real four-seed river source audit JSON | **NOT RUN** |
| One small 256m macro audit JSON | **NOT RUN** |
| Full EditMode/PlayMode | **NOT RUN** |
| 1024/3072/6144 × 2/8/16 root matrix | **NOT RUN** |
| Frame Profiler / target Player GPU / visual scene | **NOT RUN** |

Historic 175/175 EditMode and 5/5 PlayMode from the earlier desktop
concept revision do not constitute verification of Q15. Keep issue
OPEN and PR DRAFT until the root owner runs the actual audits and
accepts their real output.

Parallel Q08 scarp/Blender agent work is left completely untouched.
