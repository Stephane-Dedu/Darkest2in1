"""Check a native cold-Hamlet capture: visible torch inside an opaque loading page.

Run against a private window PNG captured while town models are still loading. This is a native-render
regression for the Canvas/IMGUI layering bug, not a substitute for the service/lifecycle fixture.
"""
import argparse
import json
from pathlib import Path

from PIL import Image


def inspect(path):
    image = Image.open(path).convert('RGB')
    width, height = image.size
    scale = min(width / 1920, height / 1080)
    offset_x, offset_y = (width - 1920 * scale) / 2, (height - 1080 * scale) / 2

    def pixels(rect):
        x1, y1, x2, y2 = rect
        box = (round(offset_x + x1 * scale), round(offset_y + y1 * scale),
               round(offset_x + x2 * scale), round(offset_y + y2 * scale))
        return list(image.crop(box).get_flattened_data())

    torch = pixels((708, 215, 1212, 680))
    margin = pixels((0, 0, 600, 700))
    bright = sum(max(p) > 45 for p in torch)
    dark_fraction = sum(max(p) <= 45 for p in margin) / len(margin)
    return {'torch_bright_pixels': bright, 'opaque_margin_fraction': dark_fraction,
            'pass': bright > 200 and dark_fraction > .98}


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('capture', type=Path)
    args = parser.parse_args()
    result = inspect(args.capture)
    print(json.dumps(result, indent=2))
    raise SystemExit(0 if result['pass'] else 1)
