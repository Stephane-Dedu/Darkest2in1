"""Fit private closed/used curio paintings to one body scale and foot anchor.

This only resamples and pads RGBA sprites. It does not repaint, warp, harden
alpha or generate art. The source landmarks must be visually reviewed first.
Use fixed body landmarks in both states, never a moving lid or raised shell.
For collapsing bodies, scale_landmarks can specify a stable horizontal span.
"""
import argparse
import hashlib
import json
import math
from pathlib import Path

from PIL import Image
from departed_state import SEMANTICS, departure_proof


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def fit(spec_path, output):
    spec_path, output = spec_path.resolve(), output.resolve()
    if output.is_relative_to(Path(__file__).resolve().parents[2]):
        raise ValueError('Private curio art must stay outside the repository')
    if output.exists() and any(output.iterdir()):
        raise ValueError('Use a new output folder')
    spec = json.loads(spec_path.read_text(encoding='utf-8-sig'))
    key = spec['id']
    departed = departure_proof(spec, spec_path.parent)
    if not key or any(c not in 'abcdefghijklmnopqrstuvwxyz0123456789_' for c in key):
        raise ValueError('Unsafe curio identity')
    height = spec.get('pixel_span', spec.get('pixel_height', 900))
    display = spec.get('display_height', 400)
    if not 128 <= height <= 1600 or not 100 <= display <= 430:
        raise ValueError('Invalid canonical or display height')
    display_width = spec.get('display_width')
    if display_width is not None and not 100 <= display_width <= 400:
        raise ValueError('Invalid display width')
    padding = 16
    items = {}
    half_width, min_y = 0, 0
    for state in ('closed', 'used'):
        record = spec['states'][state]
        source = (spec_path.parent / record['file']).resolve()
        with Image.open(source) as loaded:
            image = loaded.copy()
        if image.mode != 'RGBA':
            raise ValueError('Source must contain its generated/native alpha')
        fx, fy = record['foot']
        if not 0 <= fx < image.width or not 0 <= fy < image.height:
            raise ValueError('Invalid reviewed landmarks')
        if 'scale_landmarks' in record:
            landmarks = record['scale_landmarks']
            if len(landmarks) != 2 or any(len(p) != 2 or not 0 <= p[0] < image.width or not 0 <= p[1] < image.height for p in landmarks):
                raise ValueError('Invalid reviewed scale span')
            span = math.dist(*landmarks)
            if not math.isfinite(span) or span <= 0:
                raise ValueError('Empty reviewed scale span')
        else:
            landmark = record['body_landmark_y']
            if not 0 <= landmark < fy:
                raise ValueError('Invalid reviewed body landmark')
            span = fy - landmark
        factor = height / span
        resized = image.resize((round(image.width * factor), round(image.height * factor)), Image.Resampling.LANCZOS)
        foot = (fx * factor, fy * factor)
        bounds = resized.getchannel('A').point(lambda a: 255 if a > 8 else 0).getbbox()
        empty_used = state == 'used' and departed is not None
        if empty_used:
            if image.getchannel('A').getextrema() != (0, 0):
                raise ValueError('A departed used sprite must be completely transparent')
            closed = items['closed']
            same_landmarks = {k: v for k, v in record.items() if k != 'file'} == {k: v for k, v in closed['record'].items() if k != 'file'}
            if image.size != closed['source_size'] or not same_landmarks:
                raise ValueError('Departed art must retain the closed canvas and virtual ground landmarks')
        elif bounds is None:
            raise ValueError('Empty source sprite')
        if bounds is not None:
            half_width = max(half_width, foot[0] - bounds[0], bounds[2] - foot[0])
            min_y = min(min_y, bounds[1] - foot[1] + height)
        # Soft contact shadows may enter the padding. Opaque geometry may not
        # extend below the reviewed foot anchor by more than that padding.
        opaque = resized.getchannel('A').point(lambda a: 255 if a > 128 else 0).getbbox()
        if opaque and opaque[3] > foot[1] + padding:
            raise ValueError('Foot landmark would clip opaque geometry')
        items[state] = {'image': resized, 'foot': foot, 'factor': factor, 'source': source, 'record': record, 'source_size': image.size}
    width = math.ceil(half_width + padding) * 2
    top = math.ceil(-min_y) + padding
    canvas_height = top + height + padding
    if width > 4096 or canvas_height > 2304 or width * canvas_height > 4194304:
        raise ValueError('Fitted canvas exceeds native preloader bounds')
    anchor = (width / 2, top + height)
    logical_width = min(400.0, display_width if display_width is not None else display * width / canvas_height,
                        430.0 * width / canvas_height)
    actual_height = logical_width * canvas_height / width
    output.mkdir(parents=True, exist_ok=True)
    metadata = {'id': key, 'size': [width, canvas_height], 'padding': padding,
                'foot_anchor': list(anchor), 'canonical_landmark_height': height,
                'proposed_runtime_width': round(logical_width, 4),
                'runtime_full_height': round(actual_height, 4),
                'registered_landmark_scale_and_ground': True, 'source_alpha_resampled_without_thresholding': True,
                'native_verified': False, 'installed': False,
                'spec_sha256': sha(spec_path), 'recipe_sha256': sha(Path(__file__)), 'states': {}}
    if departed is not None:
        metadata['used_state_semantics'] = SEMANTICS
        metadata['departure_proof'] = departed
    if 'pixel_span' in spec:
        metadata['canonical_landmark_span'] = metadata.pop('canonical_landmark_height')
    for state, item in items.items():
        offset = (round(anchor[0] - item['foot'][0]), round(anchor[1] - item['foot'][1]))
        canvas = Image.new('RGBA', (width, canvas_height))
        canvas.alpha_composite(item['image'], offset)
        path = output / (key + '-' + state + '.png')
        canvas.save(path)
        measured_anchor = [offset[i] + item['foot'][i] for i in (0, 1)]
        if any(abs(measured_anchor[i] - anchor[i]) > .5 for i in (0, 1)):
            raise ValueError('Foot registration error')
        metadata['states'][state] = {'source_sha256': sha(item['source']), 'output_sha256': sha(path),
                                     'uniform_scale': item['factor'], 'offset': list(offset),
                                     'registered_foot': measured_anchor, 'source_landmarks': item['record']}
    (output / 'provenance.json').write_text(json.dumps(metadata, indent=2), encoding='utf-8')
    print(json.dumps(metadata, indent=2))


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--spec', type=Path, required=True)
    parser.add_argument('--out', type=Path, required=True)
    args = parser.parse_args()
    fit(args.spec, args.out)
