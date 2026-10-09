# Real streamed terrain normal seams — execution & visual QA

**Date:** 2026-10-09
**Owner:** GPT-6 in ChatGPT, user-requested Little Castle continuation.
**Base:** `gpt6/concept-streamed-terrain-masks` (PR #28, head at cut `1e5650e582224a388900e5d19eeca61c927857ca`) built on desktop PR #20 integration `deaf52c`.
**Branch:** `gpt6/streamed-terrain-normal-seams`, to be reviewed as a **stacked draft PR based on #28**.
**Status:** SOURCE ONLY, NOT COMPILED OR PLAYMODE/EDITMODE/GPU VERIFIED IN UNITY.
**Affected issue:** landscape / real streamed scene (#11), performance (#12), desktop handoff (#19).

## Problem observed in source — NOT a claimed screenshot diagnosis

`ChunkMeshBuilder.Build` calls `mesh.RecalculateNormals()` for **every heightfield chunk independently**. Even when adjacent terrain vertices have exactly matching heights, this only sees triangles inside one chunk: the normal at the east edge of chunk (-1,z) is one-sided, while the normal at west edge of chunk (0,z) uses the other side. Under the existing smooth lighting shader, this can yield visible light/dark stripes at positive or negative chunk boundaries. There was no corresponding normal reconciliation in `WorldStreamer.CreateChunkView`.

The change intentionally does not add geometry, new surfaces, scene GameObjects, heightfield noise or extra map chunks.

## Implementation

- New `Assets/_Game/Scripts/World/Rendering/ChunkTerrainNormalSeams.cs`: `RefreshAround(newlyLoaded, activeViews, chunkWorldSize, getData)`. Reads **already cached/pinned** `WorldChunkData` and already created runtime meshes; never calls `GenerateChunk` or changes deterministic world input. For each of the four adjacent loaded/playability-compatible chunks, computes a centered x/z gradient from the **two sides** of their shared edge and writes the identical `Vector3(-dx,1,-dz).normalized` to each shared mesh vertex.
- At the intersection of **all four loaded chunks**, computes the centered derivative using samples across both x and z boundaries and writes a single identical normal into all four corner vertices. This is order independent once the four sources are present.
- Updates are accumulated per `Mesh` so `mesh.SetNormals` runs once per touched mesh per newly loaded chunk event, leaving `mesh.vertices`, triangles, colors32 (PR #28's R/G masks), UVs, bounds and positions unchanged.
- Reuses a 4,225-normal scratch list on the Unity main thread to avoid a full fresh normal array allocation for every touched mesh; small per-load dictionary allocations remain. Works with negative chunk indices without clamping or world-origin aliases.
- Skips incompatible profiles: finite-map **nonplayable visual padding** may only run `TerrainAnalysis` instead of river/bridge stamping, so it must never be welded indiscriminately to authoritative playable chunk meshes if the heights don't match.
- `WorldStreamer.CreateChunkView` adds a small opt-in call **after** an actual streamed chunk is added to `activeChunks`; active/cache lookups are read-only, no neighboring chunk load. A new Profiler marker `World.Terrain.StitchSeamNormals` records cost. Same gating as PR #28: only when assigned terrain material has `_UseVertexMasks >= 0.5`. Main/legacy terrain presentation remains untouched.
- New `Assets/_Game/Tests/Editor/ChunkTerrainNormalSeamTests.cs`: actual Unity Mesh + synthetic world-coordinate analytic quadratic terrain, negative EW seam centered slope checks, exact shared four-way corner in both load orders, stable vertices/index/UV/vertex colors/bounds and missing/incompatible neighbors.
- Stable Unity .meta GUIDs added for both new sources. No scenes, materials, prefabs, shaders, imported FBX or Unity settings changed.

## Exact Codex desktop integration & acceptance

1. Read PR #20 integration status and previous [PR #28](https://github.com/DimaFi/Little-Castle/pull/28) (new scene/material masks). This PR is **stacked ON TOP of PR #28**, not directly mergeable into `current-unity-fix` without #28. Use reviewed separate local worktree; don't destroy user changes.
2. In Unity 6000.5.5f1, compile; run all EditMode/PlayMode including `ChunkTerrainNormalSeamTests`, `ChunkTerrainVertexMaskTests` and existing `WorldGenerationIntegrationTests`. Log actual new NUnit XML and WARN/FAIL output. **Previous PR #20's 120/120 + 3/3 results do NOT validate new branches.**
3. Run `LittleCastle.Editor.ConceptProceduralWorldSceneBuilder.Create` on a clean Unity Editor after verifying real bridge source, so `ConceptProceduralWorld.unity` exists and is visually inspectable. Scene is generated only in real Unity; it was not committed by GPT-6.
4. Capture daylight and dusk/night screenshots at strategy-camera normal/gameplay/far zoom. Compare BEFORE (PR #28 opt-in masked material without stitching) vs AFTER, for the same seed/camera/time at x=0 and negative chunk seam (e.g. x=-32, z=-32). Inspect border lighting on grassy and highland slopes, not merely shaded plains. Report whether there was actually a visible strip and whether it disappeared, with file paths and crop images. This source-level fix is not proof of visible improvement until that comparison.
5. Profile cold/warm chunk creation/unload-return with a real GPU player and Unity profiler. Specifically report `World.Terrain.StitchSeamNormals` p50/p95/max duration and GC alloc, `World.MeshBuild`, total chunk load frame p95/p99, collider recooking and effect on GPU. `Mesh.SetNormals` can update whole vertex-normal buffers of multiple active meshes; **do not optimize solely by guessing**. If overhead is too high, implement budgeted dirty seam updates, nonalloc lists/map reuse, or restrict to screen-visible seams and retest.
6. Test load-order permutations and finite/non-playable fringe: normal stitching deliberately avoids welding dissimilar source profiles. A missing neighbor or an incomplete 2-/3-chunk corner retains fallback original normal until all necessary chunks load; if a visible corner artifact remains, consider a separately reviewed partial-corner relaxation. Do not move world-boundary/camera limit.
7. Test 2/8/16 actual finite sessions as distinct from test fixture positions. Macro network/bridge route correctness, day-night scene aesthetics, real source material textures and FPS are still separate acceptance gates.
8. Publish new dated `docs/reports/streamed-terrain-normal-acceptance-YYYY-MM-DD.md` with actual paths, CPU/GPU and screenshot evidence, Unity SHA, machine/hardware and PASS/FAIL/BLOCKED matrix; merge/mark ready only with user approval.

## Compatibility/performance considerations

- No runtime geometry modification means collider **shape** stays identical, but `Mesh.SetNormals` may still trigger engine-side buffer/collider work that must be measured.
- The helper is main-thread-only, intentionally not Burst/Jobs. No background promises.
- No synthetic border hills or generated resource edits. Does not modify any player start/fairness/road/bridge or macro plan constraints.
- The guarded seam treatment activates only for PR #28's concept masked terrain material, not every legacy mesh.
- This source patch is not an assertion that all Unity terrain shader seams are gone: vegetation LOD, material tiling, shadow cascades, normal detail textures and dissimilar analysis-vs-playable padding can also cause visible transitions.
