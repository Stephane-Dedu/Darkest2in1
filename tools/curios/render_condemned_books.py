"""Render a complete private native book stack and remove its whole top book.

Blender: --python this_file -- --source <export> --components <private audit>
--native-tools <kit/tools> --out <new private folder>. Source geometry, UVs
and paint retain. Common ground buries the native skirt. Cull2/clip0.5 are
modeled; Unity lighting, fog, dithering and near-darkening are not.
"""
import argparse
import hashlib
import json
import math
from pathlib import Path
import sys

import bpy
import numpy as np


def render(source, components, native_tools, output):
    source, components, native_tools, output = [Path(p).resolve() for p in
                                               (source, components, native_tools, output)]
    repo = Path(__file__).resolve().parents[2]
    if any(p.is_relative_to(repo) for p in (source, components, output)) or output.exists():
        raise ValueError('Use private audited assets and a new private output')
    doc = json.loads((source / 'scene.json').read_text())
    if len(doc['objects']) != 1 or doc['skipped'] or doc['objects'][0]['name'] != 'city_book_pile_02':
        raise ValueError('Use the complete audited native book stack')
    record = next(r for r in json.loads(components.read_text())['records']
                  if r['name'] == 'city_book_pile_02')
    mesh = source / ('mesh-' + doc['objects'][0]['mesh'] + '.npz')
    if hashlib.sha256(mesh.read_bytes()).hexdigest() != record['source_mesh_sha256']:
        raise ValueError('Mesh differs from the component audit')
    data = np.load(mesh)
    groups = record['groups']
    if len(data['v']) != 242 or len(data['tri']) != 136 or len(groups) != 6:
        raise ValueError('Native stack is incomplete')
    faces = [i for group in groups for i in group['faces']]
    if sorted(faces) != list(range(136)):
        raise ValueError('Component audit must cover every source face exactly once')
    for group in groups:
        if np.unique(data['tri'][group['faces']]).tolist() != group['vertices']:
            raise ValueError('Component vertices do not match source faces')
    top = next(g for g in groups if g['group'] == 1)
    bottom = next(g for g in groups if g['group'] == 5)
    if len(top['vertices']) != 56 or len(top['faces']) != 30:
        raise ValueError('The selected whole top book changed')
    sys.path.insert(0, str(native_tools))
    import bl_lib as native
    native.reset()
    place, _ = native.place_matrix(source, (0, 0, 0), rot=180, respect_active=True)
    objects, lo, hi = native.build(source, place=place, respect_active=True)
    if len(objects) != 1 or len(objects[0].data.vertices) != 242 or len(objects[0].data.polygons) != 136:
        raise ValueError('Rendered source geometry is incomplete')
    obj = objects[0]
    for material in obj.data.materials:
        tree = material.node_tree
        for node in tree.nodes:
            if node.type == 'MATH' and node.operation == 'GREATER_THAN':
                node.inputs[1].default_value = .5
        out = next(n for n in tree.nodes if n.type == 'OUTPUT_MATERIAL')
        original = out.inputs['Surface'].links[0].from_socket
        geometry = tree.nodes.new('ShaderNodeNewGeometry')
        transparent = tree.nodes.new('ShaderNodeBsdfTransparent')
        mix = tree.nodes.new('ShaderNodeMixShader')
        tree.links.new(geometry.outputs['Backfacing'], mix.inputs[0])
        tree.links.new(original, mix.inputs[1])
        tree.links.new(transparent.outputs[0], mix.inputs[2])
        tree.links.new(mix.outputs[0], out.inputs['Surface'])
    floor = min(obj.data.vertices[i].co.z for i in bottom['vertices'])
    bpy.ops.mesh.primitive_plane_add(size=100, location=(0, 0, float(floor) + .00001))
    ground = bpy.context.object
    material = bpy.data.materials.new('render_only_ground_holdout')
    material.use_nodes = True
    tree = material.node_tree
    tree.nodes.clear()
    holdout = tree.nodes.new('ShaderNodeHoldout')
    out = tree.nodes.new('ShaderNodeOutputMaterial')
    tree.links.new(holdout.outputs[0], out.inputs['Surface'])
    ground.data.materials.append(material)
    center = (lo + hi) / 2
    extent = max(hi[0] - lo[0], hi[2] - lo[2], .01) * 1.18
    distance = max(12, extent * 2)
    native.camera((center[0], center[1] - distance, center[2] + distance * math.tan(math.radians(6))),
                  tuple(center), ortho=float(extent), res=(720, 720))
    bpy.context.scene.render.threads_mode = 'FIXED'
    bpy.context.scene.render.threads = 2
    output.mkdir(parents=True)
    native.render(output / 'closed.png', samples=4, transparent=True)
    material = bpy.data.materials.new('searched_top_book')
    material.use_nodes = True
    tree = material.node_tree
    tree.nodes.clear()
    transparent = tree.nodes.new('ShaderNodeBsdfTransparent')
    out = tree.nodes.new('ShaderNodeOutputMaterial')
    tree.links.new(transparent.outputs[0], out.inputs['Surface'])
    obj.data.materials.append(material)
    for index in top['faces']:
        obj.data.polygons[index].material_index = 1
    native.render(output / 'used.png', samples=4, transparent=True)


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    for name in ('source', 'components', 'native-tools', 'out'):
        parser.add_argument('--' + name, type=Path, required=True)
    args = parser.parse_args(sys.argv[sys.argv.index('--') + 1:])
    render(args.source, args.components, args.native_tools, args.out)
