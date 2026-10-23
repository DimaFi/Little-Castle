# Ready Models

This folder is the target for the external/local model pipeline.

Unity does not convert or optimize models here. Your external script should
place already game-ready assets using:

```text
Assets/_Game/Models/Ready/
<Category>/<ArchetypeId>/<VariantFile>
```

Example:

```text
Trees/tree_main_01/Tree_A.fbx
Trees/tree_main_01/Tree_B.fbx
Trees/tree_main_01/Tree_C.fbx

Rocks/rock_ground_01/Rock_A.fbx
Rocks/rock_ground_01/Rock_B.fbx
```

All model assets inside the same `ArchetypeId` folder become visual variants
of the same logical procedural object.

Current useful archetype IDs from the test world:

```text
tree_main_01
rock_ground_01
deposit_stone_01
deposit_iron_01
bridge_wood_small
ruin_fortified_01
neutral_village_01
```

After copying models into this folder, use:

```text
Little Castle > Assets > Sync Ready Models
```

or for the fastest visual loop:

```text
Little Castle > World > Sync Models And Generate Preview
```

The second command syncs the model variants into `WorldSpawnCatalog`, opens
the existing world-generation test scene and rebuilds the preview.

For early visual testing, models may be completely untextured.


## Before the production filling test

Run:

```text
Little Castle > Preflight > Run Full Model + World Preflight
```

Do this after every large new model batch.

The preflight synchronizes this folder, checks every production model, verifies
catalog coverage and stress-generates multiple seeds before the models are judged
in the world.

A model can be visually unfinished during an early composition test, but it must
still have sane geometry/import structure. Major repeated assets should have
authored LODs before large-scale performance testing.
