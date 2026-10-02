# Night Lighting + LOD Foundation Audit — 2026-10-02

Unity Editor was not launched from this environment. This report records the
repository-side implementation and the remaining runtime validation.

## Existing foundation confirmed

Before this pass Little Castle already had:

- global day/night atmosphere;
- shared _LC_* shader globals;
- LC_StylizedLit emission;
- LC_StylizedLit ForwardAdd local-light path;
- LC_NightLightPool additive shader;
- NightLightEmitter;
- NightLightBudgetManager;
- authored LODGroup contract;
- LC_DistantSimple far shader;
- Production Asset Validator.

## Improvements implemented in this pass

### Realtime night lights

NightLightEmitter now has:

- smooth fade-in;
- smooth fade-out;
- subtle two-wave realtime flicker;
- no per-emitter Update while dormant;
- static active/fading presentation registry;
- separate realtime-light distance and cheap-pool distance;
- default realtime shadows disabled.

NightLightBudgetManager now has:

- conservative default realtime budget;
- nearby priority sorting;
- selection-retention hysteresis;
- periodic budget selection;
- per-frame ticking only of active/fading lights;
- diagnostics counters.

Current test-scene defaults:

- max realtime local lights: 12;
- global realtime eligibility distance: 78 m;
- evaluation interval: 0.18 s;
- selection retention distance multiplier: 0.84.

### Cheap visual glow

Added:

- LC_EmissiveGlow.shader;
- LC_EmissiveGlow_Default.mat;
- NightLightPoolVisual.

LC_EmissiveGlow:

- one pass;
- additive transparent;
- no shadows;
- no physical local-light calculation;
- shared night amount;
- shared time;
- cheap de-synchronized flicker;
- fog-to-black;
- instancing variant.

NightLightPoolVisual:

- shared material;
- MaterialPropertyBlock;
- no runtime material cloning;
- distance-controlled renderer visibility.

### Diagnostics

F8 WorldPerformanceOverlay now reports:

- registered emitters;
- realtime active count;
- candidate count;
- fading count;
- visible cheap pool count.

### Production validation

Production Asset Validator now warns about:

- unmanaged Point/Spot lights;
- realtime shadows on repeated local lights;
- ForcePixel local lights;
- very large local-light range;
- excessive local lights inside one prefab;
- excessive NightLightEmitter camera distance;
- incorrect NightLightPoolVisual shader;
- far-LOD motion vectors.

Stylized Rendering Validator and contract tests now include LC Emissive Glow.

### LOD policy

Geometry LOD remains Unity LODGroup + authored Astra/Blender/Codex models.

Do not create runtime mesh simplification.

Lighting LOD is separate:

near:
- emissive;
- optional ground pool;
- budgeted realtime Point/Spot.

medium:
- emissive;
- optional pool.

far:
- emissive only where authored LOD needs it.

farthest:
- LC Distant Simple / silhouette;
- no realtime local light;
- no pool;
- no detailed glow;
- no shadows/probes/motion vectors.

Canonical document:

docs/architecture/night-lighting-and-lod.md

## Static checks completed

Checked structural brace/parenthesis/bracket balance for:

- NightLightEmitter.cs
- NightLightPoolVisual.cs
- NightLightBudgetManager.cs
- WorldPerformanceOverlay.cs
- StylizedRenderingValidator.cs
- ProductionAssetValidator.cs
- StylizedRenderingContractTests.cs
- NightLightingRuntimeTests.cs

Checked shader structure:

- LC_EmissiveGlow.shader: one Pass, balanced CGPROGRAM/ENDCG;
- LC_NightLightPool.shader: one Pass, balanced CGPROGRAM/ENDCG;
- LC_StylizedLit.shader: existing three passes remain structurally balanced.

Checked WorldGenerationTest serialization:

- realtime budget = 12;
- distance = 78;
- hysteresis field serialized;
- F8 overlay references NightLightBudgetManager.

## Unity validation still required

1. Let Unity 6000.5.5f1 compile all new C#.
2. Run EditMode tests.
3. Run Little Castle -> Rendering -> Validate Stylized Rendering.
4. Create/import one representative lantern.
5. Configure:
   - Body / LC Stylized Lit;
   - Glow / LC Emissive Glow;
   - optional pool / LC Night Light Pool + NightLightPoolVisual;
   - compact Point Light + NightLightEmitter.
6. Test full day -> twilight -> night.
7. Check there is no visible hard pop when lights enter/leave realtime budget.
8. Test 50-200 visible lanterns.
9. Inspect F8 light counters.
10. Profile ForwardAdd draw cost and overdraw.
11. Tune final point-light range/intensity/color.
12. Test authored LOD0/LOD1/LOD2/far transition.
13. Confirm far LOD has no shadows/probes/motion vectors/local lights.
14. Compare Gamma versus Linear only as a deliberate A/B; do not switch
    automatically.

## Asset rule

Do not create one realtime Unity Light per emissive window.

The visual impression of a lit settlement must survive even when most
realtime-light slots are unavailable.

Emission is the baseline visual layer; realtime Light is a near-camera luxury.
