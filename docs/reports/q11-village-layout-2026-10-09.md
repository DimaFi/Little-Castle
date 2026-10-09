# Q11 — Deterministic neutral-village data layout / Codex handoff

Date: 2026-10-09. Source contributor: GPT-6 ChatGPT.
Tracking: [issue #40](https://github.com/DimaFi/Little-Castle/issues/40).
**Stacked dependency:** Q10 [draft PR #50](https://github.com/DimaFi/Little-Castle/pull/50),
branch `gpt6/q10-footprint-data-validator-2026-10-09`,
commit `6a28b447e8fece8afc4f10d39665defad34b7acd`.
Work branch: `gpt6/q11-deterministic-village-layout-2026-10-09`.
Status: **source/test candidate; not integrated or tested in Unity**.

## Why

The initial `ConceptVillageFootprintPresenter` directly creates three
houses from streamed terrain physics observations at fixed offsets.
The new Q10 validation contract verifies a house foundation/door as
world data but does not compose a village: houses may overlap, doors
might end in walls, and a square/well needs a safe walkable area.
Streaming collider arrival order must not become authoritative.

This Q11 PR introduces a **pure deterministic candidate planner** for
the first small village while deliberately preserving the original
presentation-only concept prefab and its existing runtime behavior.

## Files owned relative to Q10

- NEW `Assets/_Game/Scripts/World/Core/VillageLayoutData.cs` + stable Unity `.meta`
- NEW `Assets/_Game/Scripts/World/Macro/VillageLayoutPlanner.cs` + `.meta`
- NEW `Assets/_Game/Tests/Editor/VillageLayoutTests.cs` + `.meta`
- THIS report

No changes to `WorldStreamer`, `MacroWorldPlanner`,
`ConceptVillageFootprintPresenter`, generation stages,
scenes, source FBX, prefabs, Main, shader/material pipeline or save schema.
The change only adds unused code until root integration after Q10 acceptance.

## Authoritative data-only contract

`VillageLayoutPlanner.TryPlan(worldSeed, villageStableId, worldCenter,
settings, sampleWorld, out layout, out failure)` requires
`Func<Vector2, BuildingSiteSample>` from **approved world-space
terrain/macro data**, not a scene physics raycast.

Successful `VillageLayoutData` includes:
- the same authoritative `VillageStableId` as the macro settlement anchor;
- logical `WellStableId`, well/courtyard world center and verified
  minimum well support height;
- exactly **3, 4 or 5** house descriptors with logical stable IDs,
  slot index, real world X/Z center, yaw facing the courtyard and
  minimum foundation height;
- corresponding local `VillageApproachPath` data: house ID, path ID,
  doorway edge, courtyard ring edge and reserved path half-width.

Stable IDs are derived via `DeterministicHash.StablePairId` from
world seed + unchanged village stable ID + logical slot + distinct
entity type salt. Retrying a rejected house site does **not**
change its logical identity or change the order of other slots.
Global rotation of a village is deterministic from the same inputs,
with no mutable `UnityEngine.Random`.

The result is returned only after the well, ring, ALL requested houses
and ALL their approaches have passed. A failed attempt returns
`layout = null`, typed `VillageLayoutRejection`, attempted candidate
count, slot, rejected point and last underlying footprint rejection.
No partial village structures are committed/spawned.

## Bounded algorithm and safety

1. Verify all settings and inputs for finite geometry.
2. Validate center well ground (requires Ready, Playable, Walkable,
   Buildable, not Road/Water/Occupied, slope bound).
3. Validate 12 points around the well radius for support (also
   max well height spread), plus 12 points around the courtyard
   radius for walkability — **25 fixed samples**.
4. Generate equally spaced deterministic house slots on a ring
   outside the courtyard. Each has **eight fixed alternatives** in
   reproducible angular/radial order.
5. Before expensive terrain probing, compare the candidate house
   oriented OBB with already placed house OBBs (four-axis SAT), and
   reject intersections between reserved approach corridors and
   existing house/approach geometry, using slab clipping and
   segment-distance tests. Maintain a minimum configurable separation.
6. Run Q10 `BuildingFootprintValidator.Validate` on actual oriented
   footprint and entrance. Requires Ready/Playable/Buildable
   foundation, no Water/Road/Occupied, bounded interior support
   height spread and slope, walkable unoccupied entrance and step.
7. Check a small path from the real local +Z doorway edge to the
   shared courtyard ring. Sample left/middle/right lanes at a
   configurable interval; require Ready/Playable/Walkable and
   no Water/Occupied; reject steep steps and slopes.
8. Commit only the successful entire layout.

Bounds:
- max **5 houses × 8 alternatives = 40 candidate placements**;
- each Q10 footprint max **9×9 + 3×3 = 90** samples;
- each local approach max **64 segments × 3 lanes + start
  = 195 lane samples**;
- well+ring sample count **25**;
- therefore strict worst-case raw samples
  `25 + 40 × (90 + 195) = 11,425`, independent of world-map
  dimensions and fixed-slot village count. Spatial checks are
  bounded by previously accepted ≤4 houses. Most actual layouts
  use far fewer calls; this is an operation-count bound, **not
  measured CPU time**. The sampler may itself be expensive and
  must not materialize entire-world detailed chunks.

Float math uses the current project world X/Z coordinates and
yaw convention. Negative-coordinate worlds work without
chunk-local rounding. No terrain flattening, GameObject, Physics,
FBX, random draw or collider arrival order is used.

## Known limitations — do not misrepresent as gameplay navigation

This is a **discrete local walkable-corridor** check plus exact
geometric separation among the planned houses/paths. It does NOT
prove unit NavMesh/pathfinding connectivity to the external road
network, suitable caravan width, accessibility for a 16-player
battle path, player building compatibility, forest/resource
fairness, or that every tiny obstacle smaller than path-sample
spacing is found. Macro road, bridge, river and dynamic occupancy
masks are trustworthy only if the root sampler supplies correct
readiness and authoritative exclusions.

The 12-point courtyard ring and 9×9 grid are bounded sampled
approximations; not continuous exact slope or polygon coverage.
No explicit generation-version/save migration is done by Q11;
root must approve serialized definition values, physical house
BoxCollider footprint, door pivot (+Z facing courtyard), well
radius, stable village and house IDs, and placement version before
shipping. The approach local paths are **logical planned corridors**,
not upgraded `WorldRoadData` and not automatically painted terrain
or baked navigation.

The root integrator must also handle:
- no suitable layout: deterministic failure policy (reject entire
  village anchor or resample within bounded strategic candidate
  selection), never quietly spawn fewer houses or flatten the map;
- streaming: place from stored plan data only after required chunks
  are playable/presented, not from collider arrival order;
- fair multiplayer spawn/resource mask coupling and positive/negative
  seams; identical world seed/settings must generate identical
  layout after eviction/revisit;
- ensuring a real road and unit-usable traversal from village
  courtyard to wider graph; never claim connectivity from an
  approach line alone;
- root-specific prefab/well dimensions from approved production
  assets, not arbitrary source code constants.

## Tests (authored; not yet executed)

`LittleCastle.Tests.VillageLayoutTests` covers:
- 3, 4, 5 houses on flat world plus well and entrances toward center;
- stable per-house/per-path ID, yaw, positions and well at a negative origin;
- changed world seed and village ID change derived logical IDs;
- occupied/water well or courtyard fail with null layout;
- missing peripheral terrain exhausts exactly 8 alternatives,
  retains underlying `SampleUnavailable` rejection;
- blocked first house support falls back to another position
  **without changing logical house ID**;
- tiny obstruction on the planned entrance-to-courtyard path
  triggers deterministic alternative;
- invalid data/sampler/candidate coordinate fail without probes;
- height-spread and slope/site mask rejection of courtyard;
- extreme path length fails **before** an unbounded sample loop.

Need desktop Unity 6000.5.5f1 focused EditMode:

```text
-batchmode -nographics -projectPath <root> -runTests -testPlatform EditMode -testFilter LittleCastle.Tests.FootprintValidatorTests -testResults <Logs/Q10-Footprint.xml> -logFile <Logs/Q10-Footprint.log>
-batchmode -nographics -projectPath <root> -runTests -testPlatform EditMode -testFilter LittleCastle.Tests.VillageLayoutTests -testResults <Logs/Q11-Village.xml> -logFile <Logs/Q11-Village.log>
```

Run in separate Unity processes and do **not** pass `-quit` with
`-runTests`. Read actual XML, logs, skipped/error counts.
Then run existing `ConceptVillageFootprintTests` and
`ConceptVillageRuntimeTests`, full EditMode and PlayMode tests,
and one real generated same-seed village route/camera assessment.

Desktop root must test actual approved house prefab scale/entrance,
real river/bridge/path masks, 2/8/16-player region boundaries, negative
chunk seams, repeated seed, unload/revisit, and sampled walkability
versus actual NavMesh/agent clearance. Capture day/night/near/far GPU
screenshots and validate no hovering/intersecting houses.

## Verification status

| Gate | Status |
|---|---|
| Q10 source and task/architecture contracts reviewed | DONE |
| Pure Q11 data/planner/test source and GUIDs authored | DONE |
| Exact Q11 GitHub diff | PENDING at report creation |
| Unity compile / focused EditMode | **NOT RUN** |
| Full EditMode/PlayMode / scene | **NOT RUN** |
| Actual measured Player frame budget or unit paths | **NOT RUN** |
| Production prefab/root sampler integration | **NOT ATTEMPTED** |

The earlier desktop 175/175 EditMode and 5/5 PlayMode results
describe a different tested revision, not this Q11 code.
Keep Q10 and Q11 in draft until root/Codex desktop acceptance,
then merge Q10 first, Q11 second without silently touching Main.
