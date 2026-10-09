# CliffKit v001: Q08 integration handoff

Art: `DimaFi/Little-Castle_Assets`, branch `codex/cliff-kit-v001`.
Unity intake: branch `codex/cliff-kit-intake-v001`; tested code/prefab commit
`5c069b85d7fd67ebb2ff33b4cbfa6c3ffe06ed3c` over concept base `e6a02df`.
Source geometry commit `7dcd0945b507d4b78e620d5afd8a0cfedf2915c3`.
Immutable release commit `7d71b6c`; package SHA256
`6caf39663fb6c7c2956b620add0fdf20afde30b95900013b5850eb78782fcff0`.

This provides real 14 prefabs / 42 FBX, shared materials, authored LODGroup,
sockets, a separate catalog, and a terrain contact validator. Q08 remains open;
no competing `ScarpDecorationStage`, height stamp, WorldStreamer edit or global
catalog/scene edit was introduced. All temporary Python/Blender recipes stay
in the art repository. Only the useful contact API belongs to runtime.

## Resolve and place

Use `Assets/_Game/Art/Imported/CliffKit/v001/CliffKitCatalog.asset` as the reviewed
source mapping. IDs are `cliffkit_` plus lowercase archetype names, for example
`cliffkit_cliff_straight_a`, `cliffkit_rock_outcrop_large`, `cliffkit_ramp_hike_a`.
Copy selected entries into the owning concept catalog only in root integration.
Raw FBX Unity orientation differs: the tested prefab composes 180° yaw with the
original imported axis rotation. Do not replace that composition with identity,
or bypass the prefab by loading raw FBX. Root scale is one; Y-up; +Z uphill.

Q08 generates plain stable placement records, not Unity objects. Choose IDs from
worldSeed/generationVersion/world-space feature key and an explicit archetype
salt using the project's stable deterministic hash, never string.GetHashCode,
UnityEngine.Random, visit order, object GUID, or a per-chunk local counter.
One feature belongs to one owner chunk (floor coordinates for negative space).
Neighboring data may be sampled, but never spawn the same feature in both chunks.
Eviction destroys presentation; revisit must reproduce the same IDs and respect
runtime removal state. Decoration must not create stone/metal resource data.

Sample the **final** terrain after river/road/bridge adjustments. Check the full
world-space footprint against road/river/bridge sites, entrances, strategic exits,
starts, building reservations and deposits. `CliffTerrainContactValidator`
requires a ground callback that returns false if adjacent data is unavailable,
and a reservation callback. Wait or reject; do not clamp a sample to the current
chunk or treat decorative mesh colliders as terrain.

`ValidateStraightCliff` supports full-height straight/terrace v001 modules only.
It reserves a 2m front skirt envelope plus the whole shelf and checks lower/upper
support. Curved and end profiles require their exact authored contact curve;
this helper cannot approve them. Crest variation up to approximately 0.8m and
slight internal shelves still need contact blending and visual review on actual
generated hills. The heightfield cannot supply a walkable overhang.

`ValidateRamp` checks a 0.5m or finer sample grid along the clear corridor, both
longitudinal and transverse grades on every cell including the final corner,
and all shoulder reservations. Nominal width is 7/9m; a 0.35m margin each side
covers actual bowed shoulders (measured bounds 7.582/9.582m). Clear central
corridor is 3.5/4.5m; rise 6.5/9m; length 24/32m. Agent grade limits are explicit
caller parameters. Tests use proposed infantry30/cart12 only as an example,
without changing game rules. Acceptance is **contact only**, never NavMesh proof:
root must check continuous real walk surface, entry/exit, obstacle clearance,
agent capability and animation. Different cliff/ramp heights are not a snap match.

## Presentation budget

Cliffs 924/272/170 triangles; rocks 192/96/64; ramps 576/192/108.
One mesh per tier: two submeshes rock/cap, one on bare rocks. Three near materials
and three shared distant variants. LOD2 uses existing LC Distant Simple, shadows
and probe cost off. No per-object collider, alpha grass, realtime lights or wind
on cap geometry. Model/texture ReadWrite off; instancing enabled on shared
materials. This is eligibility, not proof of batched draw calls or Player speed.
LOD screen fractions .5/.18/.025; root must review culling of horizon silhouettes.

## Explicit outstanding gates

- Q04/Q08 dependency/ownership acceptance and actual final-height placement.
- Valid cliff + adjacent ramp on a real generated hill; invalid footprints rejected.
- Procedural terrain/cap palette and seams, including near/far/day/night views.
- Real infantry/cart navigation/physics, streaming eviction/revisit duplicate IDs.
- Actual Player CPU/GPU/draw calls/shadow/memory/spikes on one hardware baseline.

Four bounded source-terrain audits prove negative-height seams and repeated
generation only. They omit macro plans/masks and do not certify gameplay contacts.
All selected naive fixed-height footprints were correctly refused. Seeds12345
and777 had no scarp mask in the sampled concept_slice; never invent a scarp to
make a screenshot. The separate review scene is an asset fixture, not the world.
