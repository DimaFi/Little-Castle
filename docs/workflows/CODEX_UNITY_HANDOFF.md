# Codex Unity Handoff — Little Castle

## Purpose

This document is the handoff from architecture/procedural-world development to the first real Unity integration and verification pass.

The repository already contains a substantial deterministic procedural-world foundation. The next Codex task is **not** to redesign it. The task is to make the existing code compile and run inside Unity, verify the architecture in practice, fix integration/compiler/runtime issues, and create a minimal test scene/configuration that demonstrates the current systems.

Repository root:

```text
DimaFi/Little-Castle
```

The repository root is intended to be the **Unity project root**.

Do not create a nested second Unity project such as:

```text
Little-Castle/UnityProject/
```

unless the project layout is explicitly migrated and documented.

---

# 1. Read before changing code

Read these files first, in this order:

1. `AGENTS.md`
2. `README.md`
3. `docs/PROJECT_STRUCTURE.md`
4. `docs/workflows/UNITY_SETUP.md`
5. `docs/workflows/CODEX_WORKFLOW.md`
6. `docs/workflows/WORLD_GENERATION_PRESET.md`
7. `docs/architecture/world-generation.md`
8. `docs/architecture/world-object-generation.md`
9. `docs/architecture/macro-world-generation.md`
10. `docs/architecture/rivers-roads-bridges.md`
11. `docs/architecture/world-streaming.md`
12. `docs/architecture/finite-session-maps.md`
13. `docs/architecture/fog-of-war.md`
14. `docs/architecture/world-generation-roadmap.md`
15. `docs/decisions/ADR-0002-fast-session-bootstrap-lazy-world-materialization.md`
16. other relevant ADR files under `docs/decisions/`

Do not begin by rewriting classes before reading the architecture.

---

# 2. Core architectural rules

These rules are intentional and should be preserved.

## Determinism

The same:

```text
world seed
+ generation version
+ configuration
```

must generate the same base world.

Do not replace deterministic generation with `UnityEngine.Random`.

## Generated data vs presentation

Authoritative world generation produces data.

```text
World Data
    ↓
Presentation
    ↓
Unity GameObjects / Meshes / Prefabs
```

Do not move prefab references into authoritative generated records.

## Chunk borders

Terrain uses absolute world coordinates.

Adjacent chunks must agree on border samples.

Do not introduce chunk-local noise that creates seams.

## Macro features

These are world-scale features:

- rivers;
- roads;
- bridges;
- neutral settlements;
- ruins;
- landmarks.

A chunk must not invent its own river, road, bridge or village.

## Runtime history

Base generated world and runtime player changes are separate:

```text
GeneratedBaseWorld
+
WorldRuntimeDeltaState
=
CurrentWorld
```

A chopped generated tree must stay removed after unload/regeneration because its stable ID is stored in runtime delta state.

---

# 3. Important existing systems

## Root configuration

`WorldDefinition` is the root ScriptableObject.

It references:

```text
WorldDefinition
├─ WorldGenerationSettings
├─ MacroWorldPlannerSettings
├─ WorldStreamingSettings
└─ WorldSpawnCatalog
```

Scene components should normally reference `WorldDefinition`, not independently duplicate all settings.

---

# 4. Current terrain generation

The procedural terrain pipeline already supports:

- deterministic seed generation;
- chunk coordinates;
- layered regional terrain;
- rolling hills;
- highlands/ridges;
- fine detail;
- river terrain carving;
- slope calculation;
- terrain classification;
- climate;
- biome classification;
- terrain surface classification;
- forest density;
- ground cover fields;
- resource generation;
- deterministic object scatter.

Important classes include:

```text
ChunkCoordinate
WorldChunkData
WorldGenerationSettings
WorldGenerationPipeline
WorldGenerationStage
WorldGenerationStagePhase
LayeredTerrainStage
TerrainClassificationStage
ClimateStage
BiomeClassificationStage
TerrainSurfaceStage
ForestDensityStage
GroundCoverStage
ResourceDepositStage
ObjectScatterStage
MacroFeatureProjectionStage
RiverTerrainCarvingStage
```

Do not create duplicate versions of these systems.

---

# 5. Generated objects

Generated objects use:

```text
Generation Rule
    ↓
WorldSpawnData
    ↓
archetypeId
    ↓
WorldSpawnCatalog
    ↓
Unity prefab variant
```

