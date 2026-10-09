# Q14 — Concept visual quality presets (Low / Medium / High)

Date: 2026-10-09. Contributor: GPT-6 ChatGPT.
Tracking [issue #43](https://github.com/DimaFi/Little-Castle/issues/43).

Stacked **Q14 draft source** is based on
Q13 branch `gpt6/q13-village-night-light-budget-2026-10-09`
at commit `14e41263c360c712b6a943c02a1f372a8cb0319e`.
Q13 depends on Q11 draft PR #55, itself dependent on Q10 #50.
**Q14 also logically depends on the separate Q12 forest
presentation proposal, draft PR #56, which is NOT in the
stacked branch ancestry.** Q14 intentionally references no
Q12-only C# symbol until its independent desktop acceptance.

This is a **source-only presentation configuration proposal**.
Unity import/compile, NUnit, PlayMode, real GPU/FPS and visual
acceptance HAVE NOT BEEN RUN here.

## Owned Q14 scope / no unsafe dependencies

New relative to Q13:
1. `Assets/_Game/Scripts/World/Rendering/ConceptQualitySettings.cs`
   plus .meta — ScriptableObject, tier DTO and conservative logic.
2. `Assets/_Game/Settings/World/ConceptWorld_v001/ConceptQualitySettings.asset`
   plus .meta — saved isolated Low/Medium/High visual profiles.
3. `Assets/_Game/Tests/Editor/QualityPresetTests.cs` plus .meta —
   12 authored NUnit methods.
4. This report.

No edits to preexisting world generation, resources, wheat,
roads, rivers, bridges, fairness, start layouts, deterministic
macro/spawn IDs, seeds, save/delta schema, multiplayer, Unity
QualitySettings, URP assets, shaders/materials, scenes, production
FBX, `WorldStreamer`, `ChunkSpawnPresenter` or global lighting
manager. Concurrent Q08 rocks/scarps remain entirely untouched.

## Proposed profiles — presentation ONLY

These values are initial review candidates. They are **not**
demonstrated target hardware performance budgets.

| Knob | Low | Medium | High |
|---|---:|---:|---:|
| Shadow distance (m) | 28 | 54 | 85 |
| Decorative grass render fraction | 0.40 | 0.70 | 1.00 |
| Forest near authored-prefab distance (m) | 20 | 32 | 48 |
| Forest distant silhouette threshold (m) | 68 | 110 | 170 |
| Optional decorative collider range (m) | 6 | 9 | 14 |
| Full decorative tree prefab advisory capacity | 150 | 300 | 600 |
| Optional decorative tree collider capacity | 10 | 24 | 48 |
| Far cluster individual retention | 0.10 | 0.35 | 1.00 |
| Village realtime requests (max per village) | 0 | 1 | 2 |
| Cheap village ground-pool budget | 2 | 3 | 5 |
| Village realtime range (m) | 18 | 28 | 34 |
| Village ground-pool range (m) | 42 | 64 | 92 |
| Village emissive range (m) | 95 | 140 | 200 |

**No tier raises global realtime lights beyond the existing
NightLightBudgetManager limit of 12.** Q13's root-authored
budget is also an upper bound: `ResolveVillageBudget`
applies field-wise minimums to Q13 values and cannot exceed
approved emitter count, pool count or distance. The manager
remains the sole authority for actual Point/Spot selection;
these values are merely candidate proposals.

The forest fields mirror the semantics intended by Q12
`ForestPresentationSettings`; they are not yet mapped to that
class in source, because Q12 PR #56 is independently pending.
Only an **explicitly reviewed, non-gameplay decorative tree**
with a real approved silhouette and covered cluster may be
visually simplified. An interactive tree or a missing
silhouette stays as a full authored prefab; that may exceed
the advisory capacity and should be measured/reported rather
than invisibly deleting it. Optional colliders are separate
from gameplay-critical collision/nav ownership.

**Decorative grass** never means wheat, crops, harvestable
nodes or authoritative resource vegetation. The existing
world-generation stage must retain all procedural grass/resource
records and IDs regardless of quality. The root presenter
may call `ShouldRenderDecorativeGrass(tier, seed, stableDecorId)`
only after proving a source is strictly visual-only.
The function uses deterministic `DeterministicHash.Hash01`
instead of `UnityEngine.Random`; repeated frames and chunk
arrivals cannot alter the same selection. Because each higher
preset has a larger fraction and the same hash salt/seed,
the visual subset is nested: Low ⊆ Medium ⊆ High. Quality
switching must never trigger world regeneration or save edits.

`TryValidate` rejects nonfinite or negative ranges,
invalid fractions, out-of-range village budgets and
nonmonotonic ordering. The 12-light global manager remains
untouched; `Get` and `ResolveVillageBudget` do not write
any Unity `QualitySettings` or material shader properties.

## Root implementation / measurement handoff

1. Desktop Codex accepts Q10, Q11, Q13 and Q12 in their
   correct dependency order and runs their existing focused
   tests; Q14 stays a stacked draft until then.
2. Validate serialized asset import and the actual selected
   ConceptQualitySettings in Unity 6000.5.5f1. Confirm
   shader/LODGroup support, approved near/far tree art,
   Q13 lantern/window prefabs and no Main asset overrides.
3. Root implements the *presentation-only* adapter into
   `WorldStreamer`/`ChunkSpawnPresenter`/camera/URP
   settings after source approval. Apply shadow distance
   to the appropriate view/renderer settings, not to gameplay
   visibility or fog of war. Do not alter generated macro data
   or evicted runtime deltas.
4. Root explicitly maps Q14 forest fields into accepted Q12
   `ForestPresentationSettings` and limits to a coherent
   active-interest set; preserve interactive trees regardless
   of quotas and never assume GPU instancing without
   Frame Debugger data. Add smooth/hysteretic LOD transitions
   so changing zoom doesn't flash forest silhouettes.
5. Root explicitly maps Q14 village fields to approved Q13
   emitters, without increasing global 12-light budget and
   only after actual Unity prefab import/night render approval.
   Far LOD has no realtime Point/Spot; windows can stay
   emissive without local dynamic lights.
6. Desktop captures quality tier comparisons with
   **same seed, same camera path, same scene/Player
   executable**, day/sunset/night and village/forest/water
   scenes. Record CPU main-thread and render-thread ms,
   GPU ms, SetPass/draw calls, shadow casters, transparent
   overdraw, memory allocations, chunk generation spikes,
   p95/p99 and 1% low Player frametimes on actual target
   hardware. Do not invent 60 FPS or minimum spec claims.
7. Regression verify 2/8/16-player finite sessions,
   negative boundaries and unit path/resource availability
   after switching quality. Generated IDs, river/road
   locations, fixed bridges and save deltas must match
   exactly across all three presets.

This code is a *settings contract*, not a runtime renderer
optimization. Runtime integration is explicitly deferred.

## Authored NUnit source tests (not run)

`LittleCastle.Tests.QualityPresetTests` covers:
- saved concept quality asset import, monotonic budgets;
- no mutation of Unity global shadow distance from Get;
- deterministic grass decisions by seed/stable ID;
- nested Low⊆Medium⊆High visual grass subsets;
- no modification of generated gameplay StableId;
- Q13 authored budget bounds for all three levels;
- High remains inside Q13 2 local / 5 pool / 34m limits;
- invalid quality enum fails;
- invalid profile ordering fails on temporary unsaved SO;
- invalid fractions/nonfinite values are refused;
- malformed authored village budget fails;
- actual composition with Q13's selection returns 0/1/2
  realtime requests (subject to the global manager).

Run in Unity 6000.5.5f1 after accepting dependencies:

```text
-batchmode -nographics -projectPath <root> -runTests -testPlatform EditMode -testFilter LittleCastle.Tests.QualityPresetTests -testResults <Logs/Q14-Quality.xml> -logFile <Logs/Q14-Quality.log>
```

Then rerun `NightVillageBudgetTests`,
`VillageLayoutTests`, `ForestBudgetTests` (after Q12
integration), `NightLightingRuntimeTests`,
`WorldGenerationIntegrationTests`, full EditMode/PlayMode
and graphical Player/GPU capture. Don't combine Unity
`-runTests` with `-quit`. Inspect NUnit XML and import logs.

## Verification state

| Gate | State |
|---|---|
| Q12/Q13 source contracts and actual saved profile reviewed | DONE |
| Saved isolated quality asset, C# config, tests and GUIDs | AUTHORED |
| GitHub diff/source scope | PENDING at report creation |
| Unity compile, saved quality asset import | **NOT RUN** |
| 12 EditMode tests and dependency suites | **NOT RUN** |
| Q12 forest integration into actual ChunkSpawnPresenter | **NOT ATTEMPTED** |
| Dynamic quality adaptation in camera/streamer | **NOT ATTEMPTED** |
| GPU/FPS/1% lows, screenshots, target devices | **NOT RUN** |
| Main production settings / world generation modifications | **NONE** |

Existing historical desktop 175/175 EditMode and 5/5 PlayMode
were obtained on another revision. These do not certify Q14.
Leave Q14 issue OPEN and PR DRAFT pending Codex/root acceptance.
