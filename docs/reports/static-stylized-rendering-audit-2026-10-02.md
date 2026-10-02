# Static Stylized Rendering Audit - 2026-10-02

Scope: repository-level static audit performed without launching Unity.

This report does not claim runtime or GPU validation.

## Shader structure checks

Checked:

- StylizedDayNightSky.shader
- LC_StylizedLit.shader
- LC_Foliage.shader
- LC_Grass.shader
- LC_Terrain.shader
- LC_NightLightPool.shader

Static results:

- every file has one Shader declaration;
- opening/closing braces are balanced;
- every CGPROGRAM has a matching ENDCG;
- each shader has an explicit Fallback or Fallback Off.

Observed pass structure:

### LC_StylizedLit

- ForwardBase: present;
- ForwardAdd: present;
- ShadowCaster: present;
- Roughness map path: present in base and additive lighting;
- budgeted local-light attenuation macro: present.

### LC_Foliage

- visible ApplyWind implementation: present;
- shadow ApplyWind implementation: present;
- visible and shadow calls use object vertex + normal + wind mask;
- automatic height mask: present;
- optional Vertex Color R mask: present;
- Crown Sway / Vertex Wave / Leaf Flutter parameters: present;
- separate Opacity map path: present in visible and shadow passes.

### LC_Grass

- visible ApplyGrassWind: present;
- shadow ApplyGrassWind: present;
- UV-Y rooted bend path: present;
- gust envelope: present;
- tip micro flutter: present;
- separate Opacity map path: present in visible and shadow passes.

## Material-to-shader GUID checks

Confirmed static GUID alignment:

- StylizedDayNightSky.mat -> StylizedDayNightSky.shader;
- LC_StylizedLit_Default.mat -> LC_StylizedLit.shader;
- LC_Foliage_Default.mat -> LC_Foliage.shader;
- LC_Grass_Default.mat -> LC_Grass.shader;
- LC_Wheat_Mature.mat -> LC_Grass.shader;
- LC_Terrain_Default.mat -> LC_Terrain.shader;
- LC_NightLightPool_Default.mat -> LC_NightLightPool.shader.

## Test scene serialization checks

WorldGenerationTest.unity statically contains:

- WorldTimeSystem;
- DayNightLightingController;
- StylizedLightingGlobals;
- NightLightBudgetManager;
- StrategyCameraController;
- Sun Light;
- Moon Light;
- stylized terrain material reference;
- stylized sky material reference.

## Editor validation added

Unity menu:

Little Castle -> Rendering -> Validate Stylized Rendering

Validator checks:

- required shaders resolve;
- required material assets exist;
- required shader properties exist;
- attempts to query Unity ShaderUtil compile-error state through reflection;
- explicitly reports current color space.

EditMode contract tests also cover shared shaders and materials.

## Current deliberate architecture

### Solid objects

Use LC_StylizedLit.

Supports:

- BaseColor;
- Normal;
- AO;
- Roughness;
- stylized global sun/moon;
- emission;
- night emission/flicker;
- global fog;
- local Point/Spot contribution through ForwardAdd;
- shadow casting/receiving;
- GPU instancing variant.

### Foliage

Use LC_Foliage.

Wind is GPU vertex deformation with three scales:

1. crown sway;
2. spatial vertex wave;
3. leaf-normal flutter.

This is specifically intended to make simple rounded/clustered Little Castle
leaf geometry feel alive without bones or per-leaf CPU scripts.

### Grass / wheat

Use LC_Grass.

Supports:

- UV-rooted bending;
- coherent gusts;
- tip micro flutter;
- backlight/transmission;
- GPU instancing;
- animated shadow caster.

Mature wheat preset exists as LC_Wheat_Mature.mat.

### Terrain

Use LC_Terrain.

Current implementation intentionally stays cheaper than solids.

It currently does not yet consume the full authored Normal/AO/Roughness/Height
terrain texture set.

### Night local lighting

Use three visual layers:

1. emissive material for the visible glowing window/lantern;
2. LC_NightLightPool for cheap warm ground response;
3. a limited nearby Point/Spot Light through NightLightBudgetManager when
   geometry really needs realtime illumination.

This avoids forcing every terrain/foliage draw through every local light.

## Gamma / Linear candidate

Project currently reports Gamma color space.

Linear is considered a plausible visual improvement candidate because it may
improve:

- light accumulation;
- sunset gradients;
- warm/cool light blending;
- local-light falloff;
- night tonality.

It is NOT approved automatically.

Required test before migration:

- identical camera;
- identical time of day;
- identical materials;
- screenshots in Gamma and Linear at day/sunset/night;
- texture import inspection;
- performance/build validation.

Keep Gamma if Linear does not produce a clearly better Little Castle result.

## Known limitations requiring Unity

Cannot be proven by repository inspection alone:

1. Unity 6000.5.5f1 shader compiler acceptance.
2. Built-in ForwardAdd macro behavior under the project's exact backend.
3. Real GPU instruction/sampler cost.
4. Actual batching/instancing results.
5. Foliage shadow visual alignment under deformation.
6. Whether wind amplitudes look natural on the real tree mesh.
7. Whether round leaf geometry has enough vertices for convincing flutter.
8. Alpha cutoff/shadow aliasing.
9. Point/Spot light draw-call multiplication in a populated village.
10. Gamma vs Linear visual quality.
11. Actual texture import settings.
12. Cross-platform shader behavior.

These are mandatory targets for the Sol 5.6 Medium review.

## Priority review order

1. Unity import/compile.
2. Run Stylized Rendering validator.
3. Run EditMode tests.
4. Play WorldGenerationTest at accelerated time.
5. Verify solid local lights.
6. Put a real production tree into the scene and inspect wind close/far.
7. Put grass/wheat clumps into the scene and inspect field coherence.
8. Test separate Opacity textures.
9. Assign actual Roughness maps to wood/stone/plaster.
10. A/B Gamma vs Linear.
11. Profile before adding further fullscreen effects.

## Current conclusion

The repository is structurally prepared for a real Unity visual review.

No repository-level GUID/ShaderLab-block mismatch was found in this static
audit.

Runtime correctness and visual quality remain intentionally unclaimed until
Unity 6000.5.5f1 compiles and renders the stack.


## Second static pass after wind / roughness / hemispherical ambient

Re-ran repository checks after the final pre-Unity changes.

Confirmed again:

- all six custom ShaderLab files have balanced braces;
- every CGPROGRAM has a matching ENDCG;
- all five newly added/modified C# rendering/editor/test files have balanced
  braces;
- no temporary Material is incorrectly used through IDisposable/using syntax;
- LC_StylizedLit contains exactly one ForwardAdd pass;
- the duplicate attenuation declaration around UNITY_LIGHT_ATTENUATION was
  removed;
- LC_StylizedLit still contains Roughness support;
- LC_StylizedLit/Foliage/Grass/Terrain all consume hemispherical ambient;
- StylizedLightingGlobals publishes sky/equator/ground ambient globals;
- LC_Foliage contains two ApplyWind implementations (visible + shadow), two
  wind-mask helpers, and two separate-opacity samplers;
- LC_Grass contains two ApplyGrassWind implementations and two separate-opacity
  samplers.

No new repository-structure failure was found in this second static pass.

Unity shader compilation/runtime validation is still mandatory.
