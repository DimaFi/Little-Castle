# Cozy Cottage — isolated Tiny Glade-inspired test

**Status: experimental, editor-only authoring, unverified in Unity.** This is NOT the game's building system.

## Why this branch exists

Test the central idea on one stylized medieval cottage before connecting it to Little Castle:
- resizing the footprint regenerates walls rather than scaling the same geometry;
- roof pitch, overhang and row count are regenerated;
- windows are editable per wall and cut physical rectangular openings through plaster;
- a linked straight wall can switch from a solid stone wall to a wooden fence by height;
- generated materials/meshes do not require paid assets or modify existing prefabs.

The branch was created from `main`, not from in-progress local/Codex checkpoint branches. Never merge blindly into a different checkpoint; port the isolated folder only after review.

## Quick start in Unity 6000.5 / URP

1. Commit/stash any local changes and check out **`experiment/procedural-cozy-cottage-20261102`** in a separate worktree or clean clone.
2. Open any temporary test scene. Do **not** modify the private map or save production scenes for this check.
3. Menu **GameObject > Little Castle > Prototype > Cozy Cottage**.
4. Select **Cozy Cottage - Procedural Study**, frame it in Scene View.
5. Drag colored Scene handles:
   - **cyan** — house width/depth;
   - **yellow** — wall height;
   - **orange** — roof height/eave overhang;
   - **pink** — move each window horizontally and vertically within its facade;
   - **green** — connected wall/fence extension length and height.
6. Use the Inspector to change window width/height, add/remove windows, edit the roof's gable overhang, and switch the extension's `Extension Style`.
7. With `Auto By Height` selected, set **Extension Height** below 1.55 for a fence and above 1.55 for a stone wall.
8. Optionally assign existing licensed/shared **URP-compatible** stone, roof, plaster, wood and foliage materials. Without those, the tool uses generated flat-color fallback URP/Lit materials.
9. Check Console for errors, stretch repeatedly and undo/redo. Do not judge final lighting or shaders using the temporary palette.

## What was intentionally NOT changed

- `Assets/_Game/Scripts/Building/Walls/` and all defensive-wall systems;
- existing houses, prefabs, world generation or map editor;
- saves, multiplayer replication, economy, build costs or gameplay colliders;
- sky, rain, snow, fog, lighting, weather or biome systems.

## Known limitations

- This is an **Editor Scene View authoring prototype**. It has no player-facing in-game drag controls yet.
- Only a symmetric rectangular footprint, pitched gable roof and one straight fence/wall extension.
- No roof dormers, curved fences, smart path/door interactions, automatic terrain adaptation or structural intersections.
- Simple low-cost geometry and preview materials. It is a mechanics/art-direction starting point, **not visually pixel-equivalent to Tiny Glade**.
- Window overlap/door overlap is intentionally rejected by skipping invalid openings. Move windows apart.
- Physics collision, navmesh baking, wall health, destructibility, save serialization and network state are not integrated.
- Mesh rebuild is acceptable for one art test; future mass placement needs throttled dirty-region updates, baking, LOD and profiling.

## Acceptance checklist for the first art test

- [ ] Unity imports scripts with **zero compile errors**.
- [ ] Menu creates a visible cottage in a temporary URP-lit scene.
- [ ] Dragging house width/depth keeps stable wall proportions.
- [ ] Changing roof rise/overhang regenerates shingle rows without obvious gaps.
- [ ] Window handles move rectangular cut-outs; changing sizes changes holes.
- [ ] Windows can't silently destroy the central doorway.
- [ ] Fence converts to stone wall at the specified height and both extend without detached posts.
- [ ] Repeated edits and Undo/Redo do not leak scene objects or meshes.
- [ ] Profiling confirms acceptable rebuild latency for a single preview building.
- [ ] Compare screenshots under the **same** Little Castle camera/lighting as the intended target.

## Next pass for production

1. Replace fallback palette with authored reusable project materials following `docs/architecture/material-texture-contract.md`.
2. Improve shingles (wavy silhouette, mortar, texture variation, worn corners), asymmetrical facades, roof intersections, doors/windows trims, shutters and climbing foliage.
3. Add freehand wall/fence control points, corner/gate/junction grammar, smart roof intersections, and terrain adaptation.
4. Separate pure serializable building state from procedural mesh generation; cache and rebuild only dirty surfaces.
5. Provide runtime input and gizmos **through a new adapter** after reviewing existing wall/map input, not by overwriting their controllers.
6. Integrate rain/snow masks and lighting only after validating coexistence with current render pipeline.
7. Add EditMode geometry tests, play tests, profiling, and deterministic save/network support before integrating gameplay.

No third-party art, code or proprietary Tiny Glade assets were copied.
