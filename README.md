# Little Castle

Little Castle is a long-term Unity project about building and developing a cozy medieval settlement in a large procedural world.

## Current procedural-world foundation

The repository already contains:

- deterministic seed-based generation;
- chunk coordinates and seamless world-space terrain sampling;
- ordered generation phases with validation;
- layered terrain, river carving, slope analysis;
- climate, biome and logical terrain-surface data;
- forest-density and scalable grass-density fields;
- deterministic object scatter for trees, rocks, bushes and props;
- authoritative stone/ore deposit data;
- macro neutral settlements, ruins and landmarks;
- logical road graph + coarse terrain-aware A* road paths;
- early downhill river tracing;
- bridge sites derived from road/river intersections;
- placement exclusion masks so infrastructure reserves space before local scatter;
- stable generated object IDs;
- prefab-independent `archetypeId` records;
- `WorldSpawnCatalog` for mapping logical objects to Unity prefabs;
- runtime deltas for removed objects and depleted deposits;
- generation-versioned save metadata;
- configuration validation and determinism/seam diagnostics;
- bounded LRU generated-chunk cache with active-chunk pinning;
- budgeted runtime chunk streaming around a focus transform;
- explicit fixed-session macro-plan safety boundary for current bounded macro generation.

The central rule is:

> **Generated world data is authoritative; Unity GameObjects are presentation.**

## Root configuration

Unity scenes should reference one `WorldDefinition` asset.

```text
WorldDefinition
├─ WorldGenerationSettings
│  └─ ordered generation stages
├─ MacroWorldPlannerSettings
│  ├─ settlements / ruins / landmarks
│  ├─ rivers
│  ├─ road graph / path solver
│  └─ bridges
├─ WorldStreamingSettings
│  └─ load/unload/cache/session-macro policy
└─ WorldSpawnCatalog
   └─ archetypeId -> prefab variants
```

This keeps scene setup small and gives tools/Codex one clear root for world configuration.

## Read before coding

- `AGENTS.md`
- `docs/PROJECT_STRUCTURE.md`
- `docs/workflows/UNITY_SETUP.md`
- `docs/workflows/CODEX_WORKFLOW.md`
- `docs/workflows/WORLD_GENERATION_PRESET.md`
- `docs/architecture/world-generation.md`
- `docs/architecture/world-object-generation.md`
- `docs/architecture/macro-world-generation.md`
- `docs/architecture/rivers-roads-bridges.md`
- `docs/architecture/world-streaming.md`
- `docs/architecture/world-generation-roadmap.md`

## Unity status

The repository root is intended to be the Unity project root.

Unity has **not yet been opened for this repository**, so `Packages/`, `ProjectSettings/` and Unity-generated `*.meta` files are not committed yet.

The current C# foundation is written against built-in Unity APIs, but it must still be compiled and visually verified inside the chosen Unity version before being treated as production-ready.

Once a Unity version is chosen:

1. open the repository root as the Unity project;
2. commit `Packages/`, `ProjectSettings/` and generated `*.meta` files;
3. create the settings assets described in `docs/workflows/WORLD_GENERATION_PRESET.md`;
4. create one `WorldDefinition`;
5. assign it to a scene `WorldGenerator`;
6. run **Validate World Configuration**;
7. run **Generate Preview**;
8. run **Run Generation Diagnostics**;
9. inspect several seeds before adding large art libraries.

Do not create a nested second Unity project.
