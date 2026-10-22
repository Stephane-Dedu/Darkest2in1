"""Restore DD1's four Abbey eave pinnacles with intact native Sprawl stonework.

Blender authoring only. Select complete disconnected corner columns from the
original city_tower_02 mesh, retain native UVs/paint and use uniform scale. Other
Abbey geometry and the nine other service GLBs stay unchanged.
"""
import argparse
import hashlib
import json
import sys
from pathlib import Path

import bpy
import numpy as np

sys.path.insert(0, str(Path(__file__).resolve().parent))
import geometry as G
import workshops as W


def islands(points, triangles):
    # UV seams may duplicate a physical vertex. Join only coincident positions,
    # then take whole connected pieces instead of cutting the painted geometry.
    _, ids = np.unique(np.round(points, 5), axis=0, return_inverse=True)
    parent = np.arange(ids.max()+1)

    def root(i):
        while parent[i] != i:
            parent[i] = parent[parent[i]]
            i = parent[i]
        return i

    for a, b, c in ids[triangles]:
        parent[root(b)] = root(a)
        parent[root(c)] = root(a)
    groups = {}
    for i, tri in enumerate(ids[triangles]):
        groups.setdefault(root(tri[0]), []).append(i)
    return list(groups.values())


def build(folder):
    doc = json.loads((folder/'scene.json').read_text(encoding='utf-8'))
    source = next(o for o in doc['objects'] if o['name'] == 'city_tower_02')
    assert 'first' not in source
    data = np.load(folder/('mesh-'+source['mesh']+'.npz'))
    points = (np.c_[data['v'], np.ones(len(data['v']))] @ np.array(source['world']).T)[:, :3]
    triangles = data['tri'].reshape(-1, 3)
    columns = []
    for indices in islands(points, triangles):
        p = points[np.unique(triangles[indices])]
        lo, hi = p.min(0), p.max(0)
        if hi[0]-lo[0] < 1 and hi[2]-lo[2] < 1 and hi[1]-lo[1] > 9 and hi[2] < 0:
            columns.append((lo, hi, np.asarray(indices)))
    columns.sort(key=lambda part: part[0][0])
    assert len(columns) == 2, 'Use the two complete front corner columns'
    # The rear belfry already uses this exact native tower. Reuse its imported
    # materials so the pinnacles share its grade and do not add GPU batches.
    materials = [bpy.data.materials[doc['materials'][key]['name']] for key in source['materials']]
    slots = np.repeat(np.arange(len(data['sub'])), data['sub'])
    assert len(slots) == len(triangles) and slots.max() < len(materials)
    evidence = []
    for index, x in enumerate((-1.12, .53, 2.18, 3.90)):
        lo, hi, selected = columns[index % 2]
        # Conceal the foot in the existing buttress/roof. The complete native
        # column rises through the eave; its point stays below the rear belfry.
        height = 2.55 if index % 2 == 0 else 2.64
        scale = height/(hi[1]-lo[1])
        anchor = np.array([x, 5.10, -2.73])
        centre = np.array([(lo[0]+hi[0])/2, lo[1], (lo[2]+hi[2])/2])
        faces = triangles[selected]
        used, remap = np.unique(faces.reshape(-1), return_inverse=True)
        placed = (points[used]-centre)*scale+anchor
        # G.mesh exchanges Unity Y/Z and reverses winding; keep per-loop native
        # UVs in the same order, including authored seams and chipped outlines.
        ob = G.mesh('Abbey native eave pinnacle '+str(index+1), placed.tolist(),
                    remap.reshape(-1, 3).tolist(), materials[0], data['uv'][faces].tolist())
        for material in materials[1:]:
            ob.data.materials.append(material)
        ob.data.polygons.foreach_set('material_index', slots[selected].astype(np.int32))
        ob['source_asset'] = 'city_tower_02 / complete corner column'
        G.exportable_native_materials([ob])
        evidence.append({'column': index % 2, 'triangles': len(selected),
                         'native_triangle_indices': selected.tolist(), 'uniform_scale': float(scale),
                         'height': height, 'anchor_unity': anchor.tolist(),
                         'source_bounds': [lo.tolist(), hi.tolist()]})
    textures = {folder/doc['materials'][key]['texture'] for key in source['materials']}
    (G.OUT/'abbey-pinnacles.json').write_text(json.dumps({'native_parts': evidence,
        'source_files': {str(p.resolve()): hashlib.sha256(p.read_bytes()).hexdigest()
                         for p in {folder/'scene.json', folder/('mesh-'+source['mesh']+'.npz')} | textures},
        'new_bitmaps': False, 'paint_or_uv_modification': False}, indent=2), encoding='utf-8')


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    for name in ('source', 'out', 'kit'):
        parser.add_argument('--'+name, type=Path, required=True)
    parser.add_argument('--resolution', type=int, default=1100)
    parser.add_argument('--samples', type=int, default=16)
    args = parser.parse_args(sys.argv[sys.argv.index('--')+1:])
    W.run(args, refiners={'abbey': lambda: build(args.kit.resolve()/'exp/city_tower_02')},
          detail='v54', design='DD1 Abbey eave pinnacles from complete native Sprawl columns; original paint/UVs/proportions')
