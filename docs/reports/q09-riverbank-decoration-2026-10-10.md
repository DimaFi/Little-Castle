# Q09 — Bounded and collision-safe riverbank decoration candidate

Date: 2026-10-10. GitHub issue #38.
Branch `gpt6/q09-riverbank-vegetation-scatter-2026-10-10`.
Dependency base: Q05 draft PR #63 head
`7fb7aaf03b16e8bc4aebdee409447a77566e341e`;
Q05 depends on Q04 draft PR #52.

**Source/test/asset authoring only. Unity compile, NUnit,
PlayMode, production prefab art intake and Player/GPU checks NOT RUN.**

## Owned Q09 changes

- NEW `Assets/_Game/Scripts/World/Generation/Stages/RiverbankDecorationStage.cs`
  plus stable Unity .meta GUID.
- NEW `Assets/_Game/Settings/World/ConceptWorld_v001/Stages/12_RiverbankDecoration.asset`
  plus .meta.
- NEW `Assets/_Game/Tests/Editor/RiverbankDecorationTests.cs`
  plus .meta.
- THIS report.

Absolutely no existing Main, current concept world stage list,
WorldStreamer, river source/water/shader, terrain height,
bridge terrain, road network, resource deposits, save/runtime
deltas, prefab catalog, Unity scenes, or CliffKit/scarp PR #62
changed. This only adds an **isolated disabled concept stage
asset** and data-generation code.

## Design and acceptance boundaries

`RiverbankDecorationStage` is a new LocalSpawns phase provider.
It emits **WorldSpawnData** records rather than GameObjects,
with **SpawnCategory.Decoration only**. Even a visually bush-like
asset remains decorative; using gameplay Bush/Tree/ResourceVisual
is explicitly invalid. The stage does not change forest density,
grass density, terrain, SurfaceKind or authoritative resources.

Saved concept rules in a **disabled** asset (`enabledStage: 0`):

| Logical archetype ID (intentionally unbound) | Spacing | Chance | Bank offset | Min moisture | Slope |
|---|---:|---:|---:|---:|---:|
| concept_river_reed_PENDING_INTAKE | 5m | 0.38 | 3.5–8m | 0.55 | <=15° |
| concept_river_shrub_PENDING_INTAKE | 9m | 0.15 | 5–13m | 0.45 | <=22° |
| concept_river_flower_PENDING_INTAKE | 9m | 0.07 | 4.5–11m | 0.50 | <=18° |

`PENDING_INTAKE` is a deliberately visible reminder that
**NO finished plant prefab was found/approved for these archetypes**.
Do not enable this stage, include it in the saved pipeline,
or add these archetype IDs to production catalogs until the
independent Blender/texture asset agent has prepared real
LOD-ready game-ready visuals and root has accepted it. Do not
pretend a placeholder ID is a production art asset.

One deterministic absolute-world grid per authored rule
produces cells independent of chunk arrival order:
- hash keys = world seed + absolute grid coordinates + stable
  ruleId salt from `DeterministicHash.String32`;
- jitter, chance, yaw, scale and 64-bit logical stable ID are
  independent fixed salt draws;
- candidate belongs to exactly one chunk using half-open
  world bounds [minX,maxX) × [minZ,maxZ);
- repeated generate on the *same* chunk checks preexisting
  stable IDs and will not append duplicates; regenerating
  the same chunk from scratch yields same ID/location/scale;
- per-chunk work is **explicitly bounded** by
  `maxCandidateChecksPerChunk=1024` and
  `maxSpawnsPerChunk=24` as serialized concept defaults,
  no world-size-dependent persistent spatial index;
- one optional chunk-local list of Q04
  `RiverEnvelopeUtility.Segment` and eligible road segments
  built **once** via conservative expanded world AABB.
  No physics rays, Task.Run or full-map GameObjects.

**Dry land only**:
- signed bank distance = Q04 segment distance minus its
  linearly interpolated source river half-width; require
  every candidate to be outside all collected river ribbons
  and within the rule-defined positive bank offset;
- explicit natural surface filter only
  `Grass`, `Mud`, `WoodlandFloor` — NOT Riverbed/Rock,
  Trails, Dirt/Improved/Settlement/Fortified roads;
- cell moisture and slope must satisfy rule thresholds;
  reject nonfinite slope/height/moisture;
- honor `PlacementBlockFlags.Decoration` projected by
  existing MacroFeatureProjectionStage (including river
  exclusion clearance and settlement mask);
- **independently** exclude road corridor
  `halfRoadWidth + roadExtraClearance=2m`, because legacy
  macro projector may block only Trees/LargeObjects/Resources
  there, not Decoration;
- exclude all macro point feature influence radii +2m,
  fixed bridges using the canonical local yaw-oriented
  `SupportHalfExtentX/Z +2m` and nonfixed bridge sites
  with a conservative radial exclusion of 8m+2m;
