# Legacy rigid-rock compatibility addendum

Root identified a confirmed source water-only regression after integration: source `waterOnly` skipped catalog definitions, all river rock records and every standalone rock section; the water catalog has no entries. This made saved v1 rocks invisible and newly applied standalone sections empty. Authoritative saved records were retained, but presentation and new section authoring regressed.

After root confirmed completion of the running Unity configuration process, this was fixed additively in MAIN:

- `MapRiverPresentation.cs`: separate `rockCatalog` from the water-material `catalog`. In water-only mode approved baseline `MapRockCatalog.Default` supplies definitions, prefabs and legacy bank material. Optional fourth constructor argument `legacyRockCatalog` supports explicit/custom catalogs. Original resident limits, creation budget, Changed invalidation and IDs remain in use. Saved v1 rigid rocks and explicitly manual rocks render; standalone sections always render. Existing v1 bank closure uses the original bank material even when the water material is overridden. No source experimental cliff/bank-mass meshes were added.
- `MapEditSession.Rivers.cs`: automatic rock layout is skipped only for explicitly v2-or-newer rivers whose nodes all have Sand/Sand banks. Therefore registering fallback definitions enables standalone sections without populating new sand-only rivers. v1 layout remains unchanged; existing rock records are not recomputed.
- `MapRiverWaterInteropTests.cs`: one additional test, `WaterCatalogOverride_RetainsLegacyRigidRocksAndStandaloneSectionsAcrossHistoryAndSave`, loads a saved v1 composition with the actual empty water-material catalog and implicit approved fallback, checks visible rocks and legacy bank material, creates/edits/deletes/restores sections through Undo/Redo/save/load, and verifies new v2 sand-only rivers retain an empty rock list.

`git diff --check`: PASS. Unity compilation and this regression test: **NOT RUN by this agent**. Root may now launch the next in-process/batch verification. No source workspaces or other owners' files changed.

This supersedes the initial merge-report instruction to avoid registering a rock catalog for water-only sessions: fallback registration is necessary to preserve the existing standalone and legacy mechanisms, while the v2 Sand/Sand guard suppresses unwanted new automatic layouts.
