"""Validate actual Unity captures and assemble a labelled review sheet (no retouching)."""
from __future__ import annotations

import argparse
import json
from pathlib import Path

import numpy as np
from PIL import Image, ImageDraw, ImageFont


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument("--evidence", type=Path, required=True)
    args = parser.parse_args()
    root = args.evidence.resolve()
    folders = {"Gamma": root / "Gamma" / "Captures", "Linear": root / "Final" / "Captures"}
    metrics = []
    for color_space, folder in folders.items():
        captures = sorted(folder.glob("*.png"))
        if len(captures) != 96:
            raise RuntimeError(f"{folder}: expected 96 actual camera images, got {len(captures)}")
        for path in captures:
            with Image.open(path) as picture:
                if picture.size != (1920, 1080):
                    raise RuntimeError(f"Unexpected dimensions: {path}: {picture.size}")
            metadata = json.loads(path.with_suffix(".json").read_text(encoding="utf-8-sig"))
            if metadata["colorSpace"] != color_space or metadata["dof"]:
                raise RuntimeError(f"Unexpected capture state: {path}")
        for hdr_path in sorted(folder.glob("hdr-*.png")):
            ao_path = folder / hdr_path.name.replace("hdr-", "ao-", 1)
            hdr = np.asarray(Image.open(hdr_path).convert("RGB"), dtype=np.int16)
            ao = np.asarray(Image.open(ao_path).convert("RGB"), dtype=np.int16)
            difference = ao - hdr
            metrics.append({
                "space": color_space,
                "view": hdr_path.stem[4:],
                "mean_absolute_rgb_0_255": float(np.abs(difference).mean()),
                "changed_pixel_fraction": float(np.any(difference != 0, axis=2).mean()),
                "mean_brightness_change_0_255": float(difference.mean()),
            })
        foundation = next(item for item in metrics if item["space"] == color_space and item["view"] == "foundation-12.0")
        if foundation["mean_absolute_rgb_0_255"] < .05:
            raise RuntimeError(f"AO produces no meaningful image contribution: {color_space}")

    rows = [("foundation", "12.0", "Полдень — фундамент и арка"),
            ("village", "18.5", "Вечер — общий вид"),
            ("village", "01.0", "Ночь — читаемость окружения")]
    width, image_height, heading, label_height = 960, 540, 78, 46
    sheet = Image.new("RGB", (width * 2, heading + len(rows) * (image_height + label_height)), "#141b23")
    draw = ImageDraw.Draw(sheet)
    font_path = Path("C:/Windows/Fonts/segoeui.ttf")
    title_font = ImageFont.truetype(str(font_path), 29)
    label_font = ImageFont.truetype(str(font_path), 24)
    draw.text((22, 14), "Gamma — исходный свет", font=title_font, fill="white")
    draw.text((width + 22, 14), "Linear + HDR + AO + новая палитра", font=title_font, fill="white")
    for index, (view, hour, title) in enumerate(rows):
        y = heading + index * (image_height + label_height)
        draw.text((22, y + 7), title, font=label_font, fill="#bdcbd9")
        for column, (space, mode) in enumerate((("Gamma", "baseline"), ("Linear", "ao"))):
            with Image.open(folders[space] / f"{mode}-{view}-{hour}.png") as picture:
                sheet.paste(picture.resize((width, image_height), Image.Resampling.LANCZOS),
                            (column * width, y + label_height))
    sheet.save(root / "lighting-comparison.png")
    result = {"status": "PASS", "images_checked": 192, "ao_pairs_checked": len(metrics),
              "limits": "Image differences prove rendering contribution, not artistic acceptance or frame rate.",
              "metrics": metrics}
    (root / "capture-validation.json").write_text(json.dumps(result, ensure_ascii=False, indent=2), encoding="utf-8")
    print(json.dumps({key: value for key, value in result.items() if key != "metrics"}, ensure_ascii=False))


if __name__ == "__main__":
    main()
