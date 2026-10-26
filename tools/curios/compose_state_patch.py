"""Keep a fixed sprite body while applying a reviewed local state-paint edit.

Inputs are private same-canvas RGBA images and a grayscale edit-domain mask.
The mask may cover at most one fifth of the canvas. Pixels outside it are
copied exactly from the original, including transparent RGB and alpha. This
does not register or repair a drifting object: inspect the local mechanism
before selecting its mask, then verify the result at native display scale.
"""
import argparse
import hashlib
import json
from pathlib import Path
import shutil

import numpy as np
from PIL import Image


def compose(original, edited, mask_path, output):
    original, edited, mask_path, output = [Path(p).resolve() for p in (original, edited, mask_path, output)]
    repo = Path(__file__).resolve().parents[2]
    if any(p.is_relative_to(repo) for p in (original, edited, mask_path, output)) or output.exists():
        raise ValueError('Use private inputs and a new private output folder')
    with Image.open(original) as source, Image.open(edited) as change, Image.open(mask_path) as mask:
        if source.mode != 'RGBA' or change.mode != 'RGBA' or mask.mode != 'L':
            raise ValueError('Use original RGBA pixels and a grayscale edit mask')
        if source.size != change.size or source.size != mask.size:
            raise ValueError('Images and mask must have the same canvas; no implicit registration')
        if source.getchannel('A').getextrema()[0] != 0:
            raise ValueError('Original sprite must retain a transparent background')
        domain = np.asarray(mask) > 0
        pixels = int(domain.sum())
        if pixels < 16 or pixels > domain.size * .2 or mask.getextrema()[1] != 255:
            raise ValueError('Review a bounded local patch with an opaque core')
        result = Image.composite(change, source, mask)
        before, after = np.asarray(source), np.asarray(result)
        if not np.array_equal(before[~domain], after[~domain]):
            raise ValueError('Fixed sprite pixels changed')
        difference = np.any(before != after, axis=2)
        if not difference.any():
            raise ValueError('State patch made no visible change')
        y, x = np.where(difference)
        output.mkdir(parents=True)
        shutil.copyfile(original, output / 'closed.png')
        result.save(output / 'used.png')
        record = {'purpose': 'Reviewed local state-paint patch on a fixed original body',
                  'size': list(source.size), 'mask_bounds': mask.getbbox(),
                  'mask_pixels': pixels, 'changed_pixels': int(difference.sum()),
                  'changed_bounds': [int(x.min()), int(y.min()), int(x.max() + 1), int(y.max() + 1)],
                  'all_pixels_outside_mask_exact': True, 'implicit_registration': False,
                  'native_geometry_or_shader_claim': False, 'installed': False, 'native_QA': False,
                  'inputs': {str(p): hashlib.sha256(p.read_bytes()).hexdigest() for p in (original, edited, mask_path)},
                  'outputs': {p.name: hashlib.sha256(p.read_bytes()).hexdigest() for p in output.glob('*.png')},
                  'recipe_sha256': hashlib.sha256(Path(__file__).read_bytes()).hexdigest()}
        (output / 'provenance.json').write_text(json.dumps(record, indent=2), encoding='utf-8')
        return record


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    for key in ('original', 'edited', 'mask', 'out'):
        parser.add_argument('--' + key, type=Path, required=True)
    args = parser.parse_args()
    print(json.dumps(compose(args.original, args.edited, args.mask, args.out)))
