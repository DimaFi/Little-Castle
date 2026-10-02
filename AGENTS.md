# AGENTS.md — Little Castle

This file defines rules for AI coding agents working in this repository.

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

## Production model + world-fill preflight rule

Before a production model batch is accepted for large procedural placement, run:

`Little Castle -> Preflight -> Run Full Model + World Preflight`

Do not bypass a reported error by weakening deterministic generation contracts.

Performance rules for the filling test:

- keep authoritative `WorldSpawnData` prefab-independent;
- preserve one stable archetype ID with multiple visual variants;
- use authored LODs rather than runtime mesh simplification;
- keep generated-object colliders near-only through
  `GeneratedObjectColliderRadiusChunks`;
- do not restore linear per-spawn catalog scans;
- do not restore all-entity scans for per-chunk runtime entity queries;
- preserve macro point spatial indexing and bridge river-segment spatial indexing;
- optimization must not change output for the same seed/settings unless the
  generation version is intentionally advanced;
- inspect the preflight per-stage timings before increasing generation budgets.

## Authoritative gameplay foundation rule

Before changing population, economy, construction, barracks, neutral trading,
match outcome rules, ruler progression or world events, read:

`docs/architecture/gameplay-foundation.md`

Mandatory rules:

- authoritative gameplay is plain serializable state, not scene GameObjects;
- gameplay elapsed time comes from the authoritative world clock;
- ruler defeat starts recovery and does not eliminate the player;
- default elimination is destruction of the capital main house;
- new victory/defeat modes extend `IMatchRule`;
- player-built buildings store stable IDs, transforms, construction progress and HP;
- neutral settlements derive stable identity from macro-world features;
- blessing and curse rolls use deterministic session inputs;
- curse backlash is a separate roll applied to the caster when triggered;
- balance values live in gameplay rule data, not UI code;
- systems must remain compatible with Active/Warm/Sleeping simulation.

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
- prefer the built-in procedural sky shader and cheap lighting before adding
  render-pipeline or post-processing dependencies;
- night must remain readable for building and management gameplay.

See `docs/architecture/camera-and-atmosphere.md`.

## Stylized rendering rule

Little Castle uses a shared lightweight stylized-rendering contract on the
Built-in Render Pipeline unless a separate measured migration decision is made.

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
- do not migrate to URP/HDRP only for atmosphere;
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


## FIRST CHECK — visibility / render budget

This check is mandatory before changing camera-driven LOD, renderer culling,
offscreen animation sleeping, peripheral quality or production LOD transition
behavior.

Read first:

`docs/architecture/visibility-render-budget.md`

Mandatory rules:

- Unity remains responsible for frustum/occlusion draw culling;
- do not Destroy/Instantiate objects because the camera turned;
- do not add one camera-distance Update loop per object;
- do not call `LODGroup.ForceLOD` every frame to implement peripheral quality;
- authored `LODGroup` screen-size transitions own mesh selection;
- Little Castle shared production shaders support dither LOD cross-fade;
- quality upgrades are immediate;
- quality downgrades use hysteresis;
- keep a prewarm region just outside the camera to avoid turn-in pop;
- `VisibilityBudgetManager` controls presentation only;
- camera visibility must never pause authoritative economy, residents, combat,
  construction, world events or multiplayer state;
- generated WorldSpawnCatalog objects automatically receive a
  `VisibilityBudgetTarget` unless their catalog entry explicitly opts out;
- renderer hierarchies stay alive while hidden so Unity can reveal them without
  asset recreation;
- use the F8 visibility counters and
  `World.VisibilityBudget.Evaluate` profiler marker before changing thresholds.

When a real production model batch exists, follow VIS-001 through VIS-007 in
the canonical document and record the measured result before adding GPU
vegetation, HLOD or pooling.
