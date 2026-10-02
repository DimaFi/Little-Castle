# World Streaming

## Purpose

`WorldStreamer` presents a finite deterministic session map around a moving
camera/player focus without keeping every terrain chunk as a live GameObject.

It is separate from `WorldGenerator`:

- `WorldGenerator` = bounded preview/debug tool;
- `WorldStreamer` = runtime loading/unloading policy.

## Product model

Little Castle sessions use finite host-selected maps.

Preferred production flow:

```text
Host chooses player count + map preset + seed
        ↓
WorldSessionMap
        ↓
Playable bounds + visual bounds
        ↓
one MacroWorldPlan for the entire playable map
        ↓
WorldStreamer presents nearby chunks
```

There is no requirement for an infinite world.

## Runtime pipeline

```text
Focus Transform
      ↓
Focus Chunk
      ↓
Desired Chunk Set
      ↓
Distance-prioritized Load Queue
      ↓
Per-frame Generation Budget
      ↓
WorldChunkCache (LRU)
      ↓
WorldChunkData
      ↓
Chunk Mesh + Spawn Presentation
      ↓
Active StreamedChunkView
```

## Finite-map bounds

A configured `WorldSessionMap` contains:

- `playableChunks`;
- `visualChunks`.

### Playable chunks

Full generation/presentation is allowed:

- terrain;
- macro projection;
- environment;
- resources;
- local objects;
- colliders if enabled.

### Visual-only chunks

These form a small border outside gameplay bounds.

Current streamer behavior:

- generation stops at `TerrainAnalysis`;
- no generated object presentation;
- no gameplay resource presentation;
- no MeshCollider;
- chunk exists only to continue the visible terrain beyond the gameplay edge.

Beyond `visualChunks`, no chunk is streamed.

## Camera and movement boundary

`WorldStreamer.TryClampToPlayableBounds(...)` is a utility for future camera
or unit controllers.

The movement/pathfinding system remains responsible for enforcing gameplay
movement rules.

Do not let visual padding become traversable gameplay space.

## Host-selected size

`WorldMapRules` defines map presets and player-count restrictions.

A session may not select a map preset that rejects its player count.

Session-authoritative data should include:

- seed;
- player count;
- preset ID;
- generation version.

See `docs/architecture/finite-session-maps.md`.

## Session bootstrap boundary

A finite map does not imply that every detailed chunk exists as a Unity object
when the match begins.

Session creation may build compact world-scale data for correctness, including:

- bounds;
- MacroWorldPlan;
- river/road/bridge data;
- neutral settlement anchors;
- selected player starts.

Detailed chunk presentation remains lazy.

Before MATCH READY, generate only chunks required immediately for player
starting areas according to the configured prewarm policy.

Do not:

- create every chunk mesh on the complete map;
- instantiate all trees/rocks/resources before the match starts;
- load every possible world asset simply because it exists in the installed game.

The total installed model/texture library should primarily affect install size
and runtime asset-loading behavior, not deterministic session-state size.

See ADR-0002.

## Load/unload hysteresis

The streamer uses:

```text
load radius
    <
unload radius
```

This prevents edge chunks from repeatedly loading/unloading as the focus moves
near a chunk boundary.

## Prioritized loading

Missing chunks are sorted by squared distance to the focus.

Nearest chunks load first.

Chunk generation is currently main-thread and budgeted by:

- maximum loads per frame;
- maximum unloads per frame.

Do not move the current pipeline directly into `Task.Run` without first
separating Unity asset access from pure generation data.

## Generated-data LRU cache

`WorldChunkCache` uses least-recently-used eviction.

- active streamed chunks are pinned;
- pinned chunks cannot be evicted;
- unloaded chunks are unpinned;
- old unpinned chunks may be evicted;
- evicted chunks regenerate deterministically.

A second cache can generate visual-border chunks only through an earlier
pipeline phase.

## Runtime delta

`WorldRuntimeDeltaState` remains separate from generated base data.

A removed generated object stays removed after:

1. chunk unload;
2. cache eviction;
3. deterministic regeneration;
4. presentation rebuild.

## Macro world

For finite sessions the macro planner receives the full playable world bounds
once during session initialization.

Therefore:

- settlements are stable;
- river networks are stable;
- road relationships are stable;
- bridge sites are stable.

Do not rebuild MacroWorldPlan while the camera moves.

The old `macroPlanRadiusChunks` behavior remains only as a temporary fallback
for test scenes/configurations that have not yet assigned `WorldMapRules`.

It is not the intended match architecture.

## Presentation lifecycle

`StreamedChunkView` owns transient runtime mesh resources.

When a chunk unloads:

1. remove it from active set;
2. unpin the correct data cache;
3. release runtime Mesh;
4. destroy its transient scene hierarchy.

Future presentation improvements may add:

- chunk root pooling;
- object pooling;
- GPU-instanced vegetation;
- HLOD;
- collider distance tiers.

These must not alter authoritative `WorldChunkData`.

## Current limitations

Not yet implemented:

- async/cancellation-safe chunk generation;
- pooled chunk roots;
- large vegetation GPU presentation;
- multiple streaming focuses;
- fog-of-war rendering;
- border darkness renderer;
- streaming profiler/metrics.

These are follow-up tasks, not reasons to redesign the finite-map model.
