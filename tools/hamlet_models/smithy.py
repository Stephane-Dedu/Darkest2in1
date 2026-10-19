"""Rebuild the smithy's roof and enclosed workshop around its verified v21 forge bay.

One continuous roof replaces the intersecting cottage/canopy. Native camp props remain.
Run in Blender with reviewed private thatch_v22/plaster_v22 paint and v21 source models.
"""
import argparse
import math
import sys
from pathlib import Path

import bpy
import numpy as np
from mathutils import Matrix

sys.path.insert(0,str(Path(__file__).resolve().parent))
import geometry as G
import architecture as A
import workshops as W


def plaster():
    return G.material('Smithy soot stained lime v22',(.73,.72,.69,1),'plaster_v22',.45)


def brick():
    return G.material('Smithy warm firebrick v22',(.72,.72,.73,1),'brick_v21',.4)


def world_paint(ob,mat,period=4.2):
    ob.data.materials.clear();ob.data.materials.append(mat)
    bpy.context.view_layer.update();uv=ob.data.uv_layers.active
    for face in ob.data.polygons:
        face.material_index=0
        normal=ob.matrix_world.to_3x3()@face.normal
        axes=(0,2) if abs(normal.y)>abs(normal.x) else (1,2)
        if abs(normal.z)>.7:axes=(0,1)
        for li in face.loop_indices:
            v=ob.matrix_world@ob.data.vertices[ob.data.loops[li].vertex_index].co
            uv.data[li].uv=(v[axes[0]]/period,v[axes[1]]/period)
    return ob


def box(name,centre,size,mat=None):
    return world_paint(G.box(name,centre,size,mat or plaster(),.025),mat or plaster())


def casement(name,wall,x,y,z,width,height):
    A.subtract(wall,G.box(name+' actual aperture',(x,y+height/2,z+.13),(width,height,.6),G.ink()))
    glass=G.material('Smithy small amber glass',(.65,.25,.045,1),'window_grain',1.5)
    pane=G.mesh(name+' recessed glass',[(x-width/2,y,z+.26),(x+width/2,y,z+.26),
             (x+width/2,y+height,z+.26),(x-width/2,y+height,z+.26)],[[0,1,2,3]],glass,
             [[(0,0),(1,0),(1,1),(0,1)]])
    for xx in [x-width/2-.06,x+width/2+.06]:W.timber(name+' jamb',(xx,y-.08,z-.04),(xx,y+height+.08,z-.04),.12)
    for yy in [y-.06,y+height+.06]:W.timber(name+' sill',(x-width/2-.1,yy,z-.04),(x+width/2+.1,yy,z-.04),.12)
    for xx in [x-width/6,x+width/6]:G.beam(name+' iron mullion',(xx,y,z+.21),(xx,y+height,z+.21),.035,G.ink())
    G.beam(name+' iron transom',(x-width/2,y+height*.5,z+.21),(x+width/2,y+height*.5,z+.21),.035,G.ink())


def door(wall):
    x,y,z=-2.45,.32,-.88;w,h=1.40,2.52
    A.subtract(wall,G.box('Smithy doorway cutter',(x,y+h/2,z+.12),(w,h,.64),G.ink()))
    box('Smithy deep doorway',(x,y+h/2,z+.35),(w,h,.04),G.ink())
    for i in range(6):
        xx=x-w/2+(i+.5)*w/6
        W.paint(G.box('Smithy fitted door board',(xx,y+h/2,z+.27),(w/6-.018,h-.07,.08),W.wood(),.015),
                W.wood(),(.03,.13+(i%4)*.18,.97,.23+(i%4)*.18),True)
    for xx in [x-w/2-.07,x+w/2+.07]:W.timber('Smithy heavy door jamb',(xx,y,z-.03),(xx,y+h+.12,z-.03),.15)
    W.timber('Smithy low door lintel',(x-w/2-.15,y+h+.10,z-.03),(x+w/2+.15,y+h+.10,z-.03),.18)
    for yy in [.82,2.22]:W.metal_box('Smithy door hinge',(x,yy,z+.18),(1.28,.08,.045))
    W.metal_box('Smithy latch',(x+.42,1.57,z+.11),(.09,.23,.06),True)


