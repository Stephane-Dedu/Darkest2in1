"""Shieldbreaker's DD1 costume rebuilt as weighted, volumetric surfaces.

Executed in model_blender.py's authoring context. Source art remains private.
"""
import numpy as np

PALETTE[:] = [(.045,.035,.027,1), (.16,.105,.065,1), (.32,.235,.11,1),
             (.59,.40,.11,1), (.82,.61,.21,1), (.43,.43,.35,1),
             (.78,.76,.64,1), (.16,.23,.045,1), (.36,.52,.08,1),
             (.20,.25,.25,1), (.49,.58,.56,1), (.018,.019,.015,1),
             (.39,.215,.12,1), (.48,.70,.10,1), (.56,.335,.185,1), (.92,.87,.71,1)]
TILES = ['shield','mask','face','helmet','chest','belt',
         'left_pauldron','left_knee_guard','sash','pants','turban','left_tassets02']
PAINT_ALPHA = {}


def painted(o, tile, uv):
    index = TILES.index(tile)
    x, y = index % 4, index // 4
    for loop in o.data.loops:
        u, v = uv[loop.vertex_index]
        o.data.uv_layers.active.data[loop.index].uv = (.5 + (x + .01 + .98*u) / 8, (y + .01 + .98*v) / 4)
    return o


def front(name, rows, group, colour, texture=None, steps=16):
    """Curved front panel: (centre x, y, z, half-width, convex depth)."""
    verts, weights, uv, faces = [], [], [], []
    for j, (x,y,z,w,bulge) in enumerate(rows):
        for k in range(steps+1):
            u = k / steps
            verts.append((x+(u*2-1)*w,y,z+bulge*(1-(u*2-1)**2)))
            weights.append(group[j] if len(group) == len(rows) and isinstance(group[0][0], tuple) else group)
            uv.append((u,1-j/(len(rows)-1)))
            if j and k:
                b = j*(steps+1)+k
                faces.append((b-steps-2,b-1,b,b-steps-1))
    if texture:
        if texture not in PAINT_ALPHA:
            image=bpy.data.images.load(str(OUT/'reference'/(texture+'.png')),check_existing=True)
            w,h=image.size
            PAINT_ALPHA[texture]=np.array(image.pixels[:],dtype=np.float32).reshape(h,w,4)[:,:,3]
        alpha=PAINT_ALPHA[texture]; h,w=alpha.shape
        def visible(face):
            u=sum(uv[i][0] for i in face)/len(face); v=sum(uv[i][1] for i in face)/len(face)
            return alpha[min(h-1,int(v*h)),min(w-1,int(u*w))]>.35
        faces=[face for face in faces if visible(face)]
    o = part(name,verts,faces,weights,colour)
    return painted(o,texture,uv) if texture else o


def ribbon(name, points, widths, groups, colour, fold=.45):
    verts, faces, weights = [], [], []
    for j,(point,width,group) in enumerate(zip(points,widths,groups)):
        p = Vector(point)
        tangent = Vector(points[min(j+1,len(points)-1)])-Vector(points[max(0,j-1)])
        across = tangent.cross(Vector((0,0,1))).normalized()
        for k in range(7):
            u = k/6
            verts.append(p+across*(u-.5)*width+Vector((0,0,fold*math.sin(u*math.pi*3+j*.5))))
            weights.append(group)
            if j and k:
                b=j*7+k; faces.append((b-8,b-7,b,b-1))
    return part(name,verts,faces,weights,colour)


def loft(name, points, radii, groups, colour, folds=0, sides=24):
    o=tube(name,points,radii,groups,colour,sides)
    for j in range(len(points)):
        centre=Vector(points[j])
        for k in range(sides):
            v=o.data.vertices[j*sides+k]
            a=k*2*math.pi/sides
            # Broad moving folds change the silhouette, rather than drawing stripes on a cylinder.
            factor=1+folds*(.55*math.sin(5*a+j*.55)+.30*math.sin(8*a-j*.3))
            v.co=centre+(v.co-centre)*factor
    o.data.update()
    return o


def inkline(name, points, group, width=.35, colour=11):
    return tube(name,points,[width]*len(points),[group]*len(points),colour,5)


# Weighted donor foot detail; all exposed anatomy and costume are rebuilt.
faces=[]
for j in range(0,len(D['triangles']),3):
    tri=D['triangles'][j:j+3]
    if all(D['vertices'][i][1]<11 for i in tri): faces.append(tuple(reversed(tri)))
