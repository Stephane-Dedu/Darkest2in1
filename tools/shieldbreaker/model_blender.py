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
                layer.data[li].uv = (.5 + (colour + .1 + .8 * s) / 32, .05 + .9 * t)
        poly.use_smooth = name == 'body'
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


# Retain weighted donor boot detail. Rebuild the body, clothing, covered face and
# weapons to the donor's proportions and binds. The left hand is absent, matching
# her DD1 in-game design; the shield is bound to that forearm instead.
dominant = [BONES[max(zip(ii, ww), key=lambda x: x[1])[0]] for ii, ww in zip(D['indices'], D['weights'])]
remove = ('hair', 'skirt', 'ribbon', 'leaf', 'bone_', 'scarf', 'sleeve', 'Tailbone')
faces = []
for j in range(0, len(D['triangles']), 3):
    tri = D['triangles'][j:j + 3]
    ns = [dominant[i] for i in tri]
    if any(any(t in n for t in remove) for n in ns): continue
    if any(n.startswith(('l_Finger', 'l_Thumb')) or n == 'l_Arm_WristSHJnt' for n in ns): continue
    def keep(i):
        x, y, z = D['vertices'][i]; n = dominant[i]
        return y < 12
    if not all(keep(i) for i in tri): continue
    faces.append(tuple(reversed(tri)))
body = part('body', D['vertices'], faces,
            [[(b, w) for b, w in zip(ii, ww) if w > .00001] for ii, ww in zip(D['indices'], D['weights'])], uv=D['uv'])

# Smooth connections under the clothing. Keep the original rig and boot detail
# while replacing the donor's arm armour and face silhouette.
for side in ('l', 'r'):
    shoulder, elbow, wrist = [POS[f'{side}_Arm_{n}SHJnt'] for n in ('Shoulder', 'Elbow', 'Wrist')]
    tube(side + '_arm', [shoulder, shoulder.lerp(elbow, .35), elbow, elbow.lerp(wrist, .55), wrist],
         [5.8, 5.6, 4.3, 4.2, 3.5], [rigid(f'{side}_Arm_ShoulderSHJnt')] * 2 +
         [[(bone(f'{side}_Arm_ShoulderSHJnt'), .2), (bone(f'{side}_Arm_ElbowSHJnt'), .8)],
          rigid(f'{side}_Arm_ElbowSHJnt'), rigid(f'{side}_Arm_ElbowSHJnt')], 12, 12)
tube('neck', [(0, 143, 4), (0, 155, 4), (0, 164, 4)], [6, 5, 5],
     [rigid('Spine_TopSHJnt'), rigid('Head_TopSHJnt'), rigid('Head_TopSHJnt')], 12, 12)
ellipsoid('head', POS['Head_TopSHJnt'] + Vector((0, 3, 0)), (8.4, 11, 8.2), rigid('Head_TopSHJnt'), 12, 10, 16)
# A clear eye opening between the turban and veil.
for x in (-3.4, 3.4):
    ellipsoid('eye_socket', POS['Head_TopSHJnt'] + Vector((x, 3.3, 8.0)), (2.7, 1.1, 1), rigid('Head_TopSHJnt'), 11, 4, 8)
    ellipsoid('eye', POS['Head_TopSHJnt'] + Vector((x, 3.3, 8.9)), (.55, .6, .3), rigid('Head_TopSHJnt'), 6, 4, 8)

# Brown sleeveless tunic, broad folded white scarf and ochre sash.
ys = [94, 102, 109, 120, 131, 141, 146]
tube('tunic', [(0, y, 3) for y in ys], [(17, 12), (16, 11), (15, 10), (13, 9), (16, 10), (20, 10), (14, 8)],
     [rigid('ROOTSHJnt'), rigid('ROOTSHJnt'), rigid('ROOTSHJnt'), rigid('Spine_01SHJnt'), rigid('Spine_02SHJnt'), rigid('Spine_TopSHJnt'), rigid('Spine_TopSHJnt')], 1, 16)
for j in range(5):
    y = 105 + j * 2
    tube('sash_fold_' + str(j), [(0, y, 3), (0, y + 1.7, 3)], [(16.4, 11.6)] * 2, [rigid('ROOTSHJnt')] * 2, 3 if j % 2 else 4, 16)
