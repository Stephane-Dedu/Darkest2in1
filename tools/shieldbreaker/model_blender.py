"""Author and export the Shieldbreaker POC. Executed by build_model.py in Blender."""
import bpy
import json
import math
import sys
from pathlib import Path
from mathutils import Matrix, Vector

OUT = Path(sys.argv[sys.argv.index('--') + 1])
D = json.loads((OUT / 'donor.json').read_text(encoding='utf-8'))
BONES = D['bones']
REST = [Matrix([m[i:i + 4] for i in range(0, 16, 4)]).inverted() for m in D['bindposes']]
POS = {n: REST[i].translation for i, n in enumerate(BONES)}
bpy.ops.object.select_all(action='SELECT'); bpy.ops.object.delete(use_global=False)
PARTS = []
PALETTE = [(0.13, .10, .075, 1), (.31, .23, .14, 1), (.48, .36, .21, 1),
           (.62, .46, .18, 1), (.82, .64, .29, 1), (.56, .54, .44, 1),
           (.76, .74, .62, 1), (.21, .26, .13, 1), (.48, .57, .24, 1),
           (.36, .39, .37, 1), (.65, .68, .61, 1), (.07, .065, .05, 1), (.38, .24, .15, 1)]


def bone(n):
    if n == 'Spine_TopSHJnt': n = 'Spine_02SHJnt'
    return BONES.index(n)


def rigid(n):
    return [(bone(n), 1.0)]


def part(name, verts, faces, weights, colour=None, uv=None):
    m = bpy.data.meshes.new(name)
    m.from_pydata(verts, [], faces); m.update()
    o = bpy.data.objects.new(name, m); bpy.context.collection.objects.link(o)
    for n in BONES:
        o.vertex_groups.new(name=n)
    for i, groups in enumerate(weights):
        for b, w in groups:
            if w > .00001: o.vertex_groups[b].add([i], w, 'REPLACE')
    layer = m.uv_layers.new(name='UVMap')
    xmin = min(v.co.x for v in m.vertices); xmax = max(v.co.x for v in m.vertices)
    ymin = min(v.co.y for v in m.vertices); ymax = max(v.co.y for v in m.vertices)
    for poly in m.polygons:
        for li in poly.loop_indices:
            if uv is not None:
                u, v = uv[m.loops[li].vertex_index][:2]
                layer.data[li].uv = (u * .5, v)
            else:
                co = m.vertices[m.loops[li].vertex_index].co
                s = (co.x - xmin) / max(.01, xmax - xmin); t = (co.y - ymin) / max(.01, ymax - ymin)
                layer.data[li].uv = (.5 + (colour + .1 + .8 * s) / 32, .76 + .22 * t)
        poly.use_smooth = name == 'body' or name.startswith(('skin_', 'spear_palm', 'spear_finger', 'spear_thumb', 'shield_painted'))
    PARTS.append(o)
    return o


def tube(name, points, radii, groups, colour, sides=10):
    verts, faces, weights = [], [], []
    for j, (p, radius, group) in enumerate(zip(points, radii, groups)):
        p = Vector(p)
        axis = Vector(points[min(j + 1, len(points) - 1)]) - Vector(points[max(0, j - 1)])
        axis.normalize()
        across = axis.cross(Vector((0, 0, 1)))
        if across.length < .01: across = axis.cross(Vector((1, 0, 0)))
        across.normalize(); depth = axis.cross(across).normalized()
        rx, rz = radius if isinstance(radius, tuple) else (radius, radius)
        for k in range(sides):
            a = k * 2 * math.pi / sides
            verts.append(p + across * math.cos(a) * rx + depth * math.sin(a) * rz)
            weights.append(group)
            if j:
                i = j * sides + k; prev = (k + 1) % sides + j * sides
                faces.append((i - sides, prev - sides, prev, i))
    faces.extend([tuple(reversed(range(sides))), tuple(range(len(verts) - sides, len(verts)))])
    return part(name, verts, faces, weights, colour)


def ellipsoid(name, centre, scale, group, colour, rings=8, sides=16):
    points, radii = [], []
    for j in range(rings + 1):
        a = .025 + (math.pi - .05) * j / rings
        points.append(Vector(centre) + Vector((0, math.cos(a) * scale[1], 0)))
        radii.append((math.sin(a) * scale[0], math.sin(a) * scale[2]))
    return tube(name, points, radii, [group] * len(points), colour, sides)


# Keep the costume authoring separate from native export and preview logic.
exec((Path(__file__).with_name('design_mesh.py')).read_text(), globals())

