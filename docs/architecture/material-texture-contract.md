# Material Texture Contract

This document maps the project's authored texture naming to Little Castle
shared shader inputs.

## Solid materials - LC_StylizedLit

Use for:

- wood;
- stone;
- plaster;
- roof tiles;
- bricks;
- barrels;
- carts;
- mill/building parts;
- other opaque props.

Texture mapping:

| Authored suffix | Shader property | Notes |
|---|---|---|
| BaseColor | _MainTex | sRGB color texture |
| Normal | _BumpMap | import as Normal Map |
| AO | _OcclusionMap | grayscale |
| Roughness | _RoughnessMap | grayscale; set Roughness Map Strength to 1 |
| Height | not used yet | deliberately deferred; do not add parallax blindly |
| Emission | _EmissionMap | optional windows/fire/etc |

Current scalar fallback:

- Base Roughness = 0.68;
- Roughness Map Strength = 0 by default;
- AO Strength = 0.75.

Height maps remain valid source assets even when not sampled by the current
runtime shader. Close-camera parallax/displacement should be evaluated later
against cost, silhouette artifacts and strategy-camera readability.

## Packed ORM

Some assets may contain packed ORM maps.

Current shared shaders do not yet make packed ORM the default runtime path.

Do not delete packed ORM source files.

Sol review should compare:

1. separate AO + Roughness;
2. packed texture path;
3. sampler/bandwidth savings;
4. authoring complexity.

Only standardize packed ORM after a real profiling/material migration pass.

## Foliage - LC_Foliage

Texture mapping:

| Authored suffix | Shader property |
|---|---|
| BaseColor | _MainTex |
| Normal | _BumpMap |
| AO | _OcclusionMap |
| Opacity | _OpacityMap (optional) |

Opacity behavior:

- default Separate Opacity Strength = 0;
- when BaseColor already has useful alpha, leave it at 0;
- when a separate Opacity map exists, assign it and set
  Separate Opacity Strength = 1.

This supports existing Little Castle foliage texture exports without forcing
repacking.

## Grass / wheat - LC_Grass

Texture mapping:

| Authored suffix | Shader property |
|---|---|
| BaseColor / color-alpha | _MainTex |
| Opacity | _OpacityMap (optional) |

Grass/wheat intentionally uses a cheaper lighting path than solid objects.

Mature wheat preset:

`Assets/_Game/Materials/Shared/LC_Wheat_Mature.mat`

## Terrain - LC_Terrain

Current terrain shader inputs:

- Grass / Ground BaseColor;
- Dirt / Path BaseColor;
- Rock BaseColor;
- world-space tiling;
- slope blend;
- macro variation;
- optional vertex path/wetness masks.

The terrain shader does not yet consume the complete Normal/AO/Roughness/Height
set. That is a deliberate current limitation, not a reason to delete those
source textures.

Before expanding terrain sampling, profile texture bandwidth and decide whether
rock slopes need triplanar detail.

## Import rules

- BaseColor / color maps: sRGB ON.
- Normal: texture type Normal Map.
- AO / Roughness / Height / Opacity: sRGB OFF.
- keep alpha when required by foliage;
- use mipmaps for world textures;
- use appropriate texture compression;
- do not mark textures Read/Write unless runtime CPU access is actually needed.

## Material duplication

Prefer:

- one shared material per reusable visual material;
- MaterialPropertyBlock for small per-instance tint/variation;
- GPU instancing where compatible.

Avoid:

- renderer.material in Update;
- one unique material per tree;
- one unique shader per building part.

## Review requirement

Sol 5.6 Medium should verify these import assumptions against actual texture
assets before Sol 6 performs any automated material migration.