`WorldSpawnData` stores stable generated identity and transform data.

It must not become prefab-dependent.

Generated objects have stable IDs so runtime delta state can remember changes.

Example:

```text
tree stableId 123
↓
player removes tree
↓
WorldRuntimeDeltaState marks 123 removed
↓
chunk unloads
↓
chunk regenerates later
↓
tree 123 is generated in base data
↓
presenter sees runtime removal
↓
tree is not instantiated
```

---

# 6. Forests and resources

Forests are represented as a density/strategic field before concrete tree objects.

Do not treat visible tree GameObjects as the authoritative definition of a forest.

Resources use generated data such as:

```text
WorldResourceDepositData
```

with stable identity/capacity/richness.

Visual rock/ore objects are presentation.

---

# 7. Rivers

The river system has already been upgraded beyond a fixed-width polyline.

Current river generation supports:

- deterministic river sources;
- downhill tracing;
- tributaries joining already-generated rivers;
- confluence metadata;
- downstream relative-flow accumulation;
- per-point width profile;
- per-point depth profile;
- local-width terrain carving;
- variable-width placement exclusion;
- local-width road crossing cost;
- bridge span based on river width at the crossing.

Important files:

```text
RiverPlannerSettings.cs
RiverNetworkPlanner.cs
WorldRiverData.cs
RiverTerrainCarvingStage.cs
```

Important `WorldRiverData` concepts:

```text
centerline
flow[]
widths[]
depths[]

downstreamRiverId
downstreamJoinPointIndex
confluencePosition
```

When a local width is required, prefer:

```csharp
river.GetWidthAtPoint(...)
river.GetWidthAtSegment(...)
```

instead of blindly reading `nominalWidth`.

`nominalWidth` and `nominalDepth` remain fallback/reference values.

The current river system is not yet a full physical watershed simulation. Do not replace it during the first Unity integration pass.

---

# 8. Roads

Road generation is split between logical graph and path geometry.

```text
Neutral settlements / POIs
        ↓
RoadNetworkPlanner
        ↓
WorldRoadConnectionData
        ↓
TerrainRoadPathPlanner
        ↓
WorldRoadData
```

The current path planner uses deterministic terrain-aware A*.

It considers:

- slope;
- highlands;
- maximum terrain steepness;
- river corridors;
- local river width.

Road mesh/spline rendering is not yet final.

Do not merge road path data with visual road meshes.

---

# 9. Bridges

Bridge sites come from road/river intersections.

Important classes:

```text
BridgeSitePlanner
WorldBridgeSiteData
```

Bridge span now uses local river width at the actual crossing.

Bridges are not random scatter objects.

Future bridge quality work can add:

- bank slope validation;
- crossing-angle scoring;
- approach grading;
- archetype selection by span.

Do not attempt all of that during the first compile/integration pass.

---

# 10. Macro world

Current macro features include:

- neutral settlement anchors;
- ruins;
- landmarks;
- rivers;
- roads;
- bridge sites.

Important classes:

```text
MacroWorldPlan
MacroWorldPlanner
MacroWorldPlannerSettings
MacroFeatureProjectionStage
```

## Finite session-map rule

Little Castle does **not** use an infinite world.

The host chooses a finite map-size preset and seed before the match.

Current finite-map foundation:

- `WorldMapRules`
- `WorldMapSizePreset`
- `WorldSessionMap`
- `WorldChunkBounds`

One MacroWorldPlan is generated for the complete playable map when the session
starts.

Do **not** rebuild a moving MacroWorldPlan every time the camera/player changes
chunks.

Do **not** implement deterministic macro tiles unless product design changes
explicitly.

---

# 11. Runtime chunk streaming

A first runtime streaming layer already exists.

Files:

```text
Assets/_Game/Scripts/World/Streaming/
├─ WorldStreamer.cs
├─ WorldStreamingSettings.cs
└─ StreamedChunkView.cs
```

The runtime pipeline is:

```text
Focus Transform
      ↓
Focus Chunk
      ↓
Desired Chunk Set
      ↓
Nearest-first Load Queue
      ↓
Per-frame Load Budget
      ↓
WorldChunkCache
      ↓
WorldChunkData
      ↓
Mesh + Spawn Presentation
      ↓
Active StreamedChunkView
```

