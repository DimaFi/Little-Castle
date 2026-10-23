# External Models → Procedural World

## Responsibilities

### Local/external script

Your separate modeling project owns:

- model creation;
- geometry cleanup;
- polygon optimization;
- export;
- optional textures/material preparation;
- copying the finished model into the Unity project's Ready folder.

### Unity

Unity only:

- imports the finished model;
- registers it as a visual variant;
- lets the existing procedural generator place it in the world.

## Folder contract

External script target:

```text
Assets/_Game/Models/Ready/<Category>/<ArchetypeId>/<VariantFile>
```

The category folder is organizational.

The `ArchetypeId` folder is important because the current generator already
creates logical objects by archetype ID.

Example:

```text
Assets/_Game/Models/Ready/Trees/tree_main_01/
├─ Tree01.fbx
├─ Tree02.fbx
├─ Tree03.fbx
└─ Tree04.fbx
```

All four become visual variants of `tree_main_01`.

The same generated tree keeps the same variant after chunk unload/reload
because `WorldSpawnCatalog` selects variants from the object's stable ID.

## Tomorrow's fastest iteration loop

```text
1. Make/export several models
2. Local script copies them into Ready folders
3. Let Unity import them
4. Run Little Castle > World > Sync Models And Generate Preview
5. Inspect scale, density, repetition and composition
6. Adjust models or generation settings
7. Repeat
```

Textures are optional for this stage.

Untextured models are enough to evaluate:

- silhouette;
- object scale;
- tree density;
- rock density;
- visual repetition;
- spacing;
- landmark readability;
- how well the procedural world composition works.

## Important

Do not create one procedural archetype per visual variant unless the models need
different spawn rules.

Usually:

```text
one archetype
+
many visual variants
```

is preferable.

Later, when real models are available, the pipeline can be extended with:

- automatic bounds/scale validation;
- category-specific collider rules;
- LOD generation/assignment;
- material checks;
- texture remapping;
- GPU-instanced vegetation;
- biome-specific archetype groups.


## Mandatory preflight before the real filling test

After copying a new production batch into `Models/Ready`, run:

```text
Little Castle > Preflight > Run Full Model + World Preflight
```

The command performs the test in this order:

1. synchronizes `Models/Ready` into `WorldSpawnCatalog`;
2. removes stale catalog entries that were previously managed by the Ready folder;
3. validates every ready model with the production asset rules;
4. verifies that generated archetype IDs resolve to catalog variants;
5. validates `WorldDefinition` and all generation/streaming contracts;
6. generates 125 data-only chunks across five deterministic seeds;
7. validates seams, deterministic spawns and stable IDs;
8. checks that generated objects remain inside their owning chunk;
9. checks that roads do not cross `MountainBarrier`;
10. checks the fixed standardized bridge span;
11. records per-generation-stage average/max timings;
12. reports spawn density by category and the maximum spawn count in one chunk.

A PASS means the data contracts are internally consistent. It does **not**
replace a visual pass in the Game view with the real model scale and silhouette.

### Test order with real models

Use this order:

```text
external model preparation
        ↓
Models/Ready/<Category>/<ArchetypeId>/
        ↓
Full Model + World Preflight
        ↓
Sync Models And Generate Preview
        ↓
inspect scale / density / repetition
        ↓
Play WorldGenerationTest
        ↓
F8 diagnostics while moving the camera
```

Do not tune procedural density around a broken model scale. Validate the asset
first, then judge world composition.

## Runtime presentation performance rules

The runtime catalog now caches `archetypeId -> valid prefab variants`; dense
chunks no longer scan the entire catalog for every tree/rock/prop.

Generated object colliders are distance-tiered independently from visual LOD.
Far trees/rocks may remain rendered while their Physics colliders are disabled.

The current production rule remains:

- author geometry LODs before mass placement;
- keep far shadows/lights/probes cheap;
- use the F8 overlay to identify measured bottlenecks;
- do not move authoritative generation to prefabs/GameObjects;
- do not add speculative GPU/HLOD systems until real production models show
  that ordinary authored LOD + collider tiers are insufficient.