part('body',D['vertices'],faces,
     [[(b,w) for b,w in zip(ii,ww) if w>.00001] for ii,ww in zip(D['indices'],D['weights'])],uv=D['uv'])

# Shaped abdomen, rib cage and shoulders, with the cropped gold cross-wrap above.
ys=[94,101,109,116,123,130,137,143,148]
radii=[(19,12),(17.5,10),(14.8,8.3),(14.5,8.5),(17,10),(19.7,12),(20.8,11.7),(19,10),(11,7)]
torso_groups=[rigid('ROOTSHJnt')]*3+[rigid('Spine_01SHJnt')]*2+[rigid('Spine_02SHJnt')]*4
loft('skin_torso',[(0,y,2) for y in ys],radii,torso_groups,12)
loft('skin_neck',[(0,145,2),(0,151,2),(0,158,3)],[(6,5),(5,4.5),(5.5,5)],
     [rigid('Spine_02SHJnt'),rigid('Head_TopSHJnt'),rigid('Head_TopSHJnt')],12)
for side in ('l','r'):
    shoulder,elbow,wrist=[POS[f'{side}_Arm_{n}SHJnt'] for n in ('Shoulder','Elbow','Wrist')]
    points=[shoulder,shoulder.lerp(elbow,.18),shoulder.lerp(elbow,.4),shoulder.lerp(elbow,.75),elbow,
            elbow.lerp(wrist,.2),elbow.lerp(wrist,.55),elbow.lerp(wrist,.85),wrist]
    groups=[rigid(f'{side}_Arm_ShoulderSHJnt')]*4+[
        [(bone(f'{side}_Arm_ShoulderSHJnt'),.2),(bone(f'{side}_Arm_ElbowSHJnt'),.8)]]+[rigid(f'{side}_Arm_ElbowSHJnt')]*4
    loft('skin_'+side+'_arm',points,[6.4,7,6.3,4.8,3.9,5.3,4.7,3.5,3.2],groups,12)
    # Painted anatomical crease, weighted to the same forearm.
    across=(wrist-elbow).cross(Vector((0,0,1))).normalized()
    line=[elbow.lerp(wrist,t)+Vector((0,0,3.8))+across*1.7 for t in (.15,.33,.52,.67)]
    inkline('skin_'+side+'_forearm_crease',line,rigid(f'{side}_Arm_ElbowSHJnt'),.28)

loft('gold_chest_wrap',[(0,y,2.3) for y in (124,129,136,143)],[(17.5,10.5),(20.2,12.5),(21.2,12.1),(19.5,10.3)],
     [rigid('Spine_02SHJnt')]*4,3,folds=.025)
front('gold_chest_painted',[(0,143,10.9,19.5,2.2),(0,137,12.4,21,2.4),(0,129,12.8,20.2,2.1),(0,124,10.8,17.2,2)],
      rigid('Spine_02SHJnt'),4,'chest')
inkline('abdomen_shadow',[(-8,121,10.1),(-3,115,11),(-1,111,10.5)],rigid('Spine_01SHJnt'),.48)
inkline('abdomen_flank',[(10,121,9.7),(9,115,9.8),(13,111,8.8)],rigid('Spine_01SHJnt'),.35)

