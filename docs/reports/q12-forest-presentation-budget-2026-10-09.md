# Q12 — Forest presentation budget: scoped source handoff

Date: 2026-10-09. Source contributor: GPT-6 ChatGPT.
Issue [#41](https://github.com/DimaFi/Little-Castle/issues/41).
Branch `gpt6/q12-forest-presentation-budget-2026-10-09`.
Base `codex/concept-world-pass-2026-10-09` at
`e6a02df6f57461df9cd7cf853e8e9d62b776d71a`.

**Source-only pre-integration. NO Unity compile, EditMode/PlayMode, GPU test,
FPS, Player measurement or prefab/screenshot acceptance occurred here.**

## Why Q12 and why not Q08

The user confirmed parallel work on rocks/scarps. Q08 owns scarp
decoration stages and concept rock assets; this PR intentionally
**does not touch Q08, Q18, any cliff/rock FBX, materials or landforms**.
A different source-only task avoids racing a local coding/art agent
whose unpushed changes cannot be observed through GitHub.

A desktop concept report has **already executed the actual Unity Oak
technical preflight**: Oak has separated Leaves/Trunk, normals and
UV0, zero preflight errors/warnings; imported total LOD geometry is
**12,068 / 6,858 / 3,106 triangles**. This is a *historical desktop
measurement on an earlier revision*, not a new GPU render pass, LOD
silhouette approval, or current-frame performance result.

Existing `ChunkSpawnPresenter.Populate` instantiates a GameObject for
each generated visual and layers `WorldRuntimeDeltaState` removals.
`ForestDensityStage` controls generated forest density, not true
GPU instance cost. Editing forest density to solve draw calls would
change authoritative gameplay tree/resource distribution and may
break PvP fairness / saves. Q12 therefore makes **only read-only
presentation decisions** and leaves actual scene/prefab adoption to
Codex/root.

## Changed files — exactly seven new Q12 files

1. `Assets/_Game/Scripts/World/Rendering/ForestPresentationBudget.cs`
   and Unity GUID `.meta`.
2. `Assets/_Game/Scripts/World/Rendering/ForestPresentationAdapter.cs`
   and Unity GUID `.meta`.
3. `Assets/_Game/Tests/Editor/ForestBudgetTests.cs`
   and Unity GUID `.meta`.
4. This report.

No existing `ChunkSpawnPresenter`, `WorldStreamer`,
`WorldRuntimeDeltaState`, `ForestDensityStage`, prefab,
shader, `LC_Foliage`, `Main`, settings asset, scene or model changed.

## Policy

`ForestPresentationBudget.Evaluate(candidates, viewpointXZ,
visualSeed, settings, output)` accepts pure readonly candidates and
writes sorted stable-ID decisions. Inputs have:
- stable generated tree ID and exact absolute world X/Z;
- **requiresGameplayPresence**: an interactable/choppable/blocked
  gameplay tree MUST stay as authored prefab and retain gameplay
  collider authorization even when beyond distance budgets;
- **hasApprovedSilhouette**: one *actually reviewed compatible*
  near/far tree silhouette prefab/renderer exists;
- **hasConfirmedForestCluster**: a *currently visible and adequate*
  far forest cluster covers this decorative instance's visual mass.

Default is **safe/fail-open**: `AuthoredPrefab` with preserved gameplay
collider and no suppression. If a root eligibility callback is missing
**or returns default(struct) / unreviewed**, the adapter treats all
trees as protected. The caller must positively authorize decoration
at the correct archetype/variant/instance level. Never infer
decoration merely from `SpawnCategory.Tree`.

Policy decisions:
- **Near** (<= `nearDistanceMeters`): keep full authored prefab
  for all trees. This preserves close camera/readability and interactions.
  Near decorative optional visual colliders compete for an explicitly
  bounded nearest-first budget; gameplay colliders do not.
- **Intermediate**: sort all candidates by (squared distance, stableId);
  preserve authored prefab up to the full-prefab advisory quota,
  then allow only explicitly approved decorative silhouettes.
  If no silhouette exists, preserve authored prefab and count
  `fullPrefabBudgetOverflow` rather than silently hide trees.
- **Far** (> `distantDistanceMeters`): decorative trees may use
  reviewed silhouette. A decorative tree with verified forest-cluster
  coverage can be *represented by the cluster* with a stable
  per-ID/seed thinning decision using `DeterministicHash.Hash01`.
  Default `clusterCoveredFarRetention=1` disables this thinning
  entirely; a cluster that does not exist or cover the tree
  cannot authorize suppression.
- **Any gameplay tree**: regardless of distance/quotas, the algorithm
  proposes `AuthoredPrefab` and `retainGameplayCollider=true`;
  no distance budget may erase an authoritative resource tree.
  The root still decides authoritative physics and which objects
  the server actually simulates.
- **Missing/invalid settings**, nonfinite view/position, or
  duplicate stable IDs abort before changing output. Duplicate tree
  IDs across chunk seams are errors, not arbitrarily culled clones.

The budgets here are **provisional inputs, not safe device thresholds**.
A full prefab may contain a Unity LODGroup that already simplifies
mesh tiers. This utility does not strip LODs or alter the last-tier
shadow/motion-vector contract. It also does not imply a silhouette
replacement is cheaper unless an actual Frame Debugger sample proves it.

Metrics returned: candidate count, number of authored prefabs /
silhouettes / cluster-covered trees, protected gameplay trees,
optional decorative colliders and prefab-budget overflows.
**These are selection counts, not measured draw calls, GPU time,
triangle throughput or SetPass counts.**

## Read-only adapter and reuse

`ForestPresentationAdapter.EvaluateActiveChunks` reads an input
collection of already generated `WorldChunkData` chunks and their
`Spawns`. It includes only `SpawnCategory.Tree` and excludes
tree IDs removed by `WorldRuntimeDeltaState.IsSpawnRemoved`.
It **does not mutate** the world chunk's generated spawns or
restore a chopped tree on reload/eviction.

The adapter reuses internal List/HashSet buffers between evaluations
to avoid per-candidate managed allocations after capacity is warm.
Selection sorting is O(N log N) by nearest distance and then stable ID
and uses O(N) scratch memory for an existing *active-interest* set:
NO complete-map tree enumeration / object materialization.

Do not give it "whatever chunks have just arrived this frame"
while claiming a global renderer/collider budget: it must receive a
coherent view of **all eligible active/visible chunks in one interest
domain**. The runtime root must handle multi-focus/multiplayer views
without local camera suppressing authoritative objects or another
client's needed visuals. Stable-ID output order prevents results
from depending on chunk arrival order. A camera moving across band
thresholds can still cause visible switching; **hysteresis / transition
smoothing is NOT implemented** here and must be approved at root.

`ForestAssetPresentationEligibility` currently only conveys
permissions. Actual approved silhouette resource presence and
cluster coverage must come from the root-owned catalog/review.
If the root cannot prove the visual replacement, leave eligibility
unapproved and keep the original authoring.

## Required root integration (not implemented here)

1. Unity imports and validates the *real portable Oak*
   `Assets/_Game/Art/Imported/ConceptWorldKit_v001/Prefabs/oak.prefab`.
   Rerun `FoliageModelCompatibilityValidator` and
   `ProductionAssetValidator` on this revision. Verify Leaves/Trunk
   remain separated, normals/UV/tangents/wind/shadows on actual
   meshes and no new FBX dependency.
2. Capture LOD0/1/2 triangle counts, GPU Frame Debugger batches,
   set-pass, shadow passes, overdraw, fill-rate and CPU renderer
   setup. Test multiple oaks and village/river context with day,
   sunset, night and near/far RTS camera; preserve Tiny Glade-like
   visual density before enabling far thinning.
3. Root chooses and approves real silhouette/HLOD coverage assets,
   collision/nav owner and interaction eligibility. DO NOT use
   `forestDensity` data to reduce authoritative tree spawns.
4. The actual `ChunkSpawnPresenter` must be integrated by its
   **owner** after this API passes tests, retaining `stableId`,
   `GeneratedWorldObject`, `WorldRuntimeDeltaState`,
   prefetch/cache and chunk-revisit semantics. Explicitly test
   trees chopped, replanted, resources consumed and save/reload.
5. For approved silhouette/cluster switching, pool GameObjects
   and keep authoritative resource physics separate. Do not
   instantiate the full prefab then also a duplicate silhouette
   for one ID; avoid per-frame Destroy/Instantiate on zoom.
6. Verify instancing is truly eligible: identical mesh/material and
   shader variants, compatible MaterialPropertyBlocks, per-tree
   identity/tint/wind; don't assume URP or GPU instancing is
   automatically enabled. Capture actual batches in current
   renderer, on target hardware.
7. Establish numeric budgets **after** measured Player evidence,
   not from arbitrary `maxFullPrefabInstances` defaults.
   Record 2/8/16-player sessions, close/far map travel, 1% low
   frame time, CPU render/update, GPU time, draw calls,
   memory allocations and chunk-in/out spikes. Current
   `WorldSimulationLodPolicy` is a *server-side simulation*
   tier, NOT a substitute for client visual LOD.
8. When ready, root builds source-specific integration tests
   comparing actual forest visual density and gameplay tree
   availability across reload, negative chunk borders and
   streamed arrival orders.

## Source EditMode tests to run on desktop

New `LittleCastle.Tests.ForestBudgetTests` covers **13** tests:
- non-droppable gameplay/near trees and explicit budget overflow;
- stable results on shuffled per-chunk arrival and equal distances;
- nearest-first decorative optional collider selection and ID tie;
- no approved replacement => preserve distant prefab;
- clustering only with **both decorative eligibility and coverage**;
- far thinning stable by negative ID and seed;
- bad setting/input and duplicate ID do not overwrite previous output;
- adapter drops removed tree IDs and ignores non-tree spawns;
- callback null or unreviewed default => preserve gameplay;
- repeated evaluations with reusable scratch produce no stale choice;
- duplicate ID across negative/positive chunk seam is rejected.

In Unity 6000.5.5f1 from an isolated Q12 worktree:

```text
-batchmode -nographics -projectPath <root> -runTests -testPlatform EditMode -testFilter LittleCastle.Tests.ForestBudgetTests -testResults <Logs/Q12-ForestBudget.xml> -logFile <Logs/Q12-ForestBudget.log>
```

Then full EditMode + PlayMode and the approved oak source/visual audits.
Do not put `-quit` on Unity `-runTests`. Inspect XML and Unity logs,
not exit status alone.

## Verification and dependency disclosure

| Check | State |
|---|---|
| Scoped source/issue and earlier Oak preflight reviewed | DONE |
| Read-only budget, adapter, tests and GUIDs authored | DONE |
| GitHub compare strict seven-file scope | PENDING at report creation |
| Unity code compile / focused NUnit test result | **NOT RUN** |
| Full EditMode/PlayMode results for this revision | **NOT RUN** |
| Real tree silhouette/HLOD assets approved | **NOT DONE** |
| Actual ChunkSpawnPresenter/WorldStreamer live integration | **NOT DONE** |
| Player FPS/GPU/draw calls/forest appearance | **NOT MEASURED** |

Historical 175/175 EditMode and 5/5 PlayMode plus Oak technical
preflight belong to a previous desktop revision, **not a Q12 PASS**.
Keep Q12 issue OPEN and PR DRAFT until root Codex accepts the
actual source/Player performance contract.