for side in ('l', 'r'):
    hip, knee, ankle = [POS[f'{side}_Leg_{n}SHJnt'] for n in ('Hip', 'Knee', 'Ankle')]
    points = [hip, hip.lerp(knee, .35), knee, knee.lerp(ankle, .45), ankle + Vector((0, 4, 0))]
    groups = [rigid(f'{side}_Leg_HipSHJnt'), rigid(f'{side}_Leg_HipSHJnt'),
              [(bone(f'{side}_Leg_HipSHJnt'), .25), (bone(f'{side}_Leg_KneeSHJnt'), .75)],
              rigid(f'{side}_Leg_KneeSHJnt'), rigid(f'{side}_Leg_AnkleSHJnt')]
    tube(side + '_trousers', points, [(12, 11), (13, 11), (10, 9), (9, 7), (5, 5)], groups, 3, 12)
    for j in range(6):
        p = ankle.lerp(knee, .05 + j * .035)
        tube(side + '_ankle_wrap_' + str(j), [p, p + Vector((0, 1.1, 0))], [5.6, 5.6], [rigid(f'{side}_Leg_AnkleSHJnt')] * 2, 5 if j % 2 else 6, 10)
    tube(side + '_boot_collar', [ankle + Vector((0, -6, 0)), ankle + Vector((0, 5, 0))], [5, 5],
         [rigid(f'{side}_Leg_AnkleSHJnt')] * 2, 11, 10)

# Turban and layered fabric bands. Its lower edge leaves a narrow eye opening.
head = POS['Head_TopSHJnt']
ellipsoid('turban', head + Vector((0, 13, 0)), (11.3, 8, 10.8), rigid('Head_TopSHJnt'), 5)
for j in range(6):
    points = []
    for k in range(33):
        a = k * 2 * math.pi / 32
        points.append(head + Vector((math.cos(a) * 10.3, 7 + j * 1.5 + math.sin(a + j * .3) * 1.2, math.sin(a) * 10.1)))
    tube('turban_fold_' + str(j), points, [.8] * len(points), [rigid('Head_TopSHJnt')] * len(points), 6 if j % 2 else 5, 5)
tube('veil', [head + Vector((0, 1, 7)), head + Vector((0, -5, 8)), head + Vector((0, -13, 5))],
     [(8, 2.8), (7, 3), (4, 2)], [rigid('Head_TopSHJnt')] * 3, 4, 12)
for j in range(4):
    tube('scarf_fold_' + str(j), [(-10 - j, 150 - j * 4, 8), (-5, 139 - j * 3, 15),
                               (8, 143 - j * 3, 14), (15, 154 - j * 2, 4)], [2.5] * 4,
         [rigid('Spine_TopSHJnt')] * 4, 6 if j % 2 else 5, 6)
# Left forearm bandages and sealed stump, right-hand green bangles.
for side in ('l', 'r'):
    elbow, wrist = POS[f'{side}_Arm_ElbowSHJnt'], POS[f'{side}_Arm_WristSHJnt']
    for j in range(7 if side == 'l' else 3):
        p = elbow.lerp(wrist, .25 + j * .075 if side == 'l' else .78 + j * .06)
        tube(side + '_arm_wrap_' + str(j), [p, p + (wrist - elbow).normalized() * 1.4], [4.5, 4.5],
             [rigid(f'{side}_Arm_ElbowSHJnt')] * 2, (5 if j % 2 else 6) if side == 'l' else 7, 10)
ellipsoid('left_stump', POS['l_Arm_WristSHJnt'] + Vector((1.5, 3, -3)), (4.2, 4.2, 4.2), rigid('l_Arm_ElbowSHJnt'), 5, 5, 10)

# Wooden spear, wrapped grip and angular steel leaf blade. Rigidly bound to the
# right wrist, so animation cannot slide the weapon away from the hand.
grip = POS['r_Arm_WristSHJnt'] + Vector((0, -1, 2))
ellipsoid('spear_hand', grip + Vector((0, 0, -1.4)), (3.2, 4.2, 2.6), rigid('r_Arm_WristSHJnt'), 12, 6, 10)
tube('spear_shaft', [grip + Vector((0, -72, 0)), grip + Vector((0, 115, 0))], [1.2, 1.0], [rigid('r_Arm_WristSHJnt')] * 2, 1, 8)
for j in range(14):
    p = grip + Vector((0, -8 + j * 1.1, 0))
    tube('spear_grip_' + str(j), [p, p + Vector((0, .65, 0))], [1.55, 1.55], [rigid('r_Arm_WristSHJnt')] * 2, 11 if j % 2 else 2, 6)
base = grip + Vector((0, 114, 0))
verts = [base + Vector(v) for v in [(-1.7, 0, 0), (-5, 11, 0), (0, 34, 0), (5, 11, 0), (0, 11, 1.4), (0, 11, -1.4)]]
part('spear_blade', verts, [(0, 1, 4), (1, 2, 4), (2, 3, 4), (3, 0, 4), (1, 0, 5), (2, 1, 5), (3, 2, 5), (0, 3, 5)], [rigid('r_Arm_WristSHJnt')] * 6, 10)

