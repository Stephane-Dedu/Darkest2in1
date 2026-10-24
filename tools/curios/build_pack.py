"""Build a private exact-ID curio pack with shared states and alpha picking.

No assets or derived hit masks belong in Git. Reuse audited canonical pairs,
keep curio identities separate, and never substitute a generic family object.
"""
import argparse
import base64
import hashlib
import json
import math
import os
from pathlib import Path
import shutil

import numpy as np
from PIL import Image

from catalog_quality import catalog


def build(inventory, selection, output):
    output = output.resolve()
    if output.exists():
        raise ValueError('Use a new private pack directory')
    if output.is_relative_to(Path(__file__).resolve().parents[2]):
        raise ValueError('Private art and derived masks must stay outside Git')
    coverage = catalog(inventory, selection, output / 'review')
    ready = [e for e in coverage['entries'] if e['status'] == 'offline_pair_candidate']
    artifacts = {}
    folder = output / 'art'
    folder.mkdir()
    for entry in ready:
        key = entry['art_id']
        if key in artifacts:
            continue
        metadata = json.loads((Path(entry['pair_folder']) / 'provenance.json').read_text())
        with Image.open(entry['states']['closed']['file']) as image:
            alpha = np.asarray(image.getchannel('A'))
        height, width = alpha.shape
        cell = math.ceil(max(width, height) / 256)
        mw, mh = math.ceil(width / cell), math.ceil(height / cell)
        padded = np.pad(alpha, ((0, mh * cell - height), (0, mw * cell - width)))
        # Conservative max pooling is ONLY for picking, never for displayed art.
        mask = padded.reshape(mh, cell, mw, cell).max(axis=(1, 3)) > 40
        bits = np.packbits(mask.reshape(-1), bitorder='little').tobytes()
        for state in ('closed', 'used'):
            source = Path(entry['states'][state]['file'])
            destination = folder / (key + '-' + state + '.png')
            try:
                os.link(source, destination)
            except OSError:
                shutil.copy2(source, destination)
        artifacts[key] = {'Id': key, 'Width': entry['width'], 'PixelWidth': width, 'PixelHeight': height,
                          'Padding': 16, 'MaskCellSize': cell, 'MaskWidth': mw, 'MaskHeight': mh,
                          'Mask': base64.b64encode(bits).decode('ascii'),
                          'ClosedSha256': entry['states']['closed']['sha256'],
                          'UsedSha256': entry['states']['used']['sha256']}
    document = {'Version': 2, 'Artifacts': list(artifacts.values()),
                'Entries': [{'Id': e['id'], 'ArtId': e['art_id']} for e in ready]}
    (output / 'manifest.json').write_text(json.dumps(document, separators=(',', ':')), encoding='utf-8')
    (output / 'provenance.json').write_text(json.dumps({
        'selection_sha256': hashlib.sha256(selection.read_bytes()).hexdigest(),
        'recipe_sha256': hashlib.sha256(Path(__file__).read_bytes()).hexdigest(),
        'purpose': 'Private curio pack candidate; native verification recorded separately',
        'identity_count': len(ready), 'unique_pairs': len(artifacts), 'pixel_states': len(artifacts) * 2,
        'picking': 'Closed-state alpha>40, conservative pooled bits; original display alpha unchanged',
        'installed': False, 'native_verified': False}, indent=2), encoding='utf-8')
    print(json.dumps({'identities': len(ready), 'pairs': len(artifacts), 'out': str(output)}))


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--inventory', type=Path, required=True)
    parser.add_argument('--selection', type=Path, required=True)
    parser.add_argument('--out', type=Path, required=True)
    args = parser.parse_args()
    build(args.inventory, args.selection, args.out)
