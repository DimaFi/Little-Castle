# Cozy Rain & Storm — Little Castle (isolated opt-in implementation)
Date: 2026-11-02. Related: [#89](https://github.com/DimaFi/Little-Castle/issues/89).

## Status / safety
**Code committed; Unity compile, Player, Game rendering, GPU timing, and visual comparison: NOT RUN.**
This is an **opt-in working-code candidate**, **not a verified production-ready rain system**.
No authored scenes, material assets, time system, authoritative map data, water shaders, snow shaders, or saves are modified. **Three existing shared shader sources were extended additively:** LC_Terrain.shader, LC_StylizedLit.shader and LC_FoliageURP.hlsl. Test their existing snow/lighting/LOD in Unity.
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
- `RainStormController.cs`: explicit MonoBehaviour opt-in, bounded Shuriken rain layers, sparse contact/splash sampling, pooled ripple overlays, roof-drip pool, atmospheric globals, optional lens overlay, procedural lightning, ambient rain loop and thunder; Game debug controls.
- `MapWeatherWaterQuery.cs`: read-only waterline sampling from existing authored MapRiverPath (partial MapEditSession).
- `RainRoofDripSource.cs`: optional component to place on roof eaves; pooled by the controller, no per-drop object creation.
- `Assets/_Game/Shaders/Surface/LC_WeatherWet.hlsl`: neutral-by-default material wetness and cheap terrain shallow-pool sheen.
- `LC_Terrain.shader`, `LC_StylizedLit.shader`, `LC_FoliageURP.hlsl`: scoped shader additions to darken wet ground/solid/foliage and adjust roughness; preserve existing authored masks.
- `Assets/_Game/Resources/RainStorm/LC_RainLens.shader`: optional screen-space droplets behind higher-order HUD overlays.
- `Assets/_Game/Resources/RainStorm/LC_RainStreak.shader`: URP transparent softened rain billboards.
- `Assets/_Game/Resources/RainStorm/LC_RainRipple.shader`: URP irregular pool overlay with animated repeating impact rings.
- `Assets/_Game/Editor/RainStormPreviewMenu.cs`: menu and in-Play inspector controls.
- `Assets/_Game/Tests/Editor/RainStormMathTests.cs`: focused logical NUnit checks.
- Corresponding `.meta` files with stable GUIDs are committed.

## Run in Unity 6000.5.5f1 / URP 17.5.0
1. Fetch this feature branch into a clean worktree **without replacing the user's working tree**. Confirm all three new shaders, shared HLSL include, C#/Editor/tests import and compile.
2. Open an existing **Game** test scene with the current terrain, sky, water and camera. Keep the existing saved map.
3. Menu: `Little Castle > Weather > Create Cozy Rain Preview in Current Scene`. The action creates only one ordinary, Undo-able controller GameObject; it does **not** save the scene or instantiate replacement terrain.
4. In its Inspector optionally create `RainStormProfile` via `Assets > Create > Little Castle > Weather > Cozy Rain and Storm`, assign it. Without an asset, the controller creates a default temporary ScriptableObject in Play.
5. Assign gameplay camera if it is not tagged MainCamera; optionally assign existing `DayNightLightingController` and `MeadowSurfacePainter`. Set `groundLayers`, `waterLayers`, and optionally dedicated `roofLayers` to identify shelters. Roof layer 0 disables shelter checks. Add `RainRoofDripSource` on selected eave markers; no automatic random drips from every building.
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
- Global `_LC_WeatherWetness` is now smoothly driven from RainStormController, read by additive shader code on existing terrain, solid objects and foliage. The terrain gets a cheap irregular shallow-pool sheen; solid material roughness becomes subtly smoother; snow-covered portions mask this rain response. With controller Off and dry, the global returns to zero and old looks are preserved. Existing wetness near rivers/waterfalls continues to work.
- Puddle meshes are still cheap small alpha ripple overlays; **no actual water volume or screen-space reflections** are added. The existing river waterline is sampled read-only via MapRiverPath. Shader-only wetness affects all outdoor geometry equally unless an exposure mask is later implemented.
- Fallback `MeadowSurfacePainter.Session.SampleHeight` is used only when Physics has no surface hit. Authored river strips are sampled first via `TrySampleWeatherRiverWater`. A dedicated waterfall-zone exclusion and shoreline visuals still need Game validation. The fallback checks terrain slope and snow but cannot infer arbitrary non-river water. No promise of correct depth sorting on all surfaces.
- `DayNightLightingController.SetWeatherPresentation(cloudiness, precipitation)` receives smoothed amounts. Full sky/fog attenuation requires an existing complete AtmosphereProfile; otherwise the controller's legacy lighting remains authoritative. No second sky/sun/moon/time system is created.
- A lightning bolt is one deterministic polyline plus a smaller branch, with one temporary shadowless Point Light. Thunder uses **only user-supplied licensed audio clips**; without clips it is silent. Optional 2D `rainAmbience` also accepts a licensed audio loop and fades volume according to intensity. Flash reduction is supported.
- No routine use of `FindObjectsByType`, per-drop GameObjects, compute or VFX Graph, Runtime `UnityEditor` imports, modified save schema, PlayerPrefs or editor auto-start.
- Debug `OnGUI` is for visual QA only; remove or replace with proper Game settings UI before shipping.

## Current, explicit gaps (do not claim completed)
1. **Unity compilation, shader visuals, Player build, EditMode tests and CPU/GPU measurements are unverified.** First run all of them and fix failures.
2. **Basic wet material response is implemented** through `_LC_WeatherWetness` in shared URP terrain/solid/foliage shaders (not material assets). This is not physically accurate reflection or water-filled road depression geometry: carefully tune colors/roughness/artist controls after the first Game captures. The weather wetness is globally uniform and lacks authored roof-cover/exposure masks; interior surfaces can still darken.
3. Roof-edge runoff is implemented as opt-in eave markers with one shared particle pool, but **markers must be placed**. Basic near-camera shelter culling uses `roofLayers`; a full world rain shadow map is NOT implemented. Foliage drips, accurate indoor audio, complex depth occlusion and refractive lens distortion remain undone. `allowLensRain` now enables **non-refractive** sparse UI-background drops at High/Custom only.
4. Surface overlays require appropriate terrain/water colliders and manually configured physics layers; authored rivers now have explicit waterline sampling. Fallback can sample slope/snow, but does not classify arbitrary ponds or waterfalls. Roof shelter is bounded Physics sampling, not a world rain shadow map. Verify actual river bank/bridge placement and avoid physics per particle.
5. Distant curtains currently use one larger Shuriken emitter, not real volumetric cloud-to-ground rain bands or fixed-world rainfall; their quality and fill-rate must be judged visually. No GPU instancing fallback yet.
6. Lightning is a simple branched procedural line (no true volumetric HDR bolt/shader), currently 1 strike and 1 branch; storms need aesthetic tuning, local/distant probability and measured rate. No custom light reflections on water.
7. No authoritative weather or persistence/MP sync; presentation is client-only and controlled manually in Game. Add deterministic state transport and biome rain/snow priority in a separate integration stage.
8. A baked rain ambience audio loop is not bundled, and none of the reference third-party code/assets have been copied into this branch; source links below are research only.

## Technical acceptance checklist for Codex
- [ ] Run static C# / EditMode / PlayMode, shader compile, IL2CPP/Player build. Record outputs, failing cases and fixes.
- [ ] Validate camera movement/teleport and no visible emitter rectangle, hard opacity wall, or dropped particle spikes.
- [ ] Fix roof, bridge and forest occlusion, water/terrain classification, puddle stability across chunk edges.
- [x] Add neutral-by-default weather wetness to existing terrain, stylized solids and foliage without replacing authored map/UV/snow contracts. **Visual regressions are still untested; perform fade/drying Game QA.**
- [x] Added authored river-waterline query and ripple overlay routing. **Real water rendering, bank limits and waterfall exclusions are unverified.**
- [x] Implemented opt-in roof eave markers with pooled runoff and optional sparse ScreenSpaceOverlay droplets behind HUD for High/Custom quality. **Full automatic marker placement and refractive glass are not included.**
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
| roofLayers | 0 (disabled) | Unity LayerMask | limited Physics queries |
| allowLensRain / lensStrength | false / .15 | bool / 0–1 | fullscreen translucent UI pass |
| rainAmbience / rainVolume | null / .35 | audio clip / 0–1 | audio playback |

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
