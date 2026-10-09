# Q06 — Calm one-pass concept river water material

Date: 2026-10-10. Owner: GPT-6 ChatGPT. GitHub issue #35.
Source branch: `gpt6/q06-soft-river-water-material-2026-10-10`.
**Stacked on Q05** draft PR #63 at
`7fb7aaf03b16e8bc4aebdee409447a77566e341e`,
which is itself based on Q04 draft PR #52.
Root must verify Q04 → Q05 → Q06, in that order.

**Status: source-only shader/material/test candidate.**
This GitHub-only agent has NOT compiled or imported the shader in
Unity 6000.5.5f1, run new NUnit tests, rendered a GPU frame,
measured draw calls/FPS, or proven visual quality in day/night scenes.

## Scope / collision avoidance

Exactly five NEW Q06 files:
1. `Assets/_Game/Shaders/Terrain/LC_RiverWater.shader` + .meta.
2. `Assets/_Game/Settings/World/ConceptWorld_v001/Concept_RiverWater.mat` + .meta.
3. `Assets/_Game/Tests/Editor/WaterMaterialContractTests.cs` + .meta.
4. This report.

No edits to the existing `RiverWaterMeshBuilder`,
`RiverWaterPresenter`, shared river/stylized shader/materials,
`ConceptBridgeTest`, `ConceptProceduralWorld`, Main,
URP/project render pipeline, water source hydrology, prefab bridges,
physics, rock/scarp assets or competing Q08 CliffKit PR #62.

**The original bridge/fixture water material**
`Assets/_Game/Models/Concept/BridgeValidation/Water.mat`
remains unchanged and is not implicitly replaced. The new
concept material is deliberately unused in the active scene
until the root owner approves explicit presentation wiring.

## Rendering contract

`Shader "Little Castle/Terrain/LC River Water"`:
- one forward transparent pass (`Queue=Transparent-10`,
  `Blend SrcAlpha OneMinusSrcAlpha`, `ZWrite Off`,
  `ZTest LEqual`, `Cull Back`);
- mesh world XYZ is unchanged in the vertex stage — especially
  fixed bridge water Y = `baseElevation - 0.85m` and scale=1;
- gentle ripples are TWO analytic low-amplitude sines in fragment,
  anchored to `worldPosition.xz`; no per-vertex displacement,
  no CPU Animator, no normal map or scrollable seam-prone UV tiles;
- slow movement from `_Time.y` and configured decorative
  world-space `_FlowDirection` (purely aesthetic, NOT actual
  macro stream vectors); `_FlowSpeed=0.12` and
  `_RippleStrength=0.055` initially;
- day/night palette lerps between turquoise/muted day
  `_DayColor=(.21,.43,.48,.72)` and cool blue/gray night
  `_NightColor=(.11,.22,.31,.79)` via the *existing*
  `_LC_NightAmount` global; fog uses existing Unity
  `UNITY_APPLY_FOG`, with no new time/atmosphere controller;
- cheap **approximate** shoreline contrast from
  `fwidth(worldPosition.y)` scaled by `_ShoreSlopeGain=8`
  and `_ShoreContrast=.08`. It highlights areas of changing
  water surface gradient; this is **not a true measured
  shoreline-distance mask** and may highlight profile transitions
  and bridge stamps. Root may choose to disable/re-author
  this approximate effect after visual acceptance;
- no depth/opaque/grab texture, refraction, reflection
  probe stack, geometric tessellation, camera-color fullscreen
  pass, extra lights, screen-space foam renderer or texture
  sampling. Source contains **0 texture fetches**,
  **1 transparent pass** and **2 trigonometric ripple waves**,
  plus derivative shore grade. GPU cost is NOT inferred
  from these counts until actually profiled.

This shader intentionally follows the current project's
CGPROGRAM/`UnityCG.cginc` rendering convention to match
the existing stylized sky/terrain/night glow pipeline. Root must
also test it on the actual configured render pipeline after
future URP migration: this source is **not a ShaderGraph/URP
UniversalForward rewrite** and does not certify URP support.

The saved `Concept_RiverWater.mat` is a separate isolated
material with its own GUID and the shader's GUID, blending
queue and initial parameters. It does not create/import
textures, modify the user's independent water texture pack,
or require any material instance clones for animation.

