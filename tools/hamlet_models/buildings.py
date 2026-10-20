"""DD1 exterior silhouettes rebuilt as separate DD2-painted, three-dimensional buildings."""
import math
import bpy
from geometry import *


def abbey():
    platform(9, 11, 1.25)
    wall=material('Abbey dark dressed stone',(1.05,1.10,1.18,1),'stone',.32)
    box('Church nave',(0,3.35,.3),(6.4,4.2,8.4),wall,.12)
    gable('Church nave',0,5.5,.3,7.2,9.4,2.9,'roof_slate',(1.5,1.6,1.75,1),wall)
    # A DD2 Gothic spire supplies carved stone, bell-stage tracery and real roof topology.
    native('city_tower_02',(1.0,5.6,2.4),width=3.1,shade=1.6)
    box('Entry porch',(-1.7,2.65,-4.3),(3.0,2.8,1.9),wall,.06)
    gable('Entry porch',-1.7,4.05,-4.3,3.5,2.5,2.1,'roof_slate',(1.5,1.6,1.75,1),wall)
    arch('Abbey great door',-1.7,1.3,-5.30,1.9,2.65)
    for x in [.25,1.55,2.75]:
        arch('Abbey lancet',x,2.2,-3.93,.68,1.85,True)
    for z in [-2.6,0,2.6]:
        for x in [-3.3,3.3]:
            box('Nave buttress',(x,2.75,z),(.4,3.0,.7),wall,.04)
            cylinder('Buttress pinnacle',(x,5.25,z),.2,1.1,wall,4,0)
    for z in [-2.2,.4,3]:
        previous=set(bpy.data.objects)
        arch('Nave side lancet',0,2.2,0,.7,1.85,True)
        pieces=set(bpy.data.objects)-previous
        group=bpy.data.objects.new('Nave side window',None)
        bpy.context.collection.objects.link(group)
        group.location=p((3.24,0,z))
        group.rotation_euler.z=math.pi/2
        for ob in pieces:
            ob.parent=group
    # Rose window is a volume and tracery, not a whole-building image.
    face=material('Abbey amber rose glass',(.64,.31,.07,1),emission=1.6)
    bpy.ops.mesh.primitive_cylinder_add(vertices=12,radius=.43,depth=.11,location=p((-1.7,4.75,-5.57)),rotation=(math.pi/2,0,0))
    bpy.context.object.name='Rose window glass'; bpy.context.object.data.materials.append(face)
    for i in range(8):
        a=i*math.pi/4
        beam('Rose stone tracery',(-1.7,4.75,-5.65),(-1.7+math.cos(a)*.45,4.75+math.sin(a)*.45,-5.65),.065,ink())
    stairs(-1.7,0,-5.0,2.6,1.25,8,3.6)


def tavern():
    platform(11.6,7.6,.35)
    native('inn_lvl_03_prefab',(0,.35,0),width=10.8,rotation=0,shade=1.5)
    # DD1's high chimney and uneven silhouette distinguish the tavern from the guild hall.
    chimney(-3.5,.35,1.1,9.1,.65)
    native('camp_barrel',(-4.2,.35,-3),width=1.15)
    native('camp_barrel',(-3.4,.35,-3.5),width=.95)
    native('camp_crate',(3.8,.35,-3.0),width=1.0)
    lantern(-2.1,2.8,-3.7)
    lantern(1.5,2.8,-3.7)
    # A physical hanging sign, warm entrance and paved threshold.
    beam('Tavern sign bracket',(-2.6,3.2,-3.0),(-2.6,3.2,-4.1),.12,ink())
    box('Tavern brass sign',(-2.6,2.8,-4.0),(.6,.65,.12),material('Tavern brass',(.43,.27,.09,1),emission=.6),.08)
    stairs(0,.05,-3.5,2.1,.4,3,1.2)


