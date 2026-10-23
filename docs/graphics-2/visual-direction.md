# Graphics 2.0 — Visual direction & prioritized image improvements

## Art thesis

**Little Castle is an inviting, readable living miniature medieval valley**, not photorealism and not a copy of Tiny Glade. At *strategy zoom*, color masses, silhouettes and readable paths matter; at *close zoom*, chipped plaster, hand-fashioned timber, soft rock stratification and confident textures reward inspection. No reliance on extreme bloom or high-frequency detail to imply quality.

### Five art pillars

1. **Sunlit softness with form:** stable key-directional sunlight, diffuse sky fill, warm–cool separation, tangible but soft shadow definition; consistent response across terrain/wood/stone/foliage.
2. **Handmade, not toy-plastic:** restrained gloss; believable wood grain and stone roughness; varied edges/proportions; artist-controlled irregular geometry instead of uniform noise everywhere.
3. **Grounding:** roads appear excavated/worn, stone foundations sit in land, rocky cliffs have soil skirts and debris, trunks connect to ground, bridge feet touch both banks. Contact shadows/AO support geometry, not replace it.
4. **Intentional vegetation:** near-camera clumped growth; meadow color variation; canopy silhouettes distinct across fir, deciduous, bush; movement slow and varied; no clutter blocking units/walls/pathfinding.
5. **Atmosphere with gameplay clarity:** depth through contrast/color/fog and distance silhouettes; readable day–dusk–night; local amber lights but preserved terrain/road/unit visibility.

## Improvement workstreams (relative priority)

### A. Light/color/look-dev — highest image leverage

- Develop 3 fixed time-of-day art targets: noon clear; sunset golden but not orange wash; blue-hour/night with warm windows, not pitch black.
- Calibrate Linear vs Gamma; compare shadows and color saturation in *Player* not Scene view. Avoid unbounded ambient, flat sun on full shadow, wrong sRGB scalar maps.
- Fix over-dark cracks/black SSAO halos by adjusting normals/occlusion radius and strength. Introduce restrained shadow tint based on light/ambient.
- Give buildings variation through assigned materials, small geometry wear, planned tonal accent hierarchy. Make roof/fire/lantern emissive visible but bloom subtle.
- Increase spatial legibility: ground brighter than cliff faces in noon? Test with grayscale readability and little thumbnail downscale.

### B. Rocks/cliffs/mountains

- Original modular cliff kit: 3 visually different face families + corners/tops/undercuts, dedicated road-side and riverbank pieces, underside skirt buried into terrain.
- Use slope masks and controlled tri-planar mapping on **steep cliffs only** if UV/stretching proves problematic; avoid blanket 3D triplanar terrain cost.
- Shared palette with terrain rock layers; overlay soil at upper edges, scattered rubble and clumped moss/tufts near contacts; larger structures have stable mid-frequency strata.
- Texture variation from coherent tileable BaseColor/Normal/AO/Roughness (same UV/source landmarks). Roughness scalar is data, no colorful unrelated generated images.
- Terrain silhouette/bank shape should be correct before adding AO/decals.

### C. Terrain + trails

- Meadow/soil/stone/path in 3–4 optimized blended types consistent with existing SurfaceKind. World-space macro variation reduces visible tiling, fine textures for near view. Mix by geological rules (slope, moisture, footprints), not uniform random splashes.
- Roads should have credible cross-section/soft edge, slightly packed middle, stone/dirt transitions, vegetation suppression near footpath; retain existing road/bridge gameplay masks.
- Avoid separate unique material for every ground patch. Keep chunk seams deterministic; use world positions, not per-chunk random offsets.
- Consider decals only for sparse footprints, mud and localized edge details, after profiler.

### D. Foliage / crops

- Form families: tall/young fir; wide rounded deciduous; low clustered bushes; meadow clusters; wheat sways, root stays planted.
- Author LODs for **real meshes** and validate trunk/leaf split/vertex R wind mask, UV0, normals, tangents if normal maps. Test shadows against animation.
- Wind: slow canopy sway, mid-frequency branch deformation, micro flutter constrained; screen-size attenuation near far zoom to prevent noise. Never distort trunk.
- Variation: stable per-spawn ID determines mesh archetype, rotation, tint and scale within art-controlled bounds. Avoid unnecessary unique materials/MPBs with GPU Resident Drawer.
- Strong repeated silhouettes must be mixed; don't hide bad assets behind scatter density.

### E. Water / shore / bridge

- Mild color transparency/flow and soft shoreline contrast. Boundary shallows visible, reeds/stones at bank and physically touching bridge piers.
- Keep existing river geometry/connectivity authoritative; renderer cannot move water level or bridges. Use cheap screen-space effects *only if* demonstrably required.
- Day/night specular depends on camera and light, not giant white water stripes; test transparent sorting/depth and reflections.

### F. Architecture / village storytelling

- Houses: fewer distinct coherent modules, tasteful overhangs, subtle stucco irregularity, well-defined beams; hero building silhouette clear at far zoom. Respect existing scale and wall segment contracts.
- Cluster props logically: storage by mill, chopped wood by lumber house, rubble by quarry, fields near farm. Need variation rather than excessive decorative objects.
- Night glows stay emissive at all distances, fewer budgeted realtime lamps. Distant window glow > many point lights.

### G. Finishing (only after A–F)

- Low-radius SSAO for contacts, some decals, very light bloom only for lamp highlights, subtle tone adjustments. Optional dynamic clouds/fog banks local art-directed and quality gated.
- TAA/MSAA/SMAA chosen after tests for foliage stability. No oversharpen, heavy DOF, motion blur, excessive chromatic aberration in strategic view.
- Optional APV on handcrafted static outposts after proving chunk/day-night changes; no assumed dynamic full-scene GI.
- Avoid SSR/real-time volumetric fog until a measurable artistic problem cannot be solved more cheaply.

## Golden slice acceptance scorecard (1..5 for each, owner observes)

1. House/wall/readable gate/road at high zoom-out.
2. Cliff/soil/rock/grass believable contact from close.
3. Sky / key light / shadow / window response at noon, dusk, night.
4. Plants/trees varied without noise, wind without shadows desync.
5. Ground material clarity (no tiling/shimmer/texture color clash).
6. Shore/bridge waterline continuity and object grounding.
7. Cohesive original identity; not imitation of a commercial asset pack.
8. Stable look while camera pans and scene streams in/out.

Record before/after screenshots and comments for each; prefer stable *artist-approved* scores, not claiming an objective "market best" quality metric.
