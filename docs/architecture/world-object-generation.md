# Generated World Objects

## Purpose

World generation must be able to place trees, rocks, grass, bushes, resource visuals, ruins, settlement anchors, bridges and future props without coupling deterministic generation to Unity prefabs.

The contract is:

```text
Generation rule
    ↓
WorldSpawnData
    ↓
archetypeId
    ↓
WorldSpawnCatalog
    ↓
Unity prefab / variant
```

## WorldSpawnData

A generated object stores:

- `stableId`
- `archetypeId`
- category
- world position
- yaw
- uniform scale

It does **not** store a prefab reference.

This means an art asset can be replaced without changing generated-world identity.

## Stable IDs

Stable IDs are required for runtime state.

Example:

```text
Generated tree #928...
        ↓
player chops it
        ↓
WorldRuntimeDeltaState.MarkSpawnRemoved(id)
        ↓
chunk unloads
        ↓
chunk regenerates from seed
        ↓
presenter sees removed ID
        ↓
tree stays removed
```

Do not use Unity instance IDs or `string.GetHashCode()` as generated identity.

## ruleId

Scatter/resource/macro rules use manually stable `ruleId` values.

Examples:

```text
tree_oak_common
tree_oak_sparse
rock_granite_common
stone_surface_deposit
iron_highland_deposit
neutral_village_common
ruin_watchtower_common
```

Once persistent saves matter, changing a `ruleId` changes generated IDs and must be treated as a generation-version change.

## Object scatter

`ObjectScatterStage` uses a global deterministic grid with jitter.

Because the grid exists in world coordinates rather than chunk-local coordinates:

- points remain stable when chunks load independently;
- border points belong to exactly one chunk;
- negative chunk coordinates work;
- changing preview/loading radius does not reshuffle local objects.

Rules can filter by:

- terrain class;
- height;
- slope;
- forest density;
- placement exclusion masks.

## Forests

`ForestDensityStage` generates a continuous strategic density field first.

Then tree scatter samples it.

```text
ForestDensity
    ↓
Tree placement probability
    ↓
WorldSpawnData(Tree)
```

This is intentionally different from treating the presence of tree prefabs as the forest database.

Future systems can add:

- forest region capacity;
- species mix;
- regrowth;
- managed forestry;
- fire/disease;
- strategic map overlays.

## Placement blocks

`PlacementBlockFlags` let earlier infrastructure reserve cells before local scatter.

Examples:

- settlement -> block almost everything;
- road -> block trees, large props and deposits;
- river -> block all ordinary placement;
- ruin -> block conflicting trees/resources around its footprint.

Macro projection must run before local object/resource stages.

## Resource deposits

`ResourceDepositStage` creates `WorldResourceDepositData`.

A deposit stores:

- stable ID;
- resource kind;
- location;
- radius;
- richness;
- capacity;
- optional visual archetype.

Visual ore/stone prefabs are not authoritative resource storage.

Runtime depletion belongs in `WorldRuntimeDeltaState`.

## Prefab catalog

`WorldSpawnCatalog` maps logical archetypes to one or more visual prefabs.

Example:

```text
tree_oak_01
  ├─ Oak_A.prefab
  ├─ Oak_B.prefab
  └─ Oak_C.prefab
```

The generated stable ID chooses the variant deterministically.

Therefore a tree does not randomly change visual variant every time its chunk reloads.

## Performance boundary

`ChunkSpawnPresenter` currently creates normal GameObjects.

That is appropriate for:

- early previews;
- buildings;
- bridges;
- rocks;
- medium object counts.

It is **not** the intended final renderer for tens of thousands of grass blades or dense forests.

Later presentation can replace it with:

- object pooling;
- GPU instancing;
- indirect instancing;
- HLOD/LOD clusters;

without changing `WorldSpawnData`.
