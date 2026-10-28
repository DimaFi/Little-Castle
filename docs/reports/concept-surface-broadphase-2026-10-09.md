# Concept terrain-surface broad phase — 2026-10-09

## Outcome

The concept terrain-surface stage now opts into a per-chunk road/river segment
broad phase. The root-owned GPU run measured stage 07 at 107.856 ms over the
1024 m profile with 49 visible chunks before this change. A new visual timing
capture is still required; this report does not claim an improvement without
that measurement.

Main and newly created `TerrainSurfaceStage` instances remain on the exact
legacy whole-plan scan unless `useSegmentBroadPhase` is explicitly enabled.

## Contract

For each generated chunk, the opt-in path scans macro geometry once and creates
temporary lists containing only segments whose width-expanded AABB can reach
the chunk. Each retained AABB is clipped to the chunk and checked before its
exact per-cell distance test. Candidate order remains road-major then
segment-major, preserving the first intersecting road's surface kind.

- Roads retain the legacy distance semantics, including degenerate point
  segments.
- Variable-width rivers skip degenerate segments and calculate the exact
  per-point interpolated width at the closest point on every candidate.
- Nominal-width rivers retain legacy constant-width and degenerate-segment
  behavior.
- Non-finite geometry or padding is conservatively retained for the exact
  narrow phase, preventing a culling-only false rejection.
- No index or cache persists beyond the current chunk generation call.

## Complexity

Candidate preparation is `O(total macro segments)` per chunk. Surface
classification is `O(cells * local candidate segments)` instead of scanning
the complete macro geometry for every cell. Temporary memory is
`O(local candidate segments)` and is released after the stage call.

## Files

- `Assets/_Game/Scripts/World/Generation/Stages/TerrainSurfaceStage.cs`
- `Assets/_Game/Settings/World/ConceptWorld_v001/Stages/07_TerrainSurface.asset`
- `Assets/_Game/Tests/Editor/TerrainSurfaceBroadPhaseTests.cs` and `.meta`
- `docs/reports/concept-surface-broadphase-2026-10-09.md`

## Tests added

- Full-cell enabled/disabled equivalence across negative and zero chunks with
  a sharp variable-width river bend, a repeated river point, a degenerate road
  segment, and overlapping road kinds that verify first-road priority.
- Distant macro segments plus exact expanded-width contact at north/east chunk
  cell centers to guard against false broad-phase rejection.
- Legacy nominal-width degenerate river equivalence.
- Concept-only flag isolation from Main.

These tests use real `MacroWorldPlan`, `GenerationContext`, and
`WorldChunkData` inputs without GameObjects or timing thresholds.

## Verification and root commands

Unity was intentionally not launched under the ownership plan. After all
source owners release, root should run:

```powershell
$Unity = 'E:/Unity/Unity_6.5/6000.5.5f1/Editor/Unity.exe'
$Project = 'E:/Games_Develop/Little-Castle'

& $Unity -batchmode -nographics -projectPath $Project -runTests `
  -testPlatform EditMode `
  -testFilter LittleCastle.Tests.TerrainSurfaceBroadPhaseTests `
  -testResults "$Project/Logs/concept-surface-broadphase-EditMode.xml" `
  -logFile "$Project/Logs/concept-surface-broadphase-EditMode.log"

& $Unity -batchmode -nographics -projectPath $Project -runTests `
  -testPlatform EditMode `
  -testResults "$Project/Logs/concept-combined-EditMode.xml" `
  -logFile "$Project/Logs/concept-combined-EditMode.log"
```

Root should then run one equivalent real GPU visual capture and compare the
stage 07 timing against the 107.856 ms baseline while checking road/river
surfaces at sharp bends and chunk borders. No performance acceptance threshold
is encoded in CI.
