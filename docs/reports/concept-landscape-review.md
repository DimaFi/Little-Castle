# Concept landscape measurement and gameplay visual review

Date: 2026-10-09
Executor: GPT-6 in ChatGPT; issue https://github.com/DimaFi/Little-Castle/issues/11
Branch: gpt6/landscape-measurement-preflight from current-unity-fix at c61a6de.
Status: **EDITOR PREVIEW EXPORT / SOURCE TESTS PREPARED, NOT EXECUTED IN UNITY**.

## Existing foundation (do not rewrite)

The current concept has an already opt-in StylizedLandformSettings and stateless world-space StylizedLandformSampler. Concept LayeredTerrainStage.useStylizedLandforms is enabled only in isolated ConceptWorld_v001 asset. The existing six StylizedLandformTests passed in the historic 2026-10-08 batch; this is NOT evidence of the new branch passing.

The shape roles are broad meadows, rounded hills, selected terraced/scarp highlands and suppressed local noise on plains. Heightfield cannot create overhangs, caves or detached rocky silhouettes. Existing authored rock assets / rendering scene are required for final landscape. Do not add a finished player village or artificially equalize resources.

## What this PR adds

Editor menu: **Little Castle → World → Concept Landscape → Export Seed Preview Maps**.

The new Assets/_Game/Editor/ConceptLandscapePreview.cs:
- Requires the existing *isolated* concept stage to be stylized enabled. Never writes a scene, runtime settings, materials or Main*.
- Samples 192 × 192 positions across a 3,072 × 3,072 m world-space square centered on (0,0). Hence half its coordinates are negative, with deterministic sampling independent of chunk iteration.
- Runs seeds **12345**, **54321**, **-10101**.
- Writes three `PNG` diagnostic previews per seed: a height palette, RGB masks (red=scarp, green=plain, blue=highland) and slope/buildability zones (green <=8°, yellow 8..18°, red >=18°).
- Writes corresponding per-seed `JSON` metrics: height min/max/mean, buildable sample share (slope <=8°), largest 4-neighbor connected buildable region, plain/highland/scarp/steep region sample shares, max sampled slope. Graph is SAMPLE GRID only, not navigation mesh, ownership or player start acceptance.
- Default destination, locally only: **Logs/GPT6-ConceptLandscape/**, 12 generated artifacts (9 PNG + 3 JSON), no binaries committed by GPT-6. This is a diagnostic preview, NOT a rendered Unity scene.
- Does not generate detailed terrain chunks, river carving, roads, resources, grass, shaders or runtime trees.

New Assets/_Game/Tests/Editor/ConceptLandscapeDiagnosticsTests.cs:
- Tests determinism, finite heights, seed variation and finite masks on 2,048m, 4,096m and 6,144m sampling spans (analogous to different concept sizes, **NOT actual game player sessions**).
- Tests a negative chunk seam at x=-32 m with 64 intervals of 0.5 m.
- Tests are committed with stable .meta GUIDs, **NOT RUN** in Unity.

## Exact desktop work (Codex)

1. Merge/review prerequisite #7 test baseline; after #10 water scene ownership transfer, create the real concept gameplay scene with approved actual prefab/materials. Do not modify the shared scene in parallel with bridge/water work.
2. In Unity 6000.5.5f1 Editor or batch:
   - menu: Little Castle / World / Concept Landscape / Export Seed Preview Maps
   - batch: `-executeMethod LittleCastle.Editor.ConceptLandscapePreview.ExportSeedPreviewMaps` (provide actual editor executable/project path)
3. Inspect `Logs/GPT6-ConceptLandscape/` generated image/JSON files. Track actual percentages, preview crop examples and invalid edge cases rather than quote default constants as measurements.
4. Run full EditMode suite, plus new ConceptLandscapeDiagnosticsTests and original StylizedLandformTests. Report passed/failed/skipped counts, no false PASS.
5. Preview real generated terrain **after river carving, fixed bridge stamping, surface classification, object scatter and water rendering**, using normal RTS camera height plus far/near perspectives, day/night. Compare to soft Tiny Glade-like readable silhouette direction and actual imported house/tree models. Preview PNG maps are NOT replacements for visual screenshots.
6. Fix measurable problems (buildable area fragmentation, all-map high frequency chatter, terrace crowding, repetitive slopes, masks and surface placement), keeping the legacy-off sampler exact and seed semantics stable. Need actual metrics and before/after images before altering settings.
7. If rock overhang/outcrop meshes absent, create a separate accepted art backlog and source them through proper prefab/LOD/material workflows; cannot be faked by simply exaggerating a heightfield.
8. Include screenshot paths, actual seed & real map size, camera yaw/pitch, grass/rock materials, river bank/bridge approaches, GPU profiles, and sample-vs-real mismatch in `docs/reports/concept-landscape-review.md`. Retest negative seams; do not invent an image-based PASS.

## Known limitations

- The pure sampler preview excludes rivers, stamped bridge sites, stream unloading and gameplay masks. A large connected low-slope region is NOT automatically a connected playable road graph.
- Metrics are sampled on a 192² grid; narrow or smaller-than-sample features can be missed. Multi-scale source tests do not validate full multiplayer 2/8/16.
- No final landscape shaders, rock models, camera changes, scene screenshots or GPU timings were authored in this PR.
- MainWorld and renderer unchanged; existing ConceptWorld_v001 stage and standalone sampler unchanged.
- Real PNG files and metric values have not been generated here; **do not claim specific result percentages** until executing Unity.

This PR advances issue #11 with reproducible diagnostic instrumentation. It does NOT close the visual quality acceptance or replace the downstream Codex work order in issue #19.