- no decoration added to bridge arches/path approach lanes
  and no duplication of a future root-owned bridge
  dressing pipeline.

**Potential production corner cases:**
- This Q09 source assumes the generation stage executes after
  `MacroFeatureProjectionStage`, `ClimateStage` and
  `TerrainSurfaceStage`, as intended by LocalSpawns phase.
  If root calls it before those stages, readiness/surface masks
  are absent; do not integrate out of order.
- Distances are sampled at bounded world-grid points, not
  a swept-footprint for large authored shrubs; final
  prefab bounds/nav clearance must be checked by root.
- Fixed bridge footprint uses supported `v002` contract,
  not a hand-waved radius; legacy bridge rejection uses
  conservative fallback. Future bridge art footprint changes
  need versioned clearance checks.
- The same rule ID across multiple rules can create ID
  collisions; root should approve unique ruleId values.
  Built-in emitted-ID set avoids identical spawn IDs but
  does not silently authorize duplicate serialized rule IDs.
- A global per-chunk candidate cap may truncate rare later
  rules on larger chunk formats. It is deterministic but
  **not a guarantee of a minimum floral density**. Root
  must profile/adjust cap for the intended map size.
- Cross-chunk visual instance meshes, batching/instancing,
  LOD/wind and interaction behavior remain separate
  art/presenter responsibilities. Root should ensure
  these are not gameplay resources.
- Introducing this stage to production affects actual
  generated spawn records and therefore may require a
  generation-version/save-compatibility review (Q16).
  Existing worlds remain unchanged while the asset is
  disabled and not in saved pipeline.

## Authored tests (11 methods, not executed)

`LittleCastle.Tests.RiverbankDecorationTests` covers:
- no river => zero decoration;
- wet flat bank => produced marks outside true variable
  source ribbon, only natural surface and decoration category;
- adjacent negative and positive chunks do not share IDs;
- fresh same-seed chunk regenerate gives identical IDs,
  yaw/position/scale; repeating in-place is idempotent;
- dry/steep/blocked/riverbed all refuse spawn;
- real road centerline exclusion despite legacy mask;
- fixed bridge yaw-oriented support/entry area stays clear;
- macro settlement influence radius stays clear;
- max candidate/spawn caps cannot exceed configured bounds;
- malformed rule, gameplay Bush category rejected;
- actual serialized concept stage imports DISABLED with
  three `PENDING_INTAKE` IDs.

Run from isolated stacked Q04+Q05+Q09 Unity 6000.5.5f1:

```text
-batchmode -nographics -projectPath <root> -runTests -testPlatform EditMode -testFilter LittleCastle.Tests.RiverbankDecorationTests -testResults <Logs/Q09-Bank.xml> -logFile <Logs/Q09-Bank.log>
```

Then run Q04 `RiverEnvelopeTests`, Q05
`RiverWaterJoinTests`, `WorldGenerationIntegrationTests`,
`RiverWaterPresentationTests`, `FixedBridgeGenerationTests`,
full EditMode and PlayMode. Do not use `-quit` with
`-runTests`; evaluate the XML/Unity compiler results,
not historic green counts.

## Required real desktop / root art acceptance

1. Import the isolated disabled stage asset and confirm
   script GUID, three rules/IDs, no saved profile override.
2. Approve actual reed/shrub/flower LOD meshes, foliage
   materials, mesh normals/UV/wind masks, batching and
   tree/resource distinctions. No production asset invented.
3. Test a copy of the Concept world pipeline in a **separate
   scene/profile** with the new stage deliberately enabled;
   do not change Main or user-owned pipeline without approval.
4. Overlay 2/8/16-player map starts and resource road
   corridors, bridges/confluences on actual camera route,
   inspect entry walkability and seam at negative coordinates.
5. Revisit chunk after streamer cache eviction and compare
   stable IDs, absence of duplicate instances, 1% low frame
   times, worst chunk generation spikes and GPU vegetation
   overdraw in day/night and RTS near/far.
6. If data/spawn profile becomes authoritative, review Q16
   generationVersion and backward compatibility.

## Verification status

| Gate | Status |
|---|---|
| Q04/Q05/bank masks/world spawn/bridge code inspected | DONE |
| Additive Q09 stage/disabled asset/tests and GUIDs written | DONE |
| Exact source diff and readback | PENDING at report creation |
| Unity compiler/import/NUnit | **NOT RUN** |
| In-scene visual/NavMesh/Player/GPU | **NOT RUN** |
| Approved production reed/shrub/flower prefabs | **NOT PRESENT** |
| Existing Main/prod pipeline and Q08 CliffKit changed | **NO** |

Previous desktop 175/175 EditMode+5/5 PlayMode applies to
different code and is NOT Q09 test evidence.
Keep issue OPEN and PR DRAFT pending root/Codex approval.
