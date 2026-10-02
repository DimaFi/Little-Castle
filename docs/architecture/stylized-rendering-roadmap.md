# Stylized Rendering Roadmap

Status: active implementation  
Target: soft, readable, cozy medieval strategy presentation inspired by the
visual principles visible in games such as Tiny Glade, without copying its
assets or requiring its rendering implementation.

This document is the canonical rendering handoff for future coding agents.

## Rendering baseline

Little Castle currently uses Unity Built-in Render Pipeline.

Do not migrate the project to URP or HDRP only to improve atmosphere.
A render-pipeline migration is a separate architectural decision and must have
measured benefits that justify shader/material conversion cost.

Current presentation foundation:

- continuous authoritative day/night clock;
- procedural day/night skybox;
- directional sun;
- weak directional moon;
- Trilight ambient;
- distance fog;
- ground-focused strategy camera;
- shared Little Castle shader globals.

## Shared shader contract

All custom Little Castle shaders should consume the same global presentation
state where relevant.

Current globals:

- `_LC_SunDirection.xyz`
  - normalized direction from a surface toward the sun;
- `_LC_SunColor.rgb`
  - current sun color multiplied by current sun intensity;
- `_LC_MoonDirection.xyz`
  - normalized direction from a surface toward the moon;
- `_LC_MoonColor.rgb`
  - current moon color multiplied by current moon intensity;
- `_LC_AmbientColor.rgb`
  - weighted shared ambient color derived from Unity Trilight ambient;
- `_LC_ShadowTint.rgb`
  - artistic cool/warm shadow palette;
- `_LC_Daylight`
  - 0..1 daylight factor;
- `_LC_Twilight`
  - 0..1 dawn/dusk factor;
- `_LC_NightAmount`
  - 0..1 night presentation factor;
- `_LC_Wind.xy`
  - normalized horizontal wind direction;
- `_LC_Wind.z`
  - global wind strength;
- `_LC_Wind.w`
  - global wind speed;
- `_LC_GameTime.x`
  - normalized hour of day;
- `_LC_GameTime.y`
  - game day index;
- `_LC_GameTime.z`
  - unscaled presentation time in seconds.

Producer:

`Assets/_Game/Scripts/World/Rendering/StylizedLightingGlobals.cs`

The controller is presentation-only. Gameplay code must not read shader globals
as authoritative state.

## Phase 0 - Clock, camera and atmosphere foundation

Status: DONE

Implemented:

- variable-rate continuous world clock;
- day/night never pauses simulation;
- procedural sky;
- sun/moon presentation;
- readable night ambient;
- twilight and atmospheric fog;
- strategy camera with downward pitch limits;
- camera-driven streaming focus.

## Phase 1 - Shared stylized lighting and solid materials

Status: DONE - first production foundation

Files:

- `StylizedLightingGlobals.cs`
- `LC_StylizedLit.shader`
- `LC_StylizedLit_Default.mat`

Shader target:

- houses;
- stone walls;
- wooden structures;
- carts;
- mill pieces;
- barrels;
- bridges;
- props;
- other opaque authored assets.

Current features:

- BaseColor texture and tint;
- tangent-space normal map;
- AO map;
- Roughness maps are supported with a scalar fallback;
- emission map;
- wrapped diffuse;
- soft light/shadow transition;
- artistic cool shadow tint;
- shared ambient color;
- sun lighting;
- moon lighting;
- soft restrained specular;
- subtle rim contribution;
- sun shadow reception;
- fog;
- GPU instancing variant;
- custom shadow caster.

Important current limitation:

The Phase 1 base pass is intentionally focused on global sun/moon/ambient
lighting. General nearby point/spot-light contribution is not yet part of the
custom material contract. Local night lights are addressed in Phase 4.

Do not solve that limitation by replacing this shader with Standard or by
adding an expensive per-object lighting manager.

## Phase 2 - Foliage and grass

Status: DONE - shader foundation

Implemented:

