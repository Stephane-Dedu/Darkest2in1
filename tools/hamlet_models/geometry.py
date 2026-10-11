"""Small, painted architectural mesh helpers. Inputs use Unity's X/right, Y/up, Z/back axes."""
import math
import random
from pathlib import Path
import bpy
import numpy as np
from mathutils import Vector

KIT = OUT = LIB = None
MATERIALS = {}
SOURCES = []


def multiply_node(tree, color):
    # Blender 5's glTF exporter recognizes the current Mix node, not legacy MixRGB factors.
    node=tree.nodes.new('ShaderNodeMix')
    node.data_type='RGBA'
    node.blend_type='MULTIPLY'
    node.inputs[0].default_value=1
    node.inputs[7].default_value=color
    return node


def exportable_native_materials(objects):
    for mat in {m for ob in objects for m in ob.data.materials if m}:
        tree=mat.node_tree
        for old in list(tree.nodes):
            if old.type!='MIX_RGB' or old.blend_type!='MULTIPLY':
                continue
            node=multiply_node(tree,old.inputs[2].default_value)
            for link in list(old.inputs[1].links):
                tree.links.new(link.from_socket,node.inputs[6])
            for link in list(old.outputs[0].links):
                tree.links.new(node.outputs[2],link.to_socket)
            tree.nodes.remove(old)


def configure(kit, out, lib):
    global KIT, OUT, LIB
    KIT, OUT, LIB = Path(kit), Path(out), lib
    MATERIALS.clear()
    SOURCES.clear()


def p(xyz):
    x, y, z = xyz
    return (x, z, y)


def material(name, color=(1, 1, 1, 1), texture=None, emission=.25, cutout=False):
    key = name, tuple(color), texture, emission, cutout
    if key in MATERIALS:
        return MATERIALS[key]
    m = bpy.data.materials.new(name)
    m.use_nodes = True
    m.diffuse_color = color
    nt = m.node_tree
    nt.nodes.clear()
    output = nt.nodes.new('ShaderNodeOutputMaterial')
    shader = nt.nodes.new('ShaderNodeBsdfPrincipled')
    shader.inputs['Base Color'].default_value = color
    shader.inputs['Roughness'].default_value = .95
    shader.inputs['Specular IOR Level'].default_value = .12
    shader.inputs['Emission Color'].default_value = color
    shader.inputs['Emission Strength'].default_value = emission
    if texture:
        tex = nt.nodes.new('ShaderNodeTexImage')
        tex.image = bpy.data.images.load(str(OUT / 'textures' / f'{texture}.png'), check_existing=True)
        mix = multiply_node(nt,color)
        nt.links.new(tex.outputs['Color'], mix.inputs[6])
        nt.links.new(mix.outputs[2], shader.inputs['Base Color'])
        nt.links.new(mix.outputs[2], shader.inputs['Emission Color'])
        if cutout:
            nt.links.new(tex.outputs['Alpha'], shader.inputs['Alpha'])
    nt.links.new(shader.outputs[0], output.inputs['Surface'])
    m.use_backface_culling = False
    MATERIALS[key] = m
    return m


def ink():
    return material('Ink edges', (.018, .014, .018, 1), emission=.35)


def wood():
    return material('DD2 weathered timber', (1.15, 1.0, .85, 1), 'timber', .3)


def stone():
    return material('DD2 weathered stone', (1.25, 1.22, 1.18, 1), 'stone', .3)


def mesh(name, vertices, faces, mat, uv=None):
    me = bpy.data.meshes.new(name)
    # The Y/Z exchange reverses handedness.
    me.from_pydata([p(v) for v in vertices], [], [list(reversed(f)) for f in faces])
    me.update()
    ob = bpy.data.objects.new(name, me)
    bpy.context.collection.objects.link(ob)
    me.materials.append(mat)
    layer = me.uv_layers.new(name='UVMap')
    for poly in me.polygons:
        coords = uv[poly.index] if uv else [(0, 0), (1, 0), (1, 1), (0, 1)]
        for k, li in enumerate(poly.loop_indices):
            layer.data[li].uv = coords[len(coords) - 1 - k] if uv else coords[k % len(coords)]
    return ob


def box(name, center, size, mat=None, bevel=0, rotation=0):
    bpy.ops.mesh.primitive_cube_add(size=1, location=p(center))
    ob = bpy.context.object
    ob.name = name
    ob.scale = p(size)
    ob.rotation_euler.z = -math.radians(rotation)
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    ob.data.materials.append(mat or stone())
    # Keep masonry and grain at a world scale instead of stretching one tiny atlas crop over a whole wall.
    uv=ob.data.uv_layers.active
    for poly in ob.data.polygons:
        axis=max(range(3),key=lambda k:abs(poly.normal[k]))
        axes=[k for k in range(3) if k!=axis]
        for li in poly.loop_indices:
            co=ob.data.vertices[ob.data.loops[li].vertex_index].co
            uv.data[li].uv=(co[axes[0]]/2.0,co[axes[1]]/2.0)
    if bevel:
        mod = ob.modifiers.new('Chipped corners', 'BEVEL')
        mod.width, mod.segments = bevel, 1
        bpy.ops.object.modifier_apply(modifier=mod.name)
    return ob


