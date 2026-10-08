# Sol 6 High: follow-ups after desktop verification

Base: **integration/desktop-acceptance-2026-10-08 at 66e9a414e474cca526747e65aa2d58a0806760aa**, draft PR #20, not old main/current-unity-fix.
Executor: `gpt-6-sol`, reasoning `high`, selected manually. GitHub issues do not start or configure a model automatically.
Read AGENTS.md and docs/reports/desktop-acceptance-2026-10-09.md first.

| Task | Exclusive owner | Parallel/dependency |
|---|---|---|
| A river diagnostics | RiverNetworkPlanner/Settings, new diagnostics/tests, concept macro settings | Parallel B/C/E; API additive only |
| B realizable connectivity | BridgeAwareRoutingPlanner, RoadNetworkPlanner, MacroWorldPlanner, BridgeAwareRoutingTests | Parallel A/C/E; integration retest after A |
| C terrain presentation masks | new TerrainPresentationMaskUtility/tests; optional ChunkMeshBuilder overload | Parallel A/B/E; D hooks its API |
| D real streamed concept scene | WorldStreamer hook, new concept scene builder/profile/scene/PlayMode tests | After A+B+C; one scene owner; measured acceptance follows |
| E macro probe memory/time | WorldTerrainProbe and new focused tests/audit | Parallel A/B/C; public probe API/output frozen |

Each detailed issue is sourced from the matching file in docs/tasks/sol6-high-2026-10-09/.
GitHub queue: A [#22](https://github.com/DimaFi/Little-Castle/issues/22), B [#23](https://github.com/DimaFi/Little-Castle/issues/23), C [#25](https://github.com/DimaFi/Little-Castle/issues/25), D [#24](https://github.com/DimaFi/Little-Castle/issues/24), E [#21](https://github.com/DimaFi/Little-Castle/issues/21).
No foreign-file edits without reassignment; public cross-task contracts must be proposed before mutation. New files require .meta where appropriate.
Keep finite maps, seed/negative-coordinate determinism, legacy-off behavior, Built-in, data/presentation separation, lazy materialization and shared materials. No watermills, full-map GameObjects, resource equality injection, phantom river/roads, bridge scaling, URP/HDRP or silent Main* changes.
Fixed bridge: 10.8m, clear2.86m, scale1, road+Z/river+X, sockets±5.4m, waterY=base−.85.

Cloud report: commits, exact owned files/API/dependencies, tests actually run, commands for unavailable Unity, honest PASS/FAIL/pending. Do not copy historical 120/120 as new results; do not close umbrella #8/#10/#11/#12 prematurely.
Local continuation is reserved to root: existing ConceptBridgeSceneBuilder, ConceptBridgeTest and BridgeValidation fixture files. Do not change these from task D; create a separate procedural scene.
