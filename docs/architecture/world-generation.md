# World Generation Architecture

## Goal

Build a procedural world system that can start simple now and scale toward:

- terrain;
- biomes;
- rivers and lakes;
- roads and bridges;
- vegetation;
- resource nodes;
- points of interest;
- settlements;
- player territories;
- chunk streaming;
- save overrides;
- multiplayer synchronization.

## Layers

```text
World Seed
   ↓
Macro World Planning        (future)
   ↓
Chunk Generation Pipeline
   ↓
Authoritative Chunk Data
   ↓
Rendering / Prefab Layer
   ↓
Unity Scene Objects
```

The authoritative result is data, not GameObjects.

## Chunk coordinate model

A chunk coordinate is an integer pair:

```text
(x, z)
```

Chunk samples are converted to world coordinates before evaluating noise.

This prevents visible seams caused by each chunk evaluating independent local coordinates.

## Initial pipeline

The first version contains:

```text
HeightNoiseStage
   ↓
WorldChunkData.Heights
   ↓
ChunkMeshBuilder
```

This is intentionally small. The architecture is the important part.

## Planned stages

Likely local stages:

- BaseHeight
- Climate
- Biome
- TerrainMaterial
- LocalVegetation
- ResourceDecoration

Likely macro planners:

- River network
- Road graph
- Settlement placement
- Major landmarks
- Region ownership
- Trade routes

Macro planners should generate persistent feature descriptors that chunks query/project locally.

## Determinism

Do not use mutable global RNG state.

A deterministic value should derive from stable inputs, for example:

```text
Hash(worldSeed, chunkX, chunkZ, featureSalt)
```

or from absolute world sample coordinates.

## Border rule

For a chunk with N cells per side, its height grid has N + 1 samples.

The east border of chunk (x, z) must evaluate the same world coordinates as the west border of chunk (x + 1, z).

## Data model

`WorldChunkData` currently stores:

- chunk coordinate;
- resolution;
- height samples.

It is expected to grow gradually. Avoid turning it into an unstructured bag of everything.

Future feature data should use explicit records/structures such as:

- `RiverSegmentData`
- `RoadSegmentData`
- `BiomeCellData`
- `SpawnPointData`

## Rendering

`ChunkMeshBuilder` exists only to make generated data visible.

Later it may be replaced or complemented by:

- Unity Terrain;
- custom terrain meshes;
- GPU instancing;
- splat maps;
- vegetation systems;
- road mesh builders.

Changing rendering must not require rewriting generation logic.

## Streaming

Future `WorldStreamer` responsibilities:

- determine required chunks around players;
- request/generate chunk data;
- cache chunk data;
- create/destroy visual chunk instances;
- preserve runtime modifications separately from base procedural data.

Do not add streaming responsibilities to `WorldGenerator`.

## Save games

Base procedural generation should be reproducible from:
- generator version;
- world seed;
- settings identifier/version.

Runtime changes should be stored as overrides/deltas where practical.

Examples:
- tree removed;
- building constructed;
- road upgraded;
- resource depleted.

## Versioning

Changing generation algorithms can alter existing worlds.

Before production saves exist, iteration is free.

Once persistence matters, add a `generationVersion` field and migration policy before changing world-generation semantics.
