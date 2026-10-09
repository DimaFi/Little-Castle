# Q13 — Cheap village night silhouette and explicit light budgets

Date: 2026-10-09. Contributor: GPT-6 ChatGPT.
Tracking: [issue #42](https://github.com/DimaFi/Little-Castle/issues/42).

**This is a stacked DRAFT source-only candidate on Q11**
[draft PR #55](https://github.com/DimaFi/Little-Castle/pull/55).
Base branch: `gpt6/q11-deterministic-village-layout-2026-10-09`,
head at Q13 start `7c273c7f1856eabf6fa2bbc86feee8f24369d0ea`.
Q11 itself depends on Q10 (#50).

NO Unity compile, EditMode, PlayMode, GPU capture, Player visual approval,
FPS measurements, real village presentation integration or hardware
test has been run on Q13 in this GitHub-only environment.

## Requirements and current reused infrastructure

The project already has:
- `NightLightBudgetManager` (12 default maximum active realtime lights,
  camera distance/priority, retention/hysteresis and fading);
- `NightLightEmitter` (disabled initial light, budget-managed Point/Spot,
  optional static no-shadows rule, presentation registry);
- `NightLightPoolVisual` using one shared material +
  `MaterialPropertyBlock` without material clones;
- shaders `LC_EmissiveGlow` and `LC_NightLightPool`, driven by the
  existing `_LC_NightAmount` global night exposure;
- Q11 `VillageLayoutData` with deterministic 3–5 houses, well,
  courtyard and safe local approach paths.

Q13 **does not change those systems**, the sky, shared materials,
`WorldStreamer`, `ConceptVillageFootprintPresenter`, production
prefabs, rendering pipeline, Main settings, or concurrent Q08
rock/scarp work. The existing 12-light global limit is not increased.
Only a future root integration explicitly using the Q13 policy can
change live village presentation.

## Owned additions relative to Q11 (13 files)

- NEW `Scripts/World/Rendering/ConceptVillageNightLightingSettings.cs` + GUID .meta
- NEW `Scripts/World/Rendering/VillageNightLightingPolicy.cs` + GUID .meta
- NEW `Settings/World/ConceptWorld_v001/ConceptVillageNightLighting.asset` + .meta
- NEW `Settings/World/ConceptWorld_v001/ConceptVillageNightLantern.prefab` + .meta
- NEW `Settings/World/ConceptWorld_v001/ConceptVillageNightWindowGlow.prefab` + .meta
- NEW `Tests/Editor/NightVillageBudgetTests.cs` + .meta
- THIS report

No edit to existing source files; no generated geometry/instances
are created in runtime until root intentionally adopts the prefab.

## Lighting assets

`ConceptVillageNightLighting.asset` is an isolated ScriptableObject:
- realtime distance **34 metres**, near-only;
- cheap ground pool distance **92 metres**;
- emissive far marker distance **200 metres**;
- night gating threshold **0.18**;
- max **2 per-village realtime requests**, max **5 ground pools**.
These are **initial safe screening values, not measured FPS budgets**.

`ConceptVillageNightLantern.prefab` is a tiny authored prototype:
- root at the ground; light child at relative Y=1.9m;
- ONE Point Light, initially **disabled**, warm falloff/range,
  **no realtime shadows** and no autonomous Update;
- existing `NightLightEmitter` with `targetLight` reference,
  `baseIntensity=1.25`, max camera realtime distance 34m,
  fade/flicker settings, `allowRealtimeShadows=false`;
- a ground-oriented Unity built-in quad child
  with shared `LC_NightLightPool_Default` material,
  existing `NightLightPoolVisual`, default disabled renderer,
  HDR warm tint and no cast/receive shadows;
- emitter `poolVisual` points to the quad child. The existing
  `NightLightBudgetManager` owns the actual budget, light
  enable/disable/fade and pool visibility.

`ConceptVillageNightWindowGlow.prefab` is a separate cheap
window-marker prototype:
- a vertical built-in quad using the existing
  `LC_EmissiveGlow_Default` shared material;
- **zero Light components and zero colliders**; no shadow cast;
- day/night brightness is shader-driven by `_LC_NightAmount`;
- real placement is **not** automatic: the root must align the
  authored marker to approved real house window pivots/texture
  UVs, adjust visible area/shape and avoid glowing rectangles
  floating outside building walls.

**Authoring caution:** The two concept prefab YAML assets were
authored via GitHub rather than exported by a running Unity Editor.
Root MUST import them in Unity 6000.5.5f1 and verify the built-in
quad mesh references, component/script GUIDs, assigned shared
materials and actual light/pool references before asserting that
either prefab works. If Unity import rejects a built-in quad reference,
re-author/Save As Prefab in the Editor without touching global
materials; do not claim the source text is already GPU-tested.

## Policy: different near/mid/far capabilities

`VillageNightLightingPolicy` works only with presentation data:
- `BuildMarkers(accepted Q11 layout, output)` makes exactly one
  well marker plus one entrance marker for each validated approach,
  deterministic from Q11 village seed/anchor/house stable IDs.
  Does not modify macro/road/house IDs or create GameObjects.
- `Evaluate(markers, budget, cameraXZ, nightAmount,
  globalFreeSlots, choices)` selects 3 separate boolean capabilities:
  `emissiveVisible`, `groundPoolVisible`,
  `requestRealtimeLight`.
- day/night: no realtime/pools/emissive requests before configured
  night threshold; day facade remains the normal material.
- near: path/well markers can request realtime light **only**
  inside <=34m and only within village quota (2) AND the
  **explicitly supplied** advisory global available slot count.
  WindowEmissive markers are NEVER light or pool candidates.
- mid: up to five prioritized path/well cheap ground pools, no
  Point/Spot requests beyond near distance.
- far: at most the emissive component remains visible <=200m
  with **no Point/Spot and no ground pool**.
- when root cannot know true free global slots, supply ZERO:
  cheap glow remains but realtime requests become zero.
- deterministic ranking: path entrances, then well, then windows;
  within priority by camera distance and stable ID; output sorted
  by stable ID, independent of chunk arrival or enumeration order.
- invalid/duplicate IDs, non-finite camera/positions or invalid
  config fail **before mutating caller result**.
- reusable scratch List/HashSet buffers reduce per-marker allocations,
  but all presentation work must happen on main Unity thread.

**Not a second global realtime-light manager.**
The `globalFreeSlots` argument is advisory and does not reserve
slots against other towns/lights. The current
`NightLightBudgetManager` remains the final authority and must
continue enforcing its existing 12-light cap. Root's scene
presenter must not instantiate all light-enabled prefabs at far
distances. Prefer pooled/emissive-only representations.
No per-village light management in a per-frame heavy
`Update`; use existing manager distance throttling and streaming
visibility thresholds.

This Q13 source does NOT create actual GameObjects from Q11
data, move lamps to real doors, find windows on real house
prefabs, verify LOS/obstacles, hide glow across hills, switch
prefab renderers dynamically, or attach automatically to
`ConceptVillage.prefab`. That is the owner-approved
runtime integration after desktop source/visual acceptance.

## New EditMode test cases (authored only)

`LittleCastle.Tests.NightVillageBudgetTests` includes **12 methods**:
1. Saved isolated concept settings import and valid narrow budget.
2. Day produces no light/glow request.
3. Near/middle/far selection, windows always emissive-only.
4. Local 2-light and global free-slot caps, independent cheap pools.
5. Stable output for shuffled input with equal distances/IDs.
6. Invalid settings, NaNs, duplicate IDs do not overwrite result.
7. Invalid kind/night threshold fail early.
8. Repeated evaluation does not retain stale presentation markers.
9. Real Q11 flat three-house layout yields stable well + entrance IDs.
10. Authored lantern prefab import, disabled shadowless managed
    light, real pool references and shared shader.
11. Authored window prefab contains neither Point/Spot nor collider,
    uses the existing emissive material and casts no shadows.
12. Existing manager global default cap remains 12.

Unity desktop command (Q10 + Q11 + Q13 branch worktree):

```text
-batchmode -nographics -projectPath <root> -runTests -testPlatform EditMode -testFilter LittleCastle.Tests.NightVillageBudgetTests -testResults <Logs/Q13-NightVillage.xml> -logFile <Logs/Q13-NightVillage.log>
```

Run actual Q10 `FootprintValidatorTests`, Q11
`VillageLayoutTests`, existing `NightLightingRuntimeTests`,
`WorldTimeTests`, then full EditMode and PlayMode.
No `-quit` with `-runTests`. Read actual NUnit XML,
compiler/import errors, light warnings and prefab overrides.

## Desktop visual/GPU acceptance required

- Check imported prefab hierarchies and referenced shader/material/mesh.
- Test actual 3–5-house Q11 neutral village; ensure the path is
  readable on the ground, windows align with real meshes and neither
  house façades nor landscape get excessively bright.
- Capture day / dusk / midnight, near/far RTS camera and zoom,
  with one village and several villages at the map edge.
- Use GPU Frame Debugger to count actual active/fading Point Lights,
  shader SetPass/draw calls, transparent quad fill, shadows, and
  whether GPU-instancing/shared materials are preserved.
- Test 2/8/16-player maps and chunk eviction/revisit; verify no
  stale emitter registry references, faded lights stay off and
  no permanent extra Point/Spot appears at far distance.
- Compare before/after 1% low frame time and CPU/GPU ms in a
  graphical Player, not nographics batch mode.
- Re-evaluate visual realism after setting real approved lamp/window
  pivots, doorway clearance and real terrace/river terrain heights.
- Do NOT globally raise the existing 12 realtime-light slots
  to compensate for excess lights; prefer cheap glow/distance LOD.

## Verification state

| Item | Status |
|---|---|
| Existing manager/emitter/pool/shared shaders/Q11 contract inspected | DONE |
| Isolated settings asset, policy, prefabs and tests authored | DONE |
| GitHub scoped diff / code check | PENDING at report creation |
| Unity compile + asset/prefab import | **NOT RUN** |
| Focused Q10/Q11/Q13 NUnit | **NOT RUN** |
| PlayMode village/graphics screenshot | **NOT RUN** |
| GPU light/shadow/frame debugger budget | **NOT RUN** |
| Actual Q11 runtime/spawner integration | **NOT ATTEMPTED** |
| Root approval and release | **NOT DONE** |

The previous desktop bridge night lighting refinement is real
evidence for a DIFFERENT revision and fixture, not approval of
new Q13 village prefabs. This candidate must remain DRAFT
until desktop validation.
