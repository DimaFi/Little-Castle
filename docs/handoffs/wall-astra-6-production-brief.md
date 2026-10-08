# Astra 6 production brief — modular stone wall A

Latest: **v005**, 2026-10-06. Preserves manual edits, scales wall kit by
1.546676, real long-module length 3.093352 m. Gate interior +X, hinge X=0.60 m.
Use v005 README/Connections and `../reports/fortifications-v005-update-2026-10-06.md`.
Existing Unity art fixtures were updated and validated. Older sections below
are historical; do not rebuild user edits from the legacy procedural generator.

## Current source: optimized fortifications v004, 2026-10-03

Use `E:/Games_Develop/Little-Castle_Assets/Source/Architecture/Wall_Stone_Modular/v004/`.
11 FBX with three LODs include the wall kit, archer tower, arched gatehouse,
independent hinged leaves and customizable pointed banner with separate trim.
Read its README, Connections.json, material-bindings.json and QA reports first.
Strictly internal triangles were removed; all surviving coordinates/UVs match
v003. Eight same-camera renders were compared. The approved v002 stone texture
is unchanged. v003 remains the reversible baseline. No Unity runtime acceptance
or FPS claim is implied; socket docking, banner compositor and wind need runtime
integration. Sections below preserve the original technical contract/history.

## Authored implementation, 2026-10-02

The actual six-model source kit now lives in
`E:/Games_Develop/Little-Castle_Assets/Source/Architecture/Wall_Stone_Modular/v002/`.
See its `README_RU.md` and `QA/export_validation.json` for measured results.
The user rejected v001 as flat and overly textured. v002 uses actual bevelled
stone blocks with geometric joints. The original user-selected
`E:/Games_Develop/CozySettlement/Mat/StoneWall_A_Unity` maps remain unchanged
as reference inputs. Current material uses a new single grout-free shared
albedo `T_WallStoneSurface_A_BaseColor.png`; do not layer the old masonry
normal/AO over the new blocks. See v002 `Textures/material-bindings.json`.
This is a visual-review candidate, not an approved release. No Unity import
or Play Mode acceptance has been performed.

Source-art repository rules determine storage: `.blend` stays in `Source/`,
reviewed export copies go to `Releases/`, then Unity may consume a copy under
`Assets/_Game/Art/Imported/Wall_Stone_Modular/<accepted-version>/`. UCX objects are reference
collision proxies; Unity prefab assembly must explicitly create BoxColliders.
They are not automatically recognized as colliders by the current project.

## Purpose and authority

This brief is the ready-to-use asset contract for the first production wall
set. It is derived from the current Little Castle wall implementation and the
user-provided concept sheet. It supplements, and does not replace,
`docs/architecture/curved-modular-walls.md` and
`docs/handoffs/wall-asset-integration.md`.

The wall is a set of rigid, reusable meshes. The game places and rotates them
along a path; no mesh is ever bent along a spline.

## Non-negotiable Unity coordinate contract

Unity units are metres. Export FBX with **Y up** and applied transforms.

| Property | Wall modules | Pillar / end cap |
| --- | --- | --- |
| Local length direction | +Z | n/a (symmetric footprint) |
| Local width direction | X | X |
| Vertical direction | +Y | +Y |
| Pivot | `(0, 0, 0)`: centred in X/Z, at ground/base | `(0, 0, 0)`: centre of base, at ground |
| Root transform after import | position 0,0,0; rotation 0,0,0; scale 1,1,1 | same |

This local **+Z length axis** is intentional. It takes precedence over the
generic wording "left/right" in the visual specification. A wall point
rotated to yaw 0 extends in world +Z.

## Deliverable set

Create one coherent `A` set, with no trees, soil, grass, baked heraldry,
damage variants, or baked lighting. The only permitted separate decoration is
the neutral, player-customizable banner set defined in
`player-banner-asset-contract-ru.md`; it is never merged into stone meshes.

| Asset name | Exact nominal bounds (X x Y x Z, m) | Role |
| --- | --- | --- |
| `SM_Wall_Stone_2m_A` | `0.60 x 1.40 x 2.00` | Primary runtime segment |
| `SM_Wall_Stone_1m_A` | `0.60 x 1.40 x 1.00` | Prepared residual/short segment; not yet selected by the current runtime |
| `SM_Wall_Stone_Pillar_A` | `0.60–0.70 x 1.60 x 0.60–0.70` | Start and automatically repeated structural wall tower/base |
| `SM_Wall_Stone_End_A` | `0.60 x 1.40 x 0.60` maximum footprint | Open-end cap, facing local +Z |
| `SM_Wall_Banner_Cloth_A` | `0.36 x 0.55 x 0.03` maximum | Separate neutral player-banner cloth, not a wall module |
| `SM_Wall_Banner_Bracket_A` | small, separate | Optional neutral reusable banner mount |

Every mesh has `LOD0`, `LOD1`, and `LOD2`. Preserve the outer bounds and
pivot across LODs. `LOD2` keeps only the readable wall/cap silhouette; it may
remove individual stone relief and should not cast or receive realtime
shadows after Unity integration.

## Connection design

The actual connection centreline is the local Z centreline: `X = 0`, `Y = 0`.

For a `2m` segment, the ideal logical endpoints are `(0, 0, -1.00)` and
`(0, 0, +1.00)`. For a `1m` segment, they are `(0, 0, -0.50)` and
`(0, 0, +0.50)`. They do not need exported socket objects; the mesh must make
those locations visually unambiguous.

Keep the final **0.10 m at each Z end** deliberately quiet:

