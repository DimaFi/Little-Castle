# Little Castle

Little Castle is a long-term Unity project about building and developing a cozy medieval settlement in a large procedural world.

## Current foundation

The repository starts with a deliberately small but extensible world-generation foundation:

- deterministic world seed;
- chunk coordinates and chunk-local data;
- ordered generation pipeline;
- ScriptableObject-based generation settings and stages;
- deterministic terrain-height generation;
- simple mesh preview for generated chunks;
- architecture documentation for future rivers, roads, biomes, settlements, resources and multiplayer.

The generation code is designed so that **world data is separate from Unity scene objects**. This is important for save games, testing, streaming, large worlds and future multiplayer.

Read:

- `AGENTS.md` before changing code with Codex.
- `docs/architecture/world-generation.md` for the generation architecture.
- `docs/decisions/ADR-0001-deterministic-chunk-pipeline.md` for the first architecture decision.

## Unity

This repository does not lock a Unity version yet. Create/open the real Unity project at the repository root and then commit Unity-generated `ProjectSettings/`, `Packages/` and `*.meta` files.

The current code intentionally depends only on built-in Unity APIs.
