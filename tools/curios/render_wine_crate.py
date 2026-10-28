"""Render a complete native crate with one generated bottle card inside it.

Native geometry, UVs, paint and tint stay fixed. The used state hides only the
generated bottle card. Native depth determines which boards obscure bottles;
placing bottles behind the whole flattened crate incorrectly covers them with
the back wall. These are design assets, not DLC gameplay acceptance.
"""
import argparse
import hashlib
import json
import math
from pathlib import Path
import sys

import bpy
import numpy as np
from bpy_extras.object_utils import world_to_camera_view
from mathutils import Vector


def render(source, bottles, native_tools, output):
    source, bottles, native_tools, output = [Path(p).resolve() for p in
                                            (source, bottles, native_tools, output)]
    repo = Path(__file__).resolve().parents[2]
    if output.exists() or any(p.is_relative_to(repo) for p in (source, bottles, output)):
        raise ValueError('Use private reviewed inputs and a new private output')
    sha = lambda p: hashlib.sha256(Path(p).read_bytes()).hexdigest()
    document = json.loads((source / 'scene.json').read_text())
    if sha(source / 'scene.json') != '35124d2e3a57d6cb513b3b625c492d6a48a19c8b9897ca61834b86852385bf2c':
        raise ValueError('Use the complete reviewed native crate source')
    if document['skipped'] or len(document['objects']) != 1:
        raise ValueError('Incomplete native source')
    body = document['objects'][0]
    material = document['materials'][body['materials'][0]]
    if not body['active'] or not body['enabled'] or material['tint'] != [
            0.4540000259876251, 0.4540000259876251, 0.4540000259876251, 1.0]:
        raise ValueError('Preserve native visibility and tint')
    mesh_path = source / ('mesh-' + body['mesh'] + '.npz')
    if sha(mesh_path) != '0704dacec9cd66558bfde2431771d4aace87924fad83569b2723361fd1716d16' \
            or sha(source / material['texture']) != '1b5afe20d7d25c3b08ae1022b7919da0d4e3d6bb054d3fbd26d2a583b8060d39':
        raise ValueError('Preserve all source vertices, faces, UVs and full paint')
    mesh = np.load(mesh_path)
    if mesh['v'].shape != (1040, 3) or mesh['uv'].shape != (1040, 2) \
            or mesh['tri'].shape != (724, 3):
        raise ValueError('Preserve the complete native mesh and UVs')
    if sha(bottles) != 'b4eda82ad980456b84685dfb0402d2844dcad5e257d23d9b48df1bc2d5a8076b':
        raise ValueError('Use the reviewed generated bottle component')
    sys.path.insert(0, str(native_tools))
    import bl_lib as L
    L.reset()
    place, _ = L.place_matrix(source, (0, 0, 0), rot=30, respect_active=True)
    objects, low, high = L.build(source, place=place, respect_active=True)
    if len(objects) != 1 or len(objects[0].data.vertices) != 1040 \
            or len(objects[0].data.polygons) != 724:
        raise ValueError('Native body must remain complete')
    native = objects[0]
    for mat in native.data.materials:
        nodes = mat.node_tree
        out = next(n for n in nodes.nodes if n.type == 'OUTPUT_MATERIAL')
        surface = out.inputs['Surface'].links[0].from_socket
        geometry = nodes.nodes.new('ShaderNodeNewGeometry')
        transparent = nodes.nodes.new('ShaderNodeBsdfTransparent')
        mix = nodes.nodes.new('ShaderNodeMixShader')
        nodes.links.new(geometry.outputs['Backfacing'], mix.inputs[0])
        nodes.links.new(surface, mix.inputs[1])
        nodes.links.new(transparent.outputs[0], mix.inputs[2])
        nodes.links.new(mix.outputs[0], out.inputs[0])
    image = bpy.data.images.load(str(bottles), check_existing=False)
    if tuple(image.size) != (1254, 1254):
        raise ValueError('Do not register a drifting component canvas')
    crop = (221, 136, 1081, 1126)
    # Image rows are bottom-up here. Retain every visible source alpha pixel.
    alpha = np.asarray(image.pixels[:]).reshape(1254, 1254, 4)[:, :, 3]
    keep = np.zeros(alpha.shape, dtype=bool)
    keep[1254-crop[3]:1254-crop[1], crop[0]:crop[2]] = True
    if np.any(alpha[~keep] > 8/255):
        raise ValueError('The reviewed crop would remove visible bottle paint')
    height = .9
    width = height * (crop[2]-crop[0]) / (crop[3]-crop[1])
    cx, cy = (low[0]+high[0])/2, (low[1]+high[1])/2
    bottom = low[2] + .05
    vertices = [(cx-width/2, cy, bottom), (cx+width/2, cy, bottom),
                (cx+width/2, cy, bottom+height), (cx-width/2, cy, bottom+height)]
    card_mesh = bpy.data.meshes.new('generated_bottle_card')
    card_mesh.from_pydata(vertices, [], [(0, 1, 2, 3)])
    card = bpy.data.objects.new('generated_bottles_ONLY', card_mesh)
    bpy.context.collection.objects.link(card)
    uv = card_mesh.uv_layers.new()
    values = [(crop[0]/1254, 1-crop[3]/1254), (crop[2]/1254, 1-crop[3]/1254),
              (crop[2]/1254, 1-crop[1]/1254), (crop[0]/1254, 1-crop[1]/1254)]
    for i, value in enumerate(values):
        uv.data[i].uv = value
    mat = bpy.data.materials.new('generated_bottle_alpha_emission')
    mat.use_nodes = True
    nt = mat.node_tree
    nt.nodes.clear()
    texture = nt.nodes.new('ShaderNodeTexImage')
    texture.image = image
    emission = nt.nodes.new('ShaderNodeEmission')
    transparent = nt.nodes.new('ShaderNodeBsdfTransparent')
    mix = nt.nodes.new('ShaderNodeMixShader')
    out = nt.nodes.new('ShaderNodeOutputMaterial')
    nt.links.new(texture.outputs['Color'], emission.inputs['Color'])
    nt.links.new(texture.outputs['Alpha'], mix.inputs[0])
    nt.links.new(transparent.outputs[0], mix.inputs[1])
    nt.links.new(emission.outputs[0], mix.inputs[2])
    nt.links.new(mix.outputs[0], out.inputs[0])
    card.data.materials.append(mat)
    centre = (float(cx), float(cy), float((low[2]+max(high[2], bottom+height))/2))
    extent = max(high[0]-low[0], bottom+height-low[2]) * 1.14
    L.camera((cx, cy-12, centre[2]+12*math.tan(math.radians(10))), centre,
             ortho=float(extent), res=(1024, 1024))
    scene = bpy.context.scene
    scene.render.threads_mode = 'FIXED'
    scene.render.threads = 2
    scene.cycles.seed = 0
    bpy.context.view_layer.update()
    projected = [world_to_camera_view(scene, scene.camera, native.matrix_world @ v.co)
                 for v in native.data.vertices]
    foot = [512.0, max((1-p.y)*1024 for p in projected)]
    body_top = min((1-p.y)*1024 for p in projected)
    card_projection = [world_to_camera_view(scene, scene.camera, Vector(v)) for v in vertices]
    bounds = [max(0, math.floor(min(p.x*1024 for p in card_projection))-3),
              max(0, math.floor(min((1-p.y)*1024 for p in card_projection))-3),
              min(1024, math.ceil(max(p.x*1024 for p in card_projection))+3),
              min(1024, math.ceil(max((1-p.y)*1024 for p in card_projection))+3)]
    output.mkdir(parents=True)
    for state in ('closed', 'used'):
        card.hide_render = state == 'used'
        L.render(output / (state + '.png'), samples=4, transparent=True)
    states = [np.asarray(bpy.data.images.load(str(output / (s+'.png')),
              check_existing=False).pixels[:]).reshape(1024, 1024, 4)[::-1]
              for s in ('closed', 'used')]
    changed = np.any(states[0] != states[1], axis=2)
    domain = np.zeros(changed.shape, dtype=bool)
    domain[bounds[1]:bounds[3], bounds[0]:bounds[2]] = True
    if not changed.any() or changed.mean() > .2 or np.any(changed & ~domain) \
            or np.any(changed[820:]):
        raise ValueError('Only the bounded bottle card may change; preserve native feet')
    y, x = np.where(changed)
    record = {'native_geometry': [1040, 724], 'native_UV_vertices': 1040,
              'native_full_paint': 2048, 'native_body_pose_UV_paint_tint_fixed': True,
              'generated_card_only': True, 'generated_card_height': height,
              'component_crop': list(crop), 'source_foot': foot,
              'source_body_landmark_y': body_top,
              'projected_component_domain': bounds,
              'changed_pixels': int(changed.sum()),
              'changed_bounds': [int(x.min()), int(y.min()), int(x.max()+1), int(y.max()+1)],
              'all_RGBA_outside_projected_card_exact': True,
              'native_rows820_onward_exact': True,
              'source_scene_sha256': sha(source / 'scene.json'),
              'source_mesh_sha256': sha(mesh_path),
              'source_paint_sha256': sha(source / material['texture']),
              'component_sha256': sha(bottles), 'recipe_sha256': sha(__file__),
              'native_tools_sha256': sha(native_tools / 'bl_lib.py'),
              'native_Cull2_modeled_not_full_Unity_shader': True,
              'outputs': {p.name: sha(p) for p in output.glob('*.png')},
              'installed': False, 'accepted': False, 'canonical_DLC_gameplay_QA': False}
    (output / 'provenance.json').write_text(json.dumps(record, indent=2))
    return record


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    for name in ('source', 'bottles', 'native-tools', 'out'):
        parser.add_argument('--' + name, type=Path, required=True)
    args = parser.parse_args(sys.argv[sys.argv.index('--')+1:])
    print(json.dumps(render(args.source, args.bottles, args.native_tools, args.out)))
