"""Render a complete native bone stake with a spent upper offering state.

The selected caves renderer keeps its geometry, UVs, full paint and tint.
The used adaptation hides all 11 complete disconnected upper-bundle groups;
the lower stake, skull, bindings and hide stay fixed. No generation or repaint.
Run in Blender with private audited inputs; native context QA is separate.
"""
from pathlib import Path
import argparse
import hashlib
import json
import math
import sys

import bmesh
import bpy
import numpy as np
from bpy_extras.object_utils import world_to_camera_view
from mathutils import Vector


def component_faces(folder, obj):
    data = np.load(folder / ('mesh-' + obj['mesh'] + '.npz'))
    slots = np.repeat(np.arange(len(data['sub'])), data['sub'])
    triangles = data['tri'][(slots >= obj['first']) & (slots < obj['first'] + obj['count'])]
    used = np.unique(triangles)
    parent = {int(i): int(i) for i in used}

    def find(i):
        while parent[i] != i:
            parent[i] = parent[parent[i]]
            i = parent[i]
        return i

    def union(a, b):
        parent[find(int(a))] = find(int(b))

    positions = {}
    for i in used:
        # Analysis joins UV/normal seam duplicates, without changing the mesh.
        key = tuple(np.round(data['v'][i], 5))
        union(i, positions.setdefault(key, int(i)))
    for face in triangles:
        union(face[0], face[1])
        union(face[1], face[2])
    groups = {}
    for index, face in enumerate(triangles):
        groups.setdefault(find(int(face[0])), []).append(index)
    if len(groups) != 17 or len(triangles) != 992 or len(used) != 887:
        raise ValueError('Review changed native topology or component grouping')
    hidden = (0, 3, 5, 7, 8, 9, 11, 12, 13, 14, 15)
    faces = {i for group, indices in enumerate(groups.values()) if group in hidden for i in indices}
    vertices = np.unique(triangles[sorted(faces)])
    if len(faces) != 416 or len(vertices) != 362:
        raise ValueError('Complete upper offering selection changed')
    return faces, hidden


def spent(objects, faces):
    mesh = objects[0].data
    bm = bmesh.new()
    bm.from_mesh(mesh)
    bm.faces.ensure_lookup_table()
    bmesh.ops.delete(bm, geom=[bm.faces[i] for i in sorted(faces)], context='FACES')
    bm.to_mesh(mesh)
    bm.free()
    mesh.update()
    if (len(mesh.vertices), len(mesh.polygons)) != (525, 576):
        raise ValueError('Spent state lost fixed geometry or retained offering fragments')


