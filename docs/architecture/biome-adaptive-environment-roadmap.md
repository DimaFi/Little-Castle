# Biome-adaptive environment: flowers, splines, paths, snow and gameplay conversion
Status: design / Codex handoff; **no runtime implementation in this branch**
Date: 2026-10-26
Target: Little Castle, Unity URP

## Product intent and order

Make the summer biome look finished first. Improve existing flowers immediately, then add
varieties. Plan the unified biome-adaptation architecture now, without prematurely
implementing snow, wooden fences or magical terrain conversion.

Priorities:
- **Now / P0:** fix existing meadow flower placement (uprightness, grounding, normals/pivots).
- **Now / P1:** add varied summer meadow flowers using the existing brush/scatter system.
- **Design / P1:** central biome resolver + reusable visual-variant contract for all objects,
  roads, walls and future fences.
- **Next / P1:** extensible paths and terrain-surface presentation.
- **Later / P2:** lightweight wooden fences, winter terrain/detail brushes, snowy variations
  for walls/fences/roads after summer completion.
- **Later / P3:** server-authoritative terrain conversion by magic, as in spreading undead
  ground in Warcraft. Keep this use case in every early data contract.

Do not break current water, atmosphere, lighting, grass or path authoring workflows.
No changes to `main` or runtime code are part of this design branch.

## Repository integration points already present on main

- `Assets/_Game/Scripts/World/Core/BiomeKind.cs`: ecological classifier
  (grassland, woodland, wet lowland, highland meadow, rocky highland).
- `Assets/_Game/Scripts/World/Core/BiomeMask.cs`: ecological biome flags.
- `Assets/_Game/Scripts/World/Generation/Stages/BiomeClassificationStage.cs`:
  climate + terrain to ecological class.
- `Assets/_Game/Scripts/World/Generation/Stages/GroundCoverStage.cs`:
  grass density from ecological biome and terrain surface.
- `Assets/_Game/Scripts/World/Core/WorldChunkData.cs`: sampled chunk data.
- `Assets/_Game/Scripts/Building/Walls/WallPathLayoutUtility.cs`:
  deterministic sampled centerline and rigid repeated wall sections.
- `Assets/_Game/Scripts/Building/Walls/WallPlacementDefinition.cs`:
  per-wall module length, spacing, prefab, validation, towers.
- `Assets/_Game/Scripts/Building/Walls/WallPathPresenter.cs`: module preview
  / instantiation, presently a wall-specific presenter.
- `docs/architecture/curved-modular-walls.md`: existing Smooth / Sharp spline
  authoring rules and multiplayer state.
- `docs/architecture/rivers-roads-bridges.md`: roads own authoritative
  centerlines independently of future road renderer.

IMPORTANT: The existing ecological `BiomeKind` is not synonymous with the proposed
faction/world-theme categories: summer-human, woodland-dwellers, elven, snowy-mountain.
Do not rename or reinterpret existing values, silently change save data, or make the
highlands always snow. Introduce a distinct visual/environment theme layer.

## Core distinction: terrain ecology, authored biome and magic conversion

Maintain three separate inputs:

1. **Ecological base**: generated `BiomeKind`, slope, elevation, moisture, temperature,
   grass density and `SurfaceKind`; existing behavior remains intact.
2. **Environment theme overlay**: designer-painted or procedurally authored faction
   theme field with feathered boundaries. Examples: summer kingdom, enchanted forest,
   elf woodland, snowy mountains. Multiple influences may coexist at boundaries.
3. **Runtime conversion overlay**: gameplay-driven changes (e.g. necromantic corruption)
   with source, ownership, intensity, timestamp/revision and decay/priority rules.

Conceptual resolution per world position:
```
EcologySample + AuthoringThemeSample + RuntimeConversionSample
                |
                v
EnvironmentSample { dominantTheme, weightedThemes[], materialEffects[],
                    growthRules, appearanceTags, revision }
```

Do not store one hard `summer/winter` boolean per instance. Instead expose e.g.
`winterWeight` in [0,1] and `themeWeight` for blending; at a border a point/section
can be partially snowy. Stable world-space sampling prevents seams between chunks.

Retain author/designer override precedence and define it visibly:
- Physical gameplay classification (blocking, ownership, road ID) is independent
  from appearance.
- Designer-locked objects retain identity and cannot be deleted just because the
  terrain theme changes.
- Player-built paths/fences/walls adapt to local appearance without converting
  their gameplay identities.
- Conversion priorities (e.g. enemy spells) are explicit in the runtime overlay and
  controlled by server authority, not by random render code.

## Unified visual asset mapping