- no interlocking keys, exposed corner stones, large merlons, or caps;
- flat/near-flat vertical end face for a clean straight join;
- it may enter a pillar, gate, or future tower by up to `0.08 m` without a
  visible clash;
- all high-relief stone variation must begin outside that quiet zone.

The current 2m definition uses `segmentLength = 2.00` and a `0.96` spacing
multiplier, so requested spacing is `1.92 m`. Actual spacing in the current
implementation is `pathLength / ceil(pathLength / 1.92)` and can be smaller.
The `0.08 m` overlap is the initial seam-test target, not a guaranteed maximum
overlap for every runtime path. The 1m module follows the same dimension and
endpoint conventions when its future selector is added.

The pillar is a universal node, not a 90-degree corner mesh. Its four sides
must be visually compatible with a wall entering at arbitrary yaw. Keep its
body/cap clear enough that a wall end can disappear slightly into it. Do not
make a permanent doorway, a fixed-angle notch, or a banner recess for this
asset.

The end cap has one join face at local `-Z`; the finished/sloped architectural
termination faces local `+Z`. It is only for an open endpoint and must not be
used as a general connection solution.

## Visual language from the supplied concept sheet

- Cozy, hand-made stylized medieval masonry; large readable blocks, softened
  edges, restrained irregularity, and no photoreal micro-detail.
- Wall body is a low defensive parapet, not a massive late-medieval fortress.
- Build depth into silhouette-facing stones, top courses, corners and the
  pillar cap; keep broad wall faces economical.
- Use simple, chunky crenellations. The rhythm must survive joins: the 2m
  module uses three readable merlons and the 1m module uses two, with the end
  quiet zones preventing an accidental merged merlon at joins.
- Pillar is visibly taller (`1.60 m`) with a simple projecting cap.
- The 1m piece is the same architectural kit at half length, not a scaled 2m
  mesh; thickness, stone scale, UV density, cap height and crenellation style
  remain consistent.

## Materials, UVs, and geometry

Use a single material slot named `M_Wall_Stone_Shared` on all visual LODs.
UV0 must use consistent texel density and accommodate a repeatable stone
material; do not bake per-mesh colour, ambient occlusion, moss, decals or a
unique texture atlas.

The repository currently has the shared stylized lighting material
`LC_StylizedLit_Default`, but no verified production stone material is present
under `Assets/_Game/Materials`. Therefore Astra must preserve the single-slot
material contract and not invent a final project material; the production
stone material will be assigned during Unity import.

Use geometry where it changes silhouette or readable block relief. Use normal
maps/material response for tiny surface roughness. Remove hidden internal
faces. Do not create a collider mesh from individual stones.

Provide a cheap box-like collision proxy for each asset:

- `UCX_SM_Wall_Stone_2m_A` approximately `0.60 x 1.40 x 2.00`;
- `UCX_SM_Wall_Stone_1m_A` approximately `0.60 x 1.40 x 1.00`;
- `UCX_SM_Wall_Stone_Pillar_A` approximately its footprint x `1.60`;
- `UCX_SM_Wall_Stone_End_A` approximately its visible volume.

## Future defensive structures

Do not embed sockets in the generic wall meshes. Unity will later add two
child connection objects to each tower/gate/bastion:

```text
WallConnection_A / WallConnection_B
position: exact wall-centreline docking position
local +Z: preferred direction the attached wall leaves the structure
```

Each socket reserves a small clearance region in code; ordinary wall sections
inside it are omitted. Consequently all wall ends must stay universal and
must not assume whether their neighbour is a segment, pillar, archer tower,
gatehouse, or bastion.

## Export package

```text
Wall_Stone_Modular/
├── Source/Wall_Stone_Modular.blend
├── Meshes/
│   ├── SM_Wall_Stone_2m_A.fbx
│   ├── SM_Wall_Stone_1m_A.fbx
│   ├── SM_Wall_Stone_Pillar_A.fbx
│   ├── SM_Wall_Stone_End_A.fbx
│   ├── SM_Wall_Banner_Cloth_A.fbx
│   └── SM_Wall_Banner_Bracket_A.fbx
└── Preview/Wall_Modular_Preview.png
```

The `.blend` must retain editable named source objects and the collision
proxies. FBX files must contain the LOD meshes and no scene lighting/cameras.
The banner cloth additionally follows `player-banner-asset-contract-ru.md`:
full-rectangle UV0, no baked colour/emblem, and separately exported locators
on the 2m wall and pillar.

## Astra self-check before delivery

1. Measure final FBX bounds at scale 1: the values above must hold to ±0.01m.
2. Confirm all four pivots and the wall +Z axis with a local-axis display.
3. Assemble: pillar → 2m → 2m → 2m → pillar, at `1.92m` centre spacing.
4. Assemble: 2m → 2m → 1m → end cap, with end-cap join at the final wall end.
5. Test a smooth chain with yaw increments 0°, 5°, 10°, 15°, 20°, 25°; the
   quiet end zones must avoid conspicuous wedges or collisions.
6. Test a 90° visual junction with a pillar at the corner. Do not create a
   dedicated 90° module.
7. Verify LOD switches retain bounds, material slot, pivot, and silhouette.

## Unity integration note (not an Astra task)

The current runtime instantiates only `segmentPrefab`, so `2m` is the first
live segment and its measured length must remain 2.00 in
`WallPlacementDefinition`. `1m` and the end cap are nevertheless required
deliverables: their dimensions and connection convention are now fixed, ready
for the future residual-length/endpoint selector. No wall algorithm rewrite is
required for the asset work.
