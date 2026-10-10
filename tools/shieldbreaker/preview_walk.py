"""Render the written private model with samples from the actual corridor gait.

Run ModelAudit first, then blender --background shieldbreaker.blend --python-exit-code 1
--python tools/shieldbreaker/preview_walk.py -- <private model folder>.
This checks offline deformation; it does not verify Unity lighting or combat.
"""
import bpy
import json
import math
import sys
from pathlib import Path
from mathutils import Matrix, Vector, Quaternion

out = Path(sys.argv[sys.argv.index('--') + 1])
frames = json.loads((out / 'walk_samples.json').read_text())
model = json.loads((out / 'shieldbreaker.json').read_text())
arm = bpy.data.objects['Shieldbreaker rig']
rest = {b.name: b.matrix_local.copy() for b in arm.data.bones}
scene = bpy.context.scene
scene.render.resolution_x = 480; scene.render.resolution_y = 560
scene.cycles.samples = 8
scene.camera.data.ortho_scale = 310
folder = out / 'walk'; folder.mkdir(exist_ok=True)


def solve(names, target, pole):
    s, e, w = [arm.pose.bones[n] for n in names]
    start, mid, end = [p.matrix.translation.copy() for p in (s,e,w)]
    a, b = (mid-start).length, (end-mid).length
    delta = Vector(target)-start; axis = delta.normalized()
    d = min(max(delta.length, abs(a-b)+.01), a+b-.01)
    bend = (pole-axis*pole.dot(axis)).normalized()
    along = (a*a-b*b+d*d)/(2*d)
    goal = start+axis*along+bend*math.sqrt(max(0,a*a-along*along))
    q = (mid-start).rotation_difference(goal-start)
    s.matrix = Matrix.Translation(start) @ q.to_matrix().to_4x4() @ s.matrix.to_3x3().to_4x4()
    bpy.context.view_layer.update()
    mid, end = e.matrix.translation.copy(), w.matrix.translation.copy()
    q = (end-mid).rotation_difference(start+axis*d-mid)
    e.matrix = Matrix.Translation(mid) @ q.to_matrix().to_4x4() @ e.matrix.to_3x3().to_4x4()
    bpy.context.view_layer.update()


for i, frame in enumerate(frames):
    for p in arm.pose.bones: p.matrix_basis = Matrix.Identity(4)
    bpy.context.view_layer.update()
    root = arm.pose.bones['ROOTSHJnt']; m = root.matrix.copy()
    support = frame['support']; phase = frame['phase']
    m.translation += Vector((support*1.9, -1.3-.7*math.cos((phase-.06)*4*math.pi), 0))
    root.matrix = m @ Quaternion(Vector((0,1,0)), math.radians(support*2.3)).to_matrix().to_4x4()
    bpy.context.view_layer.update()
    for side, key in [('l','left'),('r','right')]:
        hip, knee, ankle = [f'{side}_Leg_{n}SHJnt' for n in ('Hip','Knee','Ankle')]
        length = (rest[knee].translation-rest[hip].translation).length + (rest[ankle].translation-rest[knee].translation).length
        f = frame[key]
        target = rest[ankle].translation + Vector((0, f['Lift']*length, f['Forward']*length))
        solve((hip,knee,ankle), target, Vector((0,0,1)))
        p = arm.pose.bones[ankle]
        p.matrix = Matrix.Translation(p.matrix.translation) @ Quaternion(Vector((1,0,0)), math.radians(-f['Pitch'])).to_matrix().to_4x4() @ rest[ankle].to_3x3().to_4x4()
    bpy.context.view_layer.update()
    chest = arm.pose.bones['Spine_02SHJnt'].matrix @ rest['Spine_02SHJnt'].inverted()
    key = model['clips']['idle']['keys'][0]
    for side, target in [('r',key['right']),('l',key['left'])]:
        solve(tuple(f'{side}_Arm_{n}SHJnt' for n in ('Shoulder','Elbow','Wrist')), chest @ Vector(target), Vector((-1 if side=='l' else 1,-1,0)))
    wrist = arm.pose.bones['r_Arm_WristSHJnt']
    direction = (chest.to_3x3() @ Vector(key['spear'])).normalized()
    wrist.matrix = Matrix.Translation(wrist.matrix.translation) @ Vector((0,1,0)).rotation_difference(direction).to_matrix().to_4x4() @ rest['r_Arm_WristSHJnt'].to_3x3().to_4x4()
    bpy.context.view_layer.update()
    scene.render.filepath = str(folder / f'{i:03}.png'); bpy.ops.render.render(write_still=True)
print('Rendered 24 corridor gait samples from the exported package')
