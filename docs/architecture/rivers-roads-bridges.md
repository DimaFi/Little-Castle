# Rivers, Roads and Bridges

## Current implementation status

The repository already contains early executable algorithms for all three, but none should be considered final production quality until tested visually in Unity.

## Rivers

Files:

- `RiverPlannerSettings.cs`
- `RiverNetworkPlanner.cs`
- `WorldRiverData.cs`
- `RiverTerrainCarvingStage.cs`

Pipeline:

```text
terrain probe
    ↓
source candidate
    ↓
downhill trace
    ↓
WorldRiverData
    ↓
RiverTerrainCarvingStage
    ↓
placement exclusion corridor
```

Later add:

- flow accumulation;
- merging;
- lake handling;
- water surface data;
- bank materials;
- river vegetation.

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
logical nearest-neighbor graph
    ↓
terrain-aware A*
    ↓
WorldRoadData centerline
    ↓
chunk exclusion mask
    ↓
future spline/mesh renderer
```

The centerline is authoritative path data; the road mesh is presentation.

## Bridges

Files:

- `BridgeSitePlanner.cs`
- `WorldBridgeSiteData.cs`

Bridge sites derive from road/river intersections.

A bridge is not randomly scattered.

## Important future dependency

Road pathfinding currently does not yet treat rivers as a special traversal cost.

Therefore an early road may cross a river at a technically valid geometric point that is not yet the optimal bridge location.

The next road-quality pass should add river-crossing costs before investing heavily in road art.
