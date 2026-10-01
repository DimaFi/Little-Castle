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
- finite host-selected session-map foundation with player-count restrictions;
- separate playable bounds and terrain-only visual border;
- one stable macro plan for the complete finite match;
- fog-of-war data foundation with Hidden / Explored / Visible states.

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
├─ WorldMapRules
│  └─ host-selectable finite map presets / player-count restrictions
├─ WorldStreamingSettings
│  └─ load/unload/cache policy
└─ WorldSpawnCatalog
   └─ archetypeId -> prefab variants
```

This keeps scene setup small and gives tools/Codex one clear root for world configuration.

## Read before coding

- `AGENTS.md`
- `docs/PROJECT_STRUCTURE.md`
- `docs/workflows/UNITY_SETUP.md`
- `docs/workflows/CODEX_WORKFLOW.md`
- `docs/workflows/CODEX_UNITY_HANDOFF.md`
- `docs/workflows/WORLD_GENERATION_PRESET.md`
- `docs/architecture/world-generation.md`
- `docs/architecture/world-object-generation.md`
- `docs/architecture/macro-world-generation.md`
- `docs/architecture/rivers-roads-bridges.md`
- `docs/architecture/world-streaming.md`
- `docs/architecture/finite-session-maps.md`
- `docs/architecture/fog-of-war.md`
- `docs/architecture/world-generation-roadmap.md`

## Unity status

The repository root is the Unity project root and is pinned to Unity `6000.5.5f1`.

The first Unity integration pass is complete:

- `Packages/`, `ProjectSettings/` and Unity-generated `*.meta` files are present;
- the runtime and editor/test assemblies compile in batchmode;
- `Assets/_Game/Settings/World/MainWorldDefinition.asset` is the root configuration;
- `Assets/_Game/Scenes/WorldGenerationTest.unity` is the minimal preview/streaming scene;
- automated EditMode and PlayMode coverage verifies deterministic generation,
  seams, negative coordinates, macro features, caching, mesh cleanup and runtime deltas.

See `docs/workflows/UNITY_VERIFICATION_REPORT.md` for the verified environment,
commands, measurements and remaining limitations.

To inspect manually, open the repository root in Unity Hub, open
`WorldGenerationTest.unity`, then use the `WorldGenerator` context actions:

1. **Validate World Configuration**;
2. **Generate Preview**;
3. **Run Generation Diagnostics**;
4. **Regenerate Preview (Next Seed)**.

Do not create a nested second Unity project.
