# World Generation Roadmap

This is an implementation roadmap, not a requirement to build everything at once.

## Phase 0 — Foundation

- [x] Chunk coordinates
- [x] Deterministic noise
- [x] Generation settings
- [x] Ordered generation stages
- [x] Height data
- [x] Debug mesh preview
- [x] Determinism diagnostics
- [x] Chunk seam diagnostics
- [x] Macro-world data scaffold

## Phase 1 — Terrain model

- [x] Layered terrain relief
- [x] Regional relief mask
- [x] Rolling hills
- [x] Ridge/highland component
- [x] Fine terrain detail
- [x] Per-cell slope calculation
- [x] Coarse terrain classification
- [ ] Height normalization/sea-level policy
- [ ] Climate inputs
- [ ] Biome system
- [ ] Terrain material/surface IDs
- [ ] Automated Unity tests
- [ ] Erosion experiments if actually needed

## Phase 2 — World streaming

- [ ] Chunk cache
- [ ] asynchronous/background generation boundary
- [ ] player-centered loading radius
- [ ] visual chunk pooling
- [ ] cancellation/version safety
- [ ] generation profiler
- [ ] generation budget per frame

## Phase 3 — Macro world plan

- [ ] deterministic regions
- [ ] neutral settlement candidates
- [ ] ruin / landmark candidates
- [ ] river graph/network
- [ ] road graph
- [ ] stable feature IDs
- [ ] feature exclusion/influence areas

Chunks query this plan and clip/project macro features locally.

## Phase 4 — Water

- [ ] river source/catchment strategy
- [ ] river continuity
- [ ] terrain carving
- [ ] riverbed/surface data
- [ ] local water rendering
- [ ] bridge candidate extraction

## Phase 5 — Neutral settlements and POIs

- [ ] settlement spacing
- [ ] terrain suitability
- [ ] settlement archetypes
- [ ] economic profile data
- [ ] neutral-state runtime model
- [ ] ruined/fortified minor sites
- [ ] signpost destination hooks

## Phase 6 — Roads and bridges

- [ ] road connectivity graph
- [ ] terrain-aware route cost
- [ ] world-space road paths
- [ ] road types
- [ ] chunk clipping
- [ ] terrain deformation
- [ ] road material/mesh layer
- [ ] bridge validation and placement

## Phase 7 — Nature and strategic resources

- [ ] forest-region field
- [ ] tree spawn data
- [ ] high-density renewable/managed wood strategy
- [ ] bushes and ground plants
- [ ] grass
- [ ] rock scatter
- [ ] stone deposits
- [ ] ore deposits
- [ ] deposit richness/capacity
- [ ] exclusion masks near infrastructure
- [ ] resource depletion runtime state

## Phase 8 — Save / runtime modifications

Separate:

```text
BaseGeneratedWorld
+
RuntimeWorldDelta
=
CurrentWorld
```

Runtime delta examples:

- built building;
- chopped tree;
- mined resource;
- changed road;
- destroyed bridge;
- changed settlement state.

## Phase 9 — Multiplayer readiness

Prefer synchronizing:

- world seed;
- generator version;
- stable macro-plan identity/data where required;
- authoritative runtime deltas;

instead of transmitting every untouched generated tree and terrain sample.
