"""Render a Foetor food-curio pair from the complete native meat_trough export.

Both states retain the complete original timber body. The searched state hides
only the separate native meat assembly. No repaint, deformation or new geometry.
Native source placement and paint remain authoritative. This offline regional
presentation does not change the Makeshift Dining Table's gameplay identity.
Run in Blender; keep native art and output outside Git.
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


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    for key in ('kit', 'source', 'out'):
        parser.add_argument('--'+key, type=Path, required=True)
    args = parser.parse_args(sys.argv[sys.argv.index('--')+1:])
    source, out = args.source.resolve(), args.out.resolve()
    repo = Path(__file__).resolve().parents[2]
    if source.is_relative_to(repo) or out.is_relative_to(repo):
        raise ValueError('Private native art must stay outside Git')
    if out.exists():
        raise ValueError('Use a new output directory')
    doc = json.loads((source/'scene.json').read_text())
    expected = {'meat_trough':(689,466), 'meat_trough_meat_geo':(1285,1473)}
    if not doc['source'].endswith('/meat_trough.fbx') or len(doc['objects']) != 2:
        raise ValueError('Use the complete reviewed native meat_trough source')
    if {obj['name'] for obj in doc['objects']} != set(expected):
        raise ValueError('Native trough assembly changed')
    dependencies = [source/'scene.json']
    for obj in doc['objects']:
        path = source/('mesh-'+obj['mesh']+'.npz')
        mesh = np.load(path)
        if (len(mesh['v']),len(mesh['tri'])) != expected[obj['name']] or not obj['active'] or not obj['enabled']:
            raise ValueError('Review changed or incomplete native source')
        dependencies.append(path)
    dependencies += [(source/m['texture']).resolve() for m in doc['materials'].values() if m.get('texture')]
    sys.path.insert(0,str(args.kit.resolve()/'tools'))
    import bl_lib as native
    native.reset()
    place, _ = native.place_matrix(source,(0,0,0),rot=0,respect_active=True)
    objects, lo, hi = native.build(source,place=place,respect_active=True)
    if len(objects) != 2:
        raise ValueError('Native food assembly is incomplete')
    center = (lo+hi)/2
    extent = max(hi[0]-lo[0],hi[2]-lo[2])*1.15
    distance = max(12,extent*2)
    body = next(obj for obj in objects if obj.name == 'meat_trough')
    fixed = np.array([tuple(vertex.co) for vertex in body.data.vertices])
    out.mkdir(parents=True)
    records = []
    for state in ('closed','used'):
        native.reset()
        objects, _, _ = native.build(source,place=place,respect_active=True,
                                      exclude=('meat_geo',) if state == 'used' else ())
        body = next(obj for obj in objects if obj.name == 'meat_trough')
        if not np.array_equal(fixed,np.array([tuple(vertex.co) for vertex in body.data.vertices])):
            raise ValueError('Native timber body moved between states')
        if len(objects) != (2 if state == 'closed' else 1):
            raise ValueError('Wrong food state parts')
        camera = native.camera((center[0],center[1]-distance,center[2]+distance*math.tan(math.radians(6))),
                               tuple(center),ortho=float(extent),res=(1024,1024))
        scene = bpy.context.scene
        scene.render.threads_mode = 'FIXED'
        scene.render.threads = 2
        scene.cycles.seed = 0
        image = out/(state+'.png')
        native.render(image,samples=4,transparent=True)
        if not image.is_file():
            raise RuntimeError('Missing native trough state render')
        points = np.array([[1024*q.x,1024*(1-q.y)] for q in
                           (world_to_camera_view(scene,camera,Vector(point)) for point in fixed)])
        lower, upper = points.min(0),points.max(0)
        records.append({'state':state,'file':image.name,'sha256':hashlib.sha256(image.read_bytes()).hexdigest(),
                        'native_parts':len(objects),'native_triangles':1939 if state == 'closed' else 466,
                        'foot':[(lower[0]+upper[0])/2,upper[1]],
                        'scale_landmarks':[[lower[0],(lower[1]+upper[1])/2],[upper[0],(lower[1]+upper[1])/2]]})
    (out/'provenance.json').write_text(json.dumps({
        'art_id':'makeshift_dining_table_foetor','curio_id':'makeshift_dining_table','region':'dd2_farm',
        'native_body_vertices':689,'native_body_triangles':466,'native_food_vertices':1285,'native_food_triangles':1473,
        'geometry_UVs_paint_tint_and_transforms_retained':True,'body_vertices_identical_between_states':True,
        'used_state':'Only the complete separate meat assembly is hidden; all timber retained.',
        'generated_art':False,'installed':False,'native_qa':False,
        'notice':'Offline complete native source adaptation. No food, herb, disease, RNG or spawn-rule change. Native shader, actors, supply handling and regional fallback still need authorised QA.',
        'files':[{'path':str(path),'sha256':hashlib.sha256(path.read_bytes()).hexdigest()}for path in dependencies],
        'recipe_sha256':hashlib.sha256(Path(__file__).read_bytes()).hexdigest(),'outputs':records
    },indent=2))
    print('NATIVE_FOETOR_TROUGH_PAIR_READY',out,flush=True)


if __name__ == '__main__':
    main()
