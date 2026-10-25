# Meadow lighting integration checkpoint — 25–26 October 2026

This is a reviewable integration packet from the separately validated
Little-Castle_LightingIntegration_20261025 workspace. It does not activate a
rendering migration in the repository checkout. MAIN has an uncommitted local
URP baseline and extensive unrelated work; this commit deliberately preserves
the lighting delta without publishing/staging that other work.

`payload/` stores complete reference versions of the changed lighting sources,
the owned fixture, profiles, shaders and tests at their intended Unity paths.
Use `main-to-integration.patch` and the before/after SHA-256 manifests for a
function-by-function merge. Do not mass-copy payload over the newer map editor:
six runtime/authoring source files have already evolved since the Meadow snapshot.
Source matrices/meshes/textures and third-party binary assets are local-only;
restore their exact GUID dependencies from the licensed source and manifests.
This packet is not a standalone fresh-clone Unity project.

Validation: 178/178 Edit Mode, 6/6 Play Mode, 152 nonempty real Player images,
eight warmed standalone runs of 600 ordinary camera-render intervals each.
Raw measurement distributions and snapshot provenance are in this packet.
Screenshots/Players/full XML remain at the local Research path in the handoff.
No spatial GI is claimed. The Tiny Glade research note links public primary
materials and open reference code by the developers for the next prototype.

Start with [the handoff](../../handoffs/lighting-to-map-authoring-20261025-ru.md),
[Tiny Glade references](tiny-glade-source-research-20261026-ru.md), and
[cache transaction boundary](local-cache-invalidation-ru.md).
