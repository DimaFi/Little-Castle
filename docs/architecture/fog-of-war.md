# Fog of War

## Product rule

The full finite map exists from session creation, but each player/team has
limited information.

Fog of war is separate from procedural generation.

Generation decides what exists.

Fog decides what a viewer currently knows/sees.

## Logical visibility states

The current data foundation uses:

```text
Hidden
Explored
Visible
```

### Hidden

The viewer has never explored the area.

This does **not** require the terrain geometry to be absent.

The future renderer may still show a dark/grey world silhouette while hiding
meaningful information.

### Explored

The area was seen previously but is not currently observed.

Typical presentation:

- terrain remains known;
- static remembered information may remain;
- current enemy units/movement are hidden;
- current dynamic state is not guaranteed visible;
- area is grey/dimmed.

### Visible

At least one active vision source currently observes the area.

Current information can be presented normally.

## Vision sources

Fog code must not depend directly on concrete unit/building classes.

Gameplay systems expose plain `VisionSourceData`.

Possible vision sources:

- player-controlled units;
- ruler/hero;
- houses/buildings;
- towers;
- military outposts;
- owned neutral settlements;
- other future scouting structures.

Example:

```text
Unit / Building / Owned Village
        ↓
VisionSourceData
        ↓
FogOfWarGrid.RevealCircle(...)
```

## Current implementation

Files:

- `FogOfWarVisibility.cs`
- `VisionSourceData.cs`
- `FogOfWarGrid.cs`

`FogOfWarGrid` currently stores one logical visibility layer for one
viewer/player/team.

Update flow:

```text
BeginVisibilityUpdate()
        ↓
clear only current Visible state
        ↓
apply all current vision sources
        ↓
Visible cells become Explored permanently for the match
```

Therefore:

```text
Hidden
  ↓ first reveal
Visible
  ↓ source leaves
Explored
  ↓ source returns
Visible
```

## Important separation

`FogOfWarGrid` contains no:

- shader references;
- RenderTextures;
- cameras;
- materials;
- prefab references.

A future Unity renderer can convert logical visibility into:

- world-space fog texture;
- terrain darkening;
- object visibility;
- minimap visibility;
- edge darkness.

The logical gameplay contract should remain independent.

## Information filtering

Future presentation/gameplay queries should distinguish terrain visibility from
entity visibility.

An unexplored/grey area may still show the shape of the world while hiding:

- trees/resource details if desired by final design;
- neutral settlement details;
- enemy units;
- enemy buildings;
- moving armies;
- current ownership changes;
- resource depletion;
- other dynamic information.

Exact remembered-information rules are a game-design decision and should not be
hard-coded into terrain generation.

## Owned neutral settlements

If gameplay later allows a neutral settlement to become controlled/allied,
ownership may provide a persistent vision source around that settlement.

The fog system only consumes the resulting `VisionSourceData`; it does not own
settlement diplomacy/control logic.

## Map edge

Playable bounds and fog are different concepts.

- Fog controls information inside the playable map.
- Playable bounds prevent camera/unit gameplay from leaving the map.
- Visual padding hides the physical edge of generated terrain.
- Strong edge darkness/fog should visually obscure the final visual boundary.
- Beyond visual bounds no world content needs to exist.

## Multiplayer

Fog state is viewer-specific.

Do not make one global fog state for every player.

Future authority model may use:

- one layer per player;
- one shared layer per team;
- server-authoritative visibility for hidden enemy information.

The current `viewerId` in `VisionSourceData` leaves this decision open.