# Shield strapped to the left forearm, with a dark rim, inset wood and green boss.
shield_c = POS['l_Arm_ElbowSHJnt'].lerp(POS['l_Arm_WristSHJnt'], .62) + Vector((0, 0, 9))
shield_group = rigid('l_Arm_ElbowSHJnt')
def disk(name, radius, depth, colour, z=0):
    return tube(name, [shield_c + Vector((0, 0, z)), shield_c + Vector((0, 0, z + depth))],
                [radius, radius], [shield_group] * 2, colour, 24)
disk('shield_rim', 22, 2.5, 9)
disk('shield_wood', 20, 1.2, 2, 2.5)
disk('shield_boss_rim', 7.5, 1.4, 11, 3.7)
disk('shield_boss', 6.5, 2, 7, 5)
for j in range(8):
    a = j * math.pi / 4
    ellipsoid('shield_rivet_' + str(j), shield_c + Vector((math.cos(a) * 20.7, math.sin(a) * 20.7, 4)),
              (1.0, 1.0, .7), shield_group, 10, 4, 6)
# Stylized serpent slash on the boss and wood plank seams.
for j, pts in enumerate([[(-3, -4), (3, -2), (-3, 1), (2, 4)], [(-11, -15), (-11, 15)], [(11, -15), (11, 15)]]):
    points = [shield_c + Vector((x, y, 7.2 if j == 0 else 4)) for x, y in pts]
    tube('shield_mark_' + str(j), points, [.65] * len(points), [shield_group] * len(points), 8 if j == 0 else 0, 5)

# Atlas: preserve donor skin detail on the left, use painted colour swatches on
# the right. No original game textures or geometry are written into the repo.
base = bpy.data.images.load(str(OUT / 'tex_hellion_col.png'))
ink = bpy.data.images.load(str(OUT / 'tex_hellion_ink.png'))
import numpy as np
for source, filename, is_ink in [(base, 'shieldbreaker_base.png', False), (ink, 'shieldbreaker_ink.png', True)]:
    if source.size[0] > 2048: source.scale(2048, 2048)
    w, h = source.size
    old = np.array(source.pixels[:], dtype=np.float32).reshape(h, w, 4)
    result = np.ones((h, w * 2, 4), dtype=np.float32)
    if not is_ink:
        # Remove Hellion's blue war paint, darken the skin, leave its painted value detail.
        blue = (old[:, :, 2] > old[:, :, 0] * 1.05) & (old[:, :, 1] > old[:, :, 0] * 1.05)
        value = old[:, :, :3].max(axis=2)
        old[blue, :3] = value[blue, None] * np.array([.76, .48, .28])
        old[:, :, :3] *= np.array([.82, .76, .68])
    result[:, :w] = old
    for i, c in enumerate(PALETTE):
        x0 = w + int(i * w / 16); x1 = w + int((i + 1) * w / 16)
        if is_ink:
            result[:, x0:x1] = (1, 1, 1, 1)
        else:
            yy, xx = np.mgrid[0:h, 0:x1-x0]; u = xx / (x1-x0); v = yy / h
            fold = np.exp(-((u - .22 - .035 * np.sin(v * 18)) / .018) ** 2)
            fold += .7 * np.exp(-((u - .72 - .04 * np.sin(v * 11)) / .012) ** 2)
            shade = .83 + .15 * u + .06 * np.sin(v * 31 + u * 3) - (.25 if i != 12 else .07) * fold
            shade += .025 * np.sin(yy * .073 + xx * .24) * np.sin(xx * .31 - yy * .022)
            result[:, x0:x1, :3] = np.array(c[:3])[None, None, :] * shade[:, :, None]
            result[:, x0:x1, 3] = 1
    image = bpy.data.images.new(filename, width=w * 2, height=h)
    image.pixels.foreach_set(result.ravel()); image.filepath_raw = str(OUT / filename); image.file_format = 'PNG'; image.save()
    if not is_ink: ATLAS = image

mat = bpy.data.materials.new('Shieldbreaker painted'); mat.use_nodes = True
nodes = mat.node_tree.nodes
tex = nodes.new('ShaderNodeTexImage'); tex.image = ATLAS
mat.node_tree.links.new(tex.outputs['Color'], nodes.get('Principled BSDF').inputs['Base Color'])
nodes.get('Principled BSDF').inputs['Roughness'].default_value = 1
for o in PARTS: o.data.materials.append(mat)

# Export split vertices at UV/normal seams. All weights reference the untouched
# native bone order and bind poses; the runtime validates that order by name.
V, N, UV, IX, W, TRI, SEGMENTS = [], [], [], [], [], [], []
for o in PARTS:
    start = len(V)
    m = o.data; m.calc_loop_triangles()
    for tri in m.loop_triangles:
        for li in reversed(tri.loops):
            vi = m.loops[li].vertex_index
            V.append(list(m.vertices[vi].co)); N.append(list(m.vertices[vi].normal if o.name == 'body' else tri.normal))
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
