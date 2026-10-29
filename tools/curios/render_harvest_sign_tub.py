"""Render a complete native wooden tub and harvest sign with a produce-only card.

The audited five whole wood/hoop groups keep every native vertex, UV and full
paint. The seven original flesh groups stay excluded. A complete native sign stays fixed. Used
removes ONLY the new produce card. Native geometry controls its occlusion.
No accepted brine painting, water shader or original flesh is reused as food.
This is adapted native body art, not a full Unity shader or gameplay proof.
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


def render(source, components, source_audit, sign_source, produce, native_tools, output):
    source, components, source_audit, sign_source, produce, native_tools, output = [
        Path(p).resolve() for p in (source, components, source_audit,
                                  sign_source, produce, native_tools, output)]
    repo = Path(__file__).resolve().parents[2]
    if any(p.is_relative_to(repo) for p in (source, components, source_audit,
                                           sign_source, produce, output)) or output.exists():
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
    if sha(source_audit) != '08c037bdddcf92cf786fac1dc75be7c051fb6b0c34c5fdaf76fdb3a9005937da':
        raise ValueError('Use the retained actual native source audit')
    if sha(components) != '3068cf1a5f32bb46395059249bdb50765938c843eb92722253a4904b8abbfcef':
        raise ValueError('Use the retained complete source face partition')
    if sha(produce) != 'a35a87af70873d61ea2cac94ad0fd9e6348e1637bc2cc6e307d688fcad2225a2':
        raise ValueError('Use the single original generated produce bitmap unchanged')
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
    sign_doc = json.loads((sign_source/'scene.json').read_text())
    if len(sign_doc['objects']) != 1 or sign_doc['skipped']:
        raise ValueError('Keep the complete active native sign renderer')
    sign_mesh=sign_source/('mesh-'+sign_doc['objects'][0]['mesh']+'.npz')
    sign_paint=(sign_source/next(iter(sign_doc['materials'].values()))['texture']).resolve()
    for path,expected in ((sign_source/'scene.json','1d005085d5a4c093409469844c0087041462aaf0d8a3088543822a0ef856ce44'),
                          (sign_mesh,'7e79b0ed0d8fa73a077a3d3d6fbad33b0667a41ac79e01d86722fac04b361a06'),
                          (sign_paint,'9ccdf12e72b8a24f4e827059d8fbc259c461e2229de10f078954dc97c7c6536d')):
        if sha(path)!=expected: raise ValueError('Audited full native sign source changed')
    sign_data=np.load(sign_mesh)
    if sign_data['v'].shape!=(563,3) or sign_data['uv'].shape!=(563,2) or sign_data['tri'].shape!=(562,3):
        raise ValueError('Incomplete sign geometry or UVs')
    sign_floor=float((native.CHANGE@place@np.array([0,.13,0,1]))[2])
    sign_place,_=native.place_matrix(sign_source,(.45,sign_floor,.65),rot=210,respect_active=True)
    sign_objects,_,_=native.build(sign_source,place=sign_place,respect_active=True)
    if len(sign_objects)!=1 or len(sign_objects[0].data.vertices)!=563 or len(sign_objects[0].data.polygons)!=562:
        raise ValueError('Native sign renderer was filtered')
    for material in sign_objects[0].data.materials:
        tree=material.node_tree; final=next(n for n in tree.nodes if n.type=='OUTPUT_MATERIAL')
        original=final.inputs['Surface'].links[0].from_socket
        geometry=tree.nodes.new('ShaderNodeNewGeometry'); transparent=tree.nodes.new('ShaderNodeBsdfTransparent'); mix=tree.nodes.new('ShaderNodeMixShader')
        tree.links.new(geometry.outputs['Backfacing'],mix.inputs[0]);tree.links.new(original,mix.inputs[1]);tree.links.new(transparent.outputs[0],mix.inputs[2]);tree.links.new(mix.outputs[0],final.inputs['Surface'])
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
    all_xyz=np.concatenate([xyz,np.array([list(sign_objects[0].matrix_world@v.co)for v in sign_objects[0].data.vertices])])
    view_lo,view_hi=all_xyz.min(0),all_xyz.max(0);view_center=(view_lo+view_hi)/2
    extent = max(view_hi[0]-view_lo[0],view_hi[2]-view_lo[2])*1.18
    distance = max(10, extent * 2)
    native.camera((view_center[0], view_center[1] - distance,
                   view_center[2] + distance * math.tan(math.radians(18))),
                  tuple(view_center), ortho=float(extent), res=(1024, 1024))
    bpy.context.scene.render.threads_mode = 'FIXED'
    bpy.context.scene.render.threads = 2
    bpy.context.scene.cycles.seed = 0
    output.mkdir(parents=True)
    native.render(output / 'used.png', samples=8, transparent=True)


    # A new alpha card sits at native depth inside the complete wooden body.
    # Native front planks hide its lower edge; the back wall stays behind it.
    # The full generated bitmap is used, including its original alpha values.
    image = bpy.data.images.load(str(produce))
    if tuple(image.size) != (1681, 936):
        raise ValueError('Original component dimensions changed')
    width, height = 2.05, 2.05*936/1681
    card_center = np.array([center[0], center[1], floor + .85])
    up = np.array([0, math.sin(math.radians(18)), math.cos(math.radians(18))])
    across = np.array([1, 0, 0])
    vertices = [(card_center + across*x*width + up*z*height).tolist()
                for x,z in ((-.5,-.5),(.5,-.5),(.5,.5),(-.5,.5))]
    mesh = bpy.data.meshes.new('new_generated_produce_alpha_card')
    mesh.from_pydata(vertices, [], [(0,1,2,3)]); mesh.update()
    card = bpy.data.objects.new('missing_party_tomato_kale_harvest_ONLY', mesh)
    bpy.context.collection.objects.link(card)
    uv = mesh.uv_layers.new(name='FullOriginalComponentUV')
    for loop, coords in zip(uv.data, ((0,0),(1,0),(1,1),(0,1))):
        loop.uv = coords
    mat = bpy.data.materials.new('generated_produce_original_alpha_working_RGBx0.65')
    mat.use_nodes=True; tree=mat.node_tree; tree.nodes.clear()
    texture=tree.nodes.new('ShaderNodeTexImage'); texture.image=image
    multiply=tree.nodes.new('ShaderNodeMixRGB'); multiply.blend_type='MULTIPLY'
    multiply.inputs[0].default_value=1; multiply.inputs[2].default_value=(.65,.65,.65,1)
    tree.links.new(texture.outputs['Color'],multiply.inputs[1])
    emission=tree.nodes.new('ShaderNodeEmission'); tree.links.new(multiply.outputs[0],emission.inputs['Color'])
    transparent=tree.nodes.new('ShaderNodeBsdfTransparent'); mix=tree.nodes.new('ShaderNodeMixShader')
    tree.links.new(texture.outputs['Alpha'],mix.inputs[0]); tree.links.new(transparent.outputs[0],mix.inputs[1]); tree.links.new(emission.outputs[0],mix.inputs[2])
    final=tree.nodes.new('ShaderNodeOutputMaterial'); tree.links.new(mix.outputs[0],final.inputs['Surface'])
    mesh.materials.append(mat)
    bpy.context.view_layer.update()
    from bpy_extras.object_utils import world_to_camera_view
    projected=[]
    for vertex in mesh.vertices:
        q=world_to_camera_view(bpy.context.scene,bpy.context.scene.camera,card.matrix_world@vertex.co)
        projected.append([q.x*1024,(1-q.y)*1024])
    native.render(output/'closed.png',samples=8,transparent=True)
    record=dict(native_body_vertices=1275,native_body_triangles=1404,complete_native_sign_vertices=563,complete_native_sign_triangles=562,sign_yaw210=True,sign_world_place=sign_place.tolist(),native_sign_paint_positions_UV_retained=True,
                retained_complete_wood_hoop_groups=list(body_groups),excluded_whole_original_flesh_groups=[0,1,2,3,9,10,11],
                groups_are_geometric_not_authored_objects=True,native_positions_UV_full_paint_tint_retained=True,
                generated_component_vertices=4,generated_component_quads=1,full_original_component_bitmap_size=[1681,936],
                component_working_RGB_multiplier=.65,component_width_height=[width,height],
                component_world_center=card_center.tolist(),component_projected_corners=projected,
                no_generated_native_body_or_bitmap_repaint=True,no_water_or_original_flesh_payload=True,
                used_removes_only_generated_produce=True,floor_native_Y=.13,camera_pitch=18,
                real_native_depth_occlusion=True,Cull2_modeled_not_full_Unity_lighting_shader=True,
                source_audit_sha256=sha(source_audit),components_sha256=sha(components),produce_sha256=sha(produce),
                recipe_sha256=sha(__file__),installed=False,native_QA=False,accepted=False)
    (output/'provenance.json').write_text(json.dumps(record,indent=2))

if __name__ == '__main__':
    parser=argparse.ArgumentParser(description=__doc__)
    for name in ('source','components','source-audit','sign-source','produce','native-tools','out'):
        parser.add_argument('--'+name,type=Path,required=True)
    args=parser.parse_args(sys.argv[sys.argv.index('--')+1:])
    render(args.source,args.components,args.source_audit,args.sign_source,args.produce,args.native_tools,args.out)
