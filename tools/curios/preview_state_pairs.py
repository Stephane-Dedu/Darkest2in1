"""Review fitted curio pairs with the crawl renderer's actual scale and padding.

This makes an offline contact sheet, never a game capture or installation. The
background is an existing private corridor painting, fitted to1920x720 only.
"""
import argparse
import hashlib
import json
from pathlib import Path

from PIL import Image, ImageDraw


def preview(background, pairs, output):
    background, output = background.resolve(), output.resolve()
    if output.is_relative_to(Path(__file__).resolve().parents[2]):
        raise ValueError('Private review sheets must stay outside the repository')
    if output.exists():
        raise ValueError('Use a new output file')
    if not 1 <= len(pairs) <= 5:
        raise ValueError('Review one to five pairs per sheet')
    view = Image.open(background).convert('RGBA').resize((1920, 720), Image.Resampling.LANCZOS)
    sheet = Image.new('RGBA', (1920, 1440))
    records = []
    for row, state in enumerate(('closed', 'used')):
        sheet.alpha_composite(view, (0, row * 720))
        draw = ImageDraw.Draw(sheet)
        draw.rectangle((0, row * 720, 1919, row * 720 + 28), fill=(12, 12, 12, 230))
        draw.text((14, row * 720 + 7), 'OFFLINE / ' + state + ' / native draw scale, footline690', fill='white')
        for index, pair in enumerate(pairs):
            metadata = json.loads((pair / 'provenance.json').read_text())
            path = pair / (metadata['id'] + '-' + state + '.png')
            source = Image.open(path).convert('RGBA')
            if list(source.size) != metadata['size'] or metadata['padding'] != 16:
                raise ValueError('Sprite does not match registered pair metadata')
            width = metadata['proposed_runtime_width']
            height = width * source.height / source.width
            if height > 430:
                width *= 430 / height
                height = 430
            pad = 16 * width / source.width
            x = 1920 * (index + .5) / len(pairs)
            y = row * 720 + 690 - height + pad
            resized = source.resize((round(width), round(height)), Image.Resampling.LANCZOS)
            sheet.alpha_composite(resized, (round(x - width / 2), round(y)))
            draw.text((round(x - width / 2), row * 720 + 696), metadata['id'], fill='white')
            if row == 0:
                records.append({'id': metadata['id'], 'pair': str(pair), 'width': width, 'height': height,
                                'foot_y': y + height - pad, 'native_verified': False})
    output.parent.mkdir(parents=True, exist_ok=True)
    sheet.convert('RGB').save(output, quality=94)
    metadata = {'purpose': 'Offline review, not a new game capture', 'background': str(background),
                'background_sha256': hashlib.sha256(background.read_bytes()).hexdigest(), 'entries': records}
    output.with_suffix('.json').write_text(json.dumps(metadata, indent=2), encoding='utf-8')
    print(json.dumps(metadata, indent=2))


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--background', type=Path, required=True)
    parser.add_argument('--pairs', type=Path, nargs='+', required=True)
    parser.add_argument('--out', type=Path, required=True)
    args = parser.parse_args()
    preview(args.background, args.pairs, args.out)
