# AGENTS.md — Little Castle

This file defines rules for AI coding agents working in this repository.

## Environment integration authorization — 2026-10-28

The user explicitly authorized completing biome and thin-snow integration,
living river shores, verification and code publication to GitHub main. This
supersedes the older audit-only scope below. Work in this canonical project;
do not create new Unity copies. Preserve user map/draft and unrelated local work.
Publish code through the existing clean Publication worktree: the local-only
c143b4a authored-map/evidence history must not be exported. Commit small verified
steps. Biome state is authoritative/transportable; snow appearance is local.
Keep original source copies until migration has been verified; no deletion now.

## User feedback follow-up — 2026-10-28

Read `docs/handoffs/gpt6-world-editor-next-20261028-ru.md` before continuing.
The next cloud task is A (save and test-current-map workflow), not all listed tasks.
Current follow-up adds visible SceneView flowers using the existing scatter algorithm,
road coverage preservation through river vegetation exclusions, derived UV3 sand/wet
data on terrain, a continuous v2 ford edge, and an initial water/lighting preset.
Do not repurpose authored vertex R/G/B or replace the merged shaders wholesale.
Saved authoring base is separate from any unpublished river draft in the window.
Tests: 301/302 EditMode (the existing generator preset mismatch), 16/16 PlayMode.
See `docs/reports/world-editor-feedback-20261028-ru.md` for actual captures and limits.

## Canonical World Editor integration — 2026-10-27

The user's explicit 2026-10-27 request resumed lighting integration into this
main project, `E:/Games_Develop/Little-Castle`. Current branch:
`codex/world-editor-lighting-integration-20261027`. BrushFoundation `4cc6382`
and LightingIntegration published `42f317c` are read-only sources. Preserve their
workspaces. Use `WorldEditor_20261027.unity` and menu
`Little Castle > World Editor > Open Integrated Scene` for the combined fixture.
See `docs/workflows/test-world-editor-ru.md` and
`docs/reports/world-editor-lighting-integration-20261027-ru.md`.
Latest UI/water acceptance and short user report:
`docs/reports/world-editor-start-here-ru.md` and
`docs/reports/world-editor-final-20261027-ru.md`.
World Editor now has four contextual sections. New rivers use opt-in layout v2
and the owned WaterAO/WaterHDR profiles; legacy v1 records/rocks remain supported.
Do not replace the merged MapRiverPresentation with a source-worktree file.
Final checks: 294/295 EditMode (one unchanged generator preset mismatch),
16/16 PlayMode; native SceneView new-river/Apply/Undo/Redo passed.
Water night tint and synchronous river commit cost remain documented follow-ups.
Spatial GI remains a separate opt-in diagnostic scene. Disabled review drivers
still execute Awake: never leave LightingMeadowReview in the authoring scene.
Biome/snow PR79 has been audited only; full integration requires a separate
user decision after `docs/reports/environment-audit-20261027/report-task02-ru.md`.
Historical pause and source-copy coordination below remain as handoff history.

## Map authoring / lighting coordination — 2026-10-25

Before continuing map tools or integrating lighting with Meadow, read
`docs/plans/map-authoring-and-lighting-coordination-20261025-ru.md`.
This records the user's requested roadmap and parallel work boundaries; it does
not itself resume the paused lighting task below. Map tools live in the URP study
copy; lighting integration is assigned a separate copy, with no whole-file shader
replacement between the divergent projects.

## Paused lighting task — user handoff, 2026-10-25

Read `docs/handoffs/lighting-and-biomes-paused-20261025-ru.md` before resuming
lighting work. The user asked to record the current state and stop this task;
resume lighting development/integration only on a new explicit request.
This pause does not stop independent biome work in another chat.
The lighting benchmark still uses older CottageGarden ground/grass; newer
Meadow/biome authoring lives in `Little-Castle_URP_20261024` and is not merged.
Future work must account for both shader branches rather than overwriting one.

## FIRST PROJECT PREFLIGHT — foliage model/shader compatibility

This is the first rendering/model compatibility check for any coding agent
studying Little Castle. Do this **before refactoring vegetation shaders, changing
tree authoring, regenerating tree models, or assuming the current foliage wind
works on production assets**.

Current foliage motion is GPU vertex deformation in:

`Assets/_Game/Shaders/Foliage/LC_Foliage.shader`

The shader currently combines:

