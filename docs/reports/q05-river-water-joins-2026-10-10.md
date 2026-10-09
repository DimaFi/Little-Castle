# Q05 — River water seam and fixed-bridge source correctness

Date: 2026-10-10. GPT-6 ChatGPT. Tracking issue #34.
Stacked dependency: Q04 draft PR #52, source branch
`gpt6/q04-river-envelope-helper-2026-10-09` at
`8d94c0bd390eb9a6620c85849d0f141fe232c953`.
Task branch: `gpt6/q05-water-joins-bridge-stamps-2026-10-10`.

**Status: SOURCE CODE AND TESTS AUTHORED ONLY. Unity C# compilation,
new/old NUnit, PlayMode, prefab render, graphics Player captures and
performance are NOT RUN in the GitHub tool environment.**

## Ownership / why this task was selected

The user has a parallel cliff/rock art agent; it now has PR #62
(`codex/cliff-kit-intake-v001`). Q05 does not modify Q08,
any CliffKit files, Blender/FBX, generated rocks, terrain heights,
river hydrology, shared shaders/materials, Main, bridge prefab or scene.

Scoped Q05 diff (4 changed/new files + this report):
- MODIFY `Assets/_Game/Scripts/World/Rendering/RiverWaterMeshBuilder.cs`.
- MODIFY `Assets/_Game/Scripts/World/Rendering/RiverWaterPresenter.cs`.
- NEW `Assets/_Game/Tests/Editor/RiverWaterJoinTests.cs` + GUID .meta.
- THIS report.

## Correctness fixes

### 1. Exact vertex reuse without altering water shape

Previously `EmitClipped` drew a four-triangle fan per half-cell and
`AddTriangle` appended **three new vertex records per triangle**, even
when the same sampled/cut water corner had been emitted earlier in the
same or adjacent triangle.

A per-chunk `Dictionary<Vector3,int>` now maps the **exact** local
vertex XYZ to one index. For equal XYZ the world-projected UV and
upward normal are also equal; all triangles point at the same
vertex. No epsilon welding, quantization, shoreline shifting,
height blending, topology resampling or cross-chunk communication
was introduced. Any nonidentical vertex remains distinct.

Advantages: fewer duplicated mesh vertices and possibly lower
GPU memory/upload cost; **no measured CPU/GPU improvement is yet
claimed**. A hash dictionary and lookup adds CPU/GC work during
building, and must be profiled on real streamed chunks.

### 2. Linked fixed bridge source through river confluences

Previously `Evaluate` selected the river with the greatest
`halfWidth - distance` at each sample. It applied the
fixed-bridge `baseElevation - 0.85m` water stamp only if the
selected **winning river ID** matched the bridge's river ID.
At a confluence, an overlapping wider river could dominate
the score, leaving the valid fixed bridge un-stamped with a
discontinuous waterline.

Q05 validates the bridge's actual linked source **once per
chunk** in `CollectSites`, by demanding a real collected
centerline segment with matching `riverId` within its local
channel half-width at the bridge's authoritative center.
Only validated sites are considered for bridge-channel
height and shoreline stamping. The fixed channel owns its
core despite a different river winning the union score,
while ordinary outside-bridge samples keep the previous
max-width union envelope and terrain-sampled surface.
A stale/missing linked river is not allowed to create fake
water under the bridge.

The original `FixedBridgeSiteProfile` and
`BridgeSitePresentationUtility` remain the exclusive
source of supported fixed bridge footprint, channel, yaw
and water elevation. The new code does **not** move or
rescale the bridge model (scale 1), does not change site
identities and keeps `WaterHeightOffset=-0.85f`.
Real scene water-height validation still requires Unity.

### 3. Parked water chunk reuse

`RiverWaterPresenter.Populate` now searches for an existing
presenter **including inactive children**, to avoid creating
a second water surface when a streamed chunk object has
been temporarily parked. It resolves cached MeshFilter and
MeshRenderer references on reuse, updates the shared
material and releases any newly created transient mesh if
a legacy presenter is missing expected renderer components.
The existing `ReleaseOwnedResources` and ownership of one
mesh per chunk remain unchanged.

Root must still check actual streaming lifecycle; this Q05
does not integrate or refactor `WorldStreamer`. Q05 does not
attempt automatic regeneration after active input chunk data
changes, or add expensive every-frame mesh rebuilding.

