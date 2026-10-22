"""Study DD2's native rope-bound hospital cross on the Sanitarium.

Blender authoring only; no generated art. Isolate the native cross's complete
UV island, retain its paint/proportions, and fit it to the existing sign location.
Native full-town acceptance is required before installing the resulting pack.
"""
import argparse
import hashlib
import json
import math
import sys
from pathlib import Path

import bpy
import bmesh
import numpy as np
from mathutils import Vector

sys.path.insert(0, str(Path(__file__).resolve().parent))
import geometry as G
import workshops as W

ASSET = 'common_field_hospital_prefab'
MESH = 'field_hospital_node_02'
UV_RECT = (.075, .39, .26, .57)


def sign():
    old = [o for o in bpy.data.objects if o.type == 'MESH' and o.name.startswith('Apothecary cross ')]
    assert len(old) == 2
    for ob in old:
        bpy.data.objects.remove(ob, do_unlink=True)
    objects = G.native(ASSET, include=[MESH], shade=1.4)
    assert len(objects) == 1
    ob = objects[0]
    bm = bmesh.new(); bm.from_mesh(ob.data)
    uv = bm.loops.layers.uv.active
    kept = [f for f in bm.faces if all(UV_RECT[0] < l[uv].uv.x < UV_RECT[2]
            and UV_RECT[1] < l[uv].uv.y < UV_RECT[3] for l in f.loops)]
    assert len(kept) == 115, 'Native cross island changed'
    dropped = [f for f in bm.faces if f not in set(kept)]
    bmesh.ops.delete(bm, geom=dropped, context='FACES')
    loose = [v for v in bm.verts if not v.link_faces]
    bmesh.ops.delete(bm, geom=loose, context='VERTS')
    bm.to_mesh(ob.data); bm.free(); ob.data.update()
    points = np.array([v.co[:] for v in ob.data.vertices])
    low, high = points.min(0), points.max(0)
    centre = (low + high) / 2
    scale = .66 / (high[2] - low[2])
    # Face the board toward the porch approach using a rigid horizontal turn.
    # Keep the native lean; bury its back slightly into the plaster for attachment.
    local = points - centre
    values, axes = np.linalg.eigh(np.cov(local[:, :2].T))
    normal = axes[:, 0]
    if normal[1] > 0:
        normal = -normal
    yaw = -math.pi / 2 - math.atan2(normal[1], normal[0])
    rotation = np.array(((math.cos(yaw), -math.sin(yaw)),
                         (math.sin(yaw), math.cos(yaw))))
    local[:, :2] = local[:, :2] @ rotation.T
    local *= scale
    target = np.array(G.p((2.17, 3.24, -3.52)))
    target[1] -= local[:, 1].max()
    for v, point in zip(ob.data.vertices, local):
        v.co = Vector(target + point)
    ob.name = 'Sanitarium native rope-bound medical cross'
    record = {'asset': ASSET, 'mesh': MESH, 'UV_rect': UV_RECT, 'triangles': 115,
              'uniform_scale': float(scale), 'target_blender': target.tolist(),
              'rigid_yaw_degrees': math.degrees(yaw),
              'native_UVs_and_proportions_retained': True, 'new_bitmaps': False}
    (G.OUT / 'medical-sign-fit.json').write_text(json.dumps(record, indent=2))


def run(args):
    sys.path.insert(0, str(args.kit.resolve() / 'tools'))
    import bl_lib as L
    def refine():
        G.LIB = L
        sign()
    folder = args.kit.resolve() / 'exp' / ASSET
    doc = json.loads((folder / 'scene.json').read_text())
    source = next(o for o in doc['objects'] if o['name'] == MESH)
    paths = [folder / 'scene.json', folder / ('mesh-' + source['mesh'] + '.npz')]
    paths += [folder / doc['materials'][m]['texture'] for m in source['materials']]
    W.run(args, refiners={'sanitarium': refine}, detail='v44-study',
          design='Native DD2 painted rope-bound medical cross; unchanged Sanitarium architecture')
    (args.out / 'medical-sign-sources.json').write_text(json.dumps({
        str(p.relative_to(args.kit.resolve())): hashlib.sha256(p.read_bytes()).hexdigest()
        for p in paths}, indent=2))


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    for name in ('source', 'out', 'kit'):
        parser.add_argument('--' + name, type=Path, required=True)
    parser.add_argument('--resolution', type=int, default=1100)
    parser.add_argument('--samples', type=int, default=16)
    run(parser.parse_args(sys.argv[sys.argv.index('--') + 1:]))
