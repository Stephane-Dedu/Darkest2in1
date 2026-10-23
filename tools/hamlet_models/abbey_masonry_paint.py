"""Fit a reviewed detailed Abbey stone repaint without changing geometry or UVs.

Preserve the existing five material factors and all fitted stone cutout mappings.
The private painting keeps the previous course layout and shared grey palette.
"""
import argparse
import hashlib
import json
import sys
from pathlib import Path

import bpy

sys.path.insert(0,str(Path(__file__).resolve().parent))
import geometry as G
import workshops as W

TARGETS=('Fitted weathered limestone v42','Fitted limestone shadow v42',
         'Hamlet abbey wall v40','Hamlet abbey wall v40.001',
         'Hamlet painted limestone mouldings v43')


def build(paint):
    image=bpy.data.images.load(str(paint.resolve()),check_existing=True)
    assert min(image.size)>=1254 and image.size[0]==image.size[1]
    matched={};records=[]
    for name in TARGETS:
        material=bpy.data.materials[name]
        nodes=[n for n in material.node_tree.nodes if n.type=='TEX_IMAGE']
        assert 1<=len(nodes)<=2 and all(n.image and tuple(n.image.size)==(1254,1254) for n in nodes)
        for node in nodes:node.image=image
        material.name=name.replace('v40','v73').replace('v42','v73').replace('v43','v73')
        matched[material]=name
        records.append({'old_name':name,'new_name':material.name})
    members=[]
    for ob in bpy.data.objects:
        if ob.type!='MESH':continue
        materials=[m.name for m in ob.data.materials if m in matched]
        if materials:members.append({'name':ob.name,'materials':materials})
    assert len(records)==5 and len(members)>500
    (G.OUT/'abbey-masonry-paint.json').write_text(json.dumps({
      'source_texture':str(paint.resolve()),'source_sha256':hashlib.sha256(paint.read_bytes()).hexdigest(),
      'source_kind':'new detail repaint of earlier generated v40 stone, built-in ImageGen',
      'source_size':list(image.size),'materials':records,'changed_meshes':members,
      'geometry_changed':False,'UVs_changed':False,'material_factors_changed':False,
      'new_generated_art':True,'post_generation_raster_edits':False},indent=2),encoding='utf-8')


if __name__=='__main__':
    p=argparse.ArgumentParser(description=__doc__)
    for name in ('source','out','kit','paint'):p.add_argument('--'+name,type=Path,required=True)
    p.add_argument('--resolution',type=int,default=1100);p.add_argument('--samples',type=int,default=12)
    args=p.parse_args(sys.argv[sys.argv.index('--')+1:])
    W.run(args,refiners={'abbey':lambda:build(args.paint.resolve())},detail='v73',
          design='Detailed reviewed limestone repaint on five existing Abbey materials; all geometry/UVs/factors/native parts/new roof/other services retained')
