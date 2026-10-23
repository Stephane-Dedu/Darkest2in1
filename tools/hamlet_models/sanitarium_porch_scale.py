"""Fit finer native Shroud courses to the retained Sanitarium porch and caps."""
import argparse
import json
import sys
from pathlib import Path

import bpy
import numpy as np

sys.path.insert(0,str(Path(__file__).resolve().parent))
import geometry as G
import workshops as W
from sanitarium_native_roof import PORCH_RECT,native_uv

NAMES=('Sanitarium porch painted roof','Sanitarium porch fitted ridge saddles')
RECT=(450,350,740,820)


def build():
    records=[]
    for name in NAMES:
        ob=bpy.data.objects[name]
        assert {m.name for m in ob.data.materials}=={
            'Sanitarium native slate shingles v64','Sanitarium native slate cut edges v64'}
        uv=ob.data.uv_layers.active;x0,y0,x1,y1=PORCH_RECT
        for poly in ob.data.polygons:
            for li in poly.loop_indices:
                old=uv.data[li].uv
                u=(old.x*2048-x0)/(x1-x0)
                v=(old.y*2048-(2048-y1))/(y1-y0)
                assert -1e-5<=u<=1+1e-5 and -1e-5<=v<=1+1e-5
                uv.data[li].uv=native_uv(RECT,u,v)
        records.append({'member':name,'polygons':len(ob.data.polygons)})
    (G.OUT/'sanitarium-porch-scale.json').write_text(json.dumps({
        'members':records,'source_rect_before':PORCH_RECT,'source_rect_after':RECT,
        'native_pixels_geometry_materials_and_other_UVs_unchanged':True,
        'mapping':'one finite fuller panel per retained slope, caps share world phase'},indent=2))


if __name__=='__main__':
    p=argparse.ArgumentParser(description=__doc__)
    for name in ('source','out','kit'):p.add_argument('--'+name,type=Path,required=True)
    p.add_argument('--resolution',type=int,default=1000);p.add_argument('--samples',type=int,default=12)
    args=p.parse_args(sys.argv[sys.argv.index('--')+1:]);source,out=args.source.resolve(),args.out.resolve()
    if out.exists() or source in out.parents or out in source.parents:p.error('Use a new output folder separate from the source library.')
    W.run(args,refiners={'sanitarium':build},detail='v82',
          design='Finer finite native Shroud porch courses, same physical caps/phase, palette/geometry/paint retained')
