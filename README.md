# Little Castle

Little Castle is a long-term Unity project about building and developing a cozy medieval settlement in a large procedural world.

## Current foundation

The repository already contains an extensible procedural-world foundation:

- deterministic world seed;
- chunk coordinates and chunk-local data;
- ordered ScriptableObject generation pipeline;
- layered terrain with regional relief, hills, highlands/ridges and detail;
- per-cell slope and terrain classification;
- deterministic regeneration with different seeds;
- seam and determinism diagnostics;
- macro-world scaffolding for future settlements, ruins and other cross-chunk features;
- debug mesh preview;
- architecture documentation for roads, rivers, bridges, forests, resources, neutral settlements, saves and multiplayer.

The central rule is:

> **Generated world data is authoritative; Unity GameObjects are presentation.**

## Read before coding

- `AGENTS.md`
- `docs/PROJECT_STRUCTURE.md`
- `docs/workflows/UNITY_SETUP.md`
- `docs/workflows/CODEX_WORKFLOW.md`
- `docs/architecture/world-generation.md`
- `docs/architecture/world-feature-catalog.md`
- `docs/architecture/world-generation-roadmap.md`

## Unity

The repository root is the future Unity project root.

Once a Unity LTS/version is selected, open this repository itself in Unity and commit the generated:

- `Packages/`
- `ProjectSettings/`
- Unity `*.meta` files.

Do not create a nested second Unity project.

## First terrain preview

After the Unity project metadata exists:

1. Create a `WorldGenerationSettings` asset.
2. Create a `LayeredTerrainStage` asset.
3. Create a `TerrainClassificationStage` asset.
4. Add the stages to settings **in that order**.
5. Create a scene GameObject with `WorldGenerator`.
6. Assign the settings and any preview material.
7. Run **Generate Preview**.
8. Run **Regenerate Preview (Next Seed)** to see another deterministic world.
9. Run **Run Generation Diagnostics** after generation changes.

The older `HeightNoiseStage` is a simpler experimental height stage and should not be stacked with `LayeredTerrainStage` unless that composition is intentional.
