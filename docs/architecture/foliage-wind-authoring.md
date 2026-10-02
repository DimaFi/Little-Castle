# Foliage and Wind Authoring

This document defines how tree leaves, bushes, grass and wheat should be authored
so the Little Castle shader wind works predictably.

## Mandatory model compatibility gate

Before a new tree, bush, grass or wheat model is accepted into production,
verify that the model and shader contract work together.

A valid LC_Foliage shader cannot create convincing motion if the mesh does not
provide useful vertices, normals, UVs or masking data.

Minimum questions:
1. Is foliage separate from the rigid trunk?
2. If not, is Vertex Color R available for a rigid-to-flexible mask?
3. Is there enough vertex density for Crown Sway, Vertex Wave and Leaf Flutter?
4. Are normals consistent enough for normal-direction flutter?
5. Is local Y meaningful if automatic height masking is desired?
6. Do visible and shadow passes operate on the same topology?
7. Does close zoom expose deformation artifacts?
8. Does far zoom need reduced flutter or a different LOD?

Preferred fix order:
1. material/setup fix;
2. vertex-color, UV or normal authoring fix;
3. split leaves from trunk;
4. small shared-shader extension;
5. only then consider a larger shader rewrite.

## Core principle

Wind is vertex animation on the GPU.

No per-leaf MonoBehaviour, Animator, bone rig or physics object is required for
ordinary ambient foliage motion.

The geometry stays the same asset. The foliage shader offsets vertices each
frame from:

- global wind direction;
- global wind strength;
- global wind speed;
- object position;
- vertex position;
- optional vertex-color mask;
- optional automatic height mask.

This is conceptually similar to how shader packs can make otherwise static,
simple vegetation geometry feel alive: the visible mesh is still simple, but
its vertices are displaced in the vertex shader.

## Current foliage motion layers

`LC_Foliage.shader` has three simultaneous motion scales:

1. Crown sway
   - slow broad movement for the entire crown / leaf cluster;
2. Vertex wave
   - spatial wave across the crown so it does not translate as one rigid blob;
3. Leaf flutter
   - small faster movement along leaf/cluster normals.

The visible pass and shadow-caster pass use the same wind function so moving
foliage should cast matching animated shadows.

## Round / clustered Little Castle leaves

The current Little Castle tree direction uses rounded leaf pieces / clustered
leaf geometry rather than Minecraft-style cubes.

That geometry is suitable for shader wind.

The important requirement is that the leaf mesh has enough vertices for a
visible deformation. The shader cannot bend a region that contains no vertices
between its endpoints.

For leaf-only geometry:

- use `LC_Foliage`;
- default `Use Height Wind Mask = 0`;
- default `Use Vertex Color R Wind Mask = 0`;
- Crown Sway + Vertex Wave + Leaf Flutter should already create non-rigid
  movement because phase varies across world-space vertex positions.

For a mesh that combines trunk/branches/leaves:

- paint Vertex Color R;
- trunk/base vertices = 0;
- rigid branch bases = near 0;
- flexible branch tips = intermediate values;
- leaf vertices = 1;
- enable `Use Vertex Color R Wind Mask`.

This keeps the trunk fixed while leaves and flexible branch regions move.

## Automatic height mask

`Use Height Wind Mask` is optional.

When enabled:

- `Wind Anchor Height` is the local-space height that should stay fixed;
- `Wind Height Range` controls how quickly flexibility rises with height.

Use it for:

- bushes;
- saplings;
- simple plants;
- foliage meshes with a meaningful local Y base.

Do not enable it blindly for a leaf-only mesh whose pivot is centered inside
the crown. In that case it can freeze the wrong half of the crown.

## Tree authoring recommendation

Preferred production structure:

```
TreeRoot
├── TrunkAndBranches
│   └── solid material / LC_StylizedLit
└── Leaves
    └── foliage material / LC_Foliage
```

This is easy to tune, cheap and avoids moving the trunk.

A combined tree mesh is still allowed if vertex colors provide a reliable wind
mask.

## Grass and wheat

`LC_Grass.shader` uses UV Y as its bend mask:

- UV Y near 0 = root/base;
- UV Y near 1 = tip.

Therefore grass/wheat cards or clumps should be unwrapped so vertical UV Y
roughly matches physical bottom-to-top direction.

Current grass/wheat motion includes:

- base directional wave;
- slow spatial gust envelope;
- high-frequency tip flutter;
- stronger bend toward the tip;
- matching moving shadow caster.

Do not create one GameObject per blade.

Use repeated clumps/cards with GPU instancing.

## Wheat preset

Mature wheat currently has a shared material preset:

`Assets/_Game/Materials/Shared/LC_Wheat_Mature.mat`

It reuses `LC_Grass.shader` with:

- warmer root/mid/tip colors;
- stronger golden sun transmission;
- slightly calmer wind than ordinary grass.

Future wheat growth stages should prefer material/property variation and a small
number of mesh variants over creating an unrelated shader per stage.

## Performance rules

- wind is global shader work, not per-leaf CPU work;
- keep one shared material wherever possible;
- use GPU instancing;
- use MaterialPropertyBlock for instance tint/variation when needed;
- do not clone a material per tree at runtime;
- avoid extremely dense leaf meshes solely for wind;
- reduce foliage density / switch LOD before removing wind entirely;
- distance LOD may reduce or disable micro flutter before disabling crown sway.

## Review checklist

During Sol 5.6 / Sol 6 review, test:

- does the crown move internally rather than sliding as a rigid blob;
- does the trunk remain stable;
- do shadows match visible foliage motion;
- does round leaf geometry have enough vertices for subtle deformation;
- does close zoom show acceptable flutter without rubber-like stretching;
- does far zoom avoid shimmering;
- do gusts travel coherently across nearby vegetation;
- is wheat readable as a field rather than noisy individual cards;
- is wind cheap enough at target vegetation density.

Tune amplitude conservatively. The goal is living vegetation, not visibly
rubbery vegetation.
