"""Assemble a regional shallow grave from complete native DD2 components.

Keep the complete wooden marker and low earth/remains layer in both states.
Remove only the complete upper mound after use. Original geometry, UVs, paint
and tint remain native; assembly placement and the used state are adaptations.
Run inside Blender. Private game art must remain outside the repository.
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
        parser.add_argument('--' + key, type=Path, required=True)
    args = parser.parse_args(sys.argv[sys.argv.index('--') + 1:])
    source, out = args.source.resolve(), args.out.resolve()
    repo = Path(__file__).resolve().parents[2]
    if source.is_relative_to(repo) or out.is_relative_to(repo):
        raise ValueError('Keep private native art outside Git')
    if out.exists():
        raise ValueError('Use a new output folder')
    definitions = {
        'marker': ('field_hospital_grave_single_03', 640, 416, (-.75, 0, .35), 1.15),
        'rim': ('forest_faction_barricade_dirt_pile_03', 134, 162, (0, 0, 0), 1),
        'mound': ('forest_faction_barricade_dirt_pile_02', 283, 414, (0, 0, .1), .72),
    }
    sys.path.insert(0, str(args.kit.resolve() / 'tools'))
    import bl_lib as native
    native.reset()
    parts, dependencies, points = {}, [], {}
    for role, (name, count, faces, position, scale) in definitions.items():
        folder = source / name
        doc = json.loads((folder / 'scene.json').read_text())
        if not doc['source'].endswith('/' + name + '.fbx') or len(doc['objects']) != 1 or doc.get('skipped'):
            raise ValueError('Use the complete reviewed native components')
        part = doc['objects'][0]
        if not part['active'] or not part['enabled']:
            raise ValueError('Disabled native component')
        mesh_path = folder / ('mesh-' + part['mesh'] + '.npz')
        data = np.load(mesh_path)
        if (len(data['v']), len(data['tri'])) != (count, faces):
            raise ValueError('Native topology changed; review the complete component')
        place, _ = native.place_matrix(folder, position, rot=180 if role == 'marker' else 0,
                                       scale=scale, respect_active=True)
        world = native.CHANGE @ place @ np.array(part['world'])
        points[role] = (np.c_[data['v'], np.ones(len(data['v']))] @ world.T)[:, :3]
        parts[role] = {'folder': folder, 'place': place, 'faces': faces}
        dependencies += [folder / 'scene.json', mesh_path]
        dependencies += [(folder / material['texture']).resolve()
                         for material in doc['materials'].values() if material.get('texture')]
    all_points = np.concatenate(list(points.values()))
    low, high = all_points.min(0), all_points.max(0)
    center = (low + high) / 2
    extent = max(high[0] - low[0], high[2] - low[2]) * 1.15
    distance = max(12, extent * 2)
    out.mkdir(parents=True)
    records = []
    for state in ('fixed-reference', 'closed', 'used'):
        native.reset()
        roles = ('marker', 'rim', 'mound') if state == 'closed' else ('marker', 'rim')
        for role in roles:
            item = parts[role]
            made, _, _ = native.build(item['folder'], place=item['place'], respect_active=True)
            if len(made) != 1 or len(made[0].data.polygons) != item['faces']:
                raise ValueError('A native component was truncated')
        camera = native.camera(
            (center[0], center[1] - distance, center[2] + distance * math.tan(math.radians(6))),
            tuple(center), ortho=float(extent), res=(1024, 1024))
        scene = bpy.context.scene
        scene.render.threads_mode = 'FIXED'
        scene.render.threads = 2
        scene.cycles.seed = 0
        path = out / (state + '.png')
        native.render(path, samples=4, transparent=True)
        if not path.is_file():
            raise RuntimeError('Missing field-grave render')
        fixed = np.concatenate([points['marker'], points['rim']])
        projected = np.array([[1024 * q.x, 1024 * (1 - q.y)] for q in
                              (world_to_camera_view(scene, camera, Vector(p)) for p in fixed)])
        lo, hi = projected.min(0), projected.max(0)
        records.append({'state': state, 'file': path.name,
                        'sha256': hashlib.sha256(path.read_bytes()).hexdigest(),
                        'foot': [(lo[0] + hi[0]) / 2, hi[1]],
                        'scale_landmarks': [[lo[0], hi[1]], [hi[0], hi[1]]]})
    (out / 'provenance.json').write_text(json.dumps({
        'art_id': 'shallow_grave_tangle', 'curio_id': 'shallow_grave', 'region': 'dd2_forest',
        'components': {role: {'source': definition[0], 'vertices': definition[1],
                              'triangles': definition[2], 'position': definition[3],
                              'uniform_scale': definition[4]} for role, definition in definitions.items()},
        'closed_vertices': 1057, 'closed_triangles': 992,
        'used_vertices': 774, 'used_triangles': 578,
        'all_selected_geometry_UVs_and_paint_retained': True,
        'marker_and_rim_fixed': True, 'marker_yaw_degrees': 180,
        'only_upper_mound_hides_after_use': True,
        'generated_art': False, 'installed': False, 'native_qa': False,
        'notice': 'Native field-hospital marker and Tangle earth/remains layers form an adapted shallow field grave. No new encounter, original used animation or dug-hole geometry is inferred. Original shovel/loot/blight/disease rules retain.',
        'files': [{'path': str(path), 'sha256': hashlib.sha256(path.read_bytes()).hexdigest()}
                  for path in sorted(set(dependencies))],
        'recipe_sha256': hashlib.sha256(Path(__file__).read_bytes()).hexdigest(), 'outputs': records
    }, indent=2))
    print('NATIVE_FIELD_GRAVE_PAIR_READY', out, flush=True)


if __name__ == '__main__':
    main()