# Harem trousers, wide above the knees, gathered into wrapped ankles.
for side in ('l','r'):
    hip,knee,ankle=[POS[f'{side}_Leg_{n}SHJnt'] for n in ('Hip','Knee','Ankle')]
    points=[hip,hip.lerp(knee,.16),hip.lerp(knee,.4),hip.lerp(knee,.7),knee,
            knee.lerp(ankle,.2),knee.lerp(ankle,.5),knee.lerp(ankle,.75),ankle+Vector((0,6,0))]
    group=[rigid(f'{side}_Leg_HipSHJnt')]*4+[
        [(bone(f'{side}_Leg_HipSHJnt'),.2),(bone(f'{side}_Leg_KneeSHJnt'),.8)]]+[rigid(f'{side}_Leg_KneeSHJnt')]*3+[rigid(f'{side}_Leg_AnkleSHJnt')]
    radii=[(13.5,12),(15.8,13),(16.2,12.5),(14,11),(12.3,10.3),(12,9.7),(10.5,8.7),(8.5,7),(5.1,5.3)]
    trousers=loft(side+'_harem_trousers',points,radii,group,3,folds=.17)
    # Project the original broad gold/black fold shapes onto the front of each leg.
    paint_uv=[]
    for v in trousers.data.vertices:
        t=max(0,min(1,(hip.y-v.co.y)/(hip.y-ankle.y-6)))
        centre=hip.lerp(ankle,t)
        u=max(0,min(1,.5+(v.co.x-centre.x)/31))
        paint_uv.append((.04+.46*u if side=='r' else .52+.44*u,1-t))
    saved_uv=[loop.uv.copy() for loop in trousers.data.uv_layers.active.data]
    painted(trousers,'pants',paint_uv)
    for polygon in trousers.data.polygons:
        if polygon.normal.z<.35:
            for li in polygon.loop_indices: trousers.data.uv_layers.active.data[li].uv=saved_uv[li]
    # Large black slashes match the deliberately broad folds in her DD1 painting.
    for k in range(3):
        sign=-1 if side=='l' else 1
        base=hip.lerp(knee,.24+k*.18)
        inkline(side+'_trouser_fold_'+str(k),[base+Vector((sign*4,3,11)),base+Vector((sign*9,-1,8.5)),base+Vector((sign*10,-6,7))],rigid(f'{side}_Leg_HipSHJnt'),.6)
    for j in range(8):
        p=ankle.lerp(knee,.03+j*.026)
        loft(side+'_ankle_wrap_'+str(j),[p,p+Vector((0,1.25,0))],[5.8,5.9],[rigid(f'{side}_Leg_AnkleSHJnt')]*2,6 if j%3 else 5,sides=16)
    loft(side+'_boot_collar',[ankle+Vector((0,-5,0)),ankle+Vector((0,3,0))],[5.2,5.2],
         [rigid(f'{side}_Leg_AnkleSHJnt')]*2,1,sides=16)

# Green sash and steel belt. Hanging tassets attach to the hips rather than the knees.
loft('waist_green_sash',[(0,y,2) for y in (99,103,107,110)],[(18.8,12),(19.3,12.1),(17.5,10.9),(16,10.2)],
     [rigid('ROOTSHJnt')]*4,7,folds=.07)
front('waist_sash_painted',[(0,111,9.5,16,2),(0,106,11.2,18.3,2),(0,100,12.2,19,1.8)],rigid('ROOTSHJnt'),7,'sash')
loft('leather_belt',[(0,104,2),(0,108,2)],[(18.3,11.6)]*2,[rigid('ROOTSHJnt')]*2,1)
front('steel_belt_painted',[(0,109,12.5,18.5,1.7),(0,100,13.1,19.5,1.2)],rigid('ROOTSHJnt'),9,'belt')
for side in ('l','r'):
    for j in range(3):
        sign=-1 if side=='l' else 1
        x=sign*(16+j*1.4); y=101-j*6.5; z=8.5-j*.7
        outline=[(x-sign*3,y+3,z),(x+sign*7,y+1,z-1),(x+sign*9,y-8,z-2),(x-sign*1,y-4,z+1)]
        o=part(side+'_tasset_'+str(j),outline,[(0,1,2),(2,3,0)],[rigid(f'{side}_Leg_HipSHJnt')]*4,9)
        painted(o,'left_tassets02',[(0,1),(1,1),(1,0),(0,0)])

# Head, painted eye shadow and the folded yellow mask.
head=POS['Head_TopSHJnt']+Vector((0,-6,1))
ellipsoid('skin_head',head+Vector((0,3,0)),(10.2,12,9),rigid('Head_TopSHJnt'),12,12,24)
front('skin_face_painted',[(0,head.y+7,head.z+7.3,9,2),(0,head.y+2.3,head.z+9,9.2,1),
                         (0,head.y-2,head.z+8,8.7,1.4)],rigid('Head_TopSHJnt'),12,'face')
front('yellow_mask',[(0,head.y+1.8,head.z+8.7,10.4,3.8),(0,head.y-3,head.z+9.5,10,4),
                     (1,head.y-8,head.z+8.5,8.2,3.2),(2,head.y-13,head.z+7,5.4,1.5)],
      rigid('Head_TopSHJnt'),4,'mask')
