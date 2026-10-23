# Little Castle — Graphics 2.0 / URP master plan

Date: 2026-10-23 | State: **roadmap approved for isolated trial; migration not executed** | ADR: [ADR-0004](../decisions/ADR-0004-urp-graphics-2-evaluation.md)

## Mission

Make Little Castle's own recognizable **cozy medieval strategy** aesthetic significantly stronger than today's baseline: painterly-but-materially-believable cliffs, lush restrained vegetation, roofs/walls that read at strategy scale, warm windows at night, natural contact/shoreline, cinematic but gameplay-readable composition. Avoid cloning Tiny Glade or importing proprietary assets. High art quality is **not** the same as expensive effects.

**Pipelines:** Built-in remains working main; URP is target for tested modern rendering. Unity version observed: 6000.5.5f1. Windows PC first; other targets TBD. Use current local Unity scene/assets to establish real baseline before changing anything.

## Read in order

1. [Repository audit](repository-audit.md): actual facts and known risks.
2. [Migration runbook](migration-runbook.md): executable and reversible order of work.
3. [Visual direction](visual-direction.md): how to improve the image, prioritized and concrete.
4. [Verification gates](verification-gates.md): required screenshot matrix, metrics, regression gates.
5. [Reference research](reference-research.md): confirmed renderer choices and transferable techniques.
6. [Codex handoff](codex-handoff.md): strict tasks, ownership, required proof.
7. [Backlog](implementation-backlog.md): workstreams, dependencies and issue mapping.

## Non-negotiable engineering boundaries

- Read `AGENTS.md`, `docs/architecture/stylized-rendering-roadmap.md`, `docs/architecture/material-texture-contract.md`, `docs/architecture/foliage-wind-authoring.md`, `docs/architecture/night-lighting-and-lod.md`, `docs/architecture/world-streaming.md`.
- **Data != presentation.** Leave world generation, map seeds, multiplayer authority, save/delta state, fog visibility logic, gameplay clock unchanged. Never spawn full maps to fix rendering.
- Maintain shader global state `_LC_SunDirection`, `_LC_SunColor`, `_LC_Moon*`, `_LC_Ambient*`, `_LC_Wind`, `_LC_GameTime`.
- Preserve authored source maps and common UV-space alignment; do not regenerate BaseColor/Normal/AO/Roughness independently. Map Roughness to appropriate URP smoothness semantics intentionally; test.
- First foliage task runs actual selected-tree compatibility preflight from AGENTS; wind and ShadowCaster must agree.
- No extra vendor packages or purchased assets for baseline. Use owned original assets and separately licensed references; do not commit store assets to this public repo.
- Main keeps shipping gameplay; migration done on an explicit branch and only merged after all gates and review.

## Milestone path (finish each gate before proceeding)

| Phase | Deliverable | Exit condition |
| --- | --- | --- |
| G0: Baseline | Full existing-world/shader/material inventory, fixed before screenshots, CPU/GPU frame captures, missing local packages | Signed evidence folder; reproducible control scene |
| G1: URP skeleton | Compatible URP via Unity Package Manager, Unity-created pipeline assets, Forward/Forward+ tier experiment, Linear/Gamma A/B | Test scene opens, basic URP Lit works; untouched main |
| G2: Shader parity | URP equivalents of LC surface/terrain/foliage/grass/sky/emissive/water/distant + shadows/fog/depth variants | Material visual contracts and shader/wind tests pass |
| G3: Golden slice | One playable cliff–path–house–trees–shore section with correct grounded palette | Owner approves day/twilight/night at two zooms |
| G4: Lighting + surfaces | Sun/moon, contact shadows, art-directed gradients, sensible post-processing, optional SSAO/decals | Better appearance in side-by-side; measured cost |
| G5: Environment systems | Terrain texture distribution, cliffs, vegetation, wind, river edges, dirt trails, grass LOD | No visual seams or flicker; performance stable |
| G6: Scale | Frame Debugger/Profiler, SRP Batcher, batching vs GPU Resident Drawer trials, quality tiers, fog-of-war | 2/8/16 player and multi-size map streaming proof |
| G7: Production gate | Full regression, packaged builds, document settings, final screenshots, rollback | Dedicated implementation PR reviewed and approved |
| G8: Later enhancements | Optional APV, season/weather, tasteful local fog, special effects, custom Render Graph effects | Only after G7 with demonstrated gameplay benefit |

## Work organization

This branch records **documentation only**. Technical implementation should branch from the reviewed/merged roadmap state, in small ownership-scoped PRs. Do not mix renderer conversion, large environment reauthoring, and world-generation optimization into one PR. Rebase/cherry-pick deliberately if root/world agents change main.

**Priority order by expected artistic impact:** (1) art direction + correct Linear/Gamma handling; (2) terrain/cliff geometry and transition; (3) coherent sun/ambient/shadow/contact; (4) authored vegetation variation and wind; (5) near/far material readability; (6) shoreline and small environmental storytelling; (7) refined restrained post-processing; (8) expensive optional effects only after profiling.

## Current status (accurate at commit creation)

Only planning and remote repository inspection were done. **No URP package was installed. No project was opened in Unity, no test was run and no new FPS/visual quality was measured.** Local uncommitted Unity changes and installed Starter asset pack are unknown. Codex on user's PC must complete the hands-on preflight.
