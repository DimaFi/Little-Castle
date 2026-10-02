# Performance / Streaming Foundation Audit — 2026-10-02

Scope: static repository audit after the first performance-oriented world
presentation pass. Unity Editor was not launched from this environment.

## Implemented in this pass

### 1. Terrain collider distance tiers

`WorldStreamingSettings` now has:

`colliderRadiusChunks`

The test configuration uses radius 1 while the visible load radius remains
larger.

`WorldStreamer` now keeps terrain MeshColliders active only on nearby playable
chunks.

Distant visible chunks are render-only.

`StreamedChunkView` owns lazy collider enable/disable behavior.

### 2. Streaming diagnostics

`WorldStreamer` now publishes:

- ActiveChunkCount
- DesiredChunkCount
- PendingLoadCount
- CachedChunkCount
- ActiveTerrainColliderCount
- LastChunkLoadMilliseconds
- LastChunkUnloadMilliseconds
- LastMeshBuildMilliseconds
- LastSpawnPresentationMilliseconds
- LastChunkSpawnCount
- TotalChunkLoads
- TotalChunkUnloads

ProfilerMarker instrumentation:

- World.Streaming.Update
- World.Streaming.LoadChunk
- World.Streaming.UnloadChunk
- World.MeshBuild
- World.SpawnPresentation

### 3. Test-scene HUD

`WorldPerformanceOverlay` is wired into `WorldGenerationTest`.

Toggle:

`F8`

Frame time is averaged across the sampling interval rather than taken from one
random frame.

### 4. Distant silhouette shader

`Little Castle/Distance/LC Distant Simple`

The shader has exactly one rendering pass.

Confirmed statically:

- no ShadowCaster pass;
- no ForwardAdd pass;
- no normal map;
- no AO map;
- no roughness map;
- no local-light path;
- GPU-instancing variant;
- fog;
- shared day/night ambient;
- cheap sun shape only;
- optional alpha clip.

Intended use: cheapest authored LOD of distant trees/buildings/forest/village
silhouettes.

### 5. Production asset validator

Unity menu:

`Little Castle -> Assets -> Validate Selected Production Model`

Checks every LODGroup in a selected model/prefab.

Current checks include:

- LOD count;
- vertex/triangle counts;
- later LOD accidentally becoming heavier;
- far LOD shadow casting;
- far LOD shadow receiving;
- Light Probe usage;
- Reflection Probe usage;
- distant shader choice;
- GPU instancing;
- mesh CPU Read/Write;
- heavy MeshCollider geometry;
- unmanaged local Point/Spot lights;
- texture CPU Read/Write;
- missing mipmaps on world textures.

The tool reports issues and does not silently rewrite production art.

## Static source audit

Checked C# brace and parenthesis balance for:

- WorldStreamingSettings.cs
- StreamedChunkView.cs
- WorldStreamer.cs
- WorldPerformanceOverlay.cs
- ProductionAssetValidator.cs
- WorldStreamingOptimizationTests.cs
- WorldStreamerPlayModeTests.cs

No structural mismatch found.

Checked LC_DistantSimple.shader:

- braces balanced;
- CGPROGRAM / ENDCG balanced;
- one Pass;
- no ShadowCaster pass;
- no ForwardAdd pass.

Checked WorldGenerationTest scene:

- WorldPerformanceOverlay serialized;
- overlay references the existing WorldStreamer.

Checked MainWorldStreamingSettings:

- AddMeshCollider = true;
- colliderRadiusChunks = 1.

## Tests added / corrected

Added EditMode tests for:

- ColliderRadius clamping;
- enabling/disabling terrain colliders;
- visual-only chunks refusing terrain colliders.

Existing PlayMode streaming test was updated to derive expected active/cache
counts from current streaming settings instead of old hardcoded radius-1
values.

## Unity validation still required

The following cannot be proven statically:

1. Unity 6000.5.5f1 C# compilation.
2. Unity.Profiling ProfilerMarker runtime behavior in the current assembly.
3. MeshCollider enable/disable spike size with real terrain.
4. Actual Physics CPU savings from collider tiers.
5. F8 overlay layout at the user's Game-view resolution.
6. LC_DistantSimple GPU cost on target hardware.
7. Real authored LOD silhouette quality.
8. Whether far LOD transition distances feel correct from the strategy camera.
9. Whether production textures/materials pass the validator with acceptable
   intentional exceptions.
10. Whether GPU instancing is actually batching the final authored prefabs.

## Recommended next review order

1. Pull latest main.
2. Let Unity compile.
3. Run EditMode tests.
4. Run PlayMode tests.
5. Open WorldGenerationTest.
6. Press Play and inspect F8 diagnostics.
7. Move camera quickly across chunks and watch:
   - pending loads;
   - load ms;
   - mesh-build ms;
   - spawn-presentation ms;
   - collider count.
8. Put one real production tree through:
   - Validate Selected Foliage Model;
   - Validate Selected Production Model.
9. Build authored LODGroup for that tree.
10. Put LC_DistantSimple on the farthest test LOD and verify silhouette/day/night.
11. Only after measurements decide whether the next major optimization is:
   - GPU vegetation presentation;
   - pooling;
   - predictive streaming;
   - HLOD clusters;
   - async/jobified pure-data generation.

## Architecture conclusion

The current deterministic generation data model did not need to change.

These optimizations stay on the presentation/streaming side, preserving:

- WorldSpawnData identity;
- deterministic seed generation;
- runtime delta/save semantics;
- future multiplayer authority.

That separation should be preserved.
