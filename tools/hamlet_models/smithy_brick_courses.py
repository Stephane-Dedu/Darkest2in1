"""Fit smaller firebrick courses without replacing the reviewed Blacksmith paint.

Keep a common physical projection on the furnace and both chimneys. The original
3.5 metre period made the painted bricks about58cm wide;2.4m retains readable
stylised facets while giving the tall stack more courses. Fitted arch UVs remain.
"""
import argparse
import json
import sys
from pathlib import Path

import bpy

sys.path.insert(0,str(Path(__file__).resolve().parent))
import geometry as G
import workshops as W

PERIOD=2.4
PREFIXES=('Tapered masonry chimney','Chimney cap','Forge recessed firebrick hood')


def build():
    bpy.context.view_layer.update()
    records=[]
    for ob in bpy.data.objects:
        if ob.type!='MESH' or not ob.name.startswith(PREFIXES):continue
        assert len(ob.data.materials)==1 and ob.data.materials[0].name=='Forge soot stained brick v21'
        uv=ob.data.uv_layers.active
        for face in ob.data.polygons:
            normal=ob.matrix_world.to_3x3()@face.normal
            axes=(0,2) if abs(normal.y)>abs(normal.x) else (1,2)
            if abs(normal.z)>.7:axes=(0,1)
            for li in face.loop_indices:
                v=ob.matrix_world@ob.data.vertices[ob.data.loops[li].vertex_index].co
                uv.data[li].uv=(v[axes[0]]/PERIOD,v[axes[1]]/PERIOD)
        records.append({'name':ob.name,'period_metres':PERIOD})
    assert len(records)==5
    (G.OUT/'smithy-brick-courses.json').write_text(json.dumps({
      'changed_meshes':records,'previous_period_metres':3.5,'period_metres':PERIOD,
      'approximate_course_height_metres':PERIOD/16,'geometry_changed':False,
      'material_and_paint_changed':False,'new_generated_art_used':False,
      'fitted_arch_UVs_retained':True},indent=2),encoding='utf-8')


if __name__=='__main__':
    p=argparse.ArgumentParser(description=__doc__)
    for name in ('source','out','kit'):p.add_argument('--'+name,type=Path,required=True)
    p.add_argument('--resolution',type=int,default=1100)
    p.add_argument('--samples',type=int,default=12)
    args=p.parse_args(sys.argv[sys.argv.index('--')+1:])
    W.run(args,refiners={'blacksmith':build},detail='v74',
          design='Smaller coherent firebrick courses on the retained chimneys and furnace; current painting/factors/all geometry/fitted arch UVs/accepted wood/thatch/native art and nine services retained')