# Dark, angular cap above the broad cream wrapping, as in her base combat art.
ellipsoid('helmet_core',head+Vector((0,15,-1)),(14,12,12),rigid('Head_TopSHJnt'),9,7,16)
front('helmet_painted',[(0,head.y+27,head.z+4,3,1),(0,head.y+23,head.z+7,10,3),
                        (0,head.y+16,head.z+9,14.5,3.7),(0,head.y+8,head.z+8.4,13.4,3.4)],
      rigid('Head_TopSHJnt'),6,'helmet')
loft('turban_back',[(0,head.y+7,head.z-1),(0,head.y+13,head.z-1)],[(13.1,11.7),(14.2,12)],
     [rigid('Head_TopSHJnt')]*2,6,folds=.045)
# Broad cloth strips frame the face, then drape asymmetrically across the chest.
ribbon('headcloth_left',[(-10,head.y+10,head.z),(-14,head.y+2,head.z+4),(-14,head.y-11,head.z+5),
                        (-17,145,8),(-15,140,12),(-7,136,15),(5,138,15),(14,144,10)],
       [7,7,7,7,6,4.5,4,3.5],[rigid('Head_TopSHJnt')]*3+[rigid('Spine_02SHJnt')]*5,6)
ribbon('headcloth_right',[(12,head.y+10,head.z),(14,head.y+1,head.z+2),(14,head.y-10,head.z+2),(18,146,5),(15,136,8)],
       [5,5,5,6,5],[rigid('Head_TopSHJnt')]*3+[rigid('Spine_02SHJnt')]*2,5)
for j in range(2):
    ribbon('neck_fold_'+str(j),[(-12,149-j*3,13),(-5,141-j*2,17),(5,142-j*2,17),(12,149-j*3,12)],
           [1.6,1.8,1.8,1.4],[rigid('Spine_02SHJnt')]*4,6 if j==0 else 5)
tube('headcloth_left_ink',[(-14,head.y-7,head.z+6),(-17,145,8.7),(-15,140,12.8),(-8,138,16)],
     [.45]*4,[rigid('Head_TopSHJnt')]+[rigid('Spine_02SHJnt')]*3,11,5)
ribbon('headcloth_back',[(0,head.y+14,-9),(-4,head.y,-12),(-9,146,-13),(-6,130,-14),(-10,115,-15)],
       [12,13,10,8,6],[rigid('Head_TopSHJnt')]*2+[rigid('Spine_02SHJnt')]*3,5)
ribbon('green_head_tie',[(-6,head.y+24,-4),(-12,head.y+28,-5),(-21,head.y+22,-7),(-24,head.y+8,-9),(-20,147,-10),(-26,130,-10)],
       [1.7,2.3,2.4,1.8,1.5,.6],[rigid('Head_TopSHJnt')]*6,8)

# Left shoulder scale armour and knee guard, both visible in DD1's base outfit.
shoulder,elbow=[POS['l_Arm_'+n+'SHJnt'] for n in ('Shoulder','Elbow')]
for j in range(3):
    p=shoulder.lerp(elbow,.04+j*.11)
    for k in range(3):
        x=p.x+(k-1)*4.4; y=p.y; z=p.z+5.8-abs(k-1)
        points=[(x-3.8,y+3,z),(x+2,y+4,z+1.2),(x+4,y-2,z+1),(x,y-7,z+1.5),(x-4,y-3,z)]
        o=part('left_shoulder_scale',points,[(0,1,2),(0,2,3),(0,3,4)],[rigid('l_Arm_ShoulderSHJnt')]*5,9)
        painted(o,'left_pauldron',[(0,1),(1,1),(1,.5),(.5,0),(0,.4)])
knee=POS['l_Leg_KneeSHJnt']
front('left_knee_plate',[(knee.x,knee.y+9,knee.z+8.7,8,2),(knee.x,knee.y+1,knee.z+10.2,10,2),
                        (knee.x,knee.y-9,knee.z+7.8,5,1.5)],rigid('l_Leg_KneeSHJnt'),9,'left_knee_guard')
for j in range(2):
    front('left_shin_plate_'+str(j),[(knee.x,knee.y-10-j*9,knee.z+8.5,7.8,1.6),
                                    (knee.x,knee.y-18-j*9,knee.z+8,6.3,1.4)],rigid('l_Leg_KneeSHJnt'),9,'left_tassets02')

