"""Place a reviewed missing torch component on an immutable native post plate.

The component has its own explicit crop, uniform scale and placement. The
native plate is never registered, warped or repainted. Taking the torch
reveals the exact original native RGBA image. Keep all assets outside Git.
"""
import argparse
import hashlib
import json
import math
from pathlib import Path

import numpy as np
from PIL import Image, ImageDraw

from compose_state_patch import compose


def assemble(spec_path, output):
    spec_path, output = Path(spec_path).resolve(), Path(output).resolve()
    repo = Path(__file__).resolve().parents[2]
    spec = json.loads(spec_path.read_text(encoding='utf-8-sig'))
    body_path = (spec_path.parent / spec['native_body']).resolve()
    torch_path = (spec_path.parent / spec['torch_component']).resolve()
    inputs = output.with_name(output.name + '-inputs')
    if any(p.is_relative_to(repo) for p in (body_path, torch_path, output, inputs)) or output.exists() or inputs.exists():
        raise ValueError('Use private assets and new private output folders')
    with Image.open(body_path) as loaded, Image.open(torch_path) as component:
        if loaded.mode != 'RGBA' or component.mode != 'RGBA':
            raise ValueError('Native body and component must retain RGBA')
        body, torch = loaded.copy(), component.copy()
    crop = spec['component_crop']
    if len(crop) != 4 or not all(isinstance(v, int) for v in crop) or not (0 <= crop[0] < crop[2] <= torch.width and 0 <= crop[1] < crop[3] <= torch.height):
        raise ValueError('Invalid reviewed component crop')
    visible = torch.getchannel('A').point(lambda a: 255 if a > 8 else 0).getbbox()
    if visible is None or any((crop[0] > visible[0], crop[1] > visible[1], crop[2] < visible[2], crop[3] < visible[3])):
        raise ValueError('Component crop would clip visible geometry')
    scale = spec['component_uniform_scale']
    if not isinstance(scale, (int, float)) or not math.isfinite(scale) or not .1 <= scale <= 2:
        raise ValueError('Invalid explicit component scale')
    torch = torch.crop(crop)
    torch = torch.resize((round(torch.width * scale), round(torch.height * scale)), Image.Resampling.LANCZOS)
    x, y = spec['component_offset']
    if not all(isinstance(v, int) for v in (x, y)) or x < 0 or y < 0 or x + torch.width > body.width or y + torch.height > body.height:
        raise ValueError('Placed component exceeds native canvas')
    if torch.width * torch.height > body.width * body.height * .2:
        raise ValueError('Review a bounded component domain within one fifth of the native canvas')
    closed = body.copy()
    closed.alpha_composite(torch, (x, y))
    mask = Image.new('L', body.size)
    ImageDraw.Draw(mask).rectangle((x, y, x + torch.width - 1, y + torch.height - 1), fill=255)
    inputs.mkdir()
    closed.save(inputs / 'closed-with-torch.png')
    body.save(inputs / 'native-empty-post.png')
    mask.save(inputs / 'component-domain.png')
    record = compose(inputs / 'closed-with-torch.png', inputs / 'native-empty-post.png', inputs / 'component-domain.png', output)
    used = np.asarray(Image.open(output / 'used.png'))
    if not np.array_equal(used, np.asarray(body)):
        raise ValueError('Searched state differs from the original native body')
    record.update({'native_body': str(body_path), 'native_body_sha256': hashlib.sha256(body_path.read_bytes()).hexdigest(),
                   'torch_component': str(torch_path), 'torch_component_sha256': hashlib.sha256(torch_path.read_bytes()).hexdigest(),
                   'component_crop': crop, 'component_uniform_scale': scale, 'component_offset': [x, y],
                   'used_RGBA_equals_native_reference': True, 'native_body_registration_or_repaint': False,
                   'generated_component_is_not_native_geometry': True, 'spec_sha256': hashlib.sha256(spec_path.read_bytes()).hexdigest(),
                   'assembly_recipe_sha256': hashlib.sha256(Path(__file__).read_bytes()).hexdigest()})
    (output / 'provenance.json').write_text(json.dumps(record, indent=2), encoding='utf-8')
    return record


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--spec', type=Path, required=True)
    parser.add_argument('--out', type=Path, required=True)
    args = parser.parse_args()
    print(json.dumps(assemble(args.spec, args.out)))
