# ADR-0004 — URP-based Graphics 2.0, gated migration

Date: 2026-10-23
Status: **ACCEPTED for isolated evaluation; NOT approved for main production switch**
Owner: Little Castle rendering/technical art
Target editor observed in main: Unity **6000.5.5f1**

## Context

Little Castle currently runs **Built-in Render Pipeline**, confirmed by:
- `Packages/manifest.json`: no `com.unity.render-pipelines.universal` dependency;
- `ProjectSettings/GraphicsSettings.asset`: `m_CustomRenderPipeline: {fileID: 0}`;
- `ProjectSettings/QualitySettings.asset`: all checked quality levels use `customRenderPipeline: {fileID: 0}`;
- `LC_StylizedLit`, `LC_Foliage`, `LC_Grass`, `LC_Terrain`, `StylizedDayNightSky` and effect/distance shaders implemented for the Built-in passes/includes;
- the canonical `docs/architecture/stylized-rendering-roadmap.md` currently tells agents not to migrate without a measured decision.

Unity's 2026 strategy prioritizes URP and begins Built-in deprecation with Unity 6.5. This is a long-term maintenance argument, **not evidence that URP alone improves Little Castle's appearance or FPS**.

Project constraints: finite procedural 2–16 player match worlds, chunk streaming, deterministic world IDs, reusable original authored models, close-up/strategy zoom, existing global `_LC_*` lighting/wind contract, continuous day/night clock, night-light budgets, fog of war separate from gameplay.

## Decision

1. Adopt **URP as the target renderer to evaluate for the future production pipeline**. Default experiment: Forward+ on capable Windows PC, with Forward as a lower-end fallback candidate measured independently. HDRP is not the baseline.
2. Authorize a **separate version-controlled branch** and small reversible experiments, golden-scene benchmarks, shader ports and art-direction iterations.
3. **Retain Built-in on main** until the acceptance gates are met and a separate review authorizes the switch. A failing experiment can be abandoned without contaminating gameplay.
4. Do not replace existing original gameplay, generation, save/network contracts, shader globals, or art content to make the migration superficially pass.
5. Require explicit visual and performance evidence, not a fixed promised FPS gain. Keep open the possibility of not shipping URP immediately if serious blockers are found.
6. Retain and adapt existing material property naming where possible (`_MainTex`, `_BumpMap`, `_RoughnessMap`, `_OcclusionMap`, opacity, wind masks) via compatible URP shaders/conversion mapping. Source texture files and UV layout remain valid; shader code and color results need retesting.
7. `MaterialPropertyBlock` optimization is **not universally free**: it can make objects ineligible for GPU Resident Drawer; use material variants/vertex data or measured alternative for large forests. Preserve `_LC_*` global parameters without runtime per-object material instantiation.
8. Use URP Render Graph for *new* custom passes; do not build new work on obsolete non-Render-Graph Compatibility Mode.
9. No Asset Store package dump in public GitHub. Original assets or explicitly redistributable licensed content only.

## Relationship to earlier guidance

This ADR provides the explicit, conditional architectural decision requested by the earlier no-migration warning in `docs/architecture/stylized-rendering-roadmap.md`. That warning still protects `main` and ordinary art tasks. This experiment is the narrow documented exception; it does **not** silently mark Built-in deprecated in the currently released project. Agent must first read `AGENTS.md` and existing render docs.

## Why not HDRP?

HDRP can deliver excellent high-end results (e.g. V Rising) but brings additional integration/performance scope for our changing, large streamed worlds. Rather than chase photorealism, focus resources on atmospheric design, authored assets, grounding, lighting, coherent materials and readable silhouettes. Reassess only with demonstrable URP technical limitations.

## Implementation and acceptance

Authoritative full plan: `docs/graphics-2/README.md`, `migration-runbook.md`, `visual-direction.md`, `verification-gates.md`, `codex-handoff.md`.

Promotion requires:
- Same seed, same art/assets, fixed camera and day time A/B in **player builds**, not just editor.
- 0 magenta materials, 0 missing shader/material refs and 0 shader compile errors on specified supported quality tiers.
- Positive visual scorecard signed off by owner for terrain contact, readable roads/walls, houses, cliffs, foliage and day/night, at close and strategy zoom.
- Stable or justified improved CPU/GPU frametimes/VRAM, unacceptable regressions eliminated. Record hardware and baseline metrics. Do not invent data.
- Correct wind displacement in both forward and shadow passes; correct fog/fog-of-war and camera stacking compatibility if used.
- Deterministic spawn IDs, chunk seams, rebuild/revisit and game-clock simulation unchanged; 2/8/16-player map profiles checked.
- CI/static checks + Editor tests + visual Unity Player acceptance.
- Migration PR independent from feature enhancements; explicit rollback instructions.

## Alternatives considered

- **Stay Built-in permanently**: least immediate effort; worse forward-looking ecosystem trajectory and blocks URP-first assets, but remains fallback until proven.
- **Go HDRP now**: aesthetic ceiling is not automatically higher for this art style; runtime/procedural integration cost is unwarranted.
- **URP + replace all materials at once**: rejected for excessive breakage risk. Prefer staged compatible ports with unchanged originals on main.

## Primary references

- Unity strategy: https://unity.com/topics/render-pipelines-strategy-for-2026
- Unity URP docs (6000.5): https://docs.unity.com/en-us/engine/6000.5/manual/render-pipelines/universal-render-pipeline
- Built-in→URP conversion: https://docs.unity.com/en-us/engine/6000.0/manual/render-pipelines/universal-render-pipeline/introduction/installing-and-configuring-urp/upgrading-from-birp/upgrade-shaders/birp-urp-custom-shader-upgrade-guide
- GPU Resident Drawer limitations: https://docs.unity.com/en-us/engine/6000.5/manual/analysis/graphics-performance-profiling/in-urp/reduce-draw-calls-urp/reduce-rendering-work-on-cpu/gpu-resident-drawer
