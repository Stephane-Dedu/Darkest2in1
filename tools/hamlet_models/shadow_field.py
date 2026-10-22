"""Bake a private, geometry-derived Hamlet ground-shadow field with Blender.

Study data only: no game deployment or native shader parity is implied. Inputs
are matching Core LayoutExport/ModelReviewExport documents. Original paint and
geometry remain untouched. Opaque service meshes/terrain cast shadows; alpha
scenery, background cottages and trees are excluded rather than made into boxes.
"""
import argparse
import hashlib
import json
import math
import struct
import sys
import time
import zlib
from pathlib import Path

import numpy as np
from mathutils import Vector
from mathutils.bvhtree import BVHTree


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def source_hash(path):
    return hashlib.sha256(path.read_text(encoding='utf-8').replace('\r\n', '\n').encode()).hexdigest()


def add(vertices, faces, points, indices):
    start = len(vertices)
    vertices.extend(Vector((x, z, y)) for x, y, z in np.asarray(points).reshape(-1, 3))
    faces.extend(tuple(int(i) + start for i in tri) for tri in np.asarray(indices).reshape(-1, 3))


def directions(unity, degrees, samples):
    key = Vector((unity[0], unity[2], unity[1])).normalized()
    if key.z <= 0:
        raise ValueError('The study sun must be above the horizon.')
    tangent = key.cross(Vector((0, 0, 1)))
    if tangent.length < .001:
        tangent = Vector((1, 0, 0))
    tangent.normalize()
    bitangent = key.cross(tangent).normalized()
    radius = math.tan(math.radians(degrees / 2))
    return [(key + (tangent * math.cos(i * math.tau / samples)
                    + bitangent * math.sin(i * math.tau / samples)) * radius).normalized()
            for i in range(samples)]


def visible(tree, point, rays):
    origin = point + Vector((0, 0, .06))
    return sum(tree.ray_cast(origin + ray * .015, ray, 300)[0] is None for ray in rays) / len(rays)


def png(path, shade):
    """A data PNG: top row is MaxZ; Unity's bottom row is MinZ. No gamma tag."""
    values = np.rint(np.clip(shade, 0, 1) * 255).astype(np.uint8)
    height, width = values.shape
    def chunk(kind, data):
        return struct.pack('>I', len(data)) + kind + data + struct.pack('>I', zlib.crc32(kind + data))
    rows = b''.join(b'\0' + row.tobytes() for row in values[::-1])
    payload = b'\x89PNG\r\n\x1a\n' + chunk(b'IHDR', struct.pack('>IIBBBBB', width, height, 8, 0, 0, 0, 0))
    path.write_bytes(payload + chunk(b'IDAT', zlib.compress(rows)) + chunk(b'IEND', b''))
    return hashlib.sha256(values[::-1].tobytes()).hexdigest()


def inputs(layout_path, review_path, repo):
    layout = json.loads(layout_path.read_text(encoding='utf-8'))
    review = json.loads(review_path.read_text(encoding='utf-8'))
    if review.get('Version') != 1 or set(review['Models']) != {s['Id'] for s in layout['Sites']}:
        raise ValueError('Use the complete ten-service Core review.')
    if layout['PaintSources'] != review['CoreSources']:
        raise ValueError('Layout and model review have different Core sources.')
    for name, digest in review['CoreSources'].items():
        path = (repo / name).resolve()
        if repo not in path.parents or source_hash(path) != digest:
            raise ValueError('Stale Core layout/model source: ' + name)
    if source_hash(repo / 'src/DarkestDungeon3.Core/Presentation/GltfModel.cs') != review['ReaderHash']:
        raise ValueError('Stale Core model reader.')
    package = Path(next(iter(review['Images'].values()))['Source']).parent
    for name, digest in review['SourceFiles'].items():
        if Path(name).name != name or sha(package / name) != digest:
            raise ValueError('Changed source package: ' + name)
    return layout, review


