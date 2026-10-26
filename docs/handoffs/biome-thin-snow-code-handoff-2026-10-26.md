# Little Castle — biome and thin-snow prototype: actual implementation handoff

**Branch:** `feature/biome-thin-snow-foundation-2026-10-26`  
**Date:** 2026-10-26  
**Status:** C# implementation and standalone URP shader prototype committed. NOT integrated into production brush/persistence/material stack. Unity Editor / Player / Shader compilation: **NOT RUN** in current execution environment.

## Read first
- Root `AGENTS.md` (especially foliage wind mesh preflight and curved walls).
- `docs/architecture/biome-adaptive-environment-roadmap.md` on `design/environment-biome-snow-prompts-2026-10-26`.
- [Previously approved prompt 01](https://github.com/DimaFi/Little-Castle/blob/design/environment-biome-snow-prompts-2026-10-26/docs/architecture/codex-environment-prompts/01-modular-environment-and-biomes.md).
- [Previously approved prompt 11](https://github.com/DimaFi/Little-Castle/blob/design/environment-biome-snow-prompts-2026-10-26/docs/architecture/codex-environment-prompts/11-thin-snow-and-weather.md).
- [GitHub issue #78](https://github.com/DimaFi/Little-Castle/issues/78).
- Graphics 2.0 URP roadmap #76 and actual active worktrees/branches.

## What code exists now

### `Assets/_Game/Scripts/World/Environment/EnvironmentFields.cs`

- `SparseWorldFloatField`: chunk-like fixed-size sparse **XZ** scalar tiles, supports negative coordinates and cell-centred bilinear sampling across tile borders. Captures/restores exact tile data.
- `BiomeInfluenceField`: independent world-space artistic theme overlay; does NOT change existing `World.BiomeKind`. Summer is implicit baseline (0), but other themes use extendable integer IDs. Circular soft brush blends intensities, suppressing competing themes, emits one bounded dirty-rectangle event per edit. Revision counter and snapshot model.
- `SnowAccumulationField`: cover independent from theme membership. Local region weather stepping with snowfall, melt and winter-influence response. Avoids geometry deformation.
- `EnvironmentFieldSnapshot`: version 1 serialization DTOs for theme tiles and snow tiles. **Not yet plugged into production save/replication.**

### `Assets/_Game/Scripts/World/Environment/BiomeSnowWorld.cs`

Runtime MonoBehaviour entrypoint for editor or future gameplay code:
`PaintTheme(Vector3 worldPoint, float radius, int themeId, float opacity)`, `SetSnowfall(float intensity)`, `AdvanceWeather(float seconds)`, `ExportJson()`, `ImportJson(string json)`. Inspector controls for resolution/brush-test region/accumulation/melting, periodic weather simulation. Two context menu paint samples at the GameObject transform for smoke testing. Restore stages both fields before committing to avoid partial invalid saves.

### `Assets/_Game/Scripts/World/Environment/SnowMaskTextureBridge.cs`

Translates local snow (R) + winter biome influence (G) into a **bounded single RGBA32 Texture2D** with configured bounds; sets `_LC_SnowField` and `_LC_SnowFieldRect` global shader parameters. Refreshes at most every configurable interval after dirty events; uses world-space sampling so a wall/road with several pieces can have a blended boundary without switching physical prefabs. **Prototype for one bounded world region**, not a production streaming/atlas implementation.

### `Assets/_Game/Scripts/World/Environment/BiomeSnowfallParticleDriver.cs`

Optional low-cost snowfall using an authored Unity ParticleSystem (with its own URP-compatible snowflake material). Follows a target near the camera, controls emission rate from both winter theme weight and `BiomeSnowWorld.SnowfallIntensity`. Does not simulate collisions/snow accumulation per particle: accumulation remains in the independent CPU field. Set ParticleSystem shape, gravity, particle lifetime, texture/material and emission area through its own Inspector. Shelter and interior exclusions remain TODO.

### `Assets/_Game/Shaders/Effects/LC_ThinSnowURP.shader`

Independent lightweight URP prototype shader for **static opaque meshes**, with base texture, soft winter tint, upward-normal threshold, breakup/noise, fog, simple Lambert/SH light with main light/shadow and URP Lit shadow/depth pass reuse. Uses the world-space snow mask. **Not a replacement for production LC_StylizedLit, terrain or LC_Foliage.** Its material must be assigned intentionally for smoke tests. No advanced PBR lighting or foliage wind support.

### `Assets/_Game/Shaders/Effects/LC_ThinSnowShaderGraph.hlsl`

Reusable Shader Graph Custom Function `LC_ThinSnow_float` returning snow coverage and blended albedo; intended for **manual integration into existing material graphs** in the actually approved URP renderer. Do not blindly transplant onto `LC_Foliage`; preserve animated ShadowCaster pass and model compatibility.

### `Assets/_Game/Tests/Editor/EnvironmentBiomeSnowTests.cs`

EditMode tests authored (not executed): world negatives, interpolation across tile seams, summer winter brush with localized event, snowfall accumulating/melting, JSON snapshot roundtrip, corrupted tile rejection.

## Try it locally after verifying render pipeline

1. Open in Unity 6000.5.5f1 (from ProjectSettings in main) or the actual matching version for the integration branch. Check Package Manager and graphics settings first: **main branch Packages/manifest.json does not include URP**, despite user's active URP development elsewhere. DO NOT install/overwrite URP or replace existing materials as a side-effect of this prototype.
2. On an **approved URP branch**, add empty GameObject `BiomeSnowWorld` with `BiomeSnowWorld` and `SnowMaskTextureBridge`. Keep the default world-space simulation rectangle or adjust to test plot. Bridge discovers controller on the same GO.
3. Place **one static opaque prop** using a new material with shader `Little Castle/Environment/URP Thin Snow Prototype` (only if valid in that URP version). For existing material/shader, adapt Custom Function instead of replacing it.
4. Optionally add a separate ParticleSystem + `BiomeSnowfallParticleDriver` with `world` and `followTarget` assigned, a small box emission area and a compatible unlit snowflake particle material. Its rate follows winter influence and weather intensity. In Play Mode call component context menu `Paint winter at this GameObject (prototype)` (or call `PaintTheme` in code); set `Snowfall Intensity` nonzero, observe coverage grow over simulation time, stop snowfall and observe slower melting. Set brush radius and GO position to match demonstration.
5. Drive `ExportJson/ImportJson` on demand in an external tool/test. This is not already plugged into `WorldSaveState`.
6. Run tests and image regression captures. If shader fails to compile, read errors from Unity; record details, adjust based on actual URP package version. No Unity visual validation has occurred yet.

## Limitations / known next tasks

- **No integration with active terrain meadow/tree/path brush**; locate correct branch and reuse existing brushes. Do not duplicate editor tools. Wire real terrain edits to localized events.
- **No gameplay/server save/network integration**; immutable ecological `BiomeKind` is untouched intentionally. Need stable theme IDs in shared saves and authoritative runtime magic only in future.
- **Snow restricted to materials opt-in**; models using original shaders do NOT magically receive snow. Bridge uniforms and Shader Graph helper provide the contract for future integration with URP terrain, LC_StylizedLit, walls, fences, tree leaves, roads.
- **Single global texture** only supports one bounded region and full mask upload when dirty; convert to chunked texture atlas/virtual region and clamp work budget before huge strategy maps or split-screen/multiple worlds.
- Snow simulation also scans the provided bounded rectangle on each tick, not sparse dirty weather cells; optimize with active/tiled updates and empty-tile pruning after real measurements. Use a fixed/deterministic timestep and authority for multiplayer later.
- Snow presently has no shelter/occlusion maps, snow precipitation particles, roof masks, snow wind/fog tie-in, temporary footprints, terrain surface blend or vegetation fading. These are subsequent work.
- Prototype material uses a simple Lambert model; final gentle high-quality Tiny Glade-like art direction needs production lit shading, consistency with existing atmosphere and URP Forward+/SRP Batcher.
- URP Lit `UsePass` and ShaderGraph macros must be **verified in actual installed URP**; cannot be marked PASS now.
- Do not use the ZGeng legacy hull/domain geometry deformation in the shipped snow; `DeepSnowFootprint` is a reference for optional fading 2D footprints only.

## Free-source references and licensing

1. Official Unity Shader Graph Production Ready Weather / Snow:
   https://github.com/Unity-Technologies/Graphics/blob/master/Packages/com.unity.shadergraph/Documentation~/Shader-Graph-Sample-Production-Ready-Weather.md
   The best start for snow accumulating on upward-facing surfaces. Check installed sample version and Unity licensing/attribution before copying sample content.
2. MIT DeepSnowFootprint (Unity 5.5): https://github.com/ZGeng/DeepSnowFootprint
   Refer only to footprint painting/fading ideas; no deep deformation needed.
3. MIT SnowShaderGraph: https://github.com/mert-dev-acc/SnowShaderGraph
   Secondary research for RenderTexture-based fading footprints. Review code and version before optional usage.

No source code/assets from these external projects have been copied into this branch. Our implementation is new prototype code based on the generic techniques.

## Acceptance gates before merging or calling complete

- [ ] Real Unity EditMode tests run and pass; log editor version, branch, commit.
- [ ] Pipeline URP version checked and material/shader compile verified in Unity on actual branch.
- [ ] Golden scene screenshot comparisons (ground, roof, rock, wall, tree, path) with soft coherent look.
- [ ] Original brush and chunk streaming regressions verified.
- [ ] Save/load integrated/versioned and negative world border tests.
- [ ] Foliage wind and shadow pass parity when snow visuals are added.
- [ ] No per-frame whole-world edits / unnecessary material instantiation; CPU/GPU timings from Player.
- [ ] Code reviewed/merged via separate PR, not direct main overwrite.

**Important:** This deliverable is a **substantial initial implementation** of the data layer, runtime test entrypoints, texture presentation contract, demo shader, and tests; it is **not** a finished winter production system.