## Authored tests — **not run**

`LittleCastle.Tests.WaterMaterialContractTests` has **9 NUnit
source test methods**. It checks:
- the new shader and material import with linked GUID, and
  the existing bridge `Water.mat` remains separate;
- exactly one pass, transparent blend, ZWrite off, back culling;
- no depth/opaque/grab/tessellation/refraction/texture fetch;
- vertex world position does not move or recalculate fixed
  bridge water elevation;
- material initial RGB/alpha/queue remains soft, with darker
  night tint;
- flow speed/ripple/slope caps and nonzero decorative flow
  direction;
- approximate shore grade independent of depth buffer;
- use of `_LC_NightAmount`, not a duplicate gameplay clock;
- no unique textures, material clones, and existing bridge
  `FixedBridgeSiteProfile.WaterHeightOffset=-0.85f` constant.

**Test runner (Unity 6000.5.5f1, isolated Q04+Q05+Q06 worktree):**

```text
-batchmode -nographics -projectPath <root> -runTests -testPlatform EditMode -testFilter LittleCastle.Tests.WaterMaterialContractTests -testResults <Logs/Q06-Contract.xml> -logFile <Logs/Q06-Contract.log>
-batchmode -nographics -projectPath <root> -runTests -testPlatform EditMode -testFilter LittleCastle.Tests.RiverWaterJoinTests -testResults <Logs/Q05-Join.xml> -logFile <Logs/Q05-Join.log>
-batchmode -nographics -projectPath <root> -runTests -testPlatform EditMode -testFilter LittleCastle.Tests.RiverWaterPresentationTests -testResults <Logs/Q05-Baseline.xml> -logFile <Logs/Q05-Baseline.log>
```

Use separate Unity processes, **do not combine `-runTests`
with `-quit`**. Inspect real NUnit XML, Unity Shader Inspector,
import console and shader compile errors, not merely process exit.

## Required visual/GPU evidence (PENDING)

Root/Codex after accepting Q04/Q05:
1. Verify shader/material GUID import and Shader Inspector
   compile on the target built-in/URP configuration.
2. In an **isolated test-only scene/presentation setup**,
   temporarily assign the new concept material to the river
   water presenter via owner-approved code; do NOT replace
   the permanent shared bridge fixture `Water.mat`.
3. Capture same seed/camera/path day, sunset and night; close,
   middle, far view; confluence, river bend, both negative and
   positive chunk edges, and fixed bridge `base-.85` core.
4. Specifically inspect additive-looking overly bright foam,
   water transparency sorted against banks/wheat/trees,
   bridges/distant fog, overdraw, shimmering `fwidth`
   on thin/distant geometry, flicker intensity and whether
   global flow direction is distracting on curved streams.
5. Compare real GPU/CPU render ms, SetPass/batching,
   transparent overdraw and per-chunk mesh/material count
   against previously accepted water. Validate no production
   texture, shared material or Player FPS regression.
6. Test scene and Player with existing `LC_Foliage`, sky
   and local lights under configured quality tiers; actual
   near/far readability takes precedence over retaining any
   optional shore gradient approximation.
7. If the shore contrast cannot reliably read as shoreline
   without depth, prefer a future authoritative
   vertex-color bank-distance attribute from the mesh
   pipeline under a NEW owner-approved API. Q06 does not
   clandestinely change Q05 mesh data or invent hydrology.

## Verification ledger

| Check | Result |
|---|---|
| Q05 geometry/bridge source, existing water shader style and material reviewed | DONE |
| Additive shader + material + GUID meta + 9 NUnit methods authored | DONE |
| Strict Q06 GitHub diff | PENDING at report creation |
| Unity shader/material import/compile | **NOT RUN** |
| Focused Q04/Q05/Q06 EditMode tests | **NOT RUN** |
| Full EditMode/PlayMode | **NOT RUN** |
| Real day/sunset/night GPU visuals and Player FPS | **NOT RUN** |
| Production/bridge fixture material or Main changed | **NO** |

Historical desktop 175/175 EditMode + 5/5 PlayMode and
bridge captures belong to a different checked revision.
Keep Q06 issue OPEN and PR DRAFT until desktop/Codex
acceptance with actual shader and water screenshots.
