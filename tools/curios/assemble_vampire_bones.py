"""Place a missing vampire-skull component behind a complete native bone pile.

The original native bitmap is the entire used state. Its opaque pixels and
lower footprint stay exact in the closed state. Only the generated trophy is
graded and added. This prepares design art, not a Summon or loot outcome.
"""
import argparse
import hashlib
import json
from pathlib import Path

import numpy as np
from PIL import Image


def assemble(native_body, trophy, output):
    native_body, trophy, output = [Path(p).resolve() for p in
                                  (native_body, trophy, output)]
    repo = Path(__file__).resolve().parents[2]
    if output.exists() or any(p.is_relative_to(repo) for p in (native_body, trophy, output)):
        raise ValueError('Use retained private inputs and a new private output')
    sha = lambda p: hashlib.sha256(Path(p).read_bytes()).hexdigest()
    if sha(native_body) != 'fafbc19be4181fdc5477bf607670c84190472ab19217e62256d36b48ccd23aa4' \
            or sha(trophy) != '06b35514ab94e7d363119891340652227768c9898bdb49f84ab418c3206b9dfd':
        raise ValueError('Use the complete reviewed native pile and original trophy')
    body, component = Image.open(native_body), Image.open(trophy)
    if body.mode != 'RGBA' or body.size != (768, 768) \
            or component.mode != 'RGBA' or component.size != (1254, 1254):
        raise ValueError('Preserve reviewed source canvases')
    crop = (115, 150, 1215, 1188)
    alpha = np.asarray(component.getchannel('A'))
    retained = np.zeros(alpha.shape, dtype=bool)
    retained[crop[1]:crop[3], crop[0]:crop[2]] = True
    if np.any(alpha[~retained] > 8):
        raise ValueError('Crop would remove visible trophy paint')
    size, offset = (240, 226), (270, 90)
    part = np.asarray(component.crop(crop).resize(size, Image.Resampling.LANCZOS)).copy()
    # Explicit generated-component tint in encoded RGB; native paint is fixed.
    part[:, :, :3] = np.round(part[:, :, :3].astype(float) * .75).astype('uint8')
    layer = Image.new('RGBA', body.size)
    layer.alpha_composite(Image.fromarray(part), offset)
    domain = np.asarray(layer.getchannel('A')) > 0
    original = np.asarray(body)
    composite = np.asarray(Image.alpha_composite(layer, body))
    closed = original.copy()
    closed[domain] = composite[domain]
    changed = np.any(closed != original, axis=2)
    if not changed.any() or domain.mean() > .2 or np.any(changed & ~domain) \
            or np.any(changed[316:]) \
            or not np.array_equal(closed[original[:, :, 3] == 255],
                                  original[original[:, :, 3] == 255]):
        raise ValueError('Preserve all opaque native bones, exterior RGBA and feet')
    output.mkdir(parents=True)
    Image.fromarray(closed).save(output / 'closed.png')
    body.save(output / 'used.png')
    y, x = np.where(changed)
    record = {'native_complete_model_reference': [122, 142],
              'native_full_source_paint': 1024, 'native_bitmap_not_resampled': True,
              'generated_upper_skull_only': True, 'component_crop': crop,
              'component_size': size, 'offset': offset,
              'generated_component_encoded_RGB_multiplier': .75,
              'same_canvas': list(body.size), 'domain_pixels': int(domain.sum()),
              'changed_pixels': int(changed.sum()),
              'changed_bounds': [int(x.min()), int(y.min()), int(x.max()+1), int(y.max()+1)],
              'all_opaque_native_pixels_exact': True, 'outside_RGBA_exact': True,
              'rows316_onward_exact': True, 'used_RGBA_exact_native_body': True,
              'native_body_sha256': sha(native_body), 'trophy_sha256': sha(trophy),
              'recipe_sha256': sha(__file__),
              'outputs': {p.name: sha(p) for p in output.glob('*.png')},
              'installed': False, 'native_verified': False, 'canonical_Summon_QA': False}
    (output / 'provenance.json').write_text(json.dumps(record, indent=2))
    return record


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    for name in ('native-body', 'trophy', 'out'):
        parser.add_argument('--'+name, type=Path, required=True)
    args = parser.parse_args()
    print(json.dumps(assemble(args.native_body, args.trophy, args.out)))
