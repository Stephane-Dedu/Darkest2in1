"""Add a bounded generated vapour layer to the unchanged native treatment slab.

The used state is the audited complete native corpse/table bitmap. Only the
missing contamination cue is generated; its removal leaves the source anatomy
and paint intact. This prepares art and does not establish quest availability
or native gameplay acceptance.
"""
import argparse
import hashlib
import json
from pathlib import Path

import numpy as np
from PIL import Image


def assemble(native_body, vapour, output):
    native_body, vapour, output = [Path(p).resolve() for p in
                                  (native_body, vapour, output)]
    repo = Path(__file__).resolve().parents[2]
    if output.exists() or any(p.is_relative_to(repo) for p in
                              (native_body, vapour, output)):
        raise ValueError('Use reviewed private inputs and a new private output')
    sha = lambda p: hashlib.sha256(Path(p).read_bytes()).hexdigest()
    if sha(native_body) != 'c7c8ae888704ff73ef94aa47f15831b16d74c274481723b51aefb299a008db84':
        raise ValueError('Use the audited complete native corpse/table bitmap')
    if sha(vapour) != '2b44fcfbf53fef17f53650b21e9745db26697e939947695bdbbf3f034f115c25':
        raise ValueError('Use the reviewed isolated vapour component')
    native, component = Image.open(native_body), Image.open(vapour)
    if native.mode != 'RGBA' or native.size != (1024, 1024) \
            or component.mode != 'RGBA' or component.size != (1774, 887):
        raise ValueError('Preserve the reviewed source canvases')
    crop = (350, 289, 1441, 762)
    alpha = np.asarray(component.getchannel('A'))
    retained = np.zeros(alpha.shape, dtype=bool)
    retained[crop[1]:crop[3], crop[0]:crop[2]] = True
    if np.any(alpha[~retained] > 8):
        raise ValueError('Crop would remove visible vapour paint')
    # Uniform component scale with integer rounding; the native body is never
    # resampled. Faint exterior alpha specks do not determine body placement.
    component = component.crop(crop).resize((200, 87), Image.Resampling.LANCZOS)
    offset = (400, 263)
    layer = Image.new('RGBA', native.size)
    layer.alpha_composite(component, offset)
    domain = np.asarray(layer.getchannel('A')) > 0
    original = np.asarray(native)
    closed = Image.alpha_composite(native, layer)
    after = np.asarray(closed)
    changed = np.any(after != original, axis=2)
    if not changed.any() or domain.sum() > 1024 * 1024 * .2 \
            or not np.array_equal(after[~domain], original[~domain]) \
            or not np.array_equal(after[350:], original[350:]):
        raise ValueError('Vapour must stay local with all other native pixels exact')
    y, x = np.where(changed)
    if x.min() < 400 or x.max() >= 600 or y.min() < 263 or y.max() >= 350:
        raise ValueError('Component escaped the reviewed abdominal domain')
    output.mkdir(parents=True)
    closed.save(output / 'closed.png')
    native.save(output / 'used.png')
    record = {
        'native_body_vertices': 4822, 'native_body_triangles': 6859,
        'native_full_paints': [1024, 2048],
        'native_bitmap_resampled': False, 'native_pose_and_anatomy_unchanged': True,
        'generated_component_only': 'isolated toxic vapour',
        'component_crop': list(crop), 'component_size': [200, 87],
        'component_offset': list(offset), 'all_component_alpha_above8_retained': True,
        'mask_pixels': int(domain.sum()), 'mask_limit_fraction': .2,
        'changed_pixels': int(changed.sum()),
        'changed_bounds': [int(x.min()), int(y.min()), int(x.max()+1), int(y.max()+1)],
        'all_RGBA_outside_overlay_exact': True, 'native_rows350_onward_exact': True,
        'used_RGBA_exact_native_body': True,
        'native_bitmap_sha256': sha(native_body), 'component_sha256': sha(vapour),
        'recipe_sha256': sha(__file__),
        'outputs': {p.name: sha(p) for p in output.glob('*.png')},
        'installed': False, 'native_QA': False,
    }
    (output / 'provenance.json').write_text(json.dumps(record, indent=2))
    return record


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    for name in ('native-body', 'vapour', 'out'):
        parser.add_argument('--' + name, type=Path, required=True)
    args = parser.parse_args()
    print(json.dumps(assemble(args.native_body, args.vapour, args.out)))
