# Rivers, Roads and Bridges

## Current implementation status

The repository contains executable first-pass algorithms for rivers, roads and bridges.

They are intentionally data-first and deterministic, but still require visual/runtime validation in Unity before production use.

## Rivers

Files:

- `RiverPlannerSettings.cs`
- `RiverNetworkPlanner.cs`
- `WorldRiverData.cs`
- `RiverTerrainCarvingStage.cs`

Current pipeline:

```text
terrain probe
    ↓
stable source candidates
    ↓
downhill tracing
    ↓
tributary → existing river confluences
    ↓
relative flow accumulation
    ↓
local width/depth profile
    ↓
WorldRiverData
    ↓
terrain carving using local width/depth
    ↓
variable-width placement exclusion corridor
```

### Current river data

A river now stores:

- stable ID;
- centerline;
- nominal fallback width/depth;
- per-point relative flow;
- per-point local width;
- per-point local depth;
- optional downstream river ID;
- downstream join point;
- confluence position.

`nominalWidth` and `nominalDepth` remain fallback/reference values for compatibility.

### Confluences

Rivers are generated in deterministic source-grid order.

A later traced river may attach to an already generated river when it enters the configured `mergeDistance`.

The tributary records which river it joins. Flow contribution is then propagated downstream in reverse generation order, so nested tributaries contribute to larger rivers.

This is not yet a complete physical watershed simulation. It is a lightweight deterministic river-network model.

### Local channel size

Headwaters are narrower/shallower than downstream sections.

Width and depth are based on:

- normalized progress from source to end;
- accumulated relative flow;
- configured profile multipliers/exponents.

Consumers should use:

- `GetWidthAtPoint`
- `GetDepthAtPoint`
- `GetWidthAtSegment`
- `GetDepthAtSegment`

instead of reading `nominalWidth` for local geometry decisions.

### Known river limitations

Still not implemented:

- full watershed/catchment simulation;
- depression/lake/basin solving;
- realistic discharge units;
- sediment/erosion simulation;
- braided rivers/deltas;
- water-surface rendering;
- bank material/vegetation pass.

The current data contract is intentionally suitable for later replacement by a more advanced solver.

## Roads

Files:

- `RoadNetworkPlanner.cs`
- `WorldRoadConnectionData.cs`
- `TerrainRoadPathPlanner.cs`
- `WorldRoadData.cs`

Pipeline:

```text
settlements
    ↓
logical connectivity graph
    ↓
terrain-aware A*
    ↓
slope/highland cost
    ↓
river crossing cost using local river width
    ↓
WorldRoadData centerline
    ↓
chunk exclusion mask
    ↓
future spline/mesh renderer
```

The centerline is authoritative path data; the road mesh is presentation.

Road search already treats river corridors as an additional traversal cost. Wider downstream river sections therefore produce a wider crossing corridor than headwaters.

## Bridges

Files:

- `BridgeSitePlanner.cs`
- `WorldBridgeSiteData.cs`

Bridge sites derive from geometric road/river intersections.

A bridge is not randomly scattered.

Production bridge crossings default to one fixed authored bridge: the river width profile is normalized and blended around each road/river intersection, while variable river width is preserved away from the crossing. The legacy local-width span mode remains available when standardization is disabled.

Future bridge validation can add:

- bank slope;
- crossing angle scoring;
- road grade;
- bridge archetype selection by span;
- terrain deformation;
- bridge approaches;
- ford selection for very small streams.

## Important architecture rule

River centerlines, local width/depth profiles, road centerlines and bridge sites are authoritative generated data.

Meshes, water shaders, bridge prefabs and road materials are presentation.

Do not move visual references into the macro generation data.
