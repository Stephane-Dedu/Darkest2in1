"""Package approved v05 scenery and visible masks as a private runtime pack (never put output in Git)."""
import argparse
import json
import shutil
from pathlib import Path
from PIL import Image, ImageChops, ImageFilter

BUILDINGS = {
    "stagecoach": "stage_coach", "survivalist": "camping_trainer",
    **{tag: tag for tag in ("abbey", "tavern", "sanitarium", "guild", "blacksmith", "nomad_wagon", "graveyard", "statue")},
}


def prepare(source, destination):
    source, destination = Path(source).resolve(), Path(destination).resolve()
    repo = Path(__file__).resolve().parents[1]
    if destination == repo or repo in destination.parents:
        raise ValueError("Private derived game artwork must stay outside the Git repository.")
    plate = source / "hamlet-hillside-hybrid-background-v05.png"
    with Image.open(plate) as image:
        if image.size != (1920, 1080):
            raise ValueError("Expected a clean 1920x1080 scene.")
    render = source / "hamlet-hillside-render-v05"
    anchors = json.loads((render / "final-anchors.json").read_text())
    destination.mkdir(parents=True, exist_ok=True)
    shutil.copy2(plate, destination / "scene.png")
    manifest = dict(version=1, width=1920, height=1080, background="scene.png", buildings=[])
    for tag, building_id in BUILDINGS.items():
        mask = Image.open(render / "layers" / f"{tag}-visible-mask.png").convert("L")
        if mask.size != (1920, 1080):
            raise ValueError(f"Invalid mask size: {tag}")
        # Keep outline padding inside each cropped texture. Alpha is top-down, one byte per pixel.
        box = mask.getbbox()
        if not box:
            raise ValueError(f"No visible pixels: {tag}")
        x, y, right, bottom = box
        box = (max(0, x - 4), max(0, y - 4), min(1920, right + 4), min(1080, bottom + 4))
        alpha = mask.crop(box)
        (destination / f"{building_id}.mask").write_bytes(alpha.tobytes())
        tint = Image.new("RGBA", alpha.size, (255, 255, 255, 0))
        tint.putalpha(alpha)
        tint.save(destination / f"{building_id}-tint.png")
        solid = alpha.point(lambda value: 255 if value >= 48 else 0)
        edge = ImageChops.subtract(solid.filter(ImageFilter.MaxFilter(7)), solid)
        # Clip feedback to visible territory, so a rear roof cannot paint over a foreground building.
        edge = ImageChops.multiply(edge, mask.filter(ImageFilter.MaxFilter(3)).crop(box))
        outline = Image.new("RGBA", alpha.size, (255, 255, 255, 0))
        outline.putalpha(edge)
        outline.save(destination / f"{building_id}-outline.png")
        manifest["buildings"].append(dict(id=building_id, x=box[0], y=box[1], width=alpha.width, height=alpha.height,
            order=anchors[tag]["draw_order"], mask=f"{building_id}.mask", tint=f"{building_id}-tint.png", outline=f"{building_id}-outline.png"))
    # Publish metadata last: the loader rejects a partial pack instead of leaving invisible hotspots.
    (destination / "manifest.json").write_text(json.dumps(manifest, indent=2) + "\n", encoding="utf-8")
    print(f"Prepared {len(BUILDINGS)} services in {destination}")


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("source", type=Path)
    parser.add_argument("destination", type=Path)
    args = parser.parse_args()
    prepare(args.source, args.destination)
