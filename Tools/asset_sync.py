"""Validate and copy a local art-library manifest into Unity's ignored Ready folders.

Default operation is read-only. --apply copies changed files atomically and never
deletes destination files or their Unity .meta files.
"""

from __future__ import annotations

import argparse
import hashlib
import json
import os
from pathlib import Path
import re
import shutil
import tempfile


IDENTIFIER = re.compile(r"^[A-Za-z0-9][A-Za-z0-9_-]*$")
MODEL_EXTENSIONS = {".fbx"}
TEXTURE_EXTENSIONS = {".png", ".tga", ".jpg", ".jpeg", ".exr"}


class ManifestError(ValueError):
    pass


def hash_file(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for block in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(block)
    return digest.hexdigest()


def require_identifier(value: object, label: str) -> str:
    if not isinstance(value, str) or not IDENTIFIER.fullmatch(value):
        raise ManifestError(f"{label} must use letters, digits, '_' or '-': {value!r}")
    return value


def source_file(root: Path, relative: object, extensions: set[str]) -> Path:
    if not isinstance(relative, str) or not relative or "\\" in relative:
        raise ManifestError(f"Invalid source path: {relative!r}")
    raw = Path(relative)
    if raw.is_absolute() or any(part in {"", ".", ".."} for part in raw.parts):
        raise ManifestError(f"Source path must stay inside the library: {relative!r}")
    path = (root / raw).resolve()
    if not path.is_relative_to(root) or not path.is_file():
        raise ManifestError(f"Source file missing or outside library: {relative}")
    if path.suffix.lower() not in extensions:
        raise ManifestError(f"Unsupported file type: {relative}")
    return path


def check_destination(destination: Path, project: Path) -> None:
    if destination.is_symlink():
        raise ManifestError(f"Destination is a symlink: {destination}")
    if destination.exists() and not destination.is_file():
        raise ManifestError(f"Destination is not a file: {destination}")
    current = destination.parent
    while current != project:
        if current.is_symlink():
            raise ManifestError(f"Destination contains a symlink: {current}")
        current = current.parent


def build_plan(library: Path, project: Path) -> list[tuple[Path, Path, str]]:
    manifest_path = library / "manifest.json"
    try:
        manifest = json.loads(manifest_path.read_text(encoding="utf-8"))
    except (OSError, json.JSONDecodeError) as error:
        raise ManifestError(f"Cannot read {manifest_path}: {error}") from error
    if not isinstance(manifest, dict) or manifest.get("schemaVersion") != 1:
        raise ManifestError("manifest.json requires schemaVersion: 1")
    assets = manifest.get("assets")
    if not isinstance(assets, list):
        raise ManifestError("manifest.json requires an assets array")

    planned: list[tuple[Path, Path, str]] = []
    seen: set[str] = set()
    for number, asset in enumerate(assets, 1):
        if not isinstance(asset, dict):
            raise ManifestError(f"Asset #{number} must be an object")
        category = require_identifier(asset.get("category"), "category")
        archetype = require_identifier(asset.get("archetypeId"), "archetypeId")
        variant = require_identifier(asset.get("variant"), "variant")
        if "assetId" in asset:
            require_identifier(asset["assetId"], "assetId")
        models = asset.get("lodModels")
        if models is not None:
            if "model" in asset or not isinstance(models, dict) or not all(
                    key in models for key in ("LOD0", "LOD1", "LOD2")) or any(
                    key not in {"LOD0", "LOD1", "LOD2", "LOD3"} for key in models):
                raise ManifestError(f"{archetype}/{variant}: lodModels needs LOD0-2 "
                                    "(optional LOD3) and must replace model")
            for level in range(len(models)):
                key = f"LOD{level}"
                if key not in models:
                    raise ManifestError(f"{archetype}/{variant}: missing {key}")
                model = source_file(library, models[key], MODEL_EXTENSIONS)
                destination = (project / "Assets/_Game/Models/Ready" / category /
                               archetype / f"{variant}_{key}.fbx")
                planned.append((model, destination, f"{archetype}/{variant} {key}"))
        else:
            model = source_file(library, asset.get("model"), MODEL_EXTENSIONS)
            destination = (project / "Assets/_Game/Models/Ready" / category /
                           archetype / f"{variant}.fbx")
            planned.append((model, destination, f"{archetype}/{variant} model"))
        textures = asset.get("textures", [])
        if not isinstance(textures, list):
            raise ManifestError(f"{archetype}/{variant}: textures must be an array")
        for relative in textures:
            texture = source_file(library, relative, TEXTURE_EXTENSIONS)
            destination = (
                project / "Assets/_Game/Textures/Ready" / category /
                archetype / variant /
                (texture.stem + texture.suffix.lower())
            )
            planned.append((texture, destination, f"{archetype}/{variant} texture"))

    for _, destination, _ in planned:
        key = str(destination).casefold()
        if key in seen:
            raise ManifestError(f"Duplicate destination in manifest: {destination}")
        seen.add(key)
        check_destination(destination, project)
    return planned


def copy_atomic(source: Path, destination: Path) -> None:
    destination.parent.mkdir(parents=True, exist_ok=True)
    handle, temporary = tempfile.mkstemp(prefix=".lc-sync-", dir=destination.parent)
    os.close(handle)
    try:
        shutil.copyfile(source, temporary)
        os.replace(temporary, destination)
    finally:
        if os.path.exists(temporary):
            os.unlink(temporary)


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--library", type=Path, required=True,
                        help="External library containing manifest.json and Exports")
    parser.add_argument("--project", type=Path,
                        default=Path(__file__).resolve().parent.parent,
                        help="Unity project root (defaults to this repository)")
    parser.add_argument("--apply", action="store_true",
                        help="Copy changed files; default is validation and dry run")
    args = parser.parse_args()

    library = args.library.resolve()
    project = args.project.resolve()
    if not (project / "ProjectSettings/ProjectVersion.txt").is_file():
        parser.error(f"Not a Unity project: {project}")
    try:
        plan = build_plan(library, project)
        changed = 0
        for source, destination, label in plan:
            same = destination.is_file() and hash_file(source) == hash_file(destination)
            status = "UNCHANGED" if same else ("COPY" if args.apply else "WOULD COPY")
            print(f"{status}: {label}: {source} -> {destination}")
            if not same:
                changed += 1
                if args.apply:
                    copy_atomic(source, destination)
        print(f"Validated {len(plan)} file(s); {changed} changed; " +
              ("applied" if args.apply else "dry run"))
        return 0
    except (ManifestError, OSError) as error:
        parser.exit(2, f"Asset sync failed: {error}\n")


if __name__ == "__main__":
    raise SystemExit(main())