- slow whole-crown sway;
- a spatial vertex wave across the crown;
- small leaf/cluster flutter along leaf normals;
- global wind direction/strength/speed;
- optional automatic local-height mask;
- optional Vertex Color R wind mask;
- the same deformation in the visible and ShadowCaster passes.

The preferred tree structure is:

```
TreeRoot
├── TrunkAndBranches  -> LC_StylizedLit
└── Leaves            -> LC_Foliage
```

A combined trunk+leaves mesh is allowed only when a reliable wind mask exists,
normally Vertex Color R:

- trunk/base = 0;
- rigid branch base = near 0;
- flexible branch tips = intermediate;
- leaves = 1.

### Mandatory first compatibility check

On first project inspection, locate a real current production tree/foliage mesh
and verify the actual mesh data, not only screenshots/concept art.

Run in Unity:

`Little Castle -> Rendering -> Foliage -> Validate Selected Model`

Then verify manually in Play Mode at close and far camera zoom.

The agent must answer these questions before changing the foliage architecture:

1. Are leaves actual vertices/meshes/cards/clusters that the vertex shader can
   displace?
2. Are leaves separated from the trunk, or does a combined mesh have a usable
   Vertex Color R mask?
3. Does the leaf mesh have normals for leaf-normal flutter/lighting?
4. Does it have UV0 for BaseColor/Opacity?
5. Does it have tangents if the material uses a Normal map?
6. Does it have enough vertex distribution for visible non-rigid crown motion,
   rather than only moving as one block?
7. Do visible leaves and animated shadows remain aligned?
8. Does close zoom avoid rubber-like stretching?
9. Does far zoom avoid shimmer/noisy flutter?
10. Is GPU instancing/batching still viable with the chosen authoring?

A technical validator PASS is not enough to approve the visual result. The
actual production tree must still be tested in Unity.

The current rounded/clustered Little Castle leaf style is **potentially
compatible** with this approach and is intentionally designed to benefit from
shader-driven wind, but compatibility must be confirmed against the actual mesh
topology before treating it as production-ready.

If the real model is not compatible, prefer the smallest asset-authoring fix
(separate Leaves mesh, better vertex distribution, Vertex Color R mask, UV/
normal/tangent correction) before replacing the shared wind architecture.

Canonical details:

- `docs/architecture/foliage-model-shader-preflight.md`
- `docs/architecture/foliage-wind-authoring.md`

## FIRST CHECK — foliage / tree model compatibility

This check is mandatory before any work involving trees, leaves, bushes,
grass or wheat.

Current leaf wind in LC_Foliage combines:
- Crown Sway;
- Vertex Wave;
- Leaf Flutter.

Before editing a vegetation model, material or shader, inspect the real model
and verify model + shader compatibility together.

Required checks:
- trunk/branches and leaves are separate, or Vertex Color R can mask rigid
  trunk vertices from flexible foliage;
- leaf geometry has enough vertices for visible deformation;
- leaf/cluster normals are usable for flutter;
- pivot/local Y is suitable if height masking is used;
- grass/wheat UV Y runs approximately root 0 -> tip 1;
- visible and ShadowCaster deformation can use the same topology safely;
- close zoom does not produce rubber-like stretching/intersection;
- far zoom does not produce unacceptable flutter shimmer.

Classify the asset before implementation as:
- COMPATIBLE;
- COMPATIBLE WITH MASK/AUTHORING CHANGE;
- REQUIRES SEPARATE LEAF MESH;
- NOT SUITABLE FOR CURRENT WIND.

Do not rewrite LC_Foliage only because one model is authored incorrectly.
Prefer fixing material setup, UVs, normals, vertex colors or mesh separation
before making the shared shader more complicated.

For every foliage/tree task, read first:
- docs/architecture/foliage-wind-authoring.md;
- docs/architecture/material-texture-contract.md.

When a concrete vegetation prefab/GameObject is available in Unity, run:
Little Castle -> Rendering -> Validate Selected Foliage Model

Record the compatibility result in task/PR notes before implementation.

## FIRST CHECK — curved wall model contract

This check is mandatory before any work involving player-built stone walls,
wooden walls, fences or city perimeter wall modules.

Core wall placement already exists in:

