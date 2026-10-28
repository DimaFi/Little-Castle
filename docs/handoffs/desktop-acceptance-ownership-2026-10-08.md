# Desktop verification ownership

Base game c61a6de, art 0e23f67. Separate integration branches; unrelated PR #6 excluded.
Agents: gpt-6-sol / high; one Unity runner (root), no simultaneous Editor runs.

| Owner | Allowed edits | Contracts/dependencies | Checks |
|---|---|---|---|
| root | baseline tests/runtime collider refresh, Editor bridge importer/prefab/generated imports, new concept scene builder, concept-only profile, integration report/evidence, git operations | Main unchanged; fixed bridge geometry immutable; integrates reviewed PRs in dependency order | baseline then combined EditMode/PlayMode, real FBX import, visual capture |
| routing_review | after explicit reassignment only Macro routing validator/planner/BridgeSitePlanner and routing tests/report | keep public APIs/seed semantics and fixed geometry; no scene or streaming edits | collinear river overlap, yaw, legacy-site, bounded retries and real macro seeds |
| art_review | Tools/verify_portable_releases.py, test_portable_releases.py, art report; explicit reassignment permits bridge v001/v002 build reference paths and imported wall helper reference path only; new Source/Dependencies/Bridge_Stone_A | --json CLI preserved; no immutable Releases/catalog/geometry changes; source rebuild dependency may be copied with provenance | fixtures, payload/manifest SHA, clean clone, read-only Blender mesh/UV/packed textures |
| water_streaming | new Rendering/RiverWaterPresenter.cs, RiverWaterMeshBuilder.cs and meta; new Editor/RiverWaterPresentationTests.cs and meta; water report | root alone hooks WorldStreamer/StreamedChunkView; material null means off; read-only macro/fixed profile; scene follows prefab gate | world-space negative seams, fixed waterline, finite mesh, ownership/unload |

No foreign-file edits without root reassignment. No agent git mutations or Unity runs.
Graph: baseline → routing + reviewed art → prefab → scene/water → landscape → measured performance.
Independent read-only reviews and new pure water code may run concurrently; shared scene/import/config edits remain root-only.
