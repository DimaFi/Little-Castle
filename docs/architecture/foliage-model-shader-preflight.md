# Foliage Model / Shader Compatibility Preflight

This is the mandatory first compatibility check for the current Little Castle
tree/foliage pipeline.

It exists because the foliage wind is only useful if the production mesh is
authored in a way the shader can deform correctly.

## Current shader behavior

Primary shader:

`Assets/_Game/Shaders/Foliage/LC_Foliage.shader`

Ambient wind is performed entirely in the vertex shader on the GPU.

There is no requirement for:

- one GameObject per leaf;
- bones;
- Animator;
- per-leaf MonoBehaviour;
- rigidbody/physics animation.

The current shader stacks three motion scales:

### 1. Crown Sway

A slower object-scale motion.

Purpose:

- the crown has a broad response to wind;
- nearby trees do not remain perfectly static;
- object-position phase variation prevents every tree moving identically.

### 2. Vertex Wave

A spatial phase varies across vertex world position.

Purpose:

- different areas of one crown move at slightly different times;
- the foliage does not behave as one rigid translated ball;
- simple clustered/rounded foliage can gain internal motion even if it is one
  combined Leaves mesh.

### 3. Leaf Flutter

Small faster displacement along the leaf/cluster normal.

Purpose:

- close camera gets subtle fine movement;
- rounded leaf clusters/cards feel less static;
- the effect can be reduced before crown sway at distant LODs.

The same wind function is used by the visible foliage pass and ShadowCaster
pass so the intended result is animated leaves with matching animated shadows.

## Is the current rounded-leaf visual style compatible?

Potentially yes.

A tree that visually consists of many rounded leaf pieces/clusters can work
well with this shader even when those pieces are merged into a single foliage
mesh.

What matters is not whether each leaf is a separate GameObject.

What matters is the mesh data:

- there must be vertices to move;
- vertex positions must vary across the crown;
- normals must be usable;
- UV0 must be usable;
- alpha/opacity must be usable where cutout is required;
- the trunk must not receive the same unrestricted leaf deformation.

A screenshot or render cannot prove those conditions. Inspect the actual Unity
Mesh/FBX.

## Preferred production hierarchy

```
TreeRoot
├── TrunkAndBranches
│   └── LC_StylizedLit
└── Leaves
    └── LC_Foliage
```

This is the preferred setup because:

- the trunk remains rigid;
- leaves can use aggressive enough wind without bending bark;
- materials/shaders remain reusable;
- LODs are easier to tune;
- the foliage mesh can use its own opacity/normal settings.

## Combined trunk + leaves mesh

Allowed, but it needs a reliable deformation mask.

Preferred contract: Vertex Color R.

Suggested values:

| Region | Vertex Color R |
|---|---:|
| trunk/base | 0.0 |
| rigid large branch base | 0.0-0.15 |
| flexible branch | 0.2-0.7 |
| branch tip | 0.7-0.9 |
| leaf geometry | 1.0 |

Enable:

`Use Vertex Color R Wind Mask`

Do not assume a combined mesh is safe just because it visually looks like a
tree.

## Automatic height mask

Optional fallback for vegetation with a meaningful local-Y base.

Useful for:

- bushes;
- saplings;
- simple plants.

Risk:

A leaf-only crown whose pivot is inside the crown may freeze the wrong region.

Therefore height masking is not the default for leaf-only production trees.

## Required mesh data

### Vertices

Required.

The shader only moves vertices. A visually curved shape with too few vertices
may remain visibly rigid or distort as a whole patch.

Do not add extreme geometry density only for wind; test the minimum topology
that looks good at the game's closest intended camera distance.

### Normals

Required for the intended result.

They are used for:

- foliage lighting;
- leaf-normal flutter direction;
- backlighting/transmission.

Missing/bad normals can produce incorrect flutter and lighting.

### UV0

Required.

Used for:

- BaseColor;
- alpha/Opacity;
- Normal/AO textures.

### Tangents

Required when the foliage material uses its Normal map.

Without valid tangents, tangent-space normal mapping should not be considered
production-ready.

### Vertex Color R

Optional for leaf-only meshes.

Required/recommended when trunk/branches/leaves share one deforming mesh and
need different movement weights.

## Opacity

`LC_Foliage` supports both:

- alpha in BaseColor;
- a separate Opacity map.

When a separate opacity texture is used:

- assign it to `_OpacityMap`;
- set `Separate Opacity Strength = 1`.

## Mandatory Unity check

Select the actual production tree root, leaf GameObject, or Mesh asset.

Run:

`Little Castle -> Rendering -> Foliage -> Validate Selected Model`

The validator checks the mechanical prerequisites it can inspect:

- Mesh exists;
- vertex count;
- normals;
- tangents;
- UV0;
- vertex colors;
- bounds/local Y range;
- material/shader assignment;
- whether LC_Foliage is assigned;
- whether a usable Vertex Color R range exists.

This is a technical preflight, not a visual approval.

## Mandatory Play Mode check

After technical validation, test the real asset in the game camera.

Check at close zoom:

- crown has internal motion;
- leaves do not stretch like rubber;
- trunk stays stable;
- leaf-normal flutter is subtle;
- alpha edges are stable;
- shadows follow leaves.

Check at far zoom:

- no obvious shimmer;
- micro flutter does not turn into noise;
- the crown still has slow readable movement;
- tree-to-tree motion is not perfectly synchronized.

Check at day / sunset / night:

- daylight remains readable;
- sunset backlight becomes warm/golden;
- night foliage is cool but visible;
- moving shadow behavior remains acceptable.

## Decision rules

### PASS — keep current shader/model architecture

Use when:

- foliage is separate or correctly masked;
- vertices/normals/UV/tangents are valid;
- close/far wind looks good;
- shadow motion matches;
- cost is acceptable.

### FIX MODEL — preferred when architecture is sound

Examples:

- separate Leaves from Trunk;
- paint Vertex Color R mask;
- improve leaf vertex distribution;
- fix normals/tangents;
- correct UV/opacity setup.

Prefer this over rewriting the whole foliage renderer when the defect is asset
authoring.

### FIX SHADER

Use when multiple correctly authored production foliage assets exhibit the same
shader defect.

Examples:

- flutter phase produces shimmer;
- deformation creates unstable shadow aliasing;
- LOD transition needs distance-based wind reduction.

### REVISIT ARCHITECTURE

Only when real production tests show the shared model/shader contract cannot
meet visual/performance targets.

Document that decision before replacing the current system.

## Codex / Sol first action

Before proposing foliage improvements, Codex/Sol must report:

- which real tree/foliage asset was inspected;
- whether leaves are separate or combined;
- mesh vertex count;
- normals present/missing;
- tangents present/missing;
- UV0 present/missing;
- Vertex Color R present/range;
- shader/material used;
- technical validator result;
- close/far Play Mode result if Unity execution is available;
- recommendation: PASS / FIX MODEL / FIX SHADER / REVISIT ARCHITECTURE.

Do not skip this step and do not infer mesh compatibility from screenshots.