## Loading behavior

`WorldStreamingSettings` controls:

- load radius;
- unload padding/hysteresis;
- circular/square chunk selection;
- max chunk loads per frame;
- max chunk unloads per frame;
- max generated chunk cache entries;
- fixed session macro-plan radius;
- macro-edge warning distance;
- spawn presentation;
- optional MeshCollider creation.

## Load order

Missing chunks are loaded nearest-first relative to the focus.

## Hysteresis

Unload radius is larger than load radius.

This prevents edge chunks from repeatedly loading/unloading when the player moves around a chunk boundary.

---

# 12. Generated chunk cache

`WorldChunkCache` is now a bounded LRU cache.

Important behavior:

- generated chunk data is cached;
- recently used chunks are retained;
- active streamed chunks are pinned;
- pinned chunks cannot be evicted;
- unloaded chunks are unpinned;
- old unpinned chunks can be evicted;
- deterministic regeneration makes eviction safe.

Use:

```csharp
GetOrGeneratePinned(...)
```

when loading an active streamed chunk.

Do not manually duplicate another chunk cache inside `WorldStreamer`.

---

# 13. Streamed presentation lifecycle

`StreamedChunkView` owns transient runtime mesh resources.

On unload:

1. chunk is removed from active dictionary;
2. chunk data is unpinned;
3. runtime Mesh is explicitly released;
4. chunk GameObject is destroyed.

Generated child prefab instances are destroyed together with the chunk root.

## Critical transform rule

Streamed chunks must not be children of a Transform that follows the player.

`WorldStreamer` supports an optional stationary:

```text
chunkPresentationRoot
```

If it is null, chunks can live at the scene root.

Do not parent the whole generated world under the moving player/camera.

---

# 14. Finite session map and visual border

Production matches should use `WorldMapRules` and a host-selected
`WorldMapSizePreset`.

The streamer then uses:

```text
Playable Bounds
      ↓
one MacroWorldPlan for the entire match
      ↓
Visual Bounds = Playable Bounds + terrain-only padding
```

Playable chunks may contain gameplay objects/resources/colliders.

Visual-only border chunks generate terrain only and must not become traversable
gameplay space.

Beyond visual bounds, no chunks should be streamed.

The legacy `macroPlanRadiusChunks` path may remain temporarily for old test
configurations, but it is not the intended match architecture.

---

# 15. What Codex should do now

The task is to perform the **first real Unity integration and verification pass**.

Do these steps in order.

## Step A — inspect environment

1. Inspect the repository.
2. Check whether a Unity installation is available.
3. Determine the installed Unity editor version(s).
4. Prefer an installed stable/LTS Unity version.
5. Do not silently upgrade an existing Unity project version if `ProjectVersion.txt` appears.
6. Do not create a nested Unity project.

If no suitable Unity editor is available, still perform static C# review and prepare the repository, but clearly report that actual compilation could not be run.

---

# 16. Create/complete Unity project metadata

The repository currently may not yet contain real Unity-generated:

```text
Packages/
ProjectSettings/
*.meta
```

If needed, initialize the repository root as a Unity project.

Preserve:

```text
Assets/_Game/
docs/
AGENTS.md
README.md
```

Commit Unity-generated meta files once created.

Do not delete/recreate GUIDs unnecessarily after they exist.

---

# 17. First compile pass

Compile the current project **before adding new gameplay systems**.

Fix:

- C# compiler errors;
- Unity API compatibility errors;
- assembly-definition problems;
- serialization errors;
- missing namespaces;
- invalid ScriptableObject references;
- editor/runtime assembly separation issues.

Do not refactor working architecture just to satisfy personal style preferences.

For every meaningful fix:

- understand why it fails;
- make the smallest coherent fix;
- preserve deterministic behavior;
- update documentation if the contract changes.

---

# 18. Create a minimal test configuration

Create project-owned assets under a clear structure, for example:

```text
Assets/_Game/Settings/World/
```

Create at minimum:

```text
MainWorldDefinition.asset
MainWorldGenerationSettings.asset
MainMacroWorldPlannerSettings.asset
MainWorldStreamingSettings.asset
MainWorldSpawnCatalog.asset
```

Create generation-stage ScriptableObjects required by the current documented preset.

Use `docs/workflows/WORLD_GENERATION_PRESET.md` as the primary reference.

