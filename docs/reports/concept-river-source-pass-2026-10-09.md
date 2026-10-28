# Concept river source pass — 2026-10-09

## Outcome

Implemented a concept-only, opt-in natural-source fallback for maps where the
ordinary river source pass produces no river. The fallback does not create a
channel or change terrain. It selects deterministic source candidates, calls
the existing `TraceRiver` downhill tracer, applies the existing flow/width/depth
profile builder, and retains an explicit zero-river result when no trace has the
configured minimum point count and real source-to-terminal height loss.

The earlier Unity eligibility audit remains the reason for this policy change:
at 1024 m, saved seeds `12345` and `54321` had no ordinary eligible sources.
Their in-bounds candidates were rejected by the RollingHills/Highlands class
gate; admitting Plains alone moved the rejection to the saved 8 m height gate.
Increasing chance alone did not recover seed `12345`.

## Saved concept policy

Only
`Assets/_Game/Settings/World/ConceptWorld_v001/ConceptMacroWorldPlannerSettings.asset`
opts in:

- ordinary policy remains spacing 480 m, chance 0.65,
  RollingHills|Highlands, height at least 8 m;
- fallback spacing is 320 m;
- at most 24 in-bounds candidates are sampled and at most 24 traces can run;
- fallback eligibility is Plains|RollingHills at height at least 1 m, matching
  the stylized terrain's 3 m base-height lowlands rather than assuming 8 m
  highland sources;
- the existing 32-degree source slope limit is retained;
- a fallback trace must lose at least 0.5 m from source to terminal point;
- the first accepted genuine downhill route ends fallback work, so this is not
  a many-river or whole-map probing pass.

`Main` and all other saved macro profiles remain unchanged because the new
runtime setting defaults off.

## Contracts

- Existing five-argument `RiverNetworkPlanner.BuildRivers` remains unchanged.
- New six-argument overload accepts `RiverPlannerDiagnostics`; collection is
  observational and does not consume random values or affect ordering.
- With `useNaturalSourceFallback == false`, the ordinary source loop, trace
  acceptance, insertion order, IDs, centerlines, confluences, flow, width, and
  depth outputs follow the legacy path exactly.
- Fallback runs only when the plan has zero rivers after the ordinary pass.
- Candidate traversal is a deterministic permutation of a world-aligned grid.
  No `UnityEngine.Random` state is used.
- Ordinary IDs keep the established `river_source` salt. Fallback IDs use the
  versioned `river_source_fallback_v1` salt plus world seed and source-grid
  coordinates. This prevents collisions while making the new ID policy
  explicit. Enabling fallback intentionally changes only formerly empty concept
  worlds; legacy/default-off worlds retain their previous output.
- No bridge validity, bridge minimum, road solver, seed replacement, terrain
  height, or carving policy was changed.

## Files

- `Assets/_Game/Scripts/World/Macro/RiverPlannerSettings.cs`
- `Assets/_Game/Scripts/World/Macro/RiverNetworkPlanner.cs`
- `Assets/_Game/Tests/Editor/RiverSourceFallbackTests.cs`
- `Assets/_Game/Tests/Editor/RiverSourceFallbackTests.cs.meta`
- `Assets/_Game/Editor/ConceptRiverSourceAudit.cs`
- `Assets/_Game/Editor/ConceptRiverSourceAudit.cs.meta`
- `Assets/_Game/Settings/World/ConceptWorld_v001/ConceptMacroWorldPlannerSettings.asset`
- `docs/reports/concept-river-source-pass-2026-10-09.md`

The audit writes its execution result to
`docs/reports/concept-river-source-pass-2026-10-09.json`. That JSON is not
fabricated by this source-only pass; it is created by the pending Unity audit.

## Tests added

`RiverSourceFallbackTests` covers:

1. fallback-disabled legacy output identity, including exact river IDs,
   centerlines, confluence fields, flow, widths, and depths while all fallback
   parameters differ;
2. same-seed repeatability in wholly negative source bounds;
3. the configured candidate/trace attempt cap;
4. flat terrain producing explicit zero rivers rather than a fabricated route;
5. recovery of one real downhill river, including the existing flow/width/depth
   profile, when ordinary coarse source eligibility misses.

## Saved-source audit

`LittleCastle.Editor.ConceptRiverSourceAudit.Run` runs only the saved river
settings for seeds `12345`, `54321`, `-10101`, and `777` at a centered 1024 m
playable map plus the saved 384 m planning halo. It does not invoke settlements,
road planning, bridges, carving, chunk rendering, or the full macro planner.
The JSON includes ordinary eligibility counters, bounded fallback rejection and
acceptance counters, actual river count/IDs/endpoints/net drop, and per-seed
river-build wall time.

## Verification status and root commands

Source review and `git diff --check` completed without whitespace errors. Per
the ownership handoff, this agent did not launch Unity and did not claim runtime
results. The focused tests and saved-seed audit are pending root execution after
all source owners release files.

Focused EditMode command:

```powershell
& $UnityEditor -batchmode -nographics -quit `
  -projectPath "E:\Games_Develop\Little-Castle" `
  -runTests -testPlatform EditMode `
  -testFilter "LittleCastle.Tests.RiverSourceFallbackTests" `
  -testResults "E:\Games_Develop\Little-Castle\TestResults-river-source.xml" `
  -logFile "E:\Games_Develop\Little-Castle\river-source-tests.log"
```

Saved-source audit command (run separately, after compilation/tests finish):

```powershell
& $UnityEditor -batchmode -nographics -quit `
  -projectPath "E:\Games_Develop\Little-Castle" `
  -executeMethod LittleCastle.Editor.ConceptRiverSourceAudit.Run `
  -logFile "E:\Games_Develop\Little-Castle\concept-river-source-audit.log"
```

Pending acceptance is the actual JSON result for all four seeds, focused and
full EditMode results, then the root-owned combined macro/bridge validation.
Zero rivers for an individual saved seed remains a valid explicit result when
all bounded candidates fail natural tracing; it must not be converted to a
synthetic river or weaker bridge requirement.