def beam(name, a, b, width=.12, mat=None):
    start, end = Vector(p(a)), Vector(p(b))
    ob = box(name, (0, 0, 0), (width, (end - start).length, width), mat or wood())
    # box's long Unity-Y axis is Blender Z.
    ob.location = (start + end) / 2
    ob.rotation_euler = (end - start).to_track_quat('Z', 'Y').to_euler()
    return ob


def cylinder(name, center, radius, height, mat=None, n=10, radius2=None):
    bpy.ops.mesh.primitive_cone_add(vertices=n, radius1=radius, radius2=radius if radius2 is None else radius2,
        depth=height, location=p(center))
    ob = bpy.context.object
    ob.name = name
    ob.data.materials.append(mat or stone())
    return ob


def gable(name, x, base, z, width, depth, rise, roof='roof_red', tint=(1.35, 1.16, 1.1, 1), wall=None):
    # Real roof volume, with a dipped ridge and uneven overhanging eaves.
    mat = material('DD2 ' + roof, tint, roof, .45)
    xs = [-width/2, -width*.24, 0, width*.24, width/2]
    ys = [base, base + rise*.43, base + rise, base + rise*.43, base]
    vertices = [(x + px, py - (.07 if iz else 0), z + dz) for iz, dz in enumerate([-depth/2, 0, depth/2]) for px, py in zip(xs, ys)]
    distances=[0]
    for i in range(4):
        distances.append(distances[-1]+math.hypot(xs[i+1]-xs[i],ys[i+1]-ys[i]))
    faces, uvs = [], []
    for row in range(2):
        for col in range(4):
            i = row * 5 + col
            faces.append([i, i + 1, i + 6, i + 5])
            period=4 if roof=='roof_red' else 6
            u0,u1=row*depth/period,(row+1)*depth/period
            v0,v1=distances[col],distances[col+1]
            uvs.append([(u0,v0),(u0,v1),(u1,v1),(u1,v0)])
    ob = mesh(name + ' roof', vertices, faces, mat, uvs)
    for zz in [z-depth/2, z+depth/2]:
        for i in range(4):
            beam(name + ' gable verge', (x+xs[i], ys[i], zz), (x+xs[i+1], ys[i+1], zz), .15, ink())
        if wall:
            mesh(name+' gable wall', [(x-width/2, base, zz), (x+width/2, base, zz), (x, base+rise, zz)], [[0,1,2]], wall,
                 [[(0,0), (width/1.4,0), (width/2.8,rise/1.4)]])
    for xx, yy in [(x-width/2, base), (x, base+rise), (x+width/2, base)]:
        beam(name + ' ridge/eave', (xx, yy, z-depth/2-.08), (xx, yy-.07, z+depth/2+.08), .13, ink())
    return ob


def stairs(x, base, z, width, height, count=7, depth=2.8, mat=None):
    for i in range(count):
        h = (i+1)*height/count
        box('Entry stair', (x, base+h/2, z-depth + (i+.5)*depth/count), (width, h, depth/count+.03), mat or stone(), .04)


def arch(name, x, y, z, width, height, glass=False, color=None):
    outline = [(-.5,0),(.5,0),(.5,.61),(.32,.82),(0,1),(-.32,.82),(-.5,.61)]
    # A shallow solid frame with a separate inset opening and physical mullions/planks.
    outer = [(x+u*width, y+v*height, z) for u,v in outline]
    inner = [(x+u*width*.78, y+.10+v*(height-.23), z-.045) for u,v in outline]
    mesh(name+' surround', outer + [(a,b,c+.18) for a,b,c in outer],
         [list(range(7))]+[[i,(i+1)%7,(i+1)%7+7,i+7] for i in range(7)], stone())
    mat = material(name+' opening', color or ((.53,.3,.09,1) if glass else (.065,.041,.026,1)),
        texture='window_grain' if glass else None,emission=1.4 if glass else .3)
    mesh(name+' opening', inner, [list(range(7))], mat,[[(u+.5,v) for u,v in outline]])
    for a,b in zip(outer,outer[1:]+outer[:1]):
        beam(name+' ink frame', a,b,.075,ink())
    for u in [-.22,0,.22]:
        top = .64 if abs(u)>.1 else .86
        beam(name+' mullion' if glass else name+' door plank', (x+u*width,y+.13,z-.07), (x+u*width,y+height*top,z-.07), .055,ink() if glass else wood())
    if not glass:
        for v in [.25,.55]:
            beam(name+' iron strap',(x-width*.35,y+height*v,z-.10),(x+width*.35,y+height*v,z-.10),.075,ink())


