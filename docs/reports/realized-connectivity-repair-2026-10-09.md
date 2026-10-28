# Realized feature-graph connectivity recovery — source patch + Codex desktop verification

**Date:** 2026-10-09
**Author:** GPT-6 in ChatGPT, continuing the user-assigned Sol 6 / High game development queue.
**Game base:** DimaFi/Little-Castle, branch `integration/desktop-acceptance-2026-10-08` at **`deaf52c595747f3d9dedf6a48b455960f0ca4bdd`** (desktop PR #20), NOT old `main`.
**Work branch:** `gpt6/realized-network-repair`.
**Scope:** Issue #8, strict opt-in bridge-aware routing. The tested desktop integration branch itself is NOT modified.
**Status:** SOURCE + TEST CANDIDATE. **UNITY COMPILE / EDITMODE / PLAYMODE / ACTUAL SEED MATRIX NOT RUN BY GPT-6** on this branch.

## Evidence motivating this change (from DESKTOP PR #20, not our tests)

The existing [desktop acceptance PR #20](https://github.com/DimaFi/Little-Castle/pull/20) reports **120/120 EditMode and 3/3 PlayMode** on its own reviewed head, but failed strict concept macro routing in three of four measured seed/area cases:

| Seed / width | Actual desktop observation | Result |
|---|---|---|
| 12345 / 1024m | 0 rivers, 0 bridges, connected graph | FAIL: strict minimum 1 bridge, natural no-river seed; must NOT fabricate river |
| 54321 / 1024m | 0 rivers, 0 bridges, connected graph | FAIL: strict minimum 1 bridge, natural no-river seed; must NOT fabricate river |
| -10101 / 1024m | 2 rivers, 1 valid bridge, 2 graph components, 2 rejected original desired edges | FAIL: disconnected feature graph |
| 12345 / 3072m | 1 river, 2 valid fixed bridges, connected | PASS for that macro plan only |

Those desktop results do not prove player count / finite-session fairness. The failing 2-component case motivates **alternative-realized-edge recovery**, not a fake minimum-bridge relaxation.

## Problem and source-level change

Before this patch, `MacroWorldPlanner`:
1. emitted a nearest-neighbor logical feature backbone;
2. solved A* roads;
3. removed roads crossing rivers without valid authored fixed stone bridge;
4. retried only **original rejected** edges. If a required original road was topologically blocked but a different pair of nearby settlements could connect surviving graph components, it was never tried. The strict validator then rejected the entire graph.

New `RealizedRoadConnectivityRepair.Repair` runs **only when** existing strict opt-in flags are set (`enableBridgeAwareRouting` and `requireConnectedFeatureGraph`) and initial graph really has more than one component:
- Build connected components with deterministic disjoint sets using `plan.Roads` stable IDs matched to `plan.RoadConnections`. The aspirational connection list alone cannot claim access.
- Enumerate world-feature pairs **across different realized components** within existing `RoadNetworkPlannerSettings.maxConnectionDistance` (0 still means unbounded, matching original network semantics); sort by squared world distance then stable feature IDs. No UnityEngine.Random.
- Respect `BridgePlannerSettings.maxConnectivityRepairCandidates` (default 12, clamped 0–32; zero disables). Skip previously failed original connection IDs from the first retry pass, and skip existing realized road IDs to avoid wasting budget.
- For each candidate, reuse existing `BridgeAwareRoutingPlanner.RecoverRejectedConnections`, which runs bounded terrain-aware A* / optional guided bridge socket candidates, and rejects any invalid fixed span/angle/river width/grade/footprint or unsupported water crossing. Do NOT create a road edge until the unchanged planner actually accepts a matching `WorldRoadData`.
- Add accepted alternative edge and update connected components, stop at one component or exhausted cap. Stable IDs use exact `DeterministicHash.StablePairId(seed, featureA, featureB, "road_network_connection")` convention.
- Keep the original failure when the physical network **still cannot** be connected, fixed bridge minimum remains unmet, or site/crossing validator rejects geometry. If a rejected *specific* original edge is safely bypassed by a valid alternative and the realized graph is fully connected, do not mislabel that original dead-end as a bootstrap fatal error.
- Expose `originalRejectedConnections`, `repairInitialComponents`, `repairAttemptedCandidates`, `repairAcceptedConnections`, `repairPathAttempts` in `WorldRouteConnectivityValidator.Report.Summary` for diagnostics and user-facing issue reports.

**No changes** to original fixed `BridgeSitePlanner`, `FixedBridgeSiteProfile`, immutable v002 source/LOD, streamed water owner, shaders, trees, resource placement, Main* profiles, scenes or `WorldStreamer`. Built-in renderer and gameplay/world seed identity preserved.

## File inventory

- `Assets/_Game/Scripts/World/Macro/RealizedRoadConnectivityRepair.cs` — new data-only bounded secondary graph repair.
- `Assets/_Game/Scripts/World/Macro/BridgePlannerSettings.cs` — new opt-in candidate cap (when strict mode enabled).
- `Assets/_Game/Scripts/World/Macro/MacroWorldPlanner.cs` — wire recovery after old rejection/route retries; don't fail solely on a rejected original edge if an actual alternative graph is now connected.
- `Assets/_Game/Scripts/World/Macro/WorldRouteConnectivityValidator.cs` — counters + summary, without weakening geometry/bridge validation.
- `Assets/_Game/Tests/Editor/RealizedRoadConnectivityRepairTests.cs` — new source NUnit regression fixtures and deterministic flat data.
- Stable `.meta` files for both new C# sources, committed as text.
- This report.

## Regression test source (NOT YET EXECUTED)

- Alternative real path connects two originally disconnected groups of 4 point features, makes 3 realized roads / 1 connected component, does not manufacture a bridge where no river exists, and chooses same stable alternate edge when features are added in reversed order.
- Repeated repair of a now-connected graph is idempotent.
- A too-small permitted max connection distance preserves a disconnected explicit failure and adds no road/bridge.
- Candidate cap=0 refuses to modify graph; rejected stable edge IDs stay skipped and cost no path attempts.
- Even after reconnecting a river-free world, `minimumFixedBridgeCount=1` still fails correctly instead of granting an imaginary crossing.

These source tests are not a substitute for real macro generation with 2/8/16 players, long negative-coordinate river meanders, real authored site snap and Unity profiler performance.

## Codex: EXACT next working acceptance procedure

Start from the **actual tested integration PR #20 branch**, not old `current-unity-fix`, to avoid repeating old work. Read PR #20 logs/report, this patch PR and its diff, `AGENTS.md`, issue #8, existing `docs/architecture/bridge-aware-routing.md`, and issue #19. Stage changes in a dedicated local reviewed branch/worktree; do not merge drafts blindly.

1. Unity 6000.5.5f1 compile with exact current source. Full EditMode and PlayMode NUnit; execute specifically `RealizedRoadConnectivityRepairTests` and original `BridgeAwareRoutingTests`. Record new XML with pass/fail/ignored totals. Do not reuse PR #20's 120/120 or 3/3 as if they passed this patch.
2. Re-run `LittleCastle.Editor.DesktopRoutingAudit.RunDesktopMatrix` on isolated strict settings: seeds **12345, 54321, -10101, 777**, width **1024 and 3072 m** (and any real presets user supports). Measure `RouteDiagnostics.Summary`, components before/after, accepted alternates, candidate/path attempt count and wall-clock/GC costs. Assert exactly zero orphan bridges/unbridged realized crossings/missing realized roads.
3. For `-10101/1024`, verify if accepted alternate edges reduce 2 components to 1; if still FAIL, inspect actual source river bank/road grade/maximum distance, and save failure diagnostics before further changes. **Do not promise that any seed becomes valid without measured evidence.**
4. Seeds 12345/54321 with 0 naturally generated rivers MUST still fail the artificial strict min-one-bridge check. Decide product-level behavior: accept natural no-river maps as a valid **separate opt-in preset** with bridge minimum=0, or reject/move to a user-visible seeded map retry. Do NOT generate phantom rivers/bridges, silently lower requested rules, or claim their FAIL is a routing regression.
5. Benchmark cost on 3072m macro plan; pair enumeration is O(featureCount²), at most 32 alternative *candidate evaluations* each bounded by existing A*. If too slow, instrument before optimizing/indexing; don't move Unity API to worker threads blindly.
6. Run actual finite session 2/8/16-player spawn/accessibility map-size matrix with reachable exits/roads (as distinct from graph of generated neutral features), negative borders, and streaming reload stability.
7. Submit new dated `docs/reports/desktop-routing-repair-YYYY-MM-DD.md` with per-case **PASS/FAIL/BLOCKED**, added/rejected IDs and routing/bridge invariants, timings, NUnit XML/log paths, exact hardware+Unity+source SHA, fixes made, and proper handoff. Keep original 2026-10-08 reports immutable.

## Safety and honest limitations

- A candidate budget of 12 (maximum 32) is a bounded cost, **not a proof of globally optimal navigability**. Higher values can cause synchronous macro planning hitches; profile before enabling in production.
- River count cannot be created by pathfinding. Missing river for a strict bridge minimum is an unsatisfied input/world constraint, not something to hide.
- Graph of features does NOT by itself guarantee every 2/8/16-player start location can reach an enemy or neutral market. Needs runtime spawn/nav tests.
- No GPU/Unity results were produced by GPT-6 for this newly opened branch. This source patch is **draft, not Ready/merged**.
