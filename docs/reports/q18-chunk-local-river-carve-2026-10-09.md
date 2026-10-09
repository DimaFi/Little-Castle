# Q18 — Chunk-local river-carve broad phase (source-only acceptance)

Date: 2026-10-09. GPT-6 ChatGPT.
Issue: [#47](https://github.com/DimaFi/Little-Castle/issues/47).
Branch `gpt6/q18-chunk-local-river-carve-2026-10-09`.

**Stacked dependency:** Q04 [draft PR #52](https://github.com/DimaFi/Little-Castle/pull/52),
branch `gpt6/q04-river-envelope-helper-2026-10-09`,
commit `8d94c0bd390eb9a6620c85849d0f141fe232c953`.
Do not merge Q18 into concept/main before Q04 desktop acceptance.

Status: CODE + tests + profile opt-in authored, **Unity compilation, EditMode,
PlayMode, Player FPS and true timing NOT RUN in this tool session**.

## Motivation and measured prior baseline

The previously tested isolated `ConceptProceduralWorld` scene contained
real rivers and fixed bridges. The Oct 9 desktop Play report measured
**77.224 ms worst generation stage `02_RiverTerrainCarving`**
after the TerrainSurface broadphase optimization. This number is a
*historical one-run Editor Play maximum for a previous commit*, not an
expected post-Q18 value, not a 60 FPS claim and not a matched benchmark.
Root must verify whether this change actually reduces the worst spike.

Before Q18, opt-in any-segment mode searched **all rivers and all
centerline segments for every terrain vertex**. With 65×65 samples
and many halo river segments, this can repeatedly calculate far-away
segment distances despite them never touching a chunk.

## Exact changed files relative to Q04

1. MODIFY `Assets/_Game/Scripts/World/Generation/Stages/RiverTerrainCarvingStage.cs`
2. MODIFY `Assets/_Game/Settings/World/ConceptWorld_v001/Stages/02_RiverTerrainCarving.asset`
3. NEW `Assets/_Game/Tests/Editor/RiverCarveBroadPhaseTests.cs` + GUID `.meta`
4. This report.

No `Main` world profile, `FixedBridgeTerrainStage`, macro river
source/path, terrain height generation, water shader/prefab, scarp/rock
assets, `WorldStreamer` or save/delta schema were modified.
Parallel scarp and rock work (Q08) is deliberately not touched.

## Algorithm and safety proof outline

Adds serialized **default-off** `useChunkLocalCarveBroadPhase` to
`RiverTerrainCarvingStage`, plus diagnostics
`LastScannedSegments` and `LastCandidateSegments`. It only becomes
active if **both**:
- `useAnySegmentCarveEnvelope=true`, and
- `useChunkLocalCarveBroadPhase=true`.

This preserves the old `Main` nearest-centerline selection exactly
even if the new toggle is accidentally set there. The historical
nearest-only method still reads every segment to select the true
globally closest segment rather than choosing a chunk-local subset.

For opt-in any-segment mode, `CollectChunkCandidates`:
- computes absolute chunk min/max X,Z including boundary vertices;
- scans macro rivers **once per chunk**, in original stable
  river/segment order (no reordering, no ID changes);
- skips null/short rivers and repeated segment points with the same
  squared-length cutoff `<= 1e-6f`;
- reuses Q04 `RiverEnvelopeUtility.TryCreateSegment` for endpoints
  and `OverlapsChunk` for expanded AABB;
- collects references/indexes only for potentially influential
  segments in one transient per-chunk list; **no persistent full-map
  tree/index** and no additional `GameObject`/global cache.

### Conservative broad-phase geometry

Before carve, any-segment mode computes:

```text
width(t) = river.GetWidthAtSegment(index, t)
influenceRadius = max(0.5m, width(t)/2 + bankFalloff)
```

Q04 helper's candidate endpoint half-widths are
`max(0.1m, GetWidthAtPoint(endpoint)/2)`.
These are **at least as large** as carving's endpoint half-widths
(min carving width=0.1m => half=0.05m). Candidate AABB expands by:

```text
max(helperEndpointHalfWidthA, helperEndpointHalfWidthB)
    + max(0.5m, bankFalloff)
```

This is a conservative superset of all points within the
actual carve influence radius over a segment, including
variable-width profiles, rounded endpoints and chunk seams.
It may include false-positive segments; it must not skip
any segment capable of changing a terrain vertex's height.

For each candidate, `GetSegmentCarve` runs the same
`DistancePointSegmentSqr`, width/depth `Get...AtSegment`, clamped
influenceRadius, AnimationCurve cross-section and `Mathf.Max`
accumulation as the previous inner loop. The original full-scan path
now calls **the same** helper to reduce arithmetic drift.
No nearest-segment approximation, grid snapping or height welding.
No `Task.Run` around ScriptableObject/AnimationCurve.

This improves typical asymptotic per-chunk work from
approximately `O(vertexCount × allRiverSegments)` to
`O(allRiverSegments + vertexCount × candidateSegments)`.
Worst-case remains proportional to all segments if every broad AABB
overlaps the chunk. A transient `List` is allocated per opt-in
chunk: evaluate GC pressure in a real large-map stream and optimize
only after measured evidence.

### Concept opt-in only

The tracked isolated concept asset
`Assets/_Game/Settings/World/ConceptWorld_v001/Stages/02_RiverTerrainCarving.asset`
now contains:

```yaml
useAnySegmentCarveEnvelope: 1
useChunkLocalCarveBroadPhase: 1
```

The production/Main asset remains untouched. This is a performance
policy change and **must not** silently alter generation version
unless root confirms measured height parity (the source explicitly
aims at byte-identical float heights for deterministic saves).

## Tests authored; execute in Unity

`LittleCastle.Tests.RiverCarveBroadPhaseTests` has 8 tests:
1. **All-height parity** of local vs old full any-segment scan across
   eight negative/positive/distant chunks, multiple differently
   profiled rivers, a confluence and repeated points.
2. Far river candidate excluded before per-vertex work.
3. Sharp bend still carved by wide neighboring profile, exact parity.
4. Opposite sides of negative/positive chunk border agree.
5. Legacy nearest-only unchanged even with broadphase toggle on.
6. Uniform nominal-width fallback and narrow varying widths/depths
   preserve exact heights.
7. Repeated/degenerate segment ignored but valid neighbors retained.
8. Only concept tracked stage opt-in; Main remains nearest-only/off.

Together with existing `RiverBendEnvelopeTests`, Q04
`RiverEnvelopeTests` and full EditMode/PlayMode, run in
Unity **6000.5.5f1** (isolated stacked Q04+Q18 worktree):

```text
-batchmode -nographics -projectPath <root> -runTests -testPlatform EditMode -testFilter LittleCastle.Tests.RiverCarveBroadPhaseTests -testResults <Logs/Q18-Carve.xml> -logFile <Logs/Q18-Carve.log>
-batchmode -nographics -projectPath <root> -runTests -testPlatform EditMode -testFilter LittleCastle.Tests.RiverBendEnvelopeTests -testResults <Logs/Q18-Bend.xml> -logFile <Logs/Q18-Bend.log>
-batchmode -nographics -projectPath <root> -runTests -testPlatform EditMode -testFilter LittleCastle.Tests.RiverEnvelopeTests -testResults <Logs/Q04-Envelope.xml> -logFile <Logs/Q04-Envelope.log>
```

Run separate Unity processes; do not append `-quit` to
`-runTests`. Inspect NUnit XML, Unity compile/import console,
PlayMode and actual world launch. If any exact-height mismatch is
reported, do not approve the optimization or roll a new save
generation version silently. First inspect the actual mismatched
world coordinates / river widths / precise float sampling order.

### Performance/visual gate for root Codex

Desktop owner must record side-by-side:
- exact same finite `ConceptWorldDefinition` settings and map seed
  `-10101`, chunk set, frame camera path and same hardware;
- legacy any-segment full scan vs opt-in local culling;
- percent of macro segments retained per chunk
  (`LastCandidateSegments/LastScannedSegments`), allocations,
  total carved vertices and full height checksum;
- per-stage `02_RiverTerrainCarving` median, p95, p99 and worst time
  on a warm and cold chunk, total bootstrap duration, 1% low
  frame time, max frame spike and managed allocation count;
- negative chunk border, fixed bridge terrain seam, near/far
  water and shadow captures day/sunset/night;
- 2/8/16-player map sizes and multiple seeds where affordable
  (not a one-map speed claim), and revisit after cache eviction;
- run full EditMode and PlayMode suites and ensure no collateral
  regressions or unsafe route/bridge changes.

The 77.224ms earlier worst stage is **only baseline context**. No
post-Q18 speedup, hitch budget, 60 FPS or deterministic save
compatibility is asserted until these desktop tests are accepted.

## Verification state

| Gate | State |
|---|---|
| Existing stage/profile/Q04 helper/older tests reviewed | DONE |
| Opt-in candidate list, exact carve helper, new NUnit source | AUTHORED |
| Strict GitHub Q18 diff ownership/static braces | PENDING at report creation |
| Unity compile + focused new and legacy NUnit | **NOT RUN** |
| Full EditMode + PlayMode | **NOT RUN** |
| Real chunk height hashes against accepted saved profile | **NOT RUN** |
| Actual Player/Editor performance and visual capture | **NOT RUN** |
| Safe root release/Merge | **NOT ATTEMPTED** |

Keep Q18 issue **OPEN** and Q18 PR **DRAFT** until Q04
is accepted and a real Unity/root owner verifies the exact height
regressions. Do not overwrite or coordinate other agents' scarp files.
