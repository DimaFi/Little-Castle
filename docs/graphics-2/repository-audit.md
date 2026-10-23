# G0 — Static repository graphics audit (2026-10-23)

Scope: inspected committed `DimaFi/Little-Castle` main at `b337687cfee7d6ded9a14677d64edfe8f164a56f`; **not** a Unity Player/editor run. Update with local working tree details before any conversion.

## Confirmed from committed files

- `ProjectSettings/ProjectVersion.txt`: `6000.5.5f1`.
- `Packages/manifest.json`: `com.unity.test-framework`, `com.unity.modules.physics`; no explicit URP package.
- `ProjectSettings/GraphicsSettings.asset`: `m_CustomRenderPipeline: {fileID: 0}` — Built-in.
- `ProjectSettings/QualitySettings.asset`: six predefined tiers seen; all `customRenderPipeline: {fileID: 0}` — changing only Graphics Asset can be overridden later if quality assets differ.
- `docs/architecture/stylized-rendering-roadmap.md` identifies Built-in as baseline and requires measured ADR to change.
- `Assets/_Game/Shaders/Surface/LC_StylizedLit.shader` uses `ForwardBase`, `ForwardAdd`, `ShadowCaster`, `CGPROGRAM`, `UnityCG.cginc`, `Lighting.cginc`; **not a URP-native shader**.
- `Assets/_Game/Shaders/Foliage/LC_Foliage.shader` has vertex wind + separate ShadowCaster deformation; `ForwardBase` and `CGPROGRAM` need URP equivalents. Check topology before changing.
- `Assets/_Game/Shaders/Terrain/LC_Terrain.shader`: Built-in ForwardBase/ShadowCaster, three current terrain textures plus vertex mask/macro coloration.
- Also present in repo: `LC_Grass`, `LC_DistantSimple`, `LC_EmissiveGlow`, `LC_NightLightPool`, `StylizedDayNightSky`.
- Rendering C# systems: `DayNightLightingController`, `StylizedLightingGlobals`, `NightLightBudgetManager`, `NightLightEmitter`, `NightLightPoolVisual`, `ChunkMeshBuilder`, `ChunkSpawnPresenter`.
- Automated checks: `StylizedRenderingValidator`, `StylizedRenderingContractTests`, `NightLightingRuntimeTests`, `WorldStreamerPlayModeTests`.
- Scene: `Assets/_Game/Scenes/WorldGenerationTest.unity`.
- `docs/research/INNERVERSE_PACKS_STUDY_2026-10-22.md` reports Starter is already locally imported (not verified in remote repo) and advertises URP-only publisher support; do not assume remotely present.

## Mandatory dynamic audit on user's Unity machine

1. Confirm current checked-out branch, `git status`, unpublished art/shader changes, and editor version; preserve local changes before checkout.
2. Dump actual packages from Package Manager / `Packages/packages-lock.json` if present; inspect custom asset GUIDs and current prefab textures.
3. Open `WorldGenerationTest.unity`; execute Voxel Generate → Generate Preview → Play. Capture baseline errors, null refs and shader status.
4. From identical seed: daylight 12:00, low-angle sunlight 18:00, night 23:00; two zoom levels; representative cliff/path/bridge/settlement/forest.
5. Inventory shaders, pass types, material props/keywords and prefabs; mark custom vs Unity built-in, shader compilation, depth/shadow, alpha cutout, double-sided behavior, fog.
6. Run `Little Castle -> Rendering -> Validate Stylized Rendering`, foliage model validator for *real* production tree, and EditMode tests; document exact menu found (legacy docs have variant names).
7. Profile **standalone Player**: CPU main/render thread, GPU frame time, draw calls/batches, triangles/overdraw, texture/mesh memory, allocations and spikes. Record resolution, VSync, graphics API, GPU/CPU, driver, Unity build, quality.
8. Capture chunk stream in/out, far-zoom forest and full night settlement; check local light budget and shadow draw calls. Validate F8 overlay where supported.
9. Audit all texture import settings against `material-texture-contract.md`: sRGB BaseColor ON; Normal imported as normal; AO/Roughness/Opacity sRGB OFF; alpha clip; mipmaps; source map alignment; tangent space.
10. Detect non-project-owned assets; never copy third-party package files to public repo. Asset Store Starter free does not mean free redistribution.

## Risk register (owner must update)

| Risk | Why real in current project | Mitigation |
| --- | --- | --- |
| Pink materials after URP switch | Built-in custom passes/includes | Port each LC shader deliberately before broad asset conversion |
| Altered moon/sun and local lamps | Custom surface ForwardAdd and LC globals | Preserve constants/behavior; separate lighting test suite |
| Wrong soft shadows | Vertex wind ShadowCaster + pipeline shadow keywords | Render close/far foliage shadow parity |
| Unrealistic rocks/terrain | Texture scale, color management, normals, contact lacking | Golden slice and art direction first, not SSR |
| Slower high-density forest | MPB vs GPU Resident Drawer incompatibility, overdraw | Compare SRP Batcher, instancing, drawer using real meshes |
| GI assumptions invalid | Fully procedural, runtime-changing chunks; baked probes cannot follow changes automatically | Treat APV as separate prototype, do not enable globally |
| Large automated converter diff | Affects materials/quality and Unity YAML | Snapshot branch, convert in disposable copy first, review GUID/meta |
| Hidden external asset breakage | Starter/package installed only on desktop | Audit locally before converting |
| Incorrect old docs | Built-in docs valid on main, ADR only trials | Clearly separate statuses until promotion |