def cull(objects):
    for material in objects[0].data.materials:
        nodes = material.node_tree
        output = next(n for n in nodes.nodes if n.type == 'OUTPUT_MATERIAL')
        original = output.inputs['Surface'].links[0].from_socket
        geometry = nodes.nodes.new('ShaderNodeNewGeometry')
        transparent = nodes.nodes.new('ShaderNodeBsdfTransparent')
        mix = nodes.nodes.new('ShaderNodeMixShader')
        nodes.links.new(geometry.outputs['Backfacing'], mix.inputs[0])
        nodes.links.new(original, mix.inputs[1])
        nodes.links.new(transparent.outputs[0], mix.inputs[2])
        nodes.links.new(mix.outputs[0], output.inputs['Surface'])


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    for key in ('kit', 'source', 'out'):
        parser.add_argument('--' + key, type=Path, required=True)
    args = parser.parse_args(sys.argv[sys.argv.index('--') + 1:])
    folder, out = args.source.resolve(), args.out.resolve()
    repo = Path(__file__).resolve().parents[2]
    if folder.is_relative_to(repo) or out.is_relative_to(repo) or out.exists():
        raise ValueError('Use private audited inputs and a new private output folder')
    doc = json.loads((folder / 'scene.json').read_text())
    if doc['source'] != 'scenes_scenes_combat_arena_caves_cultist' or len(doc['objects']) != 1:
        raise ValueError('Use the reviewed complete native bone stake renderer')
    obj = doc['objects'][0]
    if obj['name'] != 'caves_skull_torch_01' or not obj['active'] or not obj['enabled'] or obj.get('lod_skip'):
        raise ValueError('Native renderer selection changed')
    material = doc['materials'][obj['materials'][0]]
    if len(obj['materials']) != 1 or material['cutout'] or material['floats'].get('_Cull') != 2:
        raise ValueError('Review changed native materials or alpha/culling')
    faces, hidden = component_faces(folder, obj)
    sys.path.insert(0, str(args.kit.resolve() / 'tools'))
    import bl_lib as native
    native.reset()
    place, _ = native.place_matrix(folder, (0, 0, 0), respect_active=True)
    objects, low, high = native.build(folder, place=place, respect_active=True)
    if len(objects) != 1 or (len(objects[0].data.vertices), len(objects[0].data.polygons)) != (887, 992):
        raise ValueError('Complete native body changed')
    removed_points = [tuple(v.co) for i in faces for v in (objects[0].data.vertices[j] for j in objects[0].data.polygons[i].vertices)]
    center = (low + high) / 2
    extent = max(high[0] - low[0], high[2] - low[2]) * 1.18
    distance = max(12, extent * 2)
    location = (center[0], center[1] - distance, center[2] + distance * math.tan(math.radians(6)))
    camera = native.camera(location, tuple(center), ortho=float(extent), res=(1024, 1024))
    bpy.context.view_layer.update()
    scene = bpy.context.scene

    def project(points):
        return np.array([[1024 * q.x, 1024 * (1 - q.y)] for q in
                         (world_to_camera_view(scene, camera, Vector(p)) for p in points)])

    spent(objects, faces)
    fixed = project([tuple(v.co) for v in objects[0].data.vertices])
    foot_y = float(fixed[:, 1].max())
    foot = [float(np.median(fixed[fixed[:, 1] >= foot_y - 1, 0])), foot_y]
    removed_bounds = project(removed_points)
    out.mkdir(parents=True)
    outputs = []
    for state in ('native-original-reference', 'closed', 'used'):
        native.reset()
        objects, _, _ = native.build(folder, place=place, respect_active=True)
        if state == 'used':
            spent(objects, faces)
        cull(objects)
        native.camera(location, tuple(center), ortho=float(extent), res=(1024, 1024))
        scene = bpy.context.scene
        scene.render.threads_mode = 'FIXED'
        scene.render.threads = 2
        scene.cycles.seed = 0
        path = out / (state + '.png')
        native.render(path, samples=4, transparent=True)
        outputs.append({'state': state, 'file': path.name, 'sha256': hashlib.sha256(path.read_bytes()).hexdigest(),
                        'foot': foot, 'body_landmark_y': float(fixed[:, 1].min()),
                        'vertices': len(objects[0].data.vertices), 'faces': len(objects[0].data.polygons)})
    files = [folder / 'scene.json', folder / ('mesh-' + obj['mesh'] + '.npz'), (folder / material['texture']).resolve()]
    record = {'curio_id': 'skull_altar', 'art_id': 'skull_altar_foetor', 'region': 'dd2_farm',
              'native_full_geometry_UV_paint_tint_retained': True, 'generated_art': False,
              'used_state': 'All 11 complete upper bone-bundle components hide; fixed lower stake, skull, bindings and hide remain.',
              'hidden_welded_components': list(hidden), 'hidden_vertices': 362, 'hidden_faces': 416,
              'source_component_analysis_weld_only_no_geometry_weld': True,
              'removed_projected_bounds': [*removed_bounds.min(0).tolist(), *removed_bounds.max(0).tolist()],
              'native_cull': 2, 'cutout_enabled': False, 'full_outline_lighting_fog_dither_not_recreated': True,
              'outputs': outputs, 'files': {str(p): hashlib.sha256(p.read_bytes()).hexdigest() for p in files},
              'recipe_sha256': hashlib.sha256(Path(__file__).read_bytes()).hexdigest(), 'native_QA': False, 'installed': False}
    (out / 'provenance.json').write_text(json.dumps(record, indent=2))
    print(json.dumps({'outputs': outputs, 'removed_faces': len(faces), 'native_paint_retained': True}))


if __name__ == '__main__':
    main()
