"""Fit continuous-grain oak planks inside the Abbey's existing pointed door.

Blender-only recipe. Use the reviewed full wood painting through four grain strips;
no new bitmap. Preserve the oak backing, fitted iron and all service bounds.
"""
import argparse
import json
import sys
from pathlib import Path

import bpy

sys.path.insert(0,str(Path(__file__).resolve().parent))
import architecture as A
import geometry as G
import workshops as W

# Reviewed 900x80px continuous grain, matching the roughly11:1 physical plank.
STRIPS=((864,144,1764,224),(576,357,1476,437),(504,595,1404,675),(48,696,948,776))


def clip(points, boundary, keep_left):
    result=[]
    for a,b in zip(points,points[1:]+points[:1]):
        inside_a=a[0]<=boundary if keep_left else a[0]>=boundary
        inside_b=b[0]<=boundary if keep_left else b[0]>=boundary
        if inside_a: result.append(a)
        if inside_a != inside_b:
            t=(boundary-a[0])/(b[0]-a[0])
            result.append((boundary,a[1]+(b[1]-a[1])*t))
    return result


def area(points):
    return abs(sum(a[0]*b[1]-b[0]*a[1] for a,b in zip(points,points[1:]+points[:1])))/2


def abbey():
    joints=[o for o in bpy.data.objects if o.type=='MESH' and o.name.startswith('Abbey great portal plank joint')]
    assert len(joints)==5
    for ob in joints:bpy.data.objects.remove(ob,do_unlink=True)
    x,y,width,height=-2.55,1.32,1.65,2.9
    outline=A.pointed(x,y+.04,width*.91,height-.11)
    left=x-width*.91/2; step=width*.91/6
    mat=G.material('Abbey fitted oak planks',(.85,.82,.78,1),'wood_v20',.45)
    records=[]
    for i in range(6):
        lo=left+i*step+.0035;hi=left+(i+1)*step-.0035
        shape=clip(clip(outline,lo,False),hi,True);assert len(shape)>=4
        ob=A.prism('Abbey continuous grain plank',shape,-4.079,-4.055,mat)
        x0,y0,x1,y1=STRIPS[[0,1,3,2,0,3][i]]
        # Outer boards end at the pointed head. Keep world grain density rather
        # than stretching the same entire strip onto a shorter board.
        ratio=(max(p[1] for p in shape)-min(p[1] for p in shape))/(height-.11)
        rect=(x0/1774,1-y1/887,(x0+(x1-x0)*ratio)/1774,1-y0/887)
        W.paint(ob,mat,rect,grain=True)
        records.append({'outline':shape,'uv_rect':rect,'area':area(shape)})
    coverage=sum(r['area'] for r in records)/area(outline)
    assert .96<coverage<.995
    (G.OUT/'plank-fit.json').write_text(json.dumps({'planks':records,'coverage':coverage,
        'backing_and_iron_retained':True,'original_paint_reused':True},indent=2))


if __name__=='__main__':
    parser=argparse.ArgumentParser(description=__doc__)
    for name in ['source','out','kit']:parser.add_argument('--'+name,type=Path,required=True)
    parser.add_argument('--resolution',type=int,default=1100)
    parser.add_argument('--samples',type=int,default=16)
    args=parser.parse_args(sys.argv[sys.argv.index('--')+1:])
    W.run(args,refiners={'abbey':abbey},detail='v39',
          design='Abbey fitted oak planks with continuous grain, existing backing and iron retained')
