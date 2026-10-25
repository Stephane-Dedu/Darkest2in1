"""Render a regional dock-cache pair from the owner's native coast_chest export.

Retain every original face and UV. Split nine shared rim vertices so the lid,
its ironwork and attached shell clusters hinge without stretching the body.
The searched state adds two underside faces using existing native plank paint.
This is an offline adaptation, not an original native animation or acceptance.
Run in Blender. Extracted sources and rendered outputs must stay outside Git.
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
    repo = Path(__file__).resolve().parents[2]
    source, out = args.source.resolve(), args.out.resolve()
    if source.is_relative_to(repo) or out.is_relative_to(repo):
        raise ValueError('Private native art must stay outside Git')
    if out.exists():
        raise ValueError('Use a new output directory')
    doc = json.loads((source/'scene.json').read_text())
    if not doc['source'].endswith('/coast_chest.fbx') or len(doc['objects']) != 1:
        raise ValueError('Use the complete reviewed coast_chest source')
    mesh_path = source/('mesh-'+doc['objects'][0]['mesh']+'.npz')
    data = np.load(mesh_path)
    vertices, triangles, uv = data['v'], data['tri'], data['uv']
    if vertices.shape != (484, 3) or triangles.shape != (282, 3) or uv.shape != (484, 2):
        raise ValueError('Native topology changed; review the complete lid again')
    # Audited structural panels plus complete attached shell clusters. The
    # source's UV seams split shells into many islands; height alone cuts them.
    lid_ranges = ((1,4), (23,29), (31,33), (37,40), (42,44), (46,50),
                  (52,58), (64,75), (77,79), (81,131), (143,151),
                  (171,207), (234,246), (252,276))
    lid = np.zeros(len(triangles), dtype=bool)
    for start, end in lid_ranges:
        lid[start:end] = True
    shared = np.intersect1d(np.unique(triangles[lid]), np.unique(triangles[~lid]))
    if lid.sum() != 169 or shared.tolist() != [43,46,51,52,55,58,62,81,84]:
        raise ValueError('Unexpected native rim partition')
    hinge = vertices[147].astype(float)
    axis = vertices[137]-hinge
    axis /= np.linalg.norm(axis)
    cross = np.array([[0,-axis[2],axis[1]], [axis[2],0,-axis[0]], [-axis[1],axis[0],0]])

    def moved(angle):
        a = math.radians(angle)
        rotation = np.eye(3)*math.cos(a)+(1-math.cos(a))*np.outer(axis,axis)+math.sin(a)*cross
        return (vertices-hinge)@rotation.T+hinge

    sys.path.insert(0, str(args.kit.resolve()/'tools'))
    import bl_lib as native
    native.reset()
    place, _ = native.place_matrix(source, (0,0,0), rot=180, respect_active=True)
    world = native.CHANGE@place

    def world_points(points):
        return (np.c_[points, np.ones(len(points))]@world.T)[:,:3]

    body_indices = np.unique(triangles[~lid])
    lid_indices = np.unique(triangles[lid])
    fixed = world_points(vertices[body_indices])
    all_points = np.concatenate((fixed, world_points(vertices[lid_indices]), world_points(moved(-100)[lid_indices])))
    low, high = all_points.min(0), all_points.max(0)
    center = (low+high)/2
    extent = max(high[0]-low[0], high[2]-low[2])*1.15
    distance = max(12, extent*2)

    def make_mesh(name, coordinates, faces, face_uv, material):
        mesh = bpy.data.meshes.new(name)
        mesh.from_pydata(coordinates.tolist(), [], faces.tolist())
        mesh.materials.append(material)
        layer = mesh.uv_layers.new(name='UVMap')
        layer.data.foreach_set('uv', face_uv.reshape(-1).astype(np.float32))
        obj = bpy.data.objects.new(name, mesh)
        bpy.context.collection.objects.link(obj)
        return obj

    out.mkdir(parents=True)
    records = []
    for state in ('reference', 'closed', 'used'):
        native.reset()
        material = native.make_mat(source, next(iter(doc['materials'])), next(iter(doc['materials'].values())))
        angle = -100 if state == 'used' else 0
        if state == 'reference':
            native.build(source, place=place, respect_active=True)
        else:
            for name, selection, coordinates in (('fixed native body', ~lid, vertices),
                                                   ('complete native lid', lid, moved(angle))):
                faces = triangles[selection, ::-1]
                indices = np.unique(faces)
                remap = np.zeros(len(vertices), dtype=int)
                remap[indices] = np.arange(len(indices))
                make_mesh(name, world_points(coordinates[indices]), remap[faces], uv[faces], material)
            if state == 'used':
                # The native closed shell has no underside. This inward panel
                # uses only a clean plank patch from its original 2048 atlas.
                corners = moved(angle)[[147,137,136,51]]
                corners = corners.mean(0)+(corners-corners.mean(0))*.985
                cap_faces = np.array([(2,1,0), (3,2,0)])
                cap_uv = np.array([[.312,.226], [.417,.226], [.417,.187], [.312,.187]])
                inside = material.copy()
                inside.name = 'recessed native Shroud timber'
                next(n for n in inside.node_tree.nodes if n.type == 'MIX_RGB').inputs[2].default_value = (.34,.38,.41,1)
                make_mesh('native timber lid underside', world_points(corners), cap_faces, cap_uv[cap_faces], inside)
        camera = native.camera((center[0], center[1]-distance, center[2]+distance*math.tan(math.radians(6))),
                               tuple(center), ortho=float(extent), res=(1024,1024))
        scene = bpy.context.scene
        scene.render.threads_mode = 'FIXED'
        scene.render.threads = 2
        scene.cycles.seed = 0
        path = out/(state+'.png')
        native.render(path, samples=4, transparent=True)
        if not path.is_file():
            raise RuntimeError('Missing native chest render')
        points = np.array([[1024*q.x, 1024*(1-q.y)] for q in
                           (world_to_camera_view(scene, camera, Vector(point)) for point in fixed)])
        lo, hi = points.min(0), points.max(0)
        records.append({'state':state, 'file':path.name, 'sha256':hashlib.sha256(path.read_bytes()).hexdigest(),
                        'foot':[(lo[0]+hi[0])/2, hi[1]],
                        'scale_landmarks':[[lo[0],(lo[1]+hi[1])/2], [hi[0],(lo[1]+hi[1])/2]]})
    dependencies = [source/'scene.json', mesh_path]
    dependencies += [(source/m['texture']).resolve() for m in doc['materials'].values() if m.get('texture')]
    record = {'art_id':'locked_display_cabinet_shroud', 'curio_id':'locked_display_cabinet', 'region':'dd2_coast',
              'source_vertices':484, 'source_triangles_retained':282, 'lid_triangles':169, 'fixed_body_triangles':113,
              'split_shared_rim_vertices':shared.tolist(), 'partition_vertices':len(body_indices)+len(lid_indices),
              'added_used_underside_triangles':2, 'source_UVs_unchanged':True,
              'hinge':[hinge.tolist(), vertices[137].tolist()], 'angle_degrees':-100,
              'original_body_material_and_paint_retained':True, 'underside_tint':[.34,.38,.41],
              'generated_art':False, 'installed':False, 'native_qa':False,
              'notice':'Offline native geometry/paint adaptation. The lid state and underside are adapted, not original shipped animation. Native shader, actor, hover and key/shovel checks remain pending.',
              'files':[{'path':str(path), 'sha256':hashlib.sha256(path.read_bytes()).hexdigest()} for path in dependencies],
              'recipe_sha256':hashlib.sha256(Path(__file__).read_bytes()).hexdigest(), 'outputs':records}
    (out/'provenance.json').write_text(json.dumps(record, indent=2))
    print('NATIVE_SHROUD_COFFER_PAIR_READY', out, flush=True)


if __name__ == '__main__':
    main()
