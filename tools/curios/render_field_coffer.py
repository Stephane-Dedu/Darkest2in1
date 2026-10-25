"""Render a Tangle cache pair from the complete audited native camp coffer.

The native mesh has a disconnected lid assembly, including its bands and cap.
Rotate the whole assembly, keep the body fixed and reuse native plank paint
on the formerly flat-black underside. No generated or repainted raster art.
Run in Blender; source exports and outputs must remain outside Git.
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

p = argparse.ArgumentParser(description=__doc__)
for key in ('kit', 'source', 'out'):
    p.add_argument('--'+key, type=Path, required=True)
args = p.parse_args(sys.argv[sys.argv.index('--')+1:])
repo = Path(__file__).resolve().parents[2]
args.source, args.out = args.source.resolve(), args.out.resolve()
if args.source.is_relative_to(repo) or args.out.is_relative_to(repo):
    raise ValueError('Private native art must stay outside Git')
if args.out.exists():
    raise ValueError('Use a new output directory')
doc = json.loads((args.source/'scene.json').read_text())
if not doc['source'].endswith('/camp_chest.fbx') or len(doc['objects']) != 1:
    raise ValueError('Use the complete reviewed camp_chest source')
mesh_path = args.source/('mesh-'+doc['objects'][0]['mesh']+'.npz')
d = np.load(mesh_path); original = d['v']; tri = d['tri']
if original.shape != (368,3) or tri.shape != (256,3):
    raise ValueError('Native coffer topology changed; review its islands again')
lid = tri.max(1) < 190
assert lid.sum() == 144 and np.all(lid | (tri.min(1) >= 190))
assert np.all(original[:190,1] >= .2207) and original[190:,1].max() < .2571
cap = np.arange(114,120)
assert np.allclose(original[tri[cap],1],.22074619)
assert np.allclose(d['uv'][tri[cap]], [.521484375,.42236328125])
pivot = np.array([0,.25703746,-.21177803])
angle = math.radians(-100)
rotation = np.array([[1,0,0],[0,math.cos(angle),-math.sin(angle)],
                     [0,math.sin(angle),math.cos(angle)]])
used = original.copy()
used[:190] = ((used[:190]-pivot)@rotation.T+pivot).astype(np.float32)
assert np.array_equal(used[190:],original[190:])
sys.path.insert(0,str(args.kit.resolve()/'tools'))
import bl_lib as L
L.reset()
place,_ = L.place_matrix(args.source,(0,0,0),rot=165,respect_active=True)
matrix = L.CHANGE@place
worlds = {state:(np.c_[v,np.ones(len(v))]@matrix.T)[:,:3]
          for state,v in (('closed',original),('used',used))}
low = np.minimum(worlds['closed'].min(0),worlds['used'].min(0))
high = np.maximum(worlds['closed'].max(0),worlds['used'].max(0))
center = (low+high)/2
extent = max(high[0]-low[0],high[2]-low[2])*1.15
distance = max(12,extent*2)
args.out.mkdir(parents=True)
records = []
for state in ('closed','used'):
    L.reset()
    objects,_,_ = L.build(args.source,place=place,respect_active=True)
    if len(objects) != 1 or len(objects[0].data.vertices) != 368:
        raise ValueError('Incomplete native coffer export')
    obj = objects[0]
    obj.data.vertices.foreach_set('co',worlds[state].astype(np.float32).reshape(-1))
    native = obj.data.materials[0]
    tint = next(node for node in native.node_tree.nodes if node.type == 'MIX_RGB')
    # Shared damp oak/grey iron grade, retaining all native painted grain.
    tint.inputs[2].default_value = (.38,.76,.84,1)
    if state == 'used':
        inside = native.copy();inside.name = 'Tangle native plank underside'
        next(node for node in inside.node_tree.nodes if node.type == 'MIX_RGB').inputs[2].default_value = (.19,.38,.42,1)
        obj.data.materials.append(inside)
        uv = obj.data.uv_layers.active
        for face in cap:
            poly = obj.data.polygons[int(face)];poly.material_index = 1
            for li in poly.loop_indices:
                point = original[obj.data.loops[li].vertex_index]
                uv.data[li].uv = (.327392578125+(point[0]+.3352868557)/.6705737114*(.60546875-.327392578125),
                                  .60498046875+(point[2]+.21177803)/.42355609*(.69677734375-.60498046875))
    camera = L.camera((center[0],center[1]-distance,center[2]+distance*math.tan(math.radians(6))),
                      tuple(center),ortho=float(extent),res=(1024,1024))
    scene = bpy.context.scene
    scene.render.threads_mode='FIXED';scene.render.threads=2;scene.cycles.seed=0
    image = args.out/(state+'.png');L.render(image,samples=4,transparent=True)
    if not image.is_file():raise RuntimeError('Missing coffer state render')
    points = np.array([[1024*q.x,1024*(1-q.y)] for q in
        (world_to_camera_view(scene,camera,Vector(v)) for v in worlds['closed'][190:])])
    lo,hi = points.min(0),points.max(0)
    records.append({'state':state,'file':image.name,'sha256':hashlib.sha256(image.read_bytes()).hexdigest(),
        'foot':[(lo[0]+hi[0])/2,hi[1]],'scale_landmarks':[[lo[0],(lo[1]+hi[1])/2],[hi[0],(lo[1]+hi[1])/2]]})
files = [args.source/'scene.json',mesh_path]
files += [(args.source/m['texture']).resolve() for m in doc['materials'].values() if m.get('texture')]
(args.out/'provenance.json').write_text(json.dumps({
    'art_id':'locked_display_cabinet_tangle','curio_id':'locked_display_cabinet','region':'dd2_forest',
    'native_triangles':256,'native_vertices':368,'lid_vertices':190,'lid_triangles':144,
    'body_vertices_fixed':178,'triangle_order_and_winding_retained':True,
    'used_UV_changes':'Only6lid underside triangles reuse native lid plank paint; other UVs unchanged.',
    'hinge':pivot.tolist(),'angle_degrees':-100,'body_tint':[.38,.76,.84],'underside_tint':[.19,.38,.42],
    'generated_art':False,'installed':False,'native_qa':False,
    'notice':'Native geometry/paint emission study with explicit lid articulation, underside UV adaptation and material grade. Not a shipped native state or game screenshot.',
    'files':[{'path':str(path),'sha256':hashlib.sha256(path.read_bytes()).hexdigest()} for path in files],
    'recipe_sha256':hashlib.sha256(Path(__file__).read_bytes()).hexdigest(),'outputs':records
},indent=2))
print('NATIVE_FIELD_COFFER_PAIR_READY',args.out,flush=True)