def bake(args):
    repo = Path(__file__).resolve().parents[2]
    out = args.out.resolve()
    if out == repo or repo in out.parents or out.exists():
        raise ValueError('Use a new private output directory outside Git.')
    if not 64 <= args.size <= 4096 or not 1 <= args.samples <= 16 or not 0 <= args.disc <= 6:
        raise ValueError('Use size64-4096, samples1-16 and a sun disc0-6 degrees.')
    if not .35 <= args.floor <= 1 or not all(math.isfinite(v) for v in args.direction):
        raise ValueError('Use a finite direction and a shadow floor0.35-1.')
    layout, review = inputs(args.layout.resolve(), args.review.resolve(), repo)
    terrain = layout['Terrain']
    points = np.asarray(terrain['Positions']).reshape(-1, 3)
    minx, minz = points[:, [0, 2]].min(0)
    maxx, maxz = points[:, [0, 2]].max(0)
    receiver_v, receiver_f, caster_v, caster_f, platforms = [], [], [], [], []
    for data, indices in [(terrain, terrain['Ground'] + terrain['Rock'])] + [
            (terrain[k], terrain[k]['Indices']) for k in ('Paving', 'Masonry', 'Stairs')]:
        add(receiver_v, receiver_f, data['Positions'], indices)
        add(caster_v, caster_f, data['Positions'], indices)
    for key, model in review['Models'].items():
        for part in model['Primitives']:
            material = model['Materials'][part['Material']]
            if material['Mask'] or material['Blend'] or material['Light']:
                continue
            add(caster_v, caster_f, part['Positions'], part['Indices'])
            if any(s in (part['Name'] or '').lower() for s in ('terrace', 'stone foundation', 'camp ground')):
                add(receiver_v, receiver_f, part['Positions'], part['Indices'])
                platforms.append(key + '/' + part['Name'])
    receive = BVHTree.FromPolygons(receiver_v, receiver_f, all_triangles=True)
    cast = BVHTree.FromPolygons(caster_v, caster_f, all_triangles=True)
    rays = directions(args.direction, args.disc, args.samples)
    shade = np.ones((args.size, args.size), dtype=np.float32)
    coverage = 0
    started = time.time()
    for row in range(args.size):
        z = minz + (row + .5) / args.size * (maxz - minz)
        for column in range(args.size):
            x = minx + (column + .5) / args.size * (maxx - minx)
            point, _, _, _ = receive.ray_cast(Vector((x, z, 120)), Vector((0, 0, -1)), 200)
            if point is None:
                continue
            coverage += 1
            shade[row, column] = args.floor + (1 - args.floor) * visible(cast, point, rays)
        if row % 128 == 0:
            print('SHADOW ROW', row, 'of', args.size, flush=True)
    assert np.isfinite(shade).all() and args.floor - 1e-6 <= shade.min() <= 1 and shade.max() <= 1
    out.mkdir(parents=True)
    pixel_hash = png(out / 'ground-shadow.png', shade)
    np.savez_compressed(out / 'ground-shadow.npz', shade=shade)
    (out / 'manifest.json').write_text(json.dumps({
        'notice': __doc__, 'size': args.size,
        'bounds': dict(MinX=float(minx), MaxX=float(maxx), MinZ=float(minz), MaxZ=float(maxz)),
        'rows': 'PNG top=MaxZ, native bottom=MinZ; pixel-centre sampling; grayscale data, no gamma tag',
        'direction_unity': args.direction, 'sun_disc_degrees': args.disc, 'samples': args.samples,
        'floor': args.floor, 'covered_pixels': coverage, 'receiver_platforms': platforms,
        'receiver_triangles': len(receiver_f), 'caster_triangles': len(caster_f),
        'layout_sha256': sha(args.layout), 'review_sha256': sha(args.review),
        'source_glb_hashes': {k: v['SourceGlbHash'] for k, v in review['Models'].items()},
        'png_sha256': sha(out / 'ground-shadow.png'), 'decoded_gray_sha256': pixel_hash,
        'seconds': time.time() - started, 'native_or_installed': False}, indent=2), encoding='utf-8')
    print('SHADOW FIELD COMPLETE', coverage, 'pixels;', round(time.time() - started, 2), 'seconds', flush=True)


def self_test():
    # A box above a plane must cast toward -X for a sun toward +X. The opposite
    # side stays clear. This catches axis swaps, reversed rays and self-contact.
    plane = [(-5, -5, 0), (5, -5, 0), (5, 5, 0), (-5, 5, 0)]
    top = [(-.5, -.5, 2), (.5, -.5, 2), (.5, .5, 2), (-.5, .5, 2)]
    faces = [(0, 1, 2), (0, 2, 3)]
    empty = BVHTree.FromPolygons(plane, faces, all_triangles=True)
    box = BVHTree.FromPolygons(plane + top, faces + [(4, 5, 6), (4, 6, 7)], all_triangles=True)
    rays = directions((1, 1, 0), 0, 1)
    assert visible(empty, Vector((-2, 0, 0)), rays) == 1
    assert visible(box, Vector((-2, 0, 0)), rays) == 0
    assert visible(box, Vector((2, 0, 0)), rays) == 1
    assert visible(box, Vector((-2, 2, 0)), rays) == 1
    assert directions((0, 1, 0), 0, 1)[0].z == 1
    print('PASS procedural occluder, ray direction, self-contact, outside shadow and vertical sun')


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--self-test', action='store_true')
    for name in ('layout', 'review', 'out'):
        parser.add_argument('--' + name, type=Path)
    parser.add_argument('--size', type=int, default=1024)
    parser.add_argument('--direction', nargs=3, type=float, default=[.60, .45, .55], help='Unity X,Y-up,Z direction toward the sun')
    parser.add_argument('--disc', type=float, default=3)
    parser.add_argument('--samples', type=int, default=4)
    parser.add_argument('--floor', type=float, default=.6)
    args = parser.parse_args(sys.argv[sys.argv.index('--') + 1:])
    if args.self_test:
        self_test()
    else:
        if any(getattr(args, name) is None for name in ('layout', 'review', 'out')):
            parser.error('Pass --layout, --review and --out.')
        bake(args)
