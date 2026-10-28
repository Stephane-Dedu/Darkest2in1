"""Render a complete native Foetor serving stall and a searched food-stock state.

All source vertices, UVs, paint, tint and pose stay fixed. Used visibility hides
three complete geometric food members after joining exact-position seams for
analysis only. No source welding, arbitrary face cuts, generation or repaint.
Private source assets and outputs remain outside Git. Cull2 is modeled; this
emission reference does not reconstruct full Unity lighting, fog or dithering.
"""
import argparse
import hashlib
import json
import math
from pathlib import Path
import sys

import bpy
from bpy_extras.object_utils import world_to_camera_view
from mathutils import Vector
import numpy as np


def component_roots(vertices, triangles):
    unique, inverse = np.unique(vertices, axis=0, return_inverse=True)
    parent = np.arange(len(unique))

    def find(index):
        while parent[index] != index:
            parent[index] = parent[parent[index]]
            index = parent[index]
        return int(index)

    for face in inverse[triangles]:
        root = find(face[0])
        for vertex in face[1:]:
            parent[find(vertex)] = root
    return np.array([find(face[0]) for face in inverse[triangles]])


parser = argparse.ArgumentParser(description=__doc__)
for key in ('kit', 'source', 'out'):
    parser.add_argument('--'+key, type=Path, required=True)
args = parser.parse_args(sys.argv[sys.argv.index('--')+1:])
repo = Path(__file__).resolve().parents[2]
args.source, args.out = args.source.resolve(), args.out.resolve()
if any(path.is_relative_to(repo) for path in (args.source, args.out)) or args.out.exists():
    raise ValueError('Use private inputs and a fresh private output directory')
expected_source_hashes = {'scene.json': 'a691190717a0e0bd30649c5dd129e56b2756f15c5d587eb437541ac333446414', 'mesh--4641825131334258438.npz': 'f05e939daa24b97ec103b9f98c81a954772177d2bff3fd87d104d8df0e243d58'}
if not expected_source_hashes:
    raise ValueError('Reviewed native source pins are required')
for name, expected in expected_source_hashes.items():
    if hashlib.sha256((args.source/name).read_bytes()).hexdigest() != expected:
        raise ValueError('Reviewed complete native serving stall changed: '+name)
doc = json.loads((args.source/'scene.json').read_text())
if not doc['source'].endswith('/farm_foodcart_02.fbx') or len(doc['objects']) != 1 or doc['skipped']:
    raise ValueError('Use the complete reviewed FBX, not a scene batch fragment')
native_texture_guard = Path(next(iter(doc['materials'].values()))['texture']).resolve()
if hashlib.sha256(native_texture_guard.read_bytes()).hexdigest() != '1ec5080ec3ba722569ef426dc58f13b25423ed96b7ded7f1dc01d9f71eab619c':
    raise ValueError('Complete native foodcart paint changed')
entry = doc['objects'][0]
if entry['name'] != 'farm_foodcart_02' or not entry['active'] or not entry['enabled'] or entry.get('batched'):
    raise ValueError('Complete active FBX renderer required')
data = np.load(args.source/('mesh-'+entry['mesh']+'.npz'))
v, triangles = data['v'], data['tri']
if v.shape != (1773, 3) or triangles.shape != (1455, 3) or data['uv'].shape != (1773, 2):
    raise ValueError('Full native topology or UVs changed')
roots = component_roots(v, triangles)
hidden = np.isin(roots, [510, 531, 546])
if len(np.unique(roots)) != 25 or hidden.sum() != 437 or len(np.unique(triangles[hidden])) != 411:
    raise ValueError('Review the complete food-stock members again')
fixed = np.setdiff1d(np.arange(len(v)), np.unique(triangles[hidden]))
if len(fixed) != 1362:
    raise ValueError('Fixed cart/body membership changed')
sys.path.insert(0, str(args.kit.resolve()/'tools'))
import bl_lib as L

