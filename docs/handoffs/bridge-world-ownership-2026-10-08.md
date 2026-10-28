# Bridge and concept-world work ownership

Requested workers: gpt-5.6-sol, xhigh. Shared workspace; no agents may edit
another owner's files without explicit reassignment from the primary agent.
Existing user changes in game scenes, materials, settings, rendering and editor
files are preserved. No commits, resets or changes to production imports.

| Owner | Exclusive files | Public contract authority |
|---|---|---|
| bridge_art | `../Little-Castle_Assets/Source/Architecture/Bridge_Stone_A/v002/**`, except `bridge_contract.py` | Mesh names/materials/LOD/export structure; keep site dimensions/profile/sockets fixed. No AssetBook or Releases changes. |
| fixed_crossings | `Assets/_Game/Scripts/World/Macro/BridgeSitePlanner.cs`, `BridgePlannerSettings.cs`, `WorldBridgeSiteData.cs`; new `FixedBridgeSiteProfile.cs`; `Assets/_Game/Scripts/World/Generation/Stages/FixedBridgeTerrainStage.cs`; `MacroFeatureProjectionStage.cs`; new `Assets/_Game/Tests/Editor/FixedBridgeGenerationTests.cs`; `docs/architecture/fixed-bridge-crossings.md`; corresponding `.meta` | Add opt-in fixed bridge contract, plain baseElevation/contractVersion data, deterministic stamp/planner/masks; preserve legacy defaults and signatures. No layer terrain edits. |
| landforms | `Assets/_Game/Scripts/World/Generation/Stages/LayeredTerrainStage.cs`; new `Assets/_Game/Scripts/World/Generation/StylizedLandformSettings.cs`, `StylizedLandformSampler.cs`; new `Assets/_Game/Tests/Editor/StylizedLandformTests.cs`; `docs/architecture/stylized-landforms.md`; corresponding `.meta` | Add opt-in heightfield landform settings/sampler; preserve legacy output by default. No bridge/river/planner/schema edits. |
| primary | This file; `../Little-Castle_Assets/Source/Architecture/Bridge_Stone_A/v002/bridge_contract.py`; AssetBook registration; new world-concept handoff/report; new independent profile creation editor utility; integration/verification tools and aggregate report | Pipeline/profile wiring and final cross-contract reconciliation only, after agent work is ready. Existing dirty settings/assets remain untouched. |

Dependency graph:

```text
existing deterministic world pipeline
  + landforms: opt-in plains/hills/scarp height sampler
  + fixed_crossings: macro sites -> fixed profile -> chunk stamp/masks/spawn
  + bridge_art: measured v002 meshes / LODs / sockets
       -> primary: reconcile model/profile, create optional profile, validate/report
```

Art and code agree on local +Z road, +X river, 10.8 m length, 2.86 m clear path,
3.6 m structural width / 3.78 m cap extent, crown Y=1.05, input sockets
(0,0,+/-5.4), water Y=-.85, support rectangle half extents X=7/Z=8.
Authoritative v002 functions are in the source-art `bridge_contract.py`.
Two flags are seated on the caps (X=+1.64,Z=-5.12) and
(X=-1.64,Z=+5.12), one to the traveler's right at each entrance.

Each agent reports changed files/classes, public API changes, tests and actual
results, remaining risks/limits, and any integration request. Minimum tests:
art FBX roundtrip/dimensions/UV/normals/LOD/flags plus visual inspection;
crossings same-seed/negative seam/yaw/no-stretch/grade/masks/legacy output;
landforms same-seed/changed-seed/negative borders/finiteness/legacy mode and
terrain distribution metrics. Unit tests are not visual approval.

No watermills in this scope. Full-map GameObject generation is forbidden;
macro planning remains finite and detailed presentation remains streamed.

## Ownership amendment: crossing coherence

Before follow-up dispatch, `MacroWorldPlanner.cs` and `MacroWorldPlan.cs` in
`Assets/_Game/Scripts/World/Macro/` are assigned exclusively to fixed_crossings.
The agent may pass the existing terrain probe to bridge planning and add a
fixed-mode-only rejection pass/removal API for road edges with unserved river
crossings. Legacy planning must remain unchanged. Removed roads must not leave
orphan bridge sites or pretend to be usable connectivity. Tests must cover a
road with both valid and rejected crossings, and the legacy opt-out. Re-routing
and guaranteed global connectivity remain future work, explicitly documented.
Primary also owns new `Assets/_Game/Editor/ConceptWorldProfileBuilder.cs`, its
meta, `Assets/_Game/Tests/Editor/ConceptWorldProfileTests.cs`, its meta, and the
new isolated `Assets/_Game/Settings/World/ConceptWorld_v001/**` output.
