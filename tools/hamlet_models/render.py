"""Blender entry point: build, export and render each private Hamlet model from four angles."""
import argparse
import json
import math
import sys
from pathlib import Path

import bpy
import numpy as np
from mathutils import Vector

sys.path.insert(0, str(Path(__file__).resolve().parent))
import geometry as G
from buildings import BUILDERS, DESIGN


def camera(location, target, field=32, resolution=1024):
    data=bpy.data.cameras.new('Building study camera')
    ob=bpy.data.objects.new('Building study camera',data)
    bpy.context.collection.objects.link(ob)
    ob.location=location
    ob.rotation_euler=(Vector(target)-Vector(location)).to_track_quat('-Z','Y').to_euler()
    data.sensor_fit='VERTICAL'
    data.sensor_height=24
    data.lens=12/math.tan(math.radians(field)/2)
    data.clip_start=.05
    data.clip_end=1000
    sc=bpy.context.scene
    sc.camera=ob
    sc.render.resolution_x=sc.render.resolution_y=resolution
    sc.render.resolution_percentage=100
    return ob


def light(name,position,energy,size,color):
    data=bpy.data.lights.new(name,'AREA')
    data.energy=energy
    data.shape='DISK'
    data.size=size
    data.color=color
    ob=bpy.data.objects.new(name,data)
    bpy.context.collection.objects.link(ob)
    ob.location=position
    ob.rotation_euler=(Vector((0,0,3))-ob.location).to_track_quat('-Z','Y').to_euler()


def finish_scene():
    sc=bpy.context.scene
    sc.render.engine='CYCLES'
    sc.cycles.samples=20
    sc.cycles.use_denoising=True
    sc.cycles.max_bounces=5
    sc.cycles.transparent_max_bounces=32
    sc.render.film_transparent=False
    sc.view_settings.view_transform='Standard'
    sc.view_settings.look='None'
    sc.view_settings.exposure=.1
    sc.world=bpy.data.worlds.new('Neutral charcoal backdrop')
    sc.world.use_nodes=True
    bg=sc.world.node_tree.nodes.get('Background')
    bg.inputs['Color'].default_value=(.20,.23,.28,1)
    bg.inputs['Strength'].default_value=.4
    bpy.ops.mesh.primitive_plane_add(size=200,location=(0,0,-.055))
    ob=bpy.context.object
    ob.name='Study floor, not part of exported building'
    ob.data.materials.append(G.material('Study charcoal',(.035,.043,.052,1),emission=.3))
    light('Warm overcast key',(-10,-14,20),1800,12,(1,.81,.64))
    light('Cold fill',(12,-2,13),1000,10,(.55,.69,1))
    light('Roof rim',(-4,12,18),1600,9,(.74,.82,1))
    sc.render.image_settings.file_format='PNG'
    sc.render.image_settings.color_mode='RGBA'


def build(args):
    kit,out=args.kit.resolve(),args.out.resolve()
    repo=Path(__file__).resolve().parents[2]
    if out==repo or repo in out.parents:
        raise ValueError('Private models must stay outside the Git repository.')
    if not (out/'texture-sources.json').exists():
        raise ValueError('Run prepare.py first.')
    sys.path.insert(0,str(kit/'tools'))
    import bl_lib as L
    builders,design=BUILDERS,DESIGN
    if args.detail_pass=='v02':
        from buildings_v02 import BUILDERS as builders, DESIGN as design
    selected=args.only.split(',') if args.only else list(BUILDERS)
    if not set(selected)<=set(builders):
        raise ValueError('Unknown building selection.')
    angles={'front':0,'hero':35,'rear':145,'side':-75}
    views=args.views.split(',')
    if not set(views)<=set(angles) or args.samples<1 or args.resolution<1:
        raise ValueError('Invalid camera views, resolution or sample count.')
    path=out/'models.json'
    manifest=json.loads(path.read_text()) if path.exists() else {'version':1,'models':{}}
    if manifest['models'] and manifest.get('detail_pass','v01')!=args.detail_pass:
        raise ValueError('Use separate output directories for different modeling passes.')
    manifest['detail_pass']=args.detail_pass
    for key in selected:
        L.reset()
        G.configure(kit,out,L,args.detail_pass)
        builders[key]()
        bpy.context.view_layer.update()
        objects=[o for o in bpy.data.objects if o.type=='MESH']
        points=np.array([tuple(o.matrix_world@v.co) for o in objects for v in o.data.vertices])
        lo,hi=points.min(0),points.max(0)
        if not np.isfinite(points).all() or min(hi-lo)<.5:
            raise ValueError('Invalid or flat building: '+key)
        triangles=sum(sum(len(p.vertices)-2 for p in o.data.polygons) for o in objects)
        folder=out/key
        folder.mkdir(exist_ok=True)
        bpy.ops.object.select_all(action='DESELECT')
        for ob in objects:
            ob.select_set(True)
        bpy.context.view_layer.objects.active=objects[0]
        bpy.ops.export_scene.gltf(filepath=str(folder/f'{key}.glb'),export_format='GLB',use_selection=True,
            export_apply=True,export_yup=True,export_materials='EXPORT',export_animations=False)
        finish_scene()
        bpy.context.scene.cycles.samples=args.samples
        centre=(lo+hi)/2
        target=(centre[0],centre[1],lo[2]+(hi[2]-lo[2])*.43)
        radius=max(hi[0]-lo[0],hi[1]-lo[1],hi[2]-lo[2])*2.15
        cam=None
        for view in views:
            angle=angles[view]
            a=math.radians(angle)
            location=(target[0]+math.sin(a)*radius,target[1]-math.cos(a)*radius,target[2]+radius*.47)
            if cam is None:
                cam=camera(location,target,resolution=args.resolution)
            else:
                cam.location=location
                cam.rotation_euler=(Vector(target)-cam.location).to_track_quat('-Z','Y').to_euler()
            bpy.context.scene.render.filepath=str(folder/f'{view}.png')
            bpy.ops.render.render(write_still=True)
            print('RENDERED',key,view,flush=True)
        # Keep the hero view as the opening view of the editable Blender document.
        a=math.radians(35)
        cam.location=(target[0]+math.sin(a)*radius,target[1]-math.cos(a)*radius,target[2]+radius*.47)
        cam.rotation_euler=(Vector(target)-cam.location).to_track_quat('-Z','Y').to_euler()
        bpy.ops.file.pack_all()
        bpy.ops.wm.save_as_mainfile(filepath=str(folder/f'{key}.blend'))
        manifest['models'][key]={'design':design[key],'mesh_objects':len(objects),'triangles':triangles,
            'bounds_blender':[lo.tolist(),hi.tolist()],'native_parts':list(G.SOURCES),
            'glb':f'{key}/{key}.glb','blend':f'{key}/{key}.blend',
            'views':{name:f'{key}/{name}.png' for name in views}}
        path.write_text(json.dumps(manifest,indent=2),encoding='utf-8')
        print('MODEL COMPLETE',key,len(objects),triangles,flush=True)


if __name__=='__main__':
    parser=argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--kit',required=True,type=Path)
    parser.add_argument('--out',required=True,type=Path)
    parser.add_argument('--only')
    parser.add_argument('--resolution',type=int,default=1024)
    parser.add_argument('--samples',type=int,default=20)
    parser.add_argument('--views',default='front,hero,rear,side')
    parser.add_argument('--detail-pass',choices=['v01','v02'],default='v01')
    args=parser.parse_args(sys.argv[sys.argv.index('--')+1:])
    build(args)
