"""Place a private ritual-bowl state pair on an unchanged native stone render.

The native table bitmap comes from the audited complete 76-vertex/46-face
component. Only the missing bowl is generated. Its state pair must already
pass the bounded local-eye compositor; this assembler never uses the full
drifting edit. No gameplay, summon routing or native acceptance is implied.
"""
import argparse
import hashlib
import json
from pathlib import Path

import numpy as np
from PIL import Image


def assemble(native_stone, component_pair, output):
    native_stone, component_pair, output = [Path(p).resolve() for p in
                                            (native_stone, component_pair, output)]
    repo = Path(__file__).resolve().parents[2]
    if any(p.is_relative_to(repo) for p in (native_stone, component_pair, output)) or output.exists():
        raise ValueError('Use private reviewed sources and a new private output')
    sha = lambda p: hashlib.sha256(Path(p).read_bytes()).hexdigest()
    if sha(native_stone) != '88dbe32a5c44b0e55bc03095d34c119c118bdadb055c84b36752ac1bf9cd2f78':
        raise ValueError('Use the unchanged audited native stone render')
    audit = json.loads((component_pair / 'provenance.json').read_text())
    if audit['size'] != [1774, 887] or not audit['all_pixels_outside_mask_exact'] \
            or audit['implicit_registration'] or audit['mask_pixels'] > 1774*887*.2:
        raise ValueError('Review a bounded same-canvas local bowl state first')
    native = Image.open(native_stone)
    if native.mode != 'RGBA' or native.size != (1024, 1024):
        raise ValueError('Preserve native RGBA dimensions')
    states = {}
    original = np.asarray(native)
    for state in ('closed', 'used'):
        path = component_pair / (state + '.png')
        if sha(path) != audit['outputs'][path.name]:
            raise ValueError('State pixels differ from the reviewed local composition')
        component = Image.open(path)
        if component.mode != 'RGBA' or component.size != (1774, 887):
            raise ValueError('Do not register or recrop a drifting component')
        # Faint generated alpha specks are not a physical foot. The reviewed
        # crop retains every pixel above 8/255 plus padding around the solid
        # body. Preserve all alpha within it; no thresholding or repainting.
        alpha = np.asarray(component.getchannel('A'))
        if np.any(alpha[:140] > 8) or np.any(alpha[806:] > 8):
            raise ValueError('The reviewed crop would cut visible component paint')
        component = component.crop((0, 140, 1774, 806))
        # One uniform component scale with rounded dimensions. Native support
        # pixels remain unresampled. Source opaque foot787 lands at slabY414.
        component = component.resize((600, 225), Image.Resampling.LANCZOS)
        result = native.copy()
        result.alpha_composite(component, (212, 196))
        if not np.array_equal(np.asarray(result)[430:], original[430:]):
            raise ValueError('Component paint leaked into the native lower body')
        states[state] = result
    before, after = [np.asarray(states[s]) for s in ('closed', 'used')]
    changed = np.any(before != after, axis=2)
    y, x = np.where(changed)
    if not len(y) or x.min() < 440 or x.max() >= 580 or y.min() < 270 or y.max() >= 370:
        raise ValueError('The state must change only the reviewed central bowl eye')
    output.mkdir(parents=True)
    for state, result in states.items():
        result.save(output / (state + '.png'))
    record = {'native_body_vertices': 76, 'native_body_triangles': 46,
              'native_full_paint_resolution': 2048, 'native_bitmap_resampled': False,
              'generated_component_uniform_scale': 600/1774,
              'component_crop': [0, 140, 1774, 806],
              'component_size': [600, 225], 'component_offset': [212, 196],
              'component_source_opaque_foot_Y': 787, 'target_slab_contact_Y': 414,
              'all_component_alpha_above8_retained': True,
              'alpha_within_reviewed_crop_retained_and_resampled': True,
              'native_rows430_onward_exact_both_states': True,
              'native_visible_pixels_outside_component_occlusion_exact': True,
              'changed_pixels': int(changed.sum()),
              'changed_bounds': [int(x.min()), int(y.min()), int(x.max()+1), int(y.max()+1)],
              'native_bitmap_sha256': sha(native_stone),
              'component_local_audit_sha256': sha(component_pair / 'provenance.json'),
              'recipe_sha256': sha(__file__), 'installed': False,
              'native_QA': False, 'canonical_summon_gameplay_accepted': False}
    (output / 'provenance.json').write_text(json.dumps(record, indent=2))
    return record


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    for name in ('native-stone', 'component-pair', 'out'):
        parser.add_argument('--' + name, type=Path, required=True)
    args = parser.parse_args()
    print(json.dumps(assemble(args.native_stone, args.component_pair, args.out)))
