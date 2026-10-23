"""Bake bounded service self-shadow visibility into the existing contact channel.

Uses Core world geometry/normals and the current service key. Original buffer
bytes, geometry, UVs, paintings and material definitions stay intact. Flat
connected faces share area-weighted exposure to avoid triangulation diagonals.
No new runtime path or material batch is required. Inspect native results.
"""
import argparse
import hashlib
import json
import math
import shutil
import sys
from pathlib import Path

import numpy as np
from mathutils import Vector
from mathutils.bvhtree import BVHTree

sys.path.insert(0,str(Path(__file__).resolve().parent))
from bake_contact import read_accessor,planar_visibility
from shadow_field import inputs,directions

SAMPLES=((1/3,1/3,1/3),(2/3,1/6,1/6),(1/6,2/3,1/6),(1/6,1/6,2/3),
         (.1,.45,.45),(.45,.1,.45),(.45,.45,.1))


def exposure(tree,points,normals,triangle,rays,offset):
    normal=sum((normals[int(i)] for i in triangle),Vector()).normalized()
    if normal.length<.5 or normal.dot(rays[0])<=0:return 1.
    a,b,c=(points[int(i)] for i in triangle)
    clear=0
    for wa,wb,wc in SAMPLES:
        origin=a*wa+b*wb+c*wc+normal*offset
        clear+=sum(tree.ray_cast(origin+ray*offset,ray,300)[0] is None for ray in rays)
    return clear/(len(SAMPLES)*len(rays))


def bake(args):
    repo=Path(__file__).resolve().parents[2];source=args.source.resolve();out=args.out.resolve()
    if out.exists() or out==repo or repo in out.parents or out==source or source in out.parents:
        raise ValueError('Use a new private output folder outside Git.')
    if not 1<=args.samples<=16 or not 0<=args.disc<=6:
        raise ValueError('Use1-16sun rays and a0-6degree disc.')
    layout,review=inputs(args.layout.resolve(),args.review.resolve(),repo)
    if len(set(args.models))!=len(args.models) or set(args.models)-review['Models'].keys():
        raise ValueError('Use distinct known services.')
    package=Path(next(iter(review['Images'].values()))['Source']).parent.resolve()
    if package!=source:raise ValueError('Review and source package differ.')
    # The shared helper returns Blender X/Z/Y; this BVH uses Core Unity X/Y/Z.
    rays=[Vector((r.x,r.z,r.y)) for r in directions((-.35,.75,-.55),args.disc,args.samples)]
    tree_points=[];tree_faces=[]
    for key in args.models:
        model=review['Models'][key]
        for part in model['Primitives']:
            material=model['Materials'][part['Material']]
            if material['Light'] or material['Mask'] or material['Blend']:continue
            start=len(tree_points)
            tree_points.extend(Vector(p) for p in np.asarray(part['Positions']).reshape(-1,3))
            tree_faces.extend(tuple(int(i)+start for i in tri) for tri in np.asarray(part['Indices']).reshape(-1,3))
    tree=BVHTree.FromPolygons(tree_points,tree_faces,all_triangles=True,epsilon=0)
    shutil.copytree(source,out)
    results={}
    for key in args.models:
        path=out/(key+'.gltf');doc=json.loads(path.read_text())
        buffers=[bytearray((out/b['uri']).read_bytes()) for b in doc['buffers']]
        mesh_nodes={n['mesh']:n for n in doc['nodes'] if 'mesh' in n}
        assert len(mesh_nodes)==sum('mesh' in n for n in doc['nodes'])
        primitives=[(mesh_nodes[mi],p) for mi,m in enumerate(doc['meshes']) for p in m['primitives']]
        model=review['Models'][key]
        assert len(primitives)==len(model['Primitives'])
        changed=0;shadowed=0;groups=0;lights=0;records=[]
        for (node,prim),part in zip(primitives,model['Primitives']):
            assert node['name']==part['Name'] and prim['material']==part['Material']
            points=[Vector(p) for p in np.asarray(part['Positions']).reshape(-1,3)]
            normals=[Vector(n) for n in np.asarray(part['Normals']).reshape(-1,3)]
            indices=np.asarray(part['Indices']).reshape(-1,3)
            assert len(normals)==len(points) and np.isfinite(part['Normals']).all()
            previous=read_accessor(doc,buffers,prim['extras']['hamlet_contact_shade']).reshape(-1)
            assert len(previous)==len(indices)
            material=model['Materials'][part['Material']]
            preserve=material['Light'] or material['Mask'] or material['Blend'] or any(n in part['Name'].lower() for n in ('native lamp','candle','flame'))
            direct=np.ones(len(indices),dtype='<f4')
            if preserve:lights+=len(indices)
            else:
                for i,triangle in enumerate(indices):direct[i]=exposure(tree,points,normals,triangle,rays,.015)
            direct,count=planar_visibility(indices,points,direct);groups+=count
            shade=previous.copy() if preserve else np.minimum(previous,.25+.75*direct).astype('<f4')
            assert np.isfinite(shade).all() and (shade<=previous+1e-7).all() and shade.min()>=0
            changed+=int((shade<previous-1e-6).sum());shadowed+=int((direct<.999).sum())
            binary=buffers[0];binary.extend(b'\0'*((-len(binary))%4));start=len(binary);binary.extend(shade.tobytes())
            view=len(doc['bufferViews']);doc['bufferViews'].append({'buffer':0,'byteOffset':start,'byteLength':shade.nbytes})
            accessor=len(doc['accessors']);doc['accessors'].append({'bufferView':view,'componentType':5126,'type':'SCALAR','count':len(shade),'min':[float(shade.min())],'max':[float(shade.max())]})
            prim['extras']['hamlet_contact_shade']=accessor
            records.append({'name':part['Name'],'triangles':len(shade),'minimum_direct_visibility':float(direct.min()),
                            'mean_direct_visibility':float(direct.mean()),'preserved_light_or_alpha':preserve,
                            'contact_values_reduced':int((shade<previous-1e-6).sum())})
        doc['buffers'][0]['byteLength']=len(buffers[0]);(out/doc['buffers'][0]['uri']).write_bytes(buffers[0])
        path.write_text(json.dumps(doc,separators=(',',':')))
        results[key]={'triangles':model['TriangleCount'],'changed_contact_values':changed,'directly_occluded_triangles':shadowed,
                      'planar_groups':groups,'preserved_light_or_alpha_triangles':lights,'members':records}
    manifest=json.loads((out/'manifest.json').read_text())
    manifest['files']={name:hashlib.sha256((out/name).read_bytes()).hexdigest() for name in manifest['files']}
    (out/'manifest.json').write_text(json.dumps(manifest,indent=2))
    (out.parent/(out.name+'-facade.json')).write_text(json.dumps({'source':str(source),
      'direction_unity':[-.35,.75,-.55],'sun_disc_degrees':args.disc,'sun_rays':args.samples,
      'area_samples_per_triangle':len(SAMPLES),'offset_metres':.015,'self_shadow_only':len(args.models)==1,
      'caster_services':args.models,
      'geometry_UVs_materials_and_paint_changed':False,'original_buffer_prefix_retained':True,
      'new_runtime_path':False,'models':results},indent=2))
    print('PASS facade visibility baked',[(key,v['changed_contact_values']) for key,v in results.items()],flush=True)


