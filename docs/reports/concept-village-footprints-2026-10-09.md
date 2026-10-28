# Concept neutral-village terrain footprints — source handoff

Date: 2026-10-09

**Status: SOURCE CANDIDATE. Unity compilation, EditMode, PlayMode, prefab wiring and streamed-scene acceptance were intentionally left to the root executor.**

## Implemented contract

`ConceptVillageFootprintPresenter` is a presentation-only component intended for the generated `neutral_village_01` prefab root. It has public serialized references for an approved house and well prefab, with default house half-extents `(2.5, 2.5)`, maximum five-point height spread `0.35m`, and maximum sampled terrain-normal slope `12 degrees`.

The layout is deterministic and entirely local to the village root:

- house centers: `(-7, 0, -7)`, `(7, 0, -7)`, `(0, 0, 8)`;
- each house yaw faces local village center;
- the well stays at local center;
- no random values, global lookup, cached world-generation data, terrain edits, or physics setting changes are used;
- the presenter never edits `WorldStreamer` or `ChunkSpawnPresenter`.

Each house requires successful downward ray samples at its center and four yaw-rotated footprint corners. A support hit is accepted only when the hit collider is the root `MeshCollider` on the same GameObject as a playable `StreamedChunkView`, and that view reports its terrain collider enabled. Child, house, bridge, plant, trigger, non-mesh, padding-chunk and unrelated mesh colliders cannot qualify.

Every configured slot is sampled before a placement decision. No structure is instantiated during retries. When every requested sample is available, each house is independently checked for finite five-point height spread and terrain-normal slope; unsupported houses are knowingly skipped. Supported houses are placed at the minimum sampled height with a `0.05m` sink. The prefab's authored local scale is not reassigned. The well requires a valid playable-terrain center hit and a supported center slope.

## Bounded runtime limits

- At most **60** complete sampling attempts.
- **0.5 real seconds** between attempts (maximum retry window is about 29.5 seconds after the immediate first attempt).
- `RaycastNonAlloc` uses a fixed **32-hit** per-presenter buffer and a **1024m** downward ray beginning **512m** above the village sample height.
- A ray result that fills the 32-hit buffer is considered ambiguous overflow and fails closed.
- Missing colliders after the final attempt produce no structures and set `PlacementFinished = true`.
- `PlacedHouseCount` reports only successfully instantiated houses; `PlacementFinished` is read-only to consumers.

## Pure EditMode coverage

New focused test filter:

```text
-runTests -testPlatform EditMode -testFilter LittleCastle.Tests.ConceptVillageFootprintTests
```

The six pure tests cover five finite samples, NaN rejection, infinity rejection, the inclusive maximum-spread boundary, excessive spread, and unsafe arguments (null/wrong count/negative or NaN limit).

Recommended root execution after prefab/catalog wiring:

```text
-runTests -testPlatform EditMode -testFilter LittleCastle.Tests.ConceptVillageFootprintTests
-runTests -testPlatform EditMode -testFilter LittleCastle.Tests
-runTests -testPlatform PlayMode
```

## Root-owned Unity acceptance still required

1. Add this presenter to the generated neutral-village prefab root and assign only the approved house/well prefab references.
2. Keep the village catalog entry scale multiplier at `1`.
3. Confirm a flat streamed playable location finishes with three non-overlapping houses and the center well, with all house fronts facing inward and authored prefab scale preserved.
4. Confirm missing neighbor terrain colliders create nothing before they arrive; after arrival, placement happens once and does not depend on chunk arrival order.
5. Confirm an unsafe footprint skips that house without moving the slot, flattening terrain or substituting cached heights.
6. Confirm non-terrain colliders above a sample are ignored, child terrain-like colliders are rejected, padding chunks are rejected, and a deliberately saturated ray buffer fails closed.
7. Unload/reload the owning streamed chunk and confirm its village children follow the chunk lifecycle without leaked structures.

No Unity editor or player was launched for this source handoff, so there is no runtime visual, compile, NUnit or physics-scene result to claim yet.
