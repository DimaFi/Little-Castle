# Q04 — Shared river-envelope geometry / parity handoff

Date: 2026-10-09. Source contributor: GPT-6 ChatGPT.
Issue: [#33](https://github.com/DimaFi/Little-Castle/issues/33).
Branch: `gpt6/q04-river-envelope-helper-2026-10-09`.
Base: `codex/concept-world-pass-2026-10-09` at
`e6a02df6f57461df9cd7cf853e8e9d62b776d71a`.
**Status: narrow source refactor and EditMode tests authored, NOT yet compiled/run in Unity.**

## Existing behavior before this PR

`ChunkTerrainVertexMasks.Build` creates a presentation-only terrain
Color32 vertex mask from a chunk's absolute X/Z grid. It projects all
locally eligible river segments from the macro plan, skipping segments
whose squared length is <= 0.000001f; each endpoint half-width equals
`Mathf.Max(0.1f, river.GetWidthAtPoint(s) * 0.5f)`.
The chunk AABB broad-phase is inclusive and expanded by
`max(halfA, halfB) + 1.8f`.

For each vertex, every eligible segment contributes:

```text
t = clamp(dot(point-a,b-a)/|b-a|^2, 0..1)
distance = |point - (a + (b-a)*t)|
halfWidth = Mathf.Lerp(halfA, halfB, t)
wetness = 1 if distance <= halfWidth;
          else 1 - Mathf.SmoothStep(0,1,
                    Mathf.Clamp01((distance-halfWidth)/max(0.01,1.8)))
riverbank = maximum wetness across all eligible segments/rivers
G = round(clamp01(riverbank)*255)
R = round(clamp01(road*(1-riverbank))*255); B = 0; A = 255
```

**Maximum over segments is essential** at wide sharp bends and
confluences. Choosing only the nearest centerline segment would
suppress a wider neighbor and change the current visual.

## This change

Exact owned files:
- NEW `Assets/_Game/Scripts/World/Core/RiverEnvelopeUtility.cs` + .meta.
- MODIFIED `Assets/_Game/Scripts/World/Rendering/ChunkTerrainVertexMasks.cs`.
- NEW `Assets/_Game/Tests/Editor/RiverEnvelopeTests.cs` + .meta.
- This report.

The pure helper now owns `Segment`, `TryCreateSegment`,
`OverlapsChunk`, `DistanceToSegment`, `WetnessAt`,
`SoftCorridor`. The existing chunk mask code uses its helper
for river candidate preparation and G-channel wetness, but preserves:
- the same macro plan and ray-free per-chunk filtering;
- road segment math and material path behavior;
- segment traversal/union order;
- original float operations, clamp/interpolation and `Color32`
  conversion;
- no allocations per vertex except pre-existing output `Color32[]`;
- no terrain height, surface, macro plan, water mesh, bridge, placement,
  shaders or production materials changed.

This refactor is deliberately **one callsite**. Future river-carving,
water mesh, shoreline decoration and terrain surface owners must
evaluate the API contract separately before adopting it; do **not**
silently change their current behavior to claim all river systems
are already geometrically identical.

The helper uses `UnityEngine.Vector2/Mathf` for pure calculations
but does not use `GameObject`, `Physics`, `Random`, scene objects,
asset import or mutable world state. It uses absolute world X/Z:
negative coordinates and both sides of a chunk boundary get the
same results.

## Tests added / desktop acceptance

`LittleCastle.Tests.RiverEnvelopeTests` covers:
- endpoint width interpolation and riverbank influence;
- zero-length/repeated centerline points and invalid indices;
- inclusive, expanded, negative-coordinate chunk AABB;
- sharp bends and maximal adjacent wide segment contribution;
- confluence / repeated point / strongly variable width / negative
  and positive chunk seams;
- byte-for-byte equality of the new rendered green-channel wetness mask
  against a separately reproduced copy of the **previous code's exact
  arithmetic and culling order** across six chunks;
- uniform nominal-width fallback including minimum half-width;
- scalar distance and SoftCorridor parity with the previous formula.

The legacy comparison test intentionally remains a second independent
reference calculation; **do not refactor the oracle to call the new
helper**, or it would stop detecting changed logic.

Run in Unity 6000.5.5f1 (desktop):

```text
-batchmode -nographics -projectPath <projectRoot> -runTests -testPlatform EditMode -testFilter LittleCastle.Tests.RiverEnvelopeTests -testResults <Logs/Q04-RiverEnvelope.xml> -logFile <Logs/Q04-RiverEnvelope.log>
```

Then run existing focused suites:
`LittleCastle.Tests.ChunkTerrainVertexMaskTests`,
`LittleCastle.Tests.RiverBendEnvelopeTests`,
`LittleCastle.Tests.TerrainSurfaceVariableWidthTests`,
`LittleCastle.Tests.TerrainSurfaceBroadPhaseTests`,
and `LittleCastle.Tests.RiverWaterPresentationTests`, followed by full
EditMode and PlayMode suites. For `-runTests` do not add `-quit`;
inspect XML, not merely Unity's process return code.

Root visual gate: use same seed, camera, shader and concept profile to
compare river-confluence/bridge-bank appearance before/after at
near/far and day/sunset/night, and confirm identical fixed-bridge
waterline and terrain contacts. Q04 itself **does not change the water
mesh, shader, or bridge height**, and should not be approved as proof
that full water joins/shorelines work.

## Integration and risk log

- Byte mask parity is a **test requirement**, not a verified result in
  this source-only environment.
- Floating point compiler/runtime differences are handled by the
  exact output Color32 byte baseline; test precise scalar outputs as
  an additional guard in EditMode on the same Unity version.
- The helper's segment half-width floor of 0.1m matches wetness,
  not necessarily carving or riverbed's per-stage historical floors.
  Do not force other systems to adopt it without an explicit
  migration / compatibility review.
- The helper currently performs no sanitize for nonfinite river
  profile coordinates/widths because introducing new filters in Q04
  would change the old algorithm's behavior. Input validation remains
  macro-plan owner responsibility.
- Future Q05/Q09/Q18 should avoid repeated full-plan list creation or
  unbounded caches: Q04 does not provide a world-sized spatial index.
  Q18 must preserve legacy Main vs opt-in concept carve settings.
- No authored FBX, textures, shaders or other rendering assets changed.
  `Main` behavior must remain invariant; only the existing vertex-mask
  presentation function delegates its formerly local calculations.
- No gameplay/render acceptance, test PASS count or FPS can be claimed
  before root captures true Unity/Player evidence.

## Verification status

| Gate | Status |
|---|---|
| Narrow isolated source authored and legacy calculation read | DONE |
| New Unity GUIDs and independent NUnit parity tests authored | DONE |
| GitHub branch exact diff review | PENDING at report creation |
| Unity C# compilation and focused EditMode | **NOT RUN** |
| Full EditMode and PlayMode | **NOT RUN** |
| Real seam/bridge/shore GPU captures and Player CPU/GPU | **NOT RUN** |
| Production water/riverbed/carving integration | **NOT ATTEMPTED** |

The historic desktop 175/175 EditMode and 5/5 PlayMode are a different
revision and must not be presented as verification of this PR.

## Codex root review checklist

1. Read exact diff against concept branch and confirm only owned files.
2. Compile Unity and run both new and existing focused tests.
3. Preserve legacy reference test and report exact byte mismatches if any.
4. Verify no extra meshes, altered chunk heights, improved/changed river
   routes, or unexpected world-version changes.
5. Share the helper API with Q05/Q09/Q18 owners **after** desktop acceptance.
6. Record edit/play logs, actual GPU views and any differences with Git SHA.
7. Keep Q04 draft and issue open until root acceptance.
