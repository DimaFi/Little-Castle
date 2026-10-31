# G1–G7 — URP migration runbook (for Codex + desktop Unity)

**Do not execute until baseline G0 is recorded.** This plan is not itself a completed migration. Keep original main unmodified.

## 0. Branch safety and rollback

- Roadmap branch: `graphics/urp-graphics-2-roadmap-2026-10-23`.
- Implementation: create a fresh `graphics/urp-foundation` branch after roadmap is available, off the agreed current base. Verify local `git status` first. Don't discard work from other Codex agents.
- Tag/log baseline commit + capture current `Packages`, `ProjectSettings`, `Assets/_Game/Shaders`, `Materials`, scenes, prefabs and GUID/`.meta`.
- Keep separate disposable Unity project copy if running bulk `Render Pipeline Converter`.
- If launch fails, checkout original commit/branch in a separate worktree or restore snapshot. Never restore only URP package without matching project settings/material/shader state.

## 1. Install package carefully (no guessed version)

In **Unity Editor 6000.5.5f1**, open Package Manager > Unity Registry > Universal RP; select the editor-compatible released package. Record exact `manifest.json`, lockfile, dependencies and package version (do not hardcode an unverified version in plan). Create `Assets/_Game/Rendering/URP/` assets using Unity menu (URP Pipeline Asset and Universal Renderer Data) so GUIDs/YAML are generated correctly; commit `.meta`.

Start with two experimental quality assets:
- `LC_URP_PC_High`: Forward+ candidate, HDR output, shadow cascades, restrained postprocessing; optimize only after benchmarks.
- `LC_URP_PC_Low`: Forward candidate if compatible with light budget, shorter shadows, no expensive screen effects.
Assign pipeline asset in Project Settings > Graphics **and inspect all Quality tier overrides**; quality is allowed to override Graphics. Verify correct renderer and no duplicate asset mappings.

Avoid committing a project-wide irreversible converter pass before manually porting representative shared materials. URP's converter only converts compatible built-in shaders; **it does not convert our custom LC shader code**.

## 2. Representative shader parity (one family at a time)

First establish URP baseline with built-in `Universal Render Pipeline/Lit` on temporary **duplicate test materials**, then author URP counterparts under a new namespaced prototype folder; do not destroy originals in the experiment's early stage.

Port order and exact tests:
1. **Opaque `LC_StylizedLit`**: URP `UniversalForward`/`UniversalForwardOnly` as appropriate, `ShadowCaster`, `DepthOnly` and `DepthNormalsOnly` if chosen SSAO/decals need; URP lighting/shadow includes; check main/additional local lights and `_LC_*` artistic tint; no Built-in ForwardAdd reliance.
2. **`LC_Terrain`**: grass/dirt/rock and vertex masks, chunk UV/world-space tiling, sun shadow receipt, normal response; seam tests after chunk load/reload.
3. **`LC_Foliage` and `LC_Grass`**: alpha clip/culling/two-sided normals, light transmission, GPU wind in rendered + shadow + depth/motion-vector passes where required. Real mesh preflight first. UV Y/vertex R masks, shadow silhouette and TAA ghosting check.
4. **`StylizedDayNightSky`**: URP skybox compatibility with dynamic sun/moon/time; no dependence on arbitrary RenderSettings state that changed.
5. **`LC_EmissiveGlow`, `LC_NightLightPool`**: emission visible at night, stable transparent sorting/depth, budgeted point/spot lights; don't create one realtime light per window.
6. **`LC_DistantSimple`**, wheat and any water/bridge/placeholder/vendor shaders actually used: alpha, shadows, fog and simplified far LOD.

Preserve shader property contracts for existing material assets. Where names/cbuffers differ, add deterministic editor migration scripts with dry-run and log, not hidden runtime renaming. Unity `Render Pipeline Converter` can handle Unity built-in standard materials in controlled batches only.

## 3. Color/lighting matching