# Export split vertices at UV/normal seams. All weights reference the untouched
# native bone order and bind poses; the runtime validates that order by name.
V, N, UV, IX, W, TRI, SEGMENTS = [], [], [], [], [], [], []
for o in PARTS:
    start = len(V)
    m = o.data; m.calc_loop_triangles()
    for tri in m.loop_triangles:
        for li in reversed(tri.loops):
            vi = m.loops[li].vertex_index
            V.append(list(m.vertices[vi].co)); N.append(list(m.corner_normals[li].vector if m.polygons[tri.polygon_index].use_smooth else tri.normal))
            UV.append(list(m.uv_layers.active.data[li].uv))
            weights = sorted([(g.group, g.weight) for g in m.vertices[vi].groups if g.weight > .00001], key=lambda x: -x[1])[:4]
            total = sum(w for _, w in weights)
            IX.append([b for b, _ in weights] + [0] * (4 - len(weights)))
            W.append([w / total for _, w in weights] + [0] * (4 - len(weights)))
            TRI.append(len(V) - 1)
    SEGMENTS.append(dict(name=o.name, start=start, count=len(V) - start))
clips = {
    'idle': {'duration': 2.4, 'loop': True, 'keys': [
        {'t': 0, 'right': [30, 117, 32], 'left': [-24, 125, 30], 'spear': [.16, .98, .10]},
        {'t': 1.2, 'right': [30, 118, 32], 'left': [-24, 126, 30], 'spear': [.18, .98, .10]},
        {'t': 2.4, 'right': [30, 117, 32], 'left': [-24, 125, 30], 'spear': [.16, .98, .10]}]},
    'attack': {'duration': 1.6, 'loop': False, 'keys': [
        {'t': 0, 'right': [30, 117, 32], 'left': [-24, 125, 30], 'spear': [.16, .98, .10]},
        {'t': .30, 'right': [24, 130, 7], 'left': [-22, 133, 29], 'spear': [0, .10, 1]},
        {'t': .48, 'right': [23, 130, 54], 'left': [-20, 132, 26], 'spear': [0, .02, 1]},
        {'t': .85, 'right': [23, 130, 54], 'left': [-20, 132, 26], 'spear': [0, .02, 1]},
        {'t': 1.6, 'right': [30, 117, 32], 'left': [-24, 125, 30], 'spear': [.16, .98, .10]}]},
    'defend': {'duration': 1.1, 'loop': False, 'keys': [
        {'t': 0, 'right': [30, 117, 32], 'left': [-24, 125, 30], 'spear': [.16, .98, .10]},
        {'t': .16, 'right': [26, 125, 23], 'left': [-10, 140, 33], 'spear': [.1, .96, .25]},
        {'t': .65, 'right': [26, 125, 23], 'left': [-10, 140, 33], 'spear': [.1, .96, .25]},
        {'t': 1.1, 'right': [30, 117, 32], 'left': [-24, 125, 30], 'spear': [.16, .98, .10]}]}}
model = dict(version=1, donor='hellion', bones=BONES, bindposes=D['bindposes'], vertices=V, normals=N, uv=UV,
             indices=IX, weights=W, triangles=TRI, clips=clips, parts=SEGMENTS)
