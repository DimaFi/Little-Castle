# Fixed bridge crossing presentation — preflight contract and pending Unity work

Date: 2026-10-08; executor GPT-6 in ChatGPT.
Issue: https://github.com/DimaFi/Little-Castle/issues/10
Branch: gpt6/bridge-water-contract-preflight from current-unity-fix at c61a6de.
Status: **DATA-TO-PRESENTATION CONTRACT PREPARED; GLOBAL WATER RENDERING NOT YET IMPLEMENTED/VERIFIED**.

## Repository finding

The current-unity-fix repository has RiverTerrainCarvingStage and FixedBridgeTerrainStage, but no explicit RiverWaterPresenter/global river-water rendering implementation in Assets/_Game. Blender release v002 contains SM_Bridge_Water_A.fbx, but it is strictly an authored reference/visual, NOT suitable as a second game-world water surface. The prefab importer in #9 draft PR #16 deliberately excludes it. This means the complete #10 acceptance requires an actual single-owner water presentation system and Unity scene integration AFTER #9 review.

## What this draft implements

New read-only Assets/_Game/Scripts/World/Rendering/BridgeSitePresentationUtility.cs:
- Validate the fixed archetype/version before deriving any geometry;
- Compute root position (site XY maps to Unity X/Z), south/north road sockets at local Z = -5.4/+5.4, river edge anchors at local X = -7/+7, and absolute water height at baseElevation - 0.85;
- Convert localXZ to rotated world space using existing FixedBridgeSiteProfile, including negative coordinates, without duplicating transform math;
- Classify protected local river channel and fixed bridge footprint for a future shared water-owner implementation, and sample authored deck crown only in 2.86-m clear corridor;
- No new terrain stamp, mesh/prefab, shader, object spawn, or modification to authoritative MacroWorldPlan/FixedBridgeSiteProfile/WorldStreamer.

New Editor tests BridgeCrossingPresentationTests.cs cover yaw 0/37/90/180, negative world positions, water/deck heights, road sockets, channel wet vs bank dry, rejects unknown bridge contract. Tests are source only; **Unity Editor tests NOT RUN**. Stable .meta GUIDs committed.

## Remaining integration / explicit handoff to Codex

Only AFTER desktop verification of game #7 (PR #14), routing #8 (PR #15), hydrated art and import #9 (PR #16):
1. Create a global single-owner river water presentation system or reuse an actually present river renderer if introduced by concurrent PRs. Do not overlay v002 local SM_Bridge_Water_A on it. Need one waterline per river consistent with site bridge waterY and continuous upstream/downstream tiles.
2. Own water meshes per streamed chunk or managed region; never materialize every river across finite map in one GO mesh. Use a stable river/site ID and chunk coordinates to avoid duplicates on streaming load/unload. Preserve world-space seam vertices across negative chunk borders.
3. For each fixed site use GetWorldAnchors and IsProtectedWaterSurface to prevent double water geometry at the bridge. Tie material and water Y to existing fixed site contract (baseElevation - 0.85). River/terrain intersection must not have dry holes or z-fighting; test 4 bridge rotations and two ends.
4. Road-to-bridge transition needs actual polyline alignment to ±5.4 sockets and natural bank slope; do NOT move static FBX/Stretch requiredSpan or manufacture phantom perpendicular rivers. Address steep local river grades via deterministic route failure, not teleport.
5. Vegetation/bank decor belongs to stable site, does not block clear path or duplicate generic scatter; check authored dressing LODs.
6. Build a separate ConceptBridgeTest.unity scene only after #9 finishes its prefab ownership handoff. Validate gameplay/close/mid/far, night/day, negative-border, movement/collider, stream unload/reload, and runtime deltas.
7. Attach actual measurements and screenshots, shader/material compatibility and stability across same/different seeds. Keep Main* profiles unchanged and preserve Built-in renderer.

## Acceptance matrix (all still pending)

- Fixed waterY = baseElevation - 0.85 at all authored sites — **UNIT TEST SOURCE PRESENT, UNITY NOT RUN**.
- Correct sockets and yaw for negative border chunks — **UNIT TEST SOURCE PRESENT, UNITY NOT RUN**.
- One global water surface, no duplicate locally embedded water — **NOT IMPLEMENTED**.
- Fully streamed river water (load/unload, seam, buffer) — **NOT IMPLEMENTED**.
- Visual banks and approach grades — **NOT IMPLEMENTED**.
- Scene screenshots and GPU benchmarking — **NOT RUN**.

Do not merge this source draft as a completed water rendering feature. Unity 6000.5.5f1 and actual scene tests must determine next implementation steps. Record PASS/FAIL/BLOCKED against the acceptance criteria in Issue #10 with new dated evidence.
