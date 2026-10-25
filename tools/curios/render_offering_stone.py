"""Assemble native painted stone and offerings for the Foetor Sacrificial Stone.

Use the complete native table component, without its large architectural stairs,
and a complete native offering heap. Only the heap disappears in the used state.
Geometry, UVs and paint remain native; placement and state are curio adaptations,
not a claim of a shipped encounter or native animation. Run inside Blender.
Private source art and rendered output must remain outside the repository.
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
    for key in ('kit', 'altar', 'offering', 'out'):
        parser.add_argument('--' + key, type=Path, required=True)
    args = parser.parse_args(sys.argv[sys.argv.index('--') + 1:])
    repo = Path(__file__).resolve().parents[2]
    sources = {'stone': args.altar.resolve(), 'offering': args.offering.resolve()}
    out = args.out.resolve()
    if any(path.is_relative_to(repo) for path in [*sources.values(), out]):
        raise ValueError('Private native art must stay outside Git')
    if out.exists():
        raise ValueError('Use a new output directory')
    selected = {}
    dependencies = []
    for role, folder in sources.items():
        doc = json.loads((folder / 'scene.json').read_text())
        suffix = '/chirurgeon_node_altar.fbx' if role == 'stone' else '/beastmen_node_meat_02.fbx'
        name = 'table' if role == 'stone' else 'beastmen_node_meat_02'
        if not doc['source'].endswith(suffix) or doc.get('skipped'):
            raise ValueError('Use the complete reviewed native sources')
        parts = [part for part in doc['objects'] if part['name'] == name]
        if len(parts) != 1 or not parts[0]['active'] or not parts[0]['enabled']:
            raise ValueError('Missing or ambiguous native component')
        part = parts[0]
        mesh_path = folder / ('mesh-' + part['mesh'] + '.npz')
        data = np.load(mesh_path)
        expected = (76, 46) if role == 'stone' else (1098, 1549)
        if (len(data['v']), len(data['tri'])) != expected:
            raise ValueError('Native topology changed; review the whole assembly')
        selected[role] = {'doc': doc, 'part': part, 'data': data, 'include': [name]}
        dependencies += [folder / 'scene.json', mesh_path]
        dependencies += [(folder / doc['materials'][key]['texture']).resolve()
                         for key in part['materials'] if doc['materials'][key].get('texture')]
    sys.path.insert(0, str(args.kit.resolve() / 'tools'))
    import bl_lib as native
    native.reset()
    stone = selected['stone']
    place, size = native.place_matrix(sources['stone'], (0, 0, 0),
                                      include=stone['include'], respect_active=True)
    placements = {'stone': place}
    # Place the complete offering on the slab, slightly embedded at contact.
    # No geometry or UV is stretched to change its silhouette.
    placements['offering'], _ = native.place_matrix(
        sources['offering'], (0, float(size[1]) - .025, 0), scale=.82,
        include=selected['offering']['include'], respect_active=True)
    points = {}
    for role, item in selected.items():
        world = native.CHANGE @ placements[role] @ np.array(item['part']['world'])
        vertices = item['data']['v']
        points[role] = (np.c_[vertices, np.ones(len(vertices))] @ world.T)[:, :3]
    all_points = np.concatenate(list(points.values()))
    low, high = all_points.min(0), all_points.max(0)
    center = (low + high) / 2
    extent = max(high[0] - low[0], high[2] - low[2]) * 1.15
    distance = max(12, extent * 2)
    out.mkdir(parents=True)
    records = []
    for state in ('stone-reference', 'closed', 'used'):
        native.reset()
        roles = ('stone', 'offering') if state == 'closed' else ('stone',)
        for role in roles:
            item = selected[role]
            objects, _, _ = native.build(sources[role], place=placements[role],
                                          include=item['include'], respect_active=True)
            if len(objects) != 1 or len(objects[0].data.polygons) != len(item['data']['tri']):
                raise ValueError('A native component was truncated')
            # The shared native kit defaults to .4; retain this source's .5 cutoff.
            for material in objects[0].data.materials:
                for node in material.node_tree.nodes:
                    if node.bl_idname == 'ShaderNodeMath' and node.operation == 'GREATER_THAN':
                        node.inputs[1].default_value = .5
        camera = native.camera(
            (center[0], center[1] - distance, center[2] + distance * math.tan(math.radians(6))),
            tuple(center), ortho=float(extent), res=(1024, 1024))
        scene = bpy.context.scene
        scene.render.threads_mode = 'FIXED'
        scene.render.threads = 2
        scene.cycles.seed = 0
        image = out / (state + '.png')
        native.render(image, samples=4, transparent=True)
        if not image.is_file():
            raise RuntimeError('Missing offering-stone render')
        projected = np.array([[1024 * q.x, 1024 * (1 - q.y)] for q in
                              (world_to_camera_view(scene, camera, Vector(p)) for p in points['stone'])])
        lo, hi = projected.min(0), projected.max(0)
        records.append({'state': state, 'file': image.name,
                        'sha256': hashlib.sha256(image.read_bytes()).hexdigest(),
                        'foot': [(lo[0] + hi[0]) / 2, hi[1]],
                        'scale_landmarks': [[lo[0], hi[1]], [hi[0], hi[1]]]})
    (out / 'provenance.json').write_text(json.dumps({
        'art_id': 'sacrificial_stone_foetor', 'curio_id': 'sacrificial_stone', 'region': 'dd2_farm',
        'stone_vertices': 76, 'stone_triangles': 46,
        'offering_vertices': 1098, 'offering_triangles': 1549,
        'stone_is_fixed_between_states': True, 'offering_scale_uniform': .82,
        'offering_contact_embed': .025, 'native_paint_UVs_and_all_selected_faces_retained': True,
        'native_alpha_cutoff': .5, 'generated_art': False, 'installed': False, 'native_qa': False,
        'notice': 'Complete native stone component and complete native offering heap. Architectural stairs are deliberately excluded. Used hides only the offering. This is an adapted regional assembly, not a shipped encounter or native used animation. Outcomes and supplies remain authoritative.',
        'placements_unity': {role: matrix.tolist() for role, matrix in placements.items()},
        'files': [{'path': str(path), 'sha256': hashlib.sha256(path.read_bytes()).hexdigest()}
                  for path in sorted(set(dependencies))],
        'recipe_sha256': hashlib.sha256(Path(__file__).read_bytes()).hexdigest(), 'outputs': records
    }, indent=2))
    print('NATIVE_OFFERING_STONE_PAIR_READY', out, flush=True)


if __name__ == '__main__':
    main()