- `Assets/_Game/Scripts/Building/Walls/WallPlacementDefinition.cs`
- `Assets/_Game/Scripts/Building/Walls/WallRuntimeState.cs`
- `Assets/_Game/Scripts/Building/Walls/WallPathLayoutUtility.cs`
- `Assets/_Game/Scripts/Building/Walls/WallPlacementValidator.cs`
- `Assets/_Game/Scripts/Building/Walls/WallPathPresenter.cs`
- `Assets/_Game/Scripts/Building/Walls/WallPlacementController.cs`
- `Assets/_Game/Scripts/Building/Walls/WallConnectionSocket.cs`

Read first:

`docs/architecture/curved-modular-walls.md`

For real prefab/model integration also read:

`docs/handoffs/wall-asset-integration.md`

Mandatory asset contract:

- one base wall module is straight and rigid;
- module length axis is local +Z;
- root transform should normally be identity;
- pivot should be centered in X/Z and near the wall base in Y;
- real authored module length must match
  `WallPlacementDefinition.segmentLength`;
- curves are created by repeated rigid modules rotated along the path;
- do not bake a curve into the base module;
- do not rewrite the wall layout system merely because one model has a bad
  pivot/scale/axis/length;
- use simple colliders and shared materials;
- authored LODs belong to the prefab/model, not to the path data.
- the first wall point may instantiate `startTowerPrefab`; the wall is drawn
  outward from this small structural tower;
- long walls may generate `repeatTowerPrefab` deterministically by configured
  spacing;
- control points support Smooth and Sharp modes; one Shift press toggles the
  last committed point, and Sharp is a true hard corner suitable for squares;
- large archer towers/gatehouses are separate structures and must expose
  `WallConnectionSocket` child points for clean wall docking;
- a socket's local +Z is the preferred outgoing wall direction;
- socket clearance must reserve enough space for the tower so ordinary wall
  modules do not overlap it;
- do not bake archer towers directly into the generic wall segment asset;

Authoritative multiplayer/save state is one compact `WallRuntimeState`, not a
list of per-section transforms.

The derived section IDs are deterministic from `wallId + sectionIndex`.

Codex should adapt the real Blender/Astra prefab to this contract first.
Small adapter changes are allowed when the real asset proves a genuine
requirement, but architecture changes must be justified.

## Project direction

Little Castle is intended to become a long-lived Unity project with:
- procedural world generation;
- streamed/chunked world data;
- roads, rivers, terrain, vegetation and settlements;
- population, economy, combat and events;
- save/load;
- possible multiplayer later.

Do not optimize for a one-off prototype at the cost of architecture.

## Mandatory engineering rules

1. **Determinism first**
   - Generation must be reproducible from a world seed.
   - Never rely on UnityEngine.Random for authoritative generation.
   - Any random-looking choice must derive from stable inputs such as world seed, chunk coordinate and feature key.

2. **World data is not scene presentation**
   - Generation stages write data.
   - Rendering reads data.
   - Do not spawn visual prefabs directly from core generation stages.

3. **Chunk-safe generation**
   - Neighboring chunks must agree on shared borders.
   - Sampling must use world-space coordinates, not isolated per-chunk local noise.

4. **Stage-based pipeline**
   - New generation features should be added as stages or clearly separated planners.
   - Keep responsibilities narrow.
   - Do not grow one giant WorldGenerator class.

5. **Macro features before local projection**
   - Roads, rivers, settlements and other cross-chunk features should eventually be planned at world/macro scale and then projected into chunks.
   - Do not generate a road independently inside each chunk.

6. **Save-game compatibility**
   - Avoid storing transient Unity object references in authoritative world data.
   - Prefer serializable identifiers and plain data.

7. **Future multiplayer**
   - Treat generation output as deterministic simulation data.
   - Avoid hidden local state that would differ between machines.

8. **No premature package lock-in**
   - Core systems should not depend on optional third-party packages unless explicitly approved.

9. **Small verified changes**
   - Preserve existing architecture unless a change is documented.
   - If an architecture rule must change, create or update an ADR under docs/decisions/.

## Folder intentions

- `Assets/_Game/Scripts/World/Core`: stable world primitives and data.
- `Assets/_Game/Scripts/World/Generation`: generation orchestration.
- `Assets/_Game/Scripts/World/Generation/Stages`: data-producing generation stages.
- `Assets/_Game/Scripts/World/Noise`: deterministic sampling utilities.
- `Assets/_Game/Scripts/World/Rendering`: Unity visual representation only.
- `docs/architecture`: current architecture.
- `docs/decisions`: important irreversible or cross-cutting decisions.

## Before changing world generation

Read:
- `docs/architecture/world-generation.md`
- relevant ADRs in `docs/decisions/`

