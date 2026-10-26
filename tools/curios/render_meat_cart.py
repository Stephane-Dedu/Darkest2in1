"""Render a regional Dinner Cart pair using complete native offal components.

The complete native static-batch renderer owns submesh0. Both states keep its
geometry, UVs, paint and tint. Searched-state visibility hides complete draped
offal components, including their reverse surfaces. The carcass and cart stay
fixed. Private component selection and all game assets remain outside Git.
This unlit paint study includes source backface culling, not the full native
outline, lighting, fog or shader reconstruction. Native QA is still required.
"""
from pathlib import Path
import argparse
import hashlib
import json
import math
import sys

import bpy
import numpy as np
from bpy_extras.object_utils import world_to_camera_view
from mathutils import Vector


def components(vertices, triangles):
    parent = np.arange(vertices)

    def find(index):
        while parent[index] != index:
            parent[index] = parent[parent[index]]
            index = parent[index]
        return index

    for face in triangles:
        first = find(face[0])
        for vertex in face[1:]:
            parent[find(vertex)] = first
    return np.array([find(face[0]) for face in triangles])


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    for key in ('kit', 'source', 'selection', 'out'):
        parser.add_argument('--' + key, type=Path, required=True)
    args = parser.parse_args(sys.argv[sys.argv.index('--') + 1:])
    source, selection, out = args.source.resolve(), args.selection.resolve(), args.out.resolve()
    repo = Path(__file__).resolve().parents[2]
    if any(path.is_relative_to(repo) for path in (source, selection, out)) or out.exists():
        raise ValueError('Use private inputs and a new output folder outside Git')
    doc = json.loads((source / 'scene.json').read_text())
    chosen = json.loads(selection.read_text())
    if len(doc['objects']) != 1 or doc['objects'][0]['name'] != 'farm_cart_meat':
        raise ValueError('Use the complete reviewed native cart renderer')
    obj = doc['objects'][0]
    if not obj['active'] or not obj['enabled'] or obj.get('first') != 0 or obj.get('count') != 1:
        raise ValueError('Native static-batch selection changed')
    mesh_path = source / ('mesh-' + obj['mesh'] + '.npz')
    data = np.load(mesh_path)
    tri = data['tri'][:int(data['sub'][0])]
    if len(np.unique(tri)) != 5748 or len(tri) != 7232:
        raise ValueError('Native cart geometry changed')
    roots = chosen['component_roots']
    if len(roots) != 8 or len(set(roots)) != 8:
        raise ValueError('Review all four complete front/back offal strips')
    group = components(len(data['v']), tri)
    if not set(roots).issubset(set(group)):
        raise ValueError('Selected native components no longer exist')
    hidden = np.isin(group, roots)
    if hidden.sum() != 1832 or len(np.unique(tri[hidden])) != 1172:
        raise ValueError('Native offal assembly changed')
    sys.path.insert(0, str(args.kit.resolve() / 'tools'))
    import bl_lib as native
    native.reset()
    place, _ = native.place_matrix(source, (0, 0, 0), respect_active=True)
    objects, low, high = native.build(source, place=place, respect_active=True)
    if len(objects) != 1:
        raise ValueError('Incomplete native source')
    center = (low + high) / 2
    extent = max(high[0] - low[0], high[2] - low[2]) * 1.14
    distance = max(12, extent * 2)
    fixed_ids = np.unique(tri[~hidden])
    fixed = (np.c_[data['v'][fixed_ids], np.ones(len(fixed_ids))] @ (native.CHANGE @ place).T)[:, :3]
    remap = np.zeros(len(data['v']), dtype=int)
    remap[np.unique(tri)] = np.arange(5748)
    out.mkdir(parents=True)
    records = []
    for state in ('native-reference', 'legacy-no-cull-reference', 'closed', 'used'):
        native.reset()
        objects, _, _ = native.build(source, place=place, respect_active=True)
        body = objects[0]
        actual = np.array([list(poly.vertices) for poly in body.data.polygons])
        if not np.array_equal(actual, remap[tri[:, ::-1]]):
            raise ValueError('Renderer changed native face order')
        if state != 'legacy-no-cull-reference':
            for material in body.data.materials:
                nodes = material.node_tree
                output = next(node for node in nodes.nodes if node.type == 'OUTPUT_MATERIAL')
                original = output.inputs['Surface'].links[0].from_socket
                geometry = nodes.nodes.new('ShaderNodeNewGeometry')
                transparent = nodes.nodes.new('ShaderNodeBsdfTransparent')
                mix = nodes.nodes.new('ShaderNodeMixShader')
                nodes.links.new(geometry.outputs['Backfacing'], mix.inputs[0])
                nodes.links.new(original, mix.inputs[1])
                nodes.links.new(transparent.outputs[0], mix.inputs[2])
                nodes.links.new(mix.outputs[0], output.inputs['Surface'])
        if state == 'used':
            material = bpy.data.materials.new('UsedWholeStripVisibility')
            material.use_nodes = True
            nodes = material.node_tree
            nodes.nodes.clear()
            transparent = nodes.nodes.new('ShaderNodeBsdfTransparent')
            output = nodes.nodes.new('ShaderNodeOutputMaterial')
            nodes.links.new(transparent.outputs[0], output.inputs['Surface'])
            slot = len(body.data.materials)
            body.data.materials.append(material)
            for index in np.flatnonzero(hidden):
                body.data.polygons[int(index)].material_index = slot
        camera = native.camera((center[0], center[1] - distance, center[2] + distance * math.tan(math.radians(6))),
                               tuple(center), ortho=float(extent), res=(1024, 1024))
        scene = bpy.context.scene
        scene.render.threads_mode = 'FIXED'
        scene.render.threads = 2
        scene.cycles.seed = 0
        path = out / (state + '.png')
        native.render(path, samples=4, transparent=True)
        points = np.array([[1024 * point.x, 1024 * (1 - point.y)] for point in
                           (world_to_camera_view(scene, camera, Vector(vertex)) for vertex in fixed)])
        lower, upper = points.min(0), points.max(0)
        records.append({'state': state, 'file': path.name, 'sha256': hashlib.sha256(path.read_bytes()).hexdigest(),
                        'foot': [(lower[0] + upper[0]) / 2, upper[1]], 'body_landmark_y': lower[1]})
    files = [source / 'scene.json', mesh_path, selection]
    files += [(source / material['texture']).resolve() for material in doc['materials'].values() if material.get('texture')]
    metadata = {'art_id': 'dinner_cart_foetor', 'curio_id': 'dinner_cart', 'region': 'dd2_farm',
                'geometry_vertices': 5748, 'geometry_triangles': 7232,
                'used_hidden_vertices': 1172, 'used_hidden_triangles': 1832,
                'used_visible_vertices': 4576, 'used_visible_triangles': 5400,
                'geometry_UVs_original_paint_and_tint_fixed': True, 'used_changes_material_visibility_only': True,
                'used_state': 'Four whole draped offal strips hidden, including complete reverse surfaces. Main carcass, timber, spikes, legs and loose scraps stay fixed.',
                'native_cull_mode': 2, 'native_cutout_enabled': False,
                'outline_lighting_and_other_shader_paths_not_recreated': True,
                'outputs': records, 'files': [{'path': str(path), 'sha256': hashlib.sha256(path.read_bytes()).hexdigest()} for path in files],
                'recipe_sha256': hashlib.sha256(Path(__file__).read_bytes()).hexdigest(),
                'generated_art': False, 'installed': False, 'native_QA': False}
    (out / 'provenance.json').write_text(json.dumps(metadata, indent=2))
    print('NATIVE_MEAT_CART_PAIR_READY')


if __name__ == '__main__':
    main()
