# Render Optimization and HLOD Architecture

## Status

Foundation implemented. Concrete production wall/village HLOD proxy generation is intentionally still a measured integration step, not a blind automatic mesh-combine pass.

This document extends:

- `docs/architecture/visibility-render-budget.md`;
- `docs/architecture/lod-and-production-assets.md`;
- `docs/architecture/night-lighting-and-lod.md`.

The central rule remains:

> Authoritative simulation owns truth. Rendering may replace, simplify, sleep or hide presentation, but must never change gameplay state.

## Why this system exists

Little Castle can contain hundreds or thousands of repeated wall modules, towers, houses, trees, props and residents. The bottleneck will not be one 20k-triangle gatehouse by itself. Cost grows from the combination of:

- visible triangles;
- renderer/submesh count;
- material/SetPass changes;
- shadow casters;
- local realtime lights;
- animated presentation;
- physics/collider scope;
- GameObject churn;
- rebuild spikes after construction/destruction.

The optimization stack therefore has several layers instead of one global "make meshes low-poly" switch.

## Ownership map

```text
Authoritative gameplay / save / multiplayer
        |
        | stable IDs + state only
        v
Presentation roots / prefabs
        |
        +--> authored LODGroup              mesh detail by screen size
        |
        +--> VisibilityBudgetTarget         camera/visibility cost tier
        |       |
        |       +--> VisibilityFeatureBudgetReceiver
        |       +--> VisibilityHlodProxyReceiver
        |
        +--> HLOD proxy presentation        cluster silhouette only
                |
                +--> HlodRebuildScheduler   coalesced rebuild work
```

There must not be a second camera-distance manager beside `VisibilityBudgetManager`.

## Tier contract

The existing visibility system is authoritative for presentation tier selection:

```text
Tier 0 / Full
Tier 1 / Balanced
Tier 2 / Far
Tier 3 / Hidden
```

`LODGroup` still owns geometric LOD selection by projected screen size. Do not call `LODGroup.ForceLOD` every frame.

Typical presentation behavior:

| Tier | Geometry | Shadows | Extra visuals | HLOD |
|---|---|---|---|---|
| Full | authored LODGroup | authored | enabled | near renderers |
| Balanced | authored LODGroup | mostly authored | selected detail | near renderers |
| Far | low authored LOD | reduced/off | lights/particles mostly off | proxy allowed |
| Hidden | Unity culling + budget sleep | off/reduced | off | normally both hidden |

Quality upgrades remain immediate. Downgrades use the existing hysteresis/prewarm rules.

## New runtime components

### VisibilityFeatureBudgetReceiver

Optional receiver for visual-only features not already handled by `VisibilityBudgetTarget`:

- decorative GameObjects;
- optional non-authoritative local lights;
- particles;
- expensive visual-only Behaviours.

Do not place economy, combat, construction, wall HP, resident AI, save logic or multiplayer logic in its managed Behaviour list.

Repeated night lights still use `NightLightEmitter` + `NightLightBudgetManager`. The receiver is not a replacement for the global night-light budget.

### VisibilityHlodProxyReceiver

Switches presentation between:

- source `Renderer[]`;
- one cheap HLOD proxy root.

It disables Renderers, not the source GameObjects. This is deliberate: colliders, handles, stable IDs and gameplay components remain alive.

The proxy root must be visual only. It must not own:

- colliders used for gameplay;
- health;
- interaction scripts;
- ownership truth;
- save/network state.

### HlodRebuildScheduler

One central queue for expensive proxy rebuilds.

Default starting budget:

- max 1 actual rebuild/frame;
- soft rebuild budget 1.5 ms/frame;
- duplicate dirty notifications coalesce into one queued target;
- canceled/disabled targets are skipped;
- one failed rebuild is logged without stopping the queue.

Profiler marker:

`World.HLOD.RebuildProxy`

These defaults are starting points, not final performance guarantees.

### HlodProxyRebuildable

Base class for a game-specific proxy builder.

A concrete builder should call `MarkHlodDirty()` when structural presentation changes. It must rebuild from presentation data and must not mutate authoritative wall/building state.