- `LC_Foliage.shader`
- `LC_Grass.shader`
- `LC_Foliage_Default.mat`
- `LC_Grass_Default.mat`

Implemented foliage motion now explicitly supports round/clustered leaf geometry:

- Crown Sway + Vertex Wave + Leaf Flutter;
- optional automatic height mask;
- optional Vertex Color R wind mask;
- separate Opacity maps as well as BaseColor alpha;
- visible and shadow passes share the same deformation.

Foliage requirements:

- alpha-cutout support;
- double-sided normals / deliberate back-face lighting;
- fake transmission / backlight when the sun is behind leaves;
- warm golden transmission around sunset;
- shared wind from `_LC_Wind`;
- per-instance color variation;
- AO-compatible branch/leaf darkening;
- readable moon/night response;
- GPU instancing;
- distance-aware simplification.

Grass requirements:

- low-cost clump/card geometry;
- darker root and lighter tip response;
- shared wind;
- small per-instance height/color variation;
- backlighting;
- fade/simplification before grass becomes visually noisy in the distance;
- no per-blade GameObjects.

Wheat should use the same family of foliage/grass wind and backlight logic
instead of introducing a separate unrelated lighting system.

## Phase 3 - Terrain

Status: DONE - first terrain shader foundation

Implemented:

- `LC_Terrain.shader`
- `Assets/_Game/Materials/Material_terrain/LC_Terrain_Default.mat`
- test scene now uses the stylized terrain material instead of WorldTerrainDebug

Inputs should reuse materials in the project's terrain material organization.

Primary goals:

- large-scale color/macro variation so terrain does not repeat like wallpaper;
- slope-aware material response;
- height-aware variation only where artistically useful;
- road/dirt blending hooks;
- wetness hooks for future water/weather;
- shared day/night palette;
- soft terrain normal response;
- low-frequency noise should be mathematical or use a tiny reusable mask,
  not many large unique textures.

Avoid overbuilding a general landscape system before actual gameplay terrain
needs require it.

## Phase 4 - Night emissive and local lights

Status: IN PROGRESS - infrastructure implemented, content hookup pending

Implemented infrastructure:

- `LC_StylizedLit.shader` emission responds to `_LC_NightAmount`;
- `LC_StylizedLit.shader` has a controlled ForwardAdd path for nearby
  budgeted Point/Spot lights on solid objects;
- `LC_NightLightPool.shader` provides a cheap additive ground-light pool so
  terrain does not need one expensive realtime lighting pass per lantern;
- day/night emission strengths are material parameters;
- subtle spatially de-synchronized flicker is available;
- `NightLightEmitter` registers local realtime lights;
- `NightLightBudgetManager` activates only a nearby priority-sorted subset;
- default manager budget in the test scene is 24 realtime lights within 95 m.

Still pending:

- production window/lantern/fire materials and prefabs;
- deciding how custom Little Castle materials should receive nearby Point/Spot
  contribution without creating an excessive ForwardAdd cost;
- profiling the local-light budget with an actual settlement.

Desired visual relationship:

- night environment remains blue/cool/readable;
- windows and lanterns are warm;
- emissive remains visible without every window owning a permanent realtime
  Point Light.

The shader should provide the glow appearance; realtime lights are reserved for
the small subset that materially affects nearby geometry.

## Phase 5 - Depth, grounding and finishing

Status: PLANNED / OPTIONAL AFTER PROFILING

Candidates:

- subtle contact AO / screen-space grounding;
- improved depth fog / aerial perspective;
- very light color grading;
- cheap firefly particles at night;
- camera-near depth-of-field only if it does not harm strategy readability;
- material-specific edge/rim tuning;
- weather-driven sky/wind/fog profiles.

Do not add effects simply because they exist. Every fullscreen effect requires
a measurable visual benefit and a performance check.

## Art-direction rules

The target is soft stylization, not photorealism.

Day:

