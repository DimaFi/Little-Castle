# Copy-ready Codex handoff — Graphics 2.0

## Global instruction (paste into first Codex task)

You work in DimaFi/Little-Castle. Read AGENTS.md first and the **entire** docs/graphics-2/ plan incl. ADR-0004. The branch `graphics/urp-graphics-2-roadmap-2026-10-23` contains planning only; actual Unity migration has not occurred. The committed base is Unity 6000.5.5f1, Built-in, with custom CGPROGRAM/ForwardBase/ForwardAdd shaders. Keep main playable. Do not edit other agents' work or existing generation/network/world save logic. One small PR per subsystem; report exactly which files were touched and which tests were truly run. No guessed URP version, no false FPS claims, no copied copyrighted asset content.

### Task 0 — G0 acceptance: **preflight only**, no project writes

1. `git status`, branch, Unity editor and local package/asset audit. User's local work may differ from GitHub.
2. Open real `WorldGenerationTest`, Validate Stylized Rendering, selected production foliage validator; inspect real tree geometry before shader changes.
3. Establish repeatable fixed seed/time/camera golden screenshot matrix and standalone Player timings; report CPU/GPU, memory, light count, draw calls, shader errors.
4. Count custom shaders and material GUIDs and assess each Built-in pass; especially ForwardBase/ForwardAdd and animated ShadowCaster, generated terrain, local lamps and fog of war.
5. Deliver `docs/reports/graphics-2-baseline-<date>.md` with evidence links, no fake screenshots. STOP at G0 if missing local production models.

### Task 1 — experimental URP foundation (only AFTER G0)

- Create dedicated `graphics/urp-foundation` work branch from agreed base; record exactly how branch relates to current main (don't rebase active uncommitted work).
- Install Unity-supported URP from Package Manager; use Unity-created URP Pipeline and Universal Renderer assets with `.meta`, Graphics and each Quality override, no guessed YAML.
- Create fresh minimal comparison scene; render URP Lit; compare Forward and Forward+ at realistic night local-light counts. Validate current pipeline at runtime.
- Limit edits to URP config, own experimental scene, and report. No custom shader replacement/global converter yet. Make a draft PR with before/after evidence.

### Task 2 — custom shared shader porting (AFTER Task 1)

- Start with `LC_StylizedLit` proof; use URP HLSL lighting/shadow macros/passes, maintain `_LC_*` globals and all original texture property semantics.
- Add correct additional point/spot lights under existing night budget, shadow caster and depth/normal passes if required; match daytime/nighttime.
- Then **separate small PRs**: terrain; foliage/grass/wheat (real mesh preflight); sky; emissive pools; distant; water. Keep old shader files as migration reference until test pass.
- Upgrade material assets with dry-run report and preserve GUID/meta; no mass changes without approval.

### Task 3 — visual golden slice and profile

- Build original 40–60m cliff/road/forest/bridge/house vertical slice. Repeat 12/18/23h, near/normal/far views.
- Fix geometry, material alignment, UV/sRGB/roughness and palette first; shadows/contact then AO and post FX.
- Benchmark TAA vs MSAA vs SMAA for wind; profile GPU Resident Drawer vs other compatible batching (MaterialPropertyBlock caveat); write performance report with real hardware.
- Do not merge unless visually accepted with screenshot diffs and all automated/functional tests.

## Mandatory one-PR checklist

- [ ] Confirm file ownership and base branch; run `git status`.
- [ ] State scope, affected shaders/render passes, project version/package versions.
- [ ] No procedural data/seed/save/multiplayer/fog changes.
- [ ] Keep `_LC_*` globals and asset GUID references stable.
- [ ] Test real PlayMode and Player where possible; note unrun steps as **NOT RUN**.
- [ ] Before/after same camera/time/seed, frame debug, colored material/wind/night checks.
- [ ] Measured CPU/GPU p95/VRAM with hardware + raw profiling context, not guesses.
- [ ] No third-party proprietary pack binaries in public Git.
- [ ] Reversible migration, stop/revert conditions and visual review result.