Do not invent a completely different pipeline unless the documented one cannot compile.

---

# 19. Create a minimal Unity test scene

Create a test scene such as:

```text
Assets/_Game/Scenes/WorldGenerationTest.unity
```

The scene should minimally contain:

```text
WorldRoot
├─ WorldStreamer
└─ ChunkPresentationRoot

TestFocus
Camera
Light
```

Assign:

- `WorldDefinition`;
- `WorldStreamingSettings`;
- a basic terrain material;
- `TestFocus` as streamer focus;
- stationary `ChunkPresentationRoot`.

Do not parent `ChunkPresentationRoot` under `TestFocus`.

If no art prefabs are available yet, terrain-only streaming is acceptable.

Missing archetype prefabs should not prevent terrain generation from being tested.

---

# 20. Test static preview first

Before runtime streaming, verify `WorldGenerator`.

Run:

1. Validate World Configuration
2. Generate Preview
3. Run Generation Diagnostics
4. Regenerate Preview (Next Seed)

Verify:

- no compiler errors;
- no exceptions;
- adjacent chunks have no terrain seams;
- same seed produces same results;
- different seed changes terrain;
- generated-spawn diagnostics pass.

---

# 21. Test rivers

Test multiple seeds.

Verify:

- rivers appear in macro data;
- river carving does not create obvious discontinuities at chunk borders;
- tributaries can join main rivers;
- downstream sections become wider/deeper;
- variable river widths affect exclusion masks;
- bridge span reflects local river width;
- road crossing cost does not crash with profiled rivers.

Do not spend excessive time tuning visuals before correctness is established.

---

# 22. Test runtime streaming

Run `WorldStreamer`.

Move `TestFocus` across multiple chunks.

Verify:

- nearby chunks load;
- nearest chunks load first;
- load budget is respected;
- distant chunks unload;
- unload hysteresis prevents constant thrashing;
- streamed chunk GameObjects do not move with `TestFocus`;
- runtime Mesh objects are released;
- active chunk data remains pinned;
- old unloaded data is eventually evicted when cache capacity is exceeded;
- returning to an evicted chunk reproduces the same base terrain/spawns.

---

# 23. Test runtime deltas

If generated object prefabs are available:

1. generate a chunk;
2. choose a `GeneratedWorldObject`;
3. record its `StableId`;
4. call `WorldRuntimeDeltaState.MarkSpawnRemoved(stableId)`;
5. unload the chunk;
6. move far enough to allow cache eviction;
7. return.

Expected:

The generated object with that stable ID must remain absent.

Do not solve this by mutating the procedural base data.

---

# 24. Test negative coordinates

The world is not only positive X/Z.

Move/generate chunks around positions such as:

```text
(-1, 0)
(0, -1)
(-1, -1)
(-10, 4)
```

Verify:

- correct chunk coordinate conversion;
- no seams;
- stable scatter;
- no duplicate border objects.

---

# 25. Performance sanity pass

Do not attempt final optimization yet.

Measure/observe:

- chunk generation time;
- mesh creation cost;
- number of active chunks;
- cache count;
- GameObject count;
- obvious frame spikes while moving focus.

Keep:

```text
maxChunkLoadsPerFrame
```

low initially.

The current generator is deliberately main-thread/budgeted.

Do not prematurely move the whole pipeline to `Task.Run`.

Unity/ScriptableObject/AnimationCurve access must be audited before background execution.

---

# 25A. Test session bootstrap separately

Session creation must not equal full-map detailed generation.

For a finite session, verify the intended flow:

```text
seed + session settings
        ↓
finite map bounds
        ↓
compact MacroWorldPlan
        ↓
start/fairness resolution
        ↓
prewarm only required starting areas
        ↓
MATCH READY
        ↓
additional chunks materialize through WorldStreamer during exploration
```

Do not generate every terrain mesh/tree/rock/resource GameObject on the full map
before declaring the session ready.

For representative configurations, at minimum:

```text
2 players
8 players
16 players
```

and more than one map size, record/report:

- total bootstrap wall-clock time;
- MacroWorldPlan generation time;
- fairness/start-selection time if enabled;
- number of detailed chunks generated before MATCH READY;
- number of active chunk GameObjects before MATCH READY;
- generated object count before MATCH READY;
- whether distant chunks remain unmaterialized;
- first-visit generation/materialization time for a distant chunk;
- memory/cache growth after traversing multiple areas.

