"""Fit complete native DD2 inn timbers into the Guild's two large gables.

Blender-only. Rigid rotations, translations and uniform scales preserve each
native member's proportions, original UVs and atlas. Existing Guild is retained.
"""
import argparse,hashlib,json,sys
from pathlib import Path
import bpy,numpy as np
sys.path.insert(0,str(Path(__file__).resolve().parent))
import geometry as G,workshops as W


def build(kit,components):
    assert len([o for o in bpy.data.objects if o.type=='MESH'])==647,'Use accepted331 raw source'
    sys.path.insert(0,str(kit/'tools'));import bl_lib as L
    asset='inn_lvl_03_prefab';folder=kit/'exp'/asset
    doc=json.loads((folder/'scene.json').read_text(encoding='utf-8'))
    parts=json.loads(components.read_text(encoding='utf-8'))[asset]['parts'];records=[]
    def member(side,role,index,a,b):
        part=parts[index];src=next(o for o in doc['objects'] if o['name']==part['object'])
        path=folder/('mesh-'+src['mesh']+'.npz');data=np.load(path)
        points=(np.c_[data['v'],np.ones(len(data['v']))]@np.array(src['world']).T)[:,:3]
        indices=part['triangle_indices'];faces=data['tri'].reshape(-1,3)[indices]
        used,remap=np.unique(faces,return_inverse=True);points=points[used]
        lo,hi=points.min(0),points.max(0);centre=(lo+hi)/2
        a,b=np.array(a,float),np.array(b,float);length=float(np.linalg.norm(b-a));scale=length/(hi[0]-lo[0])
        x=(b-a)/length*side;z=np.array([-side,0.,0.]);y=np.cross(z,x);rotation=np.array([x,y,z]).T
        assert np.allclose(rotation.T@rotation,np.eye(3)) and abs(np.linalg.det(rotation)-1)<1e-8
        placed=(points-centre)@rotation.T*scale+(a+b)/2
        slots=np.repeat(np.arange(len(data['sub'])),data['sub'])[indices]
        mats=[L.make_mat(folder,key,doc['materials'][key],shade=1) for key in src['materials']]
        ob=G.mesh(f'Guild native gable {side:+d} {role} / inn03-{index}',placed.tolist(),remap.reshape(-1,3).tolist(),mats[0],data['uv'][faces].tolist())
        for mat in mats[1:]:ob.data.materials.append(mat)
        for face,slot in zip(ob.data.polygons,slots):face.material_index=int(slot)
        G.exportable_native_materials([ob]);ob['source_asset']=asset
        records.append({'name':ob.name,'source_object':src['name'],'source_component':index,'triangle_indices':indices,
         'source_mesh':str(path),'source_mesh_sha256':hashlib.sha256(path.read_bytes()).hexdigest(),
         'source_centre_unity':centre.tolist(),'destination_centre_unity':((a+b)/2).tolist(),
         'rotation_unity':rotation.tolist(),'uniform_scale':scale,'triangles':len(faces),'original_UVs_and_materials':True})
    # The existing eave rail remains the bottom tie. A central post and collar
    # divide the broad plaster field; two short braces meet the collar corners.
    for side,wall in ((-1,-3.72),(1,2.42)):
        depth=wall+side*.065
        member(side,'kingpost',81,(depth,6.70,.3),(depth,9.80,.3))
        member(side,'collar',83,(depth,8.15,-.83),(depth,8.15,1.43))
        member(side,'front brace',83,(depth,6.81,.3),(depth,8.12,-.80))
        member(side,'rear brace',83,(depth,6.81,.3),(depth,8.12,1.40))
    assert len(records)==8
    (G.OUT/'guild-gable-frame.json').write_text(json.dumps({'members':records,'original647_members_untouched':True,
      'native_source':'DD2 inn_lvl_03_prefab / the_inn_lvl_03_left','original_texture_pixels':True,
      'new_generated_art':False,'rigid_uniform_only':True,'existing_bottom_ties_and_roof_verges_retained':True},indent=2),encoding='utf-8')


if __name__=='__main__':
    p=argparse.ArgumentParser(description=__doc__)
    for name in ('source','out','kit','components'):p.add_argument('--'+name,type=Path,required=True)
    p.add_argument('--resolution',type=int,default=900);p.add_argument('--samples',type=int,default=12)
    args=p.parse_args(sys.argv[sys.argv.index('--')+1:])
    if args.out.exists():raise ValueError('Keep previous studies; use a fresh private output.')
    W.run(args,refiners={'guild':lambda:build(args.kit,args.components)},detail='v89',
      design='Two Guild gables framed with complete native DD2 inn kingposts, collars and braces; original647 members and all paint retained')
