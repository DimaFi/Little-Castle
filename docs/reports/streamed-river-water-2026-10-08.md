# Streamed river water — source handoff

Date: 2026-10-08, desktop verification 2026-10-09. Status: compiled in Unity
6000.5.5f1; integrated EditMode 120/120 and PlayMode 3/3 passed. Runtime
streamed-scene visual/performance acceptance remains pending.

## Scope

`RiverWaterMeshBuilder` reads `MacroWorldPlan.Rivers`, fixed bridge sites, and
already generated `WorldChunkData.Heights`. It creates at most one water mesh
for the requested playable chunk. It neither changes macro/terrain data nor
creates a complete-map water object. `RiverWaterPresenter` creates one child
renderer under the streamed chunk and owns its runtime mesh; explicit release
and `OnDestroy` are idempotent. The caller supplies a material. A null material
is an opt-out and creates nothing.

The builder samples at twice the terrain cell resolution on a world-indexed
lattice. Neighboring chunks use the same absolute positions and their shared
terrain height samples. Every water triangle is clipped within its own chunk
footprint. The signed wet mask uses actual macro river centerline segments and
their width profiles; multiple overlapping river records are combined into
one mesh, not layered renderers. No water is generated when the plan lacks
river tracks.

At a supported v002 fixed bridge site belonging to the selected river ID, the
protected core takes its channel width and absolute water height from
`BridgeSitePresentationUtility` and `FixedBridgeSiteProfile`. That height is
`baseElevation - 0.85`. The outer 2 m along the local river axis blends its
shore mask and height back into generic river water. This includes arbitrary
yaw and negative chunk coordinates. No `SM_Bridge_Water_A` reference mesh is
instantiated.

## Integration hook for WorldStreamer owner

The integrated `WorldStreamer` has an opt-in `Material riverWaterMaterial` with null
as the default. In `CreateChunkView`, after `view.Initialize` and terrain
presentation, for playable chunks only and when the field is non-null, call:

```csharp
RiverWaterPresenter water = RiverWaterPresenter.Populate(
    chunkObject.transform, chunkData, chunkSize, macroPlan,
    riverWaterMaterial);
view.SetOwnedRiverWater(water);
```

`StreamedChunkView` now holds this optional presenter and invokes
`water.ReleaseOwnedResources()` from `ReleaseOwnedResources()` before clearing
the reference. The child presenter also releases on destruction. Visual-only
padding chunks should receive no river water. No existing scene or Main asset
has been changed by this source handoff.

## Source checks and pending validation

New `RiverWaterPresentationTests` source covers matching border vertices at a
negative chunk seam, fixed water height at yaw 0/37/90/180, finite in-chunk
mesh coordinates and valid triangle indices, one renderer for overlapping
rivers, release and repopulate using the same owner, null-material opt-out, and
no water without an actual river. A diagnostic test logs mesh vertex/triangle
counts and build time for a synthetic 64 m, 32-cell chunk: 4,224 vertices,
1,408 triangles, 8.134 ms in this Editor run. This is NOT the actual 32 m,
64-cell concept profile or a stable benchmark. All water EditMode cases passed.
The added real PlayMode lifecycle test passed explicit chunk release,
idempotent release, owner reuse without duplicate child and destruction of
the second mesh after parent destruction. XML is in the desktop evidence folder.

## Known limits

Generic river surface height is a deterministic approximation above the
carved terrain height using local river depth. The macro plan has no hydraulic
water elevation or gradient profile, and `RiverTerrainCarvingStage` can use a
custom cross-section/depth multiplier. Therefore this source does not promise
physically level pools or fully hide all bank exposure on every profile. The
half-cell raster and triangle clipping can leave visible shoreline facets.
World-space border positions/heights are sampled consistently, but material
UVs/normals and actual rendering still require Unity scene inspection.

This does not align road polylines to the bridge's +/-5.4 m sockets, guarantee
at least one bridge in a map, replace a missing route, alter vegetation/bank
decoration, add colliders/gameplay water, or introduce a watermill. Those
remain separate road/planning/art tasks. Renderer material selection, close
and far zoom appearance, day/night, seam visuals, unload/reload, and timing
remain unverified in an actual streamed river scene. The baked contract test
scene demonstrates the fixed waterline in GPU camera renders, not runtime
streaming or complete procedural river quality. No existing Main scene was changed.