def sanitarium():
    platform(9,8.5,1.1)
    wall=material('Sanitarium cold plaster',(1.4,1.45,1.35,1),'plaster',.5)
    box('Sanitarium main stone floor',(.9,2.65,.6),(5.8,3.1,5.9),stone(),.08)
    box('Sanitarium overhanging upper floor',(.9,5.25,.6),(6.2,2.1,6.2),wall,.07)
    timber_frame(.9,4.2,.6,6.2,2.1,6.2)
    gable('Sanitarium tall gable',.9,6.3,.6,7,6.9,3.2,'roof_slate',(1.55,1.65,1.58,1),wall)
    # DD1 has a round stone turret, timbered upper stage and pointed slate cap.
    tower=cylinder('Round sanitarium stair tower',(-3.0,4.0,-2.2),1.45,5.8,stone(),12)
    upper=cylinder('Turret timber stage',(-3.0,7.0,-2.2),1.55,1.6,wood(),8)
    roof=cylinder('Turret pointed roof',(-3.0,8.9,-2.2),1.85,2.2,material('Turret slate',(1.6,1.7,1.65,1),'roof_slate',.45),8,0)
    for ob,radius,height in [(tower,1.45,5.8),(upper,1.55,1.6),(roof,1.85,2.2)]:
        for loop in ob.data.uv_layers.active.data:
            loop.uv.x*=math.pi*radius
            loop.uv.y*=height/2
    for a in range(8):
        r=a*math.pi/4
        beam('Turret rib',(-3.0+1.56*math.cos(r),6.2,-2.2+1.56*math.sin(r)),(-3.0+1.56*math.cos(r),7.8,-2.2+1.56*math.sin(r)),.13,ink())
    arch('Sanitarium door',.9,1.1,-2.47,1.65,2.65)
    green=(.12,.38,.29,1)
    for x in [-.6,1.0,2.6]:
        arch('Sanitarium green glass',x,4.65,-2.56,.67,1.28,True,green)
    arch('Sanitarium gable glass',.9,6.65,-2.92,1.25,1.65,True,green)
    stairs(.9,0,-2.4,2.4,1.1,7,3.2)
    lantern(-.5,2.7,-2.8)
    chimney(2.8,6.3,2.2,2.3,.6)


def guild():
    platform(8.8,7,.4)
    wall=material('Guild limestone',(1.42,1.35,1.27,1),'stone',.45)
    box('Tall guild hall',(-.65,4.1,.3),(5.4,7.4,5.6),wall,.08)
    gable('Guild steep red roof',-.65,7.8,.3,6.1,6.3,2.9,'roof_red',(1.55,1.3,1.22,1),wall)
    box('Guild side wing',(3,2.55,.65),(2.1,4.3,4.6),wall,.07)
    gable('Guild side wing',3,4.7,.65,2.6,5.1,1.4,'roof_slate',(1.6,1.4,1.45,1),wall)
    arch('Guild armoured door',-.65,.4,-2.56,1.65,2.4)
    for x in [-2.7,-.65,1.4]:
        arch('Guild arrow slit',x,6.35,-2.56,.37,.85,True)
    for x in [-2.3,1.0]:
        banner('DD1 sword standard',x,2.8,-2.63,.90,3.2)
    # Crossed steel blades and a shield occupy real space in front of the stone wall.
    steel=material('Worn sword steel',(.3,.29,.25,1),emission=.4)
    for sign in [-1,1]:
        beam('Guild crossed sword',(-.65-sign*.6,3.3,-2.72),(-.65+sign*.6,4.5,-2.72),.10,steel)
        beam('Sword crossguard',(-.65-sign*.7,3.6,-2.76),(-.65-sign*.35,3.25,-2.76),.10,steel)
    box('Guild shield',(-.65,3.8,-2.85),(.55,.68,.15),material('Guild shield ochre',(.35,.22,.08,1),emission=.55),.08)
    for z in [-2.7,3.4]:
        for i in range(10):
            x=-3.5+i*.64
            beam('Roof crest iron',(x,7.85,z),(x,8.42,z),.055,ink())
        beam('Roof crest rail',(-3.5,8.15,z),(2.3,8.15,z),.055,ink())
    native('camp_axe',(-3.5,.4,-2.5),width=.7)
    lantern(2.8,2.6,-1.75)


