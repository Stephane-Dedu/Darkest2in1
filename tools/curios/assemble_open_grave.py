"""Place only a missing excavation component around an unchanged native marker.

Private JSON spec: native_marker, native_offset, canvas, component_closed,
component_edit, state_mask, component_crop, component_width, component_offset.
The bounded state patch is applied before uniform component fitting. Neither
native marker registration nor native repainting is inferred from an AI edit.
"""
import argparse
import hashlib
import json
from pathlib import Path

import numpy as np
from PIL import Image

from compose_state_patch import compose


def assemble(spec_path, output):
    spec_path, output = Path(spec_path).resolve(), Path(output).resolve()
    repo = Path(__file__).resolve().parents[2]
    if spec_path.is_relative_to(repo) or output.is_relative_to(repo) or output.exists():
        raise ValueError('Use private inputs and a new private output')
    spec = json.loads(spec_path.read_text())
    paths = {key: (spec_path.parent / spec[key]).resolve() for key in
             ('native_marker', 'component_closed', 'component_edit', 'state_mask')}
    if any(p.is_relative_to(repo) for p in paths.values()):
        raise ValueError('Keep native and generated paint outside the repository')
    marker = Image.open(paths['native_marker'])
    component = Image.open(paths['component_closed'])
    if marker.mode != 'RGBA' or component.mode != 'RGBA':
        raise ValueError('Preserve original RGBA inputs')
    canvas = tuple(spec['canvas'])
    native_offset = tuple(spec['native_offset'])
    crop = tuple(spec['component_crop'])
    offset = tuple(spec['component_offset'])
    width = spec['component_width']
    if len(canvas) != 2 or min(canvas) < 16 or any(type(n) is not int for n in
            (*canvas, *native_offset, *crop, *offset, width)):
        raise ValueError('Use explicit integer canvas and component placement')
    if len(native_offset) != 2 or len(offset) != 2 or len(crop) != 4 or width <= 0:
        raise ValueError('Invalid assembly dimensions')
    box = marker.getchannel('A').point(lambda a: 255 if a > 8 else 0).getbbox()
    if not box or box[0] + native_offset[0] < 0 or box[1] + native_offset[1] < 0 \
            or box[2] + native_offset[0] > canvas[0] or box[3] + native_offset[1] > canvas[1]:
        raise ValueError('Native marker placement clips visible source paint')
    if not (0 <= crop[0] < crop[2] <= component.width and
            0 <= crop[1] < crop[3] <= component.height):
        raise ValueError('Invalid component crop')
    alpha = np.asarray(component.getchannel('A'))
    outside = np.ones(alpha.shape, dtype=bool)
    outside[crop[1]:crop[3], crop[0]:crop[2]] = False
    if outside.any() and int(alpha[outside].max()) > 8:
        raise ValueError('Component crop clips visible excavation paint')
    height = round((crop[3] - crop[1]) * width / (crop[2] - crop[0]))
    if height < 1 or min(offset) < 0 or offset[0] + width > canvas[0] or offset[1] + height > canvas[1]:
        raise ValueError('Excavation placement clips the component below or outside the canvas')
    output.mkdir(parents=True)
    patch = compose(paths['component_closed'], paths['component_edit'], paths['state_mask'],
                    output / 'component-pair')
    body = Image.new('RGBA', canvas)
    body.paste(marker, native_offset)
    body.save(output / 'native-body.png')
    states, layers = {}, {}
    for state in ('closed', 'used'):
        part = Image.open(output / 'component-pair' / (state + '.png')).crop(crop)
        part = part.resize((width, height), Image.Resampling.LANCZOS)
        layer = Image.new('RGBA', canvas)
        layer.alpha_composite(part, offset)
        layers[state] = layer
        states[state] = Image.alpha_composite(body, layer)
    native_mask = np.asarray(body)[:, :, 3] > 8
    before, after = np.asarray(states['closed']), np.asarray(states['used'])
    if not np.array_equal(before[native_mask], after[native_mask]):
        raise ValueError('The state patch changes native marker paint')
    difference = np.any(before != after, axis=2)
    y, x = np.where(difference)
    if not len(x):
        raise ValueError('State cue vanished during component fitting')
    for state, image in states.items():
        image.save(output / (state + '.png'))
    digest = lambda p: hashlib.sha256(Path(p).read_bytes()).hexdigest()
    record = {'native_marker_resampled': False, 'native_offset': native_offset,
              'component_crop': crop, 'component_width': width, 'component_offset': offset,
              'component_output_size': [width, height], 'component_not_clipped': True,
              'native_marker_state_pixels_exact': True, 'local_component_patch': patch,
              'changed_pixels': int(difference.sum()),
              'changed_bounds': [int(x.min()), int(y.min()), int(x.max() + 1), int(y.max() + 1)],
              'soil_occluded_native_pixels': int(((np.asarray(body)[:, :, 3] > 128) &
                                                  (np.asarray(layers['closed'])[:, :, 3] > 128)).sum()),
              'inputs': {str(p): digest(p) for p in paths.values()},
              'spec_sha256': digest(spec_path), 'recipe_sha256': digest(__file__),
              'native_geometry_for_generated_pit_claim': False, 'native_QA': False, 'installed': False}
    (output / 'provenance.json').write_text(json.dumps(record, indent=2))
    return record


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--spec', type=Path, required=True)
    parser.add_argument('--out', type=Path, required=True)
    args = parser.parse_args()
    print(json.dumps(assemble(args.spec, args.out)))
