"""Render a complete native grey stone heap with one missing teal growth card.

Every144native vertex/111triangle,UV,full2048paint and original0.475tint stays
fixed. Used hides only the generated local nodules. This adapted spent cue
does not reproduce the original bursting mound or the complete Unity shader.
"""
import argparse,hashlib,json,math,sys
from pathlib import Path
import bpy,numpy as np

def sha(path):return hashlib.sha256(Path(path).read_bytes()).hexdigest()

def render(source,nodules,native_tools,output):
    source,nodules,native_tools,output=[Path(p).resolve()for p in (source,nodules,native_tools,output)]
    repo=Path(__file__).resolve().parents[2]
    if any(p.is_relative_to(repo)for p in (source,nodules,output))or output.exists():
        raise ValueError('Use guarded private inputs and a new private output')
    doc=json.loads((source/'scene.json').read_text())
    if len(doc['objects'])!=1 or doc['skipped']:raise ValueError('Use the complete native FBX root')
    meshpath=source/('mesh-'+doc['objects'][0]['mesh']+'.npz')
    material=next(iter(doc['materials'].values()));paint=(source/material['texture']).resolve()
    for p,h in ((source/'scene.json','caed2e661885b0525d56cc0e3c6451b051580d01ac1f601e93702a8af0a49d89'),
                (meshpath,'e685f0703a7ec092f2a37e2db5aa0b0f0f23168f6582f1d81e6943e3e48ed07c'),
                (paint,'ffc77c3a1d87b0b77749c1bd4496f8c22a701769cb48db4bb9d1f73fa809cdb4'),
                (nodules,'c9cac35ced18ec4ee91afc3cf8f464af4d70eea6ddda09690c04f1a141cfbc4f')):
        if sha(p)!=h:raise ValueError('Audited native or single generated original changed')
    data=np.load(meshpath)
    if data['v'].shape!=(144,3)or data['uv'].shape!=(144,2)or data['tri'].shape!=(111,3):raise ValueError('Incomplete native body')
    if material['tint']!=[0.4749999940395355]*3+[1.0]:raise ValueError('Retain original native tint')
    sys.path.insert(0,str(native_tools));import bl_lib as native
    native.reset();place,_=native.place_matrix(source,(0,0,0),rot=210,respect_active=True)
    objects,lo,hi=native.build(source,place=place,respect_active=True)
    if len(objects)!=1 or len(objects[0].data.vertices)!=144 or len(objects[0].data.polygons)!=111:raise ValueError('Native body filtered')
    for mat in objects[0].data.materials:
        tree=mat.node_tree
        for threshold in tree.nodes:
            if threshold.type=='MATH'and threshold.operation=='GREATER_THAN':threshold.inputs[1].default_value=.5
        final=next(n for n in tree.nodes if n.type=='OUTPUT_MATERIAL');surface=final.inputs['Surface'].links[0].from_socket
        geometry=tree.nodes.new('ShaderNodeNewGeometry');transparent=tree.nodes.new('ShaderNodeBsdfTransparent');mix=tree.nodes.new('ShaderNodeMixShader')
        tree.links.new(geometry.outputs['Backfacing'],mix.inputs[0]);tree.links.new(surface,mix.inputs[1]);tree.links.new(transparent.outputs[0],mix.inputs[2]);tree.links.new(mix.outputs[0],final.inputs['Surface'])
    center=(lo+hi)/2;extent=max(hi[0]-lo[0],hi[2]-lo[2])*1.15;distance=max(12,extent*2)
    native.camera((center[0],center[1]-distance,center[2]+distance*math.tan(math.radians(10))),tuple(center),ortho=float(extent),res=(1024,1024))
    scene=bpy.context.scene;scene.render.threads_mode='FIXED';scene.render.threads=2;scene.cycles.seed=0
    output.mkdir(parents=True);native.render(output/'used.png',samples=8,transparent=True)
    image=bpy.data.images.load(str(nodules))
    if tuple(image.size)!=(1774,887):raise ValueError('Retain full original component bitmap')
    width,height=.9,.45
    card_center=np.array([center[0],lo[1]+.10,lo[2]+.28]);up=np.array([0,math.sin(math.radians(10)),math.cos(math.radians(10))]);across=np.array([1,0,0])
    vertices=[(card_center+across*x*width+up*z*height).tolist()for x,z in ((-.5,-.5),(.5,-.5),(.5,.5),(-.5,.5))]
    mesh=bpy.data.meshes.new('new_local_teal_nodule_card');mesh.from_pydata(vertices,[],[(0,1,2,3)]);mesh.update();card=bpy.data.objects.new('generated_missing_nodules_ONLY',mesh);bpy.context.collection.objects.link(card)
    uv=mesh.uv_layers.new(name='FullOriginalComponentUV')
    for loop,coord in zip(uv.data,((0,0),(1,0),(1,1),(0,1))):loop.uv=coord
    mat=bpy.data.materials.new('generated_nodules_original_alpha_working_RGBx0.55');mat.use_nodes=True;tree=mat.node_tree;tree.nodes.clear();tex=tree.nodes.new('ShaderNodeTexImage');tex.image=image
    multiply=tree.nodes.new('ShaderNodeMixRGB');multiply.blend_type='MULTIPLY';multiply.inputs[0].default_value=1;multiply.inputs[2].default_value=(.55,.55,.55,1);tree.links.new(tex.outputs['Color'],multiply.inputs[1])
    emission=tree.nodes.new('ShaderNodeEmission');tree.links.new(multiply.outputs[0],emission.inputs['Color']);transparent=tree.nodes.new('ShaderNodeBsdfTransparent');mix=tree.nodes.new('ShaderNodeMixShader');tree.links.new(tex.outputs['Alpha'],mix.inputs[0]);tree.links.new(transparent.outputs[0],mix.inputs[1]);tree.links.new(emission.outputs[0],mix.inputs[2]);final=tree.nodes.new('ShaderNodeOutputMaterial');tree.links.new(mix.outputs[0],final.inputs['Surface']);mesh.materials.append(mat)
    bpy.context.view_layer.update();from bpy_extras.object_utils import world_to_camera_view
    projected=[]
    for vertex in mesh.vertices:
        p=world_to_camera_view(scene,scene.camera,card.matrix_world@vertex.co);projected.append([p.x*1024,(1-p.y)*1024])
    native.render(output/'closed.png',samples=8,transparent=True)
    record=dict(native_vertices=144,native_triangles=111,native_UVs=144,full_native_paint=[2048,2048],native_tint=material['tint'],native_source_positions_UVs_full_paint_retained=True,whole_native_body_retained_both_states=True,native_Cull2_clip0_5_modeled_not_full_Unity_lighting=True,yaw=210,camera_pitch=10,generated_component_vertices=4,generated_component_quads=1,full_generated_original_bitmap=[1774,887],component_working_RGB=.55,component_center=card_center.tolist(),component_width_height=[width,height],projected_component_corners=projected,used_hides_only_nodules=True,adapted_spent_state_not_literal_canonical_burst=True,recipe_sha256=sha(__file__),source_files={str(p):sha(p)for p in (source/'scene.json',meshpath,paint,nodules)},accepted=False,installed=False,native_gameplay_QA=False)
    (output/'provenance.json').write_text(json.dumps(record,indent=2))

if __name__=='__main__':
    parser=argparse.ArgumentParser(description=__doc__)
    for name in ('source','nodules','native-tools','out'):parser.add_argument('--'+name,type=Path,required=True)
    args=parser.parse_args(sys.argv[sys.argv.index('--')+1:]);render(args.source,args.nodules,args.native_tools,args.out)
