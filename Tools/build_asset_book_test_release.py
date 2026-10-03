"""Build a versioned, local-only Unity test handoff from both Asset Books.

The package is reviewed for file integrity and LOD counts, NOT for production
visual quality. It imports under Test_* folders and cannot enter the world
spawn overlay. Never overwrites an existing version.
"""

from __future__ import annotations

import argparse
import hashlib
import json
from pathlib import Path
import shutil
import tempfile

from audit_asset_books import audit


def sha256(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for block in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(block)
    return digest.hexdigest()


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    base = Path(__file__).resolve().parents[2]
    parser.add_argument("--legacy", type=Path, default=base / "CozySettlement")
    parser.add_argument("--new", type=Path, default=base / "Little-Castle_Assets")
    parser.add_argument("--version", default="v001")
    args = parser.parse_args()
    legacy, new = args.legacy.resolve(), args.new.resolve()
    handoffs = legacy / "LocalHandoffs"
    handoffs.mkdir(exist_ok=True)
    destination = handoffs / f"UnityTestRelease-{args.version}"
    if destination.exists():
        parser.error(f"Will not overwrite existing package: {destination}")
    data = audit(legacy, new)
    staging = Path(tempfile.mkdtemp(prefix=".unity-test-release-", dir=handoffs))
    manifest = {"schemaVersion": 1, "assets": []}
    reviews = []
    try:
        wall_qa_path = new / "Source/Architecture/Wall_Stone_Modular/v004/QA/export_validation.json"
        wall_qa = json.loads(wall_qa_path.read_text(encoding="utf-8"))
        if wall_qa.get("status") != "PASS":
            raise ValueError("Wall export QA is not PASS")
        walls = {item["name"]: item for item in wall_qa["assets"]}
        for record in data["records"]:
            asset_id = record["id"]
            root = legacy if record["library"] == "CozySettlement" else new
            source = root / record["source"]
            category = "Test_" + record["category"].replace(" ", "")
            files: list[Path]
            qa_note: dict = {}
            if record["lodPackaging"] == "not-ready":
                versions = sorted((handoffs / asset_id).glob("candidate-v*/candidate.json"))
                if not versions:
                    raise ValueError(f"No candidate for {asset_id}")
                qa = json.loads(versions[-1].read_text(encoding="utf-8"))
                counts = qa["lodTriangles"]
                if not (counts[0] > counts[1] > counts[2] and
                        counts[1] <= .85 * counts[0] and
                        counts[2] <= .50 * counts[0]):
                    raise ValueError(f"LOD reduction gate failed: {asset_id}: {counts}")
                files = [versions[-1].parent / f"{asset_id}_LOD{level}.fbx"
                         for level in range(3)]
                qa_note = {"lodTriangles": counts, "candidate": str(versions[-1]),
                           "sourceMeshes": qa.get("objects")}
            elif record["lodPackaging"] == "separate":
                files = [source] + [legacy / relative for relative in
                                    record["separateLodFiles"][:2]]
                if len(files) != 3:
                    raise ValueError(f"Incomplete existing LOD set: {asset_id}")
            else:
                files = [source]
                wall = walls.get(source.stem)
                if wall is None or wall.get("status") != "PASS" or \
                        wall.get("sha256") != sha256(source):
                    raise ValueError(f"Wall QA/source hash mismatch: {asset_id}")
                qa_note = {"lodTriangles": wall["triangles"],
                           "wallGeometryQa": "PASS", "unityTested": False}
            if any(not path.is_file() for path in files):
                raise ValueError(f"Missing FBX for {asset_id}")
            digests = [sha256(path) for path in files]
            if len(set(digests)) != len(digests):
                raise ValueError(f"Duplicate LOD file bytes: {asset_id}")
            model_root = Path("Models") / asset_id
            package_paths = []
            for level, path in enumerate(files):
                name = f"{asset_id}_LOD{level}.fbx" if len(files) > 1 else f"{asset_id}.fbx"
                relative = model_root / name
                target = staging / relative
                target.parent.mkdir(parents=True, exist_ok=True)
                shutil.copy2(path, target)
                if sha256(target) != digests[level]:
                    raise ValueError(f"Copy hash mismatch: {asset_id} {level}")
                package_paths.append(relative.as_posix())
            entry = {"category": category, "archetypeId": asset_id,
                     "assetId": asset_id, "variant": asset_id, "textures": []}
            if len(package_paths) > 1:
                entry["lodModels"] = {f"LOD{level}": path
                                      for level, path in enumerate(package_paths)}
            else:
                entry["model"] = package_paths[0]
            manifest["assets"].append(entry)
            reviews.append({"assetId": asset_id, "book": record["library"],
                            "source": record["source"], "sourceSha256": sha256(source),
                            "packageFiles": package_paths, "packageSha256": digests,
                            "status": "EDITOR_TEST_ONLY", "productionApproved": False,
                            "materialsReviewed": False, "unityVisualTested": False,
                            **qa_note})
        (staging / "manifest.json").write_text(
            json.dumps(manifest, ensure_ascii=False, indent=2), encoding="utf-8")
        (staging / "review.json").write_text(json.dumps({
            "version": args.version, "status": "EDITOR_TEST_ONLY",
            "productionApproved": False, "assets": reviews,
        }, ensure_ascii=False, indent=2), encoding="utf-8")
        staging.rename(destination)
        print(f"Built {destination}: {len(reviews)} assets, "
              f"{sum(len(r['packageFiles']) for r in reviews)} FBX; "
              "EDITOR_TEST_ONLY, not production approved")
    except Exception:
        print(f"Incomplete staging retained for inspection: {staging}")
        raise


if __name__ == "__main__":
    main()
