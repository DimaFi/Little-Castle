# World Streaming

## Purpose

`WorldStreamer` turns deterministic chunk generation into a runtime world
that follows a player/camera focus.

It is deliberately separate from `WorldGenerator`.

- `WorldGenerator` is a bounded preview/debug tool.
- `WorldStreamer` owns runtime loading/unloading policy.

## Current runtime pipeline

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
Active Chunk View
```

When a chunk becomes distant:

```text
Active Chunk
    ↓
Unpin generated data
    ↓
Release runtime Mesh
    ↓
Destroy transient GameObject tree
    ↓
LRU cache may retain or evict generated data
```

Eviction does not alter the authoritative world because base chunks regenerate
from the same seed/generation version.

## Configuration

`WorldStreamingSettings` is referenced from `WorldDefinition`.

Current settings include:

- load radius in chunks;
- unload padding/hysteresis;
- circular or square loading shape;
- maximum chunk loads per frame;
- maximum chunk unloads per frame;
- generated-data cache capacity;
- fixed session macro-plan radius;
- macro-edge warning distance;
- generated-spawn presentation toggle;
- optional MeshCollider creation.

## Load and unload radii

The streamer uses two radii:

```text
load radius
    <
unload radius
```

The gap prevents chunks at the edge from repeatedly loading/unloading when the
focus moves around a boundary.

## Prioritized loading

Missing chunks are sorted by squared distance to the focus chunk.

This means the terrain immediately around the player is generated before
farther edge chunks.

The current implementation limits how many new chunks are generated each
frame.

Generation is intentionally still main-thread/budgeted.

Do not move current generation directly to `Task.Run` without auditing:

- ScriptableObject access;
- AnimationCurve evaluation;
- Unity object access;
- presentation creation;
- cancellation/version races.

A later async generation pass should split pure generation snapshots from Unity
asset access first.

## Generated-data LRU cache

`WorldChunkCache` now uses least-recently-used eviction.

Important behavior:

- active streamed chunks are pinned;
- pinned chunks are never evicted;
- unloaded chunks become unpinned;
- unpinned old chunks may be evicted when capacity is exceeded;
- capacity is a soft limit if all cached entries are active/pinned.

This keeps a long exploration session from retaining every previously visited
chunk forever.

## Runtime world delta

`WorldStreamer` passes `WorldRuntimeDeltaState` into
`ChunkSpawnPresenter`.

Therefore a generated object marked removed can remain absent after:

1. its chunk unloads;
2. cached data is evicted;
3. the chunk regenerates from seed;
4. presentation is rebuilt.

The generated base world and player history remain separate.

## Current macro-world safety rule

The current `MacroWorldPlanner` plans bounded world areas.

Rebuilding a different bounded macro plan every time the player moves could
change:

- settlement neighbor relationships;
- roads;
- river networks;
- bridge sites

inside overlapping areas.

Therefore the current streamer builds **one fixed MacroWorldPlan per streaming
session** around the initial focus.

```text
Initial Focus
      ↓
Large Session Macro Bounds
      ↓
One MacroWorldPlan
      ↓
Many streamed chunks inside it
```

The streamer deliberately does not silently regenerate the macro plan while
moving.

When the focus approaches the configured macro edge, it logs a warning.

This is a correctness decision, not the intended final infinite-world design.

## Future macro tiles

The production direction is fixed deterministic macro tiles/regions.

Conceptually:

```text
Macro Tile (x,z)
   ├─ owned point features
   ├─ river descriptors
   ├─ road graph fragments
   └─ cross-tile connection contracts
```

Then a streamer can load/unload macro tiles independently without changing
already visited world structure.

Do not implement moving-window macro replanning as a shortcut.

## Presentation lifecycle

Each streamed chunk owns a `StreamedChunkView`.

It currently owns the runtime-created terrain Mesh and releases it when the
chunk unloads.

Generated prefab children are destroyed with the chunk root.

Future presentation improvements can add:

- pooled chunk roots;
- pooled GameObjects;
- GPU-instanced trees/grass;
- HLOD;
- collider distance tiers;
- terrain material/splat presentation.

These must not change `WorldChunkData`.

## Current limitations

Not yet implemented:

- true background/asynchronous chunk generation;
- cancellation tokens/generation request versions;
- chunk GameObject pooling;
- GPU-instanced vegetation;
- multiple streaming focuses;
- deterministic macro tiles;
- world-origin rebasing for extreme distances;
- runtime-profiler metrics.

These are explicit follow-up tasks rather than hidden behavior.
