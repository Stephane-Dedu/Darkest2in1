"""Refine verified forge, caravan and Guild crest models without rebuilding native parts.

Blender entry point. Source and output are private ten-model libraries outside Git.
The output textures directory must contain the reviewed v20 paint and v21 iron/canvas.
"""
import argparse
import hashlib
import json
import math
import shutil
import sys
from pathlib import Path

import bpy
import numpy as np
from mathutils import Vector, Matrix

sys.path.insert(0, str(Path(__file__).resolve().parent))
import geometry as G
import architecture as A
from render import camera, finish_scene


def remove(*prefixes):
    for ob in list(bpy.data.objects):
        if ob.type == 'MESH' and ob.name.startswith(prefixes):
            bpy.data.objects.remove(ob, do_unlink=True)


def paint(ob, material, rect=(0, 0, 1, 1), grain=False):
    """Per-face local mapping; long-axis wood grain follows the actual beam transform."""
    ob.data.materials.clear(); ob.data.materials.append(material)
    uv = ob.data.uv_layers.active or ob.data.uv_layers.new(name='UVMap')
    points = np.array([v.co[:] for v in ob.data.vertices])
    lo = points.min(0); span = np.maximum(.001, points.max(0)-lo)
    long = int(span.argmax())
    for face in ob.data.polygons:
        face.material_index = 0
        axis = max(range(3), key=lambda k: abs(face.normal[k]))
        axes = [k for k in range(3) if k != axis]
        if grain and long in axes: axes = [long, next(k for k in axes if k != long)]
        for li in face.loop_indices:
            v = ob.data.vertices[ob.data.loops[li].vertex_index].co
            u, w = [(v[k]-lo[k])/span[k] for k in axes]
            uv.data[li].uv = (rect[0]+u*(rect[2]-rect[0]), rect[1]+w*(rect[3]-rect[1]))
    return ob


def wood():
    return G.material('Workshop worn oak v21', (.76,.72,.67,1), 'wood_v20', .45)


def iron(light=False):
    return G.material('Workshop rubbed steel v21' if light else 'Workshop oxidised iron v21',
                      (1.7,1.6,1.45,1) if light else (.72,.68,.62,1), 'iron_v21', .5)


def timber(name, a, b, width=.12):
    return paint(G.beam(name, a, b, width, wood()), wood(), (.02,.07,.98,.93), True)


def replace_old_wood():
    for ob in list(bpy.data.objects):
        if ob.type != 'MESH' or ' / ' in ob.name: continue
        if any(m and m.name.startswith('DD2 painted wood panels') for m in ob.data.materials):
            paint(ob, wood(), (.02,.07,.98,.93), True)


def brick_paint(ob, material):
    """World-height courses survive the source glTF's rotated local axes."""
    ob.data.materials.clear();ob.data.materials.append(material)
    uv=ob.data.uv_layers.active
    bpy.context.view_layer.update()
    for face in ob.data.polygons:
        face.material_index=0
        normal=ob.matrix_world.to_3x3()@face.normal
        axes=(0,2) if abs(normal.y)>abs(normal.x) else (1,2)
        if abs(normal.z)>.7:axes=(0,1)
        for li in face.loop_indices:
            v=ob.matrix_world@ob.data.vertices[ob.data.loops[li].vertex_index].co
            uv.data[li].uv=(v[axes[0]]/3.5,v[axes[1]]/3.5)
    return ob


def metal_box(name, centre, size, bright=False, bevel=.03):
    return paint(G.box(name, centre, size, iron(bright), bevel), iron(bright), (.16,.18,.84,.82))