def anvil(x,y,z):
    metal=material('Hammered anvil',(.17,.17,.16,1),emission=.45)
    box('Anvil foot',(x,y+.12,z),(1.15,.24,.7),metal,.05)
    box('Anvil waist',(x,y+.45,z),(.5,.55,.5),metal,.06)
    box('Anvil face',(x,y+.78,z),(1.4,.22,.6),metal,.045)
    mesh('Anvil tapered horn',[(x+.7,y+.68,z-.27),(x+.7,y+.9,z-.27),(x+.7,y+.9,z+.27),(x+.7,y+.68,z+.27),(x+1.2,y+.81,z)],[[0,1,4],[1,2,4],[2,3,4],[3,0,4]],metal)


def blacksmith():
    platform(10,8,.3)
    native('farm_cottage',(-.7,.3,1.1),width=8.1,shade=1.55)
    # The open workshop is modeled separately from the closed native cottage.
    for x in [-3,2.0]:
        for z in [-3.2,-.9]:
            beam('Forge shelter post',(x,.3,z),(x,3.45,z),.24)
    roof=material('DD2 forge thatch',(1.15,1.05,.88,1),'thatch',.45)
    mesh('Open forge lean-to',[(-3.4,3.25,-3.7),(2.4,3.25,-3.7),(2.4,4.5,-.6),(-3.4,4.5,-.6)],[[0,1,2,3]],roof,
         [[(0,0),(2,0),(2,1),(0,1)]])
    for x in [-3.4,2.4]:
        beam('Awning verge',(x,3.25,-3.7),(x,4.5,-.6),.17,ink())
    chimney(1.55,.3,.0,8.4,.95)
    chimney(-2.3,2.5,2.3,3.2,.65)
    box('Forge stone hood',(1.55,1.6,-1.15),(1.65,2.6,1.3),stone(),.1)
    arch('Orange forge mouth',1.55,.6,-1.85,1.25,1.25,True,(.9,.22,.035,1))
    box('Forge hearth',(1.55,.45,-2.1),(1.8,.3,1.6),stone(),.08)
    cylinder('Anvil oak stump',(-.7,.65,-2.4),.52,.7,wood(),8)
    anvil(-.7,1.0,-2.4)
    beam('Forge sign arm',(-3.05,3.7,-2.6),(-4.0,3.7,-2.6),.13)
    banner('DD1 anvil sign',-3.65,2.7,-2.65,.95,.75,'forge_sign')
    native('camp_crate',(3.1,.3,-2.6),width=1.2)
    native('camp_barrel',(-3.7,.3,.4),width=1.1)
    wheel(3.45,1,-2.6,.85)
    beam('Stacked board',(2.6,.45,-3.3),(4,1.7,-.9),.18)
    beam('Stacked board',(2.9,.4,-3.5),(4.3,1.7,-1.1),.18)


def camping_trainer():
    # DD1's Survivalist is an open ridge tent, not a permanent house.
    canvas=material('Survivalist canvas',(.30,.18,.12,1),'thatch',.55)
    mesh('Full depth ridge tent',[(-3.5,.18,-2.8),(0,3.8,-2.8),(3.5,.18,-2.8),(-3.5,.18,2.8),(0,3.8,2.8),(3.5,.18,2.8)],
        [[0,1,4,3],[1,2,5,4],[3,4,5]],canvas,
        [[(0,0),(0,1),(1,1),(1,0)],[(0,1),(0,0),(1,0),(1,1)],[(0,0),(.5,1),(1,0)]])
    # Partially open flaps show the dark interior and bedroll.
    mesh('Left tent flap',[(-3.5,.18,-2.81),(0,3.8,-2.81),(-1.1,.18,-2.85)],[[0,1,2]],canvas, [[(0,0),(1,1),(1,0)]])
    mesh('Right tent flap',[(1.1,.18,-2.85),(0,3.8,-2.81),(3.5,.18,-2.81)],[[0,1,2]],canvas, [[(0,0),(0,1),(1,0)]])
    for z in [-2.8,2.8]:
        beam('Tent ridge pole',(0,.0,z),(0,4.0,z),.12)
    beam('Ridge beam',(0,3.85,-3.2),(0,3.85,3.0),.16)
    for x in [-3.5,3.5]:
        for z in [-2.8,2.8]:
            beam('Tent guy rope',(x,1.1,z),(x*1.2,.1,z*1.2),.035,material('Hemp cord',(.36,.29,.17,1),emission=.3))
    native('camp_firepit',(3.5,0,-3.3),width=1.8)
    native('camp_chest',(-3.3,0,-2.8),width=1.6)
    native('camp_barrel',(-3.9,0,-.9),width=1.05)
    native('camp_crate',(3.8,0,1.3),width=1.3)
    native('camp_axe',(2.8,0,-1.9),width=.7)
    cylinder('Rolled bedroll',(-.3,.3,-1.6),.28,1.8,material('Bedroll',(.27,.20,.14,1),emission=.4),10).rotation_euler.y=math.pi/2
    for i in range(3):
        cylinder('Camp flame',(3.35+i*.15,.55,-3.3),.22,.9,material('Campfire',(.85,.22,.025,1),emission=2),5,0)