L.reset()
place, _ = L.place_matrix(args.source, (0, 0, 0), rot=210, respect_active=True)
args.out.mkdir(parents=True)
records = []
for state in ('closed', 'used'):
    L.reset()
    objects, low, high = L.build(args.source, place=place, respect_active=True)
    if len(objects) != 1 or len(objects[0].data.vertices) != 1773 or len(objects[0].data.polygons) != 1455:
        raise ValueError('Incomplete source model')
    obj = objects[0]
    for mat in obj.data.materials:
        nt = mat.node_tree
        out = next(node for node in nt.nodes if node.type == 'OUTPUT_MATERIAL')
        surface = out.inputs['Surface'].links[0].from_socket
        geometry = nt.nodes.new('ShaderNodeNewGeometry')
        transparent = nt.nodes.new('ShaderNodeBsdfTransparent')
        mix = nt.nodes.new('ShaderNodeMixShader')
        nt.links.new(geometry.outputs['Backfacing'], mix.inputs[0])
        nt.links.new(surface, mix.inputs[1])
        nt.links.new(transparent.outputs[0], mix.inputs[2])
        nt.links.new(mix.outputs[0], out.inputs[0])
    if state == 'used':
        mat = bpy.data.materials.new('Whole food members invisible after search')
        mat.use_nodes = True
        nt = mat.node_tree
        nt.nodes.clear()
        tr = nt.nodes.new('ShaderNodeBsdfTransparent')
        out = nt.nodes.new('ShaderNodeOutputMaterial')
        nt.links.new(tr.outputs[0], out.inputs[0])
        obj.data.materials.append(mat)
        for face in np.where(hidden)[0]:
            obj.data.polygons[int(face)].material_index = 1
    center = (low+high)/2
    extent = max(high[0]-low[0], high[2]-low[2])*1.15
    distance = max(12, extent*2)
    camera = L.camera((center[0], center[1]-distance,
        center[2]+distance*math.tan(math.radians(10))), tuple(center), ortho=float(extent), res=(1024, 1024))
    scene = bpy.context.scene
    scene.render.threads_mode = 'FIXED'
    scene.render.threads = 2
    scene.cycles.seed = 0
    path = args.out/(state+'.png')
    L.render(path, samples=4, transparent=True)
    bpy.context.view_layer.update()
    world = [obj.matrix_world @ obj.data.vertices[int(i)].co for i in fixed]
    points = np.array([[1024*q.x, 1024*(1-q.y)] for q in
        (world_to_camera_view(scene, camera, point) for point in world)])
    lo, hi = points.min(0), points.max(0)
    records.append({'state': state, 'file': path.name,
        'sha256': hashlib.sha256(path.read_bytes()).hexdigest(),
        'foot': [(lo[0]+hi[0])/2, hi[1]],
        'scale_landmarks': [[lo[0], (lo[1]+hi[1])/2], [hi[0], (lo[1]+hi[1])/2]]})
if records[0]['foot'] != records[1]['foot'] or records[0]['scale_landmarks'] != records[1]['scale_landmarks']:
    raise ValueError('Fixed body/camera registration drift')
native_texture = Path(next(iter(doc['materials'].values()))['texture']).resolve()
(args.out/'provenance.json').write_text(json.dumps({
    'art_id': 'cc_food_cart_foetor_design', 'canonical_id': 'cc_food_cart',
    'design_context': 'dd2_farm, paired with unchanged canonical Warrens source',
    'native_vertices': 1773, 'native_triangles': 1455, 'native_UV0': 1773,
    'all_source_geometry_and_paint_retained': True, 'full_native_paint': [1024, 1024],
    'FBX_white_tint_unchanged': True, 'component_memberships': 25,
    'used_hidden_whole_geometric_food_roots': [510, 531, 546],
    'hidden_vertices': 411, 'hidden_triangles': 437, 'fixed_body_vertices': 1362,
    'all_native_vertices_remain_in_both_state_meshes_visibility_only_changes': True,
    'no_source_welding_or_arbitrary_face_selection': True,
    'pose_yaw': 210, 'camera_elevation': 10, 'generated_art': False,
    'native_qa': False, 'accepted': False, 'installed': False,
    'native_texture': str(native_texture),
    'native_texture_sha256': hashlib.sha256(native_texture.read_bytes()).hexdigest(),
    'recipe_sha256': hashlib.sha256(Path(__file__).read_bytes()).hexdigest(),
    'outputs': records}, indent=2))
print('NATIVE_SERVING_STALL_PAIR_UNACCEPTED', args.out)
