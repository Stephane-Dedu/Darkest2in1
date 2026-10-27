"""Render an audited native tub with a small static brine surface.

Blender --python this_file -- --source <private export> --components <audit>
--source-audit <installed-source audit> --water-textures <private folder>
--native-tools <kit/tools> --out <new private folder>.

All native tub planks and hoops retain geometry, UVs, full paint and tint.
Whole native payload groups are excluded. The new 64-face water disc uses
an unchanged native foam mask with an explicit static palette adaptation;
lowering that surface supplies the spent cue. This is not a reconstruction
of the game's animated water, lighting, fog, dithering or outline shaders.
"""
import argparse
import hashlib
import json
import math
from pathlib import Path
import sys

import bmesh
import bpy
import numpy as np


def sha(path):
    return hashlib.sha256(Path(path).read_bytes()).hexdigest()


def render(source, components, source_audit, water_textures, native_tools, output):
    source, components, source_audit, water_textures, native_tools, output = [
        Path(p).resolve() for p in (source, components, source_audit,
                                  water_textures, native_tools, output)]
    repo = Path(__file__).resolve().parents[2]
    if any(p.is_relative_to(repo) for p in (source, components, source_audit,
                                           water_textures, output)) or output.exists():
        raise ValueError('Use audited private inputs and a new private output')
    doc = json.loads((source / 'scene.json').read_text())
    audit = json.loads(source_audit.read_text())
    if len(doc['objects']) != 1 or doc['skipped']:
        raise ValueError('A complete single-renderer source is required')
    mesh_path = source / ('mesh-' + doc['objects'][0]['mesh'] + '.npz')
    paint = (source / next(iter(doc['materials'].values()))['texture']).resolve()
    for path in (source / 'scene.json', mesh_path, paint):
        if sha(path) != audit['source_files'].get(str(path)):
            raise ValueError('Source differs from the actual installed-source audit')
    data = np.load(mesh_path)
    groups = json.loads(components.read_text())['groups']
    if len(data['v']) != 2121 or len(data['tri']) != 2603 or len(groups) != 12:
        raise ValueError('Native source or component partition changed')
    if sorted(i for g in groups for i in g['face_indices']) != list(range(2603)):
        raise ValueError('Groups must partition all native faces exactly once')
    body_groups = (4, 5, 6, 7, 8)
    body_faces = [i for g in groups if g['group'] in body_groups for i in g['face_indices']]
    hidden = [i for g in groups if g['group'] not in body_groups for i in g['face_indices']]
    if len(body_faces) != 1404 or len(np.unique(data['tri'][body_faces])) != 1275:
        raise ValueError('Keep every plank and both complete iron hoops')
    foam_path = water_textures / '_FoamTexture.png'
    if sha(foam_path) != 'a21e201d848612cede70d7b58fc6a422bf3477e222f19b57e9c07ab7bd279781':
        raise ValueError('Use the full original native ocean foam mask')
    sys.path.insert(0, str(native_tools))
    import bl_lib as native
    native.reset()
    place, _ = native.place_matrix(source, (0, 0, 0), rot=180, respect_active=True)
    objects, _, _ = native.build(source, place=place, respect_active=True)
    if len(objects) != 1 or len(objects[0].data.polygons) != 2603:
        raise ValueError('Native renderer is incomplete')
    body = objects[0]
    bm = bmesh.new()
    bm.from_mesh(body.data)
    bm.faces.ensure_lookup_table()
    bmesh.ops.delete(bm, geom=[bm.faces[i] for i in hidden], context='FACES')
    bm.to_mesh(body.data)
    bm.free()
    body.data.update()
    if (len(body.data.vertices), len(body.data.polygons)) != (1275, 1404):
        raise ValueError('Body selection lost native geometry')
    for material in body.data.materials:
        tree = material.node_tree
        out = next(n for n in tree.nodes if n.type == 'OUTPUT_MATERIAL')
        original = out.inputs['Surface'].links[0].from_socket
        geometry = tree.nodes.new('ShaderNodeNewGeometry')
        transparent = tree.nodes.new('ShaderNodeBsdfTransparent')
        mix = tree.nodes.new('ShaderNodeMixShader')
        tree.links.new(geometry.outputs['Backfacing'], mix.inputs[0])
        tree.links.new(original, mix.inputs[1])
        tree.links.new(transparent.outputs[0], mix.inputs[2])
        tree.links.new(mix.outputs[0], out.inputs['Surface'])
    # The native painted cards extend below the actual timber into a black
    # ground skirt. Bury that skirt in both states without deleting geometry.
    floor = float((native.CHANGE @ place @ np.array([0, .13, 0, 1]))[2])
    bpy.ops.mesh.primitive_plane_add(size=100, location=(0, 0, floor))
    ground = bpy.context.object
    material = bpy.data.materials.new('common_render_only_ground_holdout')
    material.use_nodes = True
    tree = material.node_tree
    tree.nodes.clear()
    holdout = tree.nodes.new('ShaderNodeHoldout')
    out = tree.nodes.new('ShaderNodeOutputMaterial')
    tree.links.new(holdout.outputs[0], out.inputs['Surface'])
    ground.data.materials.append(material)
    xyz = np.array([list(body.matrix_world @ v.co) for v in body.data.vertices])
    lo, hi = xyz.min(0), xyz.max(0)
    center = (lo + hi) / 2
    extent = max(hi[0] - lo[0], hi[2] - lo[2]) * 1.18
    distance = max(10, extent * 2)
    native.camera((center[0], center[1] - distance,
                   center[2] + distance * math.tan(math.radians(18))),
                  tuple(center), ortho=float(extent), res=(1024, 1024))
    bpy.context.scene.render.threads_mode = 'FIXED'
    bpy.context.scene.render.threads = 2
    bpy.context.scene.cycles.seed = 0
    output.mkdir(parents=True)
    native.render(output / 'native-empty-reference.png', samples=8, transparent=True)

    # The rim tapers inwards. The lower surface must fit its narrower section.
    matrix = native.CHANGE @ place
    def points(level, radius):
        local = [(0, level, 0, 1)] + [
            (radius * math.cos(2 * math.pi * i / 64), level,
             radius * math.sin(2 * math.pi * i / 64), 1) for i in range(64)]
        return [(matrix @ np.array(p))[:3].tolist() for p in local]
    mesh = bpy.data.meshes.new('new_64_face_brine_surface')
    mesh.from_pydata(points(.88, .93), [], [(0, 1+i, 1+(i+1) % 64) for i in range(64)])
    mesh.update()
    plane = bpy.data.objects.new('adapted_static_brine_surface', mesh)
    bpy.context.collection.objects.link(plane)
    uv = mesh.uv_layers.new(name='WaterUV')
    for poly in mesh.polygons:
        for loop_index in poly.loop_indices:
            vertex = mesh.loops[loop_index].vertex_index
            a = 2 * math.pi * (vertex - 1) / 64
            uv.data[loop_index].uv = ((.5, .5) if vertex == 0 else
                                      (.5 + .5*math.cos(a), .5 + .5*math.sin(a)))
    mat = bpy.data.materials.new('native_foam_static_palette_adaptation')
    mat.use_nodes = True
    tree = mat.node_tree
    tree.nodes.clear()
    coord = tree.nodes.new('ShaderNodeTexCoord')
    mapping = tree.nodes.new('ShaderNodeVectorMath')
    mapping.operation = 'SCALE'
    mapping.inputs[3].default_value = 1.8
    tree.links.new(coord.outputs['UV'], mapping.inputs[0])
    foam = tree.nodes.new('ShaderNodeTexImage')
    foam.image = bpy.data.images.load(str(foam_path))
    foam.image.colorspace_settings.name = 'Non-Color'
    foam.extension = 'REPEAT'
    tree.links.new(mapping.outputs[0], foam.inputs['Vector'])
    ramp = tree.nodes.new('ShaderNodeValToRGB')
    ramp.color_ramp.elements[0].position = .60
    ramp.color_ramp.elements[0].color = (.1724368334*.12, .2252803892*.12, .2358490825*.12, 1)
    ramp.color_ramp.elements[1].position = .90
    ramp.color_ramp.elements[1].color = (.1415094137*.6,)*3 + (1,)
    tree.links.new(foam.outputs['Color'], ramp.inputs[0])
    emission = tree.nodes.new('ShaderNodeEmission')
    tree.links.new(ramp.outputs[0], emission.inputs['Color'])
    out = tree.nodes.new('ShaderNodeOutputMaterial')
    tree.links.new(emission.outputs[0], out.inputs['Surface'])
    mesh.materials.append(mat)
    native.render(output / 'closed.png', samples=8, transparent=True)
    for vertex, point in zip(mesh.vertices, points(.56, .82)):
        vertex.co = point
    mesh.update()
    native.render(output / 'used.png', samples=8, transparent=True)
    record = {'native_body_vertices': 1275, 'native_body_triangles': 1404,
              'whole_original_payload_excluded_vertices': 846,
              'whole_original_payload_excluded_triangles': 1199,
              'groups_are_geometric_not_authored_objects': True,
              'native_source_positions_UV_full_paint_tint_retain': True,
              'new_water_vertices': 65, 'new_water_triangles': 64,
              'closed_water_level_radius': [.88, .93], 'used_water_level_radius': [.56, .82],
              'camera_pitch_degrees': 18, 'native_Cull2_opaque_modeled': True,
              'common_render_only_ground_native_Y': .13,
              'all_native_body_geometry_retained_under_ground': True,
              'static_mask_mapping_palette_adaptation_not_full_native_shader': True,
              'foam_thresholds': [.6, .9], 'foam_UV_scale': 1.8,
              'native_ColorA_multiplier': .12, 'native_FoamColor_multiplier': .6,
              'source_audit_sha256': sha(source_audit), 'components_sha256': sha(components),
              'native_foam_png_sha256': sha(foam_path), 'recipe_sha256': sha(__file__),
              'no_generation_or_bitmap_repaint': True, 'installed': False, 'native_QA': False}
    (output / 'adaptation.json').write_text(json.dumps(record, indent=2))


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    for name in ('source', 'components', 'source-audit', 'water-textures', 'native-tools', 'out'):
        parser.add_argument('--' + name, type=Path, required=True)
    args = parser.parse_args(sys.argv[sys.argv.index('--') + 1:])
    render(args.source, args.components, args.source_audit, args.water_textures,
           args.native_tools, args.out)