## Authored LOD vs HLOD

They solve different problems.

### LOD

One object becomes cheaper:

```text
wall module LOD0 -> LOD1 -> LOD2 -> silhouette
```

### HLOD

Many distant objects become one presentation object:

```text
20 wall sections + 2 small towers
        ↓
1 wall-cluster proxy
```

Use both.

Do not replace good authored LODs with HLOD, and do not keep hundreds of separate far renderers just because each has a cheap LOD2.

## Wall, tower and gate strategy

### Near representation

Player-built walls remain modular and deterministic.

Authoritative state stays one compact `WallRuntimeState`. Derived modules remain local presentation.

Near representation uses:

- shared wall/tower meshes;
- shared materials;
- authored LODGroups;
- GPU instancing where the shader/material path supports it;
- simple colliders only where gameplay requires them.

### Far representation

A future concrete wall HLOD builder should create cluster proxies from the cheapest useful authored representation, not from LOD0.

Prefer source input such as:

- wall LOD2/silhouette mesh;
- tower LOD2/silhouette mesh;
- one or a few shared far materials;
- no realtime local lights;
- no gameplay colliders;
- no tiny banners/props unless silhouette-important.

### Cluster membership

Do not hardcode one universal cluster size before profiling.

Recommended first experiment:

- spatial cells around 24-48 m;
- avoid proxies spanning obviously disconnected wall runs;
- keep gatehouses/large defensive towers independently recognizable when needed;
- cap cluster source renderer count so rebuilds remain predictable.

Measure several cell sizes in the stress scene and keep the best tradeoff between draw calls, popping and rebuild cost.

### Rebuild triggers

Mark an HLOD cluster dirty for structural/visual changes such as:

- wall path committed or rebuilt;
- section/tower added or removed;
- destroyed section changes silhouette;
- ownership/faction identity changes if the proxy contains player color/emblem;
- chunk/presentation set enters or leaves the cluster;
- material family changes.

Do not rebuild for every HP tick if the visible geometry did not change.

Coalesce rapid building edits through `HlodRebuildScheduler`.

## Player identity and materials

Repeated walls must not clone materials per player.

Use shared materials plus `MaterialPropertyBlock` (or another measured shared-buffer strategy) for player color/emblem parameters.

A future HLOD proxy builder must preserve identity without creating a unique material for every cluster. Candidate approaches, in order of simplicity:

1. one owner per cluster + MaterialPropertyBlock;
2. split a mixed-owner cluster by owner;
3. material/texture atlas only if measured need justifies it.

Do not merge multiple owners into one proxy if doing so destroys readable faction identity.

## Renderers, materials and draw calls

Triangle count alone is not the target metric.

For repeatable modular architecture:

- reuse the same Mesh assets instead of duplicating meshes;
- reuse shared Material assets;
- keep submesh/material-slot count low;
- do not make every stone a separate GameObject/Renderer;
- preserve functional animated parts separately (gate leaves, flags when needed);
- use GPU instancing where compatible;
- use the far simple shader for distant silhouettes where visually acceptable.

The existing `ProductionAssetValidator` is the canonical asset-analysis tool. Do not add a second competing optimization-analyzer window unless it provides genuinely new measured data.

## Initial art budgets

These are provisional review targets, not hard rejection limits. Real stress-scene profiling wins over the table.

| Asset family | LOD0 review target | Typical renderer target | Material target |
|---|---:|---:|---:|
| small wall module | ~1k-4k tris | 1-3 | 1-2 |
| large wall module | ~2k-6k tris | 1-4 | 1-3 |
| small/repeat tower | ~4k-12k tris | 1-5 | 1-3 |
| gatehouse | ~8k-25k tris | 2-8 | 2-4 |
| major unique building | ~15k-50k tris | measured | shared families preferred |

A visually important asset may exceed these numbers if its distant LODs, renderer count, materials and stress-scene cost remain healthy.

More important than one LOD0 number:

- LOD1/LOD2 reduction ratio;
- how many copies are visible;
- material sharing;
- shadow cost;
- HLOD availability for large repeated groups.

