"""Review private corridor plates at the runtime's bounded crop, without launching a game.

python tools/review_corridor_depth.py <before-pack> <depth-pack> <private-review> [--video]
"""
import argparse
import json
import math
import shutil
import subprocess
from pathlib import Path

from PIL import Image, ImageDraw, ImageFont

from compose_corridor_depth import BANDS, TRAVEL

NAMES = {"city": "Sprawl", "farm": "Foetor", "forest": "Tangle", "coast": "Shroud"}


def crop(image, progress, size=(1920,720)):
    """Pixel equivalent of CorridorPanoramaLayout.Crop, including the y680 footline."""
    w, h = image.size
    scale = max(1920 / w, 720 / h) * 1.08
    left = (w * scale - 1920) * max(0, min(1, progress)) / scale
    top = (680 / 720 * h * scale - 680) / scale
    result = image.transform(size, Image.Transform.EXTENT,
                             (left, top, left + 1920 / scale, top + 720 / scale), Image.Resampling.BILINEAR)
    return result.convert("RGBA") if result.mode == "RGBa" else result


def layers(folder, scene):
    # Premultiply once rather than reprocessing each full 4096px source on every
    # motion frame. Convert only the small crop back to straight-alpha RGBA.
    return [Image.open(folder / (scene.removesuffix(".png") + f".depth-{band}.png")).convert("RGBa") for band in BANDS]


def frame(images, progress, size=(1920,720)):
    result = Image.new("RGBA", size)
    for image, travel in zip(images, TRAVEL):
        result.alpha_composite(crop(image, .5 + (max(0, min(1, progress)) - .5) * travel, size))
    return result.convert("RGB")


def review(before, after, output, video=False):
    before, after, output = [Path(p).resolve() for p in (before, after, output)]
    repo = Path(__file__).resolve().parents[1]
    if output == repo or repo in output.parents:
        raise ValueError("Private game art must stay outside Git")
    output.mkdir(parents=True, exist_ok=True)
    manifest = json.loads((after / "native-corridors-manifest.json").read_text())
    font_path = Path("C:/Windows/Fonts/segoeui.ttf")
    font = ImageFont.truetype(str(font_path), 24) if font_path.exists() else ImageFont.load_default()
    entries = {entry["short"]: entry for entry in manifest["files"]}
    comparison = Image.new("RGB", (1920, 4 * 408), (12, 15, 19))
    all_rows = []
    for row, (region, name) in enumerate(NAMES.items()):
        short = region + "-01"
        if short not in entries:
            continue
        file = entries[short]["file"]
        original = Image.open(before / file).convert("RGB")
        depths = layers(after, file)
        old_view, new_view = crop(original, .5).convert("RGB"), frame(depths, .5)
        new_view.save(output / f"{region}-preview.png")
        comparison.paste(old_view.resize((960, 360), Image.Resampling.LANCZOS), (0, row*408+48))
        comparison.paste(new_view.resize((960, 360), Image.Resampling.LANCZOS), (960, row*408+48))
        draw = ImageDraw.Draw(comparison)
        draw.text((16, row*408+8), name + " / before", fill=(202, 194, 176), font=font)
        draw.text((976, row*408+8), name + " / depth pass, offline preview", fill=(202, 194, 176), font=font)
        all_rows.append((name, original, depths))
    comparison.save(output / "comparison.jpg", quality=95)
    # Every variant at both hall ends and the centre exposes gaps and excessive
    # foreground coverage that a single carefully chosen crop would hide.
    atlas = Image.new("RGB", (1440, len(entries)*212), (12,15,19))
    for row, entry in enumerate(entries.values()):
        depths = layers(after,entry["file"])
        draw = ImageDraw.Draw(atlas)
        draw.text((8,row*212+2),entry["short"],fill=(202,194,176))
        for col,progress in enumerate((0,.5,1)):
            atlas.paste(frame(depths,progress).resize((480,180),Image.Resampling.LANCZOS),(col*480,row*212+28))
    atlas.save(output / "all-variants.jpg", quality=92)
    for region in NAMES:
        indices = [i for i,entry in enumerate(entries.values()) if entry["short"].startswith(region+"-")]
        if indices:
            atlas.crop((0,indices[0]*212,1440,(indices[-1]+1)*212)).save(output/f"{region}-all-variants.jpg",quality=93)
    if video:
        ffmpeg = shutil.which("ffmpeg")
        if ffmpeg is None:
            raise RuntimeError("ffmpeg is required for the optional motion comparison")
        with (output / "motion-encode.log").open("w") as log:
            process = subprocess.Popen([ffmpeg,"-y","-f","rawvideo","-pix_fmt","rgb24","-s","1600x348",
                "-r","24","-i","pipe:0","-an","-c:v","libx264","-crf","18","-pix_fmt","yuv420p",
                "-movflags","+faststart",str(output/"motion-comparison.mp4")],stdin=subprocess.PIPE,stderr=log)
            for name, original, depths in all_rows:
                for step in range(144):
                    progress = .5 - .5 * math.cos(2*math.pi*step/143)
                    canvas = Image.new("RGB",(1600,348),(12,15,19))
                    canvas.paste(crop(original,progress,(800,300)).convert("RGB"),(0,48))
                    canvas.paste(frame(depths,progress,(800,300)),(800,48))
                    draw=ImageDraw.Draw(canvas)
                    draw.text((12,8),name+" / before",font=font,fill=(202,194,176))
                    draw.text((812,8),"native paint / three scenery depths",font=font,fill=(202,194,176))
                    process.stdin.write(canvas.tobytes())
            process.stdin.close()
            if process.wait() != 0:
                raise RuntimeError("Motion encoding failed; see motion-encode.log")
    print("REVIEW",output,flush=True)


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("before",type=Path)
    parser.add_argument("after",type=Path)
    parser.add_argument("output",type=Path)
    parser.add_argument("--video",action="store_true")
    args=parser.parse_args()
    review(args.before,args.after,args.output,args.video)
