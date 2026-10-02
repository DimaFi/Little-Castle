# Atmosphere / Stylized Rendering Review Handoff

Repository: `DimaFi/Little-Castle`  
Branch: `main`  
Unity: `6000.5.5f1`  
Rendering pipeline: Built-in Render Pipeline  
Baseline commit before this handoff: `23db212e5822efaede1778815c2339b21f027bbc`

## Goal

Review and refine the current Little Castle atmosphere/rendering stack toward a
soft, cozy, highly readable stylized medieval look.

The desired visual principles are:

- warm golden direct light at sunset;
- cool ambient/shadows for warm/cool contrast;
- readable blue/teal night;
- warm windows/lanterns at night;
- soft, non-plastic solid materials;
- foliage that visibly transmits/backlights in sun;
- subtle wind in foliage/grass/wheat;
- terrain that avoids visible wallpaper repetition;
- atmospheric depth without hiding gameplay;
- strategy-camera readability at both close and far zoom.

Do not attempt to duplicate proprietary assets or an exact implementation from
another game. Reproduce visual principles with Little Castle's own shaders,
materials and assets.

## Architecture that already exists

### Authoritative time

- `WorldClock`
- `WorldTimeSystem`
- `WorldTimeSettings`

Night is shorter in real time but remains a continuous full game-time segment.

### Atmosphere presentation

- `DayNightLightingController`
- procedural skybox
- sun
- moon
- Trilight ambient
- linear fog

### Shared shader globals

- `StylizedLightingGlobals`

Canonical shader globals:

- `_LC_SunDirection`
- `_LC_SunColor`
- `_LC_MoonDirection`
- `_LC_MoonColor`
- `_LC_AmbientColor`
- `_LC_ShadowTint`
- `_LC_Daylight`
- `_LC_Twilight`
- `_LC_NightAmount`
- `_LC_Wind`
- `_LC_GameTime`

Do not introduce a second unrelated day/night shader-state system.

## Implemented shaders

### Solids

`Assets/_Game/Shaders/Surface/LC_StylizedLit.shader`

Current features:

- BaseColor;
- normal map;
- AO;
- wrapped/soft direct light;
- cool artistic shadow tint;
- sun and moon global lighting;
- restrained specular;
- subtle rim;
- fog;
- sun shadow reception;
- instancing variant;
- shadow caster;
- emission map;
- night-aware emission;
- subtle emission flicker.

### Foliage

`Assets/_Game/Shaders/Foliage/LC_Foliage.shader`

Current features:

- alpha cutout;
- two-sided rendering;
- normal map;
- AO;
- global wind;
- optional vertex-R wind mask;
- per-object tint variation;
- sun backlighting/transmission;
- warmer transmission at twilight;
- moon response;
- matching animated shadow caster.

### Grass

`Assets/_Game/Shaders/Foliage/LC_Grass.shader`

Current features:

- alpha-cutout clump/card rendering;
- root/middle/tip gradient;
- per-clump color variation;
- upward-biased stylized normals;
- global wind with stronger tip movement;
- warm backlighting;
- matching animated shadow caster.

### Terrain

`Assets/_Game/Shaders/Terrain/LC_Terrain.shader`

Current features:

- grass/dirt/rock layer colors/textures;
- world-space UVs;
- slope-driven rock;
- optional lowland dirt;
- procedural low-frequency macro color variation;
- optional vertex-mask contract:
  - R = path/dirt;
  - G = wetness;
- shared day/night lighting;
- fog;
- sun shadows.

The test scene already uses:

`Assets/_Game/Materials/Material_terrain/LC_Terrain_Default.mat`

instead of the previous debug terrain material.

## Night local-light infrastructure

Implemented:

- `NightLightEmitter`
- `NightLightBudgetManager`

Current test-scene defaults:

- max 24 realtime local lights;
- max manager distance 95 m;
- reevaluate every 0.2 real seconds;
- emitters can set individual priority/distance/night threshold;
- shadows are disabled on managed lights unless explicitly allowed.

Important:

