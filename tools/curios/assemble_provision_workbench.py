"""Assemble a generated provision workbench and a bounded searched state.

Use the two previously reviewed component crops. The body, cupboard doors,
shelf and feet stay fixed. Used removes only the top provisions. This is a
generated raster adaptation, not native geometry or a literal door animation.
Original generation and crops remain private and are not repeated here.
"""
import argparse
import hashlib
import json
from pathlib import Path

import numpy as np
from PIL import Image


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def assemble(source, output):
    source, output = source.resolve(), output.resolve()
    repo = Path(__file__).resolve().parents[2]
    if output.exists() or any(path.is_relative_to(repo) for path in (source, output)):
        raise ValueError('Use reviewed private inputs and a fresh private output')
    pins = {
        'body-source-crop.png': '317244216937003918472806cd280d6693f72fc1ffdfb93e68b621ab357ef534',
        'provisions-source-crop.png': 'e2dbfb70d7e0f53af5cb51c50a1575b74b4de5582885e45f1aa7a2a54f9b4102',
    }
    for name, expected in pins.items():
        if sha(source / name) != expected:
            raise ValueError('Reviewed component changed: ' + name)
    body = Image.open(source / 'body-source-crop.png')
    stock = Image.open(source / 'provisions-source-crop.png')
    if body.mode != 'RGBA' or stock.mode != 'RGBA' or body.size != (1140, 768) or stock.size != (716, 395):
        raise ValueError('Preserve the reviewed RGBA components')
    stock = stock.resize((501, 276), Image.Resampling.LANCZOS)
    used = Image.new('RGBA', (1140, 1024))
    used.alpha_composite(body, (0, 220))
    closed = used.copy()
    closed.alpha_composite(stock, (300, 64))
    a, b = np.array(closed), np.array(used)
    changed = np.any(a != b, axis=2)
    domain = np.zeros(changed.shape, dtype=bool)
    domain[64:340, 300:801] = True
    y, x = np.where(changed)
    bounds = [int(x.min()), int(y.min()), int(x.max()) + 1, int(y.max()) + 1]
    if changed.sum() != 73596 or bounds != [300, 64, 801, 340] or np.any(changed & ~domain) or not np.array_equal(a[400:], b[400:]):
        raise ValueError('Only the reviewed provisions may change')
    output.mkdir(parents=True)
    closed.save(output / 'closed.png')
    used.save(output / 'used.png')
    record = {
        'art_id': 'com_provision_workbench_design',
        'proposed_canonical_ids': ['com_provisionbox_1', 'com_provisionbox_2'],
        'generated_raster_body_and_stock_not_native_geometry': True,
        'source_component_hashes': pins,
        'original_component_sheet_sha256': '2a03c96f3aee38e3a10bc3b4283cac0c646c8c37ec1951e24a9ccc2021b4f96f',
        'no_generation_recrop_or_alpha_thresholding': True,
        'body_offset': [0, 220],
        'stock_uniform_scale': 0.7,
        'stock_offset': [300, 64],
        'stock_on_counter_flat_composite_not_native_depth': True,
        'state_only_stock_removed_no_door_animation': True,
        'changed_pixels': int(changed.sum()),
        'changed_bounds': bounds,
        'outside_stock_domain_RGBA_exact': True,
        'rows400_onward_RGBA_exact': True,
        'outputs': {name: sha(output / name) for name in ('closed.png', 'used.png')},
        'recipe_sha256': sha(Path(__file__)),
        'installed': False,
        'accepted': False,
        'canonical_DLC_gameplay_QA': False,
    }
    (output / 'provenance.json').write_text(json.dumps(record, indent=2))
    return record


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--source', type=Path, required=True)
    parser.add_argument('--out', type=Path, required=True)
    args = parser.parse_args()
    print(json.dumps(assemble(args.source, args.out), indent=2))
