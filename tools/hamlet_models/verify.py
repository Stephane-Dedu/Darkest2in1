"""Re-import each exported GLB, check its volume/materials and render the file itself."""
import argparse
import hashlib
import json
import math
import struct
import sys
from pathlib import Path

import bpy
import numpy as np

sys.path.insert(0,str(Path(__file__).resolve().parent))
import geometry as G
from buildings import BUILDERS
from render import camera, finish_scene


def check_painted_factors(path):
    data=path.read_bytes()
    magic,version,length=struct.unpack_from('<4sII',data)
    if magic!=b'glTF' or version!=2 or length!=len(data):
        raise ValueError('Invalid GLB header: '+str(path))
    size,kind=struct.unpack_from('<I4s',data,12)
    if kind!=b'JSON':
        raise ValueError('Missing GLB JSON chunk: '+str(path))
    doc=json.loads(data[20:20+size])
    if any('uri' in item for item in doc.get('images',[])+doc.get('buffers',[])):
        raise ValueError('GLB references external data: '+str(path))
    exported={m['name']:m for m in doc['materials']}
    checked=0
    for mat in bpy.data.materials:
        if not mat.use_nodes:
            continue
        shader=next((n for n in mat.node_tree.nodes if n.type=='BSDF_PRINCIPLED'),None)
        if shader is None or not shader.inputs['Base Color'].is_linked:
            continue
        node=shader.inputs['Base Color'].links[0].from_node
        if node.type!='MIX' or node.data_type!='RGBA' or node.blend_type!='MULTIPLY':
            continue
        expected=np.clip(node.inputs[7].default_value[:3],0,1)
        record=exported[mat.name].get('pbrMetallicRoughness',{})
        actual=record.get('baseColorFactor',[1,1,1,1])[:3]
        if 'baseColorTexture' not in record or not np.allclose(expected,actual,atol=1e-6):
            raise ValueError('Painted color factor lost during export: '+mat.name)
        checked+=1
    if checked==0:
        raise ValueError('No painted material factors checked: '+str(path))
    return checked


def verify(kit,out,resolution):
    repo=Path(__file__).resolve().parents[2]
    if out==repo or repo in out.parents:
        raise ValueError('Private model verification outputs must stay outside Git.')
    manifest=json.loads((out/'models.json').read_text())
    if set(manifest['models'])!=set(BUILDERS):
        raise ValueError('The model set does not contain all ten Hamlet locations.')
    (out/'verified.json').unlink(missing_ok=True)
    checks={}
    for key,record in manifest['models'].items():
        path=out/record['glb']
        bpy.ops.wm.open_mainfile(filepath=str(out/record['blend']))
        factors=check_painted_factors(path)
        bpy.ops.wm.read_factory_settings(use_empty=True)
        G.configure(kit,out,None)
        bpy.ops.import_scene.gltf(filepath=str(path))
        bpy.context.view_layer.update()
        objects=[o for o in bpy.data.objects if o.type=='MESH']
        points=np.array([tuple(o.matrix_world@v.co) for o in objects for v in o.data.vertices])
        if not len(points) or not np.isfinite(points).all():
            raise ValueError('Empty or non-finite export: '+key)
        lo,hi=points.min(0),points.max(0)
        if min(hi-lo)<.5:
            raise ValueError('Flat building export: '+key)
        error=float(np.max(np.abs(np.array([lo,hi])-record['bounds_blender'])))
        if error>.01:
            raise ValueError(f'{key} bounds changed during export: {error}')
        triangles=sum(sum(len(p.vertices)-2 for p in o.data.polygons) for o in objects)
        if triangles!=record['triangles']:
            raise ValueError(f'{key} topology changed: {triangles} vs {record["triangles"]}')
        images=[im for im in bpy.data.images if im.type=='IMAGE']
        # Blender's glTF importer defers decoding packed images until pixels are read.
        if not images or any(not im.packed_file or min(im.size)<1 or
                             len(im.pixels)!=im.size[0]*im.size[1]*im.channels for im in images):
            raise ValueError('Unresolved painted texture: '+key)
        for name in ['front','hero','rear','side']:
            if not (out/record['views'][name]).is_file():
                raise ValueError('Missing angle: '+key+'/'+name)
        finish_scene()
        centre=(lo+hi)/2
        target=(centre[0],centre[1],lo[2]+(hi[2]-lo[2])*.43)
        radius=max(hi-lo)*2.15
        a=math.radians(35)
        camera((target[0]+math.sin(a)*radius,target[1]-math.cos(a)*radius,target[2]+radius*.47),target,resolution=resolution)
        bpy.context.scene.render.filepath=str(out/key/'exported.png')
        bpy.ops.render.render(write_still=True)
        checks[key]={'triangles':triangles,'mesh_objects':len(objects),'size_blender':(hi-lo).tolist(),
            'bounds_error':error,'embedded_images':len(images),'painted_factors_checked':factors,
            'sha256':hashlib.sha256(path.read_bytes()).hexdigest(),
            'render':f'{key}/exported.png'}
        (out/'verified.json').write_text(json.dumps(checks,indent=2),encoding='utf-8')
        print('VERIFIED',key,triangles,len(images),'textures',flush=True)
    print('PASS: all ten GLBs re-imported, topology/bounds/textures/color factors checked and rendered',flush=True)


if __name__=='__main__':
    parser=argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--kit',required=True,type=Path)
    parser.add_argument('--out',required=True,type=Path)
    parser.add_argument('--resolution',type=int,default=1024)
    args=parser.parse_args(sys.argv[sys.argv.index('--')+1:])
    verify(args.kit.resolve(),args.out.resolve(),args.resolution)