def stage_coach():
    native('wagon_art_embark_skin_base',(0,.0,0),width=9.3,rotation=90,shade=1.9)
    native('camp_crate',(-3.7,0,-1.8),width=1.3)
    native('camp_chest',(2.9,0,-1.7),width=1.4)
    # Reception sign and waiting trunks retain DD1's carriage-stop identity.
    beam('Coach stop signpost',(-4.5,0,1.0),(-4.5,4.2,1.0),.15)
    box('Coach stop sign',(-4.0,3.7,.97),(1.1,.7,.14),material('Coach navy sign',(.045,.065,.075,1),emission=.5),.05)
    beam('Coach brass emblem',(-4.3,3.7,.87),(-3.8,3.7,.87),.10,material('Coach sign brass',(.48,.34,.13,1),emission=.8))


def nomad_wagon():
    plank=wood()
    purple=material('Nomad plum canvas',(.48,.24,.58,1),'canvas',.65)
    box('Caravan chassis',(0,1.4,0),(4.1,.4,6.5),plank,.08)
    box('Caravan living body',(0,2.65,0),(3.9,2.1,6.1),plank,.08)
    # Barrel roof has a genuine curved cross-section, independent of the camera.
    vertices,faces,uv=[],[],[]
    for zz in [-3.35,3.35]:
        for i in range(11):
            a=i*math.pi/10
            vertices.append((math.cos(a)*2.25,3.6+math.sin(a)*2.1,zz))
    for i in range(10):
        faces.append([i,i+1,i+12,i+11])
        uv.append([(i/10,0),((i+1)/10,0),((i+1)/10,2),(i/10,2)])
    mesh('Curved purple caravan roof',vertices,faces,purple,uv)
    for zz in [-3.35,3.35]:
        profile=[(math.cos(i*math.pi/10)*2.25,3.6+math.sin(i*math.pi/10)*2.1,zz) for i in range(11)]
        mesh('Caravan curved end wall',profile,[list(range(11))],plank)
        for a,b in zip(profile,profile[1:]):
            beam('Roof iron rib',a,b,.10,ink())
    for z in [-2.0,2.0]:
        for x in [-2.2,2.2]:
            wheel(x,1.0,z,1.0)
        beam('Caravan axle',(-2.4,1.0,z),(2.4,1.0,z),.18,ink())
    arch('Nomad rounded door',0,1.7,-3.43,1.5,2.5)
    arch('Nomad red glass',0,3.5,-3.51,.55,.6,True,(.48,.10,.09,1))
    stairs(0,0,-3.35,1.65,1.65,5,2.4,wood())
    # The shop awning slopes and sags; its eye crest identifies the DD1 caravan.
    mesh('Sagging market awning',[(2.0,3.8,-2.8),(2.0,3.65,0),(2.0,3.8,2.8),(4.8,3.1,-2.8),(4.8,2.9,0),(4.8,3.1,2.8)],
        [[0,1,4,3],[1,2,5,4]],purple, [[(0,0),(.5,0),(.5,1),(0,1)],[(.5,0),(1,0),(1,1),(.5,1)]])
    for z in [-2.8,2.8]:
        beam('Awning post',(4.8,0,z),(4.8,3.4,z),.13)
    for i in range(9):
        cylinder('Awning brass tassel',(4.8,2.88,-2.3+i*.58),.07,.20,material('Nomad brass',(.49,.30,.09,1),emission=.8),6,0)
    for x in [-.85,.85]:
        beam('Eye sign support',(x,5.3,-3.4),(x,6.55,-3.4),.10)
    banner('DD1 eye crest',0,5.7,-3.5,1.7,.9,'nomad_eye')
    native('camp_crate',(3.8,0,-1.5),width=1.1)
    native('camp_chest',(-2.9,0,-4.3),width=1.1)
    lantern(-1.8,3.4,-3.8)
    lantern(1.8,3.4,-3.8)


