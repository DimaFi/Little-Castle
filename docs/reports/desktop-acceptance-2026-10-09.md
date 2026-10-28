# Desktop acceptance — 2026-10-09

**Outcome: baseline and real bridge import PASS; complete concept-world acceptance FAIL.**
The cloud drafts were reviewed, integrated on isolated branches, actually
compiled/tested locally, and repaired. This report does not claim a finished
world, target art quality, validated multiplayer accessibility or good FPS.
No source PR was merged/marked Ready on GitHub and no acceptance issue was closed.

## Reproducible snapshot

- Game: `DimaFi/Little-Castle`, branch `integration/desktop-acceptance-2026-10-08`,
  base `c61a6de1af90c35a843b39325bdc007c1297ff42` (`current-unity-fix`). Tested
  implementation head `918a553` (the final report/evidence commit follows it).
- Art: `DimaFi/Little-Castle_Assets`, branch `integration/desktop-art-acceptance-2026-10-08`,
  base `0e23f677e940ffdb8620abdd381f5bb1b23b0f9e`, head `20a1ed4`.
  [Art verification PR](https://github.com/DimaFi/Little-Castle_Assets/pull/3).
- Reviewed game drafts: #14 baseline, #15 routing, #16 importer, #17 water
  contract, #18 instrumentation/work order; art draft #2. Unrelated game #6
  was excluded. Water math was integrated before the final baseline re-run
  to compile its new optional owner; runtime water stayed off in that scene.
- Unity `6000.5.5f1`, Built-in, executable
  `E:/Unity/Unity_6.5/6000.5.5f1/Editor/Unity.exe`; Blender `4.4.3`;
  Python `3.12.14`; Git `2.47.0.windows.2`; Git LFS `3.5.1`.
- Actual machine: Windows 11 Pro build 26200, Intel i7-12700F;
  Unity reports physical memory 32,625 MB and NVIDIA GeForce RTX 4070 Ti SUPER.
  Test runs use `-nographics`; screenshot runs use the real GPU. Camera renders
  are not GPU timing or a built-player benchmark.
- Three bounded, file-owned review/implementation agents used `gpt-6-sol/high`.
  Root alone ran Unity and Git. Ownership/dependencies are recorded in
  `docs/handoffs/desktop-acceptance-ownership-2026-10-08.md`.

Evidence below is relative to `docs/reports/desktop-verification-2026-10-08/`.
The folder retains the start date; completion crossed midnight Saratov time.
Original `docs/reports/verification-2026-10-08` was not overwritten.

## Acceptance matrix

| Work order | Status | Actual result / remaining gate |
|---|---|---|
| #7 baseline | PASS | 78/78 baseline EditMode; corrected original streaming PlayMode passed; final combined 120/120 EditMode and 3/3 PlayMode, zero failed/skipped |
| #8 routes | FAIL overall | Regression tests pass; strict real-map matrix has 3 failures and 1 valid plan. Player-count/session fairness matrix not run |
| Art #1 payload | PASS | 10/10 Python fixtures; 104 manifest files across four releases, hydrated LFS and catalogs verified in isolated local/shared clone |
| Art source reproducibility | FAIL/unverified | Missing versioned dependency repaired; complete rebuild and independent network hydration not performed |
| #9 real bridge import | PASS technical | Real FBX, materials, prefab, 3 LOD tiers and 286-triangle collider imported; all four real-prefab tests run/pass, not ignored |
| #9 target visual quality | FAIL | Actual Unity screenshots exist, but static bank plants, sparse placeholder surroundings and far/night readability still need art review |
| #10 streamed water | PASS source/lifecycle; FAIL scene acceptance | Implemented chunk-owned mesh, negative seams, fixed waterline and release/reload tests. Baked fixture is not a fully streamed procedural scene or proof of all approaches |
| #11 landscape | FAIL visual acceptance | Existing sampler tests rerun with finite/buildable metrics; no complete concept world with approved trees/houses/outcrops rendered |
| #12 performance | FAIL acceptance | Actual macro and legacy streaming timings recorded below. No graphical player CPU/GPU p95/p99 or 2/8/16-player MATCH READY matrix |

There was no tooling/license blocker on the completed local runs. FAIL here
means a failed or unfinished acceptance criterion, not fabricated approval.

## Repairs and retest evidence

- `094e35a`: `WorldStreamer.RefreshStreamingNow` now refreshes its collider ring
  after changing focus. Tests disable the production camera which otherwise
  overwrites their focus; explicitly initialize/refresh; wait for old meshes
  to be destroyed; move outside the unload ring but inside macro bounds.
- `5888348`: reject river-centerline overlap, incorrectly rotated fixed sites
  and legacy sites serving fixed crossings. Preserve sharp-bend rejection and
  bounded failure. Strict opt-in graph adds deterministic settlement/ruin
  connectivity before terrain pathfinding; legacy routing unchanged.
- `a3be57e`: real per-playable-chunk `RiverWaterMeshBuilder`/`RiverWaterPresenter`,
  optional `WorldStreamer.riverWaterMaterial` (null = off), view-owned release.
  Re-population reuses its child rather than creating a duplicate renderer.
  Real PlayMode tests cover Awake light budget and deferred water-mesh disposal.
  Streaming test now logs counters before shutdown and at each ready phase.
- `918a553`: immutable release import, non-readable runtime FBX, no imported
  material proliferation, four shared near and four cheap far materials,
  blue/two-sided cloth, instancing; real prefab registered in concept-only
  catalog. Separate 16-chunk bridge contract fixture and repeatable GPU captures.
  Initial fixture oversaturation corrected via fixture-only palette/light
  multipliers; shared shaders, source textures and Main assets unchanged.
- Art `43dcae1`: accept valid normalized legacy QA texture paths while rejecting
  escaping paths; remove absolute Downloads references; publish exact legacy
  grass dependency with provenance. Art `20a1ed4`: final audit report.

Fresh evidence: `Desktop-Final2-EditMode.xml/.log` **120/120**, 21.931 s;
`Desktop-Final2-PlayMode.xml/.log` **3/3**, 10.854 s. Zero skipped/inconclusive.
Commands use `-runTests -testPlatform EditMode|PlayMode` without `-quit`;
the runner exits itself. Corrected the work-order example accordingly.
Windows launcher return code alone is not proof of completed tests.

## Real macro generation

`DesktopRoutingAudit.RunDesktopMatrix` clones macro settings in memory and
enables strict bridge-aware routing: fixed sites, min 1 bridge, connected all
point features, max ordinary attempts 2 and guided attempts 2. It uses the
actual concept terrain definition, 32 m chunks, 64 cells, 384 m planning halo,
centered bounds including negative coordinates. No full-world scene objects.
The default saved macro profile is not silently switched into this strict mode.

| Seed / width metres | Time s | Features / roads | Rivers / fixed bridges | Components | Result |
|---|---:|---:|---:|---:|---|
| 12345 / 1024 | 17.300 | 34 / 41 | 0 / 0 | 1 | FAIL minimum bridge count |
| 54321 / 1024 | 21.022 | 35 / 42 | 0 / 0 | 1 | FAIL minimum bridge count |
| -10101 / 1024 | 25.361 | 31 / 34 | 2 / 1 | 2 | FAIL disconnected; two requested edges exhausted 12 bounded retries |
| 12345 / 3072 | 70.698 | 137 / 162 | 1 / 2 | 1 | PASS for this route plan only |

Each run has zero orphan sites, unbridged *realized* crossings and missing
realized edges. Rejected requested edges remain explicit errors. JSON files
`Desktop-Routing-{seed}-{size}.json` contain the full diagnostics. Before the
backbone, the 12345/1024 plan had 9 components and took 16.156 s:
`Routing-BeforeBackbone-12345-1024.json`. These are not controlled before/after
performance benchmarks; the new plan also realizes eight more roads.

Managed-memory sampled peaks across the single-process suite:
274,948,096 / 331,628,544 / 394,915,840 / 1,098,055,680 bytes. Later runs include
previous GC state. `GC.GetAllocatedBytesForCurrentThread` returned 0 under
this Mono setup and is **not trusted as zero allocations**. Profiler memory
deltas are not allocation totals or GPU memory. Macro planning has no player
count parameter; these results cannot prove 2/8/16-player reachability.

## Real art and bridge fixture

The art clone `E:/Games_Develop/Little-Castle_Assets_validation_20261008` is a
separate local/shared checkout at `43dcae1`, not an independently downloaded
network clone. Verified 279 hydrated paths / 173 unique LFS objects /
392,137,389 unique bytes; 23 versioned + 125 legacy catalog records.
Bridge v002: 31 manifest entries / 24,611,029 hashed bytes. Import checks SHA
before copying; immutable release truth flags were not rewritten.

- Bridge structural LOD triangles: **13,598 / 7,704 / 1,736**.
- Dressing LOD triangles: **12,428 / 5,022 / 80**; collider: **286**.
- Unity prefab: 1x scale; authored 10.8 m length, 2.86 m clear width,
  local road +Z, river +X, waterline baseY−0.85; flags on the right-hand
  stone entrance cap at each end. Bridge-local reference water/terrain excluded.
- Four shared near slots: Stone / Palette / Grass / FactionCloth;
  far tier uses `LC Distant Simple`, reduced shadow/probe work. No material clones.
- Production validator: 0 errors, 1 warning (hidden collider's Default-Material
  has instancing off). Foliage validator: 0 errors, 1 warning; grass has UV/normals
  but no vertex colors, all decor currently solid/static rather than approved wind.
- `Desktop-Bridge-Import2.log`, `Desktop-Bridge-Scene2.log` and
  `Desktop-Bridge-Calibrated-Capture.log` retain actual import/validator/render evidence.

Open `Assets/_Game/Scenes/ConceptBridgeTest.unity`. It is a deliberately
authored road/river crossing over actual generated/carved/stamped chunk terrain,
**not** a procedural session, multiplayer test or auto-built village. Water
meshes here are baked preview assets, not runtime-owned meshes. Camera evidence:

- `Bridge_Close_Day.png`, `Bridge_Gameplay_Day.png`, `Bridge_Far_Day.png`.
- `Bridge_Close_Night.png` (too dark for approval).
- `Bridge_Close_Day_BeforeCalibration.png` retains the oversaturated first render.

Terrain/water surroundings remain plain prototype surfaces; bank/grass art
and natural rocky transitions do not yet match the user's concepts. No watermills.
No shader/wind migration was made without production-asset compatibility proof.

## Landscape and streaming observations

Rerun landform samples: four-seed mean buildable 98.9%, largest buildable
component 97.3%, plain mask 69.7%, highland 3.9%, scarp .6%. Large-map sample
tests labelled 2/8/16-player cover 2048/4096/6144 m terrain spans and finite
heights; they are **heightfield samples**, not actual player session acceptance.
Editor chunk generation test median 37.32 ms / max 37.77 ms in the first final
run (not a steady-state player benchmark).

Actual final PlayMode lifecycle run, **legacy WorldGenerationTest**, 64 m
chunks, no graphics and no actual multiplayer players:

| Phase | Elapsed ms | Active / desired | Cached | Colliders | Loads / unloads |
|---|---:|---:|---:|---:|---:|
| Initial visible ready (includes scene load) | 7173.06 | 49 / 49 | 49 | 5 | 49 / 0 |
| First visit after focus move | 2615.68 | 29 / 29 | 78 | 4 | 78 / 49 |
| Return ready | 802.05 | 49 / 49 | 80 | 5 | 127 / 78 |

Scene load 2684.04 ms; max observed frame interval 317.68 ms; worst recorded
generation stage 42.78 ms. Edge clipping explains first-visit desired=29.
Cache remains bounded, old meshes are destroyed, runtime removed spawn stays
removed on return, and shutdown leaves zero active chunks. These observed
stalls are a performance warning, not CPU profiler p95/p99, GPU timings or
concept-session MATCH READY. No claim of optimized frame rate is justified.
Synthetic water test 64m/32 cells: 4,224 vertices, 1,408 triangles, 8.134 ms;
actual concept has 32m/64 cells and needs its own budget/steady-state measure.

## Next executable work, in priority order

1. **P1 river/route acceptance:** owner Macro/RiverNetworkPlanner + routing tests.
   Instrument source/trace rejection counts, then test concept-only settings for
   seeds 12345/54321/-10101/777. Recover connections between surviving *realizable*
   components, preserving fixed dimensions, explicit bounded failure and seeds.
   Do not relax min bridge count or draw fictitious river/roads for green tests.
2. **P1 presentation integration:** one owner for concept scene/streamer settings;
   connect actual runtime water material and generated bridge sites, verify all
   road socket grades, negative/yaw seams, unload/reload and day/night in PlayMode.
   Landscape/approved-art work follows this owner sequentially to avoid conflicts.
3. **P1 visual target:** terrain surface masks/textures, readable natural banks,
   approved trees/outcrops, static bank vegetation and night readability. Capture
   true strategy-camera overview/close/far before marking visual approval.
4. **P1 performance:** instrument cold macro cache/probe cost and per-stage hitch;
   don't move ScriptableObjects/Unity API blindly to worker threads. Then capture
   graphical player CPU/GPU p50/p95/p99, allocations/peak memory/draw calls and
   real 2/8/16-player finite-session readiness after the world is valid.
5. **P2 art reproducibility:** independently network-hydrate LFS and rebuild
   source in an isolated checkout; wall optional legacy texture copy needs review.

Keep independent source/test work parallel only with disjoint file ownership;
shared scene/profile/import edits sequential. No broad radius/density increase
or full-world GameObject generation to disguise missing data or poor performance.
