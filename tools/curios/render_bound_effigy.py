"""Adapt a complete native skull warning into a regional Troubling Effigy pair.

Preserve every native face, UV and painted surface. Rotate the whole crown,
cloth and crossbar together while the complete post stays fixed. This native
Foetor mesh is an outdoor body study for existing Tangle effigy locations,
not evidence of a new shipped encounter or an original native animation.
Run in Blender. Keep native sources and rendered art outside Git.
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
    for key in ('kit','source','out'):
        parser.add_argument('--'+key,type=Path,required=True)
    args = parser.parse_args(sys.argv[sys.argv.index('--')+1:])
    source, out = args.source.resolve(), args.out.resolve()
    repo = Path(__file__).resolve().parents[2]
    if source.is_relative_to(repo) or out.is_relative_to(repo):
        raise ValueError('Private native art must stay outside Git')
    if out.exists():
        raise ValueError('Use a new output directory')
    doc = json.loads((source/'scene.json').read_text())
    if not doc['source'].endswith('/farm_scarecrow_01.fbx') or len(doc['objects']) != 1:
        raise ValueError('Use the complete reviewed native skull warning')
    mesh_path = source/('mesh-'+doc['objects'][0]['mesh']+'.npz')
    data = np.load(mesh_path)
    vertices, triangles = data['v'], data['tri']
    if vertices.shape != (1827,3) or triangles.shape != (2518,3):
        raise ValueError('Native topology changed; review the entire assembly')
    # Weld only for classifying complete parts. Keep original UV-seam vertices
    # and triangles in the actual model.
    parent = np.arange(len(vertices))

    def find(index):
        while parent[index] != index:
            parent[index] = parent[parent[index]]
            index = int(parent[index])
        return index

    points = {}
    for i, point in enumerate(vertices):
        key = tuple(np.round(point,6))
        if key in points:
            parent[find(i)] = find(points[key])
        else:
            points[key] = i
    for face in triangles:
        for index in face[1:]:
            parent[find(int(index))] = find(int(face[0]))
    groups = {}
    for i in range(len(vertices)):
        groups.setdefault(find(i),[]).append(i)
    if len(groups) != 4:
        raise ValueError('Review changed native pole/crown components')
    body = np.zeros(len(vertices),dtype=bool)
    for indices in groups.values():
        if vertices[indices,1].min() < 1:
            body[indices] = True
    body_faces = body[triangles].all(1)
    head_faces = (~body)[triangles].all(1)
    if body.sum() != 64 or body_faces.sum() != 78 or head_faces.sum() != 2440 or not np.all(body_faces | head_faces):
        raise ValueError('Incomplete native crown/post partition')
    pivot = np.array([.125,5.2,0])
    a = math.radians(-12)
    rotation = np.array([[math.cos(a),-math.sin(a),0],[math.sin(a),math.cos(a),0],[0,0,1]])
    used = vertices.copy()
    used[~body] = ((vertices[~body]-pivot)@rotation.T+pivot).astype(np.float32)
    if not np.array_equal(used[body],vertices[body]):
        raise ValueError('Native post moved')
    sys.path.insert(0,str(args.kit.resolve()/'tools'))
    import bl_lib as native
    native.reset()
    place, _ = native.place_matrix(source,(0,0,0),rot=0,respect_active=True)
    world = native.CHANGE@place@np.array(doc['objects'][0]['world'])
    worlds = {state:(np.c_[v,np.ones(len(v))]@world.T)[:,:3] for state,v in (('closed',vertices),('used',used))}
    all_points = np.concatenate(list(worlds.values()))
    low, high = all_points.min(0), all_points.max(0)
    center = (low+high)/2
    extent = max(high[0]-low[0],high[2]-low[2])*1.15
    distance = max(12,extent*2)
    out.mkdir(parents=True)
    records = []
    indices = np.unique(triangles)
    for state in ('reference','closed','used'):
        native.reset()
        objects, _, _ = native.build(source,place=place,respect_active=True)
        if len(objects) != 1 or len(objects[0].data.vertices) != len(indices):
            raise ValueError('Incomplete native skull warning')
        if state != 'reference':
            objects[0].data.vertices.foreach_set('co',worlds[state][indices].astype(np.float32).reshape(-1))
        camera = native.camera((center[0],center[1]-distance,center[2]+distance*math.tan(math.radians(6))),
                               tuple(center),ortho=float(extent),res=(1024,1024))
        scene = bpy.context.scene
        scene.render.threads_mode = 'FIXED'
        scene.render.threads = 2
        scene.cycles.seed = 0
        image = out/(state+'.png')
        native.render(image,samples=4,transparent=True)
        if not image.is_file():
            raise RuntimeError('Missing native effigy render')
        projected = np.array([[1024*q.x,1024*(1-q.y)] for q in
                              (world_to_camera_view(scene,camera,Vector(p)) for p in worlds['closed'][body])])
        lo, hi = projected.min(0), projected.max(0)
        records.append({'state':state,'file':image.name,'sha256':hashlib.sha256(image.read_bytes()).hexdigest(),
                        'foot':[(lo[0]+hi[0])/2,hi[1]],'body_landmark_y':lo[1]})
    dependencies = [source/'scene.json',mesh_path]
    dependencies += [(source/m['texture']).resolve() for m in doc['materials'].values() if m.get('texture')]
    (out/'provenance.json').write_text(json.dumps({
        'art_id':'troubling_effigy_tangle','curio_id':'troubling_effigy','region':'dd2_forest',
        'native_source_region':'Foetor','source_vertices':1827,'source_triangles':2518,
        'fixed_post_vertices':64,'fixed_post_triangles':78,'whole_crown_vertices':1763,'whole_crown_triangles':2440,
        'crown_components':3,'pivot':pivot.tolist(),'crown_tilt_degrees':-12,
        'all_geometry_UVs_and_paint_retained':True,'post_vertices_identical_between_states':True,
        'generated_art':False,'installed':False,'native_qa':False,
        'notice':'Native Foetor warning adapted as a plausible corrupted battlefield effigy for the existing Tangle identity. No new location/spawn or original shipped used animation inferred. Regional visual and interaction acceptance remains pending.',
        'files':[{'path':str(path),'sha256':hashlib.sha256(path.read_bytes()).hexdigest()}for path in dependencies],
        'recipe_sha256':hashlib.sha256(Path(__file__).read_bytes()).hexdigest(),'outputs':records
    },indent=2))
    print('NATIVE_BOUND_EFFIGY_PAIR_READY',out,flush=True)


if __name__ == '__main__':
    main()
