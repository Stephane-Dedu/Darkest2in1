"""Build the owner's private Vilhelm model with the original c0000 rig and selected HKX clips.

Requires the local DS3 extraction kit; never writes into the DS3 installation.
Example: python prepare_model.py --kit C:/Users/Piral/ds3-extract --out <private folder>
"""
import argparse
import hashlib
import json
import shutil
import sys
from pathlib import Path

import numpy as np

parser = argparse.ArgumentParser(description=__doc__)
parser.add_argument('--kit', type=Path, required=True)
parser.add_argument('--out', type=Path, required=True)
args = parser.parse_args()
kit = args.kit.resolve()
out = args.out.resolve()
if out == kit or kit in out.parents:
    raise ValueError('Use a separate private output folder')
sys.path.insert(0, str(kit / 'tools'))
import assemble_parts as assembler
from export_gltf import export, bone_local, find_png
from hkx_skeleton import read_skeletons, quat_to_matrix, Packfile
from hkx_anim import read_clip
from souls_containers import bnd4_read, dcx_decompress

wanted = {
    'a002_000000', 'a002_004000', 'a002_020000', 'a025_030000', 'a025_030010', 'a025_030100',
    'a245_038700', 'a000_042003', 'a000_042103', 'a000_042303',
    'a000_005002', 'a000_017002', 'a000_018002'
}
spec = json.loads((kit / 'tools/specs/sir_vilhelm.json').read_text())
spec['out_dir'] = str(out)
def absolute(p):
    return str((kit / p).resolve())
for key in ('c0000_dir', 'game_dir'):
    spec[key] = absolute(spec[key])
spec['png_dirs'] = [absolute(p) for p in spec['png_dirs']]
spec['parts'] = [absolute(p) for p in spec['parts']]
for weapon in spec['weapons']:
    weapon['flver'] = absolute(weapon['flver'])
out.mkdir(parents=True, exist_ok=True)
private_spec = out / 'assemble-spec.json'
private_spec.write_text(json.dumps(spec, indent=2))
captured = []
original_export = assembler.export
original_argv = sys.argv
try:
    assembler.export = lambda model, *a, **kw: captured.append(model)
    sys.argv = ['assemble_parts.py', str(private_spec)]
    assembler.main()
finally:
    assembler.export = original_export
    sys.argv = original_argv
model = captured[0]
# FLVER weapon geometry points down local Y. The animation attachment uses
# forward local Z. Convert the equipment mount once, around the grip, rather
# than rotating the hand or changing the original animation tracks.
mount = np.array([[1., 0., 0.], [0., 0., 1.], [0., -1., 0.]])
worlds = assembler.worlds(model['bones'])
weapon_index = next(i for i, b in enumerate(model['bones']) if b['name'] == 'R_Weapon')
weapon_world = worlds[weapon_index]
rotation = weapon_world[:3, :3]
origin = weapon_world[:3, 3]
mount_meshes = 0
for mesh in model['meshes']:
    material = model['materials'][mesh['material']]['name']
    if not material.startswith('WP_A_0224'):
        continue
    vertices = mesh['vertices']
    local = (vertices['position'] - origin) @ rotation
    vertices['position'] = local @ mount.T @ rotation.T + origin
    vertices['normal'] = vertices['normal'] @ rotation @ mount.T @ rotation.T
    for tangent in vertices.get('tangent', []):
        tangent[:, :3] = tangent[:, :3] @ rotation @ mount.T @ rotation.T
    mount_meshes += 1
if mount_meshes != 1:
    raise ValueError('Expected the one original Onyx Blade equipment mesh')
skeleton_path = kit / 'unpacked/c0000/c0000.HKX'
skel = next(s for s in read_skeletons(skeleton_path.read_bytes()) if 'R_Weapon' in s['bones'])
indices = {b['name']: i for i, b in enumerate(model['bones'])}
native = {name: i for i, name in enumerate(skel['bones'])}
if not set(native) <= set(indices):
    raise ValueError('Assembled equipment is missing native character bones')
