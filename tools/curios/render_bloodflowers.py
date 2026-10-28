"""Render complete native leaves with a generated, removable flower sprig.

Native geometry, UVs, full paint and actual FBX tint remain fixed in both
states. Only the missing flower component is generated. Native depth handles
leaf/stem occlusion. This prepares a DLC design, not canonical gameplay QA.
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


def render(source, flowers, native_tools, output):
    source, flowers, native_tools, output = [Path(p).resolve() for p in
                                            (source, flowers, native_tools, output)]
    repo = Path(__file__).resolve().parents[2]
    if output.exists() or any(p.is_relative_to(repo) for p in (source, flowers, output)):
        raise ValueError('Use reviewed private inputs and a new private output')
    sha = lambda p: hashlib.sha256(Path(p).read_bytes()).hexdigest()
    if sha(source / 'scene.json') != '48f66e86a223accaa4ea6f5db93bd5f6c86e25d8a2e33cf1943bb2d02b23eb46' or sha(flowers) != '86c48a18960a8399f4d3c668518cdc3bd2a0d0c5806de3dd9deea0b0282bc2c0':
        raise ValueError('Use the reviewed complete leaf body and original sprig')
    document = json.loads((source / 'scene.json').read_text())
    if document['skipped'] or len(document['objects']) != 1:
        raise ValueError('Complete independent native body required')
    body = document['objects'][0]
    mat = document['materials'][body['materials'][0]]
    mesh = source / ('mesh-' + body['mesh'] + '.npz')
    if sha(mesh) != 'eea4e7e92635571980a4604d333cfc4a505fc246018ed33971db8bd58244229e' or sha(mat['texture']) != 'e2faa5401401e4625917e5d48575ecc9e9e11b07c38ebee08af269f668319c79' \
            or mat['tint'] != [0.8500000238418579] * 3 + [1.0] \
            or not body['active'] or not body['enabled'] or 'first' in body:
        raise ValueError('Preserve the complete mesh, full paint and actual FBX tint')
    data = np.load(mesh)
    if data['v'].shape != (520, 3) or data['uv'].shape != (520, 2) \
            or data['tri'].shape != (528, 3):
        raise ValueError('Native vertices, UVs and triangles must remain complete')
    sys.path.insert(0, str(native_tools))
    import bl_lib as L
    L.reset()
    place, _ = L.place_matrix(source, (0, 0, 0), rot=30, respect_active=True)
    objects, low, high = L.build(source, place=place, respect_active=True)
    if len(objects) != 1 or len(objects[0].data.vertices) != 520 \
            or len(objects[0].data.polygons) != 528:
        raise ValueError('Do not select a leaf or material fragment')
    for material in objects[0].data.materials:
        nt = material.node_tree
        out = next(n for n in nt.nodes if n.type == 'OUTPUT_MATERIAL')
        surface = out.inputs['Surface'].links[0].from_socket
        geom = nt.nodes.new('ShaderNodeNewGeometry')
        transparent = nt.nodes.new('ShaderNodeBsdfTransparent')
        mix = nt.nodes.new('ShaderNodeMixShader')
        nt.links.new(geom.outputs['Backfacing'], mix.inputs[0])
        nt.links.new(surface, mix.inputs[1])
        nt.links.new(transparent.outputs[0], mix.inputs[2])
        nt.links.new(mix.outputs[0], out.inputs[0])
        for node in nt.nodes:
            if node.type == 'MATH' and node.operation == 'GREATER_THAN':
                node.inputs[1].default_value = .5
    image = bpy.data.images.load(str(flowers), check_existing=False)
    if tuple(image.size) != (1254, 1254):
        raise ValueError('Keep the retained component canvas')
    crop = (45, 180, 1210, 1090)
    alpha = np.asarray(image.pixels[:]).reshape(1254, 1254, 4)[:, :, 3]
    keep = np.zeros(alpha.shape, dtype=bool)
    keep[1254-crop[3]:1254-crop[1], crop[0]:crop[2]] = True
    if np.any(alpha[~keep] > 8/255):
        raise ValueError('Crop would remove visible flower paint')
    height = 1.2
    width = height * (crop[2]-crop[0]) / (crop[3]-crop[1])
    cx, cy = (low[:2] + high[:2]) / 2
    bottom = .95
    vertices = [(cx-width/2, cy, bottom), (cx+width/2, cy, bottom),
                (cx+width/2, cy, bottom+height), (cx-width/2, cy, bottom+height)]
    mesh_card = bpy.data.meshes.new('generated_bloodflower_sprig')
    mesh_card.from_pydata(vertices, [], [(0, 1, 2, 3)])
    card = bpy.data.objects.new('generated_flowers_ONLY', mesh_card)
    bpy.context.collection.objects.link(card)
    uv = mesh_card.uv_layers.new()
    values = [(crop[0]/1254, 1-crop[3]/1254), (crop[2]/1254, 1-crop[3]/1254),
              (crop[2]/1254, 1-crop[1]/1254), (crop[0]/1254, 1-crop[1]/1254)]
    for i, value in enumerate(values):
        uv.data[i].uv = value
    material = bpy.data.materials.new('generated_flower_alpha_emission')
    material.use_nodes = True
    nt = material.node_tree
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
    card.data.materials.append(material)
    centre = (float(cx), float(cy), 1.3)
    extent = max(high[0]-low[0], 2.6) * 1.15
    L.camera((cx, cy-12, centre[2]+12*math.tan(math.radians(10))), centre,
             ortho=float(extent), res=(1024, 1024))
    scene = bpy.context.scene
    scene.render.threads_mode = 'FIXED'
    scene.render.threads = 2
    scene.cycles.seed = 0
    bpy.context.view_layer.update()
    points = [world_to_camera_view(scene, scene.camera, Vector(v)) for v in vertices]
    bounds = [math.floor(min(p.x*1024 for p in points))-3,
              math.floor(min((1-p.y)*1024 for p in points))-3,
              math.ceil(max(p.x*1024 for p in points))+3,
              math.ceil(max((1-p.y)*1024 for p in points))+3]
    output.mkdir(parents=True)
    for state in ('closed', 'used'):
        card.hide_render = state == 'used'
        L.render(output / (state+'.png'), samples=4, transparent=True)
    arrays = [np.asarray(bpy.data.images.load(str(output / (s+'.png')),
              check_existing=False).pixels[:]).reshape(1024, 1024, 4)[::-1]
              for s in ('closed', 'used')]
    changed = np.any(arrays[0] != arrays[1], axis=2)
    domain = np.zeros(changed.shape, dtype=bool)
    domain[bounds[1]:bounds[3], bounds[0]:bounds[2]] = True
    if not changed.any() or domain.mean() > .2 or np.any(changed & ~domain) \
            or np.any(changed[750:]):
        raise ValueError('Only the bounded sprig may change; keep native lower leaves exact')
    y, x = np.where(changed)
    record = {'native_geometry': [520, 528], 'native_UV_vertices': 520,
              'native_full_paint': 1024, 'native_FBX_tint': mat['tint'],
              'whole_native_body_fixed': True, 'generated_sprig_only': True,
              'component_crop': crop, 'component_height': height, 'component_bottom': bottom,
              'projected_component_domain': bounds, 'domain_pixels': int(domain.sum()),
              'changed_pixels': int(changed.sum()),
              'changed_bounds': [int(x.min()), int(y.min()), int(x.max()+1), int(y.max()+1)],
              'outside_RGBA_exact': True, 'native_rows750_onward_exact': True,
              'source_scene_sha256': sha(source / 'scene.json'), 'source_mesh_sha256': sha(mesh),
              'source_paint_sha256': sha(mat['texture']), 'component_sha256': sha(flowers),
              'recipe_sha256': sha(__file__), 'native_tools_sha256': sha(native_tools / 'bl_lib.py'),
              'Cull2_clip05_modeled_not_full_native_Unity_shader': True,
              'outputs': {p.name: sha(p) for p in output.glob('*.png')},
              'installed': False, 'native_verified': False, 'canonical_DLC_gameplay_QA': False}
    (output / 'provenance.json').write_text(json.dumps(record, indent=2))
    return record


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    for name in ('source', 'flowers', 'native-tools', 'out'):
        parser.add_argument('--'+name, type=Path, required=True)
    args = parser.parse_args(sys.argv[sys.argv.index('--')+1:])
    print(json.dumps(render(args.source, args.flowers, args.native_tools, args.out)))
