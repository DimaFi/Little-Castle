# CODEX PROMPT 11 — Lightweight Snow, Accumulation & Winter Weather

**Little Castle / planning handoff / 2026-10-26**  
**Status:** future task, NOT IMPLEMENTED. Do **not** start until summer environment is visually finished and baseline architecture is validated.  
**Dependencies:** step 01 biome/surface-cover contracts; ground materials, foliage/wind, roads, building props; current approved URP/graphics integration.

## Creative target

Create a beautiful, gentle, soft, slightly stylized **Tiny Glade-like** winter environment, not photorealistic RDR2 deep snow. We want **snow falling naturally onto existing objects and gradually accumulating as a fairly thin visible coating**, without physically replacing all meshes or adding expensive snow-depth geometry.

A tree, roof, rock, fence, wall or path should remain the same persistent game object while its appearance gradually gains snow. Do not overwrite its health/owner/collision/path state. Biome influence is a climate/theme rule, whereas actual snow-cover amount depends separately on snowing, melting, time and shelter. Snow should visually fade in/out naturally at summer↔winter boundaries, including on a single continuous road or spline fence.

### 1. Conduct integration audit

- Read root `AGENTS.md`, the existing [biome roadmap](../biome-adaptive-environment-roadmap.md), [step 01 prompt](01-modular-environment-and-biomes.md), the real current render pipeline and render-path settings; check compatibility rather than assuming URP is already active everywhere.
- Read existing lighting, water, fog, foliage, shadows, grass/material shader contracts and graphics URP tasks (#76 et al). Reuse established global parameters and weather/lighting controllers where possible. Do not create duplicate weather engines.
- Identify production materials (ground, Lit, foliage, stones, modular walls/fences, roads) and the safe shader or Shader Graph extension points. Validate leaves, GPU wind/ShadowCaster parity and instancing.

### 2. Required modules — separated, reusable and configurable

**SnowCoverageProvider**: world-space, chunk-aware accumulation mask or efficient parametric/sparse field. Supports event-driven updates, max thin coverage, accumulation and melting curves, persistence/revisions, outdoor vs sheltered surfaces (initial shelter via cheap explicit masks/volumes; only introduce complex occlusion after profiling). Target limited world-space data, not one Render Texture per object.

**SnowSurfaceOverlay**: URP compatible shader/subgraph/material functions shared by terrain, props, roads, fences, walls, trees. Blend snow using object/world normal (positive up), biome influence, noise edge breakup, coverage amount, exposure/occlusion, painter override. Optionally vary amounts for roofs, rocks, bark, leaves and vertical surfaces via authored masks; avoid uniform pure-white repaint. Small physically plausible roughness and soft highlights, low sparkle (not shiny/glittery). A thin shader cover is enough; **no mesh vertex displacement required**.

**SnowWeatherAdapter**: use existing environment/weather/light/fog control points; render graceful snowfall via particle/VFX budget near relevant cameras, localized volumes and wind. Change intensity per biome/region and avoid snow inside buildings as a later optional improvement. Weather precipitation != blanket biome override: a winter biome can have clear weather while existing snow remains.

**SnowFootprints (OPTIONAL, LAST)**: temporary soft footprint decals or inexpensive fade-out mask on nearby relevant ground only; player/animal/vehicle events if justified; max tracked events, pooling and distance-based quality; restore/fade under snowfall. No deep/volumetric deformation, tessellation, complex physics, or geometry updates.

Editor+runtime: shared logic with Inspector/ScriptableObject controls for palettes, accumulation rate, maximum coverage, melt rate, snow angle bias, softness/feather width, noise, wind/snow particle density, VFX quality, shelter mask, optional footprint lifespan. Visual previews and Undo/Redo for authoring, runtime events and save semantics.

### 3. What happens during biome conversion

The underlying authored summer ecology and placed objects persist. Applying a winter influence is not an immediate destructive replacement; snow coverage can accumulate gradually and naturally from weather or designer preview controls. At a blended border, road/fence/wall snow should change seamlessly in world space. If the runtime snowy biome spreads via future player magic, server-authored influence changes should eventually drive the same visual/material system with bounded chunk updates and reliable saves; do not implement magic mechanics in this step.

Ordinary trees should primarily receive a snow layer on leaves/branches (preserving their model and state). Replace foliage species only where an explicit biome vegetation policy allows. Grass may fade/reduce density or hide under thin coverage; do not delete every blade's logical source.

### 4. Free/open reference implementations to inspect before authoring

**A. Official Unity Shader Graph — Production Ready Shaders / Weather / Snow (PRIMARY).**
- Overview/docs: https://github.com/Unity-Technologies/Graphics/blob/master/Packages/com.unity.shadergraph/Documentation~/Shader-Graph-Sample-Production-Ready-Weather.md
- Unity guide to import: https://unity.com/blog/engine-platform/new-shader-graph-production-ready-shaders-in-unity-6
- In a compatible Unity version: Package Manager -> Shader Graph -> Samples -> Production Ready Shaders -> Import; see `Assets/Samples/Shader Graph/<version>/Production Ready Shaders/`.
- Includes a Snow subgraph designed to coat **top-facing object surfaces**, with color/smoothness/normal/etc. **Best starting point for thin snow on buildings and props**. Adapt to Little Castle's existing shared URP shaders, materials and artistic look rather than copying heavyweight graphs wholesale.
- Confirm exact sample availability for installed Shader Graph version and check applicable Unity package/content license and notices before redistributing/modifying. Package samples being importable does not imply a universal MIT license.

**B. ZGeng / DeepSnowFootprint (OPTIONAL FOOTPRINT RESEARCH, NOT PRIMARY SNOW SYSTEM).**
- Repo: https://github.com/ZGeng/DeepSnowFootprint
- License: MIT (retain copyright/license notices with copied substantial code).
- Legacy Unity 5.5.1f1, deferred pipeline and SM4 hull/domain/geometry shader approach; designed for **thick/deformable snow**, unlike our objective. Study only the idea of stamping trails and gradually refilling/erasing footprints. Do not import its deep deformation/tessellation as-is into URP.
- If temporary marks are requested, make a tiny original URP-friendly prototype using decals or limited Render Texture stamping and prove performance before shipping.

**C. mert-dev-acc / SnowShaderGraph (SECONDARY OPTIONAL).**
- Repo: https://github.com/mert-dev-acc/SnowShaderGraph
- MIT; URP/HDRP Shader Graph, with trail Render Texture and geometry displacement. Study footprint masks if needed, but do not take mesh subdivision/displacement into a thin snow production feature.

Do not directly import paid assets or third-party models/textures. For any external source: record license, exact version/commit, files used, attribution, modifications, compatibility and redistribution rules. Prefer original implementation informed by documented techniques and Unity samples. Third-party sources must never overwrite existing art assets/shaders.

### 5. Art direction and test scene

Aim for softly lit warm winter whites, gentle pastel-blue shadows, pleasant contrast, subdued sparkle and readable silhouettes. Aesthetic inspiration only, **no Tiny Glade proprietary asset/code copying**.

Build or reuse one safe deterministic test slice: summer meadow + tree + small house + stone + spline fence + road + bridge/river proximity if available. Demonstrate:
1. winter influence gradually expands across scene;
2. light snow falls, accumulating on roof/tree/stone/ground/road/fence;
3. snow remains after snowfall stops and melts separately;
4. continuous fence/road crosses biome boundary without hard prefab seams;
5. vertical walls receive little or no snow, overhangs and shelter behave plausibly (at least via controllable masks);
6. changing the overlay does not lose saved object identity or repopulate unrelated terrain.

Provide before/after at several camera distances and lighting configurations. Include controls for artists to tune without code modification.

### 6. Performance and quality policy

- **Do not** compute heavy deformation across the whole world. No snow collider mesh generation.
- Update only dirty chunks, with reasonable budgets; no full terrain refresh each frame or per-renderer `material` duplication.
- Validate URP batching/instancing/SRP Batcher interaction; don't promise universal compatibility without actual profiling. Preserve wind/ShadowCaster and LOD behavior.
- Profile representative near/far scenes and 2/8/16-player strategy loads if available; quality tiers should allow snowfall particle/footprints to be disabled separately from snow coating.
- Snow masks and shelter footprints: prefer cheap coarse fields and deterministic fixed-seed noise, don't allocate unbounded render textures.

### 7. Acceptance / completion gate

- [ ] Official Unity sample evaluated against real installed Unity/URP version.
- [ ] Third-party candidates/license/limitations recorded; chosen free foundations and exact provenance documented.
- [ ] Snow accumulation varies smoothly over time and differs from theme membership.
- [ ] Thin snow appears on **existing** building, terrain, vegetation, rocks, fences and roads; no wholesale mesh replacement.
- [ ] Clear artistic style, smooth boundaries, adjustable parameters in Inspector and runtime.
- [ ] No damage to existing fog, light, leaf wind, shadow passes, roads, saves or multiplayer identifiers.
- [ ] Performance evidence from real Editor/Player and before/after captures where available; if not executed mark NOT RUN.
- [ ] Optional short-lived visual footprints are deferred until coating and weather succeed. Deep deformable snow explicitly out of scope.
- [ ] Change log, tests, checkpoints and roadmap statuses updated; no direct merge to `main`.

## Priority reminder

Complete the **summer biome first**. This is a carefully designed **step 11**, not permission to implement winter before earlier environment work stabilizes. Any enabling biome/coverage contracts belong to step 01 but must remain minimal.
