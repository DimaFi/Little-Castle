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
