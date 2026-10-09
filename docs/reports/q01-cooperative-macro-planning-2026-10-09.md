# Q01 — Cooperative MacroWorldPlanner transaction and acceptance contract

Date: 2026-10-09. Source contributor: GPT-6 ChatGPT.
Tracking: [bootstrap issue #5](https://github.com/DimaFi/Little-Castle/issues/5),
Q01 in `docs/tasks/chat-world-graphics-queue-2026-10-09.md`.
Branch `gpt6/q01-cooperative-macro-planner-2026-10-09`;
base `codex/concept-world-pass-2026-10-09` at
`e6a02df6f57461df9cd7cf853e8e9d62b776d71a`.

**Status: source-only candidate pending Unity tests, performance and root integration.**

## Why Q01 is necessary

The current `MacroWorldPlanner.GenerateForBounds` was an all-at-once call.
For a large finite map, point-feature grid enumeration and expensive
river/road/fixed-bridge computations ran before session readiness on a
single calling thread. The previous desktop report also explicitly
observed macro startup synchronous while chunk-detail generation had
already become cooperative.

The project requires one deterministic strategic macro plan per finite
session, then lazy details in `WorldStreamer`. Do not load/build all
detailed chunk meshes or stream every art asset just to initialize the
macro world. This Q01 PR does **not** touch streamer, Player scene,
session network schema or the current save/delta contract.

## Exact source changes

Owned files:
1. MODIFIED `Assets/_Game/Scripts/World/Macro/MacroWorldPlanner.cs`
   — slim facade with existing `GenerateForBounds` API and new
   `BeginPlanning(seed, worldBounds, terrainProbe)`.
2. NEW `Assets/_Game/Scripts/World/Macro/MacroPlanningSession.cs`
   plus stable Unity `.meta`.
3. NEW `Assets/_Game/Tests/Editor/MacroPlanningSessionTests.cs`
   plus `.meta`.
4. This source-handoff report.

One **shared execution path** is used for legacy synchronous and
cooperative sessions. `GenerateForBounds` delegates to
`BeginPlanning(...).RunToCompletion()`, not a second independently
maintained copy of the macro algorithm.

`MacroPlanningSession.Step(maxWorkUnits, maxMilliseconds)` advances
on the invoking main thread. The point-feature grid is scanned
**one candidate cell per unit**, preserving:
- original rule insertion order, local nested grid order **z then x**;
- `Mathf.Max(50, rule.spacing)`, original halo and grid bounds;
- original seeded `Hash01` salt and jitter rounding;
- original terrain class/height/slope filtering;
- same separation test against *previously placed* point features;
- same stable ID and feature insertion order.

Then the same existing operations are performed in their old order:
1. river network (`RiverNetworkPlanner.BuildRivers`) if terrain probe;
2. logical road connections (`RoadNetworkPlanner.BuildConnections`);
3. strict bridge-aware connectivity backbone if opted in;
4. physical terrain-aware road paths if terrain probe;
5. desired-road-edge snapshot **before fixed bridge planner may remove**;
6. fixed bridge sites;
7. bounded recover rejected connections (opt-in);
8. last realized road connectivity repair (opt-in);
9. final world route diagnostics, failure reporting and
   `BridgeAwareRoutingSatisfied` flag (opt-in).

The previous legacy no-terrain or bridge-aware-off behaviors are
preserved by using exactly the same phase gating and order. All
`UnityEngine`/ScriptableObject/AnimationCurve access stays on the
caller thread; **never call Step from Task.Run**.

## Lifecycle, cancellation and budgets

- New session states:
  `PointFeatures`, `Rivers`, `RoadConnections`,
  `ConnectivityBackbone`, `RoadPaths`,
  `CaptureDesiredConnections`, `BridgeSites`,
  `RecoverRejectedConnections`, `RepairRoadConnectivity`,
  `ValidateRouting`, `Completed`, `Cancelled`, `Faulted`.
- `Result` is `null` until successful **Completed**, avoiding
  premature network distribution of a partly built graph or player
  start assignment. Only a completely validated plan is published.
- `Cancel()` marks irreversible `Cancelled`, clears partial
  macro plan and recovery references and stops the elapsed counter.
  Starting again uses a **new** session. The caller retains ownership
  of an optional `WorldTerrainProbe` and should `Clear()` or release
  its cache when abandoning bootstrap.
- Unhandled exceptions mark `Faulted`, release the partial plan,
  preserve `Failure`, and propagate to caller. Existing strict
  bridge-aware fail/throw behavior is preserved.
- `Step` limits work units and checks elapsed wall time with a
  zero-allocation timestamp between units. It always allows at
  least one unit when active. `ElapsedMilliseconds`,
  `PointCellsExamined` and `WorkUnitsCompleted` expose
  diagnostics, **not readiness percentages**.
- Budget error is refused before the session advances;
  `RunToCompletion` on cancelled/faulted sessions throws.

### Critical limitation — do not claim fully hitch-free bootstrap

**Existing river, road, bridge and route planners are still atomic
work units.** In particular:
- `RiverNetworkPlanner.BuildRivers` scans source grids, probes
  terrain, traces rivers and computes width/depth profiles;
- `RoadNetworkPlanner.BuildConnections` can perform O(N²)
  comparisons and sorted candidate allocations for many settlements;
- terrain-aware `TerrainRoadPathPlanner.BuildRoadPaths`;
- `BridgeSitePlanner.BuildBridgeSites`, rejected-link recovery,
  connectivity backbone and realized road repair.

One such stage may take **much longer than maxMilliseconds**,
because the budget is only checked **between stages**, never inside
an active monolithic planner call. `Cancel()` also cannot
interrupt a currently executing synchronous stage before control
returns to Unity main thread. Consequently this PR improves the
architecture and breaks down feature-grid loops, **but does not yet
meet a strict per-frame hitch budget for large maps**.

Before claiming no stalls on 2/8/16-player large maps, root/Codex must
measure each macro subphase and create separate owner-approved,
deterministic cursor/state-machine interfaces for expensive internals.
Do not fake progress percentages, use `Task.Run` around Unity assets,
or process all detailed chunks before MATCH READY. Q02 should
integrate session progress and safe cancellation only after confirming
the macro phase API, GPU screen/state readiness and startup owner.

**Additional scaling risk:** point-feature separation currently scans
already emitted point features, inherited from the baseline. This
can become a CPU hotspot on dense large grids; a future
deterministic spatial index should preserve insertion order and
exact pair distance semantics.

## Tests authored (not yet executed)

`MacroPlanningSessionTests` covers:
- same seed/settings/negative bounds: sync vs stepped work units
  **1, 7 and 127** (full feature and road connection order/IDs/positions);
- repeat across negative-coordinate seed and changed world seed;
- cancellation after one candidate, hidden partial result,
  nonresumable cancelled state and clean rerun;
- null settings => completed empty legacy plan;
- zero configured point rules still finalize phases in order;
- soft millisecond cap permits at least one unit without exceeding
  requested unit cap;
- illegal budgets never mutate a session;
- session phase monotonicity and no early result publication.

These focus on cheap **no-terrain-probe** configurations so per-PR tests
do not trace thousands of rivers or synthesize all map chunks.
The hard parity case with real terrain probe, generated rivers,
fixed bridge sites and strict repair **must be tested by desktop root**
against the prior macro output (record sorted stable IDs and
geometric centerlines on approved 1–2 representative seeds), not
assumed from comparing two new call paths.

### Required Unity commands / desktop ownership

Unity 6000.5.5f1 in clean Q01 PR worktree:

```text
-batchmode -nographics -projectPath <root> -runTests -testPlatform EditMode -testFilter LittleCastle.Tests.MacroPlanningSessionTests -testResults <Logs/Q01-Macro.xml> -logFile <Logs/Q01-Macro.log>
```

Also run:
- `WorldGenerationCooperativeTests` (different chunk generation contract);
- `WorldGenerationIntegrationTests`, `WorldRouteEndpointTests`,
  `RealizedRoadConnectivityRepairTests`,
  `RiverBendEnvelopeTests`, and full EditMode/PlayMode;
- real concept `MacroWorldPlanner` with terrain probe and fixed bridges,
  compare full IDs and geometry against existing accepted baseline;
- scope large matrix only as a separately budgeted root run:
  2/8/16 players, at least two finite map sizes, multiple seeds,
  negative origins and fallback rivers, with phase/stall profiler
  screenshots and stable routing diagnostics.

Do not include `-quit` on `-runTests` invocations.
Inspect actual NUnit XML, Unity import/compile logs and
a real graphical Editor/Player session.

### Root / Codex integration after successful tests

1. Preserve `WorldSessionMap` finite playable/visual bounds;
   planning input must be the complete **playable** map, not a
   transient camera focus.
2. Construct a new `MacroPlanningSession` once per host bootstrap;
   drive `Step` on Unity's main-thread per-frame scheduler.
3. Publish `Result` only on `Completed`, then validate/assign
   fair player starts and prewarm **only** required starting chunks.
   Distinct from actual scene visible-ready.
4. On cancel/error, remove partially allocated bootstrap state and
   release caller-owned `WorldTerrainProbe` caches; never send
   partial roads, bridges or stable IDs over multiplayer.
5. Record phase start/end CPU wall times, work units, memory/cache
   sizes and maximum frame stall; distinguish **hitch severity**
   from total bootstrap duration.
6. Keep `WorldStreamer`, prefab catalog, art and live scene untouched
   in Q01. Q02 owns the bootstrap controller adapter and UI states.
7. Codex root must approve whether a generation-version bump is
   required if independent older-baseline strict-route parity fails.
   Never silently migrate saved games.

## True verification status

| Gate | Status |
|---|---|
| Source and queue/ADR prior review | DONE |
| Single shared sync/step implementation and new test source | AUTHORED |
| GitHub owned diff/static review | PENDING at initial report |
| Unity compile and focused NUnit | **NOT RUN** |
| Full EditMode and PlayMode | **NOT RUN** |
| Real terrain-probe river/road/bridge parity | **NOT RUN** |
| Perf phase timing, frame spike and Player | **NOT RUN** |
| Root bootstrap Q02/WorldStreamer integration | **NOT ATTEMPTED** |

The prior source baseline reported 175/175 EditMode and 5/5 PlayMode
on a different commit. It does **not** certify this PR.

Keep Q01 draft until actual Codex desktop acceptance and document all
remaining atomic stage spikes. Do not prematurely close issue #5,
which covers broader MATCH READY + streaming + fairness acceptance.
