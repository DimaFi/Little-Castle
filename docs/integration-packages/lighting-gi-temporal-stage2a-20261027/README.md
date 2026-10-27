# GI Stage2A — temporal presentation and strategy-camera evidence

Experimental delta over published `f4ae7b19e70bcd9a53f8ce7eea84d97ef195d542`.
Root game Assets, Packages and ProjectSettings remain unchanged. Previous packets
and licensed Meadow/Oak inputs are prerequisites; no new third-party assets copied.

- [Report, measurements and limits](../../reports/lighting-gi-temporal-stage2a-20261027-ru.md)
- [Current plan](../../plans/lighting-next-stages-20261026-ru.md)
- [ADR](../../decisions/ADR-lighting-temporal-presentation-stage2a-20261027-ru.md)
- [Map handoff](../../handoffs/lighting-to-map-authoring-20261025-ru.md)
- [Source/GUID hashes](stage2a-manifest.json), [audit](stage2a-audit.json), [patch](gi-f4ae7b1-to-stage2a.patch)
- [Local raw evidence and both Player hashes](evidence-index.json)

Two coherent published snapshots fade over0.2s; computation remains separate.
Geometry edits forbid old radiance in both histories. Known-invalid does not use
sky fallback. Continuous WorldTime drives presentation:360s day+90s night, no
primary geometry rebuild. 1x/8x cycles, rapid undo/remote move and exact restoration
pass. 210 Edit+6Play tests;12fields match Stage1C and Immediate exactly, serial
error <=5.0664e-7. This trades global publication latency/memory for coherence;
dark pauses after edits remain an explicit limitation. Not production adoption.

Three strategy-camera pairs use the unchanged real rig, FOV50,11..190m, native
grass LOD and Oak LODGroups, day/sunset/night.180measurements,108000 ordinary
frames. Optional ground-relative fog is a runtime A/B only. Long-range shadows
and fog/palette matter more to this top-down game than roof-interior refinement.

![Actual Player camera views](evidence/game/strategy-camera-day-night.png)
![Continuous clock diagnostic](evidence/core/stage2a-continuous.png)

Apply payload by function in the isolated integration copy, preserving GUIDs.
Do not replace divergent map-authoring shaders or auto-activate main settings.
Build core with SpatialIndirectStage1Build.BuildLinear and -giStage2A; build the
separate camera Player with -giGameViewsBuild. Unity6000.5.5f1, Linear Release,
frame timings enabled temporarily; settings are restored by the builder.
run_stage2a.ps1 generates core evidence without overwriting existing output.
run_game_views.ps1 defaults to Verified prefix; use Final for the first captured
pair, Verified FirstRound2 Repeats2 for the recorded later pairs. The summarizers
expect the canonical folders listed in the report. Prior Stage1C serial/reference
local evidence is needed for numerical comparison. Large bin/PNG/frame JSON and
Player binaries are indexed locally, not bundled. Preserve them before rebuilding.

No actual terrain/height-mesh transport, canopy/local emitters, moving population,
streaming or map save/load adapter. Fixed49x9x49 field and sparse test map remain.
Next: selective current terrain/object snapshot, ready-model visual zone while
retaining cube/walls/roof, strategic near/mid/far shadows/fog/AO and joint clock/
camera/edit checks. Map editor development remains independent.
