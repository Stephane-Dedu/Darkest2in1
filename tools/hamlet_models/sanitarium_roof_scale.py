"""Fit two finite native shingle fields around the retained Sanitarium cap.

The original Shroud atlas and muted slate material stay intact. Each half wraps
once, with joins under opposite existing hip bindings and more courses uphill.
All geometry, other mappings, paintings and service materials are retained.
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
from sanitarium_native_roof import CONE_RECT,native_uv

NAME='Sanitarium swept witch cap'
RECT=(260,260,930,820)


def build():
    ob=bpy.data.objects[NAME]
    assert len(ob.data.materials)==1
    assert ob.data.materials[0].name=='Sanitarium native slate shingles v64'
    uv=ob.data.uv_layers.active
    x0,y0,x1,y1=CONE_RECT
    records=[]
    for poly in ob.data.polygons:
        old=np.array([tuple(uv.data[li].uv) for li in poly.loop_indices])
        u=(old[:,0]*2048-x0)/(x1-x0)
        v=(old[:,1]*2048-(2048-y1))/(y1-y0)
        assert u.min()>-1e-5 and u.max()<1+1e-5
        assert v.min()>-1e-5 and v.max()<1+1e-5
        # The half boundary is an existing twelve-facet hip. Choose the panel
        # by the face midpoint so shared boundary loops retain either0 or1.
        panel=min(1,int(np.floor((u.min()+u.max())+1e-5)))
        local=2*u-panel
        assert local.min()>-1e-5 and local.max()<1+1e-5
        assert np.ptp(local)<=1/6+1e-5
        for li,a,b in zip(poly.loop_indices,local,v):
            uv.data[li].uv=native_uv(RECT,a,b)
        records.append({'polygon':poly.index,'half':panel,'u_range':[float(local.min()),float(local.max())]})
    assert {p['half'] for p in records}=={0,1}
    (G.OUT/'sanitarium-roof-scale.json').write_text(json.dumps({
      'member':NAME,'source_rect_before':CONE_RECT,'source_rect_after':RECT,
      'finite_halves':2,'seam_angles_degrees':[105,285],
      'source_pixels_unchanged':True,'geometry_and_materials_unchanged':True,
      'other_UVs_unchanged':True,'faces':records},indent=2),encoding='utf-8')


if __name__=='__main__':
    parser=argparse.ArgumentParser(description=__doc__)
    for name in ('source','out','kit'):parser.add_argument('--'+name,type=Path,required=True)
    parser.add_argument('--resolution',type=int,default=1000)
    parser.add_argument('--samples',type=int,default=12)
    args=parser.parse_args(sys.argv[sys.argv.index('--')+1:])
    source,out=args.source.resolve(),args.out.resolve()
    if out.exists() or source in out.parents or out in source.parents:
        parser.error('Use a new output folder separate from the source library.')
    W.run(args,refiners={'sanitarium':build},detail='v81',
      design='Two finite original Shroud shingle fields on retained turret; denser courses and opposite hip seams, paint/palette/geometry and all other UVs retained')
