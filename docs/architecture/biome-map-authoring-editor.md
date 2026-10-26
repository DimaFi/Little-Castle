# Biome Painter — Developer Map Authoring Mode (DESIGN SPEC, NOT IMPLEMENTED)

**Owner:** Environment / map editor. **Priority:** P1, extension of Step 01 and prerequisite for Step 07 polish.  
**Status:** Required scope, no in-game/UI implementation in PR #79. Source biome/snow fields exist, but only programmatic `PaintTheme()` and GameObject context-menu demo commands exist.

## Experience / visual target

Developer opens existing **Little Castle map editor**, switches to **Biome** tool, optionally sets a top-down/orthographic city-builder-style camera (like Cities: Skylines, for editing comfort), sees the current terrain plus a translucent theme overlay and brush preview on the ground, chooses a biome from a configurable palette, paints directly over terrain with continuous drag. Size, softness, strength and shape are controllable. Updates should appear live and leave the persistent map entities intact.

**Do not create a parallel editor UI**; discover and extend the actual active editor brush/tool pipeline and reuse its camera, pointer world-ray/terrain projection, hotkeys, panels, Undo/Redo and save. Some current brush work is not on main. Do not blindly add a second paint system.

## Definitions and palette

The existing generated `World.BiomeKind` is **ecological terrain classification**: TemperateGrassland, WetLowland, etc. Keep it unchanged in save/world generation.

A `BiomeThemeProfile` / `EnvironmentThemeProfile` asset is a separate, stable-ID, versioned authoring theme with rules for:
- ground material palette, coloration and biome transition masks;
- vegetation / compatible foliage variants, density and eligibility;
- rock / road / wall / fence / building **appearance** mapping (not gameplay identity);
- optional climate / weather profile (including snowfall), local environmental effects;
- base cover defaults for editor preview, and overrides;
- slope, altitude, water-bank restrictions and fallbacks;
- explicit logical ID / name / preview icon / soft tint; no hardcoded prefab name.

Proposed initial palette: warm kingdom/meadow, enchanted forest, elven forest, snowy highland. More themes added by ScriptableObject assets, **not** new code branches. Painting meadow should influence ground cover/vegetation only after the actual grass/terrain integration is complete; until then mark preview explicitly.

## Interaction contract

1. **Tools**: paint, subtract/erase to authored base, smooth/blend boundaries, rectangle fill/select; optional lasso and eyedropper next iteration. A brush stroke interpolates dabs between mouse samples, avoiding gaps at high pointer speed; all selection uses world-space XZ and terrain raycasts.
2. **Settings**: radius, opacity/strength, softness/falloff, optional target theme weight, density/intensity preview, terrain slope restriction. Saved authoring presets.
3. **Visual feedback**: under-cursor soft disc projected on terrain; toggleable translucent biome overlays/outline legend; per-tile boundaries only in debug mode; area selected vs current base; progress/cancel for large fill.
4. **Map authoring camera**: top-down/angled orthographic view, pan/zoom/orbit and return to prior camera state. Reuse current editor camera; do not forcibly change in-game strategy camera.
5. **Undo/Redo**: stroke-based compact before/after changed tiles; redo should preserve exact values; rectangle fill is one undo command. Avoid copying entire world for one brush operation.
6. **Save/Load**: versioned edited influence raster + stable theme profile IDs (not palette indices or transient prefab names), deterministic restoration and handling missing variants. Atomic restore, compatibility with `WorldSaveState`; keep ecological original and logical entity IDs stable.
7. **Updates**: as the brush moves, only affected chunk tiles, plus seam margins, are marked dirty. Combine strokes and throttle visuals. Don't rebuild all vegetation in `OnGUI` or every pointer movement.
8. **Permissions**: tools available in developer map editor only. A future spell-driven converter calls the same runtime core via validated server commands; it must **not** access the developer UI/authoring APIs.
9. **Biomes vs snow**: winter theme weight is distinct from actual snow depth/cover. In **map authoring**, visual preset may display snow instantly (static cover seed or preview-only override) without demanding simulated hours of snowfall. In the game, weather independently accumulates/melts thin coverage. UI must expose this distinction.
10. **Existing object stability**: changing theme should adapt *appearance* of grass, flowers, trees, roads, houses, rocks and connected spline fences where compatible. Built structures, IDs, terrain heights, roads, ownership, durability and manually placed assets must not be deleted. Manual locked overrides respected.
11. **QA**: click+drag, slow/fast strokes, sharp vs soft boundaries, overlap summer/winter, rectangle+smooth, brush at negative XZ/chunk seam, preview doesn't mutate game world before commit, save/load, Undo/Redo, multiple editor sessions, performance near 2/8/16 player representative map, painter works without Play Mode if existing tooling supports it.

## Recommended integration order

**Slice 01A — Working artist brush:** find existing Editor authoring pipeline; add a new Biomes category and palette using `BiomeSnowWorld.PaintTheme` through reusable interface; top-down view, radius/strength/falloff, tint preview, continuous drag + eraser, bounded dirty event, Undo/Redo.

**Slice 01B — Authoring persistence:** adopt `EnvironmentFieldSnapshot` or equivalent in actual versioned map save, stable theme IDs/asset profiles, rectangle fill & smooth. Test save/reload/undo. Make changes non-destructive.

**Slice 07 — Real biome effects:** profile-driven reaction of terrain materials, grass/scatter, existing trees/roads/props/fences, local weather/snow. Add selected-region relabeling, lasso, visual debug and optimization as needed; no premature snow assets.

**Slice 11 — Winter content:** final snowy meadow materials, thin snow overlays and snowfall, optional temporary non-deforming footprints.

## Strong acceptance gate

Demo: in the **actual existing map editor**, with a fixed map seed and active trees/path/props, developer selects snowy profile, paints an irregular region over meadow while viewing from above; visually sees soft boundary and debug weight overlay; erases part; Undo/Redo and Save/Reload work. Scene remains playable; terrain, roads and stable logical objects stay in place. Verify changing profile doesn't require altering core paint code.

**As of 2026-10-26 this acceptance gate is NOT met by the foundation prototype**. The only existing paint API is programmatic, without a real developer brush UI or integration with existing vegetation/material rendering.