Use a data-driven `EnvironmentAppearanceProfile` or equivalent ScriptableObject:
```
themeId
surfacePalette / terrain material profile
vegetationPalette / allowed ecology / density & slope masks
appearanceVariantsByArchetype:
    logicalArchetypeId: "small_wood_fence_a", "stone_wall_a", "dirt_path_a", ...
    summer variant / winter variant / enchanted variant / corrupted variant
    supported shader features (snow mask, moss, wetness...)
    geometric compatibility / socket metadata
```

Source of truth is a **logical archetype/definition ID**, not a prefab filename.
Place only the logical archetype in persistent state; resolve its local visual
variant separately. A user's selected summer road is a selection of ROAD TYPE,
not a permanent requirement to use a green-textured material. On snowy ground
the same road automatically receives a compatible snowy appearance.

Fallback policy:
- If a theme variation is missing, display base variation with compatible
  material overlay (e.g. snow overlay).
- If no appearance fits a special case, preserve the existing asset and issue a
  developer-only warning; do not prohibit ordinary roads crossing theme edges.
- Never silently change a wall's health, a fence's collision, road travel speed,
  or ownership just because its model changes.

**Preferred transitions:** unchanged base geometry + URP world-space snow / moss /
corruption masks, with a smooth visual blend (best for continuous fences/walls).
If alternate geometry is necessary, assets MUST expose compatible end sockets,
height, width, module spacing and clear transition endpieces. Switch visuals per
section with transitional modules as needed. Avoid hard prefab changes exactly
at the boundary, where one section is green and the adjacent section suddenly
fully snow-covered.

Do not rely on MeshCollider vertex welding or indiscriminate welding of adjoining
modules. Joining by shared silhouette/contact faces, sensible small overlap,
corner/endcap pieces or targeted procedural strips is normally cheaper and safer.

## Reusable spline path foundation

Reuse/refactor the tested sampling logic of `WallPathLayoutUtility` rather than
copying it into an unrelated fence system. Keep existing wall APIs and path IDs
working; extract shared pure curve sampling only after regression tests pass.

Authoritative spline data:
- stable path ID, logical definition ID, owner, control points and Smooth/Sharp
  modes; closed/open; connection sockets/features when applicable;
- store compact path intent once, not transforms or terrain-dependent prefabs;
- derive section poses and appearance locally and deterministically.

Presentation per path type:
- **Walls:** existing rigid modules, special towers, gates, damage and sockets.
- **Fences:** cheap short rigid modules, optional end/corner posts, selectable
  asset styles; no gameplay wall towers by default.
- **Roads/paths:** centerline + width/profile, continuous terrain material
  painting or strip mesh; don't force prefab-per-meter for normal dirt roads.
  Mesh where needed, texture/surface layering where sufficient.

Fence authoring contract:
- local +Z = length, pivot bottom-center, unit scale, aligned reusable connectors;
- keep module dimensions consistent across summer and snowy variants;
- start with a short 1.5-2 m test piece; a *target*, not an enforced budget,
  is ~80-200 triangles at near LOD depending on silhouette, lower LOD farther out;
- validate bends by module spacing, tangent rotation, sloped ground and sharp
  corners. For sharp corners use authored post / corner modules where necessary.
- keep simple primitive colliders or combined collision instead of complex
  per-board MeshColliders.
- GPU-instancing/shared materials and section culling for long runs.

For per-section theme sampling, sample at midpoint AND ends/footprint when the
segment overlaps a border. Keep path structure intact. A 10 m road crossing
summer -> snow changes material continuously in world space rather than
becoming two unrelated paths.

## Biome painting and recomputation

A biome brush affects a feathered world-space influence field. Changing a summer
region to snow should update, in order, the **affected area only**:
1. Theme field and its persistence/revision (including chunk-neighbor margin).
2. Terrain-layer/material presentation and snow visual mask.
3. Cover density / species palette / tree & grass eligibility.
4. Existing path, wall, fence, building and prop **appearances**.
5. Supporting decor (e.g. snow around footprints or structures).

Object preservation rules:
- Player-built objects never disappear due to a cosmetic biome repaint.
- Decoration may be reseeded / rebalanced only where affected, using stable
  deterministic seeds and stable object IDs.
- Rebuilding one modified chunk must not regenerate unrelated terrain or break
  neighboring seams.
- For winter, snow can accumulate on an existing tree or a tree species can be
  swapped only when the design explicitly allows that prefab class and gameplay
  identity to change.
- Editor tooling should support Undo/Redo and a preview of biome conversion.
- A running multiplayer game uses server-owned terrain-conversion commands and
  serializable delta patches; clients rebuild visuals from revisions.

In the future spells/structures may emit moving influence:
```
BiomeConversionSource
  -> authoritative influenced cells / raster or sparse patches
  -> Environment resolver
  -> dirty rectangles / changed chunk revisions
  -> async/limited-budget local visual rebuild
```
Allow competing influences and gradual advancement; cap propagation work per
frame. Treat 'undead corruption' as a new theme/effect overlay, not as rewriting
the basic generator's ecological moisture/temperature outputs.

