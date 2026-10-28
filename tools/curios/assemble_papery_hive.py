"""Register a generated Hive pair using only a reviewed local burned-cap edit.

Both inputs are generated raster art. The donor is not used as a whole state:
the original lower body, tunnel mouths, sap, fibers and footprint remain exact.
This prepares a DLC design asset, not a canonical encounter or mechanic.
"""
import argparse
import hashlib
import json
from pathlib import Path

import numpy as np
from PIL import Image, ImageDraw


def assemble(closed, donor, output):
    closed, donor, output = [Path(p).resolve() for p in (closed, donor, output)]
    repo = Path(__file__).resolve().parents[2]
    if output.exists() or any(p.is_relative_to(repo) for p in (closed, donor, output)):
        raise ValueError('Use reviewed private inputs and a new private output')
    sha = lambda p: hashlib.sha256(Path(p).read_bytes()).hexdigest()
    if sha(closed) != '6b610e31042dacb679160244bf7df27f56ddaef159c1a9c8abd3172dcdbd4df3' \
            or sha(donor) != 'af8a3093ad4be98f5be956054bf848dfd95f1af2c01d192baa73a4fca4395656':
        raise ValueError('Use the retained original generated body and cap donor')
    body, cap = Image.open(closed), Image.open(donor)
    if body.mode != 'RGBA' or cap.mode != 'RGBA' or body.size != (1383, 1137) \
            or cap.size != body.size:
        raise ValueError('Preserve the reviewed same-size canvases without resampling')
    mask = Image.new('L', body.size, 0)
    polygon = [(400, 0), (1010, 0), (1100, 440), (350, 440)]
    ImageDraw.Draw(mask).polygon(polygon, fill=255)
    alpha = np.asarray(mask).copy()
    alpha[410:440] = (alpha[410:440].astype(float) *
                      np.linspace(1, 0, 30)[:, None]).round().astype('uint8')
    alpha[440:] = 0
    used = Image.composite(cap, body, Image.fromarray(alpha))
    before, after = np.asarray(body), np.asarray(used)
    domain = alpha > 0
    changed = np.any(before != after, axis=2)
    if not changed.any() or domain.mean() > .2 or np.any(changed & ~domain) \
            or np.any(changed[440:]):
        raise ValueError('Only the bounded cap may change; preserve the lower body and feet')
    y, x = np.where(changed)
    output.mkdir(parents=True)
    body.save(output / 'closed.png')
    used.save(output / 'used.png')
    record = {'generated_raster_not_native_geometry_or_paint': True,
              'same_canvas': list(body.size), 'donor_offset': [0, 0],
              'donor_no_resize': True, 'component_polygon': polygon,
              'local_bottom_fade_rows': [410, 440],
              'domain_pixels': int(domain.sum()), 'domain_fraction': float(domain.mean()),
              'changed_pixels': int(changed.sum()),
              'changed_bounds': [int(x.min()), int(y.min()), int(x.max()+1), int(y.max()+1)],
              'outside_RGBA_exact': True, 'rows440_onward_exact': True,
              'lower_mouths_sap_fibers_and_ground_exact': True,
              'closed_input_sha256': sha(closed), 'donor_input_sha256': sha(donor),
              'recipe_sha256': sha(__file__),
              'outputs': {p.name: sha(p) for p in output.glob('*.png')},
              'installed': False, 'native_verified': False, 'canonical_DLC_gameplay_QA': False}
    (output / 'provenance.json').write_text(json.dumps(record, indent=2))
    return record


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    for name in ('closed', 'donor', 'out'):
        parser.add_argument('--' + name, type=Path, required=True)
    args = parser.parse_args()
    print(json.dumps(assemble(args.closed, args.donor, args.out)))
