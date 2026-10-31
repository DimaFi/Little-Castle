"""Copy selected local texture sets into the canonical Unity project, preserving originals."""
from pathlib import Path
import shutil
import json
import argparse


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("source", type=Path)
    args = parser.parse_args()
    project = Path(__file__).resolve().parents[1]
    target = project / "Assets/_Game/Art/PathSurfaces/Sources"
    groups = [(f"Cobble{i}", args.source / "05_Строительные_материалы/Брусчатка — Cobble Stone" / str(i), i) for i in range(1, 8)]
    groups += [(f"Ground{i}", args.source / "01_Грунты_и_рельеф/Грунт — Ground" / str(i), i) for i in (2, 5)]
    manifest = []
    for name, source, number in groups:
        destination = target / name
        destination.mkdir(parents=True, exist_ok=True)
        for suffix in ("diffuseOriginal", "normal", "ao", "height", "smoothness"):
            original = source / f"{number}_{suffix}.png"
            copied = destination / f"{name}_{suffix}.png"
            if not copied.exists() or original.read_bytes() != copied.read_bytes():
                shutil.copy2(original, copied)
            manifest.append({"source": str(original), "imported": copied.relative_to(project).as_posix()})
    (target.parent / "source-manifest.json").write_text(json.dumps(manifest, ensure_ascii=False, indent=2), encoding="utf-8")
    print(f"Copied/verified {len(manifest)} textures in {project}")


if __name__ == "__main__":
    main()
