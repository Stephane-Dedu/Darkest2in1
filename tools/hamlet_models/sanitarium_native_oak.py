"""Fit native DD2 oak and inn doorway paint to the Sanitarium.

Original Assistance beam samples cover36 retained framing members. The native
inn's continuous boarded door field covers the existing door, at its actual detail
scale. Original2048 atlases/pixels, all geometry and other native parts remain.
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
from caravan_joinery import SAMPLES,sample_uv


def build(kit):
    folder=kit/'exp/common_story_assist_prefab'
    doc=json.loads((folder/'scene.json').read_text(encoding='utf-8'))
    src=next(o for o in doc['objects'] if o['name']=='story_assist_wagon_main')
    texture=folder/doc['materials'][src['materials'][0]]['texture']
    image=bpy.data.images.load(str(texture),check_existing=True)
    door_texture=kit/'exp/inn_lvl_03_prefab/tex--2625775420725934710.png'
    door_image=bpy.data.images.load(str(door_texture),check_existing=True)
    assert tuple(image.size)==tuple(door_image.size)==(2048,2048)
    oak=native_paint('Sanitarium native oak joinery v67',image,(.45,.38,.28,1))
    door_oak=native_paint('Sanitarium native oak boarded doorway v67',door_image,(.90,.80,.65,1))
    door_sample={'pixels':(379,1835,471,1987),'vertical':True,'kind':'native inn boarded door body'}
    objects=sorted((o for o in bpy.data.objects if o.type=='MESH' and
                    any(m.name=='Hamlet continuous painted timber' for m in o.data.materials)),key=lambda o:o.name)
    assert len(objects)==37 and all(len(o.data.materials)==1 for o in objects)
    assert sum('oak door' in o.name.lower() for o in objects)==1
    records=[]
    for index,o in enumerate(objects):
        points=np.array([tuple(v.co) for v in o.data.vertices]);lo=points.min(0)
        span=np.maximum(np.ptp(points,axis=0),.0001);long=int(span.argmax());uv=o.data.uv_layers.active
        door='oak door' in o.name.lower();material=door_oak if door else oak
        o.data.materials.clear();o.data.materials.append(material);faces=[]
        for face in o.data.polygons:
            axis=int(np.abs(face.normal).argmax());axes=[k for k in range(3) if k!=axis]
            if long in axes:axes=[long,next(k for k in axes if k!=long)]
            else:axes.sort(key=lambda k:span[k],reverse=True)
            ratio=float(span[axes[0]]/span[axes[1]])
            choices=(4,5,6) if ratio<8 else (0,)
            sample=door_sample if door else SAMPLES[choices[index%len(choices)]];offset=(index%7)/6
            for li in face.loop_indices:
                v=o.data.vertices[o.data.loops[li].vertex_index].co
                a,b=[(v[k]-lo[k])/span[k] for k in axes]
                if index%2:a=1-a
                uv.data[li].uv=sample_uv(sample,a,b,ratio,offset)
            face.material_index=0
            faces.append({'face':face.index,'axes':axes,'ratio':ratio,'source_sample':sample,'offset':offset})
        records.append({'name':o.name,'material':material.name,'faces':faces})
    (G.OUT/'sanitarium-native-oak.json').write_text(json.dumps({'changed_meshes':records,
      'source_texture':str(texture.resolve()),'source_sha256':hashlib.sha256(texture.read_bytes()).hexdigest(),
      'native_atlas_size':[2048,2048],'material':oak.name,'base_factor':[.45,.38,.28,1],
      'door_material':door_oak.name,'door_factor':[.90,.80,.65,1],
      'door_source_texture':str(door_texture.resolve()),'door_source_sha256':hashlib.sha256(door_texture.read_bytes()).hexdigest(),
      'geometry_changed':False,'raster_edits':False,'new_generated_art':False},indent=2),encoding='utf-8')


if __name__=='__main__':
    parser=argparse.ArgumentParser(description=__doc__)
    for name in ('source','out','kit'):parser.add_argument('--'+name,type=Path,required=True)
    parser.add_argument('--resolution',type=int,default=1100)
    parser.add_argument('--samples',type=int,default=12)
    args=parser.parse_args(sys.argv[sys.argv.index('--')+1:])
    W.run(args,refiners={'sanitarium':lambda:build(args.kit.resolve())},detail='v67',
          design='Native Assistance oak on36 framing members and native inn boards on the fitted door; all geometry/native house/slate/stone/plaster/green glass/iron retained')
