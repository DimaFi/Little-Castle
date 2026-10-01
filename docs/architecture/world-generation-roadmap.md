# World Generation Roadmap

This is an implementation roadmap, not a promise that every item must be built immediately.

## Phase 0 — Foundation

- [x] Chunk coordinates
- [x] Deterministic noise
- [x] Generation settings
- [x] Ordered generation stages
- [x] Height data
- [x] Debug mesh preview

## Phase 1 — Terrain model

- [ ] Multiple terrain noise layers
- [ ] Continental / regional masks
- [ ] Slope calculation
- [ ] Height normalization policy
- [ ] Biome climate inputs
- [ ] Terrain material IDs
- [ ] Chunk border tests

## Phase 2 — World streaming

- [ ] Chunk cache
- [ ] Asynchronous generation API
- [ ] Player-centered loading radius
- [ ] Visual chunk pooling
- [ ] Cancellation/version safety
- [ ] Generation profiler

## Phase 3 — Macro world plan

Introduce a world-scale data layer above chunks.

- [ ] Regions
- [ ] River graph
- [ ] Settlement candidates
- [ ] Road graph
- [ ] Major landmarks
- [ ] Deterministic feature IDs

Chunks query this plan and clip/project features into local data.

## Phase 4 — Nature

- [ ] Biomes
- [ ] Tree placement
- [ ] Bushes
- [ ] Rocks
- [ ] Ground vegetation
- [ ] Resource nodes
- [ ] Density masks
- [ ] exclusion zones near roads/buildings

## Phase 5 — Roads and bridges

Road generation must be a graph/path problem, not random decals per chunk.

Likely layers:

```text
Settlement / POI nodes
        ↓
Connectivity graph
        ↓
Path cost field
        ↓
World-space road splines
        ↓
Chunk clipping
        ↓
Road mesh + terrain deformation
        ↓
Bridge placement where road intersects water
```

## Phase 6 — Save / runtime modifications

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
- removed tree;
- changed road;
- depleted resource;
- destroyed bridge.

## Phase 7 — Multiplayer readiness

Prefer synchronizing:
- world seed;
- generator version;
- authoritative runtime deltas;

instead of transmitting the entire untouched generated world.
