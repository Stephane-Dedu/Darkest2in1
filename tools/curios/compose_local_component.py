"""Adapt an explicitly cropped local surface before the strict state composer.

Private JSON: closed, component, domain, protected, crop and offset. The source
edit may have a different canvas, but is never resized or registered as a body.
Only the reviewed crop is placed at the explicit offset. Existing same-canvas
and 20% state-domain guards still apply to the resulting candidate.
"""
import argparse
import hashlib
import json
from pathlib import Path

import numpy as np
from PIL import Image

from compose_state_patch import compose


def compose_component(spec_path, output):
    spec_path, output = Path(spec_path).resolve(), Path(output).resolve()
    repo = Path(__file__).resolve().parents[2]
    spec = json.loads(spec_path.read_text())
    paths = {key: (spec_path.parent / spec[key]).resolve()
             for key in ('closed', 'component', 'domain', 'protected')}
    if spec_path.is_relative_to(repo) or output.is_relative_to(repo) or output.exists() \
            or any(p.is_relative_to(repo) for p in paths.values()):
        raise ValueError('Use private inputs and a new private output')
    closed, component, domain, protected = [Image.open(paths[k]) for k in
                                            ('closed', 'component', 'domain', 'protected')]
    if closed.mode != 'RGBA' or component.mode != 'RGBA' or domain.mode != 'L' or protected.mode != 'L' \
            or domain.size != closed.size or protected.size != closed.size:
        raise ValueError('Preserve RGBA sources and explicit same-size L masks')
    crop, offset = tuple(spec['crop']), tuple(spec['offset'])
    if len(crop) != 4 or len(offset) != 2 or any(type(n) is not int for n in (*crop, *offset)) \
            or not (0 <= crop[0] < crop[2] <= component.width and
                    0 <= crop[1] < crop[3] <= component.height):
        raise ValueError('Invalid reviewed local crop')
    width, height = crop[2] - crop[0], crop[3] - crop[1]
    if min(offset) < 0 or offset[0] + width > closed.width or offset[1] + height > closed.height:
        raise ValueError('Local component clips the closed canvas')
    mask = np.asarray(domain) > 0
    allowed = np.zeros(mask.shape, dtype=bool)
    allowed[offset[1]:offset[1] + height, offset[0]:offset[0] + width] = True
    if np.any(mask & ~allowed):
        raise ValueError('State domain extends beyond the reviewed component')
    candidate = closed.copy()
    candidate.paste(component.crop(crop), offset)
    original = np.asarray(closed)
    changed = np.any(original != np.asarray(candidate), axis=2) & mask
    fixed = np.asarray(protected) > 0
    if np.any(changed & fixed):
        raise ValueError('State domain changes protected source pixels')
    output.mkdir(parents=True)
    candidate.save(output / 'local-edit.png')
    record = compose(paths['closed'], output / 'local-edit.png', paths['domain'], output / 'pair')
    after = np.asarray(Image.open(output / 'pair' / 'used.png'))
    if not np.array_equal(original[fixed], after[fixed]):
        raise ValueError('Protected source pixels changed after composition')
    digest = lambda p: hashlib.sha256(p.read_bytes()).hexdigest()
    record.update({'component_source_size': component.size, 'closed_size': closed.size,
                   'explicit_component_crop': crop, 'explicit_component_offset': offset,
                   'component_resampled': False, 'whole_body_registration': False,
                   'protected_pixels': int(fixed.sum()), 'protected_RGBA_exact': True,
                   'inputs': {str(p): digest(p) for p in paths.values()},
                   'spec_sha256': digest(spec_path), 'component_recipe_sha256': digest(Path(__file__))})
    (output / 'provenance.json').write_text(json.dumps(record, indent=2))
    return record


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--spec', type=Path, required=True)
    parser.add_argument('--out', type=Path, required=True)
    args = parser.parse_args()
    print(json.dumps(compose_component(args.spec, args.out)))
