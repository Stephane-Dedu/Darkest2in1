"""Fit native DD2 cut wood and oak paint to twelve retained Blacksmith props.

The anvil stump has a real cut-end field and finite, vertical bark panels. The wheel rim
unwraps once; handles, hub and spokes use aspect-fitted native plank samples.
Keep all geometry, ironwork, building paint and the earlier generated anvil sign.
"""
import argparse
import hashlib
import json
import math
import sys
from pathlib import Path

import bpy
import numpy as np

sys.path.insert(0,str(Path(__file__).resolve().parent))
import geometry as G
import workshops as W
from caravan_canvas import native_paint
from caravan_joinery import SAMPLES,sample_uv

CAP_CENTRE=(.1375885009765625,.8720703125)
CAP_RADIUS=.10105111449956894
BARK_RECT=(790,350,1120,800)


def finite_angles(angles):
    angles=[0 if a>1-1e-5 else a for a in angles]
    if max(angles)-min(angles)>.5:angles=[a+1 if a<.5 else a for a in angles]
    end=max(angles)
    if end>1:angles=[1-(end-a) for a in angles]
    assert min(angles)>=-1e-6 and max(angles)<=1+1e-6
    return angles


def build(kit):
    folder=kit/'exp/common_story_assist_prefab';doc=json.loads((folder/'scene.json').read_text())
    src=next(o for o in doc['objects'] if o['name']=='story_assist_wagon_main')
    texture=folder/doc['materials'][src['materials'][0]]['texture']
    stump_texture=kit/'exp/_tex/tex--3121839916537576340-2048x2048.png'
    image=bpy.data.images.load(str(texture),check_existing=True)
    stump_image=bpy.data.images.load(str(stump_texture),check_existing=True)
    assert tuple(image.size)==tuple(stump_image.size)==(2048,2048)
    objects=sorted((o for o in bpy.data.objects if o.type=='MESH' and
                    any(m.name=='Workshop worn oak v21' for m in o.data.materials)),key=lambda o:o.name)
    assert len(objects)==12 and all(len(o.data.materials)==1 for o in objects)
    oak=native_paint('Smithy native oak work props v70',image,(.45,.38,.28,1))
    stump=native_paint('Smithy native cut oak stump v70',stump_image,(.50,.43,.33,1))
    records=[]
    for index,o in enumerate(objects):
        p=np.array([tuple(v.co) for v in o.data.vertices]);lo=p.min(0);hi=p.max(0)
        span=np.maximum(hi-lo,.0001);centre=(lo+hi)*.5;uv=o.data.uv_layers.active
        is_stump=o.name=='Anvil oak stump';is_rim=o.name=='Iron shod wagon wheel'
        material=stump if is_stump else oak;o.data.materials.clear();o.data.materials.append(material)
        faces=[]
        for face in o.data.polygons:
            ids=face.loop_indices;points=[o.data.vertices[o.data.loops[i].vertex_index].co for i in ids]
            if is_stump:
                if abs(face.normal.z)>.9:
                    for li,v in zip(ids,points):
                        uv.data[li].uv=(CAP_CENTRE[0]+(v.x-centre[0])/(span[0]/2)*CAP_RADIUS,
                                         CAP_CENTRE[1]+(v.y-centre[1])/(span[1]/2)*CAP_RADIUS)
                    faces.append({'face':face.index,'mapping':'native intact cut disc',
                                  'source_uv_centre':CAP_CENTRE,'source_uv_radius':CAP_RADIUS})
                else:
                    tangent=np.array([-face.normal.y,face.normal.x,0]);tangent/=np.linalg.norm(tangent)
                    across=np.array([np.dot(v,tangent) for v in points]);u0=across.min();width=across.max()-u0
                    ratio=float(width/span[2]);a,b,c,d=BARK_RECT
                    used=min(c-a,(d-b)*ratio);angle=math.atan2(face.normal.y,face.normal.x)
                    offset=(math.sin(angle*3)+1)/2;x0=a+offset*((c-a)-used)
                    for li,v,u in zip(ids,points,across):
                        height=(v.z-lo[2])/span[2]
                        uv.data[li].uv=((x0+(u-u0)/width*used)/2048,1-(b+(1-height)*(d-b))/2048)
                    faces.append({'face':face.index,'mapping':'finite vertical bark panel',
                                  'source_sample':{'pixels':BARK_RECT},'ratio':ratio,'offset':offset})
            elif is_rim:
                assert span[2]<span[0]*.15
                major=float(np.linalg.norm(p[:,:2]-centre[:2],axis=1).mean());tube=float(span[2]/2)
                angles=finite_angles([(math.atan2(v.y-centre[1],v.x-centre[0])/math.tau)%1 for v in points])
                for li,v,u in zip(ids,points,angles):
                    radial=math.hypot(v.x-centre[0],v.y-centre[1])-major
                    w=(math.atan2(v.z-centre[2],radial)/math.tau)%1
                    uv.data[li].uv=sample_uv(SAMPLES[0],u,w,major/tube,.5)
                faces.append({'face':face.index,'mapping':'one finite wheel rim',
                              'source_sample':SAMPLES[0],'max_wrap_fraction':max(angles)-min(angles)})
            else:
                long=int(span.argmax());axis=int(np.abs(face.normal).argmax());axes=[k for k in range(3) if k!=axis]
                if long in axes:axes=[long,next(k for k in axes if k!=long)]
                else:axes.sort(key=lambda k:span[k],reverse=True)
                ratio=float(span[axes[0]]/span[axes[1]]);sample=SAMPLES[0 if ratio>8 else (4,5,6)[index%3]]
                offset=(index%7)/6
                for li,v in zip(ids,points):
                    a,b=[(v[k]-lo[k])/span[k] for k in axes]
                    if index%2:a=1-a
                    uv.data[li].uv=sample_uv(sample,a,b,ratio,offset)
                faces.append({'face':face.index,'mapping':'finite aspect-fitted grain',
                              'source_sample':sample,'ratio':ratio,'offset':offset})
            face.material_index=0
        records.append({'name':o.name,'material':material.name,'faces':faces})
    (G.OUT/'smithy-workprops.json').write_text(json.dumps({'changed_meshes':records,
      'source_texture':str(texture.resolve()),'source_sha256':hashlib.sha256(texture.read_bytes()).hexdigest(),
      'material':oak.name,'base_factor':[.45,.38,.28,1],
      'stump_source_texture':str(stump_texture.resolve()),'stump_source_sha256':hashlib.sha256(stump_texture.read_bytes()).hexdigest(),
      'stump_material':stump.name,'stump_factor':[.50,.43,.33,1],
      'cap_uv_centre':CAP_CENTRE,'cap_uv_radius':CAP_RADIUS,'bark_rect_pixels':BARK_RECT,
      'geometry_changed':False,'raster_edits':False,'new_generated_art':False},indent=2),encoding='utf-8')


if __name__=='__main__':
    parser=argparse.ArgumentParser(description=__doc__)
    for name in ('source','out','kit'):parser.add_argument('--'+name,type=Path,required=True)
    parser.add_argument('--resolution',type=int,default=1100);parser.add_argument('--samples',type=int,default=12)
    args=parser.parse_args(sys.argv[sys.argv.index('--')+1:])
    W.run(args,refiners={'blacksmith':lambda:build(args.kit.resolve())},detail='v70',
          design='Native Shroud cut-end/bark on retained anvil stump and Assistance oak on eleven workshop props; all geometry/Forge paint/iron/fire/sign retained')
