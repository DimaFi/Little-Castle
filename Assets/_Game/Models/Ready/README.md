# Ready Models

This folder is the target for the external/local model pipeline. Its imported
models, generated prefabs, materials, catalog and `.meta` files stay local and
are ignored by Git.

Run `python Tools/asset_sync.py --library <external-library>` at the Unity
project root to validate the external `manifest.json`. Add `--apply` to copy
its game-ready FBX and textures into the ignored Ready folders. The tool
updates changed files only and preserves existing Unity `.meta` files.

The model layout is:

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

Each asset needs at least three authored LODs. An FBX may contain sibling
render nodes `LOD0`, `LOD1`, `LOD2` (optionally `LOD3`), or the library manifest
may supply `lodModels` with separate FBX files for each level. The latter are
copied as `<Variant>_LOD0.fbx`, `<Variant>_LOD1.fbx`, and so on. The editor tool
creates a local prefab with `LODGroup` from either layout. All FBX files inside
the same `ArchetypeId` folder become visual variants of the same logical
procedural object; **do not put unrelated house/wall modules there just to
import them**. The separate Asset Book IDs remain the stable identity for art
releases; the archetype ID is the runtime spawn identity.

`Tools/audit_asset_books.py` compares the legacy and new Asset Books with the
22-module house catalogue and `CURRENT.json`. Run it before preparing a release:

```text
python Tools/audit_asset_books.py
python Tools/audit_asset_books.py --json
```

This audit is read-only. It does not certify a model for Unity. Source-only
`.blend` files still need a reviewed FBX/texture export, authored LODs and
Unity validation. Unpainted modules can be exported and tested with temporary
shared materials; their stable Asset Book IDs make later replacements possible.

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

The second command updates the ignored `LocalWorldSpawnCatalog`, opens the
existing world-generation test scene and rebuilds the preview. The shared
`MainWorldSpawnCatalog` remains unchanged. Local variants override its
placeholders only while running in Unity Editor.

Textures live at `Assets/_Game/Textures/Ready/<Category>/<ArchetypeId>/<Variant>/`.
Names such as `Tree_A_Leaves_BaseColor.png` and
`Tree_A_Solid_Normal.png` are mapped to local materials using the shared
Little Castle shader families. Texture-less models use shared default
materials. Inspect each generated prefab with the Production Asset Validator;
foliage also needs the Foliage Validator and a Play Mode visual check.

For early visual testing, models may be completely untextured.
