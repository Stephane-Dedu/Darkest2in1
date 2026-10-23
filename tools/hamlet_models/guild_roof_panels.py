"""Fit reviewed painted clay and native grey shingles to the Guild roofs.

Keep warm clay and all existing geometry. The small roofs sample a central
fraction of the main panel so tiles keep a comparable physical size.
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

FACTOR=(.70,.65,.59)
# Finite normalized face crop, inside the reviewed new painting.
CAP_RECT=(.415,.465,.445,.495)
SLATE_RECT=(260,300,930,650)
SLATE_CAP=(465,700,520,750)


def panel_uv(u,v):
    return (u,v)


def build(kit,texture):
    image=bpy.data.images.load(str(texture.resolve()),check_existing=True);assert min(image.size)>=1000
    face=native_paint('Guild painted clay faces v72',image,(*FACTOR,1))
    edge=native_paint('Guild painted clay cut edges v72',image,(*(v*.217 for v in FACTOR),1))
    records=[]
    for name,centre,length,width,rise,along_x,u_range,v_range in (
       ('Guild red hall painted roof',(-.65,.3),6.3,6.5,3.4,True,(0,1),(0,1)),
       ('Guild roof dormer painted roof',(-2.12,-1.85),1.6,1.38,.9,False,(.36,.64),(.32,.58)),
       ('Guild roof dormer painted roof.001',(.85,-1.85),1.6,1.38,.9,False,(.62,.90),(.38,.64))):
        ob=bpy.data.objects[name];transform=ob.matrix_world
        assert {m.name for m in ob.data.materials}=={'Guild native clay faces v62','Guild native clay cut edges v62'}
        ob.data.materials.clear();ob.data.materials.append(face);ob.data.materials.append(edge)
        ds=np.array([0,.10,.23,.36,.50])*width;hs=np.array([1,.71,.35,.13,0])*rise
        arc=np.r_[0,np.cumsum(np.hypot(np.diff(ds),np.diff(hs)))];uv=ob.data.uv_layers.active
        for poly in ob.data.polygons:
            normal=transform.to_3x3()@poly.normal;poly.material_index=0 if normal.z>.1 else 1
            for li in poly.loop_indices:
                p=transform@ob.data.vertices[ob.data.loops[li].vertex_index].co
                along=p.x-centre[0] if along_x else p.y-centre[1]
                down=abs(p.y-centre[1]) if along_x else abs(p.x-centre[0])
                u=np.clip(.5+along/length,0,1);v=np.clip(1-np.interp(down,ds,arc)/arc[-1],0,1)
                uv.data[li].uv=panel_uv(u_range[0]+u*(u_range[1]-u_range[0]),v_range[0]+v*(v_range[1]-v_range[0]))
        records.append({'name':name,'mapping':'one finite reviewed painting per arc-length slope','u_range':u_range,'v_range':v_range})
    for ob in list(bpy.data.objects):
        if ob.type!='MESH' or 'fitted ridge saddles' not in ob.name:continue
        old=[m.name for m in ob.data.materials]
        if 'Guild native clay faces v62' not in old:continue
        assert set(old)=={'Guild native clay faces v62','Guild native clay cut edges v62'}
        points=np.array([tuple(v.co) for v in ob.data.vertices]);groups=islands(points,np.array([list(p.vertices) for p in ob.data.polygons]))
        uv=ob.data.uv_layers.active
        for group in groups:
            loops=[i for fi in group for i in ob.data.polygons[fi].loop_indices]
            coords=np.array([tuple(uv.data[i].uv) for i in loops]);lo=coords.min(0);span=np.maximum(coords.max(0)-lo,.00001)
            x0,y0,x1,y1=CAP_RECT
            for li,coord in zip(loops,coords):
                u,v=np.clip((coord-lo)/span,0,1);uv.data[li].uv=(x0+u*(x1-x0),y0+v*(y1-y0))
        for i,name in enumerate(old):ob.data.materials[i]=face if name=='Guild native clay faces v62' else edge
        records.append({'name':ob.name,'mapping':'finite painted shingle face','source_rect_uv':CAP_RECT,'physical_caps':len(groups)})
    assert len(records)==6
    slate_texture=kit/'exp/_tex/tex-7766974860864528419-2048x2048.png'
    slate_image=bpy.data.images.load(str(slate_texture.resolve()),check_existing=True);assert tuple(slate_image.size)==(2048,2048)
    slate_factor=(.65,.69,.76)
    slate=native_paint('Guild armory native grey shingles v72',slate_image,(*slate_factor,1))
    slate_edge=native_paint('Guild armory native grey cut edges v72',slate_image,(*(v*.217 for v in slate_factor),1))
    centre=(3,.7);length=5.2;width=2.7;rise=1.8
    ds=np.array([0,.10,.23,.36,.50])*width;hs=np.array([1,.71,.35,.13,0])*rise
    arc=np.r_[0,np.cumsum(np.hypot(np.diff(ds),np.diff(hs)))]
    for name in ('Guild armory painted roof','Guild armory fitted ridge saddles'):
        ob=bpy.data.objects[name];old=[m.name for m in ob.data.materials];uv=ob.data.uv_layers.active
        assert set(old)=={'Guild native slate faces v62','Guild native slate cut edges v62'}
        if 'painted roof' in name:
            a,b,c,d=SLATE_RECT
            for poly in ob.data.polygons:
                for li in poly.loop_indices:
                    p=ob.matrix_world@ob.data.vertices[ob.data.loops[li].vertex_index].co
                    u=np.clip(.5+(p.y-centre[1])/length,0,1)
                    v=np.clip(1-np.interp(abs(p.x-centre[0]),ds,arc)/arc[-1],0,1)
                    uv.data[li].uv=((a+u*(c-a))/2048,1-(d-v*(d-b))/2048)
            records.append({'name':name,'mapping':'finite native grey slope','source_rect':SLATE_RECT,'source':'slate'})
        else:
            points=np.array([tuple(v.co) for v in ob.data.vertices]);groups=islands(points,np.array([list(p.vertices) for p in ob.data.polygons]))
            a,b,c,d=SLATE_CAP
            for group in groups:
                loops=[i for fi in group for i in ob.data.polygons[fi].loop_indices]
                coords=np.array([tuple(uv.data[i].uv) for i in loops]);lo=coords.min(0);span=np.maximum(coords.max(0)-lo,.00001)
                for li,coord in zip(loops,coords):
                    u,v=np.clip((coord-lo)/span,0,1);uv.data[li].uv=((a+u*(c-a))/2048,1-(d-v*(d-b))/2048)
            records.append({'name':name,'mapping':'finite native grey cap faces','source_rect':SLATE_CAP,'source':'slate','physical_caps':len(groups)})
        for i,name in enumerate(old):ob.data.materials[i]=slate if name=='Guild native slate faces v62' else slate_edge
    assert len(records)==8
    (G.OUT/'guild-roof-panels.json').write_text(json.dumps({'source_texture':str(texture.resolve()),
      'source_sha256':hashlib.sha256(texture.read_bytes()).hexdigest(),'source_kind':'new DD2-reference painting, built-in ImageGen',
      'material':face.name,'edge_material':edge.name,'factor':FACTOR,'edge_factor_ratio':.217,
      'slate_source_texture':str(slate_texture.resolve()),'slate_source_sha256':hashlib.sha256(slate_texture.read_bytes()).hexdigest(),
      'slate_material':slate.name,'slate_edge_material':slate_edge.name,'slate_factor':slate_factor,
      'changed_meshes':records,'geometry_changed':False,'raster_edits':False,'new_generated_art':True},indent=2),encoding='utf-8')


if __name__=='__main__':
    p=argparse.ArgumentParser(description=__doc__)
    for name in ('source','out','kit','paint'):p.add_argument('--'+name,type=Path,required=True)
    p.add_argument('--resolution',type=int,default=1100);p.add_argument('--samples',type=int,default=12)
    args=p.parse_args(sys.argv[sys.argv.index('--')+1:])
    W.run(args,refiners={'guild':lambda:build(args.kit.resolve(),args.paint.resolve())},detail='v72',
          design='Reviewed painted clay at finer course density; native grey armory shingles, finite arc-length panels/dormer fractions; all geometry and other architecture retained')
