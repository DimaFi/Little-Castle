# Curved Modular Wall System

## Decision

Little Castle walls are gameplay paths assembled from repeated rigid modules.

The wall mesh itself is NOT bent or procedurally deformed.

A confirmed wall stores one compact authoritative record:

```text
WallRuntimeState
- wallId
- ownerPlayerId
- definitionId
- closedLoop
- controlPoints[]
```

Derived section transforms are NOT save/network state.

Every client reproduces them with:

`WallPathLayoutUtility`

This makes a long wall cheap to save and replicate.

## Runtime flow

```text
player build tool
      ↓
WallPlacementController
      ↓
control points + moving cursor
      ↓
Catmull-Rom centerline
      ↓
fixed-distance resampling
      ↓
rigid WallSectionPose list
      ↓
terrain projection / validation
      ↓
server confirms WallRuntimeState
      ↓
WallRuntimeRegistry
      ↓
clients rebuild identical visual modules locally
```

## Why the wall stays modular

Do not create one giant deformed wall mesh.

Reasons:

- one reusable authored module;
- predictable collider/LOD/material setup;
- easy section identity;
- future damage/repair can address sectionId;
- curved walls work without mesh stretching;
- save/network payload stays small;
- asset production remains independent from placement code.

## Model contract for Astra / Blender / Codex

The required production asset is ONE straight wall module.

### Axis

The module length axis is local **+Z**.

Example:

```text
      +Z
       ↑
       │
 ┌───────────┐
 │   WALL    │
 └───────────┘
       │
      pivot
```

### Pivot

Preferred pivot:

- X = center of wall width;
- Z = center of module length;
- Y = at or very near the bottom/base.

The code places the module by its center and rotates it around Y.

### Transform

Prefab root should normally be:

- Position = 0,0,0
- Rotation = 0,0,0
- Scale = 1,1,1

Apply transforms in Blender before export where appropriate.

### Length

The real authored module length must match:

`WallPlacementDefinition.segmentLength`

Do not visually guess this value.

Measure the final prefab/model.

### Curve

The module remains rigid.

Curvature comes from many short modules being rotated along the sampled path.

Do NOT:

- bake a curved version into the base module;
- add bones for wall bending;
- add a spline component to each module;
- dynamically stretch every module to arbitrary length.

A slight configured overlap is allowed to hide tiny curve seams.

### Materials

Use shared Little Castle materials.

Do not create one material instance per wall section.

### Collider

Use a simple collider.

Do not use a high-poly visual mesh as MeshCollider unless specifically proven
necessary.

### LOD

Production wall modules may contain authored LODs.

Far LOD should aggressively reduce:

- geometry;
- shadow cost;
- probe cost.

The wall placement system must not depend on one specific LOD implementation.

## Curve behavior

With 2 points the path is straight.

With 3+ points it uses a smooth Catmull-Rom centerline, then resamples it by
fixed module spacing.

This means control points describe the *shape*, not individual wall pieces.

Sharp turns are rejected when neighboring module yaw exceeds:

`WallPlacementDefinition.maximumTurnDegrees`

If a future wall needs true 90-degree corners, prefer an authored corner module
or explicit corner placement rather than allowing severe module overlap.

## Terrain

`WallPathPresenter` can project each derived module onto nearby ground.

It checks:

- ground exists;
- ground slope;
- neighboring height step;
- curve turn angle;
- path self-intersection.

Gameplay/server placement validation should eventually use authoritative
generated terrain sampling rather than trusting client Physics.Raycast.

The presenter raycast exists for local preview/presentation.

## Closed city walls

`WallRuntimeState.closedLoop = true`

supports a closed perimeter.

Closed walls require at least three control points.

Future gates should be represented as explicit path features/openings, not by
creating a second unrelated wall system.

## Section identity

Every derived module receives deterministic:

`sectionId = Hash(wallId, sectionIndex)`

through `WallPathLayoutUtility.CreateSectionId`.

This prepares future:

- wall HP;
- repair;
- targeting;
- local damage visualization.

Do not network section transforms just to obtain section identity.

## Server ownership

`WorldStateAuthority` owns a `WallRuntimeRegistry`.

A placed wall should be submitted to server authority as one
`WallRuntimeState`.

Clients receive wall path state and derive visuals locally.

## Current placement API

`WallPlacementController` intentionally does not own global mouse bindings.

Expected build-tool integration:

```csharp
BeginPlacement(...)
AddControlPoint(worldPoint)
SetCursorPoint(worldPoint)
RemoveLastControlPoint()
TryConfirm(out wall)
Cancel()
```

This allows the final UI/input system to change without rewriting wall
geometry/layout logic.

## Responsibility split

### Core code (ChatGPT / repository architecture)

Owns:

- wall data contract;
- curve sampling;
- deterministic section layout;
- validation;
- preview presenter;
- placement-session API;
- stable section IDs;
- server wall registry.

### Codex in the real Unity project

Owns:

- importing the finished module;
- measuring real module length;
- prefab setup;
- pivot verification;
- collider;
- LODs;
- shared material assignment;
- terrain layer mask hookup;
- preview materials;
- wiring final build-tool UI/input;
- tiny adapter fixes only when required by the real asset.

Codex should adapt the asset to this contract before proposing a rewrite of the
wall architecture.
