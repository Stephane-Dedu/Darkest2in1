"""Render a complete native rooted stone heap, missing spade and local nodules.

All249native vertices/206triangles,UVs,full2048paint and original0.475tint
remain fixed. The generated spade is static. Used clears only two instances
of the retained434nodule component. This is an adapted spent presentation,
not the original bursting mound/spade animation or full Unity shader.
"""
import argparse,hashlib,json,math,sys
from pathlib import Path
import bpy,numpy as np

def sha(path):return hashlib.sha256(Path(path).read_bytes()).hexdigest()

def alpha_card(path,name,center,width,height,shade):
    image=bpy.data.images.load(str(path));up=np.array([0,math.sin(math.radians(10)),math.cos(math.radians(10))]);across=np.array([1,0,0]);center=np.array(center)
    vertices=[(center+across*x*width+up*z*height).tolist()for x,z in ((-.5,-.5),(.5,-.5),(.5,.5),(-.5,.5))]
    mesh=bpy.data.meshes.new(name);mesh.from_pydata(vertices,[],[(0,1,2,3)]);mesh.update();card=bpy.data.objects.new(name,mesh);bpy.context.collection.objects.link(card)
    uv=mesh.uv_layers.new(name='FullOriginalComponentUV')
    for loop,coord in zip(uv.data,((0,0),(1,0),(1,1),(0,1))):loop.uv=coord
    mat=bpy.data.materials.new(name+'_original_alpha_working_RGBx'+str(shade));mat.use_nodes=True;tree=mat.node_tree;tree.nodes.clear();tex=tree.nodes.new('ShaderNodeTexImage');tex.image=image
    multiply=tree.nodes.new('ShaderNodeMixRGB');multiply.blend_type='MULTIPLY';multiply.inputs[0].default_value=1;multiply.inputs[2].default_value=(shade,shade,shade,1);tree.links.new(tex.outputs['Color'],multiply.inputs[1])
    emission=tree.nodes.new('ShaderNodeEmission');tree.links.new(multiply.outputs[0],emission.inputs['Color']);transparent=tree.nodes.new('ShaderNodeBsdfTransparent');mix=tree.nodes.new('ShaderNodeMixShader');tree.links.new(tex.outputs['Alpha'],mix.inputs[0]);tree.links.new(transparent.outputs[0],mix.inputs[1]);tree.links.new(emission.outputs[0],mix.inputs[2]);final=tree.nodes.new('ShaderNodeOutputMaterial');tree.links.new(mix.outputs[0],final.inputs['Surface']);mesh.materials.append(mat)
    return card

