# Chat acceptance follow-up: routing fixes and completion plan

Date:2026-10-09. Branch: `codex/chat-acceptance-fixes-2026-10-09`.
Base: locally assembled PR26–30 at `db96902746c5f0d4080f1e62cc31a205f852ddad`,
including original desktop baseline `deaf52c`.
This is a local implementation branch, NOT a GitHub merge/Ready approval.

## Changes

- `WorldRouteConnectivityValidator.RoadReachesFeatures`: matching road ID,
  existing finite feature positions, finite centerline, at least two points,
  endpoints within0.1 m. Reversed centerline accepted. Wrong geometry never
  unions components. Shared internal helper, public API unchanged.
- `RealizedRoadConnectivityRepair`: retires only explicitly rejected,
  unrealized desired connections in strict bridge-aware opt-in mode.
  Never removes a materialized same-ID road, even suspect geometry.
  Strict disconnected/minimum-bridge failures remain visible; non-strict
  behavior and originalRejectedConnections diagnostics preserved.
  Component counting/candidate acceptance use the same endpoint predicate.
- Regression coverage for alternate reconnect + stale edge, zero budget,
  non-strict mode, materialized-road preservation, invalid endpoints,
  reversed roads, numerical tolerance and nonfinite interior points.
- Russian v1 completion plan: `../plans/world-graphics-v1-completion-2026-10-09-ru.md`.
  Separates source/tests, procedural visual acceptance and measured performance.

One scoped5.6 Sol Extra High agent owned repair source/tests only; root owned
validator/endpoint tests, plan, Unity launches and final review. Agent did not
edit foreign files or launch Unity. Standalone generated-csproj MSBuild was
inconclusive and is NOT counted as verification.

## Actual verification

Unity6000.5.5f1 Built-in, Windows/i7-12700F/RTX4070 Ti SUPER desktop.
Batchmode/nographics, not a GPU benchmark.

- Full EditMode: **153/153 PASS**, failed0, skipped0, duration19.450027 s.
  Includes strengthened regressions. Previous review had144/144; two independent
  desired-behavior probes were red before these fixes.
- One real data-only macro audit, seed **-10101**,1024 m square,32 m/chunk,
  64 cells/chunk, temporary strict settings (minimum1 bridge, connected graph,
  ordinary/guided attempts2 each): **VALID**;31 features,35 roads,2 rivers,
  **2 fixed bridges,1 component**,0 missing/orphan/unbridged errors.
  Macro elapsed **23.953 s**; observed managed peak395,079,680 bytes (~377 MiB).
  Not proof of fast MATCH READY. Player count is not an input to this macro audit.
- `git diff --check`: PASS for this change.
- No scenes, shaders, Main settings, source art or imported meshes changed.
- New PlayMode/GPU/2–8–16-player/performance-matrix runs deliberately not claimed:
  this narrow pass changes pure macro data validation/recovery, not presentation.
  Prior combined review3/3 PlayMode result remains historical, not a new run.

Evidence: `chat-routing-fixes-evidence-2026-10-09/EditMode.xml` and
`chat-routing-fixes-evidence-2026-10-09/Routing--10101-1024.json`.
Full local logs: `Logs/chat-fixes-2026-10-09-editmode.log`,
`Logs/chat-fixes-2026-10-09-routing.log`.

Tested source Git blob IDs (before documentation/evidence commit):

- repair: `c2b201f5e61f592c6422bd75dff8e99672a8591a`
- validator: `11b23f8effbc4357d5a7364a37f31388243ee012`
- repair tests: `df453bfecd8bb661ffa10e9e1771d32f2897bf88`
- endpoint tests: `b4b4816c18cf7f7517aa09515ea6f2321ed7a22c`

## Remaining gates

River sources under saved concept presets; consistent carving/surface/wetness
rules at bends; O(N²) repair candidate discovery and slow macro bootstrap;
actual textured/prod-asset streamed scene, readable night, GPU/LOD/streaming
profiling and representative map/start matrix. These remain OPEN. See the
completion plan for order, ownership/dependencies and acceptance criteria.
