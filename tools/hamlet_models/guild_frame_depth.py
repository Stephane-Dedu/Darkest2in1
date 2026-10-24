"""Project the Guild hall over its masonry and clear the upper side windows.

Blender-only recipe. Existing native oak, stone, plaster and colour factors
remain unchanged. Source distances precede the runtime service-width fit.
"""
import argparse,json,sys
from pathlib import Path
import bpy,numpy as np
from mathutils import Matrix,Vector
sys.path.insert(0,str(Path(__file__).resolve().parent))
import geometry as G
import workshops as W
from caravan_joinery import SAMPLES,sample_uv


def bounds(ob):
    v=np.array([tuple(ob.matrix_world@x.co) for x in ob.data.vertices])
    return v.min(0),v.max(0)


def build():
    oak=bpy.data.materials['Guild native oak joinery v63']
    centre=Vector((-.65,.3,0));spread=Matrix.Diagonal((1.075,1.075,1,1))
    transform=Matrix.Translation(centre)@spread@Matrix.Translation(-centre)
    changed=[];lowered=[];supports=[]
    for ob in list(bpy.data.objects):
        if ob.type!='MESH':continue
        lo,hi=bounds(ob);name=ob.name
        frame=name.startswith(('Guild hall upright','Guild hall crossbeam','Guild lower wall brace',
                              'Side wall post','Side wall timber course','Side diagonal brace')) and lo[2]>3.3 and hi[2]<6.9
        if name.startswith('Side wall timber course') and hi[1]-lo[1]<5.5:frame=False
        upper=name.startswith(('Guild plaster hall','Guild upper slit','Guild side casement'))
        crest=name.startswith(('Guild tapered steel blade','Guild curved sword guard','Guild leather wrapped grip',
           'Guild faceted sword pommel','Guild steel rimmed shield','Guild inset shield paint','Guild shield centre rib','Guild shield rivet'))
        standard=name.startswith(('Guild painted sword standard','Banner rod'))
        if name.startswith('Guild native hanging standard '):
            # Rigidly move the native cloth with its rod and wall. Retain its
            # proportions, folds and UVs instead of stretching the original.
            midpoint=Vector((lo+hi)/2);ob.location+=(transform@midpoint-midpoint)
            changed.append(name)
        if frame or upper or crest or standard:
            # Move the complete wall, its cut openings, surrounds and backing
            # together. Roof/eave geometry and the stone base stay in place.
            ob.matrix_world=transform@ob.matrix_world;changed.append(name)
        if frame and name.startswith('Side wall timber course') and 4.8<lo[2]<5.0:
            ob.location.z-=.55;lowered.append(name)
        elif frame and name.startswith('Side diagonal brace'):
            # Match the lowered side rail. This also clears both glass panes.
            inv=ob.matrix_world.inverted()
            for v in ob.data.vertices:
                p=ob.matrix_world@v.co;p.z=3.6+(p.z-3.6)*(.845/1.395);v.co=inv@p
            ob.data.update();lowered.append(name)
        if frame and name.startswith(('Guild hall crossbeam','Side wall timber course')) and lo[2]<3.7:
            # Deep sole plates beneath the projecting floor, same finite UVs.
            midpoint=(lo+hi)/2;inv=ob.matrix_world.inverted()
            for v in ob.data.vertices:
                p=ob.matrix_world@v.co;p.z=3.57+(p.z-3.6)*1.35;v.co=inv@p
            ob.data.update()
        if name.startswith('Guild overhang corbel'):
            inv=ob.matrix_world.inverted()
            for v in ob.data.vertices:
                p=ob.matrix_world@v.co;t=max(0.,min(1.,(p.z-3.1)/.58));q=transform@p
                p.x+=(q.x-p.x)*t;p.y+=(q.y-p.y)*t;v.co=inv@p
            ob.data.update();changed.append(name)
    assert len(lowered)==6 and len(changed)>100

    def beam(name,a,b,width,index):
        ob=G.beam('Guild jettied hall '+name,a,b,width,oak)
        p=np.array([tuple(v.co) for v in ob.data.vertices]);lo=p.min(0);span=np.maximum(np.ptp(p,axis=0),.0001)
        long=int(span.argmax());uv=ob.data.uv_layers.active
        for face in ob.data.polygons:
            axis=int(np.abs(face.normal).argmax());axes=[k for k in range(3) if k!=axis]
            if long in axes:axes=[long,next(k for k in axes if k!=long)]
            else:axes.sort(key=lambda k:span[k],reverse=True)
            ratio=float(span[axes[0]]/span[axes[1]]);sample=SAMPLES[(4,5,6)[index%3] if ratio<8 else 0]
            for li in face.loop_indices:
                v=ob.data.vertices[ob.data.loops[li].vertex_index].co
                a,b=[(v[k]-lo[k])/span[k] for k in axes]
                uv.data[li].uv=sample_uv(sample,a,b,ratio,(index%7)/6)
        supports.append(ob.name);return ob

    # Twelve short structural knees occupy the corners of the upper window
    # bays. Their ends stay outside the dressed stone surrounds and glazing.
    posts=[-.65+(x+.65)*1.075 for x in (-3.51,-1.63,.33,2.21)]
    for side,z in enumerate((.3+(-2.585-.3)*1.075,.3+(3.185-.3)*1.075)):
        for bay,(left,right) in enumerate(zip(posts,posts[1:])):
            for hand,x in enumerate((left,right)):
                sign=1 if hand==0 else -1
                beam(f'window knee {side+1}-{bay+1}-{hand+1}',(x+sign*.11,6.60,z),(x+sign*.46,6.24,z),.12,len(supports))
    # The original five front corbels now meet the new sole plate. Additional
    # rear and flank supports make the overhang credible from other views.
    for x in (-3.18,-2.,-.65,.7,1.88):
        endpoint=transform@Vector((x,3.24,3.68))
        beam('rear corbel',(x,3.1,3.05),(endpoint.x,endpoint.z,endpoint.y),.16,len(supports))
    for side,x in ((-1,-3.4),(1,2.1)):
        for z in (-2.15,.3,2.75):
            endpoint=transform@Vector((x+side*.14,z,3.68))
            beam('side corbel',(x,3.06,z),(endpoint.x,endpoint.z,endpoint.y),.16,len(supports))
    bpy.context.view_layer.update()
    assert len(supports)==23
    assert all(bounds(bpy.data.objects[n])[1][2]>3.65 for n in supports if "corbel" in n)
    # Side rails/diagonals must stay below the sill, rather than cross the glass.
    rails=[bounds(bpy.data.objects[n])[1][2] for n in lowered]
    glass=[bounds(o)[0][2] for o in bpy.data.objects if o.type=='MESH' and o.name.startswith('Guild side casement glass')]
    assert max(rails)<min(glass)-.2
    (G.OUT/'guild-frame-depth.json').write_text(json.dumps({'transformed_original_members':changed,'lowered_side_framing':lowered,
      'new_supports':supports,'upper_XZ_scale':1.075,'nominal_front_projection_added':2.885*.075,
      'rail_glass_clearance':min(glass)-max(rails),'all_original_UVs_and_materials_retained':True,
      'native_oak_reused':oak.name,'new_images':False},indent=2),encoding='utf-8')


if __name__=='__main__':
    p=argparse.ArgumentParser(description=__doc__)
    for name in ('source','out','kit'):p.add_argument('--'+name,type=Path,required=True)
    p.add_argument('--resolution',type=int,default=900);p.add_argument('--samples',type=int,default=12)
    W.run(p.parse_args(sys.argv[sys.argv.index('--')+1:]),refiners={'guild':build},detail='v87',
          design='Supported projecting Guild hall, clear side casements and native-oak window knees; original paint/palette retained')
