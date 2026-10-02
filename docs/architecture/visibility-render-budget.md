# Visibility and Render Budget

## Status

Runtime foundation implemented for Little Castle.

This system is presentation-only. It must never decide authoritative economy,
resident work, combat, world time, save state or multiplayer truth.
Authoritative simulation already has its own player/chunk interest policy in
`WorldSimulationLodPolicy`.

## Runtime model

```text
Tier 0 / Full      near + central
Tier 1 / Balanced  visible peripheral / normal strategy view
Tier 2 / Far       distant or prewarmed edge
Tier 3 / Hidden    not visible and not in the prewarm ring
```

Tier 3 does not Destroy/Instantiate the object and does not toggle the whole
GameObject every camera turn. Unity remains responsible for renderer
frustum/occlusion culling.

## Runtime files

- `VisibilityBudgetSettings.cs`
- `VisibilityBudgetProfile.cs`
- `VisibilityBudgetPolicy.cs`
- `VisibilityBudgetTarget.cs`
- `VisibilityBudgetManager.cs`

All are under:
`Assets/_Game/Scripts/World/Rendering/`.

Generated objects created by `ChunkSpawnPresenter` are integrated by the next
commit in this implementation series. Non-generated/player-built visual roots
must carry one `VisibilityBudgetTarget`.

## Geometry rule

Do not implement per-frame camera-angle calls to `LODGroup.ForceLOD`.

Authored `LODGroup` owns geometry selection from projected screen size.
`VisibilityBudgetManager` owns presentation cost from viewport position,
distance and culling visibility.

This avoids obvious mesh popping and avoids incorrectly reducing a large church
only because its pivot is near an edge.

## Seamless transition contract

The manager enables cross-fade on non-SpeedTree LODGroups.

Shared production shaders must compile and apply
`LOD_FADE_CROSSFADE`. The integration commit updates:

- LC_StylizedLit;
- LC_Foliage;
- LC_DistantSimple.

Default global LOD cross-fade duration: 0.12 s.

## Hysteresis

Quality upgrades are immediate.

Downgrades are delayed:

- normal downgrade: 0.18 s;
- hidden: 0.35 s;
- occlusion grace: 0.22 s.

This prevents threshold flapping while allowing instant turn-in quality.

## Prewarm

The manager evaluates an expanded viewport before real camera culling.

Default radius: 1.24.

A nearby target just outside the screen is restored to a usable visual state
before a fast camera turn reveals it.

## Centralized cost

There is no per-target Update.

One manager:

- owns one CullingGroup;
- updates cached bounding spheres;
- evaluates quality before game-camera culling;
- reacts to visibility callbacks;
- publishes tier counters.

## Bounds

A target caches one local bounding sphere from all child Renderers.

Per camera pass it transforms only that sphere.

After runtime hierarchy or finalized spawn-scale changes call:

`VisibilityBudgetTarget.RebuildBounds()`.

## Tier actions

Full / Balanced:
- authored shadow state restored;
- Animator available;
- LODGroup selects geometry normally.

Far:
- optional shadow casting off;
- optional receive-shadows off.

Hidden:
- far shadow reduction;
- optional Animator sleeping;
- renderer hierarchy remains alive for instant reveal.

Gameplay logic must not depend on Animator state.

For visual-only custom systems implement:
`IVisibilityBudgetReceiver`.

## Profiling

Profiler markers:

- `World.VisibilityBudget.Evaluate`;
- `World.VisibilityBudget.CullingEvent`.

The integration commit adds F8 overlay counters for:

- registered targets;
- camera-visible approximation;
- CullingGroup-visible;
- T0/T1/T2/T3 counts.

## Unity validation gate

Codex must validate in Unity 6000.5.5f1:

1. full C# and shader compile;
2. all EditMode tests;
3. `VisibilityBudgetPolicyTests`;
4. Full Model + World Preflight;
5. representative settlement scene;
6. aggressive pan/rotate/zoom;
7. inspect LOD0/LOD1/LOD2 transitions;
8. confirm no empty-frame pop or double silhouette;
9. Frame Debugger: offscreen ordinary camera draws are absent;
10. Profiler: inspect `World.VisibilityBudget.Evaluate`;
11. F8 counters react correctly;
12. authoritative NPC/economy state continues while camera is elsewhere.

Representative first stress scene:

```text
10 houses
4 farm/field groups
4 mills
4 barracks
1 main house
1 church
wall perimeter + towers/gates
carts/small props
100 residents/units
background trees
```

## Codex tasks

### VIS-001 — compile gate
Compile scripts and modified shared shaders in Unity 6000.5.5f1. Fix genuine
compile/runtime defects without changing the architecture.

### VIS-002 — real asset cross-fade review
Test one real house, wall/tower set, church, tree and animated character.
Record LOD thresholds, cross-fade, shadow transition and silhouette stability.

### VIS-003 — family profiles
After VIS-002 create measured profiles for Building, Wall/Tower, Tree, NPC,
Decoration and Vehicle. Do not invent large threshold tables without profiling.

### VIS-004 — player-built prefab integration
Ensure non-WorldSpawnCatalog building/army prefabs have one root
`VisibilityBudgetTarget`. Do not add per-object Update loops.

### VIS-005 — NPC presentation receiver
If the real character presentation needs extra animation sleep/wake behavior,
implement a presentation receiver. Authoritative resident/army simulation stays
separate.

### VIS-006 — stress/profile report
Record CPU/GPU frame time, batches/SetPass, triangles, shadow casters, active
Animators, tier counts, VRAM/memory and fast camera-turn spikes.

### VIS-007 — measured next steps only
GPU vegetation, HLOD and pooling remain valid future optimizations, but only
implement them after VIS-006 identifies a real bottleneck.
