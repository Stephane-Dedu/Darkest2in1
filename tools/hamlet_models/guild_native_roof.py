"""Fit native DD2 roof paint to the Guild hall, dormers and armory.

Keep the original2048 atlas intact. Finite sections fit each curved slope;
physical ridge saddles, dormer flashing, ironwork and other architecture remain.
The hall keeps warm clay, while the armory shares the Abbey's muted slate tint.
"""
import argparse
import hashlib
import json
import random
import sys
from pathlib import Path

import bpy
import numpy as np

sys.path.insert(0,str(Path(__file__).resolve().parent))
import geometry as G
import workshops as W
from abbey_pinnacles import islands
from caravan_canvas import native_paint
from native_roof_paint import SAMPLES

FINISHES={'clay':(.70,.65,.59),'slate':(.62,.88,.91)}
# Assembly, retained deck, centre, length, width, rise, ridge along X, source pixels.
ROOFS=(
 ('Guild hall','Guild red hall painted roof',(-.65,.3),6.3,6.5,3.4,True,(1170,350,1870,665),'clay'),
 ('Guild dormer 1','Guild roof dormer painted roof',(-2.12,-1.85),1.6,1.38,.9,False,(1400,420,1610,600),'clay'),
 ('Guild dormer 2','Guild roof dormer painted roof.001',(.85,-1.85),1.6,1.38,.9,False,(1580,420,1790,600),'clay'),
 ('Guild armory','Guild armory painted roof',(3,.7),5.2,2.7,1.8,False,(1170,350,1870,665),'slate'))


def fit_ridge(ob,face,edge,old_face):
    old_slots=[m.name for m in ob.data.materials]
    assert set(old_slots)=={old_face,'Tile cut-edge charcoal v28'},(ob.name,old_slots)
    points=np.array([tuple(v.co) for v in ob.data.vertices])
    assert all(len(p.vertices)==3 for p in ob.data.polygons)
    groups=islands(points,np.array([list(p.vertices) for p in ob.data.polygons]))
    assert len(groups)>=5 and max(map(len,groups))<100
    rng=random.Random(ob.name+' native roof62');bag=[];uv=ob.data.uv_layers.active;records=[]
    for group in groups:
        top=[ob.data.polygons[i] for i in group if old_slots[ob.data.polygons[i].material_index]==old_face]
        assert top
        old=np.array([tuple(uv.data[i].uv) for p in top for i in p.loop_indices])
        lo,hi=old.min(0),old.max(0);span=hi-lo;assert min(span)>.02
        if not bag:bag=list(range(len(SAMPLES)));rng.shuffle(bag)
        index=bag.pop();x0,y0,x1,y1=SAMPLES[index]
        for fi in group:
            for li in ob.data.polygons[fi].loop_indices:
                a,b=np.clip((np.array(uv.data[li].uv)-lo)/span,0,1)
                uv.data[li].uv=((x0+a*(x1-x0))/2048,1-(y1-b*(y1-y0))/2048)
        records.append({'mesh':ob.name,'faces':group,'sample':index,'source_rect':SAMPLES[index]})
    for i,name in enumerate(old_slots):ob.data.materials[i]=face if name==old_face else edge
    return records