def roof():
    mat=G.material('Smithy dense weathered reed v22',(.79,.78,.73,1),'thatch_v22',.45)
    # X runs along the ridge. Y-up/Z-depth profiles form one watertight thick roof.
    profile=[(-3.35,3.65),(-2.75,3.76),(-1.70,4.05),(-.55,4.95),(.90,6.18),
             (1.72,5.20),(2.75,4.02),(3.52,3.53),(3.90,3.43)]
    xs=np.linspace(-4.65,3.75,25)
    def at(i,j):
        x=float(xs[i]);z,y=profile[j]
        sag=.12*(1-((x+.45)/4.2)**2)
        ripple=.028*math.sin(i*1.73+j*.8)
        if j in [0,len(profile)-1]:ripple+=.045*math.sin(i*2.8)
        return x,y-sag+ripple,z
    lengths=[0]
    for (z,y),(zz,yy) in zip(profile,profile[1:]):lengths.append(lengths[-1]+math.hypot(zz-z,yy-y))
    verts=[at(i,j) for i in range(len(xs)) for j in range(len(profile))];faces=[];uvs=[];n=len(profile)
    for i in range(len(xs)-1):
        for j in range(n-1):
            k=i*n+j;faces.append([k,k+1,k+n+1,k+n])
            a,b=lengths[j],lengths[j+1]
            if j>=4:a,b=lengths[-1]-a,lengths[-1]-b
            uvs.append([(xs[i]/7.5,a/5.5),(xs[i]/7.5,b/5.5),(xs[i+1]/7.5,b/5.5),(xs[i+1]/7.5,a/5.5)])
    ob=G.mesh('Smithy continuous thatched roof',verts,faces,mat,uvs)
    bpy.context.view_layer.objects.active=ob;mod=ob.modifiers.new('Packed reed thickness','SOLIDIFY');mod.thickness=.19
    bpy.ops.object.modifier_apply(modifier=mod.name)
    # The tall chimney penetrates a real roof opening, with dark metal flashing.
    A.subtract(ob,G.box('Main chimney roof penetration',(1.55,6,0),(1.04,7,.92),G.ink()))
    A.subtract(ob,G.box('Rear chimney roof penetration',(-2.3,5,2.3),(.73,6,.63),G.ink()))
    for i in [0,len(xs)-1]:
        for j in range(n-1):
            a,b=at(i,j),at(i,j+1)
            W.timber('Smithy roof verge binding',(a[0],a[1]-.16,a[2]),(b[0],b[1]-.16,b[2]),.10)
    for j in [0,n-1]:
        for i in range(len(xs)-1):
            a,b=at(i,j),at(i+1,j)
            # Short uneven straw edge strips supply silhouette detail without alpha cards.
            G.mesh('Smithy ragged reed eave',[(a[0],a[1],a[2]),(b[0],b[1],b[2]),
                (b[0],b[1]-.19-.04*math.sin(i),b[2]+.015),(a[0],a[1]-.18,a[2]+.015)],[[0,1,2,3]],mat,
                [[(xs[i]/7.5,.05),(xs[i+1]/7.5,.05),(xs[i+1]/7.5,0),(xs[i]/7.5,0)]])
    for x in np.linspace(-4.6,3.7,17):
        # Sparse ridge spars bind the thatch rather than using a row of slate caps.
        W.timber('Smithy ridge spar',(x,5.88,.60),(x+.13,6.17,.91),.04)
        W.timber('Smithy ridge spar',(x+.13,6.17,.91),(x+.26,5.83,1.19),.04)
    for x in [-4.22,3.32]:
        outline=[(-.85,3.35),(-.55,4.70),(.9,5.96),(1.72,4.97),(2.75,3.78),(3.36,3.34)]
        pts=[(x,y,z) for z,y in outline];gable=G.mesh('Smithy closed side gable',pts,[list(range(len(pts)))],plaster())
        world_paint(gable,plaster())
        W.timber('Smithy gable tie',(x,3.36,-.85),(x,3.36,3.36),.17)
        W.timber('Smithy gable king post',(x,3.36,.9),(x,5.96,.9),.16)
        for zz in [-.65,3.15]:W.timber('Smithy gable brace',(x,3.45,zz),(x,4.82,.9),.13)
    return ob


def dormer(roof_ob):
    x,y,z=-1.72,4.10,-1.73
    cheek=box('Smithy dormer cheeks',(x,y+.62,z+.43),(1.24,1.24,.95))
    # Genuine opening in main roof below the little front gable, no native dormer sticking through it.
    A.subtract(roof_ob,G.box('Dormer roof opening',(x,4.75,-1.25),(1.20,1.85,1.13),G.ink()))
    casement('Smithy dormer casement',cheek,x,y+.17,z-.055,.70,.84)
    gable=A.prism('Smithy dormer front gable',[(x-.62,y+1.24),(x+.62,y+1.24),(x,y+2.12)],z-.05,z+.9,plaster())
    world_paint(gable,plaster())
    mat=G.material('Smithy dormer reed v22',(.79,.78,.73,1),'thatch_v22',.45)
    verts=[(x-.78,y+1.20,z-.22),(x,y+2.24,z-.22),(x+.78,y+1.20,z-.22),
           (x-.78,y+1.22,z+1.05),(x,y+2.21,z+1.05),(x+.78,y+1.22,z+1.05)]
    ob=G.mesh('Smithy dormer fitted roof',verts,[[0,1,4,3],[1,2,5,4]],mat,
              [[(0,0),(0,.45),(.5,.45),(.5,0)],[(0,.45),(0,0),(.5,0),(.5,.45)]])
    bpy.context.view_layer.objects.active=ob;mod=ob.modifiers.new('Dormer reed thickness','SOLIDIFY');mod.thickness=.16;bpy.ops.object.modifier_apply(modifier=mod.name)
    for a,b in [(verts[0],verts[1]),(verts[1],verts[2])]:W.timber('Smithy dormer carved verge',a,b,.10)
    for xx in [x-.64,x+.64]:W.timber('Smithy dormer posts',(xx,y,z-.08),(xx,y+1.25,z-.08),.12)


