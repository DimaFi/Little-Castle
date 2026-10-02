# Night Lighting and Lighting LOD

Status: production foundation implemented; real asset hookup and visual tuning
remain to be done in Unity.

## Goal

Little Castle may contain many:

- lanterns;
- torches;
- windows;
- fireplaces;
- settlement lights.

They must look warm and alive at night without turning every visible glow into
an expensive realtime Point Light.

The rendering contract therefore uses several independent cost tiers.

## Lighting tiers

### Tier 0 — emissive appearance

Cheapest persistent visual representation.

Use:

- emission inside `Little Castle/Surface/LC Stylized Lit` for opaque windows,
  hot metal, lamp glass integrated into an opaque object;
- `Little Castle/Effects/LC Emissive Glow` for a separate flame/glow mesh.

This is the main reason an object *looks* lit.

It does not require a Unity Light.

Emission responds to:

- `_LC_NightAmount`;
- shared presentation time;
- cheap de-synchronized flicker.

### Tier 1 — cheap ground pool

Optional warm additive patch below/around a lantern.

Use:

`Little Castle/Effects/LC Night Light Pool`

with:

`NightLightPoolVisual`

The visual:

- uses a shared instancing-ready material;
- uses MaterialPropertyBlock for per-instance appearance;
- does not clone materials;
- has no shadow pass;
- has no physical-light calculations;
- can remain visible farther than a real Point Light;
- is distance-disabled by NightLightBudgetManager.

This is especially useful because terrain intentionally does not pay one
ForwardAdd pass for every lantern.

### Tier 2 — budgeted realtime Point/Spot light

Only nearby important lights receive actual realtime illumination.

Use:

`NightLightEmitter`

The global selector is:

`NightLightBudgetManager`

Current conservative test-scene policy:

- maximum 12 simultaneous realtime local lights;
- global camera distance 78 m;
- reevaluate selection every 0.18 s;
- already-selected lights receive small retention hysteresis;
- default repeated lights have realtime shadows disabled.

The emitter itself:

- fades in smoothly;
- fades out smoothly;
- has subtle two-wave realtime flicker;
- remains enabled during fade-out instead of popping;
- owns no per-emitter Update while dormant;
- is frame-ticked only while selected or transitioning.

## Why emission and Light are separate

Do NOT make visual glow depend on whether a realtime budget slot exists.

Bad:

```text
Point Light selected -> lamp looks on
Point Light loses budget -> lamp visibly switches off
```

Correct:

```text
all visible lanterns:
  emissive/glow remains visible

medium distance:
  + cheap pool if configured

near camera / highest priority:
  + realtime Point/Spot illumination
```

This allows a large night settlement to stay visually populated with light
while only a small local subset affects surrounding geometry.

## Recommended production prefab

Example lantern:

```text
LanternRoot
├── Body
│   └── LC_StylizedLit
├── Glow
│   └── LC_EmissiveGlow
├── GroundPool
│   ├── renderer using LC_NightLightPool
│   └── NightLightPoolVisual
└── RealtimeLight
    ├── Unity Point Light
    └── NightLightEmitter
```

A torch can use the same contract.

A window usually does NOT need a Point Light.

For many house windows prefer:

- emissive map/material;
- possibly one shared NightLightEmitter for the whole entrance/courtyard;
- not one Unity Light per window.

## Local-light authoring defaults

These are starting points, not immutable art values.

For repeated lanterns/torches:

- Point Light shadows: Off;
- render mode: Auto;
- physical range: compact, usually roughly 6-15 m depending on scale;
- NightLightEmitter camera MaxDistance: around 50-80 m;
- PoolMaxDistance: may be around 90-120 m;
- emission/glow may remain visible until its renderer LOD removes it.

Avoid huge overlapping light ranges.

Built-in ForwardAdd cost depends on how many lit renderers intersect each local
light. A small range is often more important than raw emitter count.

## Lighting LOD and mesh LOD are separate

Unity `LODGroup` switches Renderers.

It does **not** automatically make every child Unity Light cheap.

Therefore Little Castle uses two parallel systems:

### Geometry LOD

Unity LODGroup:

```text
LOD0
  full model

LOD1
  reduced model

LOD2
  aggressive model

farthest
  silhouette / LC Distant Simple
```

### Light LOD

NightLightBudgetManager + NightLightEmitter:

```text
far:
  no realtime Light

medium:
  emissive / optional pool

near:
  emissive + pool + budgeted realtime Light
```

Do not store current LOD in authoritative save/network state.

LOD is local client presentation.

## Suggested lamp/building LOD composition

### LOD0

- full mesh;
- body material;
- emissive/glow renderer;
- nearby budgeted realtime-light eligibility;
- shadows only where justified.

### LOD1

- reduced geometry;
- emissive remains readable;
- tiny decorative parts disappear;
- realtime light is still controlled by emitter camera distance.

### LOD2

- strong simplification;
- normally no need for standalone tiny glow geometry if emissive can be baked
  into the simplified material;
- no expensive probes/shadows.

### Farthest

- LC Distant Simple where visually acceptable;
- no realtime local light;
- no ground pool;
- no detailed glow mesh;
- no realtime shadows;
- no probes.

A distant village may preserve a warm color mass in an authored HLOD later,
but must not keep hundreds of individual local Lights.

## Smoothness

Realtime-light switching is deliberately not instantaneous.

NightLightEmitter has independent:

- fade-in time;
- fade-out time;
- subtle flicker.

NightLightBudgetManager adds selection retention so nearby lights do not chatter
between budget slots while the camera moves.

The shader glow has independent cheap flicker, so visual fire continues even
when the realtime-light tier is not active.

## Diagnostics

F8 WorldPerformanceOverlay now reports:

- registered night-light emitters;
- current realtime-light budget use;
- candidate count;
- fading light count;
- currently visible cheap pool visuals.

Use these values together with Unity Profiler before increasing light budgets.

## Validation

Run:

`Little Castle -> Rendering -> Validate Stylized Rendering`

and for every production lantern/building/model:

`Little Castle -> Assets -> Validate Selected Production Model`

The production validator warns about:

- unmanaged Point/Spot Lights;
- local realtime shadows;
- ForcePixel local lights;
- very large light ranges;
- excessive local-light count in one prefab;
- excessive NightLightEmitter distance;
- invalid NightLightPoolVisual material;
- normal LOD/shadow/probe/distant-material problems.

## Codex/Astra requirements

When integrating a real lamp/torch/building:

1. do not create a unique shader when the shared shaders can express it;
2. use shared materials;
3. do not instantiate materials at runtime;
4. set up authored LODGroup;
5. include Glow only in LODs where it remains useful;
6. configure one NightLightEmitter only when the object should actually light
   nearby geometry;
7. keep realtime shadows disabled for repeated lights;
8. run both rendering and production validators;
9. profile a real settlement before increasing the global light budget.

## What still requires real Unity validation

Static repository work cannot decide:

- final lamp color temperature/look;
- ideal physical Point Light range;
- whether 12 realtime lights is enough visually;
- exact LOD transition heights;
- glow/pool mesh size;
- Gamma vs Linear final appearance;
- how many ForwardAdd passes the final production settlement produces.

Tune those against actual assets and screenshots, preserving this cost-tier
architecture.
