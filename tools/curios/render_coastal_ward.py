"""Render a complete native coastal ward with a dormant disc state.

Use the privately audited existing scene cache and exact source-component
contract. The cache's inactive interior instance has the same complete mesh,
UV and full paint as the active FBX root and exterior instance. No cached
geometry, texture or authored pose is edited. Closed state explicitly grades
two complete disc components in the renderer's working colour space; used
state retains the original native paint. This does not reconstruct Unity's
lighting/fog shader or claim quest gameplay acceptance.
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
    if output.exists() or any(p.is_relative_to(repo) for p in
                              (source, components, output)):
        raise ValueError('Use audited private inputs and a new private output')
    contract = json.loads(components.read_text())
    if not contract['cached_geometry_UV_triangles_exact_installed_source'] or \
            not contract['full2048_RGBA_paint_exact_installed_source'] or \
            contract['native_actual_complete_mesh_counts'] != [3129, 2435]:
        raise ValueError('Review the complete native source before rendering')
    sha = lambda p: hashlib.sha256(Path(p).read_bytes()).hexdigest()
    for path, digest in contract['existing_source_hashes'].items():
        if sha(path) != digest:
            raise ValueError('Native source cache changed')
    if source / 'scene.json' != Path(contract['existing_scene_source']):
        raise ValueError('Use the exact reviewed source scene')
    groups = contract['groups']
    if len(groups) != 69 or sorted(i for g in groups for i in g['face_indices']) != list(range(2435)):
        raise ValueError('Component contract must partition every source face')
    chosen = set(groups[0]['face_indices'] + groups[59]['face_indices'])
    if (groups[0]['vertices'], groups[0]['triangles'],
            groups[59]['vertices'], groups[59]['triangles']) != (404, 332, 9, 8):
        raise ValueError('Review changed disc geometry')
    mesh = np.load(contract['existing_mesh_source'])
    vertices, faces = mesh['v'], mesh['tri']
    parent = list(range(len(vertices)))
    def root(i):
        while parent[i] != i:
            parent[i] = parent[parent[i]]
            i = parent[i]
        return i
    def join(a, b):
        a, b = root(int(a)), root(int(b))
        if a != b:
            parent[b] = a
    positions = {}
    for i, vertex in enumerate(vertices):
        key = tuple(map(float, vertex))
        if key in positions:
            join(i, positions[key])
        else:
            positions[key] = i
    for a, b, c in faces:
        join(a, b)
        join(a, c)
    connected = {}
    for i, face in enumerate(faces):
        connected.setdefault(root(int(face[0])), []).append(i)
    ordered = sorted(connected.values(), key=lambda fs: (-len(fs), min(fs)))
    if len(ordered) != 69:
        raise ValueError('Native component topology changed')
    for group, indices in zip(groups, ordered):
        members = np.unique(faces[indices]).tolist()
        if group['face_indices'] != indices or group['source_vertex_indices'] != members:
            raise ValueError('Use complete reviewed geometric components')
    sys.path.insert(0, str(native_tools))
    import bl_lib as native
    output.mkdir()
    for state in ('closed', 'used'):
        native.reset()
        place, _ = native.place_matrix(source, (0, 0, 0),
                                       include=['coast_dungeon_shrine'], respect_active=False)
        objects, low, high = native.build(source, include=['coast_dungeon_shrine'],
                                          place=place, respect_active=False)
        if len(objects) != 1 or len(objects[0].data.vertices) != 3129 or len(objects[0].data.polygons) != 2435:
            raise ValueError('Keep the complete native model in both states')
        obj = objects[0]
        material = obj.data.materials[0]
        nodes = material.node_tree
        surface = next(n for n in nodes.nodes if n.type == 'OUTPUT_MATERIAL')
        original = surface.inputs['Surface'].links[0].from_socket
        for node in nodes.nodes:
            if node.type == 'MATH' and node.operation == 'GREATER_THAN':
                node.inputs[1].default_value = .5
        geometry = nodes.nodes.new('ShaderNodeNewGeometry')
        transparent = nodes.nodes.new('ShaderNodeBsdfTransparent')
        mix = nodes.nodes.new('ShaderNodeMixShader')
        nodes.links.new(geometry.outputs['Backfacing'], mix.inputs[0])
        nodes.links.new(original, mix.inputs[1])
        nodes.links.new(transparent.outputs[0], mix.inputs[2])
        nodes.links.new(mix.outputs[0], surface.inputs[0])
        if state == 'closed':
            dormant = material.copy()
            graph = dormant.node_tree
            emission = next(n for n in graph.nodes if n.type == 'EMISSION')
            paint = emission.inputs['Color'].links[0].from_socket
            grade = graph.nodes.new('ShaderNodeMixRGB')
            grade.blend_type = 'MULTIPLY'
            grade.inputs[0].default_value = 1
            grade.inputs[2].default_value = (.3, .3, .3, 1)
            graph.links.new(paint, grade.inputs[1])
            graph.links.new(grade.outputs[0], emission.inputs['Color'])
            obj.data.materials.append(dormant)
            for face in obj.data.polygons:
                face.material_index = 1 if face.index in chosen else 0
        centre = (low + high) / 2
        extent = max(high[0]-low[0], high[2]-low[2]) * 1.12
        distance = max(15, extent*2)
        native.camera((centre[0], centre[1]-distance,
                       centre[2]+distance*math.tan(math.radians(6))), tuple(centre),
                      ortho=float(extent), res=(1024, 1024))
        scene = bpy.context.scene
        scene.render.threads_mode = 'FIXED'
        scene.render.threads = 2
        scene.cycles.seed = 0
        native.render(output/(state+'.png'), samples=4, transparent=True)
    record = {'complete_native_vertices': 3129, 'triangles': 2435,
              'UV_and_full2048_paint_exact': True, 'geometric_groups': 69,
              'closed_graded_whole_groups': [0, 59], 'graded_vertices': 413,
              'graded_triangles': 340, 'closed_grade_working_RGB': .3,
              'used_original_native_paint': True,
              'geometry_UV_pose_camera_ground_fixed': True,
              'Cull2_clip05_modeled_not_full_Unity_shader': True,
              'source_contract_sha256': sha(components), 'recipe_sha256': sha(__file__),
              'installed': False, 'native_quest_QA': False}
    (output/'provenance.json').write_text(json.dumps(record, indent=2))


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    for name in ('source', 'components', 'native-tools', 'out'):
        parser.add_argument('--'+name, type=Path, required=True)
    args = parser.parse_args(sys.argv[sys.argv.index('--')+1:])
    render(args.source, args.components, args.native_tools, args.out)
