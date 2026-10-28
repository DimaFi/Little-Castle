# Natural ground-cover and decorative scatter — isolated concept preflight

**Date:** 2026-10-09
**Game:** `DimaFi/Little-Castle`
**Base:** the real desktop integration `integration/desktop-acceptance-2026-10-08`, inspected at `deaf52c595747f3d9dedf6a48b455960f0ca4bdd` (PR #20).
**Work branch:** `gpt6/concept-natural-groundcover-scatter`.
**Related:** issue #11 (landscape), #12 (streaming cost), issue #19 (desktop integration).
**Status: SOURCE + EDITMODE TESTS ONLY. NO NEW UNITY COMPILE, TEST EXECUTION, GAMEPLAY SCREENSHOT OR PERF RESULTS.**

## Source-observed defects and why the fix is limited

The existing `MacroFeatureProjectionStage` writes `PlacementBlockFlags` to chunk cells (settlement clearings, fixed bridge footprint, variable-width river channels, and other placement exclusions). `GroundCoverStage.Generate` then calculates grass density using biome/surface/moisture/forest but **never checks the Grass placement block**, so grassy material can grow through space reserved by real bridge foundations or settlement courtyards. `GroundCoverStage` also has no gradual suppression over intermediate steep slopes unless the logical surface has already become `Rock`.

The existing `ObjectScatterStage` uses the same base scatter probability for decorative rock props almost anywhere its broad terrain/biome masks permit. Both tree and rock `ScatterSpawnRule` in the isolated concept profile allow all major terrain/biomes, so trees can be candidates on `SurfaceKind.Rock` and decorative rocks can feel too uniformly distributed across meadow and rocky country. Changing default game resource density or secretly spawning more objects is explicitly not intended.

## Source changes

1. `Assets/_Game/Scripts/World/Generation/Stages/GroundCoverStage.cs`
   - Two independent defaults-OFF serialized switches `respectGrassPlacementBlocks` and `fadeGrassOnSteepSlopes`.
   - When opted in, any cell blocked with `PlacementBlockFlags.Grass` gets **exactly zero** `GrassDensity`, regardless of biome. Existing road/rock/riverbed surface suppression still holds. Masks come from the real macro projection pipeline, not invented hand-drawn decorations.
   - Smooth `Mathf.SmoothStep` slope fade between serialized `slopeFadeStart=20°` and `slopeFadeEnd=36°`; flat and easy-buildable ground keeps its old density. This is visual scalar data, not one GameObject per grass blade or resource relocation.
   - Does **not** change collision, river geometry, roads, terrain height, `SurfaceKind`, resource counts, neutral buildings or forest-density field.
2. `Assets/_Game/Scripts/World/Generation/Stages/ObjectScatterStage.cs`
   - Two serialized defaults-OFF switches `restrictTreeSurfaces` and `biasDecorativeRocksToSlopes`.
   - If enabled, Tree candidates are accepted only on actual `Grass`, `WoodlandFloor`, or `Mud` surfaces; exposed `Rock`, `Riverbed` and actual path types are excluded even if broad rule terrain/biome filters allow them.
   - Rock **decorative props** (category `SpawnCategory.Rock` only, not `ResourceDepositStage`) multiply their existing spawn chance by affinity: on exposed `SurfaceKind.Rock`, multiplier 1.0; on grass/woodland/mud, smoothly increase from `flatRockChanceMultiplier=0.25` at slopes <=8° to 1.0 by 30°. These values can only **reduce** existing base roll, never raise it, add extra candidates or introduce gameplay resources.
   - Existing deterministic hash salts, jitter, yaw, scale and stable IDs remain unchanged. Any remaining accepted candidates are a subset of the original candidate sequence at the same seed.
3. **Only isolated concept stage assets enabled:**
   - `Assets/_Game/Settings/World/ConceptWorld_v001/Stages/09_GroundCover.asset`.
   - `Assets/_Game/Settings/World/ConceptWorld_v001/Stages/11_ObjectScatter.asset`.
   - Main default stages/settings unchanged; old behavior remains when flags are off. The concept profile is NOT an approved final balance preset.
4. `Assets/_Game/Tests/Editor/ConceptNaturalVegetationTests.cs` + stable Unity `.meta`: five source NUnit tests cover actual negative-coordinate projected `Grass` clearing, slope fade, exact legacy-off behavior, tree stable-ID preservation and exposed-rock rejection, rock probability multiplier 0 rejecting flatland candidates without increasing count, and concept-only flags vs Main defaults. These are in-memory EditMode fixtures, NOT actual GPU gameplay QA.

## Dependencies and Codex desktop runbook

- Work against the previously tested PR #20 integration branch. This isolated patch changes only `GroundCoverStage`, `ObjectScatterStage`, their **concept-only** stage assets, source tests and this report. It does **not** overlap renderer branches #28/#29, bridge/water, routing #27, or terrain preview #26.
- In Unity 6000.5.5f1 with proper LFS model checkout: cherry-pick/review changes in a separate worktree. Compile and run full EditMode/PlayMode with new `ConceptNaturalVegetationTests`, previous `WorldGenerationIntegrationTests`, terrain generation and spawn-ID determinism tests. Save fresh Test Runner XML/logs with exact SHAs. Previously reported desktop PR #20 results **120/120 EditMode, 3/3 PlayMode** are NOT test evidence for this branch.
- Measure real concept world seeds **12345, 54321, -10101, 777**, at 32m / 64-cell chunk profile and both positive and negative coordinates. On each real chunk compare per-cell grass density vs authoritative `PlacementBlockFlags.Grass`, zero on settlements/actual bridge footprints and natural river channels. Check generated rocks/trees vs actual surface: zero new trees on Rock/Riverbed/path; decorative rock probability clusters on highlands without introducing resource-deposit IDs.
- Use the *real* streamed procedural concept scene from [PR #28](https://github.com/DimaFi/Little-Castle/pull/28) after desktop integration for GPU day/night overview/near/far screenshots: approved forest tree models and ground-rock assets only (no placeholder prefabs). At normal strategy camera zoom, assess whether lowland open land, forested meadow, sparse rock slopes, natural riverbanks and stone outcrops form visually plausible patterns. Grass field requires an actual batch/instanced renderer consuming `GrassDensity`: a correct *data* field alone does not guarantee visible grass.
- Compare actual counts **before and after** across identical seed/profile: grass-covered vs blocked cells, tree spawn count by `SurfaceKind`, decorative rock spawn count by slope interval, *resource deposit ID/set/count* and any loss of available gameplay harvesting options. Although only `SpawnCategory.Rock` is intentionally biased, `SpawnCategory.Tree` may represent harvestable wood and changing tree distribution impacts balance; no assertion of 2/8/16-player economic fairness without full play-test.
- Benchmark `GroundCoverStage` and `ObjectScatterStage` per-chunk CPU p50/p95/p99, managed allocations, creation/unload/return, and scene GPU. Do not replace those measurements with synthetic field assertions or alter density blindly.
- Produce a new dated `docs/reports/concept-vegetation-acceptance-YYYY-MM-DD.md` with actual Unity logs and commit hashes, screenshots at same focus/camera, seed matrix, object counts, and PASS/FAIL/BLOCKED; explain any visual deviations. Keep Main/legacy presets and art sources untouched.

## Critical limitations

- This patch is a data-level suitability/clearance fix: it cannot generate rock meshes, authored cliff models, grass rendering or final approved textures by itself. No screenshots or full gameplay scene were created by ChatGPT in this run.
- `PlacementBlockFlags.Grass` must actually be projected for an area. For example, legacy `ProjectRoadMasks` only blocks Trees/LargeObjects/Resources, so the *road SurfaceKind* is the primary grass suppression there; this change does not alter that projection policy.
- Existing bank geometry may contain a data-level fixed nominal riverbed width discrepancy; [PR #26](https://github.com/DimaFi/Little-Castle/pull/26) proposes isolated variable-width surface mapping. Review that independently before evaluating a final shoreline visual acceptance.
- All generation remains deterministic. Concept-only source new fields do not themselves bump the generation version; if these concept settings become production gameplay presets with persisted worlds, do a planned generationVersion migration/save-compatibility check first.
