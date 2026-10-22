# Innerverse packs for Little Castle — research and Codex handoff (2026-10-22)

## Scope and evidence

These five Unity Asset Store products are published by Innerverse Interactive:
- Free Ultimate Nature – Starter, id 176906: https://assetstore.unity.com/packages/3d/environments/landscapes/ultimate-nature-starter-176906
- Paid Ultimate Nature, id 311492: https://assetstore.unity.com/packages/3d/environments/ultimate-nature-311492
- Ultimate Survival – Essentials, id 193143: https://assetstore.unity.com/packages/3d/environments/ultimate-survival-essentials-193143
- Ultimate Medieval Builder, id 155572: https://assetstore.unity.com/packages/3d/environments/ultimate-medieval-builder-155572
- Ultimate Wild West Builder, id 288884: https://assetstore.unity.com/packages/3d/environments/ultimate-wild-west-builder-288884

Public store descriptions & itemized model inventories:
- https://www.gameassetdeals.com/asset/176906/ultimate-nature-starter
- https://www.gameassetdeals.com/asset/311492/ultimate-nature
- https://www.gameassetdeals.com/asset/193143/ultimate-survival-essentials
- https://www.gameassetdeals.com/asset/155572/ultimate-medieval-builder
- https://www.gameassetdeals.com/asset/288884/ultimate-wild-west-builder

**Verification limit:** The actual user's locally imported Starter package and live Unity Editor were NOT accessed in this investigation. The GitHub repository does not document this import. Screenshots are publisher marketing samples, not proof of current production geometry, shader behavior, or FPS.

## Store inventory highlights (do not assume same naming in imported package)

| Package | Stated number | Highest Little Castle relevance |
| --- | ---: | --- |
| Starter (free) | 27 | 2 trees, grass, flower, bush, stump, branch, log, mushroom, 5 rocks, 5 cliffs, 5 tiny rocks, mountain, 2 waters |
| Nature ($9.99) | 89 | 5 fir, 5 birch, 5 dead birch, 1 broadleaf + 5 dead broadleaf, 5 grass, 6 plants, 3 flowers, 2 bushes, 2 flowering bushes, 2 dead bushes, 5 decals, 5 cards, 3 large rocks, 3 cliffs, 3 small rocks, mountain, bridge, branches/logs |
| Survival ($14.99) | 215 | wooden bridge, shelter, spikes, campfire, furnace, anvil, workbench, wooden boat, foliage, hills/roads; lots of contemporary gear not appropriate for Little Castle |
| Medieval ($9.99) | 374 | 21 castle modules, 18 buildings, 190 props, 62 foliage, 21 stone, 29 terrain, 6 water; wall/tower/bridge/market village inspiration |
| Wild West ($9.99) | 482 | 177 modular building pieces (40 roofs), 20 rocks, 67 terrains (20 cliffs, 21 hills, 6 roads), 164 props incl sacks/ropes/crates, 7 vehicles; western facade styling usually unsuitable |

Prices observed 2026-10-22 and subject to change. Store compatibility badges for some paid packs mention Built-in whereas recent descriptions say Unity 6 URP; **verify actual version before import**.

## Key repo compatibility gate — do first, not optional

Repository currently pins Unity 6000.5.5f1 in `ProjectSettings/ProjectVersion.txt`. As of the accessed main branch:
- `ProjectSettings/GraphicsSettings.asset` uses `m_CustomRenderPipeline: {fileID: 0}`.
- `Packages/manifest.json` has no URP dependency.
- `docs/architecture/stylized-rendering-roadmap.md` mandates Built-in until measured architecture decision.
- Starter now advertises Unity 6+, **URP-only**, with an included UNS_URP renderer asset.
- Live user's machine may have uncommitted migration. Query its CURRENT state, do not silently migrate.

First compare A/B in disposable test scene/project if necessary. If remaining Built-in, prefer remapping prefabs to `LC_StylizedLit`, `LC_Foliage`, `LC_Grass`, `LC_Terrain`, `LC_DistantSimple` and correctly imported source textures; avoid importing URP-only shaders wholesale. Evaluate whether render pipeline migration is worth it only through ADR and evidence.

Must read `AGENTS.md`, `docs/architecture/foliage-wind-authoring.md`, `docs/architecture/material-texture-contract.md`, `docs/architecture/lod-and-production-assets.md`, `docs/workflows/MODEL_TO_WORLD_PIPELINE.md`.

## First real hands-on asset audit (Codex on user's machine)

1. Find exact imported package folder via AssetDatabase / Package Manager; record version, root and GUID; do not move/delete its content.
2. Audit Meshes, models/prefabs, LODGroups, shader names, missing scripts/material references, material dependencies, textures and compression, SRP compatibility; determine separately: package **installed**, package **renderable**, package **performance-approved**.
3. Enumerate 5 standard rocks, 5 rock cliffs, 5 tiny rocks, mountain, trees, grass, bush and water. Compare actual filenames. Screen-capture isolated 3/4 view with grid/scale and regular Little Castle game-camera view.
4. Check imported material textures against `material-texture-contract.md`; DO NOT claim every terrain texture exists, because Starter inventory does not list independent terrain splat texture count.
5. Selected tree + grass: run repo's actual `Little Castle/Rendering/Foliage/Validate Selected Model` (or relevant menu) and test moving shadows, UV0, vertex color R masks / trunk separation, bounds, foliage normals; do not rewrite LC_Foliage blindly.
6. Measure mesh vertices/triangles, material/submesh counts, draw calls, batches, GPU/CPU frame time, overdraw, terrain blending, shadow cost at near/far zoom. Test 2x2 representative chunks under at least two quality settings, day/night if time.
7. Write inventory TSV/CSV + screenshots and diagnosis `Starter-integrated/Starter-needs-remap/Starter-unsuitable` (to local ignored audit location, not GitHub with paid raw assets).
8. **No assets or screenshots of protected package source in public git history.** User's main repo is public. Source Unity Asset Store files are not redistributable merely because downloaded free; use ignored local storage or private licensed collaboration. Cite the Unity EULA: https://assetstore.unity.com/browse/eula-faq.

