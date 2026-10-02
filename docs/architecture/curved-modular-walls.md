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


## Structural wall towers

A wall does not begin as a naked line.

The first committed wall point may spawn an authored **small structural wall
tower/base**:

`WallPlacementDefinition.startTowerPrefab`

From that tower the player drags the wall.

Long walls may automatically receive more small structural towers at a
deterministic interval:

`WallPlacementDefinition.automaticTowerSpacing`

The repeated tower prefab is:

`WallPlacementDefinition.repeatTowerPrefab`

If no explicit repeat prefab is assigned, the start tower prefab may be reused.

These are part of the wall path presentation. They are NOT independent network
entities by default.

Their transforms and stable IDs are derived deterministically from:

- wallId;
- wall path;
- tower index;
- WallPlacementDefinition.

Ordinary wall sections inside
`towerSectionClearanceRadius` are omitted so the tower replaces the wall
modules rather than overlapping them.

Codex/Astra must therefore author:

1. one straight wall module;
2. one small structural wall tower/base that visually accepts wall modules on
   its sides.

The small structural tower is distinct from a future gameplay building such as
an archer tower.

## Smooth and Sharp control points

Every wall control point has one of two modes:

- `Smooth` — the wall uses the smooth spline through the point;
- `Sharp` — the adjacent path segments remain straight into/out of the point.

During player placement:

- normal clicks create Smooth control points;
- pressing Shift once toggles the **last committed point** Smooth <-> Sharp.

This is intended to make both styles easy:

```text
Smooth:
──────╮
      ╰──────

Sharp:
──────┐
      └──────
```

A square/rectangular perimeter is created by marking its corner points Sharp.

Do not replace Sharp with a very small rounding radius. The player explicitly
requested a true hard-corner mode.

The normal maximum-turn rule applies to smooth curve portions. Deliberately
Sharp points are exempt from that smooth-turn restriction.

## Defensive structures and wall sockets

Large gameplay structures such as:

- archer towers;
- gatehouses;
- bastions;
- future fortified buildings;

are separate authoritative structures, not generated wall towers.

To connect them cleanly to walls, their prefab must expose one or more child
objects with:

`WallConnectionSocket`

A WallConnectionSocket defines:

- a stable `socketId`;
- exact connection position;
- local +Z preferred outgoing wall direction;
- snap radius;
- wall clearance radius.

### Asset authoring requirement

Codex must place sockets at the exact visual place where the centerline of the
wall should meet the structure.

Recommended prefab shape:

```text
ArcherTowerRoot
├── Visuals
├── Collider(s)
├── WallSocket_Left
│   └── WallConnectionSocket
└── WallSocket_Right
    └── WallConnectionSocket
```

For current automatic mouse snapping, each socket should have a small trigger
Collider on the socket object (or otherwise be directly discoverable by the
configured socket physics layer).

The socket transform:

- position = exact wall connection point;
- local +Z = direction the wall should leave the structure.

When a player draws a wall close enough to a socket:

1. cursor preview snaps to the socket;
2. committed control point uses the exact socket position;
3. structureId + socketId are stored in `WallSocketAttachmentState`;
4. ordinary wall sections inside the socket clearance radius are omitted;
5. the independent structure visually occupies that gap.

This lets a wall terminate at / continue through a defensive structure without
drawing wall modules through the tower.

### Retrofitting a tower into an existing confirmed wall

The data contract is already prepared for this:

- insert a control point at the wall/tower connection;
- mark it with a WallSocketAttachmentState;
- rebuild the deterministic local presentation;
- sections inside clearance radius disappear.

The future tower-placement gameplay tool still needs to perform that edit when
the actual archer-tower building system exists.

Do not create a second unrelated wall system for tower retrofits.

## Input behavior target

The intended player flow is:

```text
build menu -> choose wall
first click -> small structural start tower appears
move mouse -> live wall preview stretches from it
click -> commit a Smooth path point
Shift -> toggle the last committed point to Sharp
continue dragging/clicking
near a defensive tower socket -> preview snaps to socket
Enter -> confirm
Esc -> cancel
```

This interaction contract should remain stable even if final UI bindings are
later remapped.
