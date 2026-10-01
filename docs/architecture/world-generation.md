# World Generation Architecture

## Goal

Little Castle needs a procedural world that can grow for years without coupling terrain generation to individual art assets.

The target world includes:

- plains, rolling terrain, highlands and more varied terrain later;
- rivers, water corridors and bridges;
- multiple road types;
- dense forests and strategic wood supply;
- stone and ore deposits;
- neutral settlements;
- ruins, abandoned fortified sites and landmarks;
- signs and road furniture;
- local vegetation and decorative objects;
- player construction and destruction;
- chunk streaming;
- saves;
- possible multiplayer.

## Layer model

```text
World Seed
    ↓
Regional / Macro World Plan
    ↓
Chunk Generation Pipeline
    ↓
Authoritative World Data
    ↓
Runtime World Delta
    ↓
Rendering / Prefab Layer
    ↓
Unity Scene Objects
```

**World data is authoritative. GameObjects are presentation.**

## Current terrain pipeline

Recommended early pipeline:

```text
LayeredTerrainStage
    ↓
WorldChunkData.Heights
    ↓
TerrainClassificationStage
    ↓
CellSlopes + TerrainClasses
    ↓
ChunkMeshBuilder
```

`HeightNoiseStage` remains as a simple/experimental height stage. Do not run two independent height-writing stages unless intentionally designing their composition.

## Layered terrain

`LayeredTerrainStage` currently combines:

- very-low-frequency regional relief;
- rolling hills;
- ridge-shaped mountain/highland relief;
- fine detail.

All sampling is performed in **absolute world coordinates**.

Therefore changing chunk boundaries does not change the terrain field itself.

## Chunk coordinates

A chunk coordinate is:

```text
(x, z)
```

For N cells per side the chunk stores N + 1 height samples per side.

This lets neighboring chunks share the same mathematical border samples.

Derived terrain metadata is stored per cell:

- slope in degrees;
- coarse `TerrainClass`.

Terrain class is **not** a biome. Biomes will eventually combine climate, region, height, moisture and possibly soil information.

## Determinism

Authoritative generation must never depend on mutable `UnityEngine.Random` state.

Random-looking decisions derive from stable values such as:

```text
worldSeed
world coordinate
feature salt/type
stable feature ID
generation version
```

The same inputs must produce the same generated base world.

## Diagnostics

`WorldGenerationDiagnostics` currently verifies:

- same seed + same chunk => same heights;
- east/west chunk borders match;
- north/south chunk borders match.

The scene-facing `WorldGenerator` exposes:

- **Generate Preview**
- **Regenerate Preview (Next Seed)**
- **Run Generation Diagnostics**
- **Clear Preview**

These diagnostics are a guardrail. Later automated Unity tests should supplement them.

## Macro world plan

Some features cannot be generated independently inside each chunk.

Examples:

- settlements;
- roads;
- rivers;
- bridges;
- major ruins;
- landmarks;
- regional forests.

These belong to, or are coordinated by, a world-scale plan.

Initial scaffolding:

- `MacroWorldPlan`
- `WorldPointFeatureData`
- `WorldFeatureKind`

Roads and rivers must receive dedicated line/network data types later. Do not force them into point-feature records.

## Neutral settlements

Neutral settlements are world-scale entities rather than decorative village prefabs.

Their generated descriptor should eventually contain stable identity and placement data, while gameplay/runtime state stores things such as:

- relationship with players;
- economic benefit;
- influence/control;
- competition between players;
- upgrades or destruction if the final design allows it.

The procedural generator chooses *where the settlement exists*.

The gameplay simulation decides *what is currently happening there*.

## Forests

Forests are both visual and strategic.

Wood is expected to be heavily consumed by construction and military development, so forest generation should eventually use:

```text
Forest region
    ↓
density / species / strategic capacity
    ↓
concrete tree spawn data
    ↓
rendered tree instances
```

Chopping one tree must not erase the underlying forest-region concept.

## Resources

Stone and ore are authoritative resource features.

Future deposit data should support:

- stable ID;
- resource type;
- position/area;
- richness/capacity;
- depletion runtime state;
- extraction suitability.

A visible rock mesh alone must never be the resource database.

## Roads

Roads are generated as a network.

Target pipeline:

```text
settlements / POIs
    ↓
connectivity graph
    ↓
terrain-aware path cost
    ↓
world-space road path/spline
    ↓
chunk clipping
    ↓
terrain deformation / surface data
    ↓
road visuals
```

Road types can then change width, cost and visuals without changing the concept of connectivity.

## Rivers and bridges

Rivers must preserve continuity across chunks.

Bridges are derived from meaningful crossings between route corridors and water corridors, then validated against:

- river width;
- bank slope;
- crossing angle;
- terrain suitability.

Do not scatter bridges randomly.

## Local decoration

Local deterministic decoration may include:

- grass;
- bushes;
- small rocks;
- fallen branches;
- flowers;
- minor clutter.

It must respect exclusion masks from:

- roads;
- rivers;
- settlements;
- buildings;
- resource extraction areas.

## Save model

Long term:

```text
GeneratedBaseWorld
+
RuntimeWorldDelta
=
CurrentWorld
```

Examples of delta state:

- chopped tree;
- mined deposit;
- player building;
- destroyed bridge;
- upgraded road;
- changed neutral-settlement relationship/control.

## Streaming

A future `WorldStreamer` will:

- decide which chunks players need;
- generate/load chunk data;
- cache data;
- create/recycle visuals;
- unload presentation safely;
- keep runtime changes separate from the immutable generated base.

Do not put these responsibilities into `WorldGenerator`.

## Generation versioning

Algorithm changes can alter worlds.

Before persistent saves matter, iteration is flexible.

Before shipping persistent worlds, introduce a `generationVersion` and migration/compatibility policy.