def forge():
    replace_old_wood()
    remove('Open forge lean-to', 'Awning verge', 'Anvil face', 'Anvil foot', 'Anvil waist',
           'Anvil tapered horn', 'Forge hammer', 'Forge stone hood', 'Forge hearth', 'Orange forge mouth')
    # Low, gently sagged canopy with real thickness; it joins below the cottage dormer.
    slate = G.material('Forge soot slate v21', (.66,.69,.72,1), 'slate_v20', .45)
    rows = [(-3.85,3.05),(-3.05,3.22),(-2.25,3.48),(-1.15,3.98)]
    verts = [(x,y-.07*math.sin((x+3.4)/5.8*math.pi),z) for z,y in rows for x in [-3.4,-1.95,-.5,.95,2.4]]
    faces=[];uvs=[]
    for j in range(3):
        for i in range(4):
            k=j*5+i; faces.append([k,k+1,k+6,k+5])
            uvs.append([(i/4,j/3),((i+1)/4,j/3),((i+1)/4,(j+1)/3),(i/4,(j+1)/3)])
    roof=G.mesh('Forge repaired slate canopy',verts,faces,slate,uvs)
    bpy.context.view_layer.objects.active=roof
    mod=roof.modifiers.new('Slate thickness','SOLIDIFY');mod.thickness=.10
    bpy.ops.object.modifier_apply(modifier=mod.name)
    timber('Forge eave beam',(-3.4,2.98,-3.85),(2.4,2.98,-3.85),.18)
    for x in [-3.4,2.4]:
        for (z,y),(zz,yy) in zip(rows,rows[1:]):timber('Forge verge timber',(x,y,z),(x,yy,zz),.13)
    for x in [-3,2]:
        timber('Forge knee brace',(x,2.35,-3.2),(x-.5 if x>0 else x+.5,3.04,-3.2),.14)
    # Smaller warm firebrick courses distinguish the workshop from the church stone.
    brick=G.material('Forge soot stained brick v21',(.72,.72,.73,1),'brick_v21',.4)
    for ob in list(bpy.data.objects):
        if ob.type!='MESH' or ' / ' in ob.name:continue
        if ob.name.startswith(('Tapered masonry chimney','Chimney cap')):
            brick_paint(ob,brick)
        elif ob.name.startswith('Stone foundation'):
            brick_paint(ob,A.paving())
    hood=G.box('Forge recessed firebrick hood',(1.55,1.62,-1.15),(1.7,2.64,1.35),brick,.05)
    shape=A.pointed(1.55,.62,1.20,1.35)
    A.subtract(hood,A.prism('Forge fire cavity',shape,-1.98,-.85,G.ink()))
    brick_paint(hood,brick)
    A.prism('Forge black interior',shape,-.88,-.82,G.ink())
    outer=A.pointed(1.55,.54,1.48,1.57)
    for i in range(1,len(shape)):
        j=(i+1)%len(shape)
        block=A.prism('Forge arch firebrick',[outer[i],outer[j],shape[j],shape[i]],-1.89,-1.69,brick)
        paint(block,brick,(.10,.81,.22,.86))
    brick_paint(G.box('Forge broad hearth',(1.55,.46,-2.1),(1.9,.32,1.7),brick,.05),brick)
    ember=G.material('Forge fire ember bed',(.9,.22,.02,1),'window_grain',2.5)
    for i in range(9):
        x=1.1+(i%3)*.34;z=-1.65+(i//3)*.23
        paint(G.box('Forge hot coal',(x,.7+.035*(i%2),z),(.23,.12,.18),ember,.035),ember)
    for i in range(4):
        x=1.17+i*.25;h=.35+.22*(i%2)
        G.mesh('Forge angular fire tongue',[(x-.10,.72,-1.38),(x+.09,.72,-1.38),(x+.07,1.0,-1.35),(x-.03,.72+h,-1.32)],[[0,1,2,3]],ember)
    # Faceted concave waist, wide feet, square heel and tapered round horn.
    x,y,z=-.7,1.,-2.4
    profiles=[(.0,.59,.33),(.15,.48,.29),(.32,.25,.22),(.52,.25,.22),(.69,.54,.29),(.84,.62,.30)]
    points=[(x+sx*w,y+h,z+sz*d) for h,w,d in profiles for sx,sz in [(-1,-1),(1,-1),(1,1),(-1,1)]]
    faces=[[3,2,1,0],[20,21,22,23]]
    faces += [[r*4+i,r*4+(i+1)%4,(r+1)*4+(i+1)%4,(r+1)*4+i] for r in range(5) for i in range(4)]
    paint(G.mesh('Forged anvil waist and feet',points,faces,iron()),iron(),(.15,.18,.82,.83))
    top=metal_box('Anvil rubbed working face',(x-.06,y+.875,z),(1.38,.11,.62),True,.025)
    A.subtract(top,G.box('Anvil hardy hole',(x-.55,y+.89,z+.08),(.14,.3,.14),G.ink()))
    points=[]
    for xx,r,h in [(x+.6,.29,y+.77),(x+.88,.18,y+.78),(x+1.30,.018,y+.80)]:
        points += [(xx,h+math.cos(a)*r*.44,z+math.sin(a)*r) for a in np.linspace(0,math.tau,8,endpoint=False)]
    faces=[[r*8+i,r*8+(i+1)%8,(r+1)*8+(i+1)%8,(r+1)*8+i] for r in range(2) for i in range(8)]
    paint(G.mesh('Anvil rounded tapering horn',points,faces,iron(True)),iron(True),(.18,.28,.76,.72))
    timber('Smith hammer oak handle',(-1.18,1.98,-2.55),(-.55,1.98,-2.3),.07)
    metal_box('Smith hammer steel head',(-1.18,1.99,-2.55),(.22,.20,.40),True)
    for yy in [.4,.85]:
        ring=G.cylinder('Anvil stump iron hoop',(-.7,yy,-2.4),.535,.045,iron(),10)
        paint(ring,iron())


def caravan():
    replace_old_wood()
    cloth=G.material('Nomad worn plum canvas v21',(.92,.88,.91,1),'cloth_v21',.65)
    for ob in list(bpy.data.objects):
        if ob.type!='MESH':continue
        if ob.name.startswith(('Curved purple caravan roof','Sagging market awning')):
            ob.data.materials.clear();ob.data.materials.append(cloth)
            # Original roof repeats the entire atlas twice in depth, showing duplicate patches.
            for face in ob.data.polygons:face.material_index=0
            if ob.name.startswith('Curved'):
                for loop in ob.data.uv_layers.active.data:loop.uv.y*=.5
    remove('Nomad rounded door','Nomad red glass','Caravan cloth hem','Entry stair')
    for i in range(5):
        y=(i+1)*1.65/5;z=-5.75+(i+.5)*2.4/5
        paint(G.box('Caravan separate stair tread',(0,y-.06,z),(1.65,.12,.47),wood(),.018),wood(),(.05,.58,.94,.70),True)
    for x in [-.68,.68]:timber('Caravan stair stringer',(x,.12,-5.7),(x,1.5,-3.4),.17)
    # Actual full plank door and surrounding boarded front; native Hoarder side joinery stays intact.
    for side in [-1,1]:
        for j in range(3):
            x=side*(.97+j*.34)
            paint(G.box('Caravan front individual board',(x,2.66,-3.22),(.325,2.05,.16),wood(),.018),wood(),(.02,.42+j*.12,.98,.51+j*.12),True)
        # The original clipped Hoarder cart left these sides open below the roof.
        # Close the living body with narrow, separate boards and physical corner posts.
        for j in range(5):
            paint(G.box('Caravan horizontal side board',(side*1.97,1.82+j*.39,0),(.12,.375,6.1),wood(),.015),
                  wood(),(.02,.12+(j%4)*.18,.98,.22+(j%4)*.18),True)
        for zz in [-3.1,3.1]:timber('Caravan corner joinery',(side*1.98,1.6,zz),(side*1.98,3.65,zz),.14)
        glass=G.material('Caravan amber side glass',(.43,.22,.065,1),'window_grain',1.1)
        paint(G.box('Caravan side casement',(side*2.045,2.75,.7),(.035,.82,.75),glass),glass)
        for yy in [2.30,3.20]:timber('Caravan side window sill',(side*2.075,yy,.25),(side*2.075,yy,1.15),.095)
        for zz in [.25,.7,1.15]:timber('Caravan side window jamb',(side*2.075,2.3,zz),(side*2.075,3.2,zz),.075)
    for j in range(9):
        paint(G.box('Caravan rear vertical board',(-1.76+j*.44,2.63,3.14),(.425,2.04,.14),wood(),.015),wood(),
              (.02,.12+(j%4)*.18,.98,.22+(j%4)*.18),True)
    shape=A.pointed(0,1.68,1.55,2.48)
    A.prism('Caravan dark door backing',shape,-3.40,-3.25,G.ink())
    def clip(poly, edge, keep_right):
        result=[]
        for a,b in zip(poly,poly[1:]+poly[:1]):
            ina=(a[0]>=edge) if keep_right else (a[0]<=edge)
            inb=(b[0]>=edge) if keep_right else (b[0]<=edge)
            if ina:result.append(a)
            if ina!=inb:
                t=(edge-a[0])/(b[0]-a[0]);result.append((edge,a[1]+t*(b[1]-a[1])))
        return result
    for i in range(7):
        left=-.775+i*1.55/7;right=left+1.55/7-.012
        poly=clip(clip(shape,left,True),right,False)
        paint(A.prism('Caravan fitted door plank',poly,-3.44,-3.40,wood()),wood(),(.03,.13+(i%4)*.18,.97,.23+(i%4)*.18),True)
    G.arch('Caravan small ruby glass',0,3.38,-3.49,.47,.52,True,(.34,.085,.065,1))
    for yy in [2.15,2.88]:
        metal_box('Caravan door hinge strap',(0,yy,-3.49),(1.35,.09,.055))
        for xx in [-.56,.56]:metal_box('Caravan hinge rivet',(xx,yy,-3.54),(.055,.055,.035),True,.01)
    metal_box('Caravan door latch',(.43,2.58,-3.55),(.10,.24,.07),True)
    for a,b in zip(shape,shape[1:]+shape[:1]):timber('Caravan shaped oak jamb',(a[0],a[1],-3.48),(b[0],b[1],-3.48),.10)
    # Low thin battens support the canvas without covering its painting in black bars.
    for zz in [-1.15,1.15]:
        profile=[(math.cos(i*math.pi/16)*2.255,3.61+math.sin(i*math.pi/16)*2.10,zz) for i in range(17)]
        for a,b in zip(profile,profile[1:]):paint(G.beam('Caravan thin roof binding',a,b,.035,iron()),iron())


def crest():
    remove('Guild crossed sword','Guild crossguard','Guild pointed shield')
    cx,cy,z=-.65,4.4,-2.83
    # Closed diamond-section blades, physical guards, handles and pommels.
    for sign in [-1,1]:
        before=set(bpy.data.objects)
        pts=[(-.10,-.25,0),(.10,-.25,0),(-.075,.62,0),(.075,.62,0),(0,.86,0),(0,-.25,-.045),(0,.62,-.045)]
        pts=[(cx+x,cy+y,z+d) for x,y,d in pts]
        faces=[[0,2,6,5],[5,6,3,1],[2,4,6],[6,4,3],[0,1,3,4,2],[0,5,1]]
        paint(G.mesh('Guild tapered steel blade',pts,faces,iron(True)),iron(True),(.28,.22,.66,.82))
        metal_box('Guild curved sword guard',(cx,cy-.28,z),(.49,.085,.095),True)
        timber('Guild leather wrapped grip',(cx,cy-.34,z),(cx,cy-.66,z),.085)
        metal_box('Guild faceted sword pommel',(cx,cy-.72,z),(.14,.12,.10),True)
        pivot=Vector(G.p((cx,cy,z)));rot=Matrix.Rotation(sign*math.radians(39),4,'Y')
        for ob in set(bpy.data.objects)-before:ob.matrix_world=Matrix.Translation(pivot)@rot@Matrix.Translation(-pivot)@ob.matrix_world
    shield=[(cx-.40,cy+.24),(cx+.40,cy+.24),(cx+.34,cy-.25),(cx,cy-.52),(cx-.34,cy-.25)]
    paint(A.prism('Guild steel rimmed shield',shield,z-.20,z-.11,iron(True)),iron(True),(.18,.18,.76,.76))
    inset=[(cx+(x-cx)*.82,cy+(y-cy)*.82) for x,y in shield]
    panel=G.material('Guild oxblood shield',(.43,.18,.12,1),'wood_v20',.5)
    paint(A.prism('Guild inset shield paint',inset,z-.225,z-.20,panel),panel,(.1,.2,.9,.8),True)
    metal_box('Guild shield centre rib',(cx,cy-.08,z-.25),(.045,.66,.045),True,.01)
    for xx in [-.29,.29]:metal_box('Guild shield rivet',(cx+xx,cy+.16,z-.25),(.045,.045,.04),True,.01)


REFINERS={'blacksmith':forge,'nomad_wagon':caravan,'guild':crest}


def run(args):
    source,out,kit=[p.resolve() for p in (args.source,args.out,args.kit)]
    repo=Path(__file__).resolve().parents[2]
    if out==source or out==repo or repo in out.parents:raise ValueError('Use a separate private output.')
    if (out/'verified.json').exists():raise ValueError('Preserve the previous verified study.')
    records=json.loads((source/'models.json').read_text());old=json.loads((source/'verified.json').read_text());checks={}
    for key,record in records['models'].items():
        path=source/record['glb'];folder=out/key;folder.mkdir(parents=True,exist_ok=True)
        if hashlib.sha256(path.read_bytes()).hexdigest()!=old[key]['sha256']:raise ValueError('Changed source '+key)
        if key not in REFINERS:
            shutil.copy2(path,out/record['glb']);checks[key]=old[key];continue
        bpy.ops.wm.read_factory_settings(use_empty=True);G.configure(kit,out,None,'v02');A.SERVICE=key
        bpy.ops.import_scene.gltf(filepath=str(path));bpy.context.view_layer.update()
        native={o.name:(o.data.copy(),o.matrix_world.copy()) for o in bpy.data.objects if o.type=='MESH' and ' / ' in o.name}
        REFINERS[key]();bpy.context.view_layer.update()
        for name,(mesh,transform) in native.items():
            ob=bpy.data.objects[name]
            assert np.allclose(ob.matrix_world,transform) and len(ob.data.vertices)==len(mesh.vertices),name
            assert all(a.co==b.co for a,b in zip(ob.data.vertices,mesh.vertices)),name
            assert all(a.uv==b.uv for a,b in zip(ob.data.uv_layers.active.data,mesh.uv_layers.active.data)),name
        objects=[o for o in bpy.data.objects if o.type=='MESH']
        points=np.array([tuple(o.matrix_world@v.co) for o in objects for v in o.data.vertices]);lo,hi=points.min(0),points.max(0)
        triangles=sum(len(p.vertices)-2 for o in objects for p in o.data.polygons)
        assert np.isfinite(points).all() and triangles<45000 and min(hi-lo)>1
        bpy.ops.object.select_all(action='DESELECT')
        for ob in objects:ob.select_set(True)
        bpy.context.view_layer.objects.active=objects[0];path=folder/(key+'.glb')
        bpy.ops.export_scene.gltf(filepath=str(path),export_format='GLB',use_selection=True,export_apply=True,export_yup=True,export_animations=False)
        bpy.ops.wm.read_factory_settings(use_empty=True);bpy.ops.import_scene.gltf(filepath=str(path));bpy.context.view_layer.update()
        meshes=[o for o in bpy.data.objects if o.type=='MESH'];points=np.array([tuple(o.matrix_world@v.co) for o in meshes for v in o.data.vertices])
        error=float(np.abs(np.array([points.min(0),points.max(0)])-np.array([lo,hi])).max())
        assert sum(len(p.vertices)-2 for o in meshes for p in o.data.polygons)==triangles and error<.001
        images=[im for im in bpy.data.images if im.type=='IMAGE']
        assert all(im.packed_file and len(im.pixels) for im in images)
        finish_scene();bpy.context.scene.cycles.samples=args.samples
        centre=(lo+hi)/2;target=(centre[0],centre[1],lo[2]+(hi[2]-lo[2])*.46);radius=max(hi-lo)*1.9;cam=None;views={}
        for name,angle in [('front',0),('hero',32),('rear',145),('side',-75)]:
            a=math.radians(angle);loc=(target[0]+math.sin(a)*radius,target[1]-math.cos(a)*radius,target[2]+radius*.38)
            if cam is None:cam=camera(loc,target,field=36,resolution=args.resolution)
            else:cam.location=loc;cam.rotation_euler=(Vector(target)-cam.location).to_track_quat('-Z','Y').to_euler()
            bpy.context.scene.render.filepath=str(folder/(name+'.png'));bpy.ops.render.render(write_still=True);views[name]=key+'/'+name+'.png'
        bpy.ops.file.pack_all();bpy.ops.wm.save_as_mainfile(filepath=str(folder/(key+'.blend')))
        record.update(triangles=triangles,mesh_objects=len(meshes),bounds_blender=[lo.tolist(),hi.tolist()],views=views,design='v21: fitted forge workshop, caravan cloth/joinery and physical Guild crest')
        checks[key]={'sha256':hashlib.sha256(path.read_bytes()).hexdigest(),'triangles':triangles,'bounds_error':error,'embedded_images':len(images),'native_parts_preserved':list(native),'verification':'Re-imported GLB topology, bounds, decoded paint and four review views'}
        print('VERIFIED',key,triangles,'triangles',len(images),'images',flush=True)
    records['detail_pass']='v21';(out/'models.json').write_text(json.dumps(records,indent=2));(out/'verified.json').write_text(json.dumps(checks,indent=2))
    print('PASS: three refined models and seven byte-identical source GLBs.',flush=True)


if __name__=='__main__':
    parser=argparse.ArgumentParser(description=__doc__)
    for name in ['source','out','kit']:parser.add_argument('--'+name,type=Path,required=True)
    parser.add_argument('--resolution',type=int,default=900);parser.add_argument('--samples',type=int,default=12)
    run(parser.parse_args(sys.argv[sys.argv.index('--')+1:]))