def timber_frame(x, base, z, width, height, depth):
    for zz in [z-depth/2-.035, z+depth/2+.035]:
        for xx in [x-width/2, x, x+width/2]:
            beam('Structural timber',(xx,base,zz),(xx,base+height,zz),.18)
        for yy in [base,base+height*.45,base+height]:
            beam('Timber course',(x-width/2,yy,zz),(x+width/2,yy,zz),.18)
        for lo,hi in [(x-width/2,x),(x,x+width/2)]:
            beam('Diagonal timber',(lo,base+height*.45,zz),(hi,base+height,zz),.15)


def chimney(x, y, z, height, width=.9):
    box('Tapered masonry chimney',(x,y+height/2,z),(width,height,width*.85),stone(),.08)
    box('Chimney cap',(x,y+height,z),(width*1.25,.28,width*1.1),stone(),.05)
    box('Soot opening',(x,y+height+.15,z),(width*.70,.015,width*.60),ink())


def banner(name, x, y, z, width, height, texture='guild_banner'):
    mat = material(name,(1.15,.95,.95,1),texture,.65,True)
    vertices, faces, uv = [],[],[]
    for r in range(7):
        for c in range(4):
            u,v=c/3,r/6
            vertices.append((x+(u-.5)*width,y+(1-v)*height,z+math.sin(u*math.pi*3)*.09+v*v*.12))
    for r in range(6):
        for c in range(3):
            i=r*4+c
            faces.append([i,i+1,i+5,i+4])
            uv.append([(c/3,1-r/6),((c+1)/3,1-r/6),((c+1)/3,1-(r+1)/6),(c/3,1-(r+1)/6)])
    mesh(name,vertices,faces,mat,uv)
    beam('Banner rod',(x-width*.65,y+height,z),(x+width*.65,y+height,z),.09,ink())


def native(asset, pos=(0,0,0), width=None, scale=1, rotation=0, include=None, exclude=(), height_scale=1, shade=1.4, solidify=0):
    folder = KIT / 'exp' / asset
    oldskip = LIB.SKIP
    LIB.SKIP = [s for s in oldskip if s != 'collider']
    exclude = tuple(exclude)+('wagon_meshcollider',)
    lo,hi = LIB.bbox(folder,include=include,exclude=exclude)
    if not np.isfinite(lo).all() or (hi-lo).max() <= 0:
        raise ValueError('No usable geometry: '+asset)
    if width:
        scale = width / max(hi[0]-lo[0],hi[2]-lo[2])
    transform,_ = LIB.place_matrix(folder,pos,rotation,scale,include=include,exclude=exclude)
    objs,_,_ = LIB.build(folder,transform,include=include,exclude=exclude,shade=shade)
    LIB.SKIP = oldskip
    exportable_native_materials(objs)
    for ob in objs:
        ob.name = asset+' / '+ob.name
        if height_scale != 1:
            for v in ob.data.vertices:
                v.co.z = pos[1]+(v.co.z-pos[1])*height_scale
        ob['source_asset'] = asset
        if solidify:
            bpy.context.view_layer.objects.active=ob
            mod=ob.modifiers.new('Carved headstone depth','SOLIDIFY')
            mod.thickness=solidify
            mod.offset=0
            mod.material_offset_rim=len(ob.data.materials)
            ob.data.materials.append(stone())
            bpy.ops.object.modifier_apply(modifier=mod.name)
    SOURCES.append({'asset':asset,'position':pos,'width':width,'scale':scale,'rotation':rotation,'include':include,'exclude':exclude,'height_scale':height_scale,'solidify':solidify})
    return objs


def wheel(x,y,z,radius):
    bpy.ops.mesh.primitive_torus_add(major_radius=radius*.87,minor_radius=radius*.12,
        major_segments=14,minor_segments=4,location=p((x,y,z)),rotation=(0,math.pi/2,0))
    ob=bpy.context.object; ob.name='Iron shod wagon wheel'; ob.data.materials.append(wood())
    for i in range(8):
        a=i*math.pi/4
        beam('Wheel spoke',(x,y,z),(x,y+math.sin(a)*radius*.84,z+math.cos(a)*radius*.84),.10)
    hub=cylinder('Wheel hub',(x,y,z),.2,.3,wood(),8)
    hub.rotation_euler.y=math.pi/2


def lantern(x,y,z):
    amber=material('Lantern glass',(.65,.32,.08,1),emission=2.5)
    box('Lantern glowing glass',(x,y,z),(.26,.42,.26),amber,.03)
    for xx in [-.15,.15]:
        for zz in [-.15,.15]:
            beam('Lantern iron',(x+xx,y-.24,z+zz),(x+xx,y+.24,z+zz),.045,ink())
    box('Lantern cap',(x,y+.26,z),(.38,.09,.38),ink(),.03)


def platform(width,depth,height=.3):
    return box('Stone foundation',(0,height/2,0),(width,height,depth),stone(),.12)


def stone_blocks(name, x, y, z, width, height, depth, rows=4):
    rng=random.Random(name)
    for r in range(rows):
        count=max(2,round(width/.65))
        for c in range(count):
            xx=x-width/2+(c+.5)*width/count
            box(name,(xx,y+(r+.5)*height/rows,z),(width/count-.035,height/rows-.035,depth),stone(),rng.uniform(.015,.05))