## Tests authored (11 methods, 13 cases with TestCase yaw)

New `LittleCastle.Tests.RiverWaterJoinTests` covers:
1. Exact shared XYZ welded; fewer mesh vertices than the old
   every-triangle tripling; projected UV stable.
2. Negative x-boundary seam with overlapping rivers/confluence.
3. Positive x-boundary seam through a curved width profile.
4. Valid fixed river bridge near overlapping wider confluence
   holds the canonical water level through the actual core.
5. Fixed bridge negative chunk seam at 0/37/90 degrees:
   both mesh edges agree and the core equals
   `baseElevation + WaterHeightOffset`, with no scale override.
6. Orphan site ID cannot create or alter any water mesh.
7. Displaced bridge site outside actual linked river channel
   cannot stamp a phantom pool.
8. Welded mesh retains finite vertices, valid winding and
   chunk-local positions.
9. Inactive/parked presenter is reused without extra child mesh.

Existing `RiverWaterPresentationTests` must still pass, including
contract fixed height and release/destroy behavior.
Do not treat authored tests as executed PASS.

### Desktop execution

Use Unity 6000.5.5f1 in an isolated stacked Q04+Q05 worktree:

```text
-batchmode -nographics -projectPath <root> -runTests -testPlatform EditMode -testFilter LittleCastle.Tests.RiverWaterJoinTests -testResults <Logs/Q05-Joins.xml> -logFile <Logs/Q05-Joins.log>
-batchmode -nographics -projectPath <root> -runTests -testPlatform EditMode -testFilter LittleCastle.Tests.RiverWaterPresentationTests -testResults <Logs/Q05-Baseline.xml> -logFile <Logs/Q05-Baseline.log>
-batchmode -nographics -projectPath <root> -runTests -testPlatform EditMode -testFilter LittleCastle.Tests.RiverEnvelopeTests -testResults <Logs/Q04-Envelope.xml> -logFile <Logs/Q04-Envelope.log>
```

Run separate Unity processes, without `-quit` with
`-runTests`. Read NUnit XML and import compiler log. Then run
all EditMode+PlayMode; check generation and fixed bridge
tests. Q04 must be desktop accepted first.

### Visual and GPU acceptance — still PENDING

- Observe water seam at negative/positive chunk boundaries and
  curved shores at close/far camera in ConceptProceduralWorld.
- At actual confluence/bridge verify no doubled transparent
  overlap/z-fighting, no water inside bridge deck and no
  dry gap at `baseElevation - 0.85`; bridge must remain
  translation+yaw, scale1.
- Compare actual triangle/vertex/UV arrays, CPU water build
  time and per-frame GC, live resident chunk/mesh count and
  evict/revisit. Check both height seam and shore contour.
- Observe day, sunset, night render with current material,
  camera fog, shadow and water shader; take GPU Frame Debugger
  overdraw + batching captures.
- Test real bridge sites, negative coordinate seams,
  multi-tributary confluences and complete 2/8/16 sessions
  where affordable, including version/save compatibility.
- If vertex welding increases CPU or worsens GPU efficiency
  on actual target device, profile before approving rather
  than claiming any FPS gain from topology counts alone.

**Mesh invariants in NUnit are distinct from real screenshot/GPU
approval.** The code may also need a more dedicated coherent
water elevation/flow model to eliminate all visually apparent
height jumps outside fixed bridge sites. Q05 does not invent that
hydrology or rewrite source river data.

## Verification ledger

| Gate | Status |
|---|---|
| Q04 helper, water mesh/presenter, prior bridge tests reviewed | DONE |
| Scoped water-source/mesh weld and parked reuse authored | DONE |
| New tests/meta and this report authored | DONE |
| GitHub exact diff/readback | PENDING |
| Unity compile / focused NUnit | **NOT RUN** |
| Full EditMode / PlayMode | **NOT RUN** |
| Real chunk-seam visual, bridge, GPU/Player FPS | **NOT RUN** |
| Existing scene/Main/FBX/Q08 cliff files modified | **NO** |

The historic 175/175 EditMode and 5/5 PlayMode desktop results
apply to another revision; they do not verify Q05.
Keep PR DRAFT and issue OPEN until root Codex accepts the
actual imported scene tests, mesh topology and GPU evidence.
