# Cozy Rain & Storm — Little Castle (isolated opt-in implementation)
Date: 2026-11-02. Related: [#89](https://github.com/DimaFi/Little-Castle/issues/89).

## Status / safety
**Code committed; Unity compile, Player, Game rendering, GPU timing, and visual comparison: NOT RUN.**
This is an **opt-in working-code candidate**, **not a verified production-ready rain system**.
No authored scenes, terrain materials, time system, map data, shaders shared with water/snow, or saves are modified.
Don't merge automatically. Do not replace user's local authored map, uncommitted waterfall seam changes, PR #88 smoke, or PR #87 water/firefly content.

**Base:** `codex/water-fireflies-integration-20261102` SHA `14420d9d98d47fa15a779212113e9a56a12678f8`.
This branch is stacked on PR #87 rather than `main` (which may lag behind).
Before taking over, Codex must fetch again, check newer HEAD and AGENTS.md; integrate without overwriting local work.

## Artistic target
The user approved a cinematic 3D painterly cozy medieval rainy-village reference:
- soft slate-blue cloud cover and forest haze, warm amber lanterns/windows;
- close, thin translucent wind-leaning rain streaks + distant gray rain curtains;
- irregular dark wet stone and paths, shallow puddles, organic expanding rings;
- subtle roof-eave droplets, leaf drips, wet foliage and stone, splash impacts;
- low-angle puddle close-ups, a wide storm/river/castle view, sheltered porch with water dripping outside.
**Priority:** light and atmosphere > wet material response > puddles/ripples > rain streaks > contact details > thunder.
Avoid opaque white rain wall, black night, repetition, mandatory giant full-screen post effect, or photorealism inconsistent with Tiny-Glade-inspired Little Castle.

## Files
- `Assets/_Game/Scripts/World/Weather/RainStormMath.cs`: deterministic hashing, world grid, smooth intensity and wet/dry transitions.
- `RainStormProfile.cs`: configurable ScriptableObject with safe default values, precipitation presets, custom budgets, art and lightning controls.
- `RainStormController.cs`: explicit MonoBehaviour opt-in, bounded Shuriken rain layers, sparse contact/splash sampling, pooled ripple overlays, procedural lightning and optional thunder; Game debug controls.
- `Assets/_Game/Resources/RainStorm/LC_RainStreak.shader`: URP transparent softened rain billboards.
- `Assets/_Game/Resources/RainStorm/LC_RainRipple.shader`: URP irregular pool overlay with animated repeating impact rings.
- `Assets/_Game/Editor/RainStormPreviewMenu.cs`: menu and in-Play inspector controls.
- `Assets/_Game/Tests/Editor/RainStormMathTests.cs`: focused logical NUnit checks.
- Corresponding `.meta` files with stable GUIDs are committed.

## Run in Unity 6000.5.5f1 / URP 17.5.0
1. Fetch this feature branch into a clean worktree **without replacing the user's working tree**. Ensure both shaders import and C#/Editor/tests compile.
2. Open an existing **Game** test scene with the current terrain, sky, water and camera. Keep the existing saved map.
3. Menu: `Little Castle > Weather > Create Cozy Rain Preview in Current Scene`. The action creates only one ordinary, Undo-able controller GameObject; it does **not** save the scene or instantiate replacement terrain.
4. In its Inspector optionally create `RainStormProfile` via `Assets > Create > Little Castle > Weather > Cozy Rain and Storm`, assign it. Without an asset, the controller creates a default temporary ScriptableObject in Play.
5. Assign gameplay camera if it is not tagged MainCamera; optionally assign existing `DayNightLightingController` and `MeadowSurfacePainter`. Carefully select `groundLayers` and `waterLayers`: classify water separately; do **not** count roof/bridge colliders as ground.
6. Enter Play: choose Off / Drizzle / Rain / Downpour / Storm in the debug panel or Inspector; choose Low / Medium / High. The panel can be hidden in Inspector; debug UI is only for testing.
7. Test scene camera zoom 11–190, rotation, map pan, teleports, night/twilight/day, bridges, cottages, river shore, waterfall, foliage, snow, fireflies and smoke in isolation. Turn it Off and disable the component; ensure resources and atmosphere return to baseline.
8. Capture Video and screenshots in Unity under actual lighting. **No final image or screenshot claiming to be in-engine evidence is supplied here**.

If shaders fail to load from Resources, the component disables itself with an Error log rather than creating broken white particles.
If no valid camera exists, its rendering stays inactive; do not use a SceneView or reflection camera for this opt-in Game test.

## Implementation details / behavior
- `RainMode` presets: Off=0, Drizzle=.18, Rain=.48, Downpour=.82, Storm=1.
- `RainQuality`: Off/Low/Medium/High/Custom. Defaults High until measured; change to Medium if fill-rate heavy.
- Rain and surface visuals use one owned hidden runtime root and shared temporary material instances. Two world-space Particle Systems follow the main gameplay focus; no per-drop physics.
- Wind takes `_LC_Wind.xy/z` from the existing shader global; **this system does not change wind settings**.
- Splash pulses use bounded raycast samples, and water/ground puddles share one pooled quad mesh and one ripple material with per-instance property blocks. Hashing is fixed by world coordinates and seed, rather than UnityEngine.Random.
- The ground overlay is not actual pooled water volume / SSR. It is a deliberately cheap alpha ripple mesh; the wetness factor is rendered on the mesh only. Nothing edits permanent map data.
- Fallback `MeadowSurfacePainter.Session.SampleHeight` is used only when Physics has no surface hit. **It cannot classify river / waterfall regions**, so turn `useMapHeightFallback` off where terrain overlays cross rivers. No promise that every water shader supports depth sorting.
- `DayNightLightingController.SetWeatherPresentation(cloudiness, precipitation)` receives smoothed amounts. Full sky/fog attenuation requires an existing complete AtmosphereProfile; otherwise the controller's legacy lighting remains authoritative. No second sky/sun/moon/time system is created.
- A lightning bolt is one deterministic polyline plus a smaller branch, with one temporary shadowless Point Light. Thunder uses **only user-supplied licensed audio clips**; without clips it is silent. Flash reduction is supported.
- No routine use of `FindObjectsByType`, per-drop GameObjects, compute or VFX Graph, Runtime `UnityEditor` imports, modified save schema, PlayerPrefs or editor auto-start.
- Debug `OnGUI` is for visual QA only; remove or replace with proper Game settings UI before shipping.

## Current, explicit gaps (do not claim completed)
1. **Unity compilation, shader visuals, Player build, EditMode tests and CPU/GPU measurements are unverified.** First run all of them and fix failures.
2. **Wet material response is only a screen-visible puddle overlay.** The ground, stone, wood, roofs, tree foliage and road PBR/stylized shader properties are NOT yet made globally wet. Integrate an additive `_LC_Wetness` contract into authored materials **only after** reviewing existing terrain/foliage/water shader interfaces. Avoid wholesale replacement.
3. **Roof-edge run-off, dripping foliage, rain occlusion under roofs, indoor acoustic damping, and near-camera lens droplets are not implemented.** `allowLensRain` is an explicitly reserved profile option, not a hidden effect. Add small local pooled emitters and a dedicated screen-space camera pass respecting UI. Never enable a cosmetic setting that has not been implemented.
4. Surface overlays require appropriate terrain/water colliders and **manually configured physics layers**. The map fallback knows terrain height only, not water exclusion or slope; verify at authored rivers and sloped roads. Add a spatial exposure / roof cache; avoid physics per particle.
5. Distant curtains currently use one larger Shuriken emitter, not real volumetric cloud-to-ground rain bands or fixed-world rainfall; their quality and fill-rate must be judged visually. No GPU instancing fallback yet.
6. Lightning is a simple branched procedural line (no true volumetric HDR bolt/shader), currently 1 strike and 1 branch; storms need aesthetic tuning, local/distant probability and measured rate. No custom light reflections on water.
7. No authoritative weather or persistence/MP sync; presentation is client-only and controlled manually in Game. Add deterministic state transport and biome rain/snow priority in a separate integration stage.
8. A baked rain ambience audio loop is not bundled, and none of the reference third-party code/assets have been copied into this branch; source links below are research only.

## Technical acceptance checklist for Codex
- [ ] Run static C# / EditMode / PlayMode, shader compile, IL2CPP/Player build. Record outputs, failing cases and fixes.
- [ ] Validate camera movement/teleport and no visible emitter rectangle, hard opacity wall, or dropped particle spikes.
- [ ] Fix roof, bridge and forest occlusion, water/terrain classification, puddle stability across chunk edges.
- [ ] Add optional wetness to Little Castle ground, roads, stones, walls, roofs, foliage without changing base authored maps/UV channels/biome snow; add fade/drying visual test.
- [ ] Integrate river/lake ripple shader at actual water surface with correct waterline, no ripples on banks/waterfalls.
- [ ] Add roof-drips and a subtle, UI-excluding lens rain pass at High only; accessibility toggle.
- [ ] Day, dusk, night, underwater/river/bridge, porch, long play, pause, camera zoom, snow/fireflies and water integration test.
- [ ] Compare Off, Low, Medium, High: CPU ms, GPU ms, draw calls, overdraw, GC/frame, particle caps, VRAM, scene + resolution + hardware. Do not invent FPS or GPU values.
- [ ] Keep user map and PR #88 separate; PR remains draft until user visually approves.

## Parameters (defaults / range / cost)
| Field | Default | Range | Main cost |
|---|---:|---:|---|
| rainBlendSeconds | 8s | >0.1 | negligible |
| wettingSeconds / dryingSeconds | 40 / 160s | >0.1 | negligible |
| nearRadius / distantRadius | 22 / 65m | 8–60 / 20–180 | overdraw |
| spawnHeight / fallSpeed | 30m / 22m/s | 12–80 / 4–35 | particle footprint |
| streakWidth / streakLength | .035m / .65 | .01–.15 / .1–2 | overdraw |
| windInfluence | .55 | 0–3 | negligible |
| puddleRadius / rippleSpeed | 1.25m / .65 | .3–4 / .05–2 | translucent pixels |
| wetReflection / splashIntensity | .36 / .65 | 0–.8 / 0–1 | alpha/particles |
| cloudiness / precipitation | .95 / .9 | 0–1 | existing atmosphere |
| lightningInterval | 16–42s | >=4 / >=5 | short local light |
| lightningFlash / thunderVolume | .5 / .55 | 0–1 | lights/audio |
| customBudget | 1100/1700/10/22/12 | capped at 4000/6000/24/64/40 | fillrate/queries |

**Save model:** Profile is an explicit Unity asset if the user creates it. Temporary fallback ScriptableObject is not saved in an authored map; no map persistence has been implemented. For official world/editor global and local zones, follow existing saved data schema and Undo/Redo practices, do not silently serialize Unity object pointers.

## Sources (research only, no copied third-party source)
- Unity Shader Graph Samples: https://docs.unity.cn/Packages/com.unity.shadergraph%4017.6/manual/Shader-Graph-Sample-Production-Ready.html (read as guidance and verify installation compatibility).
- Unity Particle collision docs: https://docs.unity.com/en-us/engine/6000.5/manual/particle-collisions (avoid per-drop raycasts).
- Daniel Ilett Rain URP: https://github.com/daniel-ilett/rain-urp (MIT; older URP; study puddle masks).
- Cyanilux rain VFX: https://www.cyanilux.com/tutorials/rain-effects-breakdown/ (article; study flipbook rings and drops).
- RayznGames Dyna-Wet: https://github.com/RayznGames/Dyna-Wet (check repository licensing before reuse; currently no source imported).
- Soner Eraslan lightning: https://github.com/sonereraslan/unity-lightning-system (MIT; study procedural branch ideas).
- nullsoftware UnityLightning: https://github.com/nullsoftware/UnityLightning (experimental, potentially expensive; no code imported).

**Original new code in this branch**; links are documentation/research only. Do not assume the previously discussed reference image is in the repository; ask user to attach the approved concept frame to Codex's Unity visual review if needed.