## Flower fixes: the first executable task

Because the latest brush code may be on a development branch rather than
`main`, locate the actual active brush renderer/scatter implementation before
editing. Avoid changing generated-world `GroundCoverStage` unless it is
actually the producer of those flower instances.

Suspected classes of bug to diagnose separately:
- prefab pivot not at the stem contact point; mixed source-model axes;
- random full 3D rotation rather than yaw-only; local/up vector confusion;
- terrain normal too steep or erroneous sampling around mesh borders;
- world height sampled at center rather than final jittered position;
- scene object scaled/parented with rotation or negative scale;
- floating flowers due to stem geometry not reaching the prefab origin.

Desired placement algorithm:
1. Determine deterministic candidate XZ position and species from brush density
   field, exclusion masks and world seed.
2. Sample terrain height AND normal at final XZ; ground-contact anchor agrees
   with prefab pivot/metadata.
3. Base orientation is mostly world-up; add controlled, small slope alignment
   only where allowed (blend world-up toward sampled normal; clamp tilt).
4. Randomize yaw freely, use small optional artistic lean but avoid
   unrestricted pitch/roll. Normalize model source rotations in prefab.
5. Randomize modest uniform scale and tint per clump; never random negative
   scales. Optional wind motion is separate from static alignment.
6. Mask steep slopes, paths, water and building pads appropriately.
7. Maintain deterministic randomness, correct bounds and no visible floating
   or buried stems when tiles/chunks regenerate.

Suggested initial artistic palette: 6-10 light summer meadow variants, including
small white flowers, yellow buttercups, light lilac flowers, a few taller sparse
wildflower stems and tiny low flower clusters. Keep frequency varied: many common
small blooms, fewer distinct tall flowers. Reuse atlas/shared materials and
instancing instead of one unique material per flower.

Acceptance tests:
- On flat grass: stems grounded, mostly upright, no random sideways flowers.
- On moderate slopes: stable lean and no floating / upside-down variants.
- On extreme slopes: no inappropriate flower spawn.
- Changing brush density and reloading same seed doesn't reshuffle unaffected
  areas or create duplicates.
- Paths/water/building exclusions work.
- Performance remains acceptable with multiple flower variants and URP shadows.

## Concrete staged execution

### Slice A — now: flowers
- Locate active brush, actual prefab imports, and existing transform logic.
- Add a placement-orientation helper / metadata where necessary.
- Unit/EditMode tests for height and tilt and at least one visual Unity scene test.
- Add meadow variety presets or prefab slots; do not wait for snow.

### Slice B — design + summer road foundation
- Write asset/definition contracts and versioned resolver interface.
- Add an appearance resolver returning a blend sample even if only summer exists.
- A current path is resolved from semantic road type and world-space theme.
- No winter art required yet; use a debug gradient across a mock boundary.
- Regression tests for wall path pose IDs and unchanged old layouts.

### Slice C — lightweight fence prototype
- One summer fence module + corners/endcaps, reusable path placer.
- 3m straight, 10m curved, sharp corner, slopes, join tests.
- Optional winter test material as placeholder; geometry compatibility test.

### Slice D — winter biome after summer finished
- Winter terrain/snow equivalent of summer cover painting, winter vegetation
  palette and themed material profiles.
- Paint a snow patch through existing path/wall/fence in a developer scene.
- Snow-weight blend, invalidation, stable results after save/reload.

### Slice E — future magical territory conversion
- Server-side biome field modification, spreading sources, conflict priority,
  network delta serialization and incremental rebuilds.
- Test two players watching conversion of the same fence/road sections.

## Non-goals / safeguards

- Do not replace all foliage because a "winter" flag changed at a single point.
- Do not mutate saved prefab identifiers for visual-only biome transitions.
- Do not fork four separate brush engines or fence systems.
- Do not make 3D models share/weld vertex buffers at runtime just to hide seams.
- Do not add new large runtime features during the ongoing water/light migration
  before the summer brush code and URP branch state are confirmed.
- No assumptions here that the current `main` branch contains the latest in-progress
  developer brushes or URP migration; verify their actual implementation branch.

## Open integration questions for the code implementation phase

- Which branch/commit currently implements the manual grass/meadow/flower brush?
- Does it emit GameObjects, Terrain Details, GPU instances or an indirect buffer?
- Which existing road painting tool is active outside the old macro-world road planner?
- Which repository was previously proposed as the external spline reference?
  The existing in-repo modular-wall plan is verified, but do not invent a
  previously shared external URL.

These are targeted source-code checks, not blockers to the architecture itself.