# Bandaged left stump and green right-wrist ties.
for side in ('l','r'):
    elbow,wrist=[POS[f'{side}_Arm_{n}SHJnt'] for n in ('Elbow','Wrist')]
    for j in range(8 if side=='l' else 3):
        p=elbow.lerp(wrist,.32+j*.075 if side=='l' else .77+j*.07)
        loft(side+'_wrist_wrap_'+str(j),[p,p+(wrist-elbow).normalized()*1.8],[4.4,4.3],[rigid(f'{side}_Arm_ElbowSHJnt')]*2,
             (6 if j%3 else 5) if side=='l' else (8 if j%2 else 7),sides=16)
ellipsoid('left_stump',POS['l_Arm_WristSHJnt'],(3.8,4,3.6),rigid('l_Arm_ElbowSHJnt'),5,7,16)

# The fingers curl around the spear, rather than a sphere standing in for the fist.
grip=POS['r_Arm_WristSHJnt']+Vector((0,-1,2))
ellipsoid('spear_palm',grip+Vector((0,0,-2)),(3,4.1,2.4),rigid('r_Arm_WristSHJnt'),12,8,16)
for j in range(4):
    y=-2.8+j*1.7
    points=[grip+Vector(v) for v in [(2.1,y,-2),(3,y,.2),(1.9,y,2),(-.3,y,2.4),(-1.6,y,.7)]]
    loft('spear_finger_'+str(j),points,[1,1.05,1,.85,.7],[rigid('r_Arm_WristSHJnt')]*5,12,sides=10)
loft('spear_thumb',[grip+Vector(v) for v in [(-2.2,3,-1),(-2.5,1.2,1),(-1.5,-.5,2.5),(1,-1,2.1)]],
     [1.2,1.2,1,.8],[rigid('r_Arm_WristSHJnt')]*4,14,sides=10)
loft('spear_shaft',[grip+Vector((0,-65,0)),grip+Vector((0,105,0))],[1.3,1.15],[rigid('r_Arm_WristSHJnt')]*2,1,sides=12)
for j in range(20):
    p=grip+Vector((0,-16+j*5.7,0))
    inkline('spear_cross_binding',[p+Vector((-1.25,0,.5)),p+Vector((0,1.7,1.35)),p+Vector((1.25,3.4,.5))],rigid('r_Arm_WristSHJnt'),.35,8 if j%3 else 5)
base=grip+Vector((0,103,0))
verts=[base+Vector(v) for v in [(-2,0,0),(-6,4,0),(-4,9,0),(-4.3,15,0),(0,30,0),(4.3,15,0),(4,9,0),(6,4,0),(2,0,0),(0,12,2),(0,12,-2)]]
blade_faces=[]
for k in range(8): blade_faces.extend([(k,k+1,9),(k+1,k,10)])
blade_faces.extend([(8,0,9),(0,8,10)])
part('spear_blade',verts,blade_faces,[rigid('r_Arm_WristSHJnt')]*len(verts),10)
inkline('spear_ridge',[base+Vector((0,2,2.1)),base+Vector((0,12,2.1)),base+Vector((0,27,.6))],rigid('r_Arm_WristSHJnt'),.35,9)
ribbon('spear_green_wrist_tie',[grip+Vector(v) for v in [(-3,0,-3),(-6,-11,-3),(-4,-20,-2),(-8,-29,-1)]],
       [1.9,2.1,1.5,.4],[rigid('r_Arm_WristSHJnt')]*4,8)

# Convex 3D shield with her actual olive paint, crescent plates and serpent emblem.
shield_c=POS['l_Arm_ElbowSHJnt'].lerp(POS['l_Arm_WristSHJnt'],.62)+Vector((0,0,10))
shield_group=rigid('l_Arm_ElbowSHJnt')
loft('shield_rim',[shield_c,shield_c+Vector((0,0,2.8))],[24.5,24.5],[shield_group]*2,9,sides=48)
verts=[shield_c+Vector((0,0,6))]; uv=[(.5,.5)]; faces=[]
for ring in range(1,9):
    r=ring/8
    for k in range(48):
        a=k*math.pi*2/48
        verts.append(shield_c+Vector((math.cos(a)*24*r,math.sin(a)*24*r,3+3*(1-r*r))))
        uv.append((.5+.5*r*math.cos(a),.5+.5*r*math.sin(a)))
        b=1+(ring-1)*48+k; n=1+(ring-1)*48+(k+1)%48
        if ring==1: faces.append((0,b,n))
        else: faces.append((b-48,b,n,n-48))
