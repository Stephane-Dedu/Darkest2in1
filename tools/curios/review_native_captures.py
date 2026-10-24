"""Index/crop real unsaved curio QA captures. This does not accept art quality."""
import argparse
import hashlib
import json
from pathlib import Path

from PIL import Image, ImageDraw


def review(pack, frames, output):
    pack, frames, output = pack.resolve(), frames.resolve(), output.resolve()
    if output.is_relative_to(Path(__file__).resolve().parents[2]) or output.exists():
        raise ValueError('Use a new private output folder outside the repository')
    document = json.loads((pack / 'manifest.json').read_text(encoding='utf-8-sig'))
    identities = [entry['Id'] for entry in document['Entries']]
    canonical = [art['Id'] for art in document['Artifacts']]
    records = []
    for key in identities:
        if not key or any(c not in 'abcdefghijklmnopqrstuvwxyz0123456789_' for c in key):
            raise ValueError('Unsafe capture identity')
        for state in ('closed', 'used'):
            path = frames / f'{key}-{state}.png'
            with Image.open(path) as frame:
                if frame.size != (1920, 1080):
                    raise ValueError('Native capture must use the actual 1920x1080 game canvas')
            records.append({'id': key, 'state': state, 'file': str(path),
                            'sha256': hashlib.sha256(path.read_bytes()).hexdigest()})
    output.mkdir(parents=True)
    for page in range((len(canonical) + 3) // 4):
        sheet = Image.new('RGB', (1120, 2080), (22, 23, 25))
        draw = ImageDraw.Draw(sheet)
        for row, key in enumerate(canonical[page * 4:page * 4 + 4]):
            for col, state in enumerate(('closed', 'used')):
                with Image.open(frames / f'{key}-{state}.png') as frame:
                    # Exact camera pixels around PropX1040/feet690, no repaint,
                    # resizing, replacement background or sprite compositing.
                    sheet.paste(frame.crop((790, 250, 1290, 720)).convert('RGB'), (col * 560 + 30, row * 520 + 32))
                draw.text((col * 560 + 30, row * 520 + 10), f'{key} / {state} / native capture', fill=(224, 216, 197))
        sheet.save(output / f'pairs-{page + 1:02}.jpg', quality=93)
    report = {'purpose': 'Real native screenshots and exact camera crops. Visual acceptance is recorded separately.',
              'unique_pairs': len(canonical), 'identities': len(identities), 'captures': records}
    (output / 'evidence.json').write_text(json.dumps(report, indent=2), encoding='utf-8')
    print(json.dumps({'pairs': len(canonical), 'identities': len(identities), 'captures': len(records)}))


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--pack', type=Path, required=True)
    parser.add_argument('--frames', type=Path, required=True)
    parser.add_argument('--out', type=Path, required=True)
    args = parser.parse_args()
    review(args.pack, args.frames, args.out)
