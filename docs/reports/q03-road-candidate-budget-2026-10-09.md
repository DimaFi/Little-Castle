# Q03 — Bounded realized-road connectivity candidate preparation

Date: 2026-10-09. Owner: GPT-6 ChatGPT. Work item:
[issue #32](https://github.com/DimaFi/Little-Castle/issues/32).
**Review branch:** `gpt6/q03-bounded-road-candidates-2026-10-09`;
**base:** `codex/concept-world-pass-2026-10-09` at
`e6a02df6f57461df9cd7cf853e8e9d62b776d71a`.
This is a narrowly scoped **source candidate for Codex desktop verification**.
It is **not** a Unity-compiled or Player-approved change.

## Why this patch exists

`RealizedRoadConnectivityRepair.Repair` previously allocated one candidate
per eligible cross-component feature pair, then sorted the entire list.
For N disconnected features that can all reach each other within the allowed
distance, candidate storage grew as N × (N−1)/2 even though the strict repair
can attempt only 0–32 alternative routes. This is especially wasteful during
macro-world session bootstrap.

## Changed behavior and boundaries

- Candidate preparation sorts point features by world X/Y/stableId, uses a
  conservative X sweep for `maxConnectionDistance > 0`, and evaluates exact
  squared 2D distance before admitting the pair. When max distance is 0
  (unrestricted, the existing meaning), there is no distance pruning.
- The **best 512 eligible pairs** are retained in a sorted bounded buffer,
  ranked exactly as before by `(distanceSquared, fromStableId, toStableId)`.
  Pair IDs are canonicalized by stable IDs regardless of spatial traversal.
  Rejected IDs, already materialized road IDs and already connected feature
  components are excluded before entering the buffer.
- On overflow, the most expensive eligible pair is discarded. The buffer is
  a source preparation limit, **not** a higher route-attempt budget. Existing
  `maxConnectivityRepairCandidates` continues to govern attempts (0–32).
  If many top pairs are later redundant or physically unrealizable, more
  distant pairs may not be available; **do not present a remaining disconnected
  graph as valid**.
- `Result` now records `candidatePairChecks`, `eligibleCandidatePairs`,
  `preparedCandidateCount`, `discardedCandidatePairs`,
  `candidateSelectionTruncated`, `attemptBudgetExhausted`, and at most
  32 `attemptedRoadIds`. The existing `Summary` includes the counts and
  exhaustion flag. `candidateSelectionTruncated` explicitly exposes a
  potentially incomplete candidate search even when the attempt cap is not hit.
- The strict bridge-aware opt-in gate, actual path/bridge validator, road
  materialization, stable hashes, and final disconnected/minimum-bridge
  validation are **unchanged**. No bridges, rivers or resources are invented.
  The scene, stream manager, Main presets and all art are untouched.
- Storage: candidate pool O(512), spatial feature index O(N), union-find O(N).
  Pair comparison CPU: O(N log N + P) for the spatial sort/sweep, where P is
  the number of pairs whose X distance is within range; in dense or unbounded
  cases P is still O(N²). **This is not a claim of constant-time bootstrap.**
  Sorted insertion shifts up to 512 elements per competitive replacement.
  Unity profiling is required before additional indexing/algorithm changes.

## Source changes

1. `Assets/_Game/Scripts/World/Macro/RealizedRoadConnectivityRepair.cs`:
   bounded ranked pool, optional spatial cutoff and explicit diagnostics.
2. `Assets/_Game/Tests/Editor/RepairCandidateBudgetTests.cs` and its
   persistent Unity `.meta`: synthetic 100/1000 disconnected-feature grids,
   deterministic stable-ID tie on a square under reversed insertion order,
   1000 isolated features with tiny distance radius (spatial pruning),
   zero attempt budget, and an invalid but materialized road that must not
   disappear or be reported as valid.
3. This handoff report.

For fully eligible grids the expected source-test counts, **not measured runs**,
are 4,950 pairs / 4,438 discarded (N=100) and 499,500 pairs /
498,988 discarded (N=1000), with 512 prepared. The candidate attempt
cap is deliberately just 1 in the dense fixtures to avoid testing many
full terrain paths in this memory-bound regression.

## Verification status (do not promote historical evidence)

| Gate | Status |
|---|---|
| Source inspected against Q03 ownership and existing production API | DONE |
| New NUnit fixture authored, stable Unity .meta committed | DONE |
| Unity 6000.5.5f1 C# compile | **NOT RUN** |
| Focused `RepairCandidateBudgetTests` EditMode | **NOT RUN** |
| Existing `RealizedRoadConnectivityRepairTests` + `BridgeAwareRoutingTests` | **NOT RUN** |
| Full EditMode / PlayMode | **NOT RUN** |
| 1024/3072 m actual macro seeds and negative-coordinate routes | **NOT RUN** |
| Profiler allocations, bootstrap CPU, Player memory/FPS | **NOT RUN** |

Existing desktop baseline `175/175 EditMode`, `5/5 PlayMode` belongs
to an earlier code revision reported in
`docs/reports/concept-pass-1-5-2026-10-09.md`, **not to this patch**.

## Codex desktop acceptance handoff

Use a separate working tree of this branch based on the pinned source;
avoid stale `main` and do not merge draft PRs automatically.
In PowerShell, with `$unity` set to the Unity 6000.5.5f1 editor binary and
`$project` to this repository root, execute:

```powershell
& $unity -batchmode -nographics -projectPath $project -runTests -testPlatform EditMode -testFilter "LittleCastle.Tests.RepairCandidateBudgetTests" -testResults "$project/Logs/Q03-Focused-EditMode.xml" -logFile "$project/Logs/Q03-Focused-EditMode.log"
& $unity -batchmode -nographics -projectPath $project -runTests -testPlatform EditMode -testResults "$project/Logs/Q03-Full-EditMode.xml" -logFile "$project/Logs/Q03-Full-EditMode.log"
& $unity -batchmode -nographics -projectPath $project -runTests -testPlatform PlayMode -testResults "$project/Logs/Q03-PlayMode.xml" -logFile "$project/Logs/Q03-PlayMode.log"
```

Do not append `-quit` to Unity's `-runTests`; parse NUnit XML, compiler
errors and log results rather than trusting only the process return code.
Run tests sequentially, with no concurrent Unity process modifying the
checkout.

Then run the existing desktop routing matrix on the **concept-only** saved
settings for seeds `12345`, `54321`, `-10101`, `777` and
1024/3072 m as accepted by the integration owner. Explicitly compare:
- original baseline vs Q03: candidate ordering / attempted IDs on small
  source plans, paths, final components, rejected/missing realized roads;
- candidate eligible/prepared/discarded counts, 0-budget behavior and
  whether 512 truncation prevents a formerly reachable repair;
- wall-clock session bootstrap cost, allocations and peak memory; do not
  extrapolate Editor measurements into Player FPS or 2/8/16-player
  accessibility;
- strict bridge minimum and fixed-site geometry still reject invalid worlds.

If any formerly successful plan becomes disconnected because of the bounded
512 pool, report it as FAIL and escalate the pool/search policy to the root
architecture owner; **do not silently relax strict constraints**. A bounded
top-K nearest-candidate iterator, spatial neighbor index or component-aware
priority frontier can be evaluated separately with real profiler evidence.

After test execution, publish exact commit SHA, Unity version, device,
repro command, NUnit XML counts, per-seed PASS/FAIL, before/after timing and
memory. **Only Codex desktop may mark this issue accepted after those gates.**
