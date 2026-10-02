# Codex Handoff — Wall Asset Integration

Read before importing or generating any production wall/tower asset.

Canonical architecture:

`docs/architecture/curved-modular-walls.md`

## Required assets

Codex/Astra/Blender should prepare at minimum:

1. **Straight wall module**
   - rigid;
   - local +Z = module length;
   - pivot centered in X/Z and near base Y;
   - identity root transform;
   - measured real length entered into WallPlacementDefinition;
   - simple collider;
   - authored LODs;
   - shared Little Castle materials.

2. **Small structural wall tower/base**
   - appears at the very first committed wall point;
   - wall visually grows outward from it;
   - must accept the straight wall module cleanly;
   - used by `startTowerPrefab`;
   - may also be used by `repeatTowerPrefab`;
   - repeating towers are generated automatically by configured spacing.

3. **Future archer tower / gatehouse / bastion**
   - separate gameplay building, not the same as the small generated wall tower;
   - must expose one or more child `WallConnectionSocket` objects;
   - socket position = exact wall centerline connection;
   - socket local +Z = preferred outgoing wall direction;
   - socket must reserve enough clearance so wall modules do not intersect the
     structure;
   - current mouse snapping expects the socket to be discoverable through a
     collider on the configured socket physics layer; a small trigger collider
     on the socket object is the preferred simple setup.

## Placement behavior that assets must support

Player flow:

- choose wall in build menu;
- first click -> small structural start tower appears;
- move mouse -> live wall preview stretches from it;
- click -> commits a Smooth point;
- press Shift once -> last committed point toggles Smooth <-> Sharp;
- Sharp points create true hard corners for square/rectangular walls;
- continue dragging/clicking;
- near WallConnectionSocket -> preview snaps exactly to the structure;
- Enter -> confirm;
- Esc -> cancel.

Do not redesign this interaction around the model.

## Important distinction

### Small generated wall towers

- derived from one WallRuntimeState;
- deterministic;
- not independent network entities by default;
- stable tower IDs are derived from wallId + towerIndex;
- regular wall modules are suppressed around them.

### Archer towers / gatehouses

- independent authoritative structures;
- may exist before the wall is drawn;
- wall can snap to their sockets;
- socket attachment stores structureId + socketId;
- wall modules are suppressed inside socket clearance.

A future tool may retrofit an archer tower into an already confirmed wall by
inserting a control point + socket attachment and rebuilding the wall. The data
contract already supports this; do not create a second wall system.

## Mandatory Unity checks

Run:

`Little Castle -> Building -> Validate Selected Wall Definition`

For large tower/building prefabs also run:

`Little Castle -> Assets -> Validate Selected Production Model`

Inspect manually:

- actual module length;
- pivot;
- +Z axis;
- seam quality on curves;
- true Sharp 90-degree corner;
- start tower seam;
- repeating tower seam;
- socket alignment from both wall directions;
- collider overlap;
- LOD transitions;
- shadows/performance.

## Codex rule

Adapt the production model/prefab to the existing contract first.

Do not rewrite:

- WallPathLayoutUtility;
- WallRuntimeState;
- Smooth/Sharp behavior;
- deterministic section/tower IDs;
- socket attachment contract;

just because one generated model has a bad pivot, axis, scale, socket position or
incorrect measured length.
