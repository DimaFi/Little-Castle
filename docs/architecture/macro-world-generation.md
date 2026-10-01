# Macro World Generation

## Why macro generation exists

Some features must know about a large surrounding area:

- neutral settlements;
- ruins;
- rivers;
- roads;
- bridges;
- major landmarks;
- future trade routes and territories.

Generating these independently per chunk would create broken continuity.

## Current macro pipeline

```text
Requested world bounds
        ↓
Planning halo
        ↓
Point candidates
        ↓
Terrain suitability probe
        ↓
Feature separation
        ↓
Neutral settlements / ruins / landmarks
        ↓
River source selection + downhill tracing
        ↓
Road connectivity graph
        ↓
Terrain-aware road A*
        ↓
Road × River intersection
        ↓
Bridge sites
        ↓
MacroWorldPlan
```

## Planning halo

A macro plan deliberately looks beyond the exact requested area.

This reduces edge-dependent decisions such as a settlement choosing different nearest neighbors merely because the visible area changed.

The current system is still an early bounded/on-demand planner.

For very large production worlds, evolve this toward fixed macro regions/tiles with cached plans and stable ownership rules.

## Terrain probe

`WorldTerrainProbe` generates only through `TerrainAnalysis`.

It can answer:

- height;
- slope;
- terrain class;

for an arbitrary world position without generating trees, resources or ordinary props.

Macro features use this to reject unsuitable positions.

## Neutral settlements

Neutral settlements are macro point features.

Generated/base data currently defines:

- stable identity;
- archetype hook;
- world position;
- influence radius.

Gameplay runtime state must remain separate.

Future settlement runtime state may include:

- player relationships;
- trade/economic value;
- influence;
- ownership/competition;
- upgrades;
- destruction/recovery;
- quests/events.

Do not put those mutable fields into deterministic generation rules.

## Rivers

The current `RiverNetworkPlanner` is an early deterministic downhill tracer.

It:

- selects stable source candidates;
- prefers elevated terrain;
- steps downhill through sampled terrain;
- has turn cost to reduce extreme zig-zagging;
- creates `WorldRiverData`.

Known limitations:

- it is not a full watershed simulation;
- streams do not merge yet;
- lakes/basins are not solved;
- river width is currently nominal rather than varying along the line.

These limitations are algorithmic, not data-contract limitations.

`RiverTerrainCarvingStage` can carve an early physical channel in the generated terrain.

## Road graph vs road geometry

Road generation is deliberately split.

### Graph

`RoadNetworkPlanner` decides which settlements should connect.

Output:

`WorldRoadConnectionData`

### Geometry

`TerrainRoadPathPlanner` turns each connection into a coarse path.

Output:

`WorldRoadData`

The current path solver uses deterministic coarse-grid A* and penalizes:

- slope;
- highlands;
- excessive steepness.

It is isolated so it can later be replaced with:

- hierarchical pathfinding;
- nav/terrain cost fields;
- spline optimization;
- road reuse;
- river crossing cost;
- bridge-aware planning.

## Bridges

`BridgeSitePlanner` finds geometric intersections of generated roads and rivers.

Bridge data stores:

- stable ID;
- road ID;
- river ID;
- archetype ID;
- crossing position;
- yaw;
- required span.

Later validation can additionally check:

- bank slope;
- river width profile;
- bridge type;
- terrain deformation;
- road grade.

## Projection into chunks

`MacroFeatureProjectionStage` takes a macro plan and writes local effects into each generated chunk.

Currently it can:

- reserve settlement/ruin areas;
- reserve road corridors;
- reserve river corridors;
- create macro object spawn anchors;
- create bridge spawn anchors.

The chunk never invents its own road or village.
