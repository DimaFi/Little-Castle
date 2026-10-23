# Graphics 2.0 — Evidence, QA & performance gates

This is a **test protocol**, not a claim tests have passed.

## Test scene and capture matrix

Prepare scene `LC_GraphicsGoldenSlice` alongside existing `WorldGenerationTest.unity` (do not replace). Include a camera marker and stable world seed, fog/grass/wheat/rock/bridge/house and lantern. Save fixed transform/FOV/exposure/shader settings, graphics API, resolution, Unity revision.

Screenshot coordinates to record per test:
- Camera: close/inspection vs normal strategy zoom vs very far silhouette (last optional but strongly preferred).
- Time: 12:00 noon; 18:00 evening/sunset; 23:00 night; optional cloudy/overcast.
- Environment: house + road; cliff + grass; river + bridge; dense forest; clustered nighttime town.
- Modes: Built-in main baseline vs URP experimental; Low, Medium, High where practical.
- Comparison: align source camera/time/exposure as closely as possible; disable adaptive/random effects for capture; store image pair with settings JSON/TXT and image filename convention `<pipeline>_<seed>_<camera>_<time>_<quality>_<hardware>.png`.

**Never create "pass" screenshots from concept art or reference marketing images.** Manual aesthetic approval must use screenshot from running target game.

## Automated/static gates

- `git status` clean before starting; no unrelated renderer/world content mixed in change.
- `Packages/manifest.json` and lockfile coherent; ProjectVersion fixed and package compatible.
- No missing scripts/GUIDs or custom LC shader in magenta; build output / console shader errors = 0.
- Existing `StylizedRenderingContractTests` and `StylizedRenderingValidator` adapted for *current experimental* renderer, not blindly removed; new URP-specific tests check shader name/property mappings, passes, quality asset assignments and LC globals.
- Existing EditMode/PlayMode suite executed, failures reported by test name; state of scene tests and Play builds separated.
- Model preflight for leaves: trunk separate/mask, UV0/normals/tangents, consistent vertex deformation in visible, ShadowCaster and depth/motion-vector paths where used.
- Chunk IDs/borders/roads/rivers/saves/fog/authoritative clock unaffected. Negative coords, edge seam and unload/reload get same visual instances for same seed.
- No `renderer.material` clones per frame; no vendor redistributability violations; no silent download of packages or unity version changes.

## Performance truthfulness

Only publish hardware-specific **measured** numbers and include:
- Build target and render API, GPU/CPU/RAM, driver, screen resolution, quality/preset, VSync/FPS cap, shadow distance, active lights, AA, render scale.
- CPU main and render thread ms; GPU frame ms; **median and p95** over stable frames; p99/frame spikes when captured; draw calls/batches/SetPass, instanced counts, rendered tris, texture+mesh memory, GC allocations.
- Benchmarks for close normal far camera, midday and dense night settlement; stationary and rotating/panning; one local region and heavier 2/8/16 player finite map scenarios.
- Separate cold bootstrap, world ready, first-visit streaming spike, revisit after eviction, long pan. Do not materialize full 16-player map to fake good frame rate.
- Keep reference profiler screenshots and baseline data in a sensible repo docs/report (small textual metrics only); original commercial asset sources or too-large captures remain out of public repo.

## Decision thresholds — configure only after baseline

A tentative engineering gate is **no material CPU/GPU p95 regression at the same visual equivalence**, absent signed and quantified justification for a clear visual win. Agree specific acceptable percentages/time budget **after G0** and after establishing targeted supported PCs. Do not invent target FPS, triangle/vertex/VRAM budgets before hardware and real gameplay metrics are known.

Performance strategies must show marginal benefit: SRP Batcher vs GPU RD/instancing, AA options, SSAO on/off, shadow cascades/distances, transparency/grass density, APV on/off. Report visual side-effects.

## Functional regression gate

- Initial project open, Generate Preview and Play succeed.
- Player camera rotation/zoom and streaming unaffected; no weird materials near or far.
- Day-night cycle tied only to world-time authority (default 360-second day + 90-second night, not hardcoded independent timers in rendering).
- Lamps respond to night and obey realtime budget; fog-of-war still controls visibility.
- Terrain-road-cliff river/bridge align at chunk seams.
- All 2, 8, 16-player map profiles possible without materialization explosion; save/revisit yields consistent decoration.
- Unity build launches on target graphics APIs with no pink shaders, missing texture references, rendering feature exceptions.

## Reporting template

```
Branch/commit:
Unity Editor / URP package:
Scene/seed/camera/time/quality:
Hardware, API, resolution, VSync:
Before image/path:
After image/path:
Console errors/warnings:
Editor tests:
PlayMode tests:
Standalone player tested (yes/no):
CPU frame median/p95:
GPU frame median/p95:
Draw calls / SetPass:
Video memory estimates:
Camera + foliage ghosting: pass/fail/unknown:
Night lighting: pass/fail/unknown:
Streaming/save/fog-of-war: pass/fail/unknown:
Visual scorecard + owner feedback:
Regressions/open blockers:
Decision: continue / fix / revert / ship for review
```

Do not substitute CI green for visual Player approval.
