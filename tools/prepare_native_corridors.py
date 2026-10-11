"""Fit the private DD2 road renders to the live crawl's footline. Output stays outside Git."""
import argparse
import hashlib
import json
from pathlib import Path
from PIL import Image, ImageStat

REGIONS = {"city": ("Sprawl", 1.0), "farm": ("Foetor", 1.08), "forest": ("Tangle", 1.15), "coast": ("Shroud", 1.0)}
SIZE = (2880, 1080)
FOOT_Y = 680 / 720 * SIZE[1]


def digest(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def fit(image, zoom):
    """Uniform scale, centred horizontally and anchored at the top; crop excess below the walking lane."""
    if zoom == 1:
        return image.copy()
    enlarged = image.resize((round(image.width * zoom), round(image.height * zoom)), Image.Resampling.LANCZOS)
    left = (enlarged.width - SIZE[0]) // 2
    return enlarged.crop((left, 0, left + SIZE[0], SIZE[1]))


def prepare(kit, output):
    kit, output = Path(kit).resolve(), Path(output).resolve()
    repo = Path(__file__).resolve().parents[1]
    if output == repo or repo in output.parents:
        raise ValueError("Private game artwork must stay outside Git.")
    source = kit / "corridor-renders"
    images = []
    # Validate the complete source set before publishing any metadata or touching an installed file.
    for region, (name, zoom) in REGIONS.items():
        for variant in range(1, 7):
            path = source / f"dd2_{region}-corridor-native-{variant:02}.png"
            with Image.open(path) as image:
                if image.size != SIZE or image.mode not in ("RGB", "RGBA") or path.stat().st_size > 16 * 1024 * 1024:
                    raise ValueError(f"Invalid panorama: {path}")
                image.load()
            images.append((region, name, zoom, variant, path))
    output.mkdir(parents=True, exist_ok=True)
    manifest = {"version": 1, "origin": "DD2 installed road/decor/skirt meshes and textures; offline render",
        "generated_art": False, "virtual_scene": [1920, 720], "hero_foot_y": 680,
        "source_kit": str(kit), "files": [],
        "limits": ["Flat lighting and baked haze; no native animated VFX", "Hard distant ground edges",
            "Embedded lane posts cannot occlude characters in a single background plate", "Tangle retains very dark ink/shadows"]}
    for region, name, zoom, variant, path in images:
        with Image.open(path) as source_image:
            result = fit(source_image.convert("RGB"), zoom)
        destination = output / f"dd2_{region}-corridor-{variant:02}.png"
        result.save(destination)
        foot = result.crop((0, round(FOOT_Y) - 15, result.width, round(FOOT_Y) + 15)).convert("L")
        manifest["files"].append({"file": destination.name, "region": f"dd2_{region}", "name": name,
            "variant": variant, "source": str(path), "source_sha256": digest(path), "sha256": digest(destination),
            "size": list(SIZE), "uniform_zoom": zoom, "source_foot_y": FOOT_Y / zoom,
            "foot_band_mean_luma": round(ImageStat.Stat(foot).mean[0], 2)})
    (output / "corridors-manifest.json").write_text(json.dumps(manifest, indent=2) + "\n", encoding="utf-8")
    print(f"Prepared {len(images)} native corridors in {output}")
    for region, (name, zoom) in REGIONS.items():
        samples = [entry["foot_band_mean_luma"] for entry in manifest["files"] if entry["region"] == f"dd2_{region}"]
        print(f"{name}: 6 variants; zoom {zoom:.2f}; foot-band mean luma {min(samples):.1f}–{max(samples):.1f}")


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("kit", type=Path)
    parser.add_argument("output", type=Path)
    args = parser.parse_args()
    prepare(args.kit, args.output)