## Definition of done for generation work

A generation change should:
- be deterministic;
- work for negative chunk coordinates;
- produce matching borders between adjacent chunks;
- not require scene objects to produce authoritative data;
- document any new data contract;
- preserve compatibility with future chunk streaming.


## Finite session map rule

Little Castle does **not** use an infinite world.

A multiplayer host selects a finite map-size preset and seed before the match.

Mandatory rules:

- map presets must validate allowed player counts;
- one MacroWorldPlan is generated for the complete playable match bounds;
- do not rebuild macro roads/rivers/settlements around a moving camera;
- gameplay units/buildings/resources stay inside playable bounds;
- visual padding outside playable bounds is terrain-only presentation space;
- do not implement macro tiles/infinite-world expansion unless the product design changes explicitly.

Fog of war is independent from procedural generation:

- Hidden = never explored;
- Explored = seen before but not currently visible;
- Visible = currently observed;
- units, buildings, towers and owned settlements expose plain vision-source data;
- rendering/shaders must not become authoritative fog state.


## Fair-start generation rule

Procedural starts should remain genuinely uneven.

- do not mirror the whole world just to create fairness;
- resource/forest poverty is allowed and can create emergent/funny situations;
- do not silently inject emergency resources for one player by default;
- prioritize buildability, minimum separation and multiple independent exits over resource equality;
- a river/choke must not leave a player with only one practical exit in normal configurations;
- host settings choose placement mode and fairness strength;
- interior/pressured starts may prefer richer candidates according to host-configurable compensation;
- use the already generated terrain, forest, resources, rivers and bridges;
- keep start selection deterministic from session inputs;
- only playable-bounds resources count;
- future strategic fairness should prefer travel/path cost over straight-line distance.


## Fast session bootstrap rule

Session creation must stay lightweight even when the installed game contains a
large asset library and the selected map is large.

Mandatory rules:

- creating a match must not instantiate/materialize the complete finite map;
- model/texture file size is not network session-state size;
- clients resolve stable archetype IDs to locally installed assets;
- bootstrap may build compact strategic data for the whole map, such as
  bounds, MacroWorldPlan, rivers, roads, bridges, settlements and selected starts;
- detailed terrain meshes, trees, rocks, resource visuals, colliders and other
  local presentation are generated lazily per required chunk;
- only starting/active areas may be prewarmed before MATCH READY;
- distant chunks must remain unmaterialized until required;
- do not serialize/transmit the untouched complete generated world between clients;
- runtime modifications remain deltas over deterministic base generation;
- performance checks must report bootstrap timing separately from later streaming work.

For significant generation/streaming changes, verify at representative
2/8/16-player configurations and multiple map sizes:

- bootstrap wall-clock time;
- MacroWorldPlan time;
- start/fairness time;
- number of detailed chunks created before MATCH READY;
- active GameObject/generated-object count at MATCH READY;
- first-visit chunk generation cost;
- memory growth while exploring.

See:
`docs/decisions/ADR-0002-fast-session-bootstrap-lazy-world-materialization.md`.

## Continuous world-time rule

Little Castle uses one continuous authoritative simulation clock.

Mandatory rules:

- night does not pause the match and does not trigger a global sleep/time-skip;
- default day is 06:00-20:00 in 360 real seconds;
- default night is 20:00-06:00 in 90 real seconds;
- gameplay systems advance from authoritative game-time delta, not directly
  from wall-clock `Time.deltaTime`;
- lighting is presentation and must not become authoritative time state;
- residents may work at night; future rest/mood logic should penalize repeated
  missed rest instead of hard-blocking night work;
- save/load stores world-clock state;
- future multiplayer host/server owns the clock and clients consume snapshots.

See `docs/architecture/day-night-cycle.md`.

## Camera and atmosphere rule

Little Castle uses a ground-focused strategy camera and a lightweight stylized
atmosphere.

Mandatory rules:
- camera pitch stays downward-looking; do not turn it into unrestricted
  free-look/FPS controls;
- camera movement should drive the world-streaming focus;
- preserve close zoom for inspecting authored models/textures;
- day/night sky and lighting are presentation driven by `WorldTimeSystem`;
- prefer the shared procedural sky shader and cheap lighting before adding
  further rendering or post-processing dependencies;
- night must remain readable for building and management gameplay.

See `docs/architecture/camera-and-atmosphere.md`.

## Stylized rendering rule