# Havok tracks use the behavior skeleton's reference TRS, not the armor FLVER's
# Euler representation. Keep armor/weapon geometry, weights and extra cloth bones.
for bone in model['bones']:
    if bone['name'] not in native:
        continue
    index = native[bone['name']]
    parent = skel['parents'][index]
    bone['parent'] = indices[skel['bones'][parent]] if parent >= 0 else -1
    pose = skel['pose'][index]
    matrix = np.eye(4)
    matrix[:3, :3] = quat_to_matrix(pose['q']) @ np.diag(pose['s'])
    matrix[:3, 3] = pose['t']
    bone['matrix'] = matrix

clips, sources, hashes = {}, {}, {}
walk_motion=None
for binder in sorted((kit / 'raw/chr').glob('c0000*.anibnd.dcx')):
    data = binder.read_bytes()
    for _, name, contents in bnd4_read(dcx_decompress(data)):
        clip_name = name.replace('\\', '/').rsplit('/', 1)[-1].removesuffix('.hkx')
        if clip_name not in wanted:
            continue
        clip = read_clip(contents)
        if clip and clip['blend_hint'] == 0:
            clips[clip_name] = clip
            sources[clip_name] = binder.name
            hashes[binder.name] = hashlib.sha256(data).hexdigest()
            if clip_name=='a002_020000':
                packfile=Packfile(contents)
                reference=packfile.of_class('hkaDefaultAnimatedReferenceFrame')
                if len(reference)!=1: raise ValueError('Missing native locomotion reference frame')
                offset,count=packfile.arr(reference[0]+0x48)
                samples=np.array(packfile.u(f'{count*4}f',offset)).reshape(count,4)
                distances=np.r_[0,np.cumsum(np.linalg.norm(np.diff(samples[:,[0,2]],axis=0),axis=1))]
                if count!=clip['frames'] or not np.all(np.diff(distances)>0): raise ValueError('Invalid native walking distances')
                walk_motion={'clip':clip_name,'duration':clip['duration'],'distances':distances.tolist()}
if set(clips) != wanted:
    raise ValueError('Native animation files missing: ' + ', '.join(sorted(wanted - set(clips))))
animations, report = [], []
for name, clip in sorted(clips.items()):
    tracks = {}
    for track, bone in enumerate(clip['bones'] or range(clip['T'].shape[1])):
        if bone < 0:
            continue
        tracks[indices[skel['bones'][bone]]] = (clip['T'][:, track], clip['R'][:, track], clip['S'][:, track])
    animations.append({'name': name, 'times': np.arange(clip['frames']) * clip['frame_duration'], 'tracks': tracks})
    report.append({'clip': name, 'duration': clip['duration'], 'frames': clip['frames'], 'source': sources[name]})
export(model, spec['png_dirs'], str(out), 'sir_vilhelm', spec['labels'], 'sir_vilhelm', animations=animations)
# The runtime uses DD2's painted shader, which needs diffuse color rather than
# the specular-to-metallic PBR bake. Preserve the original native diffuse PNGs.
gltf_path=out/'sir_vilhelm.gltf'
document=json.loads(gltf_path.read_text())
for index, material in enumerate(model['materials']):
    for texture in material['textures']:
        if not any(slot in texture['type'] for slot in ('Diffuse','Albedo')) or 'Detail' in texture['type']:
            continue
        stem=Path(texture['path'].replace('\\','/')).stem
        native_diffuse=find_png(spec['png_dirs'],stem)
        if not native_diffuse:
            continue
        filename=stem+'.png'
        shutil.copy2(native_diffuse,out/'textures'/filename)
        image=len(document['images']); document['images'].append({'uri':'textures/'+filename})
        tex=len(document['textures']); document['textures'].append({'source':image,'sampler':0})
        document['materials'][index]['pbrMetallicRoughness']['baseColorTexture']={'index':tex}
        break
gltf_path.write_text(json.dumps(document,indent=1))
if walk_motion is None: raise ValueError('Native walking motion is missing')
(out/'motion.json').write_text(json.dumps(walk_motion,indent=2))
hashes['c0000.HKX'] = hashlib.sha256(skeleton_path.read_bytes()).hexdigest()
(out / 'animation-report.json').write_text(json.dumps({'clips': report, 'source_sha256': hashes,
    'weapon_mount': {'native_axis': '-Y', 'animation_axis': '+Z', 'rotation_x_degrees': -90, 'pivot': 'R_Weapon'}}, indent=2))
print(f'Prepared {len(animations)} original clips; {len(model["bones"])} bones; output {out}', flush=True)