## Single vertical slice to implement first

**Environment Slice A: a 40–60m section** of game-playable terrain with road, slope, approach to cliff, trees, bushes, grass, small rocks (actual dimensions based on world settings). Make it scenic from close AND strategy camera.
- 1 terrain material with grass/dirt/rock regions and macro noise; gentle palette consistent with existing game's houses.
- 2–3 independently recognizable cliff silhouette archetypes; reuse free source (licensed) and author new original complementary shapes.
- 3 rock sizes, 3 color/shape grass variants (starter source where applicable), 2 tree sizes, 1 bush, roadside flower patches.
- Natural no-spawn masks on roads, rivers, building pads, paths and at controlled slope threshold; no vegetation floating out of slopes.
- Seed-stable instancing/variation: hash existing world object ID, not `UnityEngine.Random`. Keep core world data/presentation separate. Reuse `WorldSpawnCatalog` and `Ready/<Category>/<ArchetypeId>/` contract.
- Visible rock/ground contact gradients: allow partial burial; rocks' moss tint should match terrain. Avoid excessive repetition and toy-bright colors.
- LOD0/1/2, very far silhouette or cull tiers; minimize shadow casting on distant/detail grass.
- A/B compare under same sunlight: original package demo look versus Little Castle palette, count draw calls and frame timings; **do not report benchmark numbers before testing**.

## Original Blender library to replace paid packs

Design **original genre-consistent**, not traced/identical paid meshes. Procedural variation in Blender geometry nodes / Python is suitable, but quality still requires manual visual acceptance.

P0 (now):
- `LC_Rock_Rounded_01-05`: boulders softened with sculpt/proportional edits, 3 sizes each.
- `LC_Cliff_Modular_A/B/C`: stand-alone, face and corner fragments, pivot/base contact, clean normals, LOD and 0.5–1m underground skirt.
- `LC_Terrain_Ground_Dirt_Rock`: materials / authored source textures integrated into current terrain shader.
- `LC_Grass_Short/Medium/Tall/Dry`, `LC_Bush_Broad/Small`: cards or meshes with wind masks and controlled overdraw.
- `LC_Tree_Fir_Young/Mature`, `LC_Tree_Broadleaf_A/B`: shared tree authoring, trunk separated from canopy or vertex mask.

P1 (when slice A approved):
- Waterbank transition stones, riverbed pebbles, reeds, fallen trees, stones with moss, paths with pathsides.
- Bridges 3 silhouettes, mine-entry rock+timber kit, mossy limestone cliff; cliff parts used in neutral town approaches.
- Medieval modular stone wall straight segment/tower/gate made to `docs/architecture/curved-modular-walls.md`, no curve baked into basic wall section.
- Wooden props: barrels x3, crates x3, sacks x2, boards/log piles, lamp, fences, carts. Use existing project textures, no new material per item.

P2 (when gameplay needs):
- Winter/spring variation, river/coast overlays, background forest clusters, mountain silhouettes with lower detail.
- Survival: primitive camp, watchtower, firewood/forges. Do not create guns or modern survival gear.
- Wild West: original cliff/slope shapes, roof modules and weathered timber; no saloon or western facades.

## Acceptance and report

- Report exact installed package root/version and whether URP/Built-in issue reproduced.
- 10+ relevant asset screenshots of **user's Unity scene**, with 3/4 and gameplay zoom, material/mesh metrics.
- New vertical slice near cliff, road and grass; day/night, 2 camera zooms; shader/material errors = 0.
- Stays deterministic after chunk unload/reload and saved object deltas.
- LOD costs/performance measured (not asserted), no changes to world-state APIs without ADR.
- Original Blender work named distinct from publisher assets and stored in project-owned Ready workflow.
- Public GitHub only contains user-original content and documentation, not licensed source binaries.

## Selected publisher screenshot pointers (visual only, not package content)
- Starter forest: https://assetstorev1-prd-cdn.unity3d.com/package-screenshot/b0f1501f-8d3a-4aa6-a717-6f98ad527a50_scaled.jpg
- Starter grass: https://assetstorev1-prd-cdn.unity3d.com/package-screenshot/940b5e71-de72-4eca-862c-7652dfefa9c2_scaled.jpg
- Starter cliff: https://assetstorev1-prd-cdn.unity3d.com/package-screenshot/58ecea06-8a15-4a99-a121-11c25aedbbe7_scaled.jpg
- Starter mountain: https://assetstorev1-prd-cdn.unity3d.com/package-screenshot/7c9a82da-45bc-4a32-a0d8-4c97bf8180a9_scaled.jpg
- Paid Nature sample rocks: https://assetstorev1-prd-cdn.unity3d.com/package-screenshot/0e25fde9-2c54-4cca-995b-09dddd6b7f73_scaled.jpg
- Medieval towers: https://assetstorev1-prd-cdn.unity3d.com/package-screenshot/4e4be853-739a-4804-b31e-da27f59092dd_scaled.jpg

Be aware several paid Nature screenshots visibly bear 'Ultimate Nature PRO' even though listing is the 89-model Ultimate Nature: crosscheck actual package/retailer imagery before attributing a displayed model to this paid SKU.
