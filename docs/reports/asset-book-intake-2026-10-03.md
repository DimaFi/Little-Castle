# Asset Book intake audit — 2026-10-03

The repeatable source inventory is `python Tools/audit_asset_books.py` from
the Unity repository. This report records the first local run; no binary art
was copied to Unity in this audit.

| Source | Records | Model records | Model LOD packaging |
|---|---:|---:|---|
| `CozySettlement/AssetsDatabase/AssetBook.json` | 125 | 63 | 4 separate FBX LOD sets; 59 without complete listed LOD packaging |
| `Little-Castle_Assets/AssetBook/AssetBook.json` | 21 | 11 | 11 FBX with embedded LOD0–LOD2 declared; all still `InProgress` |

All 74 registered model source files exist locally. All 22 standalone modules
linked by `Art/House_Cottage_A/Docs/07_Module_Catalog_RU.md` are registered and
exist. All 15 active model paths in the house `CURRENT.json`, including newer
yard overrides, are registered and exist. This does **not** prove that every
historical Blender file is a distinct current asset; the scan intentionally
uses the current module catalogue and active manifest, not old version folders.
There are 20 other distinct `.blend` filename families whose stems do not
match a primary Asset Book source filename. Most appear to be blockouts,
material studies, previews or editable sources behind registered FBX exports;
the audit lists them for manual provenance review. They are **not** yet proven
to be missing game items. Thus the current house roster is complete, while a
claim that absolutely no historical model was omitted would be premature.

`TEX_DoorWood` and `TEX_RoofTile` are repeated between books. Their referenced
source images have identical SHA-256 bytes, so they are duplicate copies, not
observed divergent textures. Keep the IDs stable and reconcile provenance when
the legacy Asset Book is migrated; do not silently rename either record.

Next intake steps:

1. Build versioned, reviewed export packages keyed by Asset Book ID outside
   the Unity repository. Export current `.blend` modules, including unpainted
   house pieces; assign temporary shared materials where necessary.
2. Author LODs by model family. The 59 records without complete LOD packaging
   include small reusable parts, so review which need full LOD0–LOD2 versus
   inclusion inside a parent prefab. Do not blindly decimate foliage or wall
   sockets/collision geometry.
3. Import a small approved batch with `Tools/asset_sync.py`. It now supports
   one FBX with embedded LOD nodes or `lodModels` with separate FBX files.
   Keep Blender working files and source textures outside Unity and Git.
4. Validate Unity scale, pivot, materials, LOD switches and gameplay context.
   Trees require the foliage validator and Play Mode visual check; walls require
   the wall definition validator and seam/socket check. Connect only approved
   prefabs to runtime catalogs. Retain the same Asset Book ID and Unity path
   when replacing a model with a later version so Unity `.meta` GUIDs survive.

Current gating issue: the installed Unity Editor rejected headless compilation
for lack of a valid headless license. Python intake tests pass, but the new C#
editor importer still needs an interactive Unity compile and visual test before
any batch is described as production-ready.

## House LOD candidate batch

Blender 4.4.3 successfully opened the current source files and produced
separate LOD0/1/2 FBX *candidates* for all 22 listed house modules under
`CozySettlement/LocalHandoffs/<AssetId>/candidate-v001/`. This directory is
Git-ignored and contains about 14 MB of FBX plus QA JSON. The original `.blend`
files were not edited. These exports are deliberately **not** in Unity yet:
materials/UVs, pivots, silhouette and visual quality remain unapproved.

21/22 candidates have strictly decreasing triangle counts. A provisional
efficiency check (LOD1 at most 85% and LOD2 at most 50% of LOD0 triangles)
passes only 11/22. `MOD_StoneWall_A` has 2444 triangles at every level;
decimation did nothing, so its LODs must be redesigned. Door, gate, windows,
crate and other detailed multi-piece objects also need family-specific LOD
work; do not import this batch wholesale or infer that a successful FBX export
means optimization is finished. The exporter is
`CozySettlement/Scripts/export_house_lod_candidate.py`.
