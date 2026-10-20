"""Second modeling pass: native painted architectural meshes, fitted to DD1 service silhouettes."""
import random
import buildings as B
from geometry import *


def remove(*prefixes):
    for ob in list(bpy.data.objects):
        if any(ob.name.startswith(prefix) for prefix in prefixes):
            bpy.data.objects.remove(ob,do_unlink=True)


def replace_material(prefix, replacement):
    for ob in bpy.data.objects:
        if ob.type=='MESH':
            for i,mat in enumerate(ob.data.materials):
                if mat and mat.name.startswith(prefix):
                    ob.data.materials[i]=replacement


def quoins(x,z,bottom,height,width=.34):
    rng=random.Random(str((x,z)))
    count=max(3,round(height/.5))
    for i in range(count):
        box('Hand dressed corner stone',(x+rng.uniform(-.035,.035),bottom+(i+.5)*height/count,z),
            (width*(1.3 if i%2 else 1),height/count-.045,width*(1 if i%2 else 1.3)),stone(),.035)


def wrap_tower(ob,repeat_u,repeat_v):
    # Cone defaults use radial UV islands, which smear straight painted shingle courses.
    lo=min(v.co.z for v in ob.data.vertices)
    hi=max(v.co.z for v in ob.data.vertices)
    uv=ob.data.uv_layers.active
    for face in ob.data.polygons:
        if len(face.vertices)>4:
            continue
        coords=[ob.data.vertices[ob.data.loops[i].vertex_index].co for i in face.loop_indices]
        us=[(math.atan2(v.y,v.x)/(2*math.pi))%1 if v.xy.length>1e-5 else None for v in coords]
        base=[u for u in us if u is not None]
        if max(base)-min(base)>.5:
            us=[u+1 if u is not None and u<.5 else u for u in us]
        base=[u for u in us if u is not None]
        for i,v,u in zip(face.loop_indices,coords,us):
            uv.data[i].uv=((sum(base)/len(base) if u is None else u)*repeat_u,(v.z-lo)/(hi-lo)*repeat_v)


def abbey():
    B.abbey()
    replace_material('Abbey dark dressed stone',material('Abbey native carved masonry',(.76,.81,.9,1),'masonry_native',.6))
    remove('Entry porch','Abbey great door','Rose window glass','Rose stone tracery')
    native_fit('hero_shrine',(-1.7,3.55,-4.3),(3.6,4.8,2.65),shade=1.15)
    for x in [-3.23,3.23]:
        for z in [-3.9,4.5]:
            quoins(x,z,1.25,4.25)
        for y in [1.45,5.45]:
            beam('Nave carved string course',(x,y,-3.9),(x,y,4.5),.15,stone())
    native('common_altar_of_hope_prefab',(-2.8,1.25,-5.4),width=.7,include=['candle'],shade=1.4)


def tavern():
    B.tavern()
    # Replace the blank sign with a framed, raised tankard and suspension links.
    brass=material('Tarnished sign brass',(.3,.21,.10,1),emission=.7)
    box('Tankard sign relief',(-2.60,2.80,-4.10),(.27,.33,.075),brass,.025)
    beam('Tankard handle',(-2.43,2.91,-4.10),(-2.43,2.68,-4.10),.045,brass)
    for x in [-2.8,-2.4]:
        beam('Sign suspension chain',(x,3.2,-4),(x,3.07,-4),.035,ink())
    native('camp_chest',(3.5,.35,-2.7),width=1.1)


def sanitarium():
    B.sanitarium()
    remove('Sanitarium main stone floor','Sanitarium overhanging upper floor','Sanitarium tall gable',
           'Structural timber','Timber course','Diagonal timber','Sanitarium door','Sanitarium green glass',
           'Sanitarium gable glass')
    native_fit('valley_academic_house',(.9,5.1,.6),(6.6,8.0,6.3),shade=1.25)
    replace_material('Turret slate',material('Sanitarium painted slate cap',(.70,.76,.76,1),'roof_slate',.65))
    for ob in bpy.data.objects:
        if ob.name.startswith('Turret pointed roof'):
            wrap_tower(ob,2.5,4)
        elif ob.name.startswith('Round sanitarium stair tower'):
            wrap_tower(ob,4.5,2.5)
        elif ob.name.startswith('Turret timber stage'):
            wrap_tower(ob,2.5,1)
    for i in range(8):
        a=i*math.pi/4
        beam('Turret roof seam',(-3+1.85*math.cos(a),7.8,-2.2+1.85*math.sin(a)),(-3,10.0,-2.2),.035,ink())
    arch('Sanitarium tower lancet',-3,4.5,-3.67,.48,1.05,True,(.12,.38,.29,1))
    for y,r in [(1.35,1.5),(6.12,1.60),(7.85,1.65)]:
        cylinder('Turret carved cornice',(-3,y,-2.2),r,.16,stone(),12)
    native('camp_crate',(3.2,1.1,-2.9),width=.75)


