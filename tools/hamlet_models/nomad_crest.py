"""Replace the remaining DD1 Nomad eye sprite with DD2's native merchant crest.

Private Blender recipe. The complete427x464 native sprite keeps its pixels,
alpha and aspect. A rigid hanging sign replaces the old stretched cloth grid;
the existing suspension crossbar/posts, canopy, body and other services stay.
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
    source=kit/'sprites2d/inn_crests/inn_crest_nomadwagon.png'
    image=bpy.data.images.load(str(source),check_existing=True)
    assert tuple(image.size)==(427,464)
    old=bpy.data.objects['DD1 eye crest']
    assert old.type=='MESH' and len(old.data.polygons)==36
    original=np.array([tuple(o.matrix_world@v.co) for o in bpy.data.objects if o.type=='MESH' for v in o.data.vertices])
    bpy.data.objects.remove(old,do_unlink=True)
    material=native_paint('Nomad native timber merchant crest v59',image,(1,.95,.90,1))
    tree=material.node_tree
    texture=next(n for n in tree.nodes if n.type=='TEX_IMAGE')
    shader=next(n for n in tree.nodes if n.type=='BSDF_PRINCIPLED')
    tree.links.new(texture.outputs['Alpha'],shader.inputs['Alpha'])
    width=1.7;height=width*image.size[1]/image.size[0];top=6.60;z=-3.53
    ob=G.mesh('Nomad native painted merchant sign',
      [(-width/2,top-height,z),(width/2,top-height,z),(width/2,top,z),(-width/2,top,z)],
      [[0,1,2,3]],material,[[(0,0),(1,0),(1,1),(0,1)]])
    bpy.context.view_layer.update()
    new=np.array([tuple(ob.matrix_world@v.co) for v in ob.data.vertices])
    assert np.all(new.min(0)>=original.min(0)-1e-5) and np.all(new.max(0)<=original.max(0)+1e-5)
    (G.OUT/'native-crest.json').write_text(json.dumps({'source':str(source.resolve()),
      'source_sha256':hashlib.sha256(source.read_bytes()).hexdigest(),'native_size':[427,464],
      'removed_mesh':'DD1 eye crest','new_mesh':ob.name,'width':width,'height':height,'top':top,'depth':z,
      'material':material.name,'base_factor':[1,.95,.90,1],
      'full_sprite_UVs':True,'native_pixels_alpha_and_aspect_preserved':True,
      'raster_edits':False,'new_generated_art':False},indent=2),encoding='utf-8')


if __name__=='__main__':
    parser=argparse.ArgumentParser(description=__doc__)
    for name in ('source','out','kit'):parser.add_argument('--'+name,type=Path,required=True)
    parser.add_argument('--resolution',type=int,default=1000)
    parser.add_argument('--samples',type=int,default=12)
    args=parser.parse_args(sys.argv[sys.argv.index('--')+1:])
    W.run(args,refiners={'nomad_wagon':lambda:build(args.kit.resolve())},detail='v59',
          design='Native DD2 Nomad Wagon merchant crest, original pixels/alpha/aspect on a rigid hanging sign')
