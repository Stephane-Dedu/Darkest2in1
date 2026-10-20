"""Fit architectural stonework to the verified Abbey and Sanitarium.

Blender entry point. Uses the reviewed private v20 stone/plaster paintings and native
glass; no new bitmap generation. Native meshes and their UVs remain unchanged.
"""
import argparse
import math
import sys
from pathlib import Path

import bpy
import numpy as np
from mathutils import Matrix, Vector

sys.path.insert(0,str(Path(__file__).resolve().parent))
import geometry as G
import architecture as A
import workshops as W

# Interiors of distinct, detailed stones from the reviewed 1254px Abbey painting.
STONES=((128,84,485,182),(268,243,586,326),(649,409,926,507),(428,1046,690,1149))


def limestone(dark=False):
    return G.material('Fitted limestone shadow v25' if dark else 'Fitted weathered limestone v25',
                      (.40,.43,.44,1) if dark else (.80,.84,.85,1),'abbey_wall',.4)


def dressed(ob,seed=0,dark=False):
    x0,y0,x1,y1=STONES[seed%len(STONES)]
    return W.paint(ob,limestone(dark),(x0/1254,1-y1/1254,x1/1254,1-y0/1254))


def block(name,center,size,seed=0,dark=False):
    return dressed(G.box(name,center,size,limestone(dark),.018),seed,dark)


def ring(name,x,y,z,width,height,thickness=.18):
    """Split jambs into courses; pointed head uses individual voussoirs."""
    inner=A.pointed(x,y,width,height)
    outer=A.pointed(x,y-thickness,width+2*thickness,height+thickness*2)
    counter=0
    for i in range(1,len(inner)):
        j=(i+1)%len(inner)
        count=max(1,math.ceil(math.dist(inner[i],inner[j])/.39))
        for k in range(count):
            a,b=(k+.018)/count,(k+.982)/count
            mix=lambda p,q,t:tuple(np.array(p)*(1-t)+np.array(q)*t)
            shape=[mix(outer[i],outer[j],a),mix(outer[i],outer[j],b),
                   mix(inner[i],inner[j],b),mix(inner[i],inner[j],a)]
            ob=A.prism(name+' fitted arch course',shape,z-.135,z+.055,limestone())
            dressed(ob,counter)
            counter+=1
    # The projected outer moulding sheds water and leaves a physical shadow line.
    for i in range(2,9):
        a,b=outer[i],outer[i+1]
        dressed(G.beam(name+' hood mould',(a[0],a[1]+.035,z-.17),
                       (b[0],b[1]+.035,z-.17),.075,limestone()),i)
    block(name+' deep dressed sill',(x,y-.11,z+.045),(width+.48,.17,.48),2)
    block(name+' sill drip edge',(x,y-.16,z-.20),(width+.55,.07,.10),1,True)


def remove_frames(prefix):
    W.remove(prefix+' dressed arch stone',prefix+' deep sill')


def buttress(x,z,top,side=False):
    # Genuine stepped ashlar, staggered joints and sloping weathering caps.
    bottom=1.26
    heights=[.43,.37,.45,.40,.38,.42,.37,.40,.36,.39,.38]
    y=bottom;i=0
    while y<top-.12:
        h=min(heights[i%len(heights)],top-.12-y)
        if h<.035:break
        t=(y-bottom)/(top-bottom)
        width=.67-.31*t;depth=.89-.45*t
        split=i%3==0 and t<.6
        for part in range(2 if split else 1):
            w=width/2-.009 if split else width
            xx=x+(-width/4 if part==0 else width/4) if split else x
            size=(depth,h-.014,w) if side else (w,h-.014,depth)
            centre=(x,y+h/2,z+xx-x) if side else (xx,y+h/2,z)
            block('Abbey coursed buttress',centre,size,i+part)
        if i in [2,6]:
            # Raised rear, lowered projecting nose; bevels are real silhouette facets.
            w,d=width+.13,depth+.15
            pts=[(-w/2,-d/2,0),(w/2,-d/2,0),(w/2,d/2,0),(-w/2,d/2,0),
                 (-w/2,-d/2,.085),(w/2,-d/2,.085),(w/2,d/2,.19),(-w/2,d/2,.19)]
            if side:
                sign=-1 if x>0 else 1
                pts=[(x+sign*zz,y+h+yy,z-sign*xx) for xx,zz,yy in pts]
            else:
                pts=[(x+xx,y+h+yy,z+zz) for xx,zz,yy in pts]
            ob=G.mesh('Abbey sloping buttress weathering',pts,
                      [[0,3,2,1],[4,5,6,7],[0,1,5,4],[1,2,6,5],[2,3,7,6],[3,0,4,7]],limestone())
            dressed(ob,i)
        y+=h;i+=1


def rear_opening(name,wall,x,y,z,width,height,green=False):
    before=set(bpy.data.objects);original=wall.matrix_world.copy()
    rotation=Matrix.Rotation(math.pi,4,'Z')
    wall.matrix_world=rotation@original;bpy.context.view_layer.update()
    A.opening(name,wall,-x,y,-z,width,height,True,green,.32)
    remove_frames(name)
    ring(name,-x,y,-z,width,height,.12)
    wall.matrix_world=original
    for ob in set(bpy.data.objects)-before:
        ob.matrix_world=rotation.inverted()@ob.matrix_world
    bpy.context.view_layer.update()


