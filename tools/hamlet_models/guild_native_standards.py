"""Fit complete native DD2 heraldry to the Guild's retained hanging rods.

No raster edits: preserve original pixels, alpha, full UVs and source aspect.
The native sprite already includes painted folds or a rigid frame. Keep it on
an aspect-correct plane rather than deforming its artwork over the old cloth.
"""
import argparse
import hashlib
import json
import sys
from pathlib import Path

import bpy
import numpy as np

sys.path.insert(0,str(Path(__file__).resolve().parent))
import geometry as G
import workshops as W
from caravan_canvas import native_paint


def build(kit):
    source=kit/'sprites2d/inn_crests/inn_crest_theoldeguildhall.png'
    image=bpy.data.images.load(str(source),check_existing=True)
    expected=(347,572)
    assert tuple(image.size)==expected
    old=sorted([o for o in bpy.data.objects if o.type=='MESH' and
                o.name.startswith('Guild painted sword standard')],key=lambda o:o.name)
    assert len(old)==2 and all(len(o.data.polygons)==36 for o in old)
    original=np.array([tuple(o.matrix_world@v.co) for o in bpy.data.objects
                       if o.type=='MESH' for v in o.data.vertices])
    removed=[o.name for o in old]
    for ob in old:bpy.data.objects.remove(ob,do_unlink=True)
    factor=(.90,.82,.68,1)
    material=native_paint('Guild native hanging standards v66',image,factor)
    tree=material.node_tree
    texture=next(n for n in tree.nodes if n.type=='TEX_IMAGE')
    shader=next(n for n in tree.nodes if n.type=='BSDF_PRINCIPLED')
    tree.links.new(texture.outputs['Alpha'],shader.inputs['Alpha'])
    width=1.10;height=width*image.size[1]/image.size[0];top=5.05;depth=-2.825
    made=[]
    for i,x in enumerate((-2.35,1.05)):
        ob=G.mesh('Guild native hanging standard '+str(i+1),
          [(x-width/2,top-height,depth),(x+width/2,top-height,depth),
           (x+width/2,top,depth),(x-width/2,top,depth)],
          [[0,1,2,3]],material,[[(0,0),(1,0),(1,1),(0,1)]])
        made.append({'name':ob.name,'centre_x':x,'width':width,'height':height,
                     'top':top,'depth':depth})
    bpy.context.view_layer.update()
    points=np.array([tuple(o.matrix_world@v.co) for o in bpy.data.objects
                     if o.type=='MESH' for v in o.data.vertices])
    assert np.array_equal(points.min(0),original.min(0)) and np.array_equal(points.max(0),original.max(0))
    (G.OUT/'native-standards.json').write_text(json.dumps({
      'source':str(source.resolve()),'source_sha256':hashlib.sha256(source.read_bytes()).hexdigest(),
      'native_size':expected,'candidate':'guildhall','removed_meshes':removed,'standards':made,
      'material':material.name,'base_factor':factor,'full_source_UVs_and_aspect':True,
      'rods_and_physical_crossed_swords_retained':True,'native_pixels_and_alpha_exact':True,
      'raster_edits':False,'generated_art':False},indent=2),encoding='utf-8')


if __name__=='__main__':
    parser=argparse.ArgumentParser(description=__doc__)
    for name in ('source','out','kit'):parser.add_argument('--'+name,type=Path,required=True)
    parser.add_argument('--resolution',type=int,default=1100)
    parser.add_argument('--samples',type=int,default=12)
    args=parser.parse_args(sys.argv[sys.argv.index('--')+1:])
    W.run(args,refiners={'guild':lambda:build(args.kit.resolve())},detail='v66',
          design='Native DD2 hanging heraldry, complete source pixels/alpha/aspect; retained Guild rods and physical sword crest')