The local-light budget infrastructure exists, but custom Little Castle shaders
do not yet have a finalized cheap policy for arbitrary nearby Point/Spot light
contribution.

Do not blindly add a ForwardAdd pass to every foliage/terrain material before
profiling. In Built-in Forward rendering that can multiply draw cost per light.

The review should decide among:

1. controlled ForwardAdd for selected material families;
2. projected/mesh light pools for ground response;
3. deferred-compatible strategy if justified;
4. another measured low-cost solution.

## Current test scene

`Assets/_Game/Scenes/WorldGenerationTest.unity`

Contains:

- WorldTimeSystem;
- DayNightLightingController;
- StylizedLightingGlobals;
- NightLightBudgetManager;
- Sun Light;
- Moon Light;
- strategy camera;
- stylized terrain material.

Use `Simulation Speed Multiplier` for rapid atmosphere review.

Suggested review values:

- 10 = full default day/night cycle in roughly 45 real seconds;
- 20 = roughly 22.5 real seconds.

Do visual captures at minimum near:

- 08:00;
- 12:00;
- 18:30;
- 19:30;
- 22:00;
- 02:00;
- 05:30.

## Known project note: color space

`ProjectSettings.asset` currently reports:

`m_ActiveColorSpace: 0`

Treat this as Gamma under the current Unity serialization convention.

Review whether Linear color space is desirable, but do not flip it without:

- screenshots before/after;
- material validation;
- texture import validation;
- lighting validation;
- performance/build validation.

This is a project-wide migration, not a small shader tweak.

## Sol 5.6 Medium task

Perform a technical and architectural review first.

Required checks:

1. Open the project with Unity 6000.5.5f1.
2. Let all shaders compile.
3. Record every compile warning/error.
4. Run EditMode tests.
5. Play `WorldGenerationTest`.
6. Verify day/night timing still works.
7. Verify sky/sun/moon/fog stay synchronized.
8. Verify shader-global timing/order.
9. Verify sun/moon direction signs.
10. Verify shadow macros in every custom shader.
11. Verify animated foliage/grass shadows match visible geometry.
12. Verify GPU instancing compatibility.
13. Inspect Gamma/Linear assumptions.
14. Inspect terrain macro variation for visible banding.
15. Inspect alpha cutoff/shadow behavior on foliage.
16. Inspect wind amplitude for unrealistic whole-card movement.
17. Inspect night emission flicker for spatial artifacts.
18. Review local-light architecture and propose a measured strategy.
19. Check material count / shader keyword growth.
20. Check that no gameplay logic depends on presentation shader state.

Output should separate:

- confirmed bug;
- probable bug;
- visual tuning suggestion;
- performance concern;
- architecture concern.

Do not rewrite working systems based only on preference.

## Sol 6 Medium task

After the 5.6 review findings are recorded:

1. fix confirmed shader/compiler/runtime issues;
2. improve visual tuning from actual screenshots;
3. profile before/after;
4. refine local-light contribution;
5. validate close camera view on production-quality house/tree/grass assets;
6. validate far strategy view;
7. tune sunset warm/cool contrast;
8. tune night readability;
9. reduce obvious repetition in terrain/grass;
10. preserve the existing `_LC_*` global contract unless a documented
    technical defect requires changing it.

## Files to read before changing rendering

Mandatory:

- `AGENTS.md`
- `docs/architecture/day-night-cycle.md`
- `docs/architecture/camera-and-atmosphere.md`
- `docs/architecture/stylized-rendering-roadmap.md`
- this file

## Non-goals for the review

Do not:

- migrate to HDRP just because it offers volumetrics;
- introduce a second world-time system;
- create one shader per individual object;
- create one GameObject per grass blade;
- create permanent realtime lights for every emissive window;
- make night realistically black;
- remove close camera zoom just to hide visual defects;
- replace authored texture detail with excessive fullscreen post-processing.

The desired result is a cheap, cohesive stylized renderer that leaves CPU/GPU
budget for large settlements, simulation, animation and multiplayer.
