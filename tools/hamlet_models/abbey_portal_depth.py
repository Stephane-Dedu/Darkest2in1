"""Give the retained Abbey entrance two stepped stone orders and a deep threshold."""
import argparse,json,math,sys
from pathlib import Path
import bpy,numpy as np
from mathutils import Vector
sys.path.insert(0,str(Path(__file__).resolve().parent))
import geometry as G,architecture as A,workshops as W

CENTRE=-2.55;BASE=1.32;WIDTH=1.65;HEIGHT=2.9
OLD_THICKNESS=.23;INNER_THICKNESS=.14;OUTER_THICKNESS=.22

def ordinal(name):return 0 if '.' not in name else int(name.rsplit('.',1)[1])
def courses(inner,outer):
    result=[]
    for i in range(1,len(inner)):
        j=(i+1)%len(inner);count=max(1,math.ceil(math.dist(inner[i],inner[j])/.39))
        for k in range(count):
            a,b=(k+.018)/count,(k+.982)/count
            mix=lambda p,q,t:np.array(p)*(1-t)+np.array(q)*t
            result.append((i,j,a,b,[mix(outer[i],outer[j],a),mix(outer[i],outer[j],b),mix(inner[i],inner[j],b),mix(inner[i],inner[j],a)]))
    return result

def normals(ob):
    ob.data.update();values=[None]*len(ob.data.loops)
    for face in ob.data.polygons:
        for li in face.loop_indices:values[li]=face.normal.copy()
    ob.data.normals_split_custom_set(values)

def fit_course(ob,source,target,front,back):
    matrix=ob.matrix_world.copy();inverse=matrix.inverted()
    for v in ob.data.vertices:
        p=matrix@v.co;index=int(np.linalg.norm(np.asarray(source)-np.array([p.x,p.z]),axis=1).argmin())
        assert np.linalg.norm(np.asarray(source[index])-np.array([p.x,p.z]))<2e-6,ob.name
        t=(p.y-(-4.555))/.19;assert -.0001<=t<=1.0001
        x,y=target[index];v.co=inverse@Vector((x,front+np.clip(t,0,1)*(back-front),y))
    normals(ob)

def relocate_beam(ob,a,b,c,d):
    a,b,c,d=[Vector(G.p(p)) for p in (a,b,c,d)];old=(b-a).normalized();new=(d-c).normalized()
    rotation=old.rotation_difference(new);ratio=(d-c).length/(b-a).length
    matrix=ob.matrix_world.copy();inverse=matrix.inverted()
    for v in ob.data.vertices:
        delta=matrix@v.co-(a+b)/2;axial=old*delta.dot(old)
        v.co=inverse@((c+d)/2+rotation@(delta-axial+axial*ratio))
    normals(ob)

def resize_sill(ob,width,depth,centre_depth,centre_x=CENTRE):
    matrix=ob.matrix_world.copy();inverse=matrix.inverted();points=np.array([tuple(matrix@v.co) for v in ob.data.vertices])
    lo,hi=points.min(0),points.max(0);mid=(lo+hi)/2;span=hi-lo
    for v in ob.data.vertices:
        p=matrix@v.co;p.x=centre_x+(p.x-mid[0])*width/span[0];p.y=centre_depth+(p.y-mid[1])*depth/span[1];v.co=inverse@p
    normals(ob)

def uv_rect(ob):
    coords=np.array([tuple(loop.uv) for loop in ob.data.uv_layers.active.data])
    return np.array([coords.min(0),coords.max(0)])

def fit_field(ob,target):
    source=uv_rect(ob);span=source[1]-source[0];assert span.min()>1e-6
    for loop in ob.data.uv_layers.active.data:
        loop.uv=target[0]+(np.array(loop.uv)-source[0])/span*(target[1]-target[0])