def graveyard():
    platform(9,7,1.4)
    stone_blocks('Graveyard retaining wall',0,0,-3.55,9,1.4,.5,4)
    soil=material('Graveyard earth',(.11,.105,.085,1),emission=.55)
    box('Burial earth',(0,1.43,0),(8.6,.08,6.5),soil)
    native('gravestone_group_01',(-2.1,1.48,-.8),width=3.2,shade=1.7,solidify=.14)
    native('gravestone_group_02',(1.9,1.48,-.7),width=3.1,shade=1.7,solidify=.14)
    native('gravestone_group_03',(.2,1.48,1.4),width=4.8,shade=1.7,solidify=.14)
    for x in [-4.2,4.2]:
        for z in [-3.2,3.2]:
            box('Graveyard gatepost',(x,2.0,z),(.55,1.1,.55),stone(),.06)
            cylinder('Gatepost pointed cap',(x,2.75,z),.43,.4,stone(),4,0)
    for i in range(18):
        x=-4.1+i*.48
        beam('Graveyard iron spear',(x,1.5,3.2),(x,3,3.2),.055,ink())
        cylinder('Fence spear tip',(x,3.1,3.2),.1,.25,ink(),4,0)
    for y in [1.9,2.6]:
        beam('Graveyard fence rail',(-4.2,y,3.2),(4.2,y,3.2),.055,ink())
    stairs(-2.7,0,-3.4,1.8,1.4,7,3.0)
    native('common_prop_lamppost',(3.7,1.4,-2.4),width=.8,shade=1.8)


def statue():
    # Keep the memorial service and stone base without the unwanted foreground figure.
    for i,(w,h) in enumerate([(5.2,.28),(4.5,.28),(3.8,.32)]):
        box('Memorial stone step',(0,sum([.28,.28,.32][:i])+h/2,0),(w,h,w*.88),stone(),.08)
    cylinder('Tapered statue plinth',(0,1.7,0),1.5,1.65,stone(),4,1.12).rotation_euler.z=math.pi/4
    box('Plinth cornice',(0,2.65,0),(2.8,.27,2.35),stone(),.06)
    native('common_altar_of_hope_prefab',(1.65,.9,-.7),width=.8,include=['candle'],shade=1.8)


BUILDERS = {name: globals()[name] for name in (
    'abbey','tavern','sanitarium','guild','blacksmith','camping_trainer',
    'stage_coach','nomad_wagon','graveyard','statue')}

DESIGN = {
    'abbey': 'Raised stone nave, steep slate roof, rear bell tower, lancets, rose window, buttresses and stair.',
    'tavern': 'Unequal half-timbered wings, tall chimney, red roofs, warm lights and a hanging reception sign.',
    'sanitarium': 'Cold stone lower floor, jettied timber upper floor, round stair turret, pointed cap and green glass.',
    'guild': 'Tall stone training hall, steep red roof, sword standards, armoured door and lower side wing.',
    'blacksmith': 'Low thatched workshop, one tall and one short chimney, open forge bay, hearth, anvil and sign.',
    'camping_trainer': 'Open brown ridge tent, ropes, bedroll, chest, barrel, axe and a separate campfire.',
    'stage_coach': 'Dark enclosed passenger carriage, iron shod wheels, lanterns, waiting trunks and a coach-stop sign.',
    'nomad_wagon': 'Curved plum caravan, eye crest, door and steps, side wheels, sagging shop awning and tassels.',
    'graveyard': 'Raised burial terrace, masonry retaining wall, scattered headstones, spear fence and entry stair.',
    'statue': 'Weathered memorial plinth, cornice, candle and stepped stone base; no foreground humanoid.',
}