## Collider rule

Rendering distance and physics distance are separate.

- HLOD proxies never need gameplay colliders.
- do not use a detailed render mesh as the wall/tower collider by default;
- keep generated/world colliders near-only through the existing streaming policy;
- player-built gameplay colliders remain driven by gameplay need, not by far proxy visibility.

## Lighting and particles

For repeated structures:

```text
near     emissive + optional pool + budgeted realtime light + particles if useful
medium   emissive + cheap pool / reduced particles
far      emissive or silhouette only
very far no local realtime light / no particles
```

Do not let one torch per wall segment create one realtime shadowed Point Light.

## Memory rules

Geometry memory is usually not the first concern for a ~20k-triangle architectural set. Texture duplication, unique materials and repeated instantiated state can dominate.

Prefer:

- texture compression appropriate to platform;
- mipmaps for world textures;
- shared material families;
- texture atlases/trim sheets only when they improve measured batching/memory;
- CPU Read/Write disabled unless required;
- no runtime duplication of imported Mesh assets for ordinary instances.

## Stress-scene validation

Do not approve the final thresholds from one isolated prefab.

Required representative tests should grow in stages:

```text
Stage A: 100 wall sections + 20 towers + 2 gates
Stage B: 500 wall sections + 80 towers + 8 gates
Stage C: 1000 wall sections + 200 towers + 20 gates
Stage D: mixed settlement with houses, residents, vegetation and lights
```

For each stage capture:

- CPU frame time;
- GPU frame time;
- batches / SetPass calls;
- visible triangles/vertices;
- shadow caster count/cost;
- active realtime local lights;
- `VisibilityBudgetManager` tier counts;
- HLOD proxy count;
- HLOD pending rebuild count;
- `World.HLOD.RebuildProxy` time;
- VRAM / total memory where available;
- camera pan/rotate/zoom spike behavior;
- build/destroy burst spike behavior.

## Concrete wall HLOD builder — future integration contract

The foundation intentionally does not guess how final wall materials, damage visuals and player identity are authored.

When real production wall/tower/gate prefabs are in Unity, the concrete builder should:

1. inspect the real prefab material/submesh structure;
2. identify the cheapest correct source LOD;
3. group only presentation renderers;
4. preserve owner identity;
5. build or select a proxy without gameplay colliders;
6. keep source GameObjects alive;
7. bind a `VisibilityHlodProxyReceiver`;
8. call `VisibilityBudgetTarget.RebuildBounds()` if hierarchy/bounds changed;
9. rebuild through `HlodRebuildScheduler`, never directly on every edit event;
10. release replaced runtime Mesh resources explicitly;
11. profile rebuild memory allocations and frame spikes;
12. add tests for add/remove/destroy/ownership changes.

Prefer editor/prebaked HLOD for static authored clusters. Use runtime rebuilds only for genuinely dynamic player-built clusters.

## Things explicitly not to do

- Do not add `OptimizationWorldManager`; `VisibilityBudgetManager` already owns camera-driven tiering.
- Do not add one distance `Update()` per wall/tower.
- Do not destroy/reinstantiate objects because the camera turns.
- Do not disable whole source wall GameObjects just to show HLOD if that also disables colliders/gameplay handles.
- Do not combine an entire city into one permanent mesh.
- Do not runtime-decimate imported production meshes every session.
- Do not clone a material for every wall, player or cluster.
- Do not turn HLOD state into save/network state.
- Do not raise light/shadow budgets to hide badly authored assets.

## Next implementation order

1. Compile/test the new receiver/scheduler foundation.
2. Integrate real production wall/tower/gate prefabs with `ProductionAssetValidator` and authored LODs.
3. Build the staged rendering stress scene.
4. Measure renderer/material/shadow/triangle bottlenecks.
5. Implement one concrete wall HLOD prototype against the real asset contract.
6. Compare HLOD off/on with Frame Debugger and Profiler.
7. Tune cluster size and transition tier.
8. Add player identity handling to proxy materials.
9. Expand to villages/forests only after the wall path is proven.
