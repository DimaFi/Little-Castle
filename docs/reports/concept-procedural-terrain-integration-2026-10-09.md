# Procedural concept terrain masks + streamed test scene — Unity desktop handoff

Date: 2026-10-09
Executor: GPT-6 in ChatGPT, continuing Little Castle visual P1 task.
Base: DimaFi/Little-Castle `integration/desktop-acceptance-2026-10-08`, inspected head `deaf52c595747f3d9dedf6a48b455960f0ca4bdd` (draft PR #20).
Branch: `gpt6/concept-streamed-terrain-masks`
Issue: #11 (real landscape material integration), #10 (scene water acceptance), #12 (first-use CPU budget).
**Status: SOURCE CANDIDATE. NO UNITY COMPILATION, EDITMODE, PLAYMODE, GENERATED SCENE, SCREENSHOTS OR FPS MEASUREMENT BY GPT-6 IN THIS TURN.**

## Why a new shader was NOT created

The existing **Built-in** `Assets/_Game/Shaders/Terrain/LC_Terrain.shader` already implements real world-space grass, dirt and rock texturing, optional `_GroundPalette`, and vertex-color R/G/B channels:
- R: `_UseVertexMasks` / `_PathMaskStrength`, blends earth/paths;
- G: `_WetnessStrength`, darkens damp riverbank;
- B: soil/contact attenuation in another authored path (this patch leaves B=0);
- rock remains driven by actual terrain world-normal slope.

`WorldStreamer` and `ChunkMeshBuilder.Build(chunk,size)` previously never generated those R/G masks from actual `MacroWorldPlan`; the only existing example was manually painted colors in the **baked 16-chunk bridge fixture**. This new work connects the real streamer and its actual macro roads/rivers without shader duplication.

## Code / asset inventory

1. `Assets/_Game/Scripts/World/Rendering/ChunkTerrainVertexMasks.cs`
   - Pure deterministic data-to-mesh conversion: `Color32[] Build(WorldChunkData chunk, float chunkSize, MacroWorldPlan plan)`.
   - AABB-cull nearby road and river polyline segments **once per visible chunk**, then evaluate local 0.5m terrain vertices using **absolute world X/Z**, not per-chunk invented decals or extra terrain GameObjects.
   - R = feathered real road corridor at authored road `width` plus 0.85m soft edge; road mask fades under an actual river instead of painting a fake land bridge. G = wet river surface/bank from `WorldRiverData.GetWidthAtPoint` interpolated along actual segments plus 1.8m soft edge. Alpha=255. B=0, held for future reviewed woodland/soil contact mask.
   - Same absolute world seam vertex yields same R/G, including negative coordinates. No `UnityEngine.Random`, no mesh-height writes, no authoring `SurfaceKind` changes, no water prefab duplication.
   - Complexity: O(vertices × nearby segments), with per-chunk coarse AABB pruning. Profile CPU allocation and route density in a real 96×96 finite map; no optimization promise.
2. `Assets/_Game/Scripts/World/Rendering/ChunkMeshBuilder.cs` retains its exact old 2-argument `Build` call and adds **opt-in** `Build(chunk,size,plan,useVertexMasks)`, assigning `mesh.colors32` only if specifically enabled.
3. `Assets/_Game/Scripts/World/Streaming/WorldStreamer.cs`: very small call-site integration; checks `terrainMaterial.HasProperty("_UseVertexMasks")` and property >= 0.5, passes the existing macro plan to the new overload. If the material has no masks enabled the original mesh path remains exactly the same, including legacy Main and old tests.
4. `Assets/_Game/Settings/World/ConceptWorld_v001/Concept_Terrain_Masked.mat`: dedicated copy using the EXISTING `LC Terrain` shader GUID. `_UseVertexMasks=1` and `_GroundPalette=1`; modest softer grass/dirt/rock shades, gentle wetness. Main/LC_Terrain_Default unaffected. Material and all new C# files have committed stable `.meta` GUIDs.
5. `Assets/_Game/Editor/ConceptProceduralWorldSceneBuilder.cs`: explicit menu `Little Castle > World > Concept > Create Procedural World Scene`. This is a new separate scene **`Assets/_Game/Scenes/ConceptProceduralWorld.unity`** and refuses to overwrite an existing file or leave an unsaved current scene behind. It checks actual hydrated Stone Bridge v002 prefab referenced in concept spawn catalog, reviewed water fixture material, concept WorldDefinition/map preset, and mask material before creating anything.
   - Creates one real `WorldStreamer` with existing **finite** `concept_preview` preset, seed **12345**, `sessionPlayerCount=2` (only configured; NOT a network player session), root fixed in world space and focus controlled by the existing `StrategyCameraController`.
   - Uses existing reviewed water material with the streamer’s own (already desktop-tested) single per-chunk `RiverWaterPresenter`. It does NOT add another water owner, embed reference FBX water, bake 16 chunks, auto-place a player-owned village, or create placeholder bridges.
   - Uses `WorldTimeSystem`, `DayNightLightingController`, `StylizedLightingGlobals` with daylight sun/moon and the same Built-in pipeline. RTS camera edge scroll off to ease controlled profiler sweeps (WASD/middle-drag/zoom remain).
   - Creates **only the authored scene objects**, saves once to the new scene file. Actual terrain, road, river, bridge and catalog spawns appear at Play when existing `WorldStreamer` starts.
   - For exact replay, inspect serialized seed, player count and preset in scene. Do NOT equate `sessionPlayerCount=2` with actual multiplayer host/session validation.
6. `Assets/_Game/Tests/Editor/ChunkTerrainVertexMaskTests.cs` covers negative border at x=0 from (-1,-1)/(0,-1) for both actual road/water masks, same mesh triangles/bounds under opt-in path, no mesh colors in exact legacy method, river width downstream interpolation, concept material vs old legacy default, neutral alpha.

## Exact desktop work Codex must perform

Start from current reviewed PR #20 and inspect changes before applying the separate PR. Preserve all other draft branches / user's local Unity files. Use a fresh integration branch/worktree. Do not merge unrelated PR #6 or edit existing Main*/BridgeTest scene.

1. Import/review this PR in Unity 6000.5.5f1. Run compile+full EditMode and PlayMode, including new `ChunkTerrainVertexMaskTests`. Record **new** XML/log results with actual passed/failed/skipped; historical PR #20 tested 120/120 EditMode, 3/3 PlayMode, which do **NOT** verify this PR.
2. Confirm original `LC_Terrain_Default.mat` has `_UseVertexMasks=0`, authored `Concept_Terrain_Masked.mat` has `_UseVertexMasks=1`, and shader is still `Little Castle/Terrain/LC Terrain` (Built-in, not URP). Compare a legacy world scene without visual regressions.
3. From Unity menu `Little Castle > World > Concept > Create Procedural World Scene` or via batch `-executeMethod LittleCastle.Editor.ConceptProceduralWorldSceneBuilder.Create` with actual Editor/project paths, after saving current scene. Open the NEW `Assets/_Game/Scenes/ConceptProceduralWorld.unity` and press Play. It MUST truly generate streamed chunks from macro/river and actual Stone bridge catalog, not the fixed 16-chunk bridge test.
4. **Important known blocker:** finite concept profile is 96×96×32m = **3072m square**; desktop PR #20 measured real macro planning of 12345/3072 at ~70.7s in one diagnostic configuration. This scene may suffer severe cold startup stall; record the actual `MATCH READY` transition and profiler markers; do not claim this is fast or healthy. If strict bridge-aware settings are on and invalid world results, record explicit failures; do not lower requested minimum or fabricate crossings to continue.
5. Move camera into populated regions and along roads/rivers; capture **actual GPU-rendered** gameplay/overview/close/far/day/night screenshots from the scene, recording camera location and seed. Verify riverbank wet R/G masks match water mesh (no detached beige strips, no painted dry path over channel), real authored bridges have exactly +Z road, +X river, ±5.4m sockets, no ghost bridges. Near daylight and dark night conditions must both be legible.
6. Measure `ChunkTerrainVertexMasks.Build` and total mesh-build cost (CPU, p95/p99/max, managed allocations, drawcalls) with the real concept 32m/64 cells, not just old legacy 64m/32 cell synthetic water test. At default radius=4, user-visible chunks regenerate on camera moves. Revisit, unload, reenter, verify no leak or silhouette seam. Check different seeds 12345/54321/-10101/777; no claim every seed has a river or valid fixed bridge.
7. Integrate separately with landscape diagnostic [PR #26](https://github.com/DimaFi/Little-Castle/pull/26), which adds `TerrainSurfaceStage.useVariableRiverWidth` only in isolated concept settings, and [routing PR #27](https://github.com/DimaFi/Little-Castle/pull/27), which adds bounded real alternative connectivity. Their respective source tests were NOT newly Unity-run in the GPT-6 cloud.
8. Record all actual outcomes in `docs/reports/procedural-scene-acceptance-YYYY-MM-DD.md` (new date, exact SHAs, Unity version/CPU/GPU, NUnit counts/logs, seed/players/preset, screenshot paths, metrics and explicit **PASS/FAIL/BLOCKED**). Do not overwrite original desktop verification evidence or declare Little Castle's visual world finished without review.
9. Commit the **generated new scene** and its automatically created `.meta` only after real Unity review. This code PR does not include a fabricated `.unity` scene or screenshots, because it has not run in the cloud environment.

## Important limitations and non-goals

- This renders **real logical path and river masks** in existing shader; it does not yet map `SurfaceKind.WoodlandFloor/Mud/Settlements` to full distinct material splat channels. Shared shader keeps original grass/rock normal-dependent behavior; art-specific texture installation and real cliffs/outcrop LODs still need later reviewed work.
- Macro generation, river placement, strict connectivity and actual neutral-village prefab catalog completeness are separate issues. A valid scene file doesn't imply a connected/equitable multiplayer map.
- No additional primitive placeholder terrain beyond existing generated heightmesh. Material palette and vertex masks are a first inspectable visual pass, not a guarantee of Tiny Glade-level final art quality.
- Runtime generated scene may expose severe first-use synchronous macro world stalls. These are explicit acceptance failures until profiled/resolved, not hidden by lowering chunk budgets.
- This source branch is **DRAFT, NOT COMPILED/EXECUTED IN UNITY**. All tests remain planned, not falsely marked PASS.
