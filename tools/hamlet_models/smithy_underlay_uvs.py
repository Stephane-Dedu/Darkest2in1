"""Give the smithy's collapsed solidified roof edges a finite painted area.

Only degenerate UV triangles on the two retained underlays change. The physical
reed courses, architecture, materials, paintings and nine other services remain.
Run under Blender against a verified private raw library, then review in game.
"""
import argparse
import json
import sys
from pathlib import Path

import bpy
import numpy as np

sys.path.insert(0,str(Path(__file__).resolve().parent))
import geometry as G
import workshops as W

NAMES=('Smithy continuous thatched roof','Smithy dormer fitted roof')


def uv_area(coords):
    a,b,c=coords[:3]
    return abs((b[0]-a[0])*(c[1]-a[1])-(b[1]-a[1])*(c[0]-a[0]))*.5


def build():
    bpy.context.view_layer.update();records=[]
    for name in NAMES:
        ob=bpy.data.objects[name];uv=ob.data.uv_layers.active
        points=np.array([tuple(ob.matrix_world@v.co) for v in ob.data.vertices])
        lo=points.min(0);span=np.ptp(points,axis=0)
        assert np.isfinite(points).all() and min(span)>0
        changed=[]
        for face in ob.data.polygons:
            assert len(face.vertices)==3,'Expected the verified triangulated GLB'
            if face.area<=1e-8 or uv_area([uv.data[li].uv for li in face.loop_indices])>=1e-9:continue
            normal=(ob.matrix_world.to_3x3().inverted().transposed()@face.normal).normalized()
            # Project along the dominant normal. A whole-object phase keeps
            # adjacent edge triangles continuous without seam-sized paint strips.
            drop=max(range(3),key=lambda axis:abs(normal[axis]))
            axes=[axis for axis in range(3) if axis!=drop]
            for li in face.loop_indices:
                p=points[ob.data.loops[li].vertex_index]
                uv.data[li].uv=tuple(.04+.92*(p[axis]-lo[axis])/span[axis] for axis in axes)
            coords=[uv.data[li].uv for li in face.loop_indices]
            assert np.isfinite(coords).all() and min(v for p in coords for v in p)>=.03999
            assert max(v for p in coords for v in p)<=.96001 and uv_area(coords)>face.area*.001
            changed.append(face.index)
        assert changed,name
        records.append({'name':name,'changed_triangles':len(changed),'source_triangle_indices':changed})
    (G.OUT/'smithy-underlay-uvs.json').write_text(json.dumps({
      'members':records,'finite_image_margin':.04,'projection':'dominant world-normal axis, shared whole-member phase',
      'geometry_materials_factors_paint_and_other_UVs_changed':False,
      'physical_reed_bundles_retained':True,'new_generated_art':False},indent=2),encoding='utf-8')


if __name__=='__main__':
    p=argparse.ArgumentParser(description=__doc__)
    for name in ('source','out','kit'):p.add_argument('--'+name,type=Path,required=True)
    p.add_argument('--resolution',type=int,default=1000);p.add_argument('--samples',type=int,default=12)
    args=p.parse_args(sys.argv[sys.argv.index('--')+1:])
    W.run(args,refiners={'blacksmith':build},detail='v78',
          design='Finite painted area on collapsed smithy roof underlay edges; existing geometry, reed courses, paint, brick/oak and nine services retained')
