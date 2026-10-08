# Bridge-aware routing — opt-in recovery and diagnostics (2026-10-08)

Executor: GPT-6 in ChatGPT, under Issue [#8](https://github.com/DimaFi/Little-Castle/issues/8). This is a draft implementation; Unity compile, EditMode and PlayMode results remain **PENDING**.

## Why

The initial terrain-aware A* constructs coarse roads from logical feature connections. The fixed bridge planner subsequently removes a road and its logical connection if **any** crossing fails width, perpendicular approach, site geometry, terrain grade, or footprint checks. No alternative is sought. An unbridged road is correctly forbidden, but a disconnected settlement graph is possible.

## New opt-in contract

`BridgePlannerSettings`:
- `enableBridgeAwareRouting` (default **false**, legacy path unchanged)
- `maxBridgeRoutingAttempts` (1..8, default 4)
- `maxGuidedCrossingAttempts` (0..8, default 4; 0 disables socket-guided fallback)
- `minimumFixedBridgeCount` (default 0)
- `requireConnectedFeatureGraph` (default false)
- `failOnRoutingError` (default false)

These are plain serialized settings; **do not enable Main*** assets automatically. An opt-in developer concept profile may activate the flags after validation and decide whether a failed bootstrap should throw. The generator never uses unbounded retries.

`MacroWorldPlanner.GenerateForBounds` retains the original logical connection list before fixed-site rejection. After standard bridge validation, `BridgeAwareRoutingPlanner.RecoverRejectedConnections` visits missing roads in stable ID order. Each alternative:

1. Runs the already deterministic terrain-aware A* with a bounded alternative grid step, search envelope and river-crossing penalty; does **not** change source planner settings.
2. Constructs a temporary macro plan containing the proposed road, original rivers and all previously accepted fixed bridge sites. It asks the existing `BridgeSitePlanner` to validate that proposal using the actual polyline, authored fixed bridge geometry, angles, terrain fit and footprint-overlap checks.
3. Rejects any proposal with an unserved river intersection. Only a coherent road and its newly valid fixed-site records are committed to the actual plan.
4. If ordinary attempts are exhausted, scores deterministic anchor candidates along existing river segments. For each, A* solves two approaches independently while the authored fixed bridge owns a straight central road segment with entry/exit exactly ±5.4 m from the crossing. Every proposed composite polyline must still pass fixed-site and full-crossing validation; no geometry is stretched.
5. If both ordinary and guided attempts are exhausted, records a failure. Never fabricates resources, bridges, or arbitrary per-chunk geometry.

The source mesh remains exactly 10.8 m long, 2.86 m clear width, local road +Z/river +X, at scale 1. Bridges are not scaled to span a wider river. Original fixed-site implementation and `FixedBridgeSiteProfile` were not modified.

## Post-plan validator

`WorldRouteConnectivityValidator.Validate(plan, minimumFixedBridges, requireConnectedFeatures)` reads only compact macro data. It returns:

- Count of realized vs logical road connections (detects missing visual road).
- Count of fixed bridge sites, unsupported road/river intersections and orphan/misaligned bridge sites.
- Feature graph connected-component count.
- A bounded set of actionable error messages and a plain summary.

The graph is built from **realized** roads only, not aspirational logical connections. Opt-in macro plans expose `RouteDiagnostics`, `BridgeAwareRoutingAttempted` and `BridgeAwareRoutingSatisfied`; legacy plans retain a null report.

When `failOnRoutingError` is true, the macro planner emits an explicit `InvalidOperationException` with retry counts and errors; otherwise it emits a diagnostic warning and preserves the partial plan. This is a **bounded failure**, not an assertion that every random seed can geometrically support a bridge.

## Known limits / correctness gates

- This draft retries terrain-aware routes and includes bounded socket-guided crossing anchors, but does not build a globally optimal bridge-specific road graph. Difficult meanders, steep approaches or inaccessible anchor points can still fail. The existing fixed-site planner remains the source of truth for valid road/site geometry; a strict profile should reject infeasible seeds rather than generating phantom bridges.
- The graph validator checks centerline intersections and whether actual bridge sites coincide, rather than testing every physical river-bank polygon or pathfinding/navigation mesh. Full bank/sockets/colliders acceptance belongs in issue #10/Unity.
- The validator's connected graph covers generated point features, including any halo-region features the macro planner generated. Finite playable-bounds path-cost checks and multiplayer 2/8/16 start accessibility still require their dedicated real macro integration tests.
- It does not enforce that the eventual Unity/presentation scene contains the authored FBX. That is Issue #9.
- The current opt-in route retry runs A* synchronously on macro data, so **profile and bound its cost** before enabling it for a 3,072 m map. No detailed terrain meshes, world prefabs or full-map GameObjects are spawned by the route validator.
- No URP migration, watermill, resource injection or shared art/prefab files were touched.

## Tests added

`Assets/_Game/Tests/Editor/BridgeAwareRoutingTests.cs` covers:
- Valid fixed crossings at negative world coordinates.
- Unsupported crossing, minimum bridge requirement, incorrect/orphan site.
- Missing realized connection and disconnected feature graph.
- Repeatable bounded recovery for a missing road, deterministic straight/perpendicular guided anchor selection, and a failure for unknown feature IDs.
- Synthetic connected graphs with 2/8/16 point features and different seeds. These are not multiplayer performance results.

Actual Unity C# compile/EditMode/PlayMode: **NOT RUN** in this chat. Source-level contract inspection only. Never label these new tests PASS before Unity runs.

## Codex handoff / PC acceptance

Branch: `gpt6/bridge-aware-routing-draft`, based on `current-unity-fix` at `c61a6de`. Review this branch and its draft PR; **do not merge it before issue #7 baseline gate** and real Unity tests.

1. Unity 6000.5.5f1 compile, full EditMode and PlayMode; compare with the preserved original historical tests and draft #7 PR.
2. Create a separate opt-in test profile; exercise same/different seeds, negative chunk seams, all map sizes and 2/8/16 players. Run `WorldRouteConnectivityValidator` on each. Explicitly count failed seeds/attempts rather than claiming universal bridge success.
3. Inspect actual river widths, slopes, bends, socket-guided composite polylines, potential double intersections at a centerline vertex and approach tolerances. If fallback is insufficient, extend the route solver's graph or candidate ranking rather than weakening fixed-site geometry.
4. Measure macro plan timings/memory vs legacy path before raising retry budgets, keeping no whole-world GameObjects.
5. Integrate only after review, publish new dated NUnit and performance reports, keep original results unchanged.

Current status: **work submitted for review, all Unity acceptance pending**.
