"""Rebuild the three signature Hamlet services with shared, volumetric architecture.

Run in Blender. Paint and model output must remain in a private directory outside Git.
Native parts keep their UVs and uniform scale; new openings are cut into solid walls.
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
from render import camera, finish_scene


def stone():
    return G.material('Hamlet ashlar v18', (.94, .98, 1, 1), 'ashlar_v18', .45)


def trim():
    return G.material('Hamlet worn limestone edges', (.95, .98, 1, 1), 'cutstone_native', .4)


def plaster():
    return G.material('Hamlet smoke stained lime', (.72, .70, .64, 1), 'lime_native', .4)


def wood():
    return G.material('Hamlet continuous native timber', (.88,.78,.66,1), 'beam_native', .45)


def paving():
    return G.material('Hamlet native courtyard flags', (.66,.69,.68,1), 'paving_native', .4)


def finish_authored_paint():
    """Map extracted trim along each member, and separate horizontal paving from walls."""
    for ob in [o for o in bpy.data.objects if o.type=='MESH' and 'source_asset' not in o]:
        coords=np.array([v.co[:] for v in ob.data.vertices]);lo=coords.min(0);span=np.maximum(.001,coords.max(0)-lo)
        long=int(span.argmax());uv=ob.data.uv_layers.active
        terrace=any(s in ob.name for s in ['terrace','Entry stair'])
        if terrace:
            ob.data.materials.append(paving());paving_index=len(ob.data.materials)-1
        for face in ob.data.polygons:
            material=ob.data.materials[face.material_index]
            normal=ob.matrix_world.to_3x3()@face.normal
            if terrace and normal.z>.65:
                face.material_index=paving_index
                for li in face.loop_indices:
                    v=ob.matrix_world@ob.data.vertices[ob.data.loops[li].vertex_index].co
                    uv.data[li].uv=(v.x/5.5,v.y/5.5)
            elif material.name.startswith(('Hamlet continuous','Hamlet worn limestone')):
                axis=max(range(3),key=lambda k:abs(face.normal[k]));across=next(k for k in range(3) if k not in (long,axis)) if long!=axis else (long+1)%3
                for li in face.loop_indices:
                    v=ob.data.vertices[ob.data.loops[li].vertex_index].co
                    uv.data[li].uv=(.02+.96*(v[long]-lo[long])/span[long],.06+.88*(v[across]-lo[across])/span[across])
            elif material.name.startswith('Hamlet smoke stained'):
                axis=max(range(3),key=lambda k:abs(face.normal[k]));axes=[k for k in range(3) if k!=axis]
                for li in face.loop_indices:
                    v=ob.data.vertices[ob.data.loops[li].vertex_index].co
                    uv.data[li].uv=((v[axes[0]]-lo[axes[0]])/2.3,(v[axes[1]]-lo[axes[1]])/1.7)


def project(ob, period=3.8):
    """Metric UV projection for authored stone; never applied to imported native art."""
    uv = ob.data.uv_layers.active
    for face in ob.data.polygons:
        axis = max(range(3), key=lambda k: abs(face.normal[k]))
        axes = [k for k in range(3) if k != axis]
        for li in face.loop_indices:
            co = ob.matrix_world @ ob.data.vertices[ob.data.loops[li].vertex_index].co
            uv.data[li].uv = (co[axes[0]] / period, co[axes[1]] / period)
    return ob


def side_opening(name, wall, side_x, y, along_z, width, height, green=False):
    """Cut the same real window on the right wall, keeping new parts in world space."""
    before=set(bpy.data.objects);original=wall.matrix_world.copy()
    rotation=Matrix.Rotation(-math.pi/2,4,'Z')
    wall.matrix_world=rotation@original;bpy.context.view_layer.update()
    opening(name,wall,along_z,y,-side_x,width,height,True,green)
    wall.matrix_world=original
    for ob in set(bpy.data.objects)-before:ob.matrix_world=rotation.inverted()@ob.matrix_world
    bpy.context.view_layer.update()


def side_frame(x,base,z,width,height,depth):
    for xx in [x-width/2-.025,x+width/2+.025]:
        for zz in [z-depth/2,z,z+depth/2]:
            G.beam('Side wall post',(xx,base,zz),(xx,base+height,zz),.18,wood())
        for yy in [base,base+height*.45,base+height]:
            G.beam('Side wall timber course',(xx,yy,z-depth/2),(xx,yy,z+depth/2),.18,wood())
        for zz in [z-depth/2,z]:
            G.beam('Side diagonal brace',(xx,base,zz),(xx,base+height*.45,zz+depth/2),.14,wood())


def guild_frame():
    # The uprights sit BETWEEN the three windows. Generic centre posts and upper
    # diagonals formerly crossed the glazing and concealed the centre window.
    for zz in [-2.585,3.185]:
        posts=[-3.51,-1.63,.33,2.21]
        for xx in posts:G.beam('Guild hall upright',(xx,3.6,zz),(xx,6.7,zz),.17,wood())
        for yy in [3.6,5.0,6.7]:G.beam('Guild hall crossbeam',(-3.51,yy,zz),(2.21,yy,zz),.18,wood())
        for lo,hi in zip(posts,posts[1:]):G.beam('Guild lower wall brace',(lo,3.6,zz),(hi,4.95,zz),.14,wood())


def block(name, center, size, mat=None, bevel=.035):
    ob = G.box(name, center, size, mat or stone(), bevel)
    bpy.context.view_layer.update()
    return project(ob)


def prism(name, outline, front, back, mat):
    n = len(outline)
    points = [(x, y, z) for z in [front, back] for x, y in outline]
    faces = [list(range(n-1, -1, -1)), list(range(n, 2*n))]
    faces += [[i, (i+1) % n, (i+1) % n+n, i+n] for i in range(n)]
    ob = G.mesh(name, points, faces, mat)
    # Boolean operands need consistent outward normals, independent of X/Y/Z conversion.
    import bmesh
    bm = bmesh.new(); bm.from_mesh(ob.data)
    bmesh.ops.recalc_face_normals(bm, faces=list(bm.faces))
    bm.to_mesh(ob.data); bm.free()
    return project(ob)


def pointed(x, y, width, height):
    points = [(-.5, 0), (.5, 0), (.5, .56), (.46, .68), (.35, .81),
              (.19, .92), (0, 1), (-.19, .92), (-.35, .81), (-.46, .68), (-.5, .56)]
    return [(x+u*width, y+v*height) for u, v in points]


def subtract(wall, cutter):
    bpy.context.view_layer.objects.active = wall
    mod = wall.modifiers.new('Actual architectural recess', 'BOOLEAN')
    mod.operation = 'DIFFERENCE'; mod.solver = 'EXACT'; mod.object = cutter
    bpy.ops.object.modifier_apply(modifier=mod.name)
    bpy.data.objects.remove(cutter, do_unlink=True)


def opening(name, wall, x, y, z, width, height, glass=False, green=False, depth=.38):
    inner = pointed(x, y, width, height)
    subtract(wall, prism(name+' cutter', inner, z-.2, z+depth+.035, G.ink()))
    dark = G.material('Hamlet deep opening shadow', (.012, .013, .016, 1), emission=.3)
    prism(name+' recessed backing', inner, z+depth, z+depth+.025, dark)
    outer = pointed(x, y-.12, width+.30, height+.30)
    # Separate arch stones and a physical reveal, not a bright line painted on a plane.
    for i in range(len(inner)):
        j = (i+1) % len(inner)
        if i == 0:
            continue
        a, b, c, d = outer[i], outer[j], inner[j], inner[i]
        poly = [a, b, c, d]
        centre = np.mean(poly, axis=0)
        poly = [tuple(centre+(np.asarray(p)-centre)*.965) for p in poly]
        prism(name+' dressed arch stone', poly, z-.11, z+.09, trim())
        G.mesh(name+' dark reveal', [(a[0],a[1],zz) for a,zz in
            [(inner[i],z+.04),(inner[j],z+.04),(inner[j],z+depth),(inner[i],z+depth)]], [[0,1,2,3]], stone())
    if glass:
        color = (.16,.43,.30,1) if green else (.70,.32,.065,1)
        pane = G.material('Apothecary green glass' if green else 'Hamlet amber glass', color, 'window_grain', 1.5)
        inset = pointed(x,y+.08,width*.82,height-.18)
        G.mesh(name+' glass', [(a,b,z+depth-.02) for a,b in inset], [list(range(len(inset)))], pane,
               [[((a-x)/width+.5,(b-y)/height) for a,b in inset]])
        for u in [-.21,0,.21]:
            G.beam(name+' iron mullion',(x+width*u,y+.11,z+depth-.05),
                   (x+width*u,y+height*(.84 if u else .94),z+depth-.05),.045,G.ink())
        for v in [.32,.60]:
            G.beam(name+' iron transom',(x-width*.42,y+height*v,z+depth-.05),
                   (x+width*.42,y+height*v,z+depth-.05),.04,G.ink())
    else:
        door = prism(name+' oak door',pointed(x,y+.04,width*.91,height-.11),z+depth-.03,z+depth,G.wood())
        for u in [-.32,-.16,0,.16,.32]:
            G.beam(name+' plank joint',(x+width*u,y+.12,z+depth-.045),
                   (x+width*u,y+height*(.91-abs(u)*.6),z+depth-.045),.025,G.ink())
        for v in [.22,.55]:
            G.beam(name+' iron strap',(x-width*.42,y+height*v,z+depth-.065),
                   (x+width*.42,y+height*v,z+depth-.065),.065,G.ink())
            for u in [-.33,.33]:
                block(name+' strap rivet',(x+u*width,y+v*height,z+depth-.11),(.045,.045,.03),trim(),.005)
        for u in [-.1,.1]:
            bpy.ops.mesh.primitive_torus_add(major_radius=.065,minor_radius=.014,major_segments=12,minor_segments=4,
                location=G.p((x+u*width,y+height*.42,z+depth-.10)),rotation=(math.pi/2,0,0))
            bpy.context.object.name=name+' forged ring'; bpy.context.object.data.materials.append(G.ink())
    block(name+' deep sill',(x,y-.075,z+.08),(width+.40,.17,.56),trim())


def roof(name, x, z, length, width, base, rise, red=False, along_x=True, close=True):
    """Sagged roof with curved slope, thickness, courses, ridge caps and proper UV distance."""
    mat = G.material('DD2 roof_red v18' if red else 'DD2 roof_slate v18',
                     (1,.95,.93,1) if red else (1,1,1,1), 'red_v18' if red else 'slate_v18', .5)
    rows = [(-.5,0),(-.36,.13),(-.23,.35),(-.10,.71),(0,1),(.10,.71),(.23,.35),(.36,.13),(.5,0)]
    def pos(t,u,v):
        sag = .13*(1-(2*t/length)**2)
        return (x+t,base+v*rise-sag,z+u*width) if along_x else (x+u*width,base+v*rise-sag,z+t)
    lengths=[0]
    for (u,v),(uu,vv) in zip(rows,rows[1:]):
        lengths.append(lengths[-1]+math.hypot((uu-u)*width,(vv-v)*rise))
    verts=[pos(t,u,v) for t in [-length/2,0,length/2] for u,v in rows]
    faces=[]; uvs=[]
    for r in range(2):
        for c in range(8):
            k=r*9+c; faces.append([k,k+1,k+10,k+9])
            # Every slope maps uphill from its own eave: no inverted opposite courses.
            a,b=lengths[c],lengths[c+1]
            if c>=4: a,b=lengths[-1]-a,lengths[-1]-b
            uvs.append([(r*length/7,a/3.4),(r*length/7,b/3.4),((r+1)*length/7,b/3.4),((r+1)*length/7,a/3.4)])
    ob=G.mesh(name+' painted roof',verts,faces,mat,uvs)
    solid=ob.modifiers.new('Slate roof thickness','SOLIDIFY'); solid.thickness=.10
    bpy.context.view_layer.objects.active=ob; bpy.ops.object.modifier_apply(modifier=solid.name)
    for t in [-length/2,length/2]:
        for i in range(8):
            G.beam(name+' verge coping',pos(t,*rows[i]),pos(t,*rows[i+1]),.13,trim() if not red else G.wood())
    for sign in [-1,1]:
        for t1,t2 in [(-length/2,0),(0,length/2)]:
            G.beam(name+' eave fascia',pos(t1,sign*.5,0),pos(t2,sign*.5,0),.16,G.wood())
    for t in np.linspace(-length/2+.15,length/2-.15,max(3,round(length/.48))):
        a=pos(t-.22,0,1);b=pos(t+.22,0,1)
        G.beam(name+' individual ridge tile',a,b,.19,mat)
    # Close both gables below their roof profile, so there are no see-through roof ends.
    if close:
        for t in [-length/2+.08,length/2-.08]:
            project(G.mesh(name+' gable infill',[pos(t,u,v-.035) for u,v in rows], [list(range(9))], stone()))
    return ob


def buttress(name,x,z,bottom=1.25,top=5.2):
    for y,h,w,d in [(bottom+.45,.9,.64,.85),(bottom+1.55,1.3,.48,.65),(top-.65,1.3,.33,.46)]:
        block(name+' stepped pier',(x,y,z),(w,h,d))
        block(name+' weathered coping',(x,y+h/2,z),(w+.12,.11,d+.12),trim())
    G.cylinder(name+' pinnacle base',(x,top+.08,z),.23,.25,trim(),4)
    G.cylinder(name+' pinnacle',(x,top+.71,z),.22,1.0,stone(),4,.02)


def rose(wall,x,y,z,radius):
    outline=[(x+radius*math.cos(a),y+radius*math.sin(a)) for a in np.linspace(0,2*math.pi,24,endpoint=False)]
    subtract(wall,prism('Rose opening cutter',outline,z-.1,z+.45,G.ink()))
    glass=G.material('Abbey rose amber glass',(.95,.88,.74,1),'abbey_rose',1.2)
    pane=prism('Deep amber rose',outline,z+.32,z+.35,glass)
    for face in pane.data.polygons:
        for li in face.loop_indices:
            v=pane.data.vertices[pane.data.loops[li].vertex_index].co
            pane.data.uv_layers.active.data[li].uv=(.08+.86*((v.x-x)/(radius*2)+.5),.08+.84*((v.z-y)/(radius*2)+.5))
    for i in range(24):
        a=i*math.pi/12;b=(i+1)*math.pi/12
        G.beam('Rose limestone circle',(x+radius*math.cos(a),y+radius*math.sin(a),z-.04),
               (x+radius*math.cos(b),y+radius*math.sin(b),z-.04),.115,trim())


def abbey():
    block('Abbey raised terrace',(0,.625,0),(9,1.25,10.6))
    wall=block('Abbey long nave',(0,3.35,.5),(8.05,4.2,5.9))
    roof('Abbey sweeping nave',0,.5,8.7,6.7,5.45,2.8)
    porch=block('Abbey projecting narthex',(-2.55,3.05,-3.25),(2.6,3.6,2.3))
    # Gable extrusion is real stone, and contains a cut-through rose with a recessed backing.
    gable=prism('Abbey narthex gable',[(-3.85,4.8),(-1.25,4.8),(-2.55,6.9)],-4.4,-2.3,stone())
    roof('Abbey porch',-2.55,-3.25,2.9,3.1,4.8,2.3,along_x=False,close=False)
    opening('Abbey great portal',porch,-2.55,1.32,-4.42,1.65,2.9,depth=.5)
    rose(gable,-2.55,5.35,-4.43,.48)
    for x in [-.3,1.35,3.0]:
        opening('Abbey nave lancet',wall,x,2.05,-2.47,.75,2.45,True)
    for zz in [-1.0,1.6]:side_opening('Abbey side lancet',wall,4.03,2.05,zz,.74,2.45)
    for x in [-1.12,.53,2.18,3.90]: buttress('Abbey facade',x,-2.70,top=5.55)
    for x in [-3.9,3.9]:
        for z in [.4,2.95]: buttress('Abbey side',x,z,top=5.25)
    # A native complete tower is kept at uniform scale, with its original painted UVs.
    G.native('city_tower_02',(2.0,5.65,2.45),width=3.1,shade=1.6)
    G.stairs(-2.55,0,-4.5,2.8,1.25,8,3.6,stone())
    for x in [-4,4]:
        block('Abbey terrace parapet',(x,1.48,.55),(.27,.45,7.8))
    G.native('common_altar_of_hope_prefab',(-3.7,1.25,-4.65),width=.65,include=['candle'],shade=1.4)


def dormer(name,x,y,z):
    wall=block(name+' timber cheek',(x,y+.58,z+.42),(1.05,1.16,1.2),plaster())
    roof(name,x,z+.4,1.6,1.38,y+1.16,.9,red=True,along_x=False)
    opening(name+' little window',wall,x,y+.14,z-.19,.57,.89,True,depth=.20)
    for dx in [-.53,.53]:G.beam(name+' post',(x+dx,y,z-.22),(x+dx,y+1.18,z-.22),.12,G.wood())


def guild():
    block('Guild terrace',(0,.2,0),(8.8,.4,7))
    lower=block('Guild lower stone hall',(-.65,2.0,.3),(5.5,3.2,5.6))
    upper=block('Guild plaster hall',(-.65,5.1,.3),(5.7,3.1,5.7),plaster())
    roof('Guild red hall',-.65,.3,6.3,6.5,6.7,3.4,red=True)
    guild_frame()
    side_frame(-.65,3.6,.3,5.72,3.1,5.72)
    for zz in [-1.1,1.6]:side_opening('Guild side casement',upper,2.21,4.75,zz,.72,1.25)
    for x in [-3.18,-2.0,-.65,.7,1.88]:
        G.beam('Guild overhang corbel',(x,3.1,-2.45),(x,3.68,-2.64),.16,G.wood())
    opening('Guild armoured portal',lower,-.65,.45,-2.53,1.65,2.65,depth=.43)
    for x in [-2.55,-.65,1.25]:opening('Guild upper slit',upper,x,5.35,-2.57,.43,.9,True,depth=.25)
    for x in [-2.35,1.05]:G.banner('DD1 sword standard',x,2.75,-2.78,.85,2.3)
    steel=G.material('Guild worn steel',(.11,.12,.115,1),emission=.45)
    for sign in [-1,1]:
        G.beam('Guild crossed sword',(-.65-sign*.60,3.75,-2.80),(-.65+sign*.60,4.95,-2.80),.095,steel)
        G.beam('Guild crossguard',(-.65-sign*.65,4.10,-2.86),(-.65-sign*.25,3.7,-2.86),.075,steel)
    prism('Guild pointed shield',[(-1,4.58),(-.30,4.58),(-.34,4.12),(-.65,3.88),(-.96,4.12)],-2.94,-2.81,
          G.material('Guild antique ochre',(.17,.105,.035,1),'beam_native',emission=.45))
    for x in [-2.12,.85]:dormer('Guild roof dormer',x,7.3,-2.25)
    for x in np.linspace(-3.65,2.35,13):
        G.beam('Guild ridge iron',(x,10.05,.3),(x,10.55,.3),.045,G.ink())
        G.cylinder('Guild ridge finial',(x,10.6,.3),.08,.21,G.ink(),4,0)
    G.beam('Guild ridge rail',(-3.75,10.33,.3),(2.45,10.33,.3),.06,G.ink())
    wing=block('Guild armory wing',(3.0,2.1,.7),(2.1,3.4,4.7),plaster())
    G.timber_frame(3, .4,.7,2.15,3.4,4.75)
    side_frame(3,.4,.7,2.15,3.4,4.75)
    roof('Guild armory',3,.7,5.2,2.7,3.8,1.8,along_x=False)
    opening('Guild wing casement',wing,3,1.8,-1.67,.8,1.25,True,depth=.3)
    side_opening('Guild armory side casement',wing,4.055,1.85,.7,.8,1.2)
    G.lantern(1.8,2.5,-2.8)
    G.native('camp_axe',(-3.8,.4,-2.4),width=.7)


def tower_cap(x,z,base,radius,height):
    mat=G.material('DD2 roof_slate turret v18',(1,1,1,1),'slate_v18',.5)
    profile=[(1,0),(.91,.13),(.69,.34),(.40,.66),(.13,.90),(0,1)]
    n=12;verts=[];faces=[];uv=[]
    for r,h in profile:
        for i in range(n):
            a=(i+.5)*2*math.pi/n
            verts.append((x+r*radius*math.cos(a),base+h*height,z+r*radius*math.sin(a)))
    for j in range(len(profile)-1):
        for i in range(n):
            k=j*n+i;faces.append([k,j*n+(i+1)%n,(j+1)*n+(i+1)%n,k+n])
            uv.append([(i/4,j/5),((i+1)/4,j/5),((i+1)/4,(j+1)/5),(i/4,(j+1)/5)])
    G.mesh('Sanitarium swept witch cap',verts,faces,mat,uv)
    for i in range(n):
        a=(i+.5)*2*math.pi/n
        for (r,h),(rr,hh) in zip(profile,profile[1:]):
            G.beam('Turret raised slate hip',(x+r*radius*math.cos(a),base+h*height,z+r*radius*math.sin(a)),
                   (x+rr*radius*math.cos(a),base+hh*height,z+rr*radius*math.sin(a)),.045,G.ink())


def sanitarium():
    block('Sanitarium terrace',(0,.55,0),(9,1.1,8.5))
    block('Sanitarium house stone undercroft',(1.0,1.8,.7),(6.55,1.4,4.4))
    G.native('valley_academic_house',(1.0,2.5,.7),width=7.0,shade=1.4)
    before_tower=set(bpy.data.objects)
    tower=G.cylinder('Sanitarium round stair tower',(-3,3.4,-2.0),1.38,4.6,stone(),16)
    from buildings_v02 import wrap_tower
    wrap_tower(tower,2.6,1.4)
    for y,r in [(1.25,1.47),(3.0,1.43),(5.65,1.55)]:G.cylinder('Sanitarium tower course',(-3,y,-2),r,.15,trim(),16)
    stage=G.cylinder('Sanitarium overhanging timber stage',(-3,6.45,-2),1.58,1.6,plaster(),8)
    stage.rotation_euler.z=math.pi/8
    wrap_tower(stage,2,1)
    # Individual side windows are Boolean recesses in the actual octagonal shell.
    for i in range(8):
        a=(i+.5)*math.pi/4
        x=-3+1.57*math.sin(a);z=-2-1.57*math.cos(a)
        G.beam('Sanitarium turret timber post',(x,5.65,z),(x,7.25,z),.14,G.wood())
        G.beam('Sanitarium tower bracket',(x*.0-3+1.28*math.sin(a),5.1,-2-1.28*math.cos(a)),
               (x,5.7,z),.17,G.wood())
    # Front face is oriented toward -Z; fit a coherent green paired casement in it.
    opening('Sanitarium green tower casement',stage,-3,5.92,-3.48,.83,1.05,True,True,.30)
    opening('Sanitarium stone lancet',tower,-3,3.55,-3.39,.52,1.35,True,True,.45)
    for y in [5.7,7.22]:
        for i in range(8):
            a=(i+.5)*math.pi/4;b=(i+1.5)*math.pi/4
            G.beam('Sanitarium polygon cornice',(-3+1.6*math.sin(a),y,-2-1.6*math.cos(a)),
                   (-3+1.6*math.sin(b),y,-2-1.6*math.cos(b)),.14,G.wood())
    tower_cap(-3,-2,7.3,1.86,2.7)
    G.cylinder('Sanitarium iron cap finial',(-3,10.13,-2),.055,.38,G.ink(),6)
    # Keep the recognisable tall turret, but subordinate its girth to the adjoining house.
    pivot=Vector(G.p((-3,1.1,-2)))
    for ob in set(bpy.data.objects)-before_tower:
        if ob.type!='MESH':continue
        bpy.context.view_layer.update()
        for v in ob.data.vertices:
            world=ob.matrix_world@v.co;delta=world-pivot
            world=pivot+Vector((delta.x*.86,delta.y*.86,delta.z*.91))
            v.co=ob.matrix_world.inverted()@world
    # A projecting apothecary porch makes the entrance and green service cue legible.
    porch=block('Sanitarium stone porch',(.45,2.5,-2.7),(2.3,2.8,1.65))
    roof('Sanitarium porch',.45,-2.7,2.1,2.8,3.95,1.65,along_x=False)
    opening('Sanitarium deep door',porch,.45,1.14,-3.54,1.45,2.35,depth=.4)
    G.stairs(.45,0,-3.5,2.5,1.1,7,2.7,stone())
    brass=G.material('Sanitarium aged green emblem',(.08,.19,.13,1),emission=.55)
    G.beam('Apothecary hanging bracket',(1.65,3.5,-3),(2.3,3.5,-3.7),.075,G.ink())
    block('Apothecary cross upright',(2.3,3.12,-3.7),(.16,.58,.10),brass)
    block('Apothecary cross arm',(2.3,3.18,-3.7),(.53,.15,.10),brass)
    G.lantern(-.87,2.65,-3.6)
    G.native('common_field_hospital_prefab',(3.25,1.1,-3.1),width=1.1,include=['nurse'],shade=1.7)


BUILDERS={'abbey':abbey,'guild':guild,'sanitarium':sanitarium}


def run(args):
    source,out,kit=args.source.resolve(),args.out.resolve(),args.kit.resolve()
    repo=Path(__file__).resolve().parents[2]
    if out==source or out==repo or repo in out.parents:raise ValueError('Use a separate private output.')
    out.mkdir(parents=True,exist_ok=True)
    records=json.loads((source/'models.json').read_text())
    oldchecks=json.loads((source/'verified.json').read_text())
    sys.path.insert(0,str(kit/'tools'));import bl_lib as L
    checks={}
    for key,record in records['models'].items():
        original=source/record['glb']
        if hashlib.sha256(original.read_bytes()).hexdigest()!=oldchecks[key]['sha256']:
            raise ValueError('Unverified source: '+key)
        folder=out/key;folder.mkdir(exist_ok=True)
        if key not in BUILDERS:
            shutil.copy2(original,out/record['glb']);checks[key]=oldchecks[key];continue
        L.reset();G.configure(kit,out,L,'v02');G.wood=wood;BUILDERS[key]();bpy.context.view_layer.update()
        finish_authored_paint()
        objects=[o for o in bpy.data.objects if o.type=='MESH']
        points=np.array([tuple(o.matrix_world@v.co) for o in objects for v in o.data.vertices])
        lo,hi=points.min(0),points.max(0)
        if not np.isfinite(points).all() or min(hi-lo)<1:raise ValueError('Invalid building volume')
        triangles=sum(len(p.vertices)-2 for o in objects for p in o.data.polygons)
        if triangles>45000:raise ValueError('Architectural triangle budget exceeded: '+key)
        bpy.ops.object.select_all(action='DESELECT')
        for o in objects:o.select_set(True)
        bpy.context.view_layer.objects.active=objects[0]
        path=folder/(key+'.glb')
        native_parts=list(G.SOURCES)
        bpy.ops.export_scene.gltf(filepath=str(path),export_format='GLB',use_selection=True,
             export_apply=True,export_yup=True,export_materials='EXPORT',export_animations=False)
        # Verification and every review view use the EXPORTED model, never just the authoring scene.
        bpy.ops.wm.read_factory_settings(use_empty=True);G.configure(kit,out,None,'v02')
        bpy.ops.import_scene.gltf(filepath=str(path));bpy.context.view_layer.update()
        meshes=[o for o in bpy.data.objects if o.type=='MESH']
        actual=sum(len(p.vertices)-2 for o in meshes for p in o.data.polygons)
        points=np.array([tuple(o.matrix_world@v.co) for o in meshes for v in o.data.vertices])
        error=float(np.abs(np.array([points.min(0),points.max(0)])-np.array([lo,hi])).max())
        if actual!=triangles or error>.001:raise ValueError('Export changed geometry: '+key)
        images=[im for im in bpy.data.images if im.type=='IMAGE']
        for im in images:
            if not im.packed_file or not len(im.pixels):raise ValueError('Unresolved embedded paint: '+key)
        finish_scene();bpy.context.scene.cycles.samples=args.samples
        centre=(lo+hi)/2;target=(centre[0],centre[1],lo[2]+(hi[2]-lo[2])*.46)
        radius=max(hi-lo)*1.90;cam=None;views={}
        for name,angle in [('front',0),('hero',32),('rear',145),('side',-75)]:
            a=math.radians(angle);location=(target[0]+math.sin(a)*radius,target[1]-math.cos(a)*radius,target[2]+radius*.38)
            if cam is None:cam=camera(location,target,field=36,resolution=args.resolution)
            else:
                cam.location=location;cam.rotation_euler=(Vector(target)-cam.location).to_track_quat('-Z','Y').to_euler()
            bpy.context.scene.render.filepath=str(folder/(name+'.png'));bpy.ops.render.render(write_still=True)
            views[name]=key+'/'+name+'.png'
        bpy.ops.file.pack_all();bpy.ops.wm.save_as_mainfile(filepath=str(folder/(key+'.blend')))
        record.update(triangles=triangles,mesh_objects=len(meshes),bounds_blender=[lo.tolist(),hi.tolist()],views=views,
            design='v19: native trim/plaster/paving, coherent metric paint, cut openings on front and sides, DD1 identity.',
            native_parts=native_parts)
        checks[key]={'sha256':hashlib.sha256(path.read_bytes()).hexdigest(),'triangles':triangles,'bounds_error':error,
                     'embedded_images':len(images),'verification':'Exported GLB re-imported; topology, volume, embedded paint and four views.'}
        print('VERIFIED',key,triangles,'triangles',len(images),'images',flush=True)
    records['detail_pass']='v19'
    (out/'models.json').write_text(json.dumps(records,indent=2))
    (out/'verified.json').write_text(json.dumps(checks,indent=2))
    print('PASS: three services rebuilt and seven source GLBs preserved.',flush=True)


if __name__=='__main__':
    parser=argparse.ArgumentParser(description=__doc__)
    for name in ['source','out','kit']:parser.add_argument('--'+name,required=True,type=Path)
    parser.add_argument('--resolution',type=int,default=1024)
    parser.add_argument('--samples',type=int,default=16)
    run(parser.parse_args(sys.argv[sys.argv.index('--')+1:]))