- **Gamma vs Linear:** project reports Gamma in existing docs; test the current actual `PlayerSettings` before changing. Linear is a candidate, not free win; remap color/intensity/shadow palettes using side-by-side and player builds. Record texture import sRGB changes; keep alpha and scalar maps correct.
- Directional sun/day-night moon driven by existing `WorldTimeSystem` and `DayNightLightingController`; do not duplicate authority.
- Use toned indirect/ambient fill; sunrise/day/night split artistic settings while preserving management-game legibility; control very bright sky/overexposure.
- Probe/APV: evaluate only a separate hand-authored *static* golden scene first. Runtime-changing procedural worlds cannot assume a baked APV just works. Fallback: ambient hemispheric fill + probes for reusable static settlements.
- Keep nighttime realtime point/spot lighting inside existing `NightLightBudgetManager`, prove in crowded village.
- SSAO **after** correct normal/contact/terrain grounding, start moderate and check halos. Decals for contact mud, roads and stone edge details only if appropriate. Do not enable costly opaque/depth textures on every tier without need.
- Post-processing via URP Volumes: subtle color/tone, white balance, mild bloom on emissive windows, no excessive vignette/sharpen/chromatic aberration; no Depth of Field that harms strategy readability.

## 4. Rendering path, shadows, AA, Render Graph

- Benchmark Forward vs Forward+ using same scene with many nighttime local lights; Forward+ is candidate, not dogma.
- Start AA candidates: MSAA 4x vs TAA, optionally SMAA, **not TAA+MSAA**; monitor thin grass/wheat and moving windy foliage ghosting/shimmer. TAA incompatible with camera stacking in Unity URP versions documented.
- Shadows: main sun soft shadows, 2–4 cascades as trial, camera-dependent distances, rock/house contact; turn off detail-grass shadows far away. Never claim high settings free.
- GPU Resident Drawer requires compatible Forward+/Deferred+ and suitable renderers, compute-capable API and **no MaterialPropertyBlocks** on participating renderers; assess effect on deterministic per-object tint workflow.
- Render Graph remains default; any custom SSR/refraction/fog passes require a measured business case and modern Render Graph, not Compatibility Mode.
- No HDRP-only features promised (volumetric fog, SSR, ray-traced GI, DLSS) as built-in URP toggles. Unity roadmap previews are not shipping guarantees.

## 5. Golden scene -> scalable world

Create a small original **40–60m representative environment slice** (adjust to real world meters): dirt road bends next to cliff, house/stone foundation, 2 tree families, bush + flowers, grass/wheat patch, rock clusters, small river bank/bridge. **No production biome/generation rewrite yet.**

A/B: current Built-in and URP with identical seed, props, camera, day time. Iterate geometry material scale, roughness, AO, contact and palette before cinematic effects. Once accepted, promote original modular assets through `Assets/_Game/Models/Ready`, materials, prefab catalog and existing streamed presenter.

## 6. Optimization and quality levels

Profiler & Frame Debugger on standalone Windows Player; measure cold build + warmed, close/far, day/night, world bootstrap vs first-visit chunk and replayed chunk. Compare:
- SRP Batcher compatibility (`UnityPerMaterial` CBUFFER per shader).
- GPU Resident Drawer vs standard instancing vs static batching **rather than stacking incompatible techniques blindly**.
- Shared materials/texture atlases or arrays where actually beneficial; avoid per-object material clones.
- LODGroup and authored LOD0/1/2 meshes, billboard/impostor only after art approval; fog/range/haze to hide distance without removing navigational silhouettes.
- For foliage validate alpha overdraw, mips, cutout thresholds, polygon counts, contact shadow cost and far zoom shimmer.
- Production-friendly shadow distance, lights, SSAO and density per Low/Medium/High visual tier while *preserving gameplay IDs, paths, units, fog logic and fairness*.

## 7. Merge/rollback policy

No merge just because editor opens. Gated acceptance in `verification-gates.md` plus separate PRs and owner approval. Change `docs/architecture/stylized-rendering-roadmap.md` and `AGENTS.md` to URP baseline **only after approved production switch**, not now. If an unacceptable regression is found, keep Built-in current production and record the blocker rather than forcing migration.

Official migration: https://docs.unity.com/en-us/engine/6000.6/manual/render-pipelines/universal-render-pipeline/introduction/installing-and-configuring-urp/upgrading-from-birp/migrating-from-birp-workflow
Official custom shader warning: https://docs.unity.com/en-us/engine/6000.0/manual/render-pipelines/universal-render-pipeline/introduction/installing-and-configuring-urp/upgrading-from-birp/upgrade-shaders/birp-urp-custom-shader-upgrade-guide
