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


def painted_ground(name, texture, tint, vertex_shade=False):
    material=bpy.data.materials.new(name); material.use_nodes=True
    nodes=material.node_tree.nodes; links=material.node_tree.links; nodes.clear()
    image=nodes.new('ShaderNodeTexImage'); image.image=texture; image.extension='REPEAT'
    multiply=nodes.new('ShaderNodeMixRGB'); multiply.blend_type='MULTIPLY'; multiply.inputs[0].default_value=1
    multiply.inputs[2].default_value=(*tint,1); links.new(image.outputs['Color'],multiply.inputs[1])
    color=multiply.outputs[0]
    if vertex_shade:
        attr=nodes.new('ShaderNodeVertexColor'); attr.layer_name='TerrainShade'
        shade=nodes.new('ShaderNodeMixRGB'); shade.blend_type='MULTIPLY'; shade.inputs[0].default_value=1
        links.new(color,shade.inputs[1]); links.new(attr.outputs['Color'],shade.inputs[2]); color=shade.outputs[0]
    emission=nodes.new('ShaderNodeEmission'); links.new(color,emission.inputs['Color'])
    output=nodes.new('ShaderNodeOutputMaterial'); links.new(emission.outputs[0],output.inputs[0])
    return material


def main(args):
    repo=Path(__file__).resolve().parents[2]
    out=args.out.resolve()
    if out==repo or repo in out.parents: raise ValueError('Keep private renders outside Git.')
    out.mkdir(parents=True,exist_ok=True)
    data=json.loads(args.layout.read_text())
    models=json.loads((args.models/'models.json').read_text())['models']
    verified=json.loads((args.models/'verified.json').read_text())
    sys.path.insert(0,str(args.kit/'tools'))
    import bl_lib as L
    L.reset()
    texture=bpy.data.images.load(str(out/'foundation_ground.png'))
    soil=bpy.data.images.load(str(out/'forest_ground_distance.png'))
    cliff=bpy.data.images.load(str(out/'cultist_barricade_rocks.png'))
    earth=painted_ground('Native painted soil',soil,(2.7,1.3,1.0),True)
    rock=painted_ground('Native painted rock',cliff,(.90,.92,.94),True)
    paving=painted_ground('Native foundation paving',texture,(.78,.73,.62))
    stone=painted_ground('Native foundation retaining courses',texture,(.62,.63,.61))
    terrain=data['Terrain']
    surface('Core cliff landscape',terrain,[earth,rock],[terrain['Ground'],terrain['Rock']],terrain['Shade'])
    surface('Core lanes and stairs',terrain['Paving'],[paving])
    surface('Core retaining courses',terrain['Masonry'],[stone])
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
        buildings[key]=objects
    missing=[]
    for piece in data['Scenery']:
        folder=args.kit/'exp'/piece['Id']
        if not (folder/'scene.json').exists(): missing.append(piece['Id']); continue
        lo,hi=L.bbox(folder)
        if not np.isfinite(hi-lo).all(): raise ValueError('Invalid native scenery: '+piece['Id'])
        scale=piece['Width']/max((hi-lo)[[0,2]])
        placement,_=L.place_matrix(folder,(piece['X'],piece['Y'],piece['Z']),rot=piece['Yaw'],scale=scale)
        L.build(folder,placement)
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
    sc.render.filepath=str(out/'hamlet-landscape-v03.png')
    bpy.context.view_layer.update()
    screen={}
    for key,objects in buildings.items():
        projected=[world_to_camera_view(sc,camera,obj.matrix_world@Vector(corner)) for obj in objects for corner in obj.bound_box]
        screen[key]={'x':[min(p.x for p in projected),max(p.x for p in projected)],
                     'y':[min(1-p.y for p in projected),max(1-p.y for p in projected)]}
    (out/'composition.json').write_text(json.dumps({'notice':__doc__,'projected_bounds':screen,'missing_offline_scenery':missing},indent=2))
    bpy.ops.render.render(write_still=True)
    if args.blend:
        bpy.ops.file.pack_all(); bpy.ops.wm.save_as_mainfile(filepath=str(out/'hamlet-landscape-v03.blend'))
    print('TOWN PREVIEW COMPLETE',sc.render.filepath,flush=True)


if __name__=='__main__':
    parser=argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--models',type=Path,required=True)
    parser.add_argument('--kit',type=Path,required=True)
    parser.add_argument('--layout',type=Path,required=True)
    parser.add_argument('--out',type=Path,required=True)
    parser.add_argument('--width',type=int,default=1920)
    parser.add_argument('--samples',type=int,default=16)
    parser.add_argument('--blend',action='store_true')
    main(parser.parse_args(sys.argv[sys.argv.index('--')+1:]))
