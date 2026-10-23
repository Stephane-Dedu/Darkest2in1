"""Replace the Sanitarium's sparse lime painting with a reviewed detailed edit.

Retain the accepted cylindrical/world UVs, architecture, native assets and all
material factors. The new private2:1 painting must be compared with native DD2
references and rendered through the actual Core palette before installation.
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

TARGETS=('Sanitarium fitted lime v40','Hamlet smoke stained lime',
         'Sanitarium tower uninterrupted lime v40')


def build(paint):
    image=bpy.data.images.load(str(paint.resolve()),check_existing=True)
    width,height=image.size
    assert width>=1500 and height>=750 and abs(width/height-2)<.01
    records=[];changed=set()
    for name in TARGETS:
        material=bpy.data.materials[name]
        nodes=[n for n in material.node_tree.nodes if n.type=='TEX_IMAGE']
        assert 1<=len(nodes)<=2 and all(n.image and tuple(n.image.size)==(1774,887) for n in nodes)
        for node in nodes:node.image=image
        material.name=name.replace(' v40','')+' v79';changed.add(material)
        records.append({'previous_name':name,'new_name':material.name})
    members=[]
    for ob in bpy.data.objects:
        if ob.type!='MESH':continue
        if any(m in changed for m in ob.data.materials):members.append(ob.name)
    assert set(members)=={'Sanitarium house stone undercroft','Sanitarium stone porch',
                          'Sanitarium overhanging timber stage','Sanitarium round stair tower'}
    (G.OUT/'sanitarium-lime-paint.json').write_text(json.dumps({
      'source_texture':str(paint.resolve()),'source_sha256':hashlib.sha256(paint.read_bytes()).hexdigest(),
      'source_kind':'built-in ImageGen detail edit of accepted301 lime, native Academic-house reference',
      'decoded_size':[width,height],'materials':records,'members':members,
      'geometry_UVs_factors_and_native_assets_changed':False,'new_generated_art':True,
      'post_generation_raster_edits':False},indent=2),encoding='utf-8')


if __name__=='__main__':
    p=argparse.ArgumentParser(description=__doc__)
    for name in ('source','out','kit','paint'):p.add_argument('--'+name,type=Path,required=True)
    p.add_argument('--resolution',type=int,default=1100);p.add_argument('--samples',type=int,default=12)
    args=p.parse_args(sys.argv[sys.argv.index('--')+1:])
    out=args.out.resolve();source=args.source.resolve()
    if out.exists() or out==source or source in out.parents:raise ValueError('Use a new private output folder outside the source library.')
    W.run(args,refiners={'sanitarium':lambda:build(args.paint)},detail='v79',
          design='Richer worn lime, exposed masonry and damp-age detail on retained Sanitarium UVs; native roof/oak/green glass, architecture, factors and nine services preserved')
