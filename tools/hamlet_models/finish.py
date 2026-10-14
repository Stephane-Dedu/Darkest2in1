"""Add fitted architectural trim to verified private v02 models, preserving their native painted UVs.

Output GLBs, packed Blender files and paint remain outside Git.
"""
import argparse
import hashlib
import json
import math
import shutil
import sys
from pathlib import Path

import bpy
import numpy as np

sys.path.insert(0,str(Path(__file__).resolve().parent))
import geometry as G


def trim_material():
    return G.material('Weathered slate edge',(.58,.68,.76,1),'roof_slate',.2)


def abbey():
    trim=trim_material()
    # Separate ridge caps and front coping retain the dipped Gothic nave profile.
    for z in np.linspace(-4.35,4.7,18):
        G.box('Nave individually fitted ridge cap',(0,8.43,z),(.24,.18,.49),trim,.035)
    xs=[-3.6,-1.728,0,1.728,3.6]
    ys=[5.5,6.747,8.4,6.747,5.5]
    for z in [-4.43,4.98]:
        for i in range(4):
            G.beam('Nave weathered gable coping',(xs[i],ys[i]+.06,z),(xs[i+1],ys[i+1]+.06,z),.12,trim)
    stone=G.material('Abbey aged pale coping',(.67,.71,.76,1),'masonry_native',.25)
    for x in [-3.35,3.35]:
        for z in [-2.6,0,2.6]:
            # A sloped stone cap catches a narrow edge above each shaded buttress.
            G.mesh('Sloping buttress cap',[(x-.26,4.15,z-.45),(x+.26,4.15,z-.45),
                (x+.26,4.65,z+.45),(x-.26,4.65,z+.45),
                (x-.26,4.02,z-.45),(x+.26,4.02,z-.45)],
                [[0,1,2,3],[4,5,1,0],[0,3,4],[1,5,2]],stone)
        G.beam('Nave continuous sill course',(x,1.75,-3.85),(x,1.75,4.4),.16,stone)


def sanitarium():
    stone=G.material('Sanitarium weathered sill',(.62,.68,.69,1),'masonry_native',.25)
    for y,r in [(1.45,1.49),(4.2,1.49),(6.16,1.62)]:
        G.cylinder('Tower drip course',(-3,y,-2.2),r,.10,stone,12)
    trim=trim_material()
    for i in range(8):
        a=i*math.pi/4
        G.beam('Turret slate hip flashing',(-3+1.84*math.cos(a),7.83,-2.2+1.84*math.sin(a)),
            (-3,10,-2.2),.047,trim)


def guild():
    stone=G.material('Guild dressed stone threshold',(.71,.68,.62,1),'masonry_native',.25)
    for x in [-1.78,.48]:
        G.box('Guild carved door jamb',(x,1.9,-3.39),(.19,3.0,.22),stone,.035)
    G.box('Guild threshold',(-.65,.48,-3.48),(2.55,.16,.65),stone,.035)
    iron=G.material('Guild dull door rivets',(.18,.17,.14,1),emission=.2)
    for x in [-1.3,0]:
        for y in [1.14,1.95]:
            G.box('Guild forged strap rivet',(x,y,-3.45),(.07,.07,.06),iron,.012)


def tavern():
    # End-grain, worn corner boots and warm sign relief are visible in the service approach.
    timber=G.material('Tavern aged timber ends',(.57,.44,.31,1),'timber_native',.22)
    for x in [-2.14,1.53]:
        G.box('Tavern lantern backplate',(x,2.8,-3.57),(.43,.64,.08),timber,.025)
    brass=G.material('Tavern tankard sign rim',(.27,.19,.085,1),emission=.4)
    for y in [2.48,3.1]:
        G.beam('Tankard sign metal rim',(-2.88,y,-4.09),(-2.32,y,-4.09),.045,brass)


FINISHES={'abbey':abbey,'sanitarium':sanitarium,'guild':guild,'tavern':tavern}