def guild():
    B.guild()
    replace_material('Guild limestone',stone())
    remove('Guild steep red roof','Guild side wing','Guild armoured door','Guild arrow slit',
           'Roof crest iron','Roof crest rail')
    # Native gateway masonry retains painted chips, windows, tracery, posters and built-in shadows.
    native_fit('city_bigwall_main',(-.65,4.1,-2.65),(5.55,7.4,1.25),shade=1.3)
    native_fit('inn_lvl_01_prefab',(-.65,9.3,.3),(6.3,3.1,6.3),include=['the_inn_lvl_01_right'],keep_above=.58,shade=1.35)
    native_fit('inn_lvl_01_prefab',(3.0,3.05,.65),(2.65,5.3,5.1),include=['the_inn_lvl_01_left'],shade=1.3)
    arch('Guild recessed armoured door',-.65,.4,-3.31,2.0,3.0)
    # Move the DD1 identity onto the new façade rather than hiding it behind the native wall.
    for ob in bpy.data.objects:
        if any(ob.name.startswith(s) for s in ['DD1 sword standard','Banner rod','Guild crossed sword',
                                                'Sword crossguard','Guild shield']):
            ob.location.y-=.8
    for x in [-3.45,2.15]:
        quoins(x,-3.27,.4,7.4,.28)


def blacksmith():
    B.blacksmith()
    # Separate sooty firebrick and workshop tools give the forge its own service identity.
    soot=material('Sooty forge masonry',(.35,.31,.29,1),'masonry_native',.4)
    for ob in bpy.data.objects:
        if ob.name.startswith(('Forge stone hood','Forge hearth')):
            ob.data.materials[0]=soot
    iron=material('Blackened forged iron',(.08,.075,.065,1),emission=.45)
    for x in [.95,1.25,1.55,1.85,2.15]:
        beam('Hearth fire grate',(x,.55,-2.4),(x,.55,-1.4),.055,iron)
    for i in range(3):
        beam('Stacked iron billet',(-2.2+i*.18,.45,-2.8),(-2.2+i*.18,.45,-1.7),.11,iron)
    beam('Forge hammer handle',(-1.1,1.85,-2.55),(-.65,1.85,-2.3),.06,wood())
    box('Forge hammer head',(-1.1,1.85,-2.55),(.2,.15,.35),iron,.025)


def camping_trainer():
    B.camping_trainer()
    cloth=material('DD2 stitched survivalist canvas',(.30,.19,.115,1),'cloth_native',.8)
    replace_material('Survivalist canvas',cloth)
    # Sewn cloth, patch seams and pegged ropes instead of using thatch as fabric.
    cord=material('Worn hemp stitching',(.3,.23,.15,1),emission=.6)
    for z in [-2.75,0,2.75]:
        for side in [-1,1]:
            beam('Tent sewn ridge seam',(0,3.83,z),(side*3.48,.21,z),.028,cord)
    for x in [-4.2,4.2]:
        for z in [-3.36,3.36]:
            beam('Tent driven peg',(x,.05,z),(x-.12,.4,z),.07,wood())


def nomad_wagon():
    B.nomad_wagon()
    replace_material('Nomad plum canvas',material('DD2 plum stitched canvas',(.32,.17,.36,1),'cloth_native',.85))
    # Retain the caravan outline while replacing its plain body panels with native shop joinery.
    remove('Caravan living body')
    native_fit('the_hoarder_main',(0,2.60,0),(3.95,2.15,6.1),include=['hoarder_cart'],keep_above=.3,shade=1.25)
    for x in [-1.96,1.96]:
        for z in [-2.7,2.7]:
            beam('Caravan corner iron',(x,1.6,z),(x,3.6,z),.08,ink())
    for z in [-2,-1,0,1,2]:
        beam('Caravan cloth hem',(2.0,3.75,z),(4.8,3.05,z),.025,wood())


def stage_coach():
    B.stage_coach()
    brass=material('Aged coach brass',(.28,.19,.08,1),emission=.75)
    for i in range(8):
        a=i*math.pi/4
        beam('Coach sign wheel emblem',(-4.0,3.7,.86),(-4+math.cos(a)*.23,3.7+math.sin(a)*.23,.86),.03,brass)


def graveyard():
    B.graveyard()
    dirt=material('Graveyard damp earth',(.035,.043,.031,1),emission=.35)
    replace_material('Graveyard earth',dirt)
    for x,z in [(-3.2,-1.7),(2.9,1.6),(-1.4,2.0),(1.1,-2.1)]:
        cylinder('Sunken grave edging',(x,1.49,z),.45,.08,stone(),7)
    native('common_altar_of_hope_prefab',(.9,1.48,-2),width=.6,include=['candle'],shade=1.2)


def statue():
    B.statue()


BUILDERS={name:globals()[name] for name in B.BUILDERS}
DESIGN={**B.DESIGN,
    'abbey':B.DESIGN['abbey']+' Native DD2 carved portal and full painted UVs; dressed corner courses.',
    'sanitarium':B.DESIGN['sanitarium']+' Native Academic house façade, carved tower courses and painted slate cap.',
    'guild':B.DESIGN['guild']+' Native Sprawl masonry frontage, painted roof geometry and detailed side wing.',
    'nomad_wagon':B.DESIGN['nomad_wagon']+' Native Hoarder joinery and stitched canvas panels.',
    'statue':B.DESIGN['statue'],
}
