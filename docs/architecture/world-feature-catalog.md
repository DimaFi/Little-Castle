# World Feature Catalog

This document defines where future generated content belongs technically.

It intentionally separates **terrain**, **macro world structure**, **local procedural decoration/resources**, and **runtime changes**.

## 1. Terrain foundation

Terrain data answers:

- how high is the ground?
- how steep is it?
- what broad terrain class is this?
- later: moisture, temperature, soil, biome and surface material.

Terrain generation must not know which prefab represents a tree, sign or building.

## 2. Macro world features

Macro features influence many chunks or need global spacing/connectivity.

Examples:

### Neutral settlements

Small independent settlements inspired by neutral/city-state-style systems.

Technical requirements later:

- stable generated settlement ID;
- world position and territory/influence radius;
- settlement archetype;
- economy/output profile;
- relationship/interaction state;
- capture/control/competition state if gameplay design approves it;
- road connections;
- nearby resource context.

They must be generated before local chunk decoration so a forest does not spawn through the village center.

### Roads

Roads are world-space networks.

Future road system must support different road types, for example:

- narrow dirt trail;
- normal dirt road;
- improved road;
- settlement street;
- military/fortified route if needed later.

A chunk receives only the clipped section of already planned road geometry.

### Rivers

Rivers cross chunk boundaries and therefore require macro planning or another continuity-preserving world-space algorithm.

River data should eventually drive:

- terrain carving;
- riverbed material;
- water;
- vegetation masks;
- bridge candidate locations.

### Bridges

Bridge placement is derived from meaningful crossings:

```text
road path ∩ water/river corridor
        ↓
bridge candidate
        ↓
slope / width / bank validation
        ↓
bridge feature
```

Do not scatter bridges randomly.

### Ruins and small fortified remains

Examples:

- ruined guard post;
- abandoned camp;
- broken wall/tower;
- small fortified ruin;
- old farmstead.

These should be point/area macro features with exclusion zones and local decoration profiles.

## 3. Local procedural content

Local features can be generated deterministically from world-space fields plus macro exclusion masks.

Examples:

- trees;
- bushes;
- grass;
- small rocks;
- flowers;
- fallen branches;
- minor signs/debris.

### Forests

Forests are strategically important because wood is expected to be consumed in large quantities by settlement and military construction.

Therefore forests should not be only visual decoration.

Future forest generation should separate:

```text
ForestRegionData       strategic density / regeneration / biome
        ↓
TreeSpawnData          concrete generated trees
        ↓
Tree prefab rendering  visuals
```

This allows a tree to be chopped without destroying the concept of a forest region.

## 4. Resource deposits

Ore and stone should use explicit resource data rather than just decorative meshes.

Likely examples:

- surface stone outcrop;
- quarry-capable stone deposit;
- exposed ore;
- deeper/richer ore deposit.

A deposit needs at minimum:

- stable ID;
- resource type;
- location/area;
- capacity/richness;
- depletion state later.

Resource visuals are presentation of that data.

## 5. Signs and road furniture

Signposts are usually derived from roads and destinations.

Examples:

- direction to settlement;
- road marker;
- warning sign;
- bridge marker.

They should be generated after roads/destinations exist.

## 6. Runtime world changes

Generated base world and player history are separate.

Examples:

```text
Generated forest region
+ chopped tree overrides
= current forest state

Generated neutral settlement
+ relationship/control/economy runtime state
= current settlement state

Generated road
+ player road upgrade/damage
= current road state
```

This separation is required for saves and future multiplayer.

## Generation ordering target

Long-term target:

```text
World Seed
  ↓
Regional terrain structure
  ↓
Height + slope + climate
  ↓
Water / river network
  ↓
Settlement / ruin / landmark plan
  ↓
Road graph
  ↓
Bridge sites
  ↓
Resource regions/deposits
  ↓
Forest / vegetation regions
  ↓
Chunk-local spawn data
  ↓
Unity rendering
```

Exact ordering may evolve, but dependencies must remain explicit.
