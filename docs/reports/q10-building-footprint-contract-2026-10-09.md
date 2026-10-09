# Q10 — Deterministic building footprint and entrance contract

Date: 2026-10-09. Implementer: GPT-6 ChatGPT.
Tracking issue: [#39 Q10](https://github.com/DimaFi/Little-Castle/issues/39).
Base: `codex/concept-world-pass-2026-10-09`,
commit `e6a02df6f57461df9cd7cf853e8e9d62b776d71a`.
Work branch: `gpt6/q10-footprint-data-validator-2026-10-09`.
**Status: isolated source/tests ready for desktop review, NOT integrated into gameplay.**

## Why this matters

The existing initial `ConceptVillageFootprintPresenter` checks a center and four
corners using Physics.Raycast against streamed mesh colliders. A large building
can have a steep internal ridge, a riverbed or an obstructing lane between those
five points. Chunk arrival order also must not determine persistent generated
village placement. Q10 introduces a **data contract**, not an alternative
physical raycast implementation or an implicit replacement for that presenter.

## Exact changes and public contract

Files owned:
- `Assets/_Game/Scripts/World/Core/BuildingFootprintDefinition.cs` +
  new stable `.meta`.
- `Assets/_Game/Scripts/World/Core/BuildingFootprintValidator.cs` +
  new stable `.meta`.
- `Assets/_Game/Tests/Editor/FootprintValidatorTests.cs` +
  new stable `.meta`.
- This report; no other source modified.

`BuildingFootprintDefinition` describes a rectangular foundation in
**world metres**: half extents, odd support-grid resolution 3/5/7/9, max
height spread and slope, door width, forward corridor depth, and max individual
corridor elevation step. Local +Z points **out of the entrance**; yaw is around
Unity +Y; `Vector2(x,y)` means world X/Z, not X/Y height. Definitions with
nonfinite/unsafe geometry are refused before any sampling.

`BuildingFootprintValidator.Validate(worldCenter, yaw, definition, sampleWorld)`
takes a caller-supplied `Func<Vector2, BuildingSiteSample>` returning plain
world-data heights, slopes and `BuildingSiteFlags`. It never samples Physics,
creates scene objects, makes random choices or changes terrain. No arrays or
collections are allocated by the validator per sample.

Foundation support uses all N×N points, including boundary and interior,
max 81. The height spread is evaluated over the **foundation** after all
accepted samples; support slope is bounded at each sample.

An additional oriented 3×3 entrance corridor (max 9 samples, local +Z)
checks playable, ready, walkable, unoccupied, non-water terrain, acceptable
slope and consecutive step height, allowing a **road on the approach**, but not
under the structure. Overall max sampler calls: **90** per candidate.

`BuildingSiteFlags` are **explicit**:
- `Ready`: height/masks are available, not a placeholder/default;
- `Playable`: point is inside finite game bounds, not visual padding;
- `Buildable`: foundation support is permitted;
- `Walkable`: entrance access is permitted;
- `Water`: rejects both zones;
- `Road`: rejects only the foundation, permits a walkable approach;
- `Occupied`: reserves structures, bridge/site footprints and other
  blocking features in both zones.

Sampling is fail-closed for missing/unready data, out-of-bounds points,
nonfinite height/slope, water, occupied point, bad road/buildability masks,
slope, height spread, and bad entrance grades. A strongly typed
`BuildingFootprintRejection` and the first rejected world-space point are
returned; acceptance includes min/max foundation height and sample count.
All of these are plain generation data, with no runtime
`GameObject`/terrain/prefab ownership.

Foundation checks precede entrance checks in deterministic grid order (local
Z then X). Entrance checks traverse lanes left-to-right and steps near-to-far;
yaw modulo 360 prevents overflow for large finite angle inputs.

## Limits / decisions left to root

**Not hooked into the current pipeline.** The existing village remains
unchanged until root and Codex explicitly approve prefab footprint metadata,
mask mapping, save/generation identity and retry/failure policy. Unity `Serializable`
data fields are safe to author, but do not automatically upgrade any old
save or `WorldDefinition`. Do not silently swap the current presenter.

The authoritative sample adapter must combine:
1. phase-limited world-height/slope samples on absolute X/Z from deterministic
   generation; common seams/negative chunks must agree;
2. `MacroWorldPlan` river and realized road geometry / fixed bridge reserves;
3. generated placement/buildability masks plus occupancy of other structures;
4. finite playable bounds excluding visual halo; and
5. an explicit **ready/unavailable** state instead of silently treating missing
   macro or chunk information as zero-height flat ground.

**Do not** call the full detailed WorldGenerationPipeline for every sample,
hold an unbounded `WorldTerrainProbe` cache or force render/physics chunk
materialization in order to validate placements. Root must approve one bounded
sampler adapter and use stable world generation IDs. The validator itself
cannot guarantee a river/road is represented correctly unless the adapter
provides correct masks.

N×N coverage is a **bounded discrete approximation**, not exact polygon
collision, volume clearance, collision-overlap or unit navigation. Features
smaller than grid spacing may be missed. The root integrator must add actual
footprint polygon/segment occupancy checks for roads/riverbeds/other houses
when required, or choose sufficient sampling resolution. Q11 handles overall
village spacing/non-overlap and logical entrance-to-road connectivity; no
runtime physics-based layout is approved by Q10.

For future roundtrip/save, record approved definition version/settings hash;
where actual placement semantics change existing base generated worlds,
increment generation version or provide a reviewed migration. Existing
`ConceptWorld_v001` version must **not** be changed by this isolated PR.

## Authored NUnit checks (not yet executed here)

The `LittleCastle.Tests.FootprintValidatorTests` suite covers:
- 25 foundation + 9 door samples on flat support;
- a ridge inside the foundation invisible to center+corner sampling;
- 90 max bounded samples and repeat determinism at negative world positions;
- true oriented +Z entrance at yaw 0 vs 90 degrees;
- road forbidden under house, allowed on a walkable entrance;
- water in foundation and corridor;
- independently rejected unbuildable foundation and unwalkable corridor;
- steep slope, excessive entrance step, occupied ground;
- not-ready samples, finite playable border and invalid numeric values;
- invalid definition, missing sampler, invalid yaw;
- inclusive foundation height spread boundary.

## Real verification status

| Gate | Status |
|---|---|
| Exact scoped source + test + .meta prepared on isolated branch | DONE |
| Review existing village presenter and project rules | DONE |
| Unity 6000.5.5f1 C# compile | **NOT RUN** |
| Focused new NUnit EditMode suite | **NOT RUN** |
| Full EditMode / PlayMode regressions | **NOT RUN** |
| Scene/prefab/macro adapter integration | **NOT ATTEMPTED (root-owned)** |
| Fresh checkout, asset/build tests, negative-seam real probes | **NOT RUN** |
| Real renderer/player visuals and CPU/GPU/FPS | **NOT RUN (out of scope)** |

Previous 175/175 EditMode and 5/5 PlayMode results in
`docs/reports/concept-pass-1-5-2026-10-09.md` are a **different code revision**
and must not be claimed for this PR.

## Codex desktop handoff / acceptance

1. Create a separate clean worktree of this PR, based on the exact
   `codex/concept-world-pass-2026-10-09` revision, and inspect the complete
   diff. Do not merge/alter `Main*`, scene, prefab, streamer or art assets.
2. Unity 6000.5.5f1 EditMode focused:
   `-batchmode -nographics -projectPath <root> -runTests -testPlatform EditMode -testFilter LittleCastle.Tests.FootprintValidatorTests -testResults <Logs/Q10-Footprint.xml> -logFile <Logs/Q10-Footprint.log>`.
   No `-quit` when using `-runTests`. Read actual XML/log, not only exit code.
3. Execute existing `ConceptVillageFootprintTests` and
   `ConceptVillageRuntimeTests`, then full EditMode and PlayMode. Do not
   assume separate refactor is already integrated. Record counts, failures
   and skipped tests using new timestamped evidence.
4. Review performance in an isolated bulk validation fixture (100 or 1000
   village candidates): sample count/cost, allocations, especially repeated
   river/road mask queries and `WorldTerrainProbe` cache memory.
5. Root owner: agree on buildable/walkable/road/water/occupied mapping,
   real house half extents/entrance side with production asset validator,
   complete macro plan before selection, fixed bridges and negative seams.
6. Q11 can consume this exact API only after tests, confirmed entrance
   orientation, stable village ID, generation version and a bounded
   sampling adapter are approved. Keep no-go positions rejected instead of
   floating/flattening houses.
7. Write a new desktop acceptance report citing exact commit, Unity device,
   NUnit XML, real negative coordinates and random-seed matrix; keep #39
   open until this acceptance is complete. Never infer scene readiness
   from just unit tests.

No promise of FPS, render quality, navigation, multiplayer-accessible routes,
or suitable production house LOD is made by this change.