def abbey():
    for prefix in ['Abbey great portal','Abbey nave lancet','Abbey side lancet']:
        remove_frames(prefix)
    ring('Abbey portal',-2.55,1.32,-4.42,1.65,2.9,.23)
    for x in [-.3,1.35,3.0]:ring('Abbey lancet',x,2.05,-2.47,.75,2.45,.15)
    for z in [-1.,1.6]:
        before=set(bpy.data.objects)
        ring('Abbey side lancet',z,2.05,-4.03,.74,2.45,.15)
        for ob in set(bpy.data.objects)-before:
            ob.matrix_world=Matrix.Rotation(math.pi/2,4,'Z')@ob.matrix_world
    W.remove('Abbey facade stepped pier','Abbey facade weathered coping',
             'Abbey side stepped pier','Abbey side weathered coping')
    for x in [-1.12,.53,2.18,3.90]:buttress(x,-2.70,5.55)
    for x in [-3.9,3.9]:
        for z in [.4,2.95]:buttress(x,z,5.25,True)
    nave=bpy.data.objects['Abbey long nave']
    for x in [-2.25,.05,2.35]:
        rear_opening('Abbey rear lancet',nave,x,2.1,3.46,.62,2.15)
    # Small projecting corbels under the eaves, avoiding the lancet heads.
    for i,x in enumerate(np.linspace(-3.65,3.65,17)):
        block('Abbey eave corbel',(x,5.18,-2.53),(.15,.30,.26),i,True)
        block('Abbey eave corbel cap',(x,5.34,-2.57),(.23,.09,.33),i)
    # Two-layer front cornice stays below the roof and clear of its painted slate.
    for i in range(12):
        x=-3.86+i*.67
        block('Abbey eave drip course',(x,5.42,-2.52),(.65,.12,.28),i)
    # Keep the rose's physical tracery; replace the old stretched pale band with
    # a complete painted stone surface on each ring segment.
    for i,ob in enumerate(list(bpy.data.objects)):
        if ob.name.startswith(('Rose limestone circle','Abbey facade pinnacle base',
                               'Abbey side pinnacle base')):
            dressed(ob,i)


def world_plaster(ob,period_u=5.1,period_v=4.7,shift=(.16,.07)):
    mat=G.material('Sanitarium fitted lime v25',(.84,.87,.82,1),'sanitarium_wall',.42)
    ob.data.materials.clear();ob.data.materials.append(mat)
    uv=ob.data.uv_layers.active
    for face in ob.data.polygons:
        face.material_index=0
        n=ob.matrix_world.to_3x3()@face.normal
        axes=(0,2) if abs(n.y)>=abs(n.x) else (1,2)
        if abs(n.z)>.7:axes=(0,1)
        for li in face.loop_indices:
            v=ob.matrix_world@ob.data.vertices[ob.data.loops[li].vertex_index].co
            uv.data[li].uv=(v[axes[0]]/period_u+shift[0],v[axes[1]]/period_v+shift[1])


def sanitarium():
    house=bpy.data.objects['Sanitarium house stone undercroft']
    world_plaster(house)
    # Recesses give the blank clinical basement a readable architectural purpose.
    for x in [-1.65,2.95]:
        A.opening('Sanitarium cellar lancet',house,x,1.35,-1.52,.62,.79,True,True,.30)
        remove_frames('Sanitarium cellar lancet')
        ring('Sanitarium cellar lancet',x,1.35,-1.52,.62,.79,.105)
    for x in [-1.25,1.0,3.25]:
        rear_opening('Sanitarium rear cellar',house,x,1.35,2.91,.54,.76,True)
    remove_frames('Sanitarium deep door')
    ring('Sanitarium portal',.45,1.14,-3.54,1.45,2.35,.18)
    # Remap the circular tower in world cylindrical coordinates after the glTF
    # conversion. One complete vertical painting avoids recurring plaster bands.
    tower=bpy.data.objects['Sanitarium round stair tower']
    material=G.material('Sanitarium tower uninterrupted lime v25',(.82,.87,.82,1),'sanitarium_wall',.42)
    tower.data.materials.clear();tower.data.materials.append(material)
    uv=tower.data.uv_layers.active
    for face in tower.data.polygons:
        face.material_index=0
        coords=[tower.matrix_world@tower.data.vertices[tower.data.loops[i].vertex_index].co for i in face.loop_indices]
        us=[(math.atan2(v.y+2,v.x+3)/math.tau)%1 for v in coords]
        if max(us)-min(us)>.5:us=[u+1 if u<.5 else u for u in us]
        for li,v,u in zip(face.loop_indices,coords,us):
            uv.data[li].uv=(u*1.35+.12,(v.z-1.1)/4.6+.04)
    # Masonry under the porch and a sloping cap separate roof runoff from plaster.
    porch=bpy.data.objects['Sanitarium stone porch'];world_plaster(porch,4.7,4.8,(.29,.04))
    for i,ob in enumerate(list(bpy.data.objects)):
        if ob.name.startswith('Sanitarium tower course'):dressed(ob,i)
    # Narrow corner quoins and recessed joint shadows, distinct from the main wall.
    for x in [-2.21,4.21]:
        for i in range(4):
            block('Sanitarium undercroft quoin',(x,1.28+i*.32,-1.51),
                  (.29 if i%2 else .43,.30,.16),i)


if __name__=='__main__':
    parser=argparse.ArgumentParser(description=__doc__)
    for name in ['source','out','kit']:parser.add_argument('--'+name,type=Path,required=True)
    parser.add_argument('--resolution',type=int,default=900)
    parser.add_argument('--samples',type=int,default=16)
    args=parser.parse_args(sys.argv[sys.argv.index('--')+1:])
    W.run(args,refiners={'abbey':abbey,'sanitarium':sanitarium},detail='v25',
          design='v25: fitted stone courses, sloping buttress caps, recessed cellar and continuous tower plaster')
