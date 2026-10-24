"""Fit private terrain-only paintings without repainting opaque native scenery.

The spec names the base map, old terrain plate, coverage, terrain alpha, road
mask, logical layout and paintings. Paths are relative to the spec or absolute.
This writes a private pack only. It never installs art or accesses game saves.
"""
import argparse
import hashlib
import json
from pathlib import Path

import numpy as np
from PIL import Image, ImageFilter


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def smooth(a, b, value):
    t = np.clip((value - a) / (b - a), 0, 1)
    return t * t * (3 - 2 * t)


def compose(spec_path, output):
    spec_path = spec_path.resolve(); output = output.resolve()
    if output.is_relative_to(Path(__file__).resolve().parents[2]):
        raise ValueError('Game-derived art must stay outside the repository')
    spec = json.loads(spec_path.read_text(encoding='utf-8-sig'))
    paths = {name: (spec_path.parent / spec[name]).resolve() for name in
             ('base', 'old_terrain', 'coverage', 'terrain_alpha', 'roads_mask', 'layout')}
    inputs = set(paths.values()) | {spec_path} | {
        (spec_path.parent / painting['path']).resolve() for painting in spec.get('paintings', [])}
    outputs = {output / name for name in ('map3d.png', 'map3d.json', 'terrain-scope.png', 'provenance.json')}
    if inputs & outputs:
        raise ValueError('The output must not overwrite an input')
    base = Image.open(paths['base']).convert('RGB'); size = base.size; scale = size[0] / 1920
    if size not in ((1920, 1080), (3840, 2160)):
        raise ValueError('Expected a1920x1080 or3840x2160 reference')
    images = {name: Image.open(paths[name]) for name in ('old_terrain', 'coverage', 'terrain_alpha', 'roads_mask')}
    if any(im.size != size for im in images.values()):
        raise ValueError('All reference passes must match the base dimensions')
    if any('A' not in images[name].getbands() for name in ('coverage', 'terrain_alpha')):
        raise ValueError('Coverage and terrain references need alpha channels')
    original = np.asarray(base, dtype=np.float32)
    old = np.asarray(images['old_terrain'].convert('RGB'), dtype=np.float32)
    native = np.asarray(images['coverage'].getchannel('A'), dtype=np.float32) / 255
    alpha = images['terrain_alpha'].getchannel('A').point(lambda x: 255 if x == 255 else 0)
    alpha = alpha.filter(ImageFilter.MinFilter(2 * round(2 * scale) + 1))
    terrain = np.asarray(alpha, dtype=np.float32) / 255
    roads = np.asarray(images['roads_mask'].convert('L'), dtype=np.float32) / 255
    delta = np.zeros_like(old); scope_all = np.zeros(size[::-1], dtype=np.float32)
    occupied = np.zeros(size[::-1], dtype=bool); sources = []
    if not spec.get('paintings'): raise ValueError('No terrain paintings specified')
    for painting in spec['paintings']:
        side = painting['side']; x0, y0, x1, y1 = painting['rect']
        if side not in ('left', 'right') or not (0 <= x0 < x1 <= size[0] and 0 <= y0 < y1 <= size[1]):
            raise ValueError('Invalid painting side or crop')
        if occupied[y0:y1, x0:x1].any(): raise ValueError('Painting crops must not overlap')
        occupied[y0:y1, x0:x1] = True
        path = (spec_path.parent / painting['path']).resolve(); im = Image.open(path).convert('RGB')
        width, height = x1 - x0, y1 - y0
        if abs(im.width / im.height - width / height) / (width / height) >= .01:
            raise ValueError('Painting aspect ratio differs from its reference crop')
        actual = list(im.size)
        image = np.asarray(im.resize((width, height), Image.Resampling.LANCZOS), dtype=np.float32)
        xx = np.arange(width, dtype=np.float32)[None, :]; yy = np.arange(height, dtype=np.float32)[:, None]
        inner = smooth(0, 64 * scale, xx) if side == 'right' else 1 - smooth(width - 64 * scale, width, xx)
        bottom = 1 - smooth(height - 90 * scale, height - 8 * scale, yy)
        earth = smooth(.35, .50, image[..., 2] / np.maximum(image[..., 0], 1))
        scope = inner * bottom * earth * terrain[y0:y1, x0:x1] * (1 - roads[y0:y1, x0:x1])
        delta[y0:y1, x0:x1] += (image - old[y0:y1, x0:x1]) * scope[..., None]
        scope_all[y0:y1, x0:x1] = np.maximum(scope_all[y0:y1, x0:x1], scope)
        sources.append({'side': side, 'rect': painting['rect'], 'source_size': actual, 'sha256': sha(path)})
    # The existing map already contains the native foreground once. Change only
    # its background contribution; applying foreground alpha again creates halos.
    result = (original + delta * (1 - native)[..., None]).round().clip(0, 255).astype(np.uint8)
    unchanged = np.all(result == original.astype(np.uint8), axis=-1)
    if not (unchanged[native == 1].all() and unchanged[scope_all == 0].all() and unchanged[roads == 1].all()):
        raise ValueError('Native scenery, road or outside-scope retention failed')
    output.mkdir(parents=True, exist_ok=True)
    Image.fromarray(result).save(output / 'map3d.png')
    if (output / 'map3d.png').stat().st_size > 16 * 1024 * 1024:
        raise ValueError('PNG exceeds the runtime16MiB limit')
    (output / 'map3d.json').write_bytes(paths['layout'].read_bytes())
    Image.fromarray((scope_all * 255).round().astype(np.uint8)).save(output / 'terrain-scope.png')
    metadata = {'size': list(size), 'opaque_native_pixels_exact': True, 'road_pixels_exact': True,
                'outside_scope_pixels_exact': True, 'coverage_dilation': False,
                'changed_pixel_fraction': float((~unchanged).mean()),
                'sources': sources, 'reference_sha256': {name: sha(path) for name, path in paths.items()},
                'recipe_sha256': sha(Path(__file__)), 'spec_sha256': sha(spec_path),
                'map_sha256': sha(output / 'map3d.png'),
                'limits': ['Screen-space painting; no new3D geometry or parallax.',
                           'Antialiased edge reconstruction uses a display-space approximation.',
                           'Baked procedural motes may follow the painting; native live VFX are separate.']}
    (output / 'provenance.json').write_text(json.dumps(metadata, indent=2), encoding='utf-8')
    print(json.dumps(metadata, indent=2))


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--spec', type=Path, required=True)
    parser.add_argument('--out', type=Path, required=True)
    args = parser.parse_args()
    compose(args.spec, args.out)