Little Castle uses a shared lightweight stylized-rendering contract on URP
17.5 Forward (Unity 6000.5). The user approved full adoption on 2026-10-25;
see `docs/decisions/ADR-0004-urp-rendering-adoption.md` and
`docs/architecture/urp-rendering.md`. Preserve this baseline.

Mandatory rules:

- consume shared `_LC_*` shader globals instead of giving every shader its own
  unrelated day/night logic;
- `StylizedLightingGlobals` is presentation-only and never authoritative
  gameplay state;
- prefer shared shaders by object family: solid, foliage/grass, terrain and
  emissive/night presentation;
- preserve GPU instancing where compatible;
- use MaterialPropertyBlock for per-instance variation instead of cloning
  materials at runtime;
- do not add a new shader for a single asset when an existing shared shader can
  express it with parameters;
- do not change render pipelines or add a heavy Volume stack only for atmosphere;
- configure AA, shadow distance/cascades and per-object light limits through
  owned URP quality assets, not legacy Built-in QualitySettings fields;
- keep night readable and use warm local emissive against cool ambient;
- add realtime local lights through an explicit budget/pooling strategy;
- review `docs/architecture/stylized-rendering-roadmap.md` before modifying
  atmosphere, materials or shader architecture.

Current implementation status and future agent handoff are canonical in:
`docs/architecture/stylized-rendering-roadmap.md`.

For vegetation/material authoring also read:

- `docs/architecture/foliage-wind-authoring.md`;
- `docs/architecture/material-texture-contract.md`.

## FIRST CHECK — night lights and emissive LOD

This check is mandatory before integrating lanterns, torches, fireplaces,
emissive windows or other repeated local night lights.

Read first:

`docs/architecture/night-lighting-and-lod.md`

Required cost tiers:

- visual glow comes primarily from emission / `LC Emissive Glow`;
- optional cheap ground response uses `LC Night Light Pool` +
  `NightLightPoolVisual`;
- only a limited nearby subset uses actual Point/Spot Lights through
  `NightLightEmitter` + `NightLightBudgetManager`;
- repeated local lights normally have realtime shadows OFF;
- keep Point/Spot ranges compact;
- do not create one realtime Light for every emissive window;
- do not assume Unity LODGroup disables child Light components;
- far LODs must not keep realtime local lights, detailed glow meshes, probes or
  expensive shadowing;
- do not increase the global realtime-light budget before profiling a real
  settlement.

For production light prefabs run:

`Little Castle -> Assets -> Validate Selected Production Model`

and:

`Little Castle -> Rendering -> Validate Stylized Rendering`

## Production LOD / distant presentation rule

Major production models are expected to arrive from Blender/Astra/Codex with
authored LODs. Do not build a runtime mesh-simplification pipeline by default.

For the farthest forest/village/object LOD:

- preserve silhouette first;
- disable realtime shadow casting and receiving;
- do not use local Point/Spot lighting;
- avoid Light/Reflection Probe cost;
- prefer `Little Castle/Distance/LC Distant Simple` when visually acceptable;
- keep colliders near gameplay only, not across the full visible distance.

Before adding a major prefab to WorldSpawnCatalog, run:

`Little Castle -> Assets -> Validate Selected Production Model`

Read:
`docs/architecture/lod-and-production-assets.md`



## World streaming prefetch rule

Little Castle uses a two-ring streaming model:

- visible/presentation radius;
- larger data-only prefetch radius.

Generated chunk data should normally be prepared before the camera reaches the
visible boundary.

Mandatory rules:

- do not synchronously run the full generation pipeline from
  ChunkSpawn/ChunkView presentation;
- visible chunks have priority over background prefetch;
- background prefetch yields when frame time is already high;
- cooperative generation currently runs generation stages on Unity's main
  thread because ScriptableObject/AnimationCurve access has not yet been proven
  thread-safe;
- do not move the whole pipeline into Task.Run without first separating a
  thread-safe pure-data snapshot from Unity objects;
- keep deterministic seed/output behavior unchanged;
- use profiler/stage timing before splitting or jobifying individual stages.

Current implementation and rationale:
`docs/architecture/world-streaming.md`.


## Test placeholder asset rule

The current WorldSpawnCatalog still uses temporary test placeholder visuals.
Do not replace placeholders with production trees, buildings, mills, villages
or other finished Astra/Blender assets unless the user explicitly requests
production-model integration.

LOD validators and distant shaders are infrastructure only; their existence is
not permission to migrate finished models automatically.
