# Fixed Stone Bridge Crossings

## Status and scope

Fixed stone bridge sites are an opt-in deterministic extension of the existing
macro road/river crossing pipeline. The legacy variable-span bridge planner is
unchanged while `BridgePlannerSettings.useFixedStoneBridgeSites` is false.

The fixed contract is intentionally one complete site, not a mesh stretched to
fit arbitrary rivers. A site owns:

- one stable bridge spawn;
- one authored terrain height profile;
- the local riverbed, banks, and road approaches inside its stamp;
- one rotated support/exclusion rectangle across every touched chunk.

Generation still produces plain data. It does not instantiate a bridge or
materialize distant chunks during macro planning.

## Authoritative v002 contract

The source-art authority is:

`../Little-Castle_Assets/Source/Architecture/Bridge_Stone_A/v002/bridge_contract.py`

`FixedBridgeSiteProfile` mirrors its constants and functions in C#:

| Contract item | Value |
|---|---:|
| Archetype | `ENV_Bridge_Stone_A` |
| Version | `v002` |
| Local road axis | `+Z` |
| Local river axis | `+X` |
| Root | crossing center, `Y = approach grade` |
| Bridge length | 10.8 m |
| Structural width | 3.6 m |
| Cap extent | 3.78 m |
| Clear path | 2.86 m |
| Deck crown | 1.05 m |
| Water offset | -0.85 m from root |
| Support half extents | X=7 m, Z=8 m |
| Protected core half extents | X=5 m, Z=6.5 m |
| South/right flag | X=+1.64 m, Z=-5.12 m |
| North/right flag | X=-1.64 m, Z=+5.12 m |

`terrain_height`, `stamp_weight`, and `deck_height` use the exact v002
functions. World sampling uses translation plus yaw only. Non-uniform scale is
not part of the contract.

## Planning

`BridgeSitePlanner.BuildBridgeSites` keeps its original three-argument API and
adds a terrain-aware overload. `MacroWorldPlanner` passes its existing
`WorldTerrainProbe` into the overload.

Fixed planning:

1. finds actual road/river segment intersections;
2. rejects widths outside the configured range, with 6.5 m as a hard maximum;
3. rejects crossings too far from perpendicular;
4. confirms the real road reaches both authored sockets and the real river
   centerline remains inside the local v002 channel through the support area;
5. samples both outer road approaches to derive `baseElevation` and grade;
6. rejects steep approach endpoints and excessive approach grade;
7. samples both river sides and rejects waterfall-like longitudinal grade;
8. prefers the most perpendicular and lowest-grade compatible road groups;
9. rejects overlapping rotated support footprints;
10. records constant length 10.8 m, root elevation, `v002`, and fixed-site flag.

One realized road is atomic in fixed mode. If any river crossing on that road
is invalid or cannot receive a non-overlapping fixed site, the road, matching
logical connection, and its bridge sites are removed from `MacroWorldPlan`.
This prevents a generated road from pretending to be traversable through an
unserved water crossing. Roads without river crossings are unaffected.

If the fixed planner is called without a terrain probe, crossing roads are
removed because their grade cannot be validated safely.

## Chunk terrain and presentation data

Pipeline order for an enabled fixed profile is:

```text
base terrain
    -> river carving
    -> FixedBridgeTerrainStage
    -> terrain classification / slope analysis
    -> MacroFeatureProjectionStage
    -> local scatter
```

`FixedBridgeTerrainStage` evaluates every height sample from absolute world
coordinates. It blends the authored target once over the v002 weight field.
The protected core fully replaces the earlier generic river carve, so all
chunks see the same local riverbed, banks, and approaches. Weight is exactly
zero on the X=+/-7 and Z=+/-8 support border, including negative chunk seams.

`MacroFeatureProjectionStage` reserves the whole rotated support rectangle
with `PlacementBlockFlags.All`. Exactly one owning chunk receives the spawn.
The fixed spawn uses:

- `worldPosition.y = WorldBridgeSiteData.baseElevation`;
- authored yaw;
- uniform scale `1`.

It never resamples the carved riverbed for the root. Legacy sites still sample
chunk terrain and apply the existing `bridgeHeightOffset`.

The `water_y=-0.85` value is available to future water presentation, but this
change does not create a second water surface or add a water renderer. The
current external river-water presentation must later consume the fixed-site
contract so only one coherent water surface is shown.

## Plain data and compatibility

`WorldBridgeSiteData` adds:

- `baseElevation`;
- `contractVersion`;
- `isFixedSite`.

The original constructor remains. It initializes these fields to `0`, empty,
and `false`, preserving legacy serialized/runtime behavior.

## Configuration

Recommended initial concept profile:

```text
useFixedStoneBridgeSites = true
minimumCompatibleRiverWidth = 1.5
maximumCompatibleRiverWidth = 6.5
maximumCrossingAngleDeviation = 20 degrees
maximumApproachGrade = 0.20
maximumApproachSlope = 28 degrees
maximumRiverGrade = 0.20
fixedSiteSeparationPadding = 1 metre
```

These are authoring defaults, not a promise that every generated route will
survive validation.

## Remaining route guarantee gap

Fixed mode now removes an entire realized road when even one of its crossings
cannot be served. This is coherent but may reduce macro connectivity.

It does **not** yet re-run A* with bridge-socket constraints, bend road and
river centerlines onto the exact sockets, or retry the macro plan until a map
contains a required number of bridges. Therefore:

- no phantom perpendicular river is stamped for an oblique rejected channel;
- no unbridged road is retained as usable data;
- a finite map is not yet guaranteed to contain a bridge;
- global route connectivity is not yet guaranteed after rejection.

A production guarantee needs deterministic bridge-aware road re-planning (or
whole-plan retry), plus an explicit minimum bridge/connectivity validation at
session bootstrap. It must operate on compact macro data and must not
materialize the full map.
