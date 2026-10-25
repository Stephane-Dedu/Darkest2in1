"""Render a native trolley card with generated paint confined to its specimen jar.

Run in Blender. The source is a single painted plane, not separate glass meshes.
Subdivide its UV square without moving the native body; both states use the
same subdivision. Only two jar pieces bind the supplied edited full atlas.
No raster paint is altered here. All private inputs/outputs stay outside Git.
"""
import argparse
import hashlib
import json
import math
from pathlib import Path
import sys

import bpy
import numpy as np

parser = argparse.ArgumentParser(description=__doc__)
for name in ('kit', 'source', 'used-texture', 'out'):
    parser.add_argument('--' + name, type=Path, required=True)
parser.add_argument('--resolution', type=int, default=1024, choices=(512, 1024, 2048))
args = parser.parse_args(sys.argv[sys.argv.index('--') + 1:])
repo = Path(__file__).resolve().parents[2]
for name in ('source', 'used_texture', 'out'):
    path = getattr(args, name).resolve()
    if path.is_relative_to(repo):
        raise ValueError('Native and generated art must stay outside Git')
    setattr(args, name, path)
if args.out.exists():
    raise ValueError('Preserve previous renders; select a new output folder')
doc = json.loads((args.source / 'scene.json').read_text())
if 'plague_doctor_lab_trolley' not in doc['source'] or len(doc['objects']) != 1:
    raise ValueError('Use the complete audited native Plague Doctor trolley card')
mesh_path = args.source / ('mesh-' + doc['objects'][0]['mesh'] + '.npz')
mesh = np.load(mesh_path)
if mesh['v'].shape != (4, 3) or mesh['tri'].shape != (2, 3) or mesh['uv'].shape != (4, 2):
    raise ValueError('The reviewed native card geometry changed')
basis = np.linalg.lstsq(np.c_[mesh['uv'], np.ones(4)], mesh['v'], rcond=None)[0]
error = float(np.abs(np.c_[mesh['uv'], np.ones(4)] @ basis - mesh['v']).max())
if error > 1e-7:
    raise ValueError('Source positions are not an affine UV plane')
# These reviewed atlas coordinates leave the foreground small jar, rack,
# tabletop, frame, lower supplies and wheels on their ORIGINAL native paint.
regions = [('native-top-left', (0, 0, 665, 216), False),
           ('native-foreground', (0, 216, 746, 385), False),
           ('native-body', (0, 385, 1024, 1024), False),
           ('native-right', (966, 0, 1024, 385), False),
           ('jar-upper', (665, 0, 966, 216), True),
           ('jar-lower', (746, 216, 966, 385), True)]
assert sum((r-l)*(b-t) for _, (l,t,r,b), _ in regions) == 1024**2
sys.path.insert(0, str(args.kit.resolve() / 'tools'))
import bl_lib as L

def cutoff(material):
    for node in material.node_tree.nodes:
        if node.type == 'MATH' and node.operation == 'GREATER_THAN':
            node.inputs[1].default_value = .5

args.out.mkdir(parents=True)
outputs = []
for state in ('reference', 'closed', 'used'):
    L.reset()
    transform, _ = L.place_matrix(args.source, (0, 0, 0), rot=90, respect_active=True)
    objects, lo, hi = L.build(args.source, place=transform, respect_active=True)
    if len(objects) != 1:
        raise ValueError('Incomplete native trolley body')
    original = objects[0]
    native = original.data.materials[0]
    cutoff(native)
    if state != 'reference':
        bpy.data.objects.remove(original, do_unlink=True)
        paint = dict(next(iter(doc['materials'].values())))
        paint['name'] = 'Generated searched specimen jar ONLY'
        paint['texture'] = str(args.used_texture)
        generated = L.make_mat(args.source, 'generated-specimen-jar', paint)
        cutoff(generated)
        for name, (left, top, right, bottom), is_jar in regions:
            uv = np.array([[left/1024, 1-bottom/1024], [left/1024, 1-top/1024],
                           [right/1024, 1-top/1024], [right/1024, 1-bottom/1024]])
            positions = np.c_[uv, np.ones(4)] @ basis
            world = np.c_[positions, np.ones(4)] @ (L.CHANGE @ transform).T
            triangles = mesh['tri'][:, ::-1]
            data = bpy.data.meshes.new(name)
            data.from_pydata(world[:, :3].tolist(), [], triangles.tolist())
            data.update()
            obj = bpy.data.objects.new(name, data)
            bpy.context.collection.objects.link(obj)
            data.materials.append(generated if state == 'used' and is_jar else native)
            layer = data.uv_layers.new(name='UVMap')
            layer.data.foreach_set('uv', uv[triangles.reshape(-1)].reshape(-1).astype(np.float32))
    center = (lo + hi) / 2
    extent = max(hi[0]-lo[0], hi[2]-lo[2]) * 1.14
    distance = max(12, extent*2)
    L.camera((center[0], center[1]-distance, center[2]+distance*math.tan(math.radians(6))),
             tuple(center), ortho=float(extent), res=(args.resolution, args.resolution))
    scene = bpy.context.scene
    scene.render.threads_mode = 'FIXED'; scene.render.threads = 2; scene.cycles.seed = 0
    path = args.out / (state + '.png')
    L.render(path, samples=4, transparent=True)
    if not path.is_file():
        raise RuntimeError('Render did not produce a state image')
    outputs.append({'state': state, 'file': path.name,
                    'sha256': hashlib.sha256(path.read_bytes()).hexdigest(),
                    'card_triangles': 2 if state == 'reference' else 12})
files = [args.source/'scene.json', mesh_path, args.used_texture]
files += [(args.source/m['texture']).resolve() for m in doc['materials'].values() if m.get('texture')]
(args.out/'provenance.json').write_text(json.dumps({
    'id': 'alchemy_table_sprawl', 'curio_id': 'alchemy_table', 'region': 'dd2_city',
    'status': 'Offline candidate only; no native QA or installation',
    'fixed_body_uses_original_native_paint': True, 'manually_painted_raster_pixels': False,
    'source_affine_position_error_metres': error, 'cutoff': .5, 'yaw': 90, 'elevation': 6,
    'notice': 'Both states subdivide the original card. Only the large jar uses generated paint when used. Native emission preview is not native scene lighting.',
    'regions': [{'name': n, 'atlas_bounds': b, 'generated_when_used': g} for n,b,g in regions],
    'files': [{'path': str(p), 'sha256': hashlib.sha256(p.read_bytes()).hexdigest()} for p in files],
    'recipe_sha256': hashlib.sha256(Path(__file__).read_bytes()).hexdigest(), 'outputs': outputs
}, indent=2))
print('CURIO_APOTHECARY_PAIR_READY', args.out, flush=True)
