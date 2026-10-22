"""Add a DD1-readable Guild weapon stand with intact native DD2 weapon paint.

Blender authoring only. Native inputs are private export_prefab.py outputs for
forest_weapon_sword_01/02 and barricade_military_decor_shield_01. The battlefield
rack, skulls and cyan cloth are deliberately excluded from the civic frontage.
"""
import argparse
import hashlib
import json
import math
import sys
from pathlib import Path

import bpy
import numpy as np
from mathutils import Matrix, Vector

sys.path.insert(0, str(Path(__file__).resolve().parent))
import geometry as G
import workshops as W

PARTS = (
    ('forest_weapon_sword_01', (-2.94, .44, -3.09), 2.12, -5),
    ('forest_weapon_sword_02', (-2.29, .44, -3.07), 1.98, 7),
    ('forest_weapon_sword_01', (-3.46, .44, -2.98), 2.28, 4),
    ('barricade_military_decor_shield_01', (-3.52, .44, -3.24), 1.44, -9),
)


def build(root, lib):
    old = bpy.data.objects.get('camp_axe / camp_axe')
    assert old is not None, 'Expected original Guild axe'
    bpy.data.objects.remove(old, do_unlink=True)
    oak = bpy.data.materials['Hamlet continuous painted timber']

    def beam(name, a, b, width):
        return W.paint(G.beam('Guild training stand ' + name, a, b, width, oak),
                       oak, (.02, .07, .98, .93), grain=True)

    # The original terrace is 8.8 by 7m. Nothing changes its normalization or the
    # clear 1.65m entrance; broad beams remain visible at the normal town camera.
    for x in (-3.91, -1.86):
        beam('upright', (x, .43, -2.91), (x, 1.80, -2.91), .11)
        beam('foot', (x, .48, -3.36), (x, .48, -2.70), .13)
        beam('knee', (x, .51, -3.27), (x, 1.18, -2.91), .085)
    beam('upper rail', (-3.99, 1.66, -2.96), (-1.78, 1.66, -2.96), .13)
    beam('lower rail', (-3.99, .90, -2.91), (-1.78, .90, -2.91), .10)

    evidence = []
    for index, (asset, pos, height, roll) in enumerate(PARTS):
        folder = root / asset
        doc = json.loads((folder / 'scene.json').read_text())
        assert len(doc['objects']) == 1, 'Use the original isolated native prop'
        lo, hi = lib.bbox(folder)
        scale = height / (hi[1] - lo[1])
        place, _ = lib.place_matrix(folder, pos, 0, scale)
        objects, _, _ = lib.build(folder, place, shade=1.4)
        assert len(objects) == 1
        G.exportable_native_materials(objects)
        pivot = Vector(G.p(pos))
        # Lean rigidly about the ground anchor; keep all native proportions/UVs.
        lean = Matrix.Rotation(math.radians(roll), 4, 'Y')
        for ob in objects:
            ob.name = f'Guild native training {index + 1} / {asset}'
            ob.matrix_world = Matrix.Translation(pivot) @ lean @ Matrix.Translation(-pivot) @ ob.matrix_world
            ob['source_asset'] = asset
        files = {folder / 'scene.json'}
        for ob in doc['objects']:
            files.add(folder / ('mesh-' + ob['mesh'] + '.npz'))
            files.update(folder / doc['materials'][m]['texture'] for m in ob['materials'])
        evidence.append({'asset': asset, 'position': pos, 'height': height,
                         'uniform_scale': float(scale), 'roll_degrees': roll,
                         'objects': [o.name for o in objects],
                         'files': {str(p.resolve().relative_to(root.resolve())):
                                   hashlib.sha256(p.read_bytes()).hexdigest() for p in files}})
    bpy.context.view_layer.update()
    added = [o for o in bpy.data.objects if o.type == 'MESH'
             and o.name.startswith(('Guild training stand ', 'Guild native training '))]
    points = np.array([tuple(o.matrix_world @ v.co) for o in added for v in o.data.vertices])
    lo, hi = points.min(0), points.max(0)
    assert lo[0] >= -4.4 and hi[0] < -1.6, 'Keep the entrance clear'
    assert lo[1] >= -3.5 and hi[1] <= -2.5, 'Keep within the original terrace'
    assert lo[2] >= .4 and hi[2] < 2.8, 'Stay below the standards'
    (G.OUT / 'guild-frontage.json').write_text(json.dumps({
        'native_parts': evidence, 'new_bitmaps': False,
        'reused_paint': oak.name, 'replaced_native': 'camp_axe / camp_axe',
        'added_bounds_blender': [lo.tolist(), hi.tolist()]}, indent=2))


def run(args):
    sys.path.insert(0, str(args.kit.resolve() / 'tools'))
    import bl_lib as lib
    W.run(args, refiners={'guild': lambda: build(args.native_root.resolve(), lib)},
          detail='v51', replaced_native=('camp_axe / camp_axe',),
          design='DD1 Guild entrance weapon stand; intact native DD2 swords/shield and shared oak')


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    for name in ('source', 'out', 'kit', 'native-root'):
        parser.add_argument('--' + name, type=Path, required=True)
    parser.add_argument('--resolution', type=int, default=1100)
    parser.add_argument('--samples', type=int, default=16)
    run(parser.parse_args(sys.argv[sys.argv.index('--') + 1:]))
