"""Fit native DD2 weathered shingles to the Sanitarium's turret and porch.

Keep the swept cap, hips/finial, ridge saddles and other architecture. Original
coast_buildings_03 paint is sampled finitely, with an explicit muted slate factor.
No barnacle overlay, bitmap edits or generated art.
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

FACTOR=(.65,.69,.76)
CONE_RECT=(260,300,930,650)
PORCH_RECT=(450,580,690,820)


def native_uv(rect,u,v):
    x0,y0,x1,y1=rect
    return ((x0+np.clip(u,0,1)*(x1-x0))/2048,1-(y1-np.clip(v,0,1)*(y1-y0))/2048)


def lift(ob,distance):
    transform=ob.matrix_world;inverse=transform.inverted()
    for vertex in ob.data.vertices:
        p=transform@vertex.co;p.z+=distance;vertex.co=inverse@p


def build(kit):
    folder=kit/'exp/coast_building_large_01';doc=json.loads((folder/'scene.json').read_text())
    source=next(m for m in doc['materials'].values() if m['name']=='coast_buildings_03')
    texture=folder/source['texture'];image=bpy.data.images.load(str(texture.resolve()),check_existing=True)
    assert tuple(image.size)==(2048,2048)
    face=native_paint('Sanitarium native slate shingles v64',image,(*FACTOR,1))
    edge=native_paint('Sanitarium native slate cut edges v64',image,(*(v*.217 for v in FACTOR),1))
    removed=[]
    for name in ('Sanitarium turret fitted slate courses','Sanitarium porch fitted overlapping tiles'):
        ob=bpy.data.objects[name];removed.append(name);bpy.data.objects.remove(ob,do_unlink=True)
    cone=bpy.data.objects['Sanitarium swept witch cap'];lift(cone,.035)
    cone.data.materials.clear();cone.data.materials.append(face)
    radius=1.86*.86;base=1.1+(7.3-1.1)*.91;rise=2.7*.91
    rs=np.array([1,.91,.69,.40,.13,0])*radius;hs=np.array([0,.13,.34,.66,.90,1])*rise
    arc=np.r_[0,np.cumsum(np.hypot(np.diff(rs),np.diff(hs)))];uv=cone.data.uv_layers.active
    for poly in cone.data.polygons:
        coords=[]
        for li in poly.loop_indices:
            p=cone.matrix_world@cone.data.vertices[cone.data.loops[li].vertex_index].co
            x,y=p.x+3,p.y+2
            u=None if math.hypot(x,y)<1e-5 else ((math.atan2(y,x)-math.radians(105))/math.tau)%1
            if u is not None and u>1-1e-5:u=0
            coords.append((li,u,float(np.interp(p.z-base,hs,arc)/arc[-1])))
        known=[u for li,u,v in coords if u is not None];assert known
        wrap=max(known)-min(known)>.5
        if wrap:known=[u+1 if u<.5 else u for u in known]
        average=sum(known)/len(known)
        mapped=[]
        for li,u,v in coords:
            if u is None:u=average
            elif wrap and u<.5:u+=1
            assert -1e-5<=u<=1+1e-5,(poly.index,u)
            mapped.append(u)
            uv.data[li].uv=native_uv(CONE_RECT,u,v)
        assert max(mapped)-min(mapped)<=1/12+1e-5,poly.index
        poly.material_index=0
    # Project the same finite painting across the porch and its physical caps.
    # Both slopes read uphill. Preserve cap geometry and original dark cut sides.
    porch=bpy.data.objects['Sanitarium porch painted roof'];lift(porch,.12)
    caps=bpy.data.objects['Sanitarium porch fitted ridge saddles']
    ds=np.array([0,.10,.23,.36,.50])*2.8;hs=np.array([1,.71,.35,.13,0])*1.65
    arc=np.r_[0,np.cumsum(np.hypot(np.diff(ds),np.diff(hs)))]
    for ob in (porch,caps):
        slots=[m.name for m in ob.data.materials];uv=ob.data.uv_layers.active
        old_indices=[p.material_index for p in ob.data.polygons]
        ob.data.materials.clear();ob.data.materials.append(face);ob.data.materials.append(edge)
        for poly in ob.data.polygons:
            normal=ob.matrix_world.to_3x3()@poly.normal
            top=slots[old_indices[poly.index]]=='Service weathered slate v41' if ob==caps else normal.z>.1
            poly.material_index=0 if top else 1
            for li in poly.loop_indices:
                p=ob.matrix_world@ob.data.vertices[ob.data.loops[li].vertex_index].co
                u=.5+(p.y+2.7)/2.1;v=1-np.interp(abs(p.x-.45),ds,arc)/arc[-1]
                uv.data[li].uv=native_uv(PORCH_RECT,u,v)
    record={'source_texture':str(texture.resolve()),'source_sha256':hashlib.sha256(texture.read_bytes()).hexdigest(),
      'source_material':'coast_buildings_03','source_size':[2048,2048],'source_colour':'native weathered coastal shingles',
      'slate_factor':FACTOR,'edge_factor_ratio':.217,'emission':.45,'removed_meshes':removed,
      'changed_meshes':[cone.name,porch.name,caps.name],
      'surfaces':[{'name':cone.name,'source_rect':CONE_RECT,'lift':.035,'mapping':'one finite arc-length wrap, seam under rear hip'},
                  {'name':porch.name,'source_rect':PORCH_RECT,'lift':.12,'mapping':'finite panel once per slope'},
                  {'name':caps.name,'source_rect':PORCH_RECT,'lift':0,'mapping':'same world projection as porch'}],
      'ridge_saddles_retained':7,'native_raster_edits':False,'new_generated_art':False}
    (G.OUT/'native-shingles.json').write_text(json.dumps(record,indent=2),encoding='utf-8')


if __name__=='__main__':
    p=argparse.ArgumentParser(description=__doc__)
    for name in ('source','out','kit'):p.add_argument('--'+name,type=Path,required=True)
    p.add_argument('--resolution',type=int,default=1100);p.add_argument('--samples',type=int,default=12)
    args=p.parse_args(sys.argv[sys.argv.index('--')+1:])
    W.run(args,refiners={'sanitarium':lambda:build(args.kit.resolve())},detail='v64',
      design='Native DD2 weathered shingles on retained swept turret/porch, two dense grids removed; original decks restored .035/.12m, hips/finial/ridge caps/native house/plinth and other architecture retained')