- cream/neutral sunlight;
- slightly cool ambient;
- readable saturated materials;
- soft contact shadows.

Sunset:

- warm direct light;
- cool ambient/shadows;
- foliage backlight becomes golden;
- do not tint the entire world orange.

Night:

- cool blue/teal world;
- enough ambient visibility for building/management;
- warm windows/lanterns create contrast;
- avoid near-black gameplay areas.

## Material migration rule

Do not automatically convert every existing material at once.

When a model becomes production-ready:

1. verify texture naming and map type;
2. prefer an existing Little Castle shared shader;
3. create/reuse a material instance;
4. keep material count low;
5. enable GPU instancing where compatible;
6. add the asset/material to the Asset Book/registry;
7. profile before adding shader features unique to that asset.

A new object category should not receive a new shader if an existing shared
shader can express it with material parameters.

## Performance rules

- one shared global atmosphere update is preferred over per-renderer scripts;
- do not call `renderer.material` every frame;
- prefer shader globals and MaterialPropertyBlock for per-instance variation;
- preserve GPU instancing;
- grass/foliage must not be one GameObject per blade/leaf;
- keep shader keyword counts controlled;
- avoid unique 4K masks where procedural/packed data can solve the same job;
- local realtime lights must be budgeted;
- screen-space effects come after profiling.

## Current color-space note

ProjectSettings currently report `m_ActiveColorSpace: 0` (Gamma in Unity's
current serialization convention).

Do not switch the project to Linear casually. The Sol 5.6 review should assess
whether Linear color space would materially improve the stylized lighting and
whether existing authored textures/materials are ready for that change.

A color-space migration affects the entire project and should be a deliberate
visual validation step, not an incidental shader edit.

Gamma vs Linear must be an A/B visual test. Linear is a plausible improvement
candidate for light blending, gradients, sunset response and local lights, but
it is not pre-approved. Keep Gamma unless screenshots/material validation and
profiling demonstrate that Linear is an improvement for Little Castle.

## Review handoff for Sol 5.6 Medium

The first review should analyze, not blindly rewrite.

Review:

1. Built-in pipeline shader compilation compatibility in the current Unity
   version.
2. Coordinate/sign correctness of sun/moon directions.
3. Shadow macro correctness in `LC_StylizedLit.shader`.
4. Linear/gamma color-space behavior of global colors.
5. GPU instancing compatibility.
6. Whether the shader's current normal/AO/specular path is appropriately cheap.
7. Whether any shared parameters belong in a ScriptableObject profile instead
   of the scene component.
8. Whether the material/shader folder naming matches the Asset Book workflow.
9. Scene serialization/GUID correctness.
10. Performance implications before Phase 2.

The review should preserve the global `_LC_*` contract unless there is a
specific technical reason to change it.

Also read:

- `docs/architecture/foliage-wind-authoring.md`;
- `docs/architecture/material-texture-contract.md`.

## Review / refinement handoff for Sol 6 Medium

After the 5.6 review, Sol 6 Medium should:

1. address confirmed compile/runtime issues;
2. run/edit tests where possible;
3. tune the atmosphere using actual in-game screenshots;
4. inspect shader variants and render passes;
5. improve performance where measurements justify it;
6. complete or refine later phases without duplicating lighting/time logic;
7. preserve the presentation/gameplay authority separation.

Sol 6 should treat this roadmap plus ADRs and AGENTS.md as project constraints,
not as optional historical notes.


### Foliage compatibility gate

Before any future agent changes LC_Foliage, tree materials or vegetation model
authoring, it must first validate at least one real production model against the
current shader contract.

Use:
Little Castle -> Rendering -> Validate Selected Foliage Model

The model must be classified as COMPATIBLE, COMPATIBLE WITH MASK/AUTHORING
CHANGE, REQUIRES SEPARATE LEAF MESH, or NOT SUITABLE FOR CURRENT WIND.

A model-authoring problem is not, by itself, justification for rewriting the
shared foliage shader.
