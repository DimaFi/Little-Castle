# [Sol 6 High][P1] Reconnect surviving realizable road components after rejected crossings

Executor gpt-6-sol/high, manual selection. Base66e9a41 in integration/desktop-acceptance-2026-10-08 (PR#20). Read AGENTS, follow-up handoff, desktop acceptance and bridge-aware-routing architecture. Follow-up #8.

Evidence: -10101/1024m produces34 roads,2 rivers,1 bridge but2 graph components;2 requested edges exhaust12 bounded retries. The new deterministic logical backbone is not sufficient when terrain rejects its physical paths.

## Exclusive files/classes
Macro/BridgeAwareRoutingPlanner.cs, RoadNetworkPlanner.cs, MacroWorldPlanner.cs under Assets/_Game/Scripts/World; Tests/Editor/BridgeAwareRoutingTests.cs; new docs/reports/realizable-connectivity.md. New helper/test files only in this scope with meta.
Public contract: retain existing APIs/stable IDs; additive optional recovery settings only via new owned recovery type, not BridgePlannerSettings or MacroWorldPlan without reassignment. WorldRouteConnectivityValidator/FixedBridgeSiteProfile/RiverNetworkPlanner are read-only; strict errors must remain.

Build components from validated realized roads/sites. Try deterministic closest feasible inter-component candidates, avoiding already-failed pairs and respecting width/angles/grades/sockets/overlap. Globally bound candidates/attempts and stop on explicit failure. No ghost edges, fabricated sites, erased diagnostics or infinite retries. Legacy-off unchanged.

Minimum tests: logical connected but physically split, alternate feasible pair, impossible terrain bounded failure, stable candidate ties/insertion order, negative coordinates, no orphan/unbridged accepted crossings, legacy regression. Actual seed -10101/1024m and A's river seeds after integration. Keep missing requested edges distinct from accepted edge counts.
Parallel A/C/E; A owns river settings, no shared edits. D follows A+B+C. Report paths/API, attempts/reasons, actual route counters, wall-time, tests/commit and unresolved cases; no Unity means desktop verification pending.
