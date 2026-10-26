"""Render complete native forest treatment remains with a searched belongings cue.

The table and corpse keep their original relative pose, geometry, UVs, paint
and authored forest tint. A complete native sack is placed beside the slab;
only its visibility changes after use. No deformation or bitmap editing.
Run in Blender with private audited sources. Native shader/actor QA is separate.
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


def cull_backfaces(objects):
    for obj in objects:
        for material in obj.data.materials:
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


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    for key in ('kit', 'source', 'sack', 'out'):
        parser.add_argument('--' + key, type=Path, required=True)
    args = parser.parse_args(sys.argv[sys.argv.index('--') + 1:])
    repo = Path(__file__).resolve().parents[2]
    source, sack, out = [getattr(args, key).resolve() for key in ('source', 'sack', 'out')]
    if any(p.is_relative_to(repo) for p in (source, sack, out)) or out.exists():
        raise ValueError('Use audited private inputs and a new private output folder')
    docs = [json.loads((p / 'scene.json').read_text()) for p in (source, sack)]
    if docs[0]['source'] != 'scenes_scenes_combat_arena_forest_gaunt_chirurgeon' or docs[0].get('skipped'):
        raise ValueError('Use the reviewed native forest source')
    if sorted(o['name'] for o in docs[0]['objects']) != ['chirurgeon_node_corpse', 'table']:
        raise ValueError('Keep both complete native body and table renderers')
    if len(docs[1]['objects']) != 1 or docs[1]['objects'][0]['name'] != 'inn_embark_sack(Clone)':
        raise ValueError('Use the complete native sack renderer')
    for doc in docs:
        if any(not o['active'] or not o['enabled'] for o in doc['objects']):
            raise ValueError('Inactive native source')
        if any(m['cutout'] or m['floats'].get('_Cull') != 2 for m in doc['materials'].values()):
            raise ValueError('Review changed native culling/alpha properties')
    sys.path.insert(0, str(args.kit.resolve() / 'tools'))
    import bl_lib as native
    native.reset()
    fixed_place, _ = native.place_matrix(source, (0, 0, 0), respect_active=True)
    fixed, low, high = native.build(source, place=fixed_place, respect_active=True)
    topology = {o.name: (len(o.data.vertices), len(o.data.polygons)) for o in fixed}
    if topology != {'table': (76, 46), 'chirurgeon_node_corpse': (4746, 6813)}:
        raise ValueError('Complete native topology changed')
    center = (low + high) / 2
    extent = max(high[0] - low[0], high[2] - low[2]) * 1.16
    distance = max(12, extent * 2)
    location = (center[0], center[1] - distance, center[2] + distance * math.tan(math.radians(6)))
    camera = native.camera(location, tuple(center), ortho=float(extent), res=(1024, 1024))
    scene = bpy.context.scene
    bpy.context.view_layer.update()

    def project(points):
        return np.array([[1024 * q.x, 1024 * (1 - q.y)] for q in
                         (world_to_camera_view(scene, camera, Vector(p)) for p in points)])

    fixed_points = np.array([tuple(v.co) for ob in fixed for v in ob.data.vertices])
    pixels = project(fixed_points)
    foot = [(pixels[:, 0].min() + pixels[:, 0].max()) / 2, pixels[:, 1].max()]
    bag_place, _ = native.place_matrix(sack, (-1.95, 0, 0), scale=.9, respect_active=True)
    bags, _, _ = native.build(sack, place=bag_place, respect_active=True)
    if len(bags) != 1 or (len(bags[0].data.vertices), len(bags[0].data.polygons)) != (475, 640):
        raise ValueError('Complete native sack changed')
    bag_points = np.array([tuple(v.co) for v in bags[0].data.vertices])
    depth_axis = native.CHANGE[:3, :3] @ [0, 0, 1]
    derivative = (project([bag_points[0] + depth_axis]) - project([bag_points[0]]))[0, 1]
    depth_shift = (foot[1] - project(bag_points)[:, 1].max()) / derivative
    if not np.isfinite(depth_shift) or abs(depth_shift) > 3:
        raise ValueError('Invalid ground-plane alignment')
    bag_place[2, 3] += depth_shift
    bag_bounds = project(bag_points + depth_axis * depth_shift)
    if abs(bag_bounds[:, 1].max() - foot[1]) > .001:
        raise ValueError('Sack and fixed slab feet differ')
    out.mkdir(parents=True)
    states = []
    for state in ('native-fixed-reference', 'closed', 'used'):
        native.reset()
        fixed, _, _ = native.build(source, place=fixed_place, respect_active=True)
        objects = list(fixed)
        if state == 'closed':
            bags, _, _ = native.build(sack, place=bag_place, respect_active=True)
            objects += bags
        cull_backfaces(objects)
        native.camera(location, tuple(center), ortho=float(extent), res=(1024, 1024))
        scene = bpy.context.scene
        scene.render.threads_mode = 'FIXED'
        scene.render.threads = 2
        scene.cycles.seed = 0
        path = out / (state + '.png')
        native.render(path, samples=4, transparent=True)
        states.append({'state': state, 'file': path.name, 'sha256': hashlib.sha256(path.read_bytes()).hexdigest(),
                       'foot': foot, 'body_landmark_y': float(pixels[:, 1].min()),
                       'vertices': sum(len(o.data.vertices) for o in objects),
                       'faces': sum(len(o.data.polygons) for o in objects)})
    files = []
    for folder, doc in zip((source, sack), docs):
        files += [folder / 'scene.json'] + [folder / ('mesh-' + o['mesh'] + '.npz') for o in doc['objects']]
        files += [(folder / m['texture']).resolve() for m in doc['materials'].values()]
    meta = {'curio_id': 'mummified_remains', 'art_id': 'mummified_remains_tangle', 'region': 'dd2_forest',
            'fixed_native_body_table_relative_pose_retained': True, 'geometry_UVs_original_paint_tints_retained': True,
            'sack_uniform_scale': .9, 'sack_placement': bag_place.tolist(), 'sack_depth_alignment': float(depth_shift),
            'native_cull': 2, 'cutout_enabled': False, 'outline_lighting_fog_not_recreated': True,
            'used_state': 'Whole belongings sack hides; body and table remain fixed.',
            'sack_projected_bounds': [*bag_bounds.min(0).tolist(), *bag_bounds.max(0).tolist()],
            'outputs': states, 'files': {str(p): hashlib.sha256(p.read_bytes()).hexdigest() for p in set(files)},
            'recipe_sha256': hashlib.sha256(Path(__file__).read_bytes()).hexdigest(),
            'installed': False, 'native_QA': False}
    (out / 'provenance.json').write_text(json.dumps(meta, indent=2))
    print(json.dumps({'outputs': states, 'source_body_pose_fixed': True, 'generated_art': False}))


if __name__ == '__main__':
    main()