def build():
    assert not any(o.name.startswith('Abbey portal outer order') for o in bpy.data.objects)
    inner=A.pointed(CENTRE,BASE,WIDTH,HEIGHT)
    old_outer=A.pointed(CENTRE,BASE-OLD_THICKNESS,WIDTH+2*OLD_THICKNESS,HEIGHT+2*OLD_THICKNESS)
    new_inner=A.pointed(CENTRE,BASE-INNER_THICKNESS,WIDTH+2*INNER_THICKNESS,HEIGHT+2*INNER_THICKNESS)
    total=INNER_THICKNESS+OUTER_THICKNESS
    new_outer=A.pointed(CENTRE,BASE-total,WIDTH+2*total,HEIGHT+2*total)
    source=courses(inner,old_outer)
    objects=sorted([o for o in bpy.data.objects if o.type=='MESH' and o.name.startswith('Abbey portal fitted arch course')],key=lambda o:ordinal(o.name))
    assert len(source)==len(objects)==24
    fields=[uv_rect(ob) for ob in objects];field_records=[]
    def choose_field(start,excluded):
        for step in range(len(fields)):
            index=(start+step)%len(fields)
            if all(not np.allclose(fields[index],box,rtol=0,atol=1e-6) for box in excluded):return index,fields[index]
        raise AssertionError('No distinct approved stone field')
    for index,(ob,(i,j,a,b,quad)) in enumerate(zip(objects,source)):
        mix=lambda p,q,t:np.array(p)*(1-t)+np.array(q)*t
        inner_quad=[mix(new_inner[i],new_inner[j],a),mix(new_inner[i],new_inner[j],b),mix(inner[i],inner[j],b),mix(inner[i],inner[j],a)]
        outer_quad=[mix(new_outer[i],new_outer[j],a),mix(new_outer[i],new_outer[j],b),mix(new_inner[i],new_inner[j],b),mix(new_inner[i],new_inner[j],a)]
        duplicate=ob.copy();duplicate.data=ob.data.copy();bpy.context.collection.objects.link(duplicate)
        duplicate.name='Abbey portal outer order '+str(index+1).zfill(2);bpy.context.view_layer.update()
        fit_course(duplicate,quad,outer_quad,-4.76,-4.40)
        donor,rect=choose_field(index+5,[fields[index]])
        fit_field(duplicate,rect);field_records.append({'member':duplicate.name,'donor':objects[donor].name,'field':rect.tolist()})
        fit_course(ob,quad,inner_quad,-4.50,-4.34)
    hoods=sorted([o for o in bpy.data.objects if o.type=='MESH' and o.name.startswith('Abbey portal hood mould')],key=lambda o:ordinal(o.name))
    assert len(hoods)==7
    for i,ob in zip(range(2,9),hoods):
        a,b=old_outer[i],old_outer[i+1];c,d=new_outer[i],new_outer[i+1]
        relocate_beam(ob,(a[0],a[1]+.035,-4.59),(b[0],b[1]+.035,-4.59),
          (c[0],c[1]+.035,-4.82),(d[0],d[1]+.035,-4.82))
    sill=bpy.data.objects['Abbey portal deep dressed sill'];slabs=[sill]
    for i in range(2):
        duplicate=sill.copy();duplicate.data=sill.data.copy();duplicate.name='Abbey portal threshold side slab '+str(i+1).zfill(2)
        bpy.context.collection.objects.link(duplicate);slabs.append(duplicate)
    bpy.context.view_layer.update();slab_width=(2.45-.024)/3;selected=[]
    for slab,position,start in zip(slabs,(0,-1,1),(7,12,18)):
        resize_sill(slab,slab_width,.75,-4.505,CENTRE+position*(slab_width+.012))
        donor,rect=choose_field(start,selected);selected.append(rect)
        fit_field(slab,rect);field_records.append({'member':slab.name,'donor':objects[donor].name,'field':rect.tolist()})
    resize_sill(bpy.data.objects['Abbey portal sill drip edge'],2.51,.10,-4.88)
    (G.OUT/'abbey-portal-depth.json').write_text(json.dumps({'source_orders':1,'candidate_orders':2,
      'opening_unity':[WIDTH,HEIGHT],'inner_thickness':INNER_THICKNESS,'outer_thickness':OUTER_THICKNESS,
      'inner_front_depth':-4.50,'outer_front_depth':-4.76,'hood_depth':-4.82,
      'added_members':26,'changed_retained_members':33,'added_triangles':376,
      'original756_UV_fields_retained':True,'existing_threshold_UV_field_changed':True,
      'new_stone_fields':field_records,'three_threshold_slabs_with_12mm_joints':True,
      'retained_materials_and_decoded_paint':True,'new_bitmaps':False,
      'native_references':'Inspected Shrine circular ring and Sprawl closed pediment do not fit this pointed doorway without distortion.'},indent=2),encoding='utf-8')

if __name__=='__main__':
    p=argparse.ArgumentParser(description=__doc__)
    for n in ('source','out','kit'):p.add_argument('--'+n,type=Path,required=True)
    p.add_argument('--resolution',type=int,default=900);p.add_argument('--samples',type=int,default=12)
    args=p.parse_args(sys.argv[sys.argv.index('--')+1:]);source,out=args.source.resolve(),args.out.resolve()
    assert not out.exists() and source not in out.parents and out not in source.parents
    W.run(args,refiners={'abbey':build},detail='v86-study',design='Stepped two-order pointed portal and a deeper threshold in accepted Abbey stone; fixed door, rose, native architecture and shared palette')
