# [Sol 6 High][P1] Feed real road/wet/rock surface masks to terrain presentation

Executor gpt-6-sol/high, manual selection. Base integration/desktop-acceptance-2026-10-08@66e9a41, PR#20. Follow-up #11. Read AGENTS, material-texture-contract, stylized-rendering-roadmap, world-generation architecture.

Problem: fixture manually paints straight road/river masks; runtime concept terrain still lacks verified world-space presentation masks from its actual macro plan. Implement genuine data-driven road/bank/rock blending, not screenshot-only stripes.

## Exclusive ownership
New Assets/_Game/Scripts/World/Rendering/TerrainPresentationMaskUtility.cs and Tests/Editor/TerrainPresentationMaskTests.cs (+meta); optional additive overload in Rendering/ChunkMeshBuilder.cs; docs/reports/terrain-presentation-masks.md. No scene/profile/WorldStreamer/shader/generation-stage edits. TerrainSurfaceStage and LC_Terrain.shader are read-only source contracts.
API: additive presentation-only method taking immutable chunk/plan/world coordinates and writing mesh vertex colors under existing LC Terrain channel semantics. Existing Build overload unchanged. Agree exact API in report for task D's hook. Never rewrite authoritative terrain classes/surface IDs/heights.

Derive path/wetness from actual polylines/widths, rock from appropriate available data; coherent absolute world-space sampling including negative chunks, rotated bridge stamps and shared borders. Road socket approaches remain visible and water core protected. No per-vertex full-map repeated scan: bound/pre-filter segments. Preserve instancing/shared materials and coarse far readability.
Tests: mask0..1/finite, no fake road/river without data, nonstraight paths, overlap priority, negative seams and yaw, legacy Build output unchanged, generated real concept chunk diagnostics/allocation/timing. No hand-authored plane asserted as final world. Cloud noUnity: provide tests and runnable diagnostic, flag visual acceptance pending. Parallel A/B/E; D alone hooks streamer/material/scene after C. Foreign writes need reassignment.
