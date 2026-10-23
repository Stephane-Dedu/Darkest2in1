"""Fit original DD2 chipped stone to the Graveyard's retained posts and caps.

Only eight authored members change UVs/material bindings. Clone the existing
stone finish, preserve every original material and native headstone, and sample
finite uninscribed fields from the original2048 common_gravestones atlas.
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

PREFIXES=('Graveyard gatepost','Gatepost pointed cap')
OLD='Service native individual cut stone v24.001'
NEW='Service native individual cut stone v84'
POSTS=((572,600,636,850),(704,1115,770,1342))
CAP=(782,170,915,213)
SOURCE_COMPENSATION=.39


def mapped(rect,u,v):
    x0,y0,x1,y1=rect
    return ((x0+np.clip(u,0,1)*(x1-x0))/2048,1-(y1-np.clip(v,0,1)*(y1-y0))/2048)


def build(kit):
    source=kit/'exp/_tex/tex-790830617910084663-2048x2048.png'
    image=bpy.data.images.load(str(source.resolve()),check_existing=True)
    assert tuple(image.size)==(2048,2048)
    material=bpy.data.materials[OLD].copy();material.name=NEW
    nodes=[n for n in material.node_tree.nodes if n.type=='TEX_IMAGE'];assert nodes
    for node in nodes:node.image=image
    # The new source is brighter than the retaining-stone sample. Compensate
    # in linear material light, leaving original source pixels untouched.
    factors=0
    for node in material.node_tree.nodes:
        for socket in node.inputs:
            if socket.type=='RGBA' and not socket.is_linked and np.allclose(socket.default_value[:3],(.92,.99,.96),rtol=0,atol=2e-6):
                socket.default_value=(*(v*SOURCE_COMPENSATION for v in socket.default_value[:3]),socket.default_value[3]);factors+=1
    assert factors>=1,'Source stone base factor changed'
    shader=next(n for n in material.node_tree.nodes if n.type=='BSDF_PRINCIPLED')
    assert not shader.inputs['Emission Strength'].is_linked
    shader.inputs['Emission Strength'].default_value*=SOURCE_COMPENSATION
    material.diffuse_color=(*(v*SOURCE_COMPENSATION for v in material.diffuse_color[:3]),material.diffuse_color[3])
    objects=sorted([o for o in bpy.data.objects if o.type=='MESH' and o.name.startswith(PREFIXES)],key=lambda o:o.name)
    assert len(objects)==8
    records=[]
    for ob in objects:
        assert [m.name for m in ob.data.materials]==[OLD],ob.name
        ob.data.materials.clear();ob.data.materials.append(material)
        points=np.array([tuple(ob.matrix_world@v.co) for v in ob.data.vertices]);lo,hi=points.min(0),points.max(0)
        assert min(hi-lo)>.1
        ordinal=0 if '.' not in ob.name else int(ob.name.rsplit('.',1)[1])
        is_cap=ob.name.startswith('Gatepost pointed cap');rect=POSTS[ordinal%len(POSTS)]
        uv=ob.data.uv_layers.active
        for poly in ob.data.polygons:
            normal=ob.matrix_world.to_3x3()@poly.normal;axis=int(np.argmax(np.abs(normal)))
            axes=(0,1) if axis==2 else ((1,2) if axis==0 else (0,2))
            field=CAP if is_cap or axis==2 else rect
            for li in poly.loop_indices:
                p=ob.matrix_world@ob.data.vertices[ob.data.loops[li].vertex_index].co
                u=(p[axes[0]]-lo[axes[0]])/(hi[axes[0]]-lo[axes[0]])
                v=(p[axes[1]]-lo[axes[1]])/(hi[axes[1]]-lo[axes[1]])
                uv.data[li].uv=mapped(field,u,v)
            poly.material_index=0
        records.append({'member':ob.name,'post_field':None if is_cap else rect,
                        'horizontal_or_cap_field':CAP,'triangles':sum(len(p.vertices)-2 for p in ob.data.polygons)})
    assert sum(p['triangles'] for p in records)==200
    (G.OUT/'graveyard-native-stone.json').write_text(json.dumps({
      'source':str(source.resolve()),'source_sha256':hashlib.sha256(source.read_bytes()).hexdigest(),
      'source_material':'common_gravestones','source_size':[2048,2048],
      'old_material':OLD,'new_material':NEW,'original_material_flags_cloned':True,
      'new_source_linear_compensation':SOURCE_COMPENSATION,'original11_materials_unchanged':True,
      'source_raster_edits':False,'new_generated_art':False,'geometry_changed':False,'members':records},indent=2),encoding='utf-8')


if __name__=='__main__':
    p=argparse.ArgumentParser(description=__doc__)
    for name in ('source','out','kit'):p.add_argument('--'+name,type=Path,required=True)
    p.add_argument('--resolution',type=int,default=1000);p.add_argument('--samples',type=int,default=12)
    args=p.parse_args(sys.argv[sys.argv.index('--')+1:]);source,out=args.source.resolve(),args.out.resolve()
    if out.exists() or source in out.parents or out in source.parents:p.error('Use a new output folder separate from the source library.')
    W.run(args,refiners={'graveyard':lambda:build(args.kit.resolve())},detail='v84',
          design='Original DD2 chipped stone on retained Graveyard posts/caps, finite uninscribed fields and cloned muted stone finish; other architecture/native paint retained')
