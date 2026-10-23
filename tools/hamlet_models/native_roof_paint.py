"""Fit continuous native DD2 inn roof paint to the Abbey's curved roof decks.

Original 2048px pixels stay intact. Red-clay source paint receives an explicit
slate tint reviewed in DD2's renderer; Blender alone is not a colour proof.
Remove the two dense tile grids, restore their closed decks to original height,
and retain the physical ridge saddles. Other architecture and services remain.
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

# Original top-left pixels on the_inn_lvl_03_left2048 atlas. These are interiors,
# excluding the large black shadows drawn over the next roof course.
SAMPLES=((1404,414,1442,441),(1520,412,1556,430),(1614,421,1640,442),
 (1422,461,1455,490),(1535,469,1562,490),(1362,512,1396,534),
 (1474,512,1507,535),(1565,520,1590,553),(1390,633,1416,653),
 (1594,630,1621,655))
OLD_FACE='Abbey individual weathered slate v41'
OLD_EDGE='Abbey slate cut edges v41'


def ridge_paint(kit,tint):
    texture=kit/'exp/inn_lvl_03_prefab/tex--2625775420725934710.png'
    image=bpy.data.images.load(str(texture),check_existing=True)
    assert tuple(image.size)==(2048,2048)
    face=native_paint('Abbey native slate faces v61',image,(*tint,1))
    edge=native_paint('Abbey native slate cut edges v61',image,(*(c*.217 for c in tint),1))
    records=[];matched=[]
    for ob in list(bpy.data.objects):
        if ob.type!='MESH' or 'folded ridge caps' not in ob.name:continue
        assert {m.name for m in ob.data.materials}=={OLD_FACE,OLD_EDGE},ob.name
        matched.append(ob.name);old_slots=[m.name for m in ob.data.materials]
        points=np.array([tuple(v.co) for v in ob.data.vertices])
        assert all(len(p.vertices)==3 for p in ob.data.polygons)
        tri=np.array([list(p.vertices) for p in ob.data.polygons])
        groups=islands(points,tri)
        assert len(groups)>4 and max(map(len,groups))<100,(ob.name,len(groups),max(map(len,groups)))
        rng=random.Random(ob.name+' native slate61');bag=[];uv=ob.data.uv_layers.active
        for group in groups:
            top=[ob.data.polygons[i] for i in group if old_slots[ob.data.polygons[i].material_index]==OLD_FACE]
            assert top
            old=np.array([tuple(uv.data[i].uv) for f in top for i in f.loop_indices])
            lo,hi=old.min(0),old.max(0);span=hi-lo;assert min(span)>.02
            if not bag:bag=list(range(len(SAMPLES)));rng.shuffle(bag)
            index=bag.pop();x0,y0,x1,y1=SAMPLES[index]
            rect=np.array([x0/2048,1-y1/2048,x1/2048,1-y0/2048])
            for fi in group:
                poly=ob.data.polygons[fi]
                for li in poly.loop_indices:
                    a,b=np.clip((np.array(uv.data[li].uv)-lo)/span,0,1)
                    uv.data[li].uv=(rect[0]+a*(rect[2]-rect[0]),rect[1]+b*(rect[3]-rect[1]))
            records.append({'mesh':ob.name,'face_indices':group,'sample':index,'source_rect':SAMPLES[index]})
        for i,name in enumerate(old_slots):ob.data.materials[i]=face if name==OLD_FACE else edge
    assert len(matched)==2,matched
    return {'changed_meshes':matched,'tiles':records,
      'native_texture':str(texture.resolve()),'source_sha256':hashlib.sha256(texture.read_bytes()).hexdigest(),
      'samples':SAMPLES,'source_size':[2048,2048],'source_colour':'native red clay',
      'slate_material_factor':list(tint),'edge_factor_ratio':.217,'emission':.45,
      'geometry_changed':True,'native_raster_edits':False,'new_generated_art':False}


def build(kit,tint):
    # First retain the native interior treatment on the small physical ridge
    # saddles, then replace the repeated face grid with one continuous painting.
    record=ridge_paint(kit,tint)
    face=bpy.data.materials['Abbey native slate faces v61']
    edge=bpy.data.materials['Abbey native slate cut edges v61']
    removed=[];surfaces=[]
    for name,centre,length,width,rise,along_x,rect in (
       ('Abbey sweeping nave',(0,.5),8.7,6.7,2.8,True,(1170,350,1870,665)),
       ('Abbey porch',(-2.55,-3.25),2.9,3.1,2.3,False,(1400,350,1715,665))):
        old=bpy.data.objects[name+' fitted slate courses'];removed.append(old.name)
        bpy.data.objects.remove(old,do_unlink=True)
        ob=bpy.data.objects[name+' painted roof'];transform=ob.matrix_world;inverse=transform.inverted()
        for vertex in ob.data.vertices:
            point=transform@vertex.co;point.z+=.15;vertex.co=inverse@point
        ob.data.materials.clear();ob.data.materials.append(face);ob.data.materials.append(edge)
        # Distance down the curved slope gives consistent native course spacing.
        ds=np.array([0,.10,.23,.36,.50])*width
        hs=np.array([1,.71,.35,.13,0])*rise
        arc=np.r_[0,np.cumsum(np.hypot(np.diff(ds),np.diff(hs)))];uv=ob.data.uv_layers.active
        x0,y0,x1,y1=rect
        for poly in ob.data.polygons:
            normal=transform.to_3x3()@poly.normal;poly.material_index=0 if normal.z>.1 else 1
            for li in poly.loop_indices:
                p=transform@ob.data.vertices[ob.data.loops[li].vertex_index].co
                along=p.x-centre[0] if along_x else p.y-centre[1]
                down=abs(p.y-centre[1]) if along_x else abs(p.x-centre[0])
                u=np.clip(.5+along/length,0,1);v=1-np.interp(down,ds,arc)/arc[-1]
                uv.data[li].uv=((x0+u*(x1-x0))/2048,1-(y1-v*(y1-y0))/2048)
        surfaces.append({'name':ob.name,'source_rect':rect,'raised_to_original_deck':.15,
          'mapping':'finite native panel once per slope, arc-length uphill'})
    record.update(removed_meshes=removed,continuous_surfaces=surfaces,geometry_changed=True,
      changed_meshes=[p['name'] for p in surfaces]+[n for n in record['changed_meshes'] if 'ridge' in n])
    (G.OUT/'native-roof.json').write_text(json.dumps(record,indent=2),encoding='utf-8')


if __name__=='__main__':
    p=argparse.ArgumentParser(description=__doc__)
    for name in ('source','out','kit'):p.add_argument('--'+name,type=Path,required=True)
    p.add_argument('--tint',type=float,nargs=3,default=(.62,.88,.91))
    p.add_argument('--resolution',type=int,default=1100);p.add_argument('--samples',type=int,default=12)
    args=p.parse_args(sys.argv[sys.argv.index('--')+1:])
    W.run(args,refiners={'abbey':lambda:build(args.kit.resolve(),args.tint)},detail='v61',
          design='Continuous native DD2 roof panels with explicit slate tint; two tile grids removed, decks raised .15m, physical ridge caps and other architecture retained')