Important:

- start timing before procedural bootstrap work begins;
- do not hide generation cost before the timer;
- total installed model/texture library size must not be serialized/transmitted
  as match state;
- clients should resolve stable archetype IDs to locally installed content;
- exact release timing budgets are TBD until representative game-ready assets
  exist, but timings must still be collected now.

See:
`docs/decisions/ADR-0002-fast-session-bootstrap-lazy-world-materialization.md`.

---

# 26. Do not implement these yet unless required to fix correctness

Do not expand scope into:

- full multiplayer;
- final water shaders;
- final road spline renderer;
- final vegetation GPU renderer;
- full watershed physics;
- final city simulation;
- advanced NPC AI;
- complete save-file persistence backend;
- world-origin rebasing;
- infinite-world or macro-tiling systems.

Document issues for later instead of mixing them into the first Unity verification pass.

---

# 27. Known next architecture tasks after Unity verification

After the current code compiles/runs successfully, priority follow-ups are:

1. configure and test finite host-selected map presets;
2. fog-of-war renderer + per-player/team visibility manager;
3. request-version/cancellation-safe async chunk generation;
4. streaming profiler;
5. chunk/root pooling;
6. GPU/instanced vegetation presentation;
7. road mesh/spline presentation;
8. water surface presentation;
9. improved neutral settlement generation/runtime state;
10. save serialization to disk / multiplayer session integration.

Do not start these before completing the integration report unless an existing compile/runtime bug requires part of them.

---

# 28. Required output from Codex

At the end of the pass, provide a concise report with:

## Environment

- Unity version used;
- OS/tooling;
- whether batchmode/editor compilation was actually run.

## Compilation

- compile success/failure;
- errors found;
- files changed to fix them.

## Scene/setup

- test scene created;
- ScriptableObject assets created;
- settings used.

## Generation tests

- deterministic generation;
- chunk seams;
- negative coordinates;
- rivers;
- roads/bridges;
- spawn generation.

## Streaming tests

- load/unload behavior;
- cache behavior;
- runtime delta reload behavior;
- observed performance.

## Session bootstrap

- bootstrap time;
- MacroWorldPlan time;
- start/fairness time;
- detailed chunks generated before MATCH READY;
- active/generated object counts at MATCH READY;
- confirmation that distant chunks were not materialized;
- first-visit distant-chunk generation cost;
- memory/cache growth during exploration.

## Remaining issues

Classify each remaining issue as:

- blocker;
- important;
- later optimization;
- future feature.

## Changes

List every file added/modified and explain why.

---

# 29. Change discipline

Use small coherent commits.

Suggested commit sequence:

```text
chore(unity): initialize Unity project metadata
fix(world): resolve Unity compilation issues
test(world): add procedural world test configuration
feat(world): add minimal world-generation test scene
fix(streaming): resolve runtime integration issues
docs(world): record Unity verification results
```

Do not bundle unrelated gameplay features into the integration pass.

---

# 30. Success criteria

The first Unity integration pass is successful when:

- the repository root opens as one Unity project;
- the code compiles;
- `WorldDefinition` and its settings can be created;
- preview generation runs;
- generation diagnostics pass;
- multiple adjacent chunks render without obvious seams;
- runtime streaming loads/unloads chunks around a focus;
- the same seed regenerates the same base chunk;
- runtime removed-object state survives chunk unload/regeneration;
- session creation does not materialize the full finite map;
- distant chunks remain lazy until required;
- bootstrap/performance metrics are reported rather than hidden;
- no existing architectural rule had to be broken to achieve this.

If a criterion cannot be completed, document exactly why instead of hiding the limitation.


## Cooperative prefetch is now implemented

Do not restore synchronous full-chunk generation inside WorldStreamer
presentation.

Current rule:

```text
WorldStreamer visible request
        ↓
data cache ready?
   yes        no
    ↓          ↓
present     cooperative generation
               ↓
          cache WorldChunkData
               ↓
             present
```

Background prefetch fills a larger data-only ring outside the visible radius.

Important: do not replace this with Task.Run over the existing
WorldGenerationPipeline. Some stages still read ScriptableObject and
AnimationCurve state. First identify/snapshot thread-safe data and profile the
expensive stage.
