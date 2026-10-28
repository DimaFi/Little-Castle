# Bridge Stone A v002 — Unity import and Codex verification handoff

Date: 2026-10-08
Executor: GPT-6 in ChatGPT; former Sol 6 / High task.
Game base: current-unity-fix at c61a6de1af90c35a843b39325bdc007c1297ff42.
Branch: gpt6/bridge-v002-import-preflight
Issue: https://github.com/DimaFi/Little-Castle/issues/9
Status: SOURCE PREPARED. FULL LFS, UNITY COMPILE/IMPORT/VISUAL/PERFORMANCE NOT VERIFIED.

## Files prepared

- Assets/_Game/Editor/BridgeStoneAssetIntegration.cs — opt-in Editor import with full v002 release SHA-256 checks before copies.
- Assets/_Game/Models/Concept/Bridge_Stone_A/BridgeStoneNearColliderBudget.cs — local near-camera collision budget for generated concept prefab.
- Assets/_Game/Tests/Editor/BridgeStonePrefabTests.cs — Unity EditMode tests requiring actual imported prefab.
- Stable .meta GUIDs for new code/folders. Importer does not automatically change Main settings or any scene.

## Importer operation

1. Read LITTLE_CASTLE_BRIDGE_RELEASE environment variable as an absolute **hydrated** path to Releases/Bridge_Stone_A/v002 in a clean art repo checkout.
2. Require asset_id ENV_Bridge_Stone_A and contract version v002. Verify all 31 immutable release-manifest entries by SHA-256, reject unhydrated LFS pointers, missing assets and unsafe paths.
3. Refuse to overwrite an existing v002 import/prefab.
4. Copy release files byte-for-byte into Assets/_Game/Art/Imported/Bridge_Stone_A/v002 and import the FBX/texture assets.
5. Build separate structural and dressing Unity LODGroups. Structural triangle expectations: 13598 / 7704 / 1736. Dressing: 12428 / 5022 / 80. Require reviewed 286-triangle collision. Do not put SM_Bridge_Water_A or SM_Bridge_TerrainReference in generated prefab.
6. Create shared Built-in LC Stylized Lit materials. Keep faction cloth independent and neutral-ready; custom player tint/emblem requires actual scene validation, never instantiate renderer.material clones.
7. Make far LOD renderers shadow/probe/motion-vector cheap. Add near-camera collision budget (default 80 m, check Camera.main correctness on PC).
8. Save Assets/_Game/Models/Concept/Bridge_Stone_A/Bridge_Stone_A_v002.prefab and register ENV_Bridge_Stone_A in isolated Concept_MainWorldSpawnCatalog ONLY. Root rotation 0 and scale 1. Do not edit Main*.
9. Source release immutable. Import artifact binaries, prefab and generated .meta are NOT committed in this draft until real Unity import and review.

## Dependencies

- Review/verify game Issue #7 draft PR #14 first (source tests were never run through Unity after edits).
- Review/verify art Issue #1 draft https://github.com/DimaFi/Little-Castle_Assets/pull/2 on **fresh hydrated Git LFS checkout**, with Python validator and actual Blender assets.
- Review/verify routing Issue #8 draft PR #15. Do not automatically merge PR #6.
- Afterwards execute this importer; then give concept scene ownership to water/approach issue #10, then landscape #11, then performance #12.

## Exact desktop commands (Windows PowerShell)

Art repo clone:

    git clone https://github.com/DimaFi/Little-Castle_Assets.git Little-Castle_Assets_validation
    cd Little-Castle_Assets_validation
    git lfs install
    git lfs pull
    python Tools/verify_portable_releases.py --json
    python -m unittest discover -s Tools -p 'test_portable_releases.py' -v
    python Tools/sync_asset_book.py
    python Scripts/asset_book.py validate-catalog

Set $env:LITTLE_CASTLE_BRIDGE_RELEASE to the resolved absolute art release directory. Use Unity Editor 6000.5.5f1:

    $env:LITTLE_CASTLE_BRIDGE_RELEASE = (Resolve-Path "path/to/Little-Castle_Assets_validation/Releases/Bridge_Stone_A/v002").Path
    $Unity = "path/to/Unity/6000.5.5f1/Editor/Unity.exe"
    $Project = (Resolve-Path "path/to/Little-Castle").Path
    & $Unity -batchmode -nographics -projectPath $Project -executeMethod LittleCastle.Editor.BridgeStoneAssetIntegration.ImportFromEnvironment -quit -logFile "$Project/Logs/bridge-v002-import.log"
    & $Unity -batchmode -nographics -projectPath $Project -runTests -testPlatform EditMode -testResults "$Project/Logs/bridge-v002-EditMode.xml" -quit -logFile "$Project/Logs/bridge-v002-EditMode.log"
    & $Unity -batchmode -nographics -projectPath $Project -runTests -testPlatform PlayMode -testResults "$Project/Logs/bridge-v002-PlayMode.xml" -quit -logFile "$Project/Logs/bridge-v002-PlayMode.log"

Do not treat ignored BridgeStonePrefabTests as PASS. They are ignored when no real prefab exists. After import, they must actually run.

## Expected inspection and known risks

- Unity FBX submesh naming/import offsets/material import might differ from reviewed Blender data; importer currently source-only and must compile and run before accepting. Never weaken SHA gate or generate placeholder cubes.
- Use actual near/mid/far, day/night screenshots for model, foliage, flags, materials, terrain, shadow and clipping; verify flags at right-hand caps and material tint without per-instance clones.
- Verify position at baseElevation, rotation yaw, pivot, uniform scale 1. Existing MacroFeatureProjectionStage generates the fixed bridge spawn with these values.
- Verify near collider enabled at close camera position and disabled far away, local streaming unloading and runtime delta without duplicate instances.
- New Unity binary imports and generated .meta must be committed through reviewed LFS rules before calling clean checkout portable.
- No water or reference terrain mesh in this prefab; issue #10 owns shared water presentation.
- This report cannot claim Unity compilation, scene visual correctness, real rendered screenshots or GPU frame timings.

## Evidence and Codex reporting

Attach import log, exact SHA of each merged PR/head, new dated NUnit XML totals including ignored tests, Unity version, art LFS verification JSON, prefab/path hash, mesh/LOD triangle sums, collider states, catalog mapping, scene screenshot paths, memory/FPS benchmark hardware notes, and PASS/FAIL/BLOCKED matrix.

Always preserve original docs/reports/verification-2026-10-08 XML. Use fresh evidence files with new dates. No reset/force-push, no silent conflict resolution or automatic merge.