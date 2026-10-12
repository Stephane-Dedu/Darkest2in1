"""Render the actual Core terrain and verified buildings privately, without launching DD2.

This checks composition and geometry. Blender lighting does not establish native shader/UI acceptance.
"""
import argparse
import hashlib
import json
import math
import sys
from pathlib import Path

import bpy
import numpy as np
from bpy_extras.object_utils import world_to_camera_view
from mathutils import Matrix, Vector


def surface(name, data, materials, groups=None, shade=None):
    vertices=np.asarray(data['Positions'],dtype=float).reshape(-1,3)[:,[0,2,1]]
    groups=groups or [data['Indices']]
    indices=np.concatenate(groups).reshape(-1,3)[:,::-1]
    mesh=bpy.data.meshes.new(name)
    mesh.from_pydata(vertices.tolist(),[],indices.tolist()); mesh.update()
    obj=bpy.data.objects.new(name,mesh); bpy.context.collection.objects.link(obj)
    for material in materials: mesh.materials.append(material)
    slots=np.concatenate([np.full(len(g)//3,i,dtype=np.int32) for i,g in enumerate(groups)])
    mesh.polygons.foreach_set('material_index',slots)
    uv=np.asarray(data['Uv'],dtype=np.float32).reshape(-1,2)
    mesh.uv_layers.new().data.foreach_set('uv',uv[indices.reshape(-1)].reshape(-1))
    if shade is not None:
        colors=np.ones((len(vertices),4),dtype=np.float32)
        colors[:,:3]=np.asarray(shade)[:,None]
        mesh.color_attributes.new(name='TerrainShade',type='FLOAT_COLOR',domain='POINT').data.foreach_set('color',colors.reshape(-1))
    return obj


def painted_ground(name, texture, paint, vertex_shade=False, cutout=False):
    material=bpy.data.materials.new(name); material.use_nodes=True
    nodes=material.node_tree.nodes; links=material.node_tree.links; nodes.clear()
    image=nodes.new('ShaderNodeTexImage'); image.image=texture; image.extension='REPEAT'
    uv=nodes.new('ShaderNodeTexCoord'); crop=nodes.new('ShaderNodeVectorMath'); crop.operation='MULTIPLY_ADD'
    crop.inputs[1].default_value=(paint['UvScale']['X'],paint['UvScale']['Y'],1)
    crop.inputs[2].default_value=(paint['UvOffset']['X'],paint['UvOffset']['Y'],0)
    if paint['UvScale']['X']!=1 or paint['UvScale']['Y']!=1:
        separate=nodes.new('ShaderNodeSeparateXYZ'); links.new(uv.outputs['UV'],separate.inputs[0])
        repeat=nodes.new('ShaderNodeCombineXYZ')
        for axis in ['X','Y']:
            mirror=nodes.new('ShaderNodeMath'); mirror.operation='PINGPONG'; mirror.inputs[1].default_value=1
            links.new(separate.outputs[axis],mirror.inputs[0]); links.new(mirror.outputs[0],repeat.inputs[axis])
        links.new(repeat.outputs[0],crop.inputs[0])
    else: links.new(uv.outputs['UV'],crop.inputs[0])
    links.new(crop.outputs[0],image.inputs['Vector'])
    multiply=nodes.new('ShaderNodeMixRGB'); multiply.blend_type='MULTIPLY'; multiply.inputs[0].default_value=1
    tint=paint['Tint']
    multiply.inputs[2].default_value=(tint['X'],tint['Y'],tint['Z'],1); links.new(image.outputs['Color'],multiply.inputs[1])
    saturation=nodes.new('ShaderNodeHueSaturation'); saturation.inputs['Saturation'].default_value=paint['Saturation']
    links.new(multiply.outputs[0],saturation.inputs['Color'])
    lift=nodes.new('ShaderNodeMixRGB'); lift.blend_type='ADD'; lift.inputs[0].default_value=1
    lift.inputs[2].default_value=(*([paint['Brightness']]*3),1)
    links.new(saturation.outputs[0],lift.inputs[1]); color=lift.outputs[0]
    if vertex_shade:
        attr=nodes.new('ShaderNodeVertexColor'); attr.layer_name='TerrainShade'
        shade=nodes.new('ShaderNodeMixRGB'); shade.blend_type='MULTIPLY'; shade.inputs[0].default_value=1
        links.new(color,shade.inputs[1]); links.new(attr.outputs['Color'],shade.inputs[2]); color=shade.outputs[0]
    emission=nodes.new('ShaderNodeEmission'); links.new(color,emission.inputs['Color'])
    output=nodes.new('ShaderNodeOutputMaterial')
    if cutout:
        transparent=nodes.new('ShaderNodeBsdfTransparent'); mix=nodes.new('ShaderNodeMixShader')
        threshold=nodes.new('ShaderNodeMath'); threshold.operation='GREATER_THAN'; threshold.inputs[1].default_value=.4
        links.new(image.outputs['Alpha'],threshold.inputs[0]); links.new(threshold.outputs[0],mix.inputs[0])
        links.new(transparent.outputs[0],mix.inputs[1]); links.new(emission.outputs[0],mix.inputs[2]); links.new(mix.outputs[0],output.inputs[0])
    else: links.new(emission.outputs[0],output.inputs[0])
    return material


def world_ground(soil,stone,grass,palette,stains,cover):
    """Approximate the native one-time world paint bake with the exported Core stain field."""
    size=2048
    def tile(image,paint,u,v):
        pixels=np.array(image.pixels[:],dtype=np.float32).reshape(image.size[1],image.size[0],4)
        mirror=lambda value:1-np.abs(np.mod(value,2)-1)
        xx=(mirror(u)*paint['UvScale']['X']+paint['UvOffset']['X'])*(image.size[0]-1)
        yy=(mirror(v)*paint['UvScale']['Y']+paint['UvOffset']['Y'])*(image.size[1]-1)
        color=pixels[np.clip(yy.astype(int),0,image.size[1]-1),np.clip(xx.astype(int),0,image.size[0]-1),:3]
        t=paint['Tint']; color=color*np.array([t['X'],t['Y'],t['Z']])
        luma=np.sum(color*np.array([.2126,.7152,.0722]),axis=2,keepdims=True)
        return np.clip(luma+(color-luma)*paint['Saturation']+paint['Brightness'],0,1)
    x,z=np.meshgrid(np.linspace(-66,58,size),np.linspace(-36,69,size))
    earth=tile(soil,palette['Soil'],x/10,z/3); gravel=tile(stone,palette['Paving'],x/5.5+.37,z/5.5+.19)
    field=np.array([[s['Tint']['X'],s['Tint']['Y'],s['Tint']['Z'],s['Gravel']] for s in stains],dtype=np.float32).reshape(128,128,4)
    # Bilinear interpolation of the Core samples, rather than a second procedural noise recipe.
    at=np.linspace(0,127,size); lo=at.astype(int); hi=np.minimum(lo+1,127); f=at-lo
    horizontal=field[:,lo,:]*(1-f)[None,:,None]+field[:,hi,:]*f[None,:,None]
    field=horizontal[lo,:,:]*(1-f)[:,None,None]+horizontal[hi,:,:]*f[:,None,None]
    encode=lambda v:np.where(v<=.0031308,v*12.92,1.055*np.maximum(v,0)**(1/2.4)-.055)
    color=encode(earth)*(1-field[:,:,3:])+encode(gravel)*field[:,:,3:]
    grass_field=np.asarray(cover,dtype=np.float32).reshape(256,256)
    at=np.linspace(0,255,size); lo=at.astype(int); hi=np.minimum(lo+1,255); f=at-lo
    horizontal=grass_field[:,lo]*(1-f)[None,:]+grass_field[:,hi]*f[None,:]
    grass_field=horizontal[lo,:]*(1-f)[:,None]+horizontal[hi,:]*f[:,None]
    turf=tile(grass,palette['Grass'],x/7.5,z/7.5)
    color=(color*(1-grass_field[:,:,None])+encode(turf)*grass_field[:,:,None])*field[:,:,:3]
    color=np.where(color<=.04045,color/12.92,((color+.055)/1.055)**2.4)
    rgba=np.ones((size,size,4),dtype=np.float32); rgba[:,:,:3]=color
    image=bpy.data.images.new('World earth and gravel',width=size,height=size)
    image.pixels.foreach_set(rgba.reshape(-1)); image.pack()
    neutral={'Tint':{'X':1,'Y':1,'Z':1},'Brightness':0,'Saturation':1,
             'UvScale':{'X':1,'Y':1},'UvOffset':{'X':0,'Y':0}}
    return painted_ground('World earth and gravel',image,neutral,True)


def quiet_building_materials(objects):
    # Composition preview only. DD2 still establishes native lighting/material acceptance.
    for material in {m for obj in objects for m in obj.data.materials if m}:
        shader=next((n for n in material.node_tree.nodes if n.type=='BSDF_PRINCIPLED'),None)
        if shader is None: continue
        basis=shader.inputs['Base Color']; emission=shader.inputs['Emission Strength']
        if sum(basis.default_value[:3])<.001 and not basis.is_linked:
            emission.default_value=.9
        elif emission.default_value<=1.1:
            emission.default_value=0
        shader.inputs['Roughness'].default_value=1; shader.inputs['Specular IOR Level'].default_value=0


def main(args):
    repo=Path(__file__).resolve().parents[2]
    args.models=args.models.resolve(); args.kit=args.kit.resolve()
    out=args.out.resolve()
    if out==repo or repo in out.parents: raise ValueError('Keep private renders outside Git.')
    out.mkdir(parents=True,exist_ok=True)
    data=json.loads(args.layout.read_text())
    models=json.loads((args.models/'models.json').read_text())['models']
    verified=json.loads((args.models/'verified.json').read_text())
    sys.path.insert(0,str(args.kit/'tools'))
    import bl_lib as L
    L.reset()
    texture=bpy.data.images.load(str(out/'road_rough_patch.png'))
    soil=bpy.data.images.load(str(out/'farm_ground_hill_transition.png'))
    cliff=bpy.data.images.load(str(out/'bricks.png'))
    wall=bpy.data.images.load(str(out/'foundation_panel_06.png'))
    grass=bpy.data.images.load(str(out/'grass_dry.png'))
    tufts=bpy.data.images.load(str(out/'farm_gate_node_assets_grass.png'))
    palette=data['Palette']
    earth=world_ground(soil,texture,grass,palette,data['GroundStains'],data['GrassCover'])
    rock=painted_ground('Native painted rock',cliff,palette['Rock'],True)
    paving=painted_ground('Native foundation paving',texture,palette['Paving'])
    stone=painted_ground('Native foundation retaining courses',wall,palette['Masonry'])
    terrain=data['Terrain']
    surface('Core cliff landscape',terrain,[earth,rock],[terrain['Ground'],terrain['Rock']],terrain['Shade'])
    surface('Core lanes and stairs',terrain['Paving'],[paving])
    surface('Core retaining courses',terrain['Masonry'],[stone])
    surface('Core stair flights and landings',terrain['Stairs'],[stone])
    valley_paint={**palette['Grass'],'Tint':{k:v*f for (k,v),f in zip(palette['Grass']['Tint'].items(),[.52,.59,.65])}}
    surface('Core surrounding valley',terrain['Surroundings'],[painted_ground('Surrounding valley',grass,valley_paint)])
    surface('Core native grass tufts',data['Vegetation'],[painted_ground('Native grass silhouettes',tufts,palette['GrassTuft'],cutout=True)])
    buildings={}
    for site in data['Sites']:
        key=site['Id']; path=args.models/models[key]['glb']
        if hashlib.sha256(path.read_bytes()).hexdigest()!=verified[key]['sha256']:
            raise ValueError('Model changed after verification: '+key)
        before=set(bpy.data.objects)
        bpy.ops.import_scene.gltf(filepath=str(path))
        objects=[obj for obj in bpy.data.objects if obj not in before and obj.type=='MESH']
        points=np.array([tuple(obj.matrix_world@v.co) for obj in objects for v in obj.data.vertices])
        lo,hi=points.min(0),points.max(0); scale=site['Width']/max((hi-lo)[:2])
        center=Vector(((lo[0]+hi[0])/2,(lo[1]+hi[1])/2,lo[2]))
        placement=(Matrix.Translation((site['X'],site['Z'],site['Y']))@
                   Matrix.Rotation(math.radians(-site['Yaw']),4,'Z')@
                   Matrix.Scale(scale,4)@Matrix.Translation(-center))
        for obj in objects:
            original=obj.matrix_world.copy(); obj.parent=None; obj.matrix_world=placement@original
            obj['Service']=key
        quiet_building_materials(objects)
        buildings[key]=objects
    missing=[]
    for piece in data['Scenery']:
        folder=args.kit/'exp'/piece['Id']
        if not (folder/'scene.json').exists(): folder=out/'exp'/piece['Id']
        if not (folder/'scene.json').exists(): missing.append(piece['Id']); continue
        lo,hi=L.bbox(folder)
        if not np.isfinite(hi-lo).all(): raise ValueError('Invalid native scenery: '+piece['Id'])
        scale=piece['Width']/max((hi-lo)[[0,2]])
        placement,_=L.place_matrix(folder,(piece['X'],piece['Y'],piece['Z']),rot=piece['Yaw'],scale=scale)
        # Scale about the normalized ground pivot in Unity space, before axis conversion.
        deform=np.diag([1,piece['HeightScale'],piece['DepthScale'],1])
        pivot=np.eye(4); pivot[:3,3]=[piece['X'],piece['Y'],piece['Z']]
        placement=pivot@deform@np.linalg.inv(pivot)@placement
        objects,_,_=L.build(folder,placement)
        if piece.get('Background') and piece['Id']=='camp_treeline':
            for obj in objects:
                for slot in obj.material_slots:
                    for node in slot.material.node_tree.nodes:
                        if node.type=='MIX_RGB' and node.blend_type=='MULTIPLY': node.inputs[2].default_value=(.7,.82,1,1)
        elif piece.get('Background') or piece['Id']=='mainmenu_mountain_mdl' or piece['Id'].startswith(('barricade_military_decor_rocks','coast_skirt_cliffs')):
            paint=palette['Woodland' if piece.get('Background') else 'Mountain' if piece['Id']=='mainmenu_mountain_mdl' else 'Rubble']
            for obj in objects:
                for slot in obj.material_slots:
                    texture_node=next(n for n in slot.material.node_tree.nodes if n.type=='TEX_IMAGE')
                    slot.material=painted_ground('Graded '+piece['Id'],texture_node.image,paint,cutout=piece.get('Background',False))
    target=data['CameraTarget']; offset=data['CameraOffset']
    aim=Vector((target['X'],target['Z'],target['Y']))
    position=aim+Vector((offset['X'],offset['Z'],offset['Y']))
    camera=L.camera(position,aim,fov=data['FieldOfView'],res=(args.width,round(args.width*9/16)))
    sc=bpy.context.scene
    # The same warm/cool authoring light used by the verified building gallery.
    sys.path.insert(0,str(Path(__file__).resolve().parent))
    from render import light
    light('Warm overcast',(-40,-70,95),30000,70,(1,.81,.64))
    light('Cool sky',(45,10,80),22000,65,(.55,.69,1))
    sc.world=bpy.data.worlds.new('Charcoal sky'); sc.world.use_nodes=True
    sc.world.node_tree.nodes['Background'].inputs['Color'].default_value=(.065,.075,.105,1)
    sc.world.node_tree.nodes['Background'].inputs['Strength'].default_value=.4
    sc.render.engine='CYCLES'; sc.cycles.samples=args.samples; sc.cycles.use_denoising=True
    sc.cycles.max_bounces=4; sc.cycles.transparent_max_bounces=32
    sc.view_settings.view_transform='Standard'; sc.view_settings.look='None'
    sc.render.image_settings.file_format='PNG'; sc.render.image_settings.color_mode='RGB'
    sc.render.filepath=str(out/(args.name+'.png'))
    bpy.context.view_layer.update()
    screen={}
    for key,objects in buildings.items():
        projected=[world_to_camera_view(sc,camera,obj.matrix_world@Vector(corner)) for obj in objects for corner in obj.bound_box]
        screen[key]={'x':[min(p.x for p in projected),max(p.x for p in projected)],
                     'y':[min(1-p.y for p in projected),max(1-p.y for p in projected)]}
    (out/'composition.json').write_text(json.dumps({'notice':__doc__,'projected_bounds':screen,'missing_offline_scenery':missing},indent=2))
    bpy.ops.render.render(write_still=True)
    if args.blend:
        bpy.ops.file.pack_all(); bpy.ops.wm.save_as_mainfile(filepath=str(out/(args.name+'.blend')))
    print('TOWN PREVIEW COMPLETE',sc.render.filepath,flush=True)


if __name__=='__main__':
    parser=argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--models',type=Path,required=True)
    parser.add_argument('--kit',type=Path,required=True)
    parser.add_argument('--layout',type=Path,required=True)
    parser.add_argument('--out',type=Path,required=True)
    parser.add_argument('--width',type=int,default=1920)
    parser.add_argument('--samples',type=int,default=16)
    parser.add_argument('--name',default='hamlet-landscape-v04')
    parser.add_argument('--blend',action='store_true')
    main(parser.parse_args(sys.argv[sys.argv.index('--')+1:]))