painted(part('shield_painted_face',verts,faces,[shield_group]*len(verts),7),'shield',uv)
# Thin projecting steel rim catches the light without burying the painted crescents.
points=[shield_c+Vector((math.cos(k*math.pi*2/64)*24.2,math.sin(k*math.pi*2/64)*24.2,3)) for k in range(65)]
inkline('shield_rim_edge',points,shield_group,.48,9)

# Build the private atlas: donor boot page on the left, native painted tiles below
# sixteen hand-painted material swatches on the right. All UVs have gutter space.
for filename,is_ink in [('shieldbreaker_base.png',False),('shieldbreaker_ink.png',True)]:
    source=bpy.data.images.load(str(OUT/('tex_hellion_ink.png' if is_ink else 'tex_hellion_col.png')))
    source.scale(2048,2048)
    old=np.array(source.pixels[:],dtype=np.float32).reshape(2048,2048,4)
    result=np.ones((2048,4096,4),dtype=np.float32)
    if not is_ink: old[:,:,:3]*=np.array([.78,.72,.63])
    result[:,:2048]=old
    for i,c in enumerate(PALETTE):
        x0=2048+i*128; x1=x0+128
        if is_ink: continue
        yy,xx=np.mgrid[0:512,0:128]; u=xx/127; v=yy/511
        shade=.81+.19*u+.07*np.sin(v*19+u*5)-.16*np.exp(-((u-.25-.06*np.sin(v*12))/.025)**2)
        shade+=.025*np.sin(xx*.57+yy*.13)*np.cos(yy*.075)
        result[1536:,x0:x1,:3]=np.array(c[:3])*shade[:,:,None]
    for i,tile in enumerate(TILES):
        if is_ink: continue
        image=bpy.data.images.load(str(OUT/'reference'/(tile+'.png')))
        image.scale(512,512)
        pixels=np.array(image.pixels[:],dtype=np.float32).reshape(512,512,4)
        if tile=='pants':
            # The source legs are painted in a bent pose. Carry over gold texture and
            # small ink creases, filling broad empty/shadow fields before projecting
            # onto straight volumetric legs so they do not become black UV rectangles.
            gold=(pixels[:,:,0]>.16)&(pixels[:,:,1]>.09)&(pixels[:,:,0]>pixels[:,:,1]*1.15)&(pixels[:,:,3]>.5)
            near=gold.copy()
            for _ in range(3):
                near=near|np.roll(near,1,0)|np.roll(near,-1,0)|np.roll(near,1,1)|np.roll(near,-1,1)
            ink=near&(pixels[:,:,:3].max(axis=2)<.12)&(pixels[:,:,3]>.5)
            yy,xx=np.mgrid[0:512,0:512]
            wash=(.87+.10*np.sin(xx*.043+yy*.018)+.035*np.cos(yy*.13))[:,:,None]*np.array([.59,.40,.11])
            pixels[:,:,:3]=np.where((gold|ink)[:,:,None],pixels[:,:,:3],wash)
            pixels[:,:,3]=1
        alpha=pixels[:,:,3:4]
        pixels[:,:,:3]=pixels[:,:,:3]*alpha+np.array([.025,.022,.017])*(1-alpha)
        pixels[:,:,3]=1
        x=2048+(i%4)*512; y=(i//4)*512
        result[y:y+512,x:x+512]=pixels
    image=bpy.data.images.new(filename,width=4096,height=2048)
    image.pixels.foreach_set(result.ravel()); image.filepath_raw=str(OUT/filename); image.file_format='PNG'; image.save()
    if not is_ink: ATLAS=image

mat=bpy.data.materials.new('Shieldbreaker DD1 painted'); mat.use_nodes=True
nodes=mat.node_tree.nodes; tex=nodes.new('ShaderNodeTexImage'); tex.image=ATLAS
shader=nodes.get('Principled BSDF')
mat.node_tree.links.new(tex.outputs['Color'],shader.inputs['Base Color'])
mat.node_tree.links.new(tex.outputs['Color'],shader.inputs['Emission Color'])
shader.inputs['Emission Strength'].default_value=.12
shader.inputs['Roughness'].default_value=1
for o in PARTS: o.data.materials.append(mat)
