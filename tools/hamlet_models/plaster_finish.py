"""Fit a reviewed continuous plaster painting to the Sanitarium's architecture.

Blender authoring only. The private 2:1 painting is reviewed against native DD2
architecture before use. Existing geometry, native assets, material factors and
all other surfaces stay intact. World-sized sampling avoids oversized cracks.
"""
import argparse
import hashlib
import json
import math
import sys
from pathlib import Path

import bpy
import numpy as np

sys.path.insert(0,str(Path(__file__).resolve().parent))
import geometry as G
import workshops as W

TARGETS=('Sanitarium round stair tower','Sanitarium overhanging timber stage',
         'Sanitarium stone porch','Sanitarium house stone undercroft')


def build(paint):
    image=bpy.data.images.load(str(paint.resolve()),check_existing=True)
    width,height=image.size
    assert width>=1500 and height>=750 and abs(width/height-2)<.01
    records={}
    for name in TARGETS:
        o=bpy.data.objects[name]
        points=np.array([tuple(o.matrix_world@v.co) for v in o.data.vertices])
        lo,hi=points.min(0),points.max(0)
        materials={o.data.materials[f.material_index] for f in o.data.polygons
                   if ' lime' in o.data.materials[f.material_index].name.lower()}
        assert len(materials)==1
        material=next(iter(materials))
        nodes=[n for n in material.node_tree.nodes if n.type=='TEX_IMAGE']
        assert 1<=len(nodes)<=2
        for node in nodes:node.image=image
        uv=o.data.uv_layers.active
        for face in o.data.polygons:
            if o.data.materials[face.material_index]!=material:continue
            coords=[o.matrix_world@o.data.vertices[o.data.loops[i].vertex_index].co
                    for i in face.loop_indices]
            normal=o.matrix_world.to_3x3()@face.normal
            if abs(normal.z)>.7:
                # Finite cap mapping. Paved undercroft tops use a separate,
                # untouched material and never reach this branch.
                for li,p in zip(face.loop_indices,coords):
                    uv.data[li].uv=(.10+.80*(p.x-lo[0])/(hi[0]-lo[0]),
                                   .25+.60*(p.y-lo[1])/(hi[1]-lo[1]))
                continue
            if name in TARGETS[:2]:
                us=[(math.atan2(p.y+2,p.x+3)/math.tau)%1 for p in coords]
                if max(us)-min(us)>.5:us=[u+1 if u<.5 else u for u in us]
                # One continuous unwrap with its seam on the adjoining house.
                # Upper framed panels sample only intact lime above the rubble.
                bottom,top=(.02,.98) if name==TARGETS[0] else (.40,.77)
                for li,p,u in zip(face.loop_indices,coords,us):
                    uv.data[li].uv=(.02+.96*u,bottom+(top-bottom)*(p.z-lo[2])/(hi[2]-lo[2]))
            else:
                # The same painting describes a7.5m by4.2m wall. The porch and
                # undercroft therefore retain comparable stone/crack scale.
                axis=0 if abs(normal.y)>=abs(normal.x) else 1
                shift=.18 if name==TARGETS[2] else .03
                for li,p in zip(face.loop_indices,coords):
                    uv.data[li].uv=(shift+(p[axis]-lo[axis])/7.5,
                                   .02+(p.z-lo[2])/4.2)
        records[name]={'material':material.name,'bounds_blender':[lo.tolist(),hi.tolist()],
                       'mapping':'continuous cylindrical' if name in TARGETS[:2] else '7.5m x4.2m wall',
                       'factors_retained':True}
    (G.OUT/'plaster-fit.json').write_text(json.dumps({
        'surfaces':records,'source_image':str(paint.resolve()),
        'source_sha256':hashlib.sha256(paint.read_bytes()).hexdigest(),
        'decoded_size':[width,height],'native_assets_retained':True,
        'geometry_changes':False,'material_factors_changed':False,
        'other_UVs_changed':False,'mirroring':False},indent=2),encoding='utf-8')


if __name__=='__main__':
    parser=argparse.ArgumentParser(description=__doc__)
    for name in ('source','out','kit','paint'):parser.add_argument('--'+name,type=Path,required=True)
    parser.add_argument('--resolution',type=int,default=1100)
    parser.add_argument('--samples',type=int,default=16)
    args=parser.parse_args(sys.argv[sys.argv.index('--')+1:])
    W.run(args,refiners={'sanitarium':lambda:build(args.paint)},detail='v56',
          design='Reviewed DD2-reference broad plaster facets at physical wall scale; geometry, native art and common colours retained')