def render(source,nodules,spade,native_tools,output):
    source,nodules,spade,native_tools,output=[Path(p).resolve()for p in (source,nodules,spade,native_tools,output)];repo=Path(__file__).resolve().parents[2]
    if any(p.is_relative_to(repo)for p in (source,nodules,spade,output))or output.exists():raise ValueError('Use audited private inputs and a new private output')
    doc=json.loads((source/'scene.json').read_text())
    if len(doc['objects'])!=1 or doc['skipped']:raise ValueError('Use the complete native FBX root')
    meshpath=source/('mesh-'+doc['objects'][0]['mesh']+'.npz');material=next(iter(doc['materials'].values()));paint=(source/material['texture']).resolve()
    for p,h in ((source/'scene.json','c128ca6c286b2be2c3fb01fe7faf36d8595535b20ab04a6466c145462d7a34f9'),(meshpath,'4880b6bfed4a792a4f42ae2b6183db95bec7f9079426a9e9072e011f67440b47'),(paint,'ffc77c3a1d87b0b77749c1bd4496f8c22a701769cb48db4bb9d1f73fa809cdb4'),(nodules,'c9cac35ced18ec4ee91afc3cf8f464af4d70eea6ddda09690c04f1a141cfbc4f'),(spade,'1c14d6337394ccc78cce3f5107bfb7f2d25c254910f2e1e6f77afc99684942b3')):
        if sha(p)!=h:raise ValueError('Native source or original generated component changed')
    data=np.load(meshpath)
    if data['v'].shape!=(249,3)or data['uv'].shape!=(249,2)or data['tri'].shape!=(206,3):raise ValueError('Incomplete native body')
    if material['tint']!=[0.4749999940395355]*3+[1.0]:raise ValueError('Retain original native tint')
    sys.path.insert(0,str(native_tools));import bl_lib as native
    native.reset();place,_=native.place_matrix(source,(0,0,0),rot=210,respect_active=True);objects,lo,hi=native.build(source,place=place,respect_active=True)
    if len(objects)!=1 or len(objects[0].data.vertices)!=249 or len(objects[0].data.polygons)!=206:raise ValueError('Native body filtered')
    for mat in objects[0].data.materials:
        tree=mat.node_tree
        for threshold in tree.nodes:
            if threshold.type=='MATH'and threshold.operation=='GREATER_THAN':threshold.inputs[1].default_value=.5
        final=next(n for n in tree.nodes if n.type=='OUTPUT_MATERIAL');surface=final.inputs['Surface'].links[0].from_socket;geometry=tree.nodes.new('ShaderNodeNewGeometry');transparent=tree.nodes.new('ShaderNodeBsdfTransparent');mix=tree.nodes.new('ShaderNodeMixShader');tree.links.new(geometry.outputs['Backfacing'],mix.inputs[0]);tree.links.new(surface,mix.inputs[1]);tree.links.new(transparent.outputs[0],mix.inputs[2]);tree.links.new(mix.outputs[0],final.inputs['Surface'])
    center=(lo+hi)/2
    tool=alpha_card(spade,'missing_generated_spade_STATIC',(center[0]-.75,lo[1]+.07,lo[2]+1.12),1.6,2.4,.55)
    if tuple(tool.data.materials[0].node_tree.nodes.get('Image Texture').image.size)!=(1024,1536):raise ValueError('Original full spade dimensions changed')
    xyz=np.concatenate([np.array([list(objects[0].matrix_world@v.co)for v in objects[0].data.vertices]),np.array([list(v.co)for v in tool.data.vertices])]);view_lo,view_hi=xyz.min(0),xyz.max(0);view_center=(view_lo+view_hi)/2;extent=max(view_hi[0]-view_lo[0],view_hi[2]-view_lo[2])*1.15;distance=max(12,extent*2)
    native.camera((view_center[0],view_center[1]-distance,view_center[2]+distance*math.tan(math.radians(10))),tuple(view_center),ortho=float(extent),res=(1024,1024));scene=bpy.context.scene;scene.render.threads_mode='FIXED';scene.render.threads=2;scene.cycles.seed=0
    output.mkdir(parents=True);native.render(output/'used.png',samples=8,transparent=True)
    cards=[alpha_card(nodules,'retained434_nodules_'+str(i),(center[0]+x,lo[1]+.10,lo[2]+.32),1.10,.55,.55)for i,x in enumerate((-.65,.82))]
    if tuple(cards[0].data.materials[0].node_tree.nodes.get('Image Texture').image.size)!=(1774,887):raise ValueError('Retain full original434 nodule bitmap')
    bpy.context.view_layer.update();from bpy_extras.object_utils import world_to_camera_view
    projected=[]
    for card in cards:
        corners=[]
        for vertex in card.data.vertices:
            p=world_to_camera_view(scene,scene.camera,card.matrix_world@vertex.co);corners.append([p.x*1024,(1-p.y)*1024])
        projected.append(corners)
    native.render(output/'closed.png',samples=8,transparent=True)
    record=dict(native_vertices=249,native_triangles=206,native_UVs=249,full_native_paint=[2048,2048],native_tint=material['tint'],whole_native_body_positions_UV_paint_pose_retained_both_states=True,yaw=210,camera_pitch=10,Cull2_clip0_5_modeled_not_full_Unity_shader=True,generated_missing_spade_only=True,spade_full_original_bitmap=[1024,1536],spade_working_RGB=.55,spade_fixed_center=[float(center[0]-.75),float(lo[1]+.07),float(lo[2]+1.12)],spade_fixed_full_quad_dimensions=[1.6,2.4],retained_original434_nodules_reused_not_regenerated=True,nodule_instances=2,new_generated_card_vertices=12,new_generated_card_quads=3,nodule_working_RGB=.55,nodule_full_quad_dimensions=[1.10,.55],projected_nodule_domains=projected,used_hides_only_two_nodule_cards=True,spade_and_native_heap_fixed=True,adapted_spent_state_not_canonical_burst_or_spade_motion=True,recipe_sha256=sha(__file__),source_files={str(p):sha(p)for p in (source/'scene.json',meshpath,paint,nodules,spade)},accepted=False,installed=False,native_gameplay_QA=False)
    (output/'provenance.json').write_text(json.dumps(record,indent=2))

if __name__=='__main__':
    parser=argparse.ArgumentParser(description=__doc__)
    for name in ('source','nodules','spade','native-tools','out'):parser.add_argument('--'+name,type=Path,required=True)
    args=parser.parse_args(sys.argv[sys.argv.index('--')+1:]);render(args.source,args.nodules,args.spade,args.native_tools,args.out)
