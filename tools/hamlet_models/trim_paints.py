"""Unify fitted service stonework with the reviewed broad-facet wall painting.

Blender authoring only. Reuse private textures/abbey_wall_v40.png, unchanged.
Preserve all geometry, native art, material factors and non-trim UVs.
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

# Whole stone faces retain the native-reference painting's edge highlight/ink.
# Quiet interior-only crops made fitted trim read as unpainted grey primitives.
STONES = ((145,6,397,151),(393,164,689,333),(483,335,782,499),
          (70,501,346,665),(370,669,775,833),(160,835,600,983),
          (615,986,908,1183),(194,985,608,1177))


def fit(service):
    path = G.OUT / 'textures/abbey_wall_v40.png'
    image = bpy.data.images.load(str(path), check_existing=True)
    assert tuple(image.size) == (1254,1254)
    matched = set()
    for material in bpy.data.materials:
        if not material.name.startswith(('Fitted weathered limestone v25','Fitted limestone shadow v25')):
            continue
        nodes = [n for n in material.node_tree.nodes if n.type == 'TEX_IMAGE']
        assert 1 <= len(nodes) <= 2
        for node in nodes:
            node.image = image
        matched.add(material)
        material.name = material.name.replace('v25','v42')
    assert matched, service
    records = []
    for ob in bpy.data.objects:
        if ob.type != 'MESH' or not any(m in matched for m in ob.data.materials):
            continue
        index = int.from_bytes(hashlib.sha256(ob.name.encode()).digest()[:2], 'little') % len(STONES)
        x0,y0,x1,y1 = STONES[index]
        rect = (x0/1254,1-y1/1254,x1/1254,1-y0/1254)
        points = np.array([v.co[:] for v in ob.data.vertices])
        low = points.min(0); span = np.maximum(.001,points.max(0)-low)
        uv = ob.data.uv_layers.active
        count = 0
        for face in ob.data.polygons:
            if ob.data.materials[face.material_index] not in matched:
                continue
            axis = max(range(3),key=lambda k:abs(face.normal[k]))
            axes = [k for k in range(3) if k != axis]
            for li in face.loop_indices:
                p = ob.data.vertices[ob.data.loops[li].vertex_index].co
                u,v = [(p[k]-low[k])/span[k] for k in axes]
                uv.data[li].uv = (rect[0]+u*(rect[2]-rect[0]),rect[1]+v*(rect[3]-rect[1]))
            count += 1
        records.append({'mesh':ob.name,'patch':index,'faces':count})
    (G.OUT/(service+'-trim-fit.json')).write_text(json.dumps({'source_sha256':hashlib.sha256(path.read_bytes()).hexdigest(),'meshes':records},indent=2))


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    for name in ('source','out','kit'):
        parser.add_argument('--'+name,type=Path,required=True)
    parser.add_argument('--resolution',type=int,default=1100)
    parser.add_argument('--samples',type=int,default=16)
    args = parser.parse_args(sys.argv[sys.argv.index('--')+1:])
    W.run(args,refiners={k:lambda k=k:fit(k) for k in ('abbey','guild','sanitarium')},
          detail='v42',design='Fitted stone trim shares the reviewed facade painting and service palette')
