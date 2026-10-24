"""Extract DD1's painted curio states as private identity/pose references.

This reads the independently audited inventory. It never installs images or
changes encounters. Complex/missing states are reported instead of invented.
"""
import argparse
import hashlib
import json
from pathlib import Path

from PIL import Image


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def atlas_regions(path):
    page = None
    current = None
    result = {}
    for raw in path.read_text(encoding='utf-8-sig').splitlines():
        line = raw.strip()
        if not line:
            page = current = None
        elif page is None:
            page = path.parent / line
        elif ':' not in line:
            current = {'page': page, 'rotate': False}
            result[line] = current
        elif current is not None:
            key, value = line.split(':', 1)
            if key == 'rotate':
                current[key] = value.strip() == 'true'
            elif key in ('xy', 'size', 'orig', 'offset'):
                current[key] = tuple(int(x.strip()) for x in value.split(','))
    return result


def state_image(region):
    x, y = region['xy']
    width, height = region['size']
    with Image.open(region['page']) as loaded:
        page = loaded.convert('RGBA')
    box = (x, y, x + (height if region['rotate'] else width), y + (width if region['rotate'] else height))
    if box[0] < 0 or box[1] < 0 or box[2] > page.width or box[3] > page.height:
        raise ValueError('Atlas region outside source PNG')
    im = page.crop(box)
    if region['rotate']:
        im = im.transpose(Image.Transpose.ROTATE_270)
    if im.size != (width, height):
        raise ValueError('Unpacked dimensions differ from atlas')
    original = region.get('orig', (width, height))
    dx, dy = region.get('offset', (0, 0))
    canvas = Image.new('RGBA', original)
    canvas.paste(im, (dx, original[1] - dy - height))
    return canvas


def extract(game, inventory, output):
    game, inventory, output = game.resolve(), inventory.resolve(), output.resolve()
    if output.is_relative_to(Path(__file__).resolve().parents[2]) or output.is_relative_to(game):
        raise ValueError('Private reference art must stay outside the repo and game')
    if output.exists() and any(output.iterdir()):
        raise ValueError('Use a new output directory')
    entries = json.loads(inventory.read_text(encoding='utf-8-sig'))
    roots = [game / 'props/shared/curios']
    roots += [p for p in (game / 'dlc').rglob('curios') if p.is_dir() and p.parent.name == 'shared']
    folders = {}
    for parent in roots:
        if parent.is_dir():
            for folder in parent.iterdir():
                if folder.is_dir():
                    folders.setdefault(folder.name, []).append(folder)
    output.mkdir(parents=True, exist_ok=True)
    records = []
    for entry in entries:
        key = entry['id']
        if not key or any(c not in 'abcdefghijklmnopqrstuvwxyz0123456789_' for c in key):
            raise ValueError('Unsafe inventory identity')
        record = {'id': key, 'scope': entry['scope'], 'region': entry['region'], 'states': {}}
        if entry['scope'] == 'owner-exception':
            record['status'] = 'Existing owner-specific presentation retained; no replacement reference inferred.'
            records.append(record)
            continue
        sprite = entry.get('sprite', key)
        candidates = [p for p in folders.get(sprite, []) if list(p.glob('*.atlas'))]
        if len(candidates) != 1:
            record['status'] = 'Missing or ambiguous sprite folder'
            record['sprite'] = sprite
            records.append(record)
            continue
        folder = candidates[0]
        atlases = list(folder.glob('*.atlas'))
        if len(atlases) != 1:
            record['status'] = 'Multiple atlases require setup-pose inspection'
            records.append(record)
            continue
        atlas = atlases[0]
        regions = atlas_regions(atlas)
        record.update({'atlas': str(atlas), 'atlas_sha256': sha(atlas), 'sprite': sprite})
        for target, name in (('closed', 'closed'), ('used', 'open')):
            if name not in regions:
                record['states'][target] = {'status': 'No independent whole-state atlas region'}
                continue
            region = regions[name]
            image = state_image(region)
            path = output / key / (target + '.png')
            path.parent.mkdir(exist_ok=True)
            image.save(path)
            record['states'][target] = {'file': path.relative_to(output).as_posix(), 'size': list(image.size),
                                        'page_sha256': sha(region['page']), 'png_sha256': sha(path)}
        record['status'] = 'Direct atlas state references; not a complete skeleton render'
        records.append(record)
    manifest = {'inventory_sha256': sha(inventory), 'recipe_sha256': sha(Path(__file__)),
                'purpose': 'Private identity references only; not new runtime art.', 'entries': records}
    (output / 'manifest.json').write_text(json.dumps(manifest, indent=2), encoding='utf-8')
    exported = sum('file' in state for entry in records for state in entry['states'].values())
    print(f'{len(records)} identities reviewed; {exported} direct state references exported.')


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--game', type=Path, required=True)
    parser.add_argument('--inventory', type=Path, required=True)
    parser.add_argument('--out', type=Path, required=True)
    args = parser.parse_args()
    extract(args.game, args.inventory, args.out)
