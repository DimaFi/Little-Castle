# Q17 — House geometry / LOD art-intake gate v1

Date: 2026-10-09. Source contributor: GPT-6 ChatGPT.
Issue: [#46](https://github.com/DimaFi/Little-Castle/issues/46).
Work branch: `gpt6/q17-house-lod-budget-audit-2026-10-09`.
Base: `codex/concept-world-pass-2026-10-09` at
`e6a02df6f57461df9cd7cf853e8e9d62b776d71a`.
**Status: read-only Editor source and NUnit tests authored; Unity execution pending.**

## Known real source baseline — NOT newly measured

The earlier desktop report
`docs/reports/concept-pass-1-5-2026-10-09.md` recorded the approved
concept house triangle counts after Unity import:

| Imported tier | Desktop baseline triangles | Retained from LOD0 |
|---|---:|---:|
| LOD0 | 143,672 | 100% |
| LOD1 | 55,764 | ~38.8% |
| LOD2 | 22,794 | ~15.9% |

The portable prefab is
`Assets/_Game/Art/Imported/ConceptWorldKit_v001/Prefabs/house.prefab`.
Its committed LODGroup YAML declares transition screen heights
`0.32 / 0.075 / 0.005` (1080px display: about **345.6 / 81 / 5.4px**
at the transition). Those are serialized thresholds, not a measured
camera distance, FOV, world-space footprint, or proof of LOD switching in
a rendered Player frame.

**Problem:** the 22,794 triangle far house is still expensive when
many houses are visible at once. As a simple geometric upper-level
illustration, 50 identical LOD2 instances submit
`50 × 22,794 = 1,139,700` triangles before overdraw, shadows,
materials and engine batching; this is **not** a measured GPU workload
or a confirmed in-game house count.

`ProductionAssetValidator` reporting zero errors verifies a structural
contract, not a release-worthy art performance budget. House LOD is
an authored Blender/art-agent task, never runtime mesh decimation.

## New read-only audit

`Assets/_Game/Editor/HouseAssetBudgetAudit.cs` provides two menu actions:

1. **Little Castle > Assets > House LOD Budget > Audit Approved Concept House**
2. **Little Castle > Assets > House LOD Budget > Audit Selected Prefab**

The approved option reads the portable house prefab by explicit
`AssetDatabase` path; a missing approved asset throws instead of
falling back to an ignored author's local library. The selected option
accepts a *prefab asset*, not a scene instance. The tool does not
instantiate a GameObject, modify imported geometry, alter materials,
write an asset, or update scene/LOD visibility.

Both emit a UTF-8 JSON report in ignored project-local
`Logs/HouseBudget-YYYYMMDD-HHmmss.json` and console warnings.
Audit fields include:

- prefab source path/name, each LODGroup path, LOD index and screen-relative
  height, approximate pixel transition for a 1080px frame;
- **triangle indices / 3 per triangle-topology submesh** and raw authored
  mesh vertices, counted *per referenced renderer* (repeated renderers
  draw repeated geometry);
- renderer count, submesh count, estimated base-pass submissions as
  `max(submeshes, assignedMaterialSlots)` per renderer, distinct shared
  meshes/materials, missing materials/meshes;
- far renderer cast/receive shadow flags, non-ForceNoMotion motion vectors,
  renderers assigned to several tiers, and unassigned enabled renderers;
- per-LOD remaining triangle fraction, explanatory warnings and
  `requiresArtReview`.

**Important metrics caveat:** `estimatedBasePassDraws` is only an authored
renderer/submesh/material submission estimate. It is not Unity's actual
Batches, SetPass calls, SRP/instancing count, shader pass count, GPU vertex
cost, or frame time. Exact draw calls must be read from Unity Frame Debugger
and Profiler on the **target pipeline/device**, at close/medium/far camera
views. Shadows, depth passes, light loops, batching eligibility, shader
variants and material properties affect real cost.

Preflight *screening suggestions only*, coded explicitly and visible as
warnings:

| Level | Provisional screening warning threshold |
|---|---:|
| LOD0 | 40,000 triangles |
| Intermediate | 15,000 triangles |
| Farthest | 3,000 triangles |

These are **NOT approved product budgets or hard test gates**, and should
be revisited by the root owner after a hardware/device capture. At the
reported desktop baseline all three tiers exceed these provisional
screening values. The art-agent must retain the distinctive house shape
rather than chasing arbitrary numbers.

## Approved art intake v1: what to author, what to preserve

The artist/Astra/Blender agent should deliver separately authored,
optimized meshes with stable pivot, units, world scale, roof normals and
proper shared material mapping. Do not replace or overwrite the portable
runtime approved house before root review.

- **Close (LOD0)**: keep iconic roof silhouette, chimney, major exposed
  support beams, door/entry and clearly visible balcony/porch if the
  approved asset has one. Bake tiny surface detail into allowed textures
  instead of duplicating geometric boards, roof tiles, nails and hidden
  backsides. Simplify unseen interior and buried foundation.
- **Medium (LOD1)**: retain roof outline/ridge, chimney, large walls and
  porch mass. Merge purely decorative micro-components; remove individual
  roof tiles, bolts, tiny interior pieces and tiny plant props. Keep
  window/door color zones by texture or shared material rather than
  high-poly geometry.
- **Far (LOD2)**: **recognizable solid house silhouette** from strategy
  camera, roof, chimney and primary porch outline only; cheap materials,
  no local light/real-time shadow receiving/casting, no unnecessary
  probes/motion vectors. Do not use original high-poly roof tile geometry.
  Farthest LOD may eventually be separate authored village HLOD.
- **Colliders**: gameplay occupancy/selection collider budget is
  independent of renderer LOD. Do not attach a detailed mesh collider to
  each tile/roof beam; retain approved simple BoxCollider footprint
  semantics until the building and entrance validator is integrated.
- **Instancing**: require shared materials and stable mesh/material
  combinations across repeated houses. Do not assume instancing works
  until Frame Debugger confirms actual batching with real tint/emblem
  customization.
- **Textures**: keep sane color space, mips, normal map import and
  consistent material assignments. Don't silently rewrite the shared
  production shader or claim one material is always one GPU draw.

### Silhouette acceptance (manual GPU screenshots required)

Capture a *same-seed, same-camera* view of house at close inspection,
normal RTS zoom and far village; repeat at noon, sunset and night.
Compare old approved house to proposed authored LOD at 1080p and at
project's actual render resolution:
1. Roof outline and ridge remain distinguishable from neighboring
   houses and terrain; doorway/porch doesn't morph into a different shape.
2. Chimney/window mass does not visibly pop at authored transitions;
   don't conceal problems by disappearing the house at an overly
   aggressive screen-height cutoff.
3. The house is visibly in contact with ground; the lowest vertex and
   collision base do not float after LOD switch.
4. LODGroup bounds and yaw are correct; no missing materials, disappearing
   walls, inverted triangles, light leaks or material/pixel flicker.
5. Capture shadow transitions (including far OFF), actual
   Frame Debugger batches/SetPass and Profiler CPU/GPU spikes at camera zoom.
6. With **1, 10, 50, 100 visible houses** (repeat under a realistic
   village+forest composition), record draw calls, triangle/vertex counters,
   resident mesh and texture memory, CPU Render-thread time, GPU time,
   1% low frame time and camera-zoom stutters. These are test scenarios,
   not assertions that the game should render 100 houses today.

Release numeric budgets for each tier should be agreed from this
evidence; don't present placeholder thresholds as measured acceptable FPS.

## Commands and tests for desktop Codex

In Unity 6000.5.5f1, open current reviewed branch and ensure imported
portable house meshes actually resolve.

Batch-mode audit for the portable prefab:

```text
-batchmode -nographics -projectPath <projectRoot> -executeMethod LittleCastle.Editor.HouseAssetBudgetAudit.AuditApprovedConceptHouse -logFile <Logs/Q17-audit.log> -quit
```

Focused Editor NUnit tests:

```text
-batchmode -nographics -projectPath <projectRoot> -runTests -testPlatform EditMode -testFilter LittleCastle.Tests.HouseBudgetTests -testResults <Logs/Q17-HouseBudget.xml> -logFile <Logs/Q17-HouseBudget.log>
```

Run both in **separate Unity processes**; do not append `-quit` to the
`-runTests` command. Read test-results XML and import/audit logs and
record actual pass/fail/skip counts. Test code includes synthetic tier
counts/materials, missing mesh, duplicate assignment, always-on renderer,
multi-renderer mesh reuse, serialization and an imported-asset smoke test.
The latter **skips explicitly** when the portable prefab hasn't been
hydrated/imported — a SKIP does **not** approve the art.

Then run the full existing EditMode and PlayMode suites, including visual
scene spot checks, and compare the current JSON counts to the desktop
baseline above. If different, investigate the actual imported asset version
and exact Git SHA rather than silently declaring the older values correct.
Capture the actual screen-space LOD/silhouette at three camera distances
in a graphical Editor and final standalone Player.

### Root owner handoff / non-goals

- **No FBX/Blender export or prefab/material changes in Q17.**
- No art/quality/performance score can be declared from static geometry
  tests alone.
- Root owner approves updated authored house asset intake and meaningful
  Player budgets; separately assigns art agent to export accepted lower LODs.
- Final integration preserves stable source GUIDs and catalog IDs or
  documents a migration and prefab re-wire.
- Don't reroute model selection to stale `Models/Ready` or the ignored
  `TerrainStarter_v001` authoring library.
- Keep Q17 issue open and PR draft until exact Unity test and GPU/Player
  capture evidence exist.

## Verification status of this source handoff

| Gate | Result |
|---|---|
| Existing source/YAML/Desktop Prepare baseline inspected | **DONE** |
| New Editor audit, tests, Unity .meta and this report authored | **DONE** |
| Source ownership and branch diff inspection | **PENDING at report creation** |
| Unity compilation and focused NUnit | **NOT RUN here** |
| Real portable prefab imported and analyzed in current revision | **NOT RUN here** |
| Frame Debugger, PlayMode, Player GPU/CPU, actual screenshot | **NOT RUN here** |
| Approved new FBX authored LOD release | **NOT PART OF Q17** |

Previous EditMode **175/175** and PlayMode **5/5** are evidence for
another desktop revision only, not newly executed validations on Q17.
