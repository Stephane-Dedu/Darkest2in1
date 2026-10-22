"""Ground the Sanitarium tower with native Academic-house rubble relief.

Blender authoring only. Six native foundation stones retain their painted faces,
UVs and uniform proportions. Unpainted backing faces are excluded; the solid tower
closes their backs. Staggered courses use the house's existing material;
original service geometry and the other nine verified models stay unchanged.
"""
import argparse
import hashlib
import json
import math
import sys
from pathlib import Path

import bpy
import numpy as np

sys.path.insert(0, str(Path(__file__).resolve().parent))
import geometry as G
import workshops as W
from abbey_pinnacles import islands


def parts(folder):
    doc = json.loads((folder/'scene.json').read_text(encoding='utf-8'))
    source = next(o for o in doc['objects'] if o['name'] == 'valley_academic_house')
    assert 'first' not in source and len(source['materials']) == 1
    data = np.load(folder/('mesh-'+source['mesh']+'.npz'))
    points = (np.c_[data['v'], np.ones(len(data['v']))] @ np.array(source['world']).T)[:, :3]
    triangles = data['tri'].reshape(-1, 3)
    image = bpy.data.images.load(str(folder/doc['materials'][source['materials'][0]]['texture']),check_existing=True)
    rgba = np.array(image.pixels[:]).reshape(image.size[1],image.size[0],4)
    selected = []
    for indices in islands(points, triangles):
        p = points[np.unique(triangles[indices])]
        lo, hi = p.min(0), p.max(0); span = hi-lo
        if lo[2] > 4.2 and lo[1] < .3 and hi[1] < .65 and min(span) > .2 and max(span) < .8:
            # Native rubble includes black backing sheets that blend into the
            # source scene's ground. Keep the actual painted faces as relief,
            # leaving the original tower to close their concealed backs.
            bary = np.array([[1/3]*3,[.6,.2,.2],[.2,.6,.2],[.2,.2,.6],
                             [.45,.45,.1],[.45,.1,.45],[.1,.45,.45]])
            uv = data['uv'][triangles[indices]]
            samples = np.einsum('ka,nap->nkp',bary,uv)
            xx = np.clip((samples[:,:,0]*image.size[0]).astype(int),0,image.size[0]-1)
            yy = np.clip((samples[:,:,1]*image.size[1]).astype(int),0,image.size[1]-1)
            light = rgba[yy,xx,:3].max(2)
            cutoff = max(.002,float(np.quantile(light,.9))*.18)
            # Keep inked borders on painted faces. Only the effectively black
            # backing triangles disappear; do not trim the grey stone's outline.
            keep = light.mean(1)>cutoff*.5
            filtered = np.asarray(indices)[keep]
            assert len(filtered)>=2, 'Native stone must retain painted surfaces'
            selected.append((lo, hi, filtered))
    selected.sort(key=lambda p: p[0][0])
    assert len(selected) == 6, 'Use six rear foundation stones'
    return doc, source, data, points, triangles, selected


def build(folder):
    doc, source, data, points, triangles, stones = parts(folder)
    material_name = doc['materials'][source['materials'][0]]['name']
    material = bpy.data.materials[material_name]
    vertices, faces, uvs, records = [], [], [], []
    # The tower is radius1.38. Bury each native stone in the wall, exposing its
    # irregular front/top planes. The existing base course remains as backing.
    radius = 1.37
    for row, (bottom, height, count) in enumerate(((1.10, .35, 17), (1.40, .34, 18), (1.69, .36, 17))):
        for index in range(count):
            choice = (index*5+row*2) % len(stones)
            lo, hi, selected = stones[choice]
            angle = (index+(row % 2)*.5)*math.tau/count
            # Native X is the stone's long axis. Rotate it tangentially without
            # changing individual proportions or mirroring the painted wear.
            # These stones come from the house's rear foundation: their painted
            # outward side is native +Z. Point it away from the tower centre.
            yaw = angle-math.pi/2
            rotation = np.array([[math.cos(yaw), 0, -math.sin(yaw)],
                                 [0, 1, 0], [math.sin(yaw), 0, math.cos(yaw)]])
            scale = height/(hi[1]-lo[1])
            centre = np.array([(lo[0]+hi[0])/2, lo[1], (lo[2]+hi[2])/2])
            anchor = np.array([-3+radius*math.cos(angle), bottom, -2+radius*math.sin(angle)])
            tri = triangles[selected]; used, remap = np.unique(tri.reshape(-1), return_inverse=True)
            placed = ((points[used]-centre)*scale) @ rotation.T+anchor
            start = len(vertices);vertices.extend(placed.tolist())
            faces.extend((remap.reshape(-1, 3)+start).tolist());uvs.extend(data['uv'][tri].tolist())
            records.append({'part':choice,'row':row,'index':index,'triangle_indices':selected.tolist(),
                            'uniform_scale':float(scale),'rotation_unity':rotation.tolist(),
                            'anchor_unity':anchor.tolist(),'source_bounds':[lo.tolist(),hi.tolist()]})
    original = np.array([tuple(o.matrix_world@v.co) for o in bpy.data.objects if o.type=='MESH' for v in o.data.vertices])
    ob = G.mesh('Sanitarium native rubble plinth', vertices, faces, material, uvs)
    ob['source_asset'] = 'valley_academic_house / painted foundation relief'
    # Original native materials are already exportable; no extra material or image.
    bpy.context.view_layer.update()
    p = np.array([tuple(ob.matrix_world@v.co) for v in ob.data.vertices])
    assert p[:, 2].min() >= 1.09 and p[:, 2].max() <= 2.06
    assert np.all(p.min(0)>=original.min(0)-1e-5) and np.all(p.max(0)<=original.max(0)+1e-5), 'Stay inside the original service bounds'
    texture = folder/doc['materials'][source['materials'][0]]['texture']
    (G.OUT/'sanitarium-plinth.json').write_text(json.dumps({'native_parts':records,
        'source_files':{str(p.resolve()):hashlib.sha256(p.read_bytes()).hexdigest()
                        for p in (folder/'scene.json',folder/('mesh-'+source['mesh']+'.npz'),texture)},
        'material':material_name,'new_bitmaps':False,'paint_or_UV_modification':False},indent=2),encoding='utf-8')


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    for name in ('source', 'out', 'kit'):
        parser.add_argument('--'+name, type=Path, required=True)
    parser.add_argument('--resolution', type=int, default=1100)
    parser.add_argument('--samples', type=int, default=16)
    args = parser.parse_args(sys.argv[sys.argv.index('--')+1:])
    W.run(args, refiners={'sanitarium':lambda:build(args.kit.resolve()/'exp/valley_academic_house')},
          detail='v55',design='Native DD2 painted stone relief at the Sanitarium tower; original UVs/paint/proportions, black backing removed')
