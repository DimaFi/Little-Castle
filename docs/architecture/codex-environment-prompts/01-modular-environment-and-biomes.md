# CODEX PROMPT 01 — Modular Environment + Biome Foundation

**Little Castle / planning handoff / 2026-10-26**  
**Status:** approved design task; implementation NOT STARTED.  
**Scope:** existing world/terrain brushes, roads, vegetation, biome foundation, Editor + runtime contracts.  
**Priority:** P1 architectural prerequisite; summer polish remains first visual priority.

## Instructions for Codex

You are the Unity architect and senior technical artist for Little Castle. Create a **practical, minimal extension** of the existing world editor so future environment systems can be added as independent, configurable modules. First inspect the actual active branches: the repo default branch may not contain the newest brush implementations. Read root `AGENTS.md`, `docs/architecture/biome-adaptive-environment-roadmap.md` on the design branch, `docs/architecture/curved-modular-walls.md`, related graphics/URP work (#76 and dependent tasks), and source code. Work from the actual project's current renderer state, rather than assuming URP migration is complete in `main`. Do not overwrite ongoing grass/water/lighting/fog work.

### 1. Inventory before changing code

Locate and document the real production tools for terrain-height editing, meadow/grass/flower brush, tree scattering, roads/paths, ecological `BiomeKind` / `BiomeMask`, chunk/world data, save/load, material/shader architecture, foliage renderer, spline/walls. Record per tool: ownership, input/output, data representation, undo, updates on height change, generated data, current editor/runtime reuse, and tests. Search development branches if needed. Identify safe adapter/integration points and conflicts with ongoing PRs.

**Do not reimplement or replace existing brushes, create a parallel editor, or rename existing save/biome enums.**

### 2. Biomes as a world-space influence overlay

Keep three concepts distinct:

1. **Ecological base** — current generated terrain ecology, slope, moisture, temperature, ground cover. Existing `BiomeKind` semantics must remain stable.
2. **Authored environment theme overlay** — softly painted influence of summer kingdom, elf forest, enchanted woodland, snowy mountains, later more themes. Borders allow multiple weighted influences.
3. **Gameplay conversion overlay** — future authoritative spreading magic (undead corruption etc.), with explicit priority/owner/revision; no gameplay implementation now.

A theme covers the **same persistent world**, it does not destructively replace the terrain, stable entity IDs, path geometry, health, ownership or save records. Changes of appearance are separate from simulation. Use stable world-position sampling across chunk seams.

Design a versionable, data-driven `BiomeProfile` / `EnvironmentAppearanceProfile` (ScriptableObject where appropriate) for ground palettes, permitted vegetation, weather profile reference, material overlays, variant mapping by logical object archetype, deterministic scatter rules, fallback. Avoid hardcoded prefab names. Don't introduce a separate snow, elf, undead brush engine.

### 3. Thin surface cover — future-proof contract only

Provide only a **minimal general-purpose interface** that can later drive snow/moss/wetness on terrain and objects. Separate:
- biome/theme weight at position;
- actual local cover amount (e.g. snow accumulation), which depends on precipitation, melting, shelter and time;
- surface material/presentation response;
- optional short-lived footprint/decal effects.

This step MUST NOT implement snowfall, snow geometry, deep snow deformation, RDR2-style footprints, a second weather engine, or replace all existing shaders. No assumption that each snowy object needs a new prefab. Future approach: shared URP-compatible top-facing snow overlay + world-space mask; optional semantic mesh variants only if necessary. Material response must respect foliage wind/shadow passes and batching.

### 4. Minimal practical modules (names illustrative)

- `EnvironmentSample/Context`: world-space height, normal/slope, ecology, overlay theme weights, optional water and placement exclusion.
- `EnvironmentChangeTracker`: dirty world bounds, change categories, chunk-neighbor padding, revision; coalesce repeated brush events.
- `EnvironmentRefreshService`: independent listeners, bounded local invalidation, dependency ordering, no full world regenerate.
- `BiomeResolver`: weighted stable sampling of ecology + authored theme (+ future runtime conversion).
- `EnvironmentAppearanceResolver`: semantic logical archetype + theme weights -> material/visual presentation and sensible fallback.
- Data/Inspector profiles, debugging and tests; prefer existing project patterns rather than creating unused frameworks.

Ensure common logic runs in **runtime assemblies**, with Editor wrappers using the same APIs. Explicitly distinguish Authoring Undo/Redo from runtime commands. Allow deterministic serialization/versioning, future net-deltas and server validation; implement only the slice justified today.

### 5. First executable vertical slice

Integrate ONE existing brush (prefer height modification, or safer confirmed active tool) into change tracking, subscribe ONE demonstrator module and show only nearby chunks become dirty. Provide an illustrative theme field with 0..1 influence under a debug visualizer without producing winter art. Show unchanged base object/path stable IDs. Add Inspector-tunable parameters. Preserve all current editing tools. Test with constant seed and boundaries between chunks.

Before making any foliage/vegetation model or shader changes, follow mandatory AGENTS foliage model+shader preflight and actual production mesh validation.

### 6. Constraints and proof

- No per-frame whole-world scans; avoid per-instance material copies; preserve instancing/LOD/culling.
- No new 3D assets or network game logic.
- Unity Editor/Player validation when available; EditMode/PlayMode tests for local refresh, chunks/seams, unchanged saves, deterministic results, unaffected neighbors, correct subscriptions and no regression in existing brushes.
- Mark Unity/Player tests as **NOT RUN** if cannot execute; do not claim verified graphics with static checks.
- Commit a focused implementation in its own branch after documentation work is accepted; do not merge to `main` without approval or disturb teammates.
- Update `docs/roadmaps/environment-roadmap.md` and `docs/roadmaps/environment-progress.md` *during actual execution*, preserving the 12-step plan. Record test evidence, before/after and checkpoint. Only transition to step 02 when step 01 is actually complete and dependencies are safe; don't start step 11 early.

### 7. Acceptance criteria

- [ ] Existing production brush/road/biome architecture mapped and non-invasive integration identified.
- [ ] Ecology, authored theme and future gameplay conversion have separate responsibilities.
- [ ] Stable weighted biome sampling and future thin surface-cover contract are specified.
- [ ] One actual editor brush triggers bounded runtime-compatible world change notification.
- [ ] Base gameplay objects, world seeds, IDs, walls and saves remain stable.
- [ ] New parameters are configurable via Inspector/profiles; minimal debug visualization exists.
- [ ] Available tests recorded; Unity/Player NOT RUN clearly labelled if not executed.
- [ ] Follow-up tasks have exact integration points and no speculative mass refactor.

## Related docs

- [Existing biome-adaptive environment plan](../biome-adaptive-environment-roadmap.md)
- [Curved modular walls](../curved-modular-walls.md)
- [Step 11: light snow](11-thin-snow-and-weather.md)
- [Graphics 2.0 URP epic](https://github.com/DimaFi/Little-Castle/issues/76)

**Reminder:** The artistic target is warm, soft, coherent **Tiny Glade-like** stylization, without using its proprietary shaders, code or assets. This step prepares architecture, not a graphics makeover.