def self_test():
    roof=[Vector(p) for p in ((-2,2,-2),(2,2,-2),(2,2,2),(-2,2,2))]
    tree=BVHTree.FromPolygons(roof,[(0,1,2),(0,2,3)],all_triangles=True)
    rays=[Vector((-.35,.75,-.55)).normalized()]
    triangle=(0,1,2);normal=[Vector((0,0,-1))]*3
    under=[Vector(p) for p in ((-.1,.9,0),(.1,.9,0),(0,1.1,0))]
    clear=[v+Vector((0,0,-4)) for v in under]
    assert exposure(tree,under,normal,triangle,rays,.015)==0
    assert exposure(tree,clear,normal,triangle,rays,.015)==1
    assert exposure(tree,under,[-n for n in normal],triangle,rays,.015)==1
    points=[Vector(p) for p in ((0,0,0),(1,0,0),(1,1,0),(0,1,0))]
    shade,count=planar_visibility(np.array([[0,1,2],[0,2,3]]),points,np.array([0.,1.]))
    assert count==1 and np.allclose(shade,[.5,.5])
    print('PASS roof occlusion, clear lit side, unlit-face preservation and coplanar diagonal prevention')


if __name__=='__main__':
    p=argparse.ArgumentParser(description=__doc__);p.add_argument('--self-test',action='store_true')
    for name in ('layout','review','source','out'):p.add_argument('--'+name,type=Path)
    p.add_argument('--models',nargs='+',default=['guild']);p.add_argument('--samples',type=int,default=4);p.add_argument('--disc',type=float,default=3)
    args=p.parse_args(sys.argv[sys.argv.index('--')+1:])
    if args.self_test:self_test()
    else:
        if any(getattr(args,n) is None for n in ('layout','review','source','out')):p.error('Pass matching layout/review/source and a new private output folder.')
        bake(args)
