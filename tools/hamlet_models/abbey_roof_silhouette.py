"""Study a continuous dipped nave roof without repainting the accepted Abbey."""
import argparse,json,math,sys
from pathlib import Path
import bpy,numpy as np
from mathutils import Vector
sys.path.insert(0,str(Path(__file__).resolve().parent))
import geometry as G
import workshops as W

NAMES=('Abbey sweeping nave painted roof','Abbey sweeping nave folded ridge caps')
LENGTH=8.7;WIDTH=6.7;CENTRE_DEPTH=.5

def sample_triangles(ob,n=4):
    """Linear barycentric samples retain each original triangle and UV field."""
    old=ob.data;old.calc_loop_triangles()
    points=[];faces=[];coords=[];normals=[];slots=[]
    for tri in old.loop_triangles:
        p=[old.vertices[i].co.copy() for i in tri.vertices]
        uv=[old.uv_layers.active.data[i].uv.copy() for i in tri.loops]
        normal=[old.corner_normals[i].vector.copy() for i in tri.loops]
        for i in range(n):
            for j in range(n-i):
                cells=[((i,j),(i+1,j),(i,j+1))]
                if i+j<n-1:cells.append(((i+1,j),(i+1,j+1),(i,j+1)))
                for cell in cells:
                    face=[]
                    for a,b in cell:
                        weights=(1-(a+b)/n,a/n,b/n)
                        points.append(tuple(sum((v*w for v,w in zip(p,weights)),Vector())))
                        coords.append(tuple(sum((v*w for v,w in zip(uv,weights)),Vector((0,0)))))
                        normals.append(sum((v*w for v,w in zip(normal,weights)),Vector()).normalized())
                        face.append(len(points)-1)
                    faces.append(face);slots.append(tri.material_index)
    mesh=bpy.data.meshes.new(old.name+' linear samples');mesh.from_pydata(points,[],faces);mesh.update()
    for mat in old.materials:mesh.materials.append(mat)
    layer=mesh.uv_layers.new(name='UVMap')
    for loop,uv in zip(layer.data,coords):loop.uv=uv
    for face,slot in zip(mesh.polygons,slots):face.material_index=slot
    mesh.normals_split_custom_set(normals);ob.data=mesh

def field(p,amount):
    x,y=float(p.x),float(p.y)-CENTRE_DEPTH
    if abs(x)>=LENGTH/2 or abs(y)>=WIDTH/2:return 0.,0.,0.
    cx=math.cos(math.pi*x/LENGTH)**2;cy=math.cos(math.pi*y/WIDTH)**2
    dz=-amount*cx*cy
    dx=amount*math.pi/LENGTH*math.sin(2*math.pi*x/LENGTH)*cy
    dy=amount*math.pi/WIDTH*cx*math.sin(2*math.pi*y/WIDTH)
    return dz,dx,dy

def build(amount):
    evidence=[]
    for name in NAMES:
        ob=bpy.data.objects[name]
        assert ob.type=='MESH'
        before=sum(len(p.vertices)-2 for p in ob.data.polygons)
        assert before==(104 if name.endswith('painted roof') else 552)
        if name.endswith('painted roof'):
            # Explicit samples avoid modifier limit-surface and UV smoothing.
            sample_triangles(ob)
        bpy.context.view_layer.update()
        matrix=ob.matrix_world.copy();inverse=matrix.inverted()
        normal_to_world=matrix.to_3x3().inverted().transposed()
        normal_to_local=matrix.to_3x3().transposed()
        old_normals=[n.vector.copy() for n in ob.data.corner_normals]
        points=[matrix@v.co for v in ob.data.vertices]
        samples=[field(p,amount) for p in points]
        normals=[]
        for loop,n in zip(ob.data.loops,old_normals):
            _,dx,dy=samples[loop.vertex_index];world=normal_to_world@n
            world=Vector((world.x-dx*world.z,world.y-dy*world.z,world.z)).normalized()
            normals.append((normal_to_local@world).normalized())
        for vertex,p,(dz,_,_) in zip(ob.data.vertices,points,samples):
            vertex.co=inverse@(p+Vector((0,0,dz)))
        ob.data.update();ob.data.normals_split_custom_set(normals)
        evidence.append({'member':name,'triangles_before':before,
          'triangles_after':sum(len(p.vertices)-2 for p in ob.data.polygons),
          'maximum_extra_drop':max(-s[0] for s in samples),
          'native_paint_and_UV_fields_preserved':True,'normal_method':'Inverse-transpose analytic deformation Jacobian'})
    (G.OUT/'abbey-roof-silhouette.json').write_text(json.dumps({'extra_ridge_sag':amount,
      'boundary_offset_and_gradient':0,'changed_members':evidence,
      'native_belfry_pinnacles_entrance_and_other_architecture_unchanged':True,
      'new_bitmaps':False,'material_changes':False},indent=2),encoding='utf-8')

if __name__=='__main__':
    p=argparse.ArgumentParser(description=__doc__)
    for n in ('source','out','kit'):p.add_argument('--'+n,type=Path,required=True)
    p.add_argument('--resolution',type=int,default=900);p.add_argument('--samples',type=int,default=12)
    p.add_argument('--sag',type=float,default=.6)
    args=p.parse_args(sys.argv[sys.argv.index('--')+1:])
    assert 0<args.sag<=.8
    source,out=args.source.resolve(),args.out.resolve()
    assert not out.exists() and source not in out.parents and out not in source.parents
    W.run(args,refiners={'abbey':lambda:build(args.sag)},detail='v85-study',
      design='Continuous dipped nave ridge, intact native shingles and fixed eaves/verges; original Abbey identity and shared palette retained')
