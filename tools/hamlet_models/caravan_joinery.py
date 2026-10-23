"""Fit native DD2 oak paint to the Nomad's existing individual wooden members.

Private Blender recipe. The original 2048px Assistance atlas is reused without
raster edits. Finite, aspect-fitted native plank/beam samples follow each member's
local grain; wheel rims unwrap once around their circumference. Existing canopy,
native parts, all geometry and the nine other services stay intact.
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

# Original atlas pixel rectangles, top-left origin. Each is one continuous native
# member, rather than a whole panel of repeated boards. U follows the long grain.
SAMPLES=(
    {'pixels':(144,892,488,914),'vertical':False,'kind':'long sill'},
    {'pixels':(21,1030,49,1238),'vertical':True,'kind':'upright'},
    {'pixels':(47,500,75,648),'vertical':True,'kind':'short upright'},
    {'pixels':(179,947,304,986),'vertical':False,'kind':'broad plank'},
    {'pixels':(1148,1597,1296,1623),'vertical':False,'kind':'left cart plank'},
    {'pixels':(1348,1630,1506,1654),'vertical':False,'kind':'right cart plank'},
    {'pixels':(1180,1827,1355,1850),'vertical':False,'kind':'lower cart plank'},
)


def sample_uv(sample,u,v,ratio,offset):
    x0,y0,x1,y1=sample['pixels'];vertical=sample['vertical']
    length=(y1-y0) if vertical else (x1-x0)
    width=(x1-x0) if vertical else (y1-y0)
    # Crop the long dimension to the member's actual face aspect. Very long
    # members instead crop the width. Never stretch a tiny square over a beam.
    used_length=min(length,width*ratio);used_width=used_length/ratio
    along=offset*(length-used_length)+u*used_length
    across=(width-used_width)*.5+v*used_width
    x,y=(x0+across,y0+along) if vertical else (x0+along,y0+across)
    return (x/2048,1-y/2048)


def build(folder):
    doc=json.loads((folder/'scene.json').read_text(encoding='utf-8'))
    src=next(o for o in doc['objects'] if o['name']=='story_assist_wagon_main')
    texture=folder/doc['materials'][src['materials'][0]]['texture']
    oak=bpy.data.materials['Nomad native oak canopy hoops v57']
    images={n.image for n in oak.node_tree.nodes if n.type=='TEX_IMAGE'}
    assert len(images)==1 and tuple(next(iter(images)).size)==(2048,2048)
    native_image=bpy.data.images.load(str(texture),check_existing=True)
    assert np.array_equal(np.asarray(next(iter(images)).pixels),np.asarray(native_image.pixels))
    objects=sorted((o for o in bpy.data.objects if o.type=='MESH' and
                    any(m.name=='Workshop worn oak v21' for m in o.data.materials)),key=lambda o:o.name)
    assert len(objects)==109 and all(len(o.data.materials)==1 for o in objects)
    records=[]
    for index,o in enumerate(objects):
        p=np.array([tuple(v.co) for v in o.data.vertices]);lo,hi=p.min(0),p.max(0)
        span=np.maximum(hi-lo,.0001);long=int(span.argmax())
        uv=o.data.uv_layers.active;faces=[]
        o.data.materials.clear();o.data.materials.append(oak)
        if o.name.startswith('Iron shod wagon wheel'):
            # Torus local XY is the wheel plane. Preserve all existing vertices,
            # unwrap the rim once, with one finite seam in the local wheel plane.
            assert span[2]<span[0]*.15
            centre=(lo+hi)*.5;major=float(np.linalg.norm(p[:,:2]-centre[:2],axis=1).mean())
            tube=float(span[2]/2);sample=SAMPLES[0]
            for face in o.data.polygons:
                coords=[o.data.vertices[o.data.loops[i].vertex_index].co for i in face.loop_indices]
                angles=[(math.atan2(v.y-centre[1],v.x-centre[0])/math.tau)%1 for v in coords]
                if max(angles)-min(angles)>.5:angles=[u+1 if u<.5 else u for u in angles]
                # An edge-crossing face uses the adjacent end of the same finite
                # source beam; it must not cross into another atlas island.
                start=min(angles);end=max(angles)
                if end>1:angles=[1-(end-u) for u in angles]
                for li,v,u in zip(face.loop_indices,coords,angles):
                    radial=math.hypot(v.x-centre[0],v.y-centre[1])-major
                    w=(math.atan2(v.z-centre[2],radial)/math.tau)%1
                    uv.data[li].uv=sample_uv(sample,u,w,major/tube,.5)
                face.material_index=0
            records.append({'name':o.name,'mapping':'finite cylindrical rim','source_sample':sample})
            continue
        for face in o.data.polygons:
            axis=int(np.abs(face.normal).argmax());axes=[k for k in range(3) if k!=axis]
            if long in axes:axes=[long,next(k for k in axes if k!=long)]
            else:axes.sort(key=lambda k:span[k],reverse=True)
            ratio=float(span[axes[0]]/span[axes[1]])
            # Long sills for side boards; broader native planks for stair treads.
            # Short uprights rotate onto front boards and the pointed doorway.
            choices=(0,4,5,6) if ratio>12 else ((3,) if ratio<4.2 else (1,2,0))
            sample=SAMPLES[choices[index%len(choices)]]
            offset=(index%7)/6
            for li in face.loop_indices:
                v=o.data.vertices[o.data.loops[li].vertex_index].co
                a,b=[(v[k]-lo[k])/span[k] for k in axes]
                if index%2:a=1-a
                uv.data[li].uv=sample_uv(sample,a,b,ratio,offset)
            face.material_index=0
            faces.append({'face':face.index,'axes':axes,'ratio':ratio,'source_sample':sample,'offset':offset})
        records.append({'name':o.name,'mapping':'finite aspect-fitted plank faces','faces':faces})
    (G.OUT/'native-joinery.json').write_text(json.dumps({'changed_meshes':records,
      'source_texture':str(texture.resolve()),'source_sha256':hashlib.sha256(texture.read_bytes()).hexdigest(),
      'native_atlas_size':[2048,2048],'material_reused':oak.name,
      'geometry_changed':False,'raster_edits':False,'new_generated_art':False},indent=2),encoding='utf-8')


if __name__=='__main__':
    parser=argparse.ArgumentParser(description=__doc__)
    for name in ('source','out','kit'):parser.add_argument('--'+name,type=Path,required=True)
    parser.add_argument('--resolution',type=int,default=1000)
    parser.add_argument('--samples',type=int,default=12)
    args=parser.parse_args(sys.argv[sys.argv.index('--')+1:])
    W.run(args,refiners={'nomad_wagon':lambda:build(args.kit.resolve()/'exp/common_story_assist_prefab')},
          detail='v58',design='Native DD2 finite plank and beam paint on all109 retained oak members; original geometry/canopy/palette')
