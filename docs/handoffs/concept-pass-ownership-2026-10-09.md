# Concept pass1–5: exclusive ownership

Base97856fe, branch codex/concept-world-pass-2026-10-09. Root launches Unity/Git
and owns all scene integration, visual evidence and future Chat queue.

Dependency graph: river eligibility A + river-bend contract B -> root integration
and tests -> runtime/GPU scene -> materials/content tuning. A and B are parallel.
No Unity compilation/test run until every source owner has stopped editing.

| Owner | Exact writable files/classes | Contract/dependency |
|---|---|---|
| A, river eligibility | Scripts/World/Macro/RiverNetworkPlanner.cs, RiverPlannerSettings.cs; new RiverSourceFallbackTests.cs/meta; new Editor/ConceptRiverSourceAudit.cs/meta; Settings/World/ConceptWorld_v001/ConceptMacroWorldPlannerSettings.asset; docs/reports/concept-river-source-pass-2026-10-09.md | Legacy default off. Natural downhill tracing only, bounded attempts, stable IDs. Root applies strict bridge settings after A finishes. No changes to heights, roads, shaders or scene |
| B, river bends | Scripts/World/Generation/Stages/RiverTerrainCarvingStage.cs; concept Stages/02_RiverTerrainCarving.asset; new RiverBendEnvelopeTests.cs/meta; docs/reports/concept-river-bend-pass-2026-10-09.md | Opt-in any-segment carve envelope matching existing concept SurfaceStage and wetness masks. Old nearest-only default retained. No source settings, SurfaceStage, water/shader/scene edits |
| C, village supports | new Scripts/World/Rendering/ConceptVillageFootprintPresenter.cs/meta; new Tests/Editor/ConceptVillageFootprintTests.cs/meta; docs/reports/concept-village-footprints-2026-10-09.md | Additive concept-only component, approved prefab references supplied by root. Terrain-only support rays, bounded wait, fail closed on unsafe footprints. No terrain regeneration or edits to spawn presenter |
| B follow-up after release | Scripts/World/Generation/Stages/TerrainSurfaceStage.cs; concept Stages/07_TerrainSurface.asset; new Tests/Editor/TerrainSurfaceBroadPhaseTests.cs/meta; docs/reports/concept-surface-broadphase-2026-10-09.md | Actual Play worst-stage107.856ms triggered opt-in local segment broad phase. Exact enabled/disabled surfaces and first-road priority preserved; root owns timing rerun, no Unity while editing |
| root | Validator acceptance, ConceptProceduralWorldSceneBuilder, new concept rendering/runtime/Editor helpers and tests, isolated catalog/materials/scene/config not owned by A/B, reports/docs/tasks | No rewriting Main, shared shaders, art release or user scenes. Actual terrain/data and approved prefabs, not fake procedural screenshot |

All paths relative to Assets/_Game unless docs/. Public APIs additive only;
propose cross-owner contract changes first. No foreign edits without reassignment.
No agents spawn other agents, launch Unity, commit or push. Reports include exact
files, tests added/actually run, root-run commands and honest pending items.
Minimum tests A: repeatability, negative coordinates, disabled legacy identity,
attempt budget, natural no-valid-source failure. B: sharp bend, uniform legacy,
negative chunk border, concept-only flag. Root: combined Edit/Play and real GPU.
