"""Unpack the owner's DD1 Shieldbreaker art for private model texturing."""
import argparse
import json
import struct
from pathlib import Path


def unpack(root, output):
    from PIL import Image, ImageDraw
    output.mkdir(parents=True, exist_ok=True)
    page = Image.open(root / 'shieldbreaker_A/anim/shieldbreaker.sprite.combat.png').convert('RGBA')
    (output / 'combat.rgba').write_bytes(struct.pack('<II', *page.size) + page.tobytes())
    lines = (root / 'anim/shieldbreaker.sprite.combat.atlas').read_text().splitlines()
    regions = {}
    current = None
    for line in lines:
        if line and not line.startswith(' ') and ':' not in line and not line.endswith('.png'):
            current = line
            regions[current] = {}
        elif current and ':' in line and line.startswith(' '):
            key, value = line.strip().split(':', 1)
            regions[current][key] = value.strip()
    for name, region in regions.items():
        x, y = map(int, region['xy'].split(','))
        w, h = map(int, region['size'].split(','))
        rotated = region['rotate'] == 'true'
        crop = page.crop((x, y, x + (h if rotated else w), y + (w if rotated else h)))
        if rotated:
            crop = crop.transpose(Image.Transpose.ROTATE_270)
        crop.save(output / (name + '.png'))
    helmet = Image.open(output / 'helmet.png')
    helmet.crop((0, helmet.height - 47, helmet.width, helmet.height)).save(output / 'turban.png')
    (output / 'regions.json').write_text(json.dumps(regions, indent=2))
    sheet = Image.new('RGB', (1000, ((len(regions) + 4) // 5) * 240), (75, 70, 62))
    draw = ImageDraw.Draw(sheet)
    for i, name in enumerate(regions):
        pic = Image.open(output / (name + '.png'))
        pic.thumbnail((190, 210))
        x, y = i % 5 * 200, i // 5 * 240
        sheet.paste(pic, (x + (200 - pic.width) // 2, y + 25), pic)
        draw.text((x + 8, y + 7), name, fill='white')
    sheet.save(output / 'parts.png')


if __name__ == '__main__':
    p = argparse.ArgumentParser(description=__doc__)
    p.add_argument('--root', type=Path, required=True)
    p.add_argument('--output', type=Path, required=True)
    a = p.parse_args()
    repo = Path(__file__).resolve().parents[2]
    if a.output.resolve() == repo or repo in a.output.resolve().parents:
        p.error('DD1 art must stay outside the repository')
    unpack(a.root, a.output)
