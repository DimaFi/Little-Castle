import json
from pathlib import Path
import tempfile
import unittest

import asset_sync


class AssetSyncTests(unittest.TestCase):
    def setUp(self):
        self.temporary = tempfile.TemporaryDirectory()
        self.root = Path(self.temporary.name)
        self.library = self.root / "library"
        self.project = self.root / "project"
        self.library.mkdir()
        (self.project / "ProjectSettings").mkdir(parents=True)
        (self.project / "ProjectSettings/ProjectVersion.txt").write_text(
            "m_EditorVersion: test", encoding="utf-8")
        model = self.library / "Exports/Models/Trees/tree_main_01/Tree_A.fbx"
        model.parent.mkdir(parents=True)
        model.write_bytes(b"model-v1")
        self.model = model

    def tearDown(self):
        self.temporary.cleanup()

    def manifest(self, model="Exports/Models/Trees/tree_main_01/Tree_A.fbx"):
        (self.library / "manifest.json").write_text(json.dumps({
            "schemaVersion": 1,
            "assets": [{
                "category": "Trees",
                "archetypeId": "tree_main_01",
                "variant": "Tree_A",
                "model": model,
                "textures": [],
            }],
        }), encoding="utf-8")

    def test_plan_and_copy_preserve_existing_meta(self):
        self.manifest()
        plan = asset_sync.build_plan(self.library.resolve(), self.project.resolve())
        self.assertEqual(len(plan), 1)
        source, destination, _ = plan[0]
        destination.parent.mkdir(parents=True)
        meta = Path(str(destination) + ".meta")
        meta.write_text("stable-guid", encoding="utf-8")
        asset_sync.copy_atomic(source, destination)
        self.assertEqual(destination.read_bytes(), b"model-v1")
        self.assertEqual(meta.read_text(encoding="utf-8"), "stable-guid")
        self.model.write_bytes(b"model-v2")
        asset_sync.copy_atomic(source, destination)
        self.assertEqual(destination.read_bytes(), b"model-v2")
        self.assertEqual(meta.read_text(encoding="utf-8"), "stable-guid")

    def test_rejects_escape_and_duplicate_destination(self):
        self.manifest("../outside.fbx")
        with self.assertRaises(asset_sync.ManifestError):
            asset_sync.build_plan(self.library.resolve(), self.project.resolve())
        self.manifest()
        data = json.loads((self.library / "manifest.json").read_text(encoding="utf-8"))
        data["assets"].append(dict(data["assets"][0]))
        (self.library / "manifest.json").write_text(json.dumps(data), encoding="utf-8")
        with self.assertRaises(asset_sync.ManifestError):
            asset_sync.build_plan(self.library.resolve(), self.project.resolve())

    def test_separate_lod_models_keep_stable_variant_path(self):
        models = {}
        for level in range(3):
            relative = f"Exports/Models/Trees/tree_main_01/Tree_A_LOD{level}.fbx"
            path = self.library / relative
            path.write_bytes(f"lod{level}".encode())
            models[f"LOD{level}"] = relative
        (self.library / "manifest.json").write_text(json.dumps({
            "schemaVersion": 1,
            "assets": [{"category": "Trees", "archetypeId": "tree_main_01",
                        "assetId": "VEG_Tree_A", "variant": "Tree_A",
                        "lodModels": models}],
        }), encoding="utf-8")
        plan = asset_sync.build_plan(self.library.resolve(), self.project.resolve())
        self.assertEqual([destination.name for _, destination, _ in plan],
                         ["Tree_A_LOD0.fbx", "Tree_A_LOD1.fbx", "Tree_A_LOD2.fbx"])

    def test_rejects_lod_gap(self):
        self.manifest()
        data = json.loads((self.library / "manifest.json").read_text(encoding="utf-8"))
        data["assets"][0].pop("model")
        data["assets"][0]["lodModels"] = {"LOD0": "a.fbx", "LOD2": "b.fbx"}
        (self.library / "manifest.json").write_text(json.dumps(data), encoding="utf-8")
        with self.assertRaises(asset_sync.ManifestError):
            asset_sync.build_plan(self.library.resolve(), self.project.resolve())


if __name__ == "__main__":
    unittest.main()
