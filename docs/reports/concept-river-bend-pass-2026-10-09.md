# Concept river-bend carving pass — 2026-10-09

## Scope and contract

This pass adds a default-off, concept-only river-carving option that evaluates
every non-degenerate centerline segment. For each river and terrain sample it
uses the maximum valid width/depth/profile carve contribution. This aligns the
concept carving envelope with the existing variable-width riverbed surface and
terrain wetness-mask behavior at sharp bends.

The disabled path retains the previous closest-segment calculation. Width and
depth remain in their existing world-unit contracts. The fixed bridge terrain
stage still runs later in the configured pipeline and was not changed.

## Files changed

- `Assets/_Game/Scripts/World/Generation/Stages/RiverTerrainCarvingStage.cs`
  adds the serialized opt-in and read-only public property, plus the
  all-segment maximum-contribution branch.
- `Assets/_Game/Settings/World/ConceptWorld_v001/Stages/02_RiverTerrainCarving.asset`
  enables the option only for the isolated concept profile.
- `Assets/_Game/Tests/Editor/RiverBendEnvelopeTests.cs` and `.meta` add focused
  EditMode coverage.
- `docs/reports/concept-river-bend-pass-2026-10-09.md` records this handoff.

## Algorithm and cost

Legacy mode remains one nearest-segment scan followed by one carve evaluation.
The opt-in mode scans all valid segments and evaluates a contribution for each
segment whose local influence radius contains the sample, retaining the
maximum. Both modes are `O(samples * total river segments)`; the opt-in mode can
perform more curve/profile evaluations inside overlapping bend envelopes. It
allocates no per-sample collections and does not materialize additional chunks
or scene objects.

## Tests added

- A sharp bend where the closest narrow segment previously suppressed the
  wider adjacent segment's valid carve.
- Known uniform-river output through the default legacy nearest-only path.
- Deterministic repeated generation plus shared-border equality across chunks
  `(-1,-1)` and `(0,-1)`, with a repeated centerline point.
- Asset isolation: concept carving enables the option while Main remains off.

These tests use `WorldRiverData`, `MacroWorldPlan`, `GenerationContext`, and
`WorldChunkData` directly and require no GameObjects.

## Verification status and root commands

Unity was intentionally not launched under the ownership plan, so no Unity
compile or EditMode test result is claimed here. Root should run after all
source owners stop editing:

```powershell
$Unity = 'E:/Unity/Unity_6.5/6000.5.5f1/Editor/Unity.exe'
$Project = 'E:/Games_Develop/Little-Castle'

& $Unity -batchmode -nographics -projectPath $Project -runTests `
  -testPlatform EditMode `
  -testFilter LittleCastle.Tests.RiverBendEnvelopeTests `
  -testResults "$Project/Logs/concept-river-bend-EditMode.xml" `
  -logFile "$Project/Logs/concept-river-bend-EditMode.log"

& $Unity -batchmode -nographics -projectPath $Project -runTests `
  -testPlatform EditMode `
  -testResults "$Project/Logs/concept-combined-EditMode.xml" `
  -logFile "$Project/Logs/concept-combined-EditMode.log"
```

Then run the combined PlayMode/runtime and real GPU scene checks owned by root,
especially confirming visible bend coverage and unchanged fixed bridge
stamping. The complete EditMode result should include
`TerrainSurfaceVariableWidthTests`, `ChunkTerrainVertexMaskTests`, and
`ConceptWorldProfileTests`.

## Limitations

The option is intentionally enabled only in the concept carving asset. Main
and newly created carving stages retain legacy behavior. This pass does not
change river source/planner data, riverbed surfaces, wetness masks, water
meshes/shaders, bridge logic, materials, catalogs, or scenes.
