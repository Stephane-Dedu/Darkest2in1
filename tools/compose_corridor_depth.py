"""Compose private native DD2 passes into DD1-style scenery depths.

python tools/compose_corridor_depth.py <round240-corridors> <private-output>
The canonical PNG remains a complete fallback. Three optional RGBA companions
use the same camera and paint, with slower distant movement in the runtime.
No game files or derived images belong in Git.
"""
import argparse
import json
from pathlib import Path

import numpy as np
from PIL import Image

from render_native_rooms import PALETTES, digest

SIZE = (4096, 768)
BANDS = ("back", "middle", "front")
TRAVEL = (.18, .48, 1.0)


def save(image, target):
    image.save(target)
    if target.stat().st_size > 16 * 1024 * 1024:
        raise ValueError(f"Depth image exceeds runtime's 16 MiB PNG limit: {target}")


def smooth(value):
    value = np.clip(value, 0, 1)
    return value * value * (3 - 2 * value)


def prepare_layer(path, band, region):
    image = Image.open(path).convert("RGBA")
    if image.size != SIZE:
        raise ValueError(f"Unexpected native pass dimensions: {path}")
    pixels = np.array(image, dtype=np.float32)
    height = SIZE[1]
    y = np.arange(height)[:, None]
    haze = np.array(PALETTES[region][3], dtype=np.float32)
    fog = {"sky": .46, "vfar": .56, "far": .30, "wall": .035, "road": 0, "ground": .18}[band]
    if band == "ground":
        rows = np.flatnonzero(pixels[..., 3].max(axis=1) > 0)
        if len(rows):
            # A deep 160px fade put blue haze beneath nearby roots and palisades,
            # making them hover. Keep mist at the horizon; restore solid painted
            # ground before the near scenery's contact line.
            pixels[..., 3] *= smooth((y - rows[0]) / 56)
        # Preserve the native ink and hue, keeping the walking lane quieter than
        # the landmarks and heroes. No replacement texture or painted overlay.
        foreground = smooth((y - 540) / 220)[..., None]
        pixels[..., :3] *= 1 - .20 * foreground
        fog = fog * np.clip((height - y) / (height * .65), 0, 1)[..., None]
    if band == "sky":
        opaque = pixels[..., 3] > 240
        bottoms = np.max(np.where(opaque, y, -1), axis=0)
        pixels[..., 3] *= smooth((bottoms[None, :] - y) / 140)
    pixels[..., :3] = pixels[..., :3] * (1 - fog) + haze * fog
    return Image.fromarray(pixels.clip(0, 255).astype(np.uint8))


def compose(source, output, only=None):
    source, output = Path(source).resolve(), Path(output).resolve()
    repo = Path(__file__).resolve().parents[1]
    if output == repo or repo in output.parents:
        raise ValueError("Private game art must stay outside Git")
    if output == source:
        raise ValueError("Retain the source render pack for comparison and rollback")
    manifest = json.loads((source / "render-manifest.json").read_text())
    output.mkdir(parents=True, exist_ok=True)
    entries = []
    for entry in manifest["files"]:
        if only and entry["short"] not in only:
            continue
        region = entry["region"].removeprefix("dd2_")
        _, top, bottom, _ = PALETTES[region]
        gradient = np.linspace(0, 1, SIZE[1])[:, None, None] ** .8
        background = Image.fromarray((np.array(top) * (1-gradient) + np.array(bottom) * gradient)
                                    .astype(np.uint8).repeat(SIZE[0], 1)).convert("RGBA")
        middle = Image.new("RGBA", SIZE)
        front = Image.new("RGBA", SIZE)
        # The near ground softens the skirt foundations, as DD1's painted floor
        # does. Its alpha horizon leaves the skyline and distant trunks intact.
        for target, bands in ((background, ("sky", "vfar")), (middle, ("far",)),
                              (front, ("ground", "wall", "road"))):
            for band in bands:
                path = source / "passes" / f"{entry['short']}-{band}.png"
                if path.is_file():
                    target.alpha_composite(prepare_layer(path, band, region))
        record = dict(entry)
        record["source_pass_sha256"] = {path.name: digest(path) for path in sorted(
            (source / "passes").glob(f"{entry['short']}-*.png"))}
        record["layers"] = []
        for band, travel, image in zip(BANDS, TRAVEL, (background, middle, front)):
            name = entry["file"].removesuffix(".png") + f".depth-{band}.png"
            target = output / name
            save(image, target)
            record["layers"].append({"file": name, "sha256": digest(target), "travel": travel})
        background.alpha_composite(middle)
        background.alpha_composite(front)
        target = output / entry["file"]
        save(background.convert("RGB"), target)
        record["sha256"] = digest(target)
        record["source_sha256"] = digest(source / entry["file"])
        entries.append(record)
        print("COMPOSED", entry["file"], flush=True)
    result = dict(manifest, version=5, files=entries, generated_art=False,
                  composition_tool_sha256=digest(Path(__file__)), source_passes=str(source),
                  depth_order=list(BANDS), depth_travel=list(TRAVEL),
                  limits=["Painted 2.5D depth behind actors; no foreground actor occlusion",
                          "Native albedo and approximate haze; no deferred lighting reconstruction"])
    (output / "native-corridors-manifest.json").write_text(json.dumps(result, indent=2)+"\n", encoding="utf-8")
    return result


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("source", type=Path)
    parser.add_argument("output", type=Path)
    parser.add_argument("--only", nargs="*")
    args = parser.parse_args()
    compose(args.source, args.output, args.only)