(OUT / 'shieldbreaker.json').write_text(json.dumps(model, separators=(',', ':')), encoding='utf-8')
print('Shieldbreaker exported:', len(V), 'vertices,', len(TRI) // 3, 'triangles,', len(BONES), 'bones')

# Render the written package, not the authoring objects, to catch export errors.
loaded = json.loads((OUT / 'shieldbreaker.json').read_text(encoding='utf-8'))
for o in PARTS: o.hide_render = True; o.hide_set(True)
packed = part('Shieldbreaker exported package', loaded['vertices'],
              [tuple(reversed(loaded['triangles'][i:i+3])) for i in range(0, len(loaded['triangles']), 3)],
              [[(b,w) for b,w in zip(ii,ww) if w > .00001] for ii,ww in zip(loaded['indices'],loaded['weights'])], uv=loaded['uv'])
for loop in packed.data.loops: packed.data.uv_layers.active.data[loop.index].uv = loaded['uv'][loop.vertex_index]
packed.data.materials.append(mat)
for polygon in packed.data.polygons: polygon.use_smooth = True
packed.data.normals_split_custom_set_from_vertices(loaded['normals'])

# Build a rig for editable offline inspection. Scene units are centimetres, Y up,
# matching the source mesh and runtime instead of silently rescaling its binds.
arm_data = bpy.data.armatures.new('Hellion donor rig'); arm = bpy.data.objects.new('Shieldbreaker rig', arm_data)
bpy.context.collection.objects.link(arm); bpy.context.view_layer.objects.active = arm; arm.select_set(True)
bpy.ops.object.mode_set(mode='EDIT')
for i, n in enumerate(BONES):
    b = arm_data.edit_bones.new(n); b.matrix = REST[i]; b.length = 4
for i, parent in enumerate(D['parents']):
    if parent >= 0: arm_data.edit_bones[BONES[i]].parent = arm_data.edit_bones[BONES[parent]]
bpy.ops.object.mode_set(mode='OBJECT')
for o in PARTS:
    mod = o.modifiers.new('Native skinning', 'ARMATURE'); mod.object = arm

def aim(name, direction):
    pb = arm.pose.bones[name]
    m = pb.matrix.copy(); delta = (m.to_3x3() @ Vector((0, 1, 0))).rotation_difference(direction)
    pb.matrix = Matrix.Translation(m.translation) @ delta.to_matrix().to_4x4() @ m.to_3x3().to_4x4()
    bpy.context.view_layer.update()

def solve(side, target):
    sn, en, wn = [f'{side}_Arm_{n}SHJnt' for n in ('Shoulder', 'Elbow', 'Wrist')]
    shoulder = arm.pose.bones[sn].matrix.translation.copy()
    elbow = arm.pose.bones[en].matrix.translation.copy()
    wrist = arm.pose.bones[wn].matrix.translation.copy()
    a, b = (elbow - shoulder).length, (wrist - elbow).length
    axis = (Vector(target) - shoulder).normalized(); distance = min((Vector(target) - shoulder).length, a + b - .01)
    pole = Vector((-1 if side == 'l' else 1, -1, 0)); bend = (pole - axis * pole.dot(axis)).normalized()
    along = (a*a - b*b + distance*distance) / (2 * distance)
    goal = shoulder + axis * along + bend * math.sqrt(max(0, a*a - along*along))
    pb = arm.pose.bones[sn]; old = pb.matrix.copy()
    delta = (elbow - shoulder).rotation_difference(goal - shoulder)
    pb.matrix = Matrix.Translation(shoulder) @ delta.to_matrix().to_4x4() @ old.to_3x3().to_4x4(); bpy.context.view_layer.update()
    pb = arm.pose.bones[en]; old = pb.matrix.copy(); elbow = old.translation
    wrist = arm.pose.bones[wn].matrix.translation.copy()
    delta = (wrist - elbow).rotation_difference(shoulder + axis * distance - elbow)
    pb.matrix = Matrix.Translation(elbow) @ delta.to_matrix().to_4x4() @ old.to_3x3().to_4x4(); bpy.context.view_layer.update()

scene = bpy.context.scene; scene.render.engine = 'CYCLES'; scene.cycles.samples = 24
scene.world.color = (.20, .20, .20)
scene.render.resolution_x = 720; scene.render.resolution_y = 840; scene.render.resolution_percentage = 100
scene.view_settings.view_transform = 'Standard'
def point(o, target):
    forward = (Vector(target) - o.location).normalized()
    right = forward.cross(Vector((0, 1, 0))).normalized(); up = right.cross(forward)
    o.rotation_euler = Matrix((right, up, -forward)).transposed().to_euler()
bpy.ops.object.camera_add(location=(310, 150, 480)); cam = bpy.context.object; point(cam, (0, 130, 25)); cam.data.type = 'ORTHO'; cam.data.ortho_scale = 310; scene.camera = cam
for location, energy, size in [((-220, 300, 400), 1100000, 220), ((200, 180, -200), 500000, 180)]:
    bpy.ops.object.light_add(type='AREA', location=location); light = bpy.context.object; light.data.energy = energy; light.data.shape = 'DISK'; light.data.size = size; point(light, (0, 100, 0))
scene.render.film_transparent = False
for label, key in [('idle', clips['idle']['keys'][0]), ('attack', clips['attack']['keys'][2]), ('defend', clips['defend']['keys'][1])]:
    for pb in arm.pose.bones: pb.matrix_basis = Matrix.Identity(4)
    bpy.context.view_layer.update()
    solve('r', key['right']); solve('l', key['left'])
    pb = arm.pose.bones['r_Arm_WristSHJnt']
    pb.matrix = Matrix.Translation(pb.matrix.translation) @ Vector((0, 1, 0)).rotation_difference(Vector(key['spear']).normalized()).to_matrix().to_4x4() @ arm.data.bones['r_Arm_WristSHJnt'].matrix_local.to_3x3().to_4x4()
    bpy.context.view_layer.update()
    scene.render.filepath = str(OUT / ('preview_' + label + '.png')); bpy.ops.render.render(write_still=True)
bpy.ops.wm.save_as_mainfile(filepath=str(OUT / 'shieldbreaker.blend'))
