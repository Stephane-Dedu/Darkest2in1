"""Render complete audited DD2 curio source bodies without remapping their paint.

Run in Blender. Each source keeps its original mesh, UV, material scale, offset
and tint. This is a closed-body study, not a used-state implementation or pack.
"""
import argparse
import hashlib
import json
import math
import sys
from pathlib import Path

import bpy
import numpy as np
from bpy_extras.object_utils import world_to_camera_view
from mathutils import Vector

parser = argparse.ArgumentParser(description=__doc__)
parser.add_argument('--kit', type=Path, required=True)
parser.add_argument('--sources', type=Path, required=True)
parser.add_argument('--out', type=Path, required=True)
parser.add_argument('--ids', nargs='*')
args = parser.parse_args(sys.argv[sys.argv.index('--') + 1:])
args.out = args.out.resolve()
if args.out.is_relative_to(Path(__file__).resolve().parents[2]):
    raise ValueError('Private source art must stay outside the repository')
if args.out.exists() and any(args.out.iterdir()):
    raise ValueError('Use a new source study folder')
sys.path.insert(0, str(args.kit / 'tools'))
import bl_lib as L

SOURCES = {'stack_of_books': 'books', 'pile_of_bones': 'bones', 'sack': 'sack', 'crate': 'crate',
           'travellers_tent': 'tent', 'bookshelf': 'bookcase', 'giant_fish_carcass': 'fish'}
if args.ids and set(args.ids) - SOURCES.keys():
    raise ValueError('Unknown reviewed source identity')
args.out.mkdir(parents=True)
manifest = {'purpose': 'Complete native source bodies, closed-state studies only', 'installed': False,
            'native_verified': False, 'recipe_sha256': hashlib.sha256(Path(__file__).read_bytes()).hexdigest(),
            'camera': {'projection': 'orthographic', 'elevation_degrees': 6, 'resolution': [1024, 1024]}, 'entries': []}
for key, part in SOURCES.items():
    if args.ids and key not in args.ids:
        continue
    folder = args.sources / 'parts' / part
    L.reset()
    lo, hi = L.bbox(folder, respect_active=True)
    if not np.isfinite(lo).all() or np.any(hi <= lo):
        raise ValueError('Invalid source body bounds')
    transform, _ = L.place_matrix(folder, (0, 0, 0), respect_active=True)
    objects, lo, hi = L.build(folder, place=transform, respect_active=True)
    if not objects:
        raise ValueError('Empty native source body')
    source_doc = json.loads((folder / 'scene.json').read_text())
    # The shared preview helper defaults to.4. Retain each audited material's
    # own alpha cutoff rather than silently broadening its painted cutouts.
    for material in source_doc['materials'].values():
        cutoff = material.get('floats', {}).get('_Cutoff', .4)
        for ob in objects:
            for slot in ob.material_slots:
                if slot.material and slot.material.name == material['name']:
                    for node in slot.material.node_tree.nodes:
                        if node.type == 'MATH' and node.operation == 'GREATER_THAN':
                            node.inputs[1].default_value = cutoff
    center = (lo + hi) / 2
    extent = max(hi[0] - lo[0], hi[2] - lo[2]) * 1.14
    distance = max(12, extent * 2)
    camera = L.camera((center[0], center[1] - distance, center[2] + distance * math.tan(math.radians(6))),
                      tuple(center), ortho=float(extent), res=(1024, 1024))
    scene = bpy.context.scene
    scene.render.threads_mode = 'FIXED'
    scene.render.threads = 2
    path = args.out / (key + '-closed.png')
    L.render(path, samples=4, transparent=True)
    scene = bpy.context.scene
    floor = world_to_camera_view(scene, camera, Vector((0, 0, lo[2])))
    texture_paths = {(folder / m['texture']).resolve() for m in source_doc['materials'].values() if m.get('texture')}
    files = [folder / 'scene.json'] + sorted(folder.glob('mesh-*.npz')) + sorted(texture_paths)
    record = {'id': key, 'source_part': part, 'file': path.name, 'size': [1024, 1024],
              'source_geometry_uv_materials_retained': True, 'used_state': 'Not produced by this study',
              'lighting': 'Unlit emission preview of native texture and tint; native scene lighting not reproduced',
              'floor_projection': [1024 * floor.x, 1024 * (1 - floor.y)],
              'native_bounds': [lo.tolist(), hi.tolist()],
              'files': [{'path': str(p), 'sha256': hashlib.sha256(p.read_bytes()).hexdigest()} for p in files],
              'output_sha256': hashlib.sha256(path.read_bytes()).hexdigest()}
    manifest['entries'].append(record)
    (args.out / 'manifest.json').write_text(json.dumps(manifest, indent=2), encoding='utf-8')
    print('CURIO_NATIVE_SOURCE', key, flush=True)