def build(kit):
    texture=kit/'exp/inn_lvl_03_prefab/tex--2625775420725934710.png'
    image=bpy.data.images.load(str(texture),check_existing=True);assert tuple(image.size)==(2048,2048)
    paints={kind:(native_paint('Guild native '+kind+' faces v62',image,(*factor,1)),
                  native_paint('Guild native '+kind+' cut edges v62',image,(*(c*.217 for c in factor),1)))
            for kind,factor in FINISHES.items()}
    removed=[];surfaces=[];ridge=[]
    for name,deck,centre,length,width,rise,along_x,rect,kind in ROOFS:
        face,edge=paints[kind]
        ridge.extend(fit_ridge(bpy.data.objects[name+' fitted ridge saddles'],face,edge,
            'Guild aged clay courses v41' if kind=='clay' else 'Service weathered slate v41'))
        old=bpy.data.objects[name+' fitted overlapping tiles'];removed.append(old.name)
        bpy.data.objects.remove(old,do_unlink=True)
        ob=bpy.data.objects[deck];transform=ob.matrix_world;inverse=transform.inverted()
        for vertex in ob.data.vertices:
            point=transform@vertex.co;point.z+=.12;vertex.co=inverse@point
        ob.data.materials.clear();ob.data.materials.append(face);ob.data.materials.append(edge)
        ds=np.array([0,.10,.23,.36,.50])*width
        hs=np.array([1,.71,.35,.13,0])*rise
        arc=np.r_[0,np.cumsum(np.hypot(np.diff(ds),np.diff(hs)))];uv=ob.data.uv_layers.active
        panels=[{'source_rect':rect,'uphill_range':[0,1]}]
        if name=='Guild hall':
            # Two different intact native roof sections supply enough courses
            # for this tall pitch. Join on an existing curved-deck row; no UV
            # repeat, extra geometry or painted dormers on the roof plane.
            split=1-arc[2]/arc[-1]
            panels=[{'source_rect':(1170,350,1870,665),'uphill_range':[0,float(split)]},
                    {'source_rect':(280,735,835,925),'uphill_range':[float(split),1]}]
        for poly in ob.data.polygons:
            normal=transform.to_3x3()@poly.normal;poly.material_index=0 if normal.z>.1 else 1
            coords=[]
            for li in poly.loop_indices:
                point=transform@ob.data.vertices[ob.data.loops[li].vertex_index].co
                along=point.x-centre[0] if along_x else point.y-centre[1]
                down=abs(point.y-centre[1]) if along_x else abs(point.x-centre[0])
                u=np.clip(.5+along/length,0,1);v=1-np.interp(down,ds,arc)/arc[-1]
                coords.append((li,u,v))
            middle=sum(p[2] for p in coords)/len(coords)
            panel=next(p for p in panels if p['uphill_range'][0]-1e-6<=middle<=p['uphill_range'][1]+1e-6)
            x0,y0,x1,y1=panel['source_rect'];low,high=panel['uphill_range']
            if normal.z>.1:assert all(low-2e-6<=v<=high+2e-6 for li,u,v in coords),(name,coords,panel)
            for li,u,v in coords:
                fraction=np.clip((v-low)/(high-low),0,1)
                uv.data[li].uv=((x0+u*(x1-x0))/2048,1-(y1-fraction*(y1-y0))/2048)
        surfaces.append({'name':deck,'raised_to_original_deck':.12,
          'panels':panels,'finish':kind,'mapping':'finite native sections without repeat, arc-length uphill'})
    assert len(ridge)==47 and len(surfaces)==4 and len(removed)==4
    record={'native_texture':str(texture.resolve()),'source_sha256':hashlib.sha256(texture.read_bytes()).hexdigest(),
      'source_size':[2048,2048],'source_colour':'native red clay','material_factors':FINISHES,
      'edge_factor_ratio':.217,'emission':.45,'samples':SAMPLES,'tiles':ridge,
      'continuous_surfaces':surfaces,'removed_meshes':removed,
      'changed_meshes':[s['name'] for s in surfaces]+[s[0]+' fitted ridge saddles' for s in ROOFS],
      'geometry_changed':True,'native_raster_edits':False,'new_generated_art':False}
    (G.OUT/'native-roof.json').write_text(json.dumps(record,indent=2),encoding='utf-8')


if __name__=='__main__':
    p=argparse.ArgumentParser(description=__doc__)
    for name in ('source','out','kit'):p.add_argument('--'+name,type=Path,required=True)
    p.add_argument('--resolution',type=int,default=1100);p.add_argument('--samples',type=int,default=12)
    args=p.parse_args(sys.argv[sys.argv.index('--')+1:])
    W.run(args,refiners={'guild':lambda:build(args.kit.resolve())},detail='v62',
      design='Finite native DD2 clay/slate roof panels; four dense tile grids removed, original decks raised .12m;47 ridge saddles, dormers/flashing/ironwork and other services retained')
