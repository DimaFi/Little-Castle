# Chimney smoke ribbon — Codex handoff (2026-11-02)

## Intent and status
- Experimental **opt-in** visual smoke with a thin, wispy, continuous ribbon inspired by the *feeling* of Tiny Glade, not a copied shader or source implementation.
- Branch: `feature/chimney-smoke-ribbon-preview-20261102`; base commit `9ccdbcf4872750d20c33b9ea67897e109a33cf6c` from `codex/water-fireflies-integration-20261102`.
- **Not merged**; do not change main or work-in-progress water/firefly branches. Do not alter pre-existing scene assets, buildings, weather, shaders or world generation as part of initial test.
- No Unity editor/runtime/GPU validation has been performed in this environment. Edit Mode smoke-math tests are provided but must be run in Unity.

## Implemented architecture
- `ChimneySmokeProfile`: ScriptableObject visual settings, colour, opacity, dimensions, wind, seed variation, update/distance budgets.
- `ChimneySmokeMath`: deterministic Perlin displacement with birth-time sampling (avoids framewise jitter) and shared `_LC_Wind.xy/z/w` contract. No simulation state, no network messages.
- `ChimneySmokeEmitter`: manual chimney-outlet component; two intersecting soft ribbons; no emission unless configured and enabled in Play Mode.
- `ChimneySmokeManager`: one update loop and shared runtime material, distance culling and strip LOD.
- URP ribbon shader with transparency, edge feather and scene fog. No heavy full-screen effect, no lights or shadows.
- `ChimneySmokePreviewTools`: explicit editor commands for profile and smoke anchor, **never silently changes a scene**.

## Immediate Unity verification required
1. Open this branch in an isolated Unity 6000 project copy (avoid replacing locally unsaved private maps/art). Let Unity import assets. Check console for compile errors and missing shader.
2. Menu **Little Castle > Rendering > Chimney Smoke > Create Default Profile**.
3. Select a **test-only** roof/chimney object, choose **Add Preview At Selection**, move created `Chimney Outlet` to the pipe mouth (the default 2m offset is a helper, NOT automatic chimney detection).
4. Enter Play Mode with a camera tagged `MainCamera` and URP renderer. Inspect daytime, dusk, night, wind still/strong, and several seeds; no smoke should render in Edit Mode.
5. Run `Assets/_Game/Tests/Editor/ChimneySmokeMathTests.cs` in the Unity Test Runner. Check generated alpha, geometry, and motion with the real GPU. Test culling, disable/enable, scene unload and missing profile.
6. Profile with 10, 50, 100 and 200 emitters, near/far camera, Low/Medium/High quality. Track CPU Update/Render, mesh uploads, draw calls, transparent overdraw and garbage collection. **No FPS claim before measuring.**
7. Validate main camera switching, building destruction/rebuild, missing Shader.Find in player builds, and URP platform support.

## Important follow-up for Codex before final integration
- **Visual QA first:** compare against user-provided Tiny Glade video/screenshots for a *continuous, moving, curling thread* — not discrete puffs. The current mesh is a starting prototype. Tune segment widths, two-ribbon overlap, curl amplitude, tip fade, wind drift, opacity and distance. Halo is currently a crossed secondary ribbon, not volumetric light scattering. Inspect against sky and walls to catch harsh strip crossings and sorting.
- The current manager uses the global project quality index as a rough visual hint, not a true mapping of named quality tiers. Replace with the project's active quality settings/quality slider adapter **after inspecting** how the chosen gameplay scene exposes those settings. Do not mutate quality globals.
- `_LC_Daylight` and `_LC_Twilight` are optional presentation globals; verify their values are updated in the actual scene. Fall back to neutral smoke if absent.
- Current smoke follows an outlet's current position; moving chimneys will move the whole ribbon. For stationary cottages this is acceptable, but add emission-history buffering if moving smoke sources become necessary.
- Profile values use independent emitters with one manager and one shared material, but each emitter still owns a dynamic mesh. Measure and batch (or GPU-instance) only if meaningful after profiling.
- Once the visual is approved, add a separate **read-only adapter** that discovers **authored chimney anchors** on prefab buildings and registers emitters. For procedural `CottagePresenter` geometry, inspect actual roof/chimney plan output, then expose accurate outlet transforms from generation (or a dedicated geometry query) **without rewriting the cottage generator, runtime state, or existing world brushes**. Never put a smoke outlet on a random roof point.
- Provide explicit per-building enable, daytime schedule, furnace state (only if gameplay API actually exists), outdoor wind and quality controls; no new network traffic for cosmetic-only smoke.
- Add safe opt-out/global toggle; ensure disabling removes smoke without changing the game state. Never automatically attach to every map/asset before approval.
- Smoke material/shader stripping: if Shader.Find fails in player builds, add an explicit material asset/preload path **after** import validation.
- Respect current Codex water/firefly WIP. Rebase only after reviewing conflicts and preserving all user-local content; do not reset branches or amend unrelated commits.

## Acceptance criteria
- Thin continuous airy thread with softly feathered edges, no visible square puffs or hard polygons.
- Unique smooth seeded motion on simultaneous chimneys; wind has coherent direction.
- Correct attachment at a real chimney mouth. No ghost emitters when buildings are incomplete.
- Good silhouette from strategic camera angles and typical zoom range.
- No compile errors, project regression, material leaks, unexpected renderer changes or recurring managed allocations.
- All visual and performance verification results documented in a new QA note. Integration requires user's explicit visual approval.

## Suggested next Codex instruction
`Read docs/handoffs/chimney-smoke-codex-20261102.md on feature/chimney-smoke-ribbon-preview-20261102. Preserve all active water/firefly/local map work. In an isolated Unity test copy, compile the smoke module and inspect a single chimney, tune smoke against the approved visual target, run tests and performance measurements. Do not auto-integrate or merge until I review visuals; report video/screenshots and any defects with file/line references.`
