"""Unify114 Smithy structural wood members with the Hamlet's native oak family.

The original Assistance2048 atlas supplies finite grain on108 framing members.
Six fitted door boards share one native inn paint field in world coordinates.
Preserve all geometry, work props, anvil sign, native building and forge.
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
    assert tuple(image.size)==(2048,2048)
    door_texture=kit/'exp/inn_lvl_03_prefab/tex--2625775420725934710.png'
    door_image=bpy.data.images.load(str(door_texture),check_existing=True)
    assert tuple(door_image.size)==(2048,2048)
    objects=sorted((o for o in bpy.data.objects if o.type=='MESH' and
                    any(m.name=='Workshop worn oak v21.001' for m in o.data.materials)),key=lambda o:o.name)
    assert len(objects)==114 and all(len(o.data.materials)==1 for o in objects)
    assert all(o.name.startswith('Smithy ') for o in objects)
    oak=native_paint('Smithy native oak joinery v68',image,(.45,.38,.28,1))
    door_oak=native_paint('Smithy native oak closed door v68',door_image,(.90,.80,.65,1))
    doors=[o for o in objects if o.name.startswith('Smithy fitted door board')]
    assert len(doors)==6
    points=np.array([tuple(o.matrix_world@v.co) for o in doors for v in o.data.vertices])
    door_lo=points.min(0);door_span=np.ptp(points,axis=0)
    assert door_span[0]>1 and door_span[2]>1 and door_span[1]<.3
    door_ratio=float(door_span[2]/door_span[0])
    door_sample={'pixels':(379,1835,471,1987),'vertical':True,'kind':'native inn boarded door body'}
    records=[]
    for index,o in enumerate(objects):
        points=np.array([tuple(v.co) for v in o.data.vertices]);lo=points.min(0)
        span=np.maximum(np.ptp(points,axis=0),.0001);long=int(span.argmax());uv=o.data.uv_layers.active
        door=o in doors;material=door_oak if door else oak
        o.data.materials.clear();o.data.materials.append(material);faces=[]
        for face in o.data.polygons:
            if door:
                # All six boards sample the same continuous world-space panel.
                # Per-board native beam crops would turn them into dark bars.
                for li in face.loop_indices:
                    p=o.matrix_world@o.data.vertices[o.data.loops[li].vertex_index].co
                    u=(p.x-door_lo[0])/door_span[0];v=(p.z-door_lo[2])/door_span[2]
                    uv.data[li].uv=sample_uv(door_sample,1-v,u,door_ratio,.5)
                face.material_index=0
                faces.append({'face':face.index,'axes':[2,0],'ratio':door_ratio,
                              'source_sample':door_sample,'offset':.5,'mapping':'shared world-space doorway'})
                continue
            axis=int(np.abs(face.normal).argmax());axes=[k for k in range(3) if k!=axis]
            if long in axes:axes=[long,next(k for k in axes if k!=long)]
            else:axes.sort(key=lambda k:span[k],reverse=True)
            ratio=float(span[axes[0]]/span[axes[1]])
            choices=(4,5,6) if ratio<8 else (0,)
            sample=SAMPLES[choices[index%len(choices)]];offset=(index%7)/6
            for li in face.loop_indices:
                v=o.data.vertices[o.data.loops[li].vertex_index].co
                a,b=[(v[k]-lo[k])/span[k] for k in axes]
                if index%2:a=1-a
                uv.data[li].uv=sample_uv(sample,a,b,ratio,offset)
            face.material_index=0
            faces.append({'face':face.index,'axes':axes,'ratio':ratio,'source_sample':sample,'offset':offset})
        records.append({'name':o.name,'material':material.name,'faces':faces})
    (G.OUT/'smithy-native-oak.json').write_text(json.dumps({'changed_meshes':records,
      'source_texture':str(texture.resolve()),'source_sha256':hashlib.sha256(texture.read_bytes()).hexdigest(),
      'native_atlas_size':[2048,2048],'material':oak.name,'base_factor':[.45,.38,.28,1],
      'door_material':door_oak.name,'door_factor':[.90,.80,.65,1],
      'door_source_texture':str(door_texture.resolve()),'door_source_sha256':hashlib.sha256(door_texture.read_bytes()).hexdigest(),
      'door_world_bounds':[door_lo.tolist(),(door_lo+door_span).tolist()],
      'door_mapping':'one finite world-space field across six separate physical boards',
      'geometry_changed':False,'raster_edits':False,'new_generated_art':False},indent=2),encoding='utf-8')


if __name__=='__main__':
    parser=argparse.ArgumentParser(description=__doc__)
    for name in ('source','out','kit'):parser.add_argument('--'+name,type=Path,required=True)
    parser.add_argument('--resolution',type=int,default=1100)
    parser.add_argument('--samples',type=int,default=12)
    args=parser.parse_args(sys.argv[sys.argv.index('--')+1:])
    W.run(args,refiners={'blacksmith':lambda:build(args.kit.resolve())},detail='v68',
          design='Native Assistance oak on108 Smithy framing members; six fitted boards share native inn door paint; all geometry/native architecture/thatch/fire/iron, twelve work props and anvil sign retained')
