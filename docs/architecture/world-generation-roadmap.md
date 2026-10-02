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
- [x] Climate inputs
- [x] Biome system
- [x] Terrain material/surface IDs
- [x] Automated Unity EditMode and PlayMode integration tests
- [ ] Erosion experiments if actually needed

## Phase 2 — World streaming

- [x] bounded LRU chunk-data cache
- [x] active-chunk cache pinning
- [x] player/focus-centered loading radius
- [x] unload hysteresis radius
- [x] nearest-first load queue
- [x] generation/load budget per frame
- [x] unload budget per frame
- [x] runtime mesh cleanup on unload
- [x] runtime-delta-aware spawn presentation
- [x] finite session-map bounds foundation
- [x] host map-size/player-count validation foundation
- [x] separate playable and visual-only map bounds
- [x] terrain-only visual border streaming
- [x] one stable MacroWorldPlan for the complete finite playable map
- [ ] asynchronous/background generation boundary
- [ ] cancellation/request-version safety
- [ ] visual chunk/root pooling
- [ ] object/vegetation pooling or instancing
- [ ] generation/streaming profiler
- [ ] multiple streaming focuses for multiplayer cameras/observers

## Phase 2A — Fast session bootstrap

- [x] architecture decision: bootstrap != full-map materialization
- [x] deterministic session inputs remain small
- [x] lazy chunk streaming foundation exists
- [ ] explicit session-bootstrap coordinator/state machine
- [ ] prewarm policy for player starting chunks
- [ ] MATCH READY boundary/event
- [ ] bootstrap timing metrics
- [ ] MacroWorldPlan timing metrics
- [ ] fairness/start-selection timing metrics
- [ ] detailed-chunk count metric before MATCH READY
- [ ] active object/GameObject count metric before MATCH READY
- [ ] representative 2/8/16-player bootstrap tests
- [ ] multiple-map-size bootstrap tests
- [ ] verify distant chunks are not generated before required
- [ ] measure first-visit chunk materialization cost
- [ ] measure memory growth during exploration
- [ ] define release timing budgets after representative models/textures exist

The complete finite map may be known strategically, but detailed world
presentation must remain lazy.

## Phase 3 — Macro world plan

- [ ] deterministic regions
- [x] neutral settlement candidates
- [x] ruin / landmark candidates
- [x] river graph/network
- [x] road graph
- [x] stable feature IDs
- [x] feature exclusion/influence areas

Chunks query this plan and clip/project macro features locally.

## Phase 3A — Fair player starts

- [x] deterministic start-candidate field
- [x] buildable-area viability checks
- [x] playable-bounds-only forest scoring
- [x] configurable strategic resource requirements
- [x] resource richness/capacity scoring
- [x] map-size/player-count-scaled start separation
- [x] selected-start quality spread gate
- [x] deterministic player-index assignment
- [x] fairness report with seed rejection reason
- [ ] automatic multi-seed search/reroll helper
- [ ] neutral-settlement accessibility fairness
- [ ] road/river/chokepoint strategic fairness
- [ ] team-start fairness
- [ ] production thresholds tuned from real matches

## Phase 4 — Water

- [x] deterministic first-pass river source strategy
- [x] river continuity and tributary confluences
- [x] terrain carving
- [x] riverbed/surface data
- [ ] local water rendering
- [x] bridge candidate extraction

## Phase 5 — Neutral settlements and POIs

- [x] settlement spacing
- [x] terrain suitability
- [x] settlement archetype hooks
- [ ] economic profile data
- [ ] neutral-state runtime model
- [ ] ruined/fortified minor sites
- [ ] signpost destination hooks

## Phase 6 — Roads and bridges

- [x] road connectivity graph
- [x] terrain-aware route cost
- [x] world-space road paths
- [x] road types/width data
- [x] chunk projection/exclusion
- [ ] terrain deformation
- [ ] road material/mesh layer
- [x] first-pass intersection-based bridge placement

## Phase 7 — Nature and strategic resources

- [x] forest-density field
- [x] tree spawn data
- [ ] high-density renewable/managed wood strategy
- [ ] bushes and ground plants
- [x] scalable grass-density field
- [x] rock scatter
- [x] stone deposits
- [x] ore deposits
- [x] deposit richness/capacity
- [x] exclusion masks near infrastructure
- [x] resource depletion runtime state

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

Finite session setup should synchronize:

- world seed;
- generator version;
- player count;
- selected map-size preset ID;
- stable macro-plan identity/data where required;
- authoritative runtime deltas.

Fog-of-war state is viewer/team-specific and must not leak hidden enemy
information to unauthorized clients.

Prefer synchronizing deterministic session inputs and runtime deltas instead of
transmitting every untouched generated tree and terrain sample.

## Phase 10 — Fog of war

- [x] Hidden / Explored / Visible logical states
- [x] persistent explored grid foundation
- [x] plain VisionSourceData contract
- [ ] per-player/team fog manager
- [ ] world-space fog texture/renderer
- [ ] entity visibility filtering
- [ ] minimap fog
- [ ] owned-village vision integration
- [ ] strong map-edge darkness presentation