def rebuild():
    W.remove('farm_cottage / farm_cottage','Forge repaired slate canopy','Forge eave beam','Forge verge timber',
             'Forge shelter post','Forge knee brace','Stacked board','DD1 anvil sign','Forge sign arm','Banner rod')
    # The old rear chimney ended almost flush with the rebuilt ridge.
    for ob in bpy.data.objects:
        if ob.type=='MESH' and ob.name.startswith(('Tapered masonry chimney.001','Chimney cap.001','Soot opening.001')):
            for v in ob.data.vertices:
                p=ob.matrix_world@v.co;p.z+=.95
                v.co=ob.matrix_world.inverted()@p
    W.timber('Smithy hanging sign arm',(-3.65,3.57,-3.25),(-4.35,3.57,-3.78),.10)
    G.banner('Smithy painted anvil sign',-3.82,2.68,-3.81,.92,.72,'forge_sign')
    # Preserve workshop props, hearth, anvil, sign and two chimney positions from v21.
    shell=box('Smithy solid workshop',(-.45,1.91,1.25),(7.5,3.22,4.20))
    door(shell)
    for xx in [-4.23,-.9,3.33]:
        for zz in [-.88,3.38]:W.timber('Smithy wall upright',(xx,.30,zz),(xx,3.52,zz),.20)
    for yy in [.62,3.48]:
        for zz in [-.90,3.40]:W.timber('Smithy wall tie',(-4.28,yy,zz),(3.38,yy,zz),.20)
        for xx in [-4.25,3.35]:W.timber('Smithy side wall tie',(xx,yy,-.90),(xx,yy,3.4),.20)
    for xx in [-4.25,3.35]:
        for zz in [.4,2.0]:W.timber('Smithy side posts',(xx,.3,zz),(xx,3.5,zz),.16)
        for a,b in [((xx,.7,-.8),(xx,2.65,.4)),((xx,.7,3.3),(xx,2.65,2.0))]:W.timber('Smithy diagonal brace',a,b,.15)
    for xx in [-3.65,2.95]:
        W.timber('Smithy open bay post',(xx,.30,-3.18),(xx,3.49,-3.18),.23)
        W.timber('Smithy bay knee',(xx,2.70,-3.18),(xx+(.65 if xx<0 else -.65),3.46,-3.18),.16)
    W.timber('Smithy bay beam',(-3.8,3.43,-3.18),(3.1,3.43,-3.18),.23)
    for zz in [1.2]:
        for xx in [-4.25,3.35]:
            # Side window in the actual shell, oriented around the building vertical axis.
            before=set(bpy.data.objects);old=shell.matrix_world.copy()
            rotation=Matrix.Rotation(-math.pi/2 if xx>0 else math.pi/2,4,'Z')
            shell.matrix_world=rotation@old;bpy.context.view_layer.update()
            side=xx>0;casement('Smithy side amber casement',shell,zz if side else -zz,1.65,-abs(xx),.85,.78)
            shell.matrix_world=old
            for ob in set(bpy.data.objects)-before:ob.matrix_world=rotation.inverted()@ob.matrix_world
    world_paint(shell,plaster())
    roof_ob=roof();dormer(roof_ob)
    # Raked planks and a closed iron tyre rack remain readable workshop cues.
    for i in range(3):W.timber('Smithy spare board',(3.20+i*.17,.36,-2.6),(3.80+i*.17,2.17,-.75),.12)
    fire=G.material('Smithy furnace painted fire',(1.,.85,.65,1),'hearth_v22',1.8)
    G.mesh('Smithy warm hearth interior',[(1.03,.76,-1.48),(2.07,.76,-1.48),(2.04,1.24,-1.46),
         (1.83,1.56,-1.44),(1.3,1.54,-1.44),(1.04,1.20,-1.46)],[[0,1,2,3,4,5]],fire,
         [[(0,0),(1,0),(1,.48),(.8,1),(.2,1),(0,.48)]])
    # Static painted fire sits behind the separate glowing coal geometry.
    W.remove('Forge angular fire tongue')
    coal=G.material('Smithy glowing charcoal',(1.,.85,.65,1),'hearth_v22',1.5)
    for ob in bpy.data.objects:
        if ob.type=='MESH' and ob.name.startswith('Forge hot coal'):
            W.paint(ob,coal,(.15,.08,.85,.30))


if __name__=='__main__':
    parser=argparse.ArgumentParser(description=__doc__)
    for name in ['source','out','kit']:parser.add_argument('--'+name,type=Path,required=True)
    parser.add_argument('--resolution',type=int,default=1100);parser.add_argument('--samples',type=int,default=16)
    args=parser.parse_args(sys.argv[sys.argv.index('--')+1:])
    W.run(args,refiners={'blacksmith':rebuild},detail='v22',replaced_native=('farm_cottage / farm_cottage',),
          design='v22: continuous thick thatch, enclosed smithy, fitted dormer and open working bay')
