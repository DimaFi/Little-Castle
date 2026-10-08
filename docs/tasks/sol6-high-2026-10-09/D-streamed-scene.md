# [Sol 6 High][P1] Build actual finite streamed concept scene and verify crossings in PlayMode

Executor gpt-6-sol/high, manual selection. Base66e9a41 PR#20 plus accepted A/B/C/E commits. Follow-up #10/#11/#12. This task is SEQUENTIAL after routing/river/presentation contracts; do not edit the local static fixture.

## Exclusive ownership
Assets/_Game/Scripts/World/Streaming/WorldStreamer.cs (presentation hook only); new Editor/ConceptStreamedSceneBuilder.cs, Scenes/ConceptStreamedWorldTest.unity, Settings/World/ConceptWorld_v002/ and Tests/PlayMode/ConceptStreamedWorldTests.cs (+meta); new docs/reports/concept-streamed-acceptance.md/evidence folder. No existing ConceptBridgeSceneBuilder/ConceptBridgeTest/BridgeValidation edits, Main*, source releases, pipeline/shader or river/routing code.
Contract: serialize optional materials and use C's immutable presentation API; current null-water legacy behavior unchanged. Clone a separate concept profile, don't overwrite v001. Public authoritative API changes need reassignment.

Use actual finite session + generated macro river/roads/bridges, existing real v002 prefab and RiverWaterPresenter. No authored synthetic crossing as procedural proof, no full-map meshes/GameObjects. Built-in, one runtime water owner per playable chunk; no terrain-only padding gameplay visuals/colliders. Approved building/tree integration remains a separate explicit asset approval, not permission to replace all placeholders.

Minimum: actual accepted seeds + one explicit invalid seed; negative chunks/yaw/borders; road socket grades/waterY; far LOD/collider budget; move beyond unload+return with delta persistence/no mesh/renderer duplicates. Day/night/overview/gameplay/close/far GPU screenshots and clear visual FAIL where concepts remain unmet. Foliage preflight before vegetation changes; keep flags right-hand caps and faction tint.
Capture bootstrap/macro/start/prewarm/MATCH READY separate from exploration; allowed 2/8/16-player/map configs, actual counts/cache/peak memory, CPU p50/p95/p99 and GPU only in graphical Player. Do not treat nographics or sampled frame delta as GPU/CPU profiler. If cloud tools unavailable, implement editor/test tooling and leave real desktop acceptance pending. Follow existing umbrella performance task rather than duplicate it. Report API/files/commits, XML/screenshots/metrics and exact blockers.
