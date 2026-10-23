"""Fit denser native grey DD2 shingles to the retained Abbey roofs and caps.

The Shroud roof field is sampled once per curved slope with a shared slate
finish. Preserve all geometry, other UVs/materials and the other nine services.
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
from abbey_pinnacles import islands
from caravan_canvas import native_paint

FACTOR=(.65,.69,.76)
NAVE_RECT=(260,260,930,820)
PORCH_RECT=(450,350,740,820)
CAP_RECT=(465,700,520,750)


def native_uv(rect,u,v):
    a,b,c,d=rect
    return ((a+np.clip(u,0,1)*(c-a))/2048,1-(d-np.clip(v,0,1)*(d-b))/2048)


def build(kit):
    folder=kit/'exp/coast_building_large_01';doc=json.loads((folder/'scene.json').read_text())
    source=next(m for m in doc['materials'].values() if m['name']=='coast_buildings_03')
    texture=(folder/source['texture']).resolve();image=bpy.data.images.load(str(texture),check_existing=True)
    assert tuple(image.size)==(2048,2048)
    face=native_paint('Abbey native grey shingles v71',image,(*FACTOR,1))
    edge=native_paint('Abbey native grey shingle cut edges v71',image,(*(v*.217 for v in FACTOR),1))
    records=[]
    for name,centre,length,width,rise,along_x,rect in (
       ('Abbey sweeping nave',(0,.5),8.7,6.7,2.8,True,NAVE_RECT),
       ('Abbey porch',(-2.55,-3.25),2.9,3.1,2.3,False,PORCH_RECT)):
        ob=bpy.data.objects[name+' painted roof'];transform=ob.matrix_world
        old=[m.name for m in ob.data.materials]
        assert set(old)=={'Abbey native slate faces v61','Abbey native slate cut edges v61'}
        ob.data.materials.clear();ob.data.materials.append(face);ob.data.materials.append(edge)
        ds=np.array([0,.10,.23,.36,.50])*width;hs=np.array([1,.71,.35,.13,0])*rise
        arc=np.r_[0,np.cumsum(np.hypot(np.diff(ds),np.diff(hs)))];uv=ob.data.uv_layers.active
        for poly in ob.data.polygons:
            normal=transform.to_3x3()@poly.normal;poly.material_index=0 if normal.z>.1 else 1
            for li in poly.loop_indices:
                p=transform@ob.data.vertices[ob.data.loops[li].vertex_index].co
                along=p.x-centre[0] if along_x else p.y-centre[1]
                down=abs(p.y-centre[1]) if along_x else abs(p.x-centre[0])
                u=.5+along/length;v=1-np.interp(down,ds,arc)/arc[-1]
                uv.data[li].uv=native_uv(rect,u,v)
        records.append({'name':ob.name,'source_rect':rect,'mapping':'one finite native panel per arc-length slope'})
    caps=[]
    for ob in list(bpy.data.objects):
        if ob.type!='MESH' or 'folded ridge caps' not in ob.name:continue
        old=[m.name for m in ob.data.materials]
        assert set(old)=={'Abbey native slate faces v61','Abbey native slate cut edges v61'}
        points=np.array([tuple(v.co) for v in ob.data.vertices])
        groups=islands(points,np.array([list(p.vertices) for p in ob.data.polygons]));uv=ob.data.uv_layers.active
        for group in groups:
            loops=[i for fi in group for i in ob.data.polygons[fi].loop_indices]
            coords=np.array([tuple(uv.data[i].uv) for i in loops]);lo=coords.min(0);span=np.maximum(coords.max(0)-lo,.00001)
            for li,coord in zip(loops,coords):
                a,b=np.clip((coord-lo)/span,0,1);uv.data[li].uv=native_uv(CAP_RECT,a,b)
        for i,name in enumerate(old):ob.data.materials[i]=face if name=='Abbey native slate faces v61' else edge
        caps.append({'name':ob.name,'source_rect':CAP_RECT,'physical_caps':len(groups),'mapping':'finite painted shingle face'})
    assert len(caps)==2
    (G.OUT/'abbey-shingles.json').write_text(json.dumps({'source_texture':str(texture),
      'source_sha256':hashlib.sha256(texture.read_bytes()).hexdigest(),'source_material':'coast_buildings_03',
      'material':face.name,'edge_material':edge.name,'factor':FACTOR,'edge_factor_ratio':.217,
      'changed_meshes':records+caps,'geometry_changed':False,'raster_edits':False,'new_generated_art':False},indent=2),encoding='utf-8')


if __name__=='__main__':
    p=argparse.ArgumentParser(description=__doc__)
    for name in ('source','out','kit'):p.add_argument('--'+name,type=Path,required=True)
    p.add_argument('--resolution',type=int,default=1100);p.add_argument('--samples',type=int,default=12)
    args=p.parse_args(sys.argv[sys.argv.index('--')+1:])
    W.run(args,refiners={'abbey':lambda:build(args.kit.resolve())},detail='v71',
          design='Denser native Shroud grey shingles, finite once-per-slope arc-length mapping and muted shared slate; all geometry and other architecture retained')
