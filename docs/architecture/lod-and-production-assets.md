# LOD and Production Asset Contract

## Core rule

Little Castle production assets are authored with their important LOD models
before they enter large-scale world presentation.

The runtime should not try to procedurally simplify finished Blender/Astra/Codex
models every time a session starts.

Preferred asset flow:

```
Astra / Blender / Codex
        ↓
production model
        ↓
LOD0 / LOD1 / LOD2 / distant silhouette
        ↓
Unity prefab + LODGroup
        ↓
WorldSpawnCatalog
```

## Distant forests and villages

Very distant objects exist primarily to preserve:

- recognizable silhouette;
- large color masses;
- settlement/forest readability;
- continuity of the horizon.

The farthest LOD should normally NOT pay for:

- realtime shadow casting;
- realtime shadow receiving;
- Point/Spot light contribution;
- normal maps;
- AO texture sampling;
- roughness/specular response;
- reflection probes;
- light probes;
- detailed foliage flutter;
- complex colliders.

The project provides:

`Little Castle/Distance/LC Distant Simple`

Default material:

`Assets/_Game/Materials/Shared/LC_DistantSimple_Default.mat`

The shader intentionally has:

- no ShadowCaster pass;
- no ForwardAdd pass;
- no normal/AO/roughness sampling;
- shared day/night ambient;
- cheap sun shaping;
- fog;
- optional alpha clip;
- GPU instancing variant.

It is a candidate for the cheapest forest/building silhouette tiers.

## Suggested authored LOD roles

Exact screen-relative thresholds must be tuned in Unity.

### LOD0

Close inspection.

- full authored mesh;
- full Little Castle material;
- full foliage wind where applicable;
- shadows where visually useful;
- gameplay colliders as required.

### LOD1

Normal strategy distance.

- clearly reduced geometry;
- shared materials;
- simpler small detail;
- foliage may retain Crown Sway + Vertex Wave;
- small decorative children can disappear.

### LOD2

Far strategy distance.

- strong geometry reduction;
- preserve roof/tree/building silhouette;
- foliage micro flutter should normally be removed;
- reduce shadow cost aggressively;
- no tiny props.

### Farthest silhouette LOD

Very distant forest/village/horizon readability.

- extremely cheap geometry or authored cluster/HLOD;
- no realtime shadows;
- no local lights;
- no expensive probes;
- no motion-vector generation for static silhouettes;
- use LC Distant Simple where visually acceptable;
- preserve silhouette before surface detail.

## Forest-specific guidance

A forest does not need every distant tree to remain a full tree.

Possible progression:

```
near forest:
individual authored trees

medium:
individual lower LOD trees

far:
very cheap tree silhouettes / grouped tree meshes

very far:
forest cluster / HLOD mass
```

The authoritative world can still contain the same generated trees. The
presentation representation may change by distance.

Do not store the current LOD level in authoritative save state.

## Village-specific guidance

A distant village may eventually use an authored HLOD cluster representing:

- major roofs;
- tower/church silhouette;
- main wall mass;
- large tree masses.

It does not need:

- doors;
- barrels;
- fences;
- windows as geometry;
- animated residents;
- realtime local lights.

Close presentation is restored when the camera approaches.

## Lighting LOD

Mesh LOD and local-light LOD are intentionally separate.

Unity `LODGroup` switches Renderers; do not rely on it to make every child
Point/Spot Light cheap.

Little Castle local-light progression is:

```text
near:
  emissive + optional pool + budgeted realtime Light

medium:
  emissive + optional cheap pool

far:
  emissive only if the authored LOD still needs it

farthest:
  silhouette only, no realtime local light
```

Runtime local-light distance/budget is managed by:

- `NightLightEmitter`;
- `NightLightBudgetManager`;
- optional `NightLightPoolVisual`.

Canonical details:

`docs/architecture/night-lighting-and-lod.md`

## Collider tiers

Visible distance and Physics distance are separate.

World terrain now supports:

`WorldStreamingSettings.colliderRadiusChunks`

Only nearby playable chunks keep active terrain MeshColliders.

Distant visible chunks remain render-only.

The same principle now applies to generated object colliders through:

`WorldStreamingSettings.generatedObjectColliderRadiusChunks`

- nearby gameplay object: authored collider state is active;
- distant visual object: generated-model colliders are disabled while render LODs remain visible;
- an authored-disabled collider is never force-enabled by the distance tier.

## Production asset validator

Select a production prefab/model and run:

`Little Castle -> Assets -> Validate Selected Production Model`

The validator currently checks:

- LODGroup presence;
- vertex/triangle counts by LOD;
- LOD geometry accidentally increasing;
- far-LOD shadow casting/receiving;
- far-LOD probe usage;
- far-LOD motion-vector generation;
- far-LOD shader choice;
- GPU Instancing;
- CPU Read/Write mesh state;
- complex MeshColliders;
- unmanaged local Lights;
- texture Read/Write;
- missing mipmaps on world textures.

This validator reports problems. It does not silently rewrite the asset.

## Performance diagnostics

The test scene includes:

`WorldPerformanceOverlay`

Toggle:

`F8`

It shows:

- approximate frame time / FPS;
- active/desired chunks;
- generated-data cache;
- pending chunk loads;
- active terrain colliders;
- last chunk load time;
- last mesh build time;
- last spawn-presentation time;
- object count of the last presented chunk;
- total load/unload counts.

ProfilerMarker names include:

- `World.Streaming.Update`;
- `World.Streaming.LoadChunk`;
- `World.Streaming.UnloadChunk`;
- `World.MeshBuild`;
- `World.SpawnPresentation`;
- `World.Macro.PointFeatures`;
- `World.Macro.Rivers`;
- `World.Macro.RoadGraph`;
- `World.Macro.RoadPaths`;
- `World.Macro.Bridges`.

Use these before attempting large optimizations.

## Important future systems

Not yet considered finished:

- GPU-instanced large vegetation presenter;
- generated-object pooling;
- chunk-root pooling;
- HLOD village/forest clusters;
- predictive camera-direction streaming;
- object collider distance tiers;
- mipmap streaming validation;
- async/jobified pure-data generation.

These should be implemented from profiler evidence rather than assumption.

## Codex / agent rule

Before accepting a new major model into WorldSpawnCatalog:

1. run foliage compatibility validation when it contains vegetation;
2. run Production Asset Validator;
3. confirm authored LODs;
4. confirm farthest LOD shadow/light policy;
5. confirm materials reuse shared shaders;
6. record intentional exceptions.

A single badly authored prefab is not justification to make the global renderer
or streamer more expensive.


## Full production preflight

Before a mass world-filling test, use:

`Little Castle -> Preflight -> Run Full Model + World Preflight`

This combines Ready-folder synchronization, production-model validation,
configuration validation and multi-seed data-only generation stress checks.

The stress pass reports each generation stage separately. If one stage exceeds
the frame budget, optimize/split that stage rather than increasing the number of
whole chunk stages executed per frame.
