"""Read-only inventory of the two art books and the current house module list.

Run from anywhere: python Tools/audit_asset_books.py [--json]. No art is copied.
"""

from __future__ import annotations

import argparse
from collections import Counter, defaultdict
import hashlib
import json
from pathlib import Path
import re


MODEL_CATEGORIES = {"Building", "Modular Component", "Prop", "Vegetation", "Architecture"}
MODULE_LINK = re.compile(r"\]\(\.\./(Source/[^)]+\.blend)\)")


def hash_file(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for block in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(block)
    return digest.hexdigest()


def inspect_book(root: Path, relative_book: str, library: str) -> list[dict]:
    book = json.loads((root / relative_book).read_text(encoding="utf-8"))
    result = []
    for item in book["assets"]:
        if item.get("category") not in MODEL_CATEGORIES:
            continue
        source = root / item.get("path", "") / item.get("source_file", "")
        source = source.resolve()
        lods = item.get("lods") or []
        companions = []
        if source.suffix.lower() == ".fbx" and source.is_file():
            for level in (1, 2, 3):
                stem = re.sub(r"_LOD0$", "", source.stem, flags=re.IGNORECASE)
                candidate = source.with_name(f"{stem}_LOD{level}.fbx")
                if candidate.is_file():
                    companions.append(str(candidate.relative_to(root.resolve())).replace("\\", "/"))
        result.append({
            "library": library,
            "id": item["id"],
            "category": item["category"],
            "status": item.get("status", ""),
            "source": str(source.relative_to(root.resolve())).replace("\\", "/"),
            "sourceExists": source.is_file(),
            "sourceType": source.suffix.lower(),
            "declaredLods": lods,
            "separateLodFiles": companions,
            "lodPackaging": ("separate" if len(companions) >= 2 else
                             "embedded-declared" if len(lods) >= 3 and source.suffix.lower() == ".fbx" else
                             "not-ready"),
        })
    return result


def audit(legacy: Path, new: Path) -> dict:
    legacy = legacy.resolve()
    new = new.resolve()
    records = inspect_book(legacy, "AssetsDatabase/AssetBook.json", "CozySettlement")
    records += inspect_book(new, "AssetBook/AssetBook.json", "Little-Castle_Assets")
    books = [json.loads((legacy / "AssetsDatabase/AssetBook.json").read_text(encoding="utf-8")),
             json.loads((new / "AssetBook/AssetBook.json").read_text(encoding="utf-8"))]
    locations = defaultdict(list)
    for label, root, book in zip(("CozySettlement", "Little-Castle_Assets"),
                                 (legacy, new), books):
        for item in book["assets"]:
            source = root / item.get("path", "") / item.get("source_file", "")
            digest = hash_file(source) if source.is_file() else None
            locations[item["id"]].append({"library": label, "source": str(source),
                                          "sha256": digest})
    collisions = {key: value for key, value in locations.items() if len(value) > 1}

    house_root = legacy / "Art/House_Cottage_A"
    catalog = (house_root / "Docs/07_Module_Catalog_RU.md").read_text(encoding="utf-8")
    house_modules = []
    legacy_by_source = {r["source"]: r for r in records if r["library"] == "CozySettlement"}
    for relative in MODULE_LINK.findall(catalog):
        path = (house_root / relative).resolve()
        key = str(path.relative_to(legacy)).replace("\\", "/")
        matched = legacy_by_source.get(key)
        house_modules.append({"source": key, "exists": path.is_file(),
                              "assetId": matched["id"] if matched else None})
    current = json.loads((house_root / "CURRENT.json").read_text(encoding="utf-8"))
    current_refs = []
    for field in ("assembly", "house", "yard_kit", "timber_module"):
        if field in current:
            current_refs.append((field, current[field]))
    current_refs += [("yard_module_overrides." + name, path)
                     for name, path in current.get("yard_module_overrides", {}).items()]
    current_models = []
    for field, relative in current_refs:
        path = (house_root / relative).resolve()
        key = str(path.relative_to(legacy)).replace("\\", "/")
        matched = legacy_by_source.get(key)
        current_models.append({"field": field, "source": key, "exists": path.is_file(),
                               "assetId": matched["id"] if matched else None})
    # A filename-family heuristic is intentionally broader than the current
    # catalogue check. It surfaces source studies/history for manual triage;
    # it must not be interpreted as a list of missing game assets.
    known_stems = {Path(item.get("source_file", "")).stem.casefold()
                   for item in books[0]["assets"]}
    unmatched_blends = Counter(path.name for path in (legacy / "Art").rglob("*.blend")
                               if path.stem.casefold() not in known_stems)
    return {"records": records, "duplicateIds": collisions,
            "houseModules": house_modules, "currentModels": current_models,
            "unmatchedBlendNames": dict(sorted(unmatched_blends.items()))}


def markdown(data: dict) -> str:
    rows = data["records"]
    counts = Counter((r["library"], r["lodPackaging"]) for r in rows)
    lines = ["# Asset Book to Unity intake audit", "",
             "Read-only inventory. `Ready` in Asset Book means source authoring status, not Unity approval.", "",
             f"Model records: {len(rows)}; missing source files: {sum(not r['sourceExists'] for r in rows)}.",
             f"House catalogue: {len(data['houseModules'])} linked modules; "
             f"unregistered: {sum(not m['assetId'] for m in data['houseModules'])}; "
             f"missing files: {sum(not m['exists'] for m in data['houseModules'])}.", "",
             f"CURRENT.json active model references: {len(data['currentModels'])}; "
             f"unregistered: {sum(not m['assetId'] for m in data['currentModels'])}; "
             f"missing files: {sum(not m['exists'] for m in data['currentModels'])}.", "",
             "| Library | Separate LOD FBX | Embedded LOD declared | No complete LOD package |",
             "|---|---:|---:|---:|"]
    for library in ("CozySettlement", "Little-Castle_Assets"):
        lines.append(f"| {library} | {counts[library, 'separate']} | "
                     f"{counts[library, 'embedded-declared']} | {counts[library, 'not-ready']} |")
    lines += ["", "Duplicate IDs across books: " +
              (", ".join(sorted(data["duplicateIds"])) or "none") + "."]
    for asset_id, sources in sorted(data["duplicateIds"].items()):
        same = len({source["sha256"] for source in sources}) == 1
        lines.append(f"- `{asset_id}`: {'identical source bytes' if same else 'DIFFERENT OR MISSING source bytes'}")
    lines += ["",
              "## Models requiring LOD/export work", "",
              "| Asset ID | Library | Source | Status |",
              "|---|---|---|---|"]
    for r in rows:
        if r["lodPackaging"] == "not-ready" or not r["sourceExists"]:
            lines.append(f"| {r['id']} | {r['library']} | `{r['source']}` | "
                         f"{'missing file' if not r['sourceExists'] else 'no complete LOD package'} |")
    lines += ["", "## House catalogue gaps", ""]
    gaps = [m for m in data["houseModules"] if not m["assetId"] or not m["exists"]]
    lines.extend(f"- `{m['source']}`: {'missing file' if not m['exists'] else 'not registered'}"
                 for m in gaps)
    if not gaps:
        lines.append("No gaps in the 22 linked module paths.")
    lines += ["", "## CURRENT.json gaps", ""]
    active_gaps = [m for m in data["currentModels"] if not m["assetId"] or not m["exists"]]
    lines.extend(f"- `{m['field']}`: `{m['source']}`: "
                 f"{'missing file' if not m['exists'] else 'not registered'}"
                 for m in active_gaps)
    if not active_gaps:
        lines.append("No gaps in active model references.")
    lines += ["", "## Other Blender source families to review", "",
              "These filenames do not match a model record's primary source stem. "
              "They may be history, studies or editable sources behind FBX exports; "
              "this heuristic does not call them lost assets.", ""]
    lines.extend(f"- `{name}` ({count} file(s))" for name, count in
                 data["unmatchedBlendNames"].items())
    lines += ["", "## Intake gate", "",
              "Only versioned, reviewed FBX/texture release packages enter Unity. "
              "An FBX plus declared LODs is a candidate, not an approved production prefab. "
              "Validate geometry, materials, scale, pivot, colliders and LOD transitions in Unity; "
              "run foliage-specific and wall-specific checks before connecting runtime catalogs. "
              "Keep `assetId` stable so a newer release replaces the previous local copy.", ""]
    return "\n".join(lines)


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    base = Path(__file__).resolve().parents[2]
    parser.add_argument("--legacy", type=Path, default=base / "CozySettlement")
    parser.add_argument("--new", type=Path, default=base / "Little-Castle_Assets")
    parser.add_argument("--json", action="store_true")
    args = parser.parse_args()
    data = audit(args.legacy, args.new)
    print(json.dumps(data, ensure_ascii=False, indent=2) if args.json else markdown(data))


if __name__ == "__main__":
    main()
