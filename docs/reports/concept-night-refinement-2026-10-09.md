# Concept bridge night refinement — 2026-10-09

Status: second calibration compiled, serialized and GPU-rendered successfully
in Unity6000.5.5f1 on RTX4070TiSUPER. First GPU capture remained too dark;
second improves bridge/terrain visibility. Overall concept world visual
approval remains FAIL/pending (prototype terrain/water/banks, not finished art).

## Cause

At midnight the fixture used the default `DayNightLightingController` night
endpoint (`maxMoonIntensity = 0.14`, ambient sky/equator/ground
`(.25,.31,.46) / (.20,.25,.36) / (.13,.16,.24)`). The previously calibrated
fixture `StylizedLightingGlobals.ambientMultiplier = 0.55` then reduced those
ambient values before the shared shaders applied albedo, AO and shadow tint.
The ground endpoint therefore reached the shader as only about
`(.072,.088,.132)`, which explains the near-black terrain in
`Bridge_Close_Night.png`. Daylight remained readable because it additionally
had the calibrated directional sun.

## Fixture-only change

`ConceptBridgeSceneBuilder.RefineNightFixture()` writes exact, idempotent
serialized values to the scene's real `DayNightLightingController` and
`StylizedLightingGlobals` components:

- `maxMoonIntensity = 0.28`
- `moonLightColor = (.62,.72,1)`
- `nightAmbientSky = (.66,.73,.92)`
- `nightAmbientEquator = (.52,.60,.78)`
- `nightAmbientGround = (.39,.46,.64)`
- `StylizedLightingGlobals.nightShadowTint = (.58,.66,.86)`

The controller reapplies these values in PlayMode. No per-frame editor
reflection, material clone, light, effect, geometry, seed, mask, time API,
shared shader or runtime controller was added or changed. Midday is
mathematically unchanged: the day ambient and day shadow endpoints, `.55`
ambient multiplier, `.65` sun multiplier and sun settings are untouched; moon
intensity is zero at full daylight. The larger cool ambient remains well below
day lighting because night has no directional sun. The lighter blue night
shadow tint prevents terrain albedo from collapsing toward black without
removing the blue night separation. The existing isolated concept
`WorldDefinition` already points to its cloned time settings, so another
profile clone was unnecessary.

`CaptureNightRefinement()` idempotently applies the refinement, then renders
matching close day/night views into the new
`docs/reports/concept-night-refinement-evidence-2026-10-09-v2/` directory. The
historical evidence and first refinement capture are not overwritten.

## Root verification

The fixture can be serialized independently in a normal non-graphics Unity run:

```powershell
& 'E:/Unity/Unity_6.5/6000.5.5f1/Editor/Unity.exe' -batchmode -nographics -projectPath 'E:/Games_Develop/Little-Castle' -executeMethod LittleCastle.Editor.ConceptBridgeSceneBuilder.RefineNightFixture -logFile 'E:/Games_Develop/Little-Castle/docs/reports/concept-night-refinement-2026-10-09.log' -quit
```

Then capture on the real GPU (deliberately omit `-nographics`):

```powershell
& 'E:/Unity/Unity_6.5/6000.5.5f1/Editor/Unity.exe' -batchmode -projectPath 'E:/Games_Develop/Little-Castle' -executeMethod LittleCastle.Editor.ConceptBridgeSceneBuilder.CaptureNightRefinement -logFile 'E:/Games_Develop/Little-Castle/docs/reports/concept-night-refinement-capture-2026-10-09.log' -quit
```

Root verification: both GPU executeMethod runs exited0 with actual RTX4070TiSUPER
renders. Repeated RefineNightFixture exited0 and retained identical scene SHA256
`548f1b51fe71e80a6e40dc8ab665706bbeffc8b8619eed15a79e03a20e2e71d9`.
No new materials, Main settings or shared runtime/shader edits. Scene diff contains
only moon/night ambient/night shadow endpoint changes.

New daytime frame versus historical calibrated day: mean absolute RGB pixel
difference `[.817,1.061,.665]` on0..255; small but not pixel-identical. In the lower
terrain rectangle x100..1200/y750..950, night meanRGB rose from
`[14.38,20.58,15.36]` to `[36.75,47.98,32.48]`. These are image readback diagnostics,
not gameplay visibility guarantees. Root inspection finds bridge/road clearer;
full terrain/water/bank composition still does not meet concept art quality.

Focused EditMode regression: **31/31**, zero failed/skipped, bridge prefab,
clock, stylized shader contracts and night-light budget. XML/log copied into v2
evidence. No new full-suite or actual streamed-scene PlayMode approval is claimed.
Historical120/120+3/3 remain in the earlier desktop report. Camera renders are
visual evidence, not a GPU timing or built-player benchmark.
