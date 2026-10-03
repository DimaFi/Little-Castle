# Codex Handoff — Render Optimization / HLOD

## Mission

Continue Little Castle rendering optimization without creating a second optimization architecture.

The current branch already contains the canonical camera-driven visibility system. Your job is to validate and extend it, then integrate real wall/tower/gate assets and only then implement a concrete HLOD proxy builder from profiler evidence.

## Read in this exact order

1. `AGENTS.md`
2. `docs/architecture/visibility-render-budget.md`
3. `docs/architecture/lod-and-production-assets.md`
4. `docs/architecture/render-optimization-hlod.md`
5. `docs/architecture/night-lighting-and-lod.md`
6. `docs/architecture/curved-modular-walls.md`
7. `docs/handoffs/wall-asset-integration.md`
8. relevant code under `Assets/_Game/Scripts/World/Rendering/`
9. `Assets/_Game/Editor/ProductionAssetValidator.cs`
10. `Assets/_Game/Scripts/Building/Walls/WallPathPresenter.cs`

Do not start by writing a new manager.

## Existing canonical runtime

Keep these as the single source of camera-driven presentation tiering:

- `VisibilityBudgetManager`
- `VisibilityBudgetTarget`
- `VisibilityBudgetPolicy`
- `VisibilityBudgetProfile`
- `VisibilityBudgetSettings`

New extension points:

- `VisibilityFeatureBudgetReceiver`
- `VisibilityHlodProxyPolicy`
- `VisibilityHlodProxyReceiver`
- `HlodProxyRebuildable`
- `HlodRebuildScheduler`

`VisibilityBudgetManager` remains the only global visibility/distance manager.

## Non-negotiable separation

```text
wall/building HP, ownership, construction, save, multiplayer
                         !=
LOD/HLOD/renderer/light/particle state
```

Never make authoritative simulation depend on current visibility tier or HLOD state.

## First task: compile gate

Use Unity `6000.5.5f1`.

1. Open the repository root as the Unity project.
2. Let scripts/shaders import completely.
3. Run all EditMode tests.
4. Run `VisibilityBudgetPolicyTests`.
5. Run `VisibilityHlodProxyPolicyTests`.
6. Run Full Model + World Preflight.
7. Fix genuine compile/runtime issues with the smallest coherent change.
8. Do not redesign the architecture to solve a simple import/serialization problem.

If Unity cannot be run, report that clearly. Do not claim runtime validation from static review.

## Second task: real wall asset gate

Use the actual current production wall, repeat tower and gatehouse assets.

For each:

1. run `Little Castle -> Assets -> Validate Selected Production Model`;
2. record LOD0/LOD1/LOD2 triangles and renderer counts;
3. record unique/shared material count;
4. inspect colliders;
5. inspect far shadows/probes/motion vectors;
6. verify shared shaders/GPU instancing;
7. verify functional moving parts remain separate;
8. verify no per-instance material clones are created.

Do not rewrite the wall placement architecture because of a bad pivot, axis, scale, collider or LOD export.

## Third task: stress scene before concrete HLOD

Build a development-only stress setup that can test at least:

- 100 wall sections / 20 towers / 2 gates;
- 500 wall sections / 80 towers / 8 gates;
- 1000 wall sections / 200 towers / 20 gates;
- one mixed settlement case with vegetation, residents and night-light presentation.

Capture:

- CPU/GPU frame time;
- batches and SetPass;
- visible triangles;
- shadow cost;
- active realtime lights;
- visibility tier counters;
- HLOD queue metrics;
- memory/VRAM if available;
- fast camera-turn spikes.

Keep screenshots or a concise text report under `docs/reports/` if that folder exists/gets introduced.

## Fourth task: concrete wall HLOD prototype

Only after real asset inspection and stress metrics.

Implement one wall HLOD builder derived from `HlodProxyRebuildable`.

Requirements:

- input is presentation renderers only;
- source wall GameObjects remain alive;
- colliders/handles/gameplay scripts remain untouched;
- use cheapest correct authored LOD/silhouette source;
- proxy contains no gameplay colliders;
- preserve player/faction identity;
- prefer shared materials and MaterialPropertyBlock;
- do not combine different owners if identity becomes unreadable;
- release replaced runtime Mesh objects explicitly;
- rebuild only through `HlodRebuildScheduler`;
- coalesce repeated build/destroy changes;
- never rebuild on every HP tick if geometry did not change;
- call `VisibilityBudgetTarget.RebuildBounds()` if proxy hierarchy/bounds changes;
- add tests for add/remove/destroy/owner changes.

Do not use runtime mesh simplification/decimation as the default pipeline.

## Cluster experiment

Start with measured candidates, not one magic value.

Try spatial cells roughly in the 24-48 m range and compare:

- draw calls;
- proxy count;
- transition quality;
- rebuild time;
- memory allocations;
- camera popping.

Keep gatehouses or major towers separate when their silhouette/readability benefits from it.

## Invalidation events

A cluster may be marked dirty when:

- wall path geometry is committed/rebuilt;
- a wall/tower visual is added or removed;
- destruction changes silhouette;
- owner/faction identity changes;
- source material family changes;
- streamed presentation membership changes.

Do not mark dirty because only numeric HP changed.

## Lighting rule

Do not solve repeated torch cost with `VisibilityFeatureBudgetReceiver` alone.

Canonical repeated light path remains:

- emissive material;
- optional `NightLightPoolVisual`;
- `NightLightEmitter`;
- `NightLightBudgetManager`.

Far/HLOD presentation should contain no realtime local lights by default.

## Validation before claiming success

Use:

- Unity Profiler;
- Frame Debugger;
- Memory Profiler if available;
- F8 world/visibility overlay;
- `World.VisibilityBudget.Evaluate`;
- `World.HLOD.RebuildProxy`;
- Full Model + World Preflight.

Test aggressive pan/rotate/zoom and rapid wall build/remove bursts.

Success means no visible double silhouette, no empty pop frame, no authoritative gameplay changes and measured performance improvement in the stress scene.

## Required final Codex report

Report:

1. Unity version actually used;
2. compile/test result;
3. real asset metrics;
4. stress-scene metrics before HLOD;
5. exact HLOD implementation, if justified;
6. before/after CPU/GPU/batches/triangles/shadows/memory;
7. rebuild-spike measurements;
8. files changed;
9. remaining risks;
10. next measured optimization.

## Stop conditions

Stop and report instead of guessing if:

- final wall/tower/gate assets are unavailable;
- player-identity shader properties are not defined yet;
- source material structure makes safe combining ambiguous;
- Unity compilation cannot be run;
- profiler data shows another bottleneck dominates HLOD.

A documented stop is better than a speculative architecture rewrite.