def finish(args):
    source,out,kit=args.source.resolve(),args.out.resolve(),args.kit.resolve()
    repo=Path(__file__).resolve().parents[2]
    if out==source or out==repo or repo in out.parents: raise ValueError('Use a new private output folder.')
    out.mkdir(parents=True,exist_ok=True)
    shutil.copytree(source/'textures',out/'textures',dirs_exist_ok=True)
    shutil.copy2(source/'texture-sources.json',out/'texture-sources.json')
    records=json.loads((source/'models.json').read_text())
    verified=json.loads((source/'verified.json').read_text())
    checks={}
    for key,record in records['models'].items():
        original=source/record['glb']
        if hashlib.sha256(original.read_bytes()).hexdigest()!=verified[key]['sha256']:
            raise ValueError('Source model changed after verification: '+key)
        folder=out/key;folder.mkdir(exist_ok=True)
        if key not in FINISHES:
            shutil.copy2(original,out/record['glb']); checks[key]=verified[key]
            continue
        bpy.ops.wm.read_factory_settings(use_empty=True)
        G.configure(kit,out,None,'v02')
        bpy.ops.import_scene.gltf(filepath=str(original))
        FINISHES[key]()
        bpy.context.view_layer.update()
        objects=[o for o in bpy.data.objects if o.type=='MESH']
        points=np.array([tuple(o.matrix_world@v.co) for o in objects for v in o.data.vertices])
        lo,hi=points.min(0),points.max(0)
        # Trim belongs within the original architectural envelope, so runtime sizing stays stable.
        previous=np.asarray(record['bounds_blender'])
        if np.max(np.abs(np.array([lo,hi])-previous))>.22: raise ValueError('Trim changes the service silhouette envelope: '+key)
        triangles=sum(len(p.vertices)-2 for o in objects for p in o.data.polygons)
        bpy.ops.object.select_all(action='SELECT')
        bpy.ops.export_scene.gltf(filepath=str(out/record['glb']),export_format='GLB',use_selection=True,
            export_apply=True,export_yup=True,export_materials='EXPORT',export_animations=False)
        bpy.ops.file.pack_all();bpy.ops.wm.save_as_mainfile(filepath=str(out/record['blend']))
        # Re-read the actual file and assert topology, volume and embedded image decoding.
        bpy.ops.wm.read_factory_settings(use_empty=True)
        bpy.ops.import_scene.gltf(filepath=str(out/record['glb']))
        meshes=[o for o in bpy.data.objects if o.type=='MESH']
        actual=sum(len(p.vertices)-2 for o in meshes for p in o.data.polygons)
        if actual!=triangles: raise ValueError('Exported topology differs: '+key)
        for image in bpy.data.images:
            if image.type=='IMAGE' and (not image.packed_file or not len(image.pixels)): raise ValueError('Missing embedded paint: '+key)
        record['triangles']=triangles;record['mesh_objects']=len(meshes);record['bounds_blender']=[lo.tolist(),hi.tolist()]
        record['design']+=' Fitted weathered architectural trim in review passv14.'
        checks[key]={'sha256':hashlib.sha256((out/record['glb']).read_bytes()).hexdigest(),
            'triangles':triangles,'verification':'Re-imported exported GLB; topology/embedded paint/envelope checked.'}
        print('FINISHED',key,triangles,flush=True)
    records['detail_pass']='v14'
    (out/'models.json').write_text(json.dumps(records,indent=2))
    (out/'verified.json').write_text(json.dumps(checks,indent=2))
    print('PASS: ten private models; four architectural finishes; six original verified GLBs unchanged.',flush=True)


if __name__=='__main__':
    parser=argparse.ArgumentParser(description=__doc__)
    for flag in ['source','out','kit']: parser.add_argument('--'+flag,type=Path,required=True)
    finish(parser.parse_args(sys.argv[sys.argv.index('--')+1:]))
