"""Fit native DD2 Assistance canvas and hoops to the Hamlet's Nomad caravan.

Private Blender authoring. Native upper canopy faces retain their UVs, original
atlas and uniform proportions. A restrained plum material tint keeps the DD1
colour cue. Existing living body, doorway, eye crest, awning and other services
stay intact. Authored end panels follow the new canopy profile.
"""
import argparse
import hashlib
import json
import sys
from pathlib import Path

import bpy
import numpy as np

sys.path.insert(0,str(Path(__file__).resolve().parent))
import geometry as G
import workshops as W
from abbey_pinnacles import islands

REPLACED=('Curved purple caravan roof','Caravan curved end wall','Roof iron rib',
          'Caravan thin roof binding')


def hull(points):
    points=sorted(set(tuple(p) for p in points))
    def cross(a,b,c):return (b[0]-a[0])*(c[1]-a[1])-(b[1]-a[1])*(c[0]-a[0])
    def half(seq):
        out=[]
        for p in seq:
            while len(out)>1 and cross(out[-2],out[-1],p)<=0:out.pop()
            out.append(p)
        return out
    return half(points)[:-1]+half(points[::-1])[:-1]


def native_paint(name,image,color):
    material=G.material(name,color,emission=.45)
    tree=material.node_tree
    shader=next(n for n in tree.nodes if n.type=='BSDF_PRINCIPLED')
    tex=tree.nodes.new('ShaderNodeTexImage');tex.image=image
    mix=G.multiply_node(tree,color)
    tree.links.new(tex.outputs['Color'],mix.inputs[6])
    tree.links.new(mix.outputs[2],shader.inputs['Base Color'])
    tree.links.new(mix.outputs[2],shader.inputs['Emission Color'])
    return material


def build(folder):
    doc=json.loads((folder/'scene.json').read_text(encoding='utf-8'))
    src=next(o for o in doc['objects'] if o['name']=='story_assist_wagon_main')
    assert len(src['materials'])==1 and 'first' not in src
    data=np.load(folder/('mesh-'+src['mesh']+'.npz'))
    points=data['v'];tri=data['tri'].reshape(-1,3)
    parts=[]
    for indices in islands(points,tri):
        p=points[np.unique(tri[indices])];lo,hi=p.min(0),p.max(0)
        # The complete cloth shell and its four curved wooden hoops are distinct
        # source islands. Keep only original upper faces; hanging side curtains
        # would cover the existing caravan windows and door.
        if len(indices) in (216,182,114,188,102) and hi[1]>2.65 and hi[0]<1.2 and lo[0]>-1.3 and lo[1]>.45:
            ids=np.asarray(indices);keep=points[tri[ids]][:,:,1].min(1)>2.1
            if keep.any():parts.append((lo,hi,ids[keep],len(indices)))
    canvas=next(p for p in parts if p[3]==216)
    hoops=[p for p in parts if p[3] in (182,114,188,102)]
    assert len(parts)==5 and len(hoops)==4
    selected=tri[canvas[2]];p=points[np.unique(selected)]
    lo,hi=p.min(0),p.max(0)
    original=np.array([tuple(o.matrix_world@v.co) for o in bpy.data.objects if o.type=='MESH' for v in o.data.vertices])
    all_points=points[np.unique(tri[np.concatenate([q[2] for q in parts])])]
    length_lo,length_hi=all_points[:,0].min(),all_points[:,0].max()
    scale=float(2*original[:,1].max())/(length_hi-length_lo)
    centre=np.array([(length_lo+length_hi)/2,lo[1],(lo[2]+hi[2])/2])
    rotation=np.array([[0,0,-1],[0,1,0],[1,0,0]])
    anchor=np.array([0,3.6,0])
    placed=lambda p:((p-centre)*scale)@rotation.T+anchor
    ends=[o for o in bpy.data.objects if o.type=='MESH' and o.name.startswith('Caravan curved end wall')]
    assert len(ends)==2 and len({m for o in ends for m in o.data.materials})==1
    wood=ends[0].data.materials[0]
    removed=[o.name for o in bpy.data.objects if o.type=='MESH' and o.name.startswith(REPLACED)]
    assert len(removed)>=25
    W.remove(*REPLACED)
    texture=folder/doc['materials'][src['materials'][0]]['texture']
    image=bpy.data.images.load(str(texture),check_existing=True)
    assert tuple(image.size)==(2048,2048)
    cloth=native_paint('Nomad native plum canvas v57',image,(.17,.09,.26,1))
    oak=native_paint('Nomad native oak canopy hoops v57',image,(.45,.38,.28,1))
    awning=bpy.data.objects['Sagging market awning']
    awning.data.materials.clear();awning.data.materials.append(cloth)
    for f in awning.data.polygons:f.material_index=0
    # A clean, complete native canvas section, with seams and patches, replaces
    # the old scratch-grain sheet on the retained sagging shop awning.
    for v in awning.data.uv_layers.active.data:
        u,w=v.uv;v.uv=(.55+u*.24,.795+w*.125)
    records=[];made=[];supports=[]
    for index,part in enumerate([canvas]+sorted(hoops,key=lambda p:p[0][0])):
        ids=part[2];faces=tri[ids];used,remap=np.unique(faces.reshape(-1),return_inverse=True)
        ob=G.mesh('Nomad native canopy '+('cloth' if index==0 else 'hoop '+str(index)),
                  placed(points[used]).tolist(),remap.reshape(-1,3).tolist(),
                  cloth if index==0 else oak,data['uv'][faces].tolist())
        made.append(ob)
        records.append({'name':ob.name,'native_triangles':ids.tolist(),'uniform_scale':float(scale),
                        'rotation_unity':rotation.tolist(),'centre_source':centre.tolist(),
                        'anchor_unity':anchor.tolist(),'source_island_triangles':part[3]})
        if index:
            native_points=placed(points[used])
            for side in (-1,1):
                side_points=native_points[native_points[:,0]*side>0]
                foot=side_points[side_points[:,1].argmin()].copy()
                # Bury the brace inside the end hoop's existing thickness so
                # its bevel cannot expand the service's rear bound.
                foot[2]=np.clip(foot[2],-3.31,3.31)
                seat=np.array([side*1.98,3.55,np.clip(foot[2],-3.1,3.1)])
                brace=G.beam('Nomad canopy seat '+str(index)+' '+str(side),foot,seat,.10,oak)
                W.paint(brace,oak,(.045,.50,.255,.63),True)
                made.append(brace);supports.append({'name':brace.name,'a':foot.tolist(),'b':seat.tolist()})
    G.exportable_native_materials(made)
    # Close both living-body ends against the actual curved native silhouette.
    # These are authored wood panels, so no native geometry or UV is stretched.
    end_records=[]
    for sign in (-1,1):
        x=lo[0] if sign==-1 else hi[0]
        strip=p[np.abs(p[:,0]-x)<.09]
        q=placed(strip);xy=q[:,:2]
        xy=np.vstack([xy,[xy[:,0].min(),3.58],[xy[:,0].max(),3.58]])
        profile=hull(xy.tolist());z=float(q[:,2].mean())-sign*.01
        ob=G.mesh('Nomad fitted canopy end panel '+str(sign),[(a,b,z) for a,b in profile],
                  [list(range(len(profile)))],oak)
        W.paint(ob,oak,(.045,.50,.255,.63),True)
        end_records.append({'name':ob.name,'profile_xy':profile,'z':z})
        made.append(ob)
    front=end_records[0]
    def top_at(x):
        profile=front['profile_xy'];heights=[]
        for a,b in zip(profile,profile[1:]+profile[:1]):
            if min(a[0],b[0])-1e-6<=x<=max(a[0],b[0])+1e-6 and abs(b[0]-a[0])>1e-6:
                heights.append(a[1]+(b[1]-a[1])*(x-a[0])/(b[0]-a[0]))
        return max(heights)
    posts=[o for o in bpy.data.objects if o.type=='MESH' and o.name.startswith('Eye sign support')]
    assert len(posts)==2
    for index,post in enumerate(posts):
        pp=np.array([tuple(post.matrix_world@v.co) for v in post.data.vertices])
        a=np.array([(pp[:,0].min()+pp[:,0].max())/2,pp[:,2].min(),(pp[:,1].min()+pp[:,1].max())/2])
        b=np.array([a[0],top_at(a[0])-.12,front['z']+.03])
        brace=G.beam('Nomad eye crest bracket '+str(index+1),a,b,.10,oak)
        W.paint(brace,oak,(.045,.50,.255,.63),True)
        made.append(brace);supports.append({'name':brace.name,'a':a.tolist(),'b':b.tolist()})
    bpy.context.view_layer.update()
    new=np.array([tuple(o.matrix_world@v.co) for o in made for v in o.data.vertices])
    assert np.all(new.min(0)>=original.min(0)-1e-5) and np.all(new.max(0)<=original.max(0)+1e-5),('Retain service envelope',new.min(0),new.max(0),original.min(0),original.max(0))
    (G.OUT/'native-canopy.json').write_text(json.dumps({'native_parts':records,'end_panels':end_records,'supports':supports,
       'removed_meshes':removed,'source_files':{str(p.resolve()):hashlib.sha256(p.read_bytes()).hexdigest()
       for p in (folder/'scene.json',folder/('mesh-'+src['mesh']+'.npz'),texture)},
       'canvas_material_tint':[.17,.09,.26,1],'oak_tint':[.45,.38,.28,1],
       'awning_UV_rect':[.55,.795,.79,.92],
       'end_panel_UV_rect':[.045,.50,.255,.63],
       'raster_edits':False,'source_UVs_retained':True,'new_generated_art':False},indent=2),encoding='utf-8')


if __name__=='__main__':
    parser=argparse.ArgumentParser(description=__doc__)
    for name in ('source','out','kit'):parser.add_argument('--'+name,type=Path,required=True)
    parser.add_argument('--resolution',type=int,default=1000)
    parser.add_argument('--samples',type=int,default=12)
    args=parser.parse_args(sys.argv[sys.argv.index('--')+1:])
    W.run(args,refiners={'nomad_wagon':lambda:build(args.kit.resolve()/'exp/common_story_assist_prefab')},
          detail='v57',design='Native DD2 folded canvas and oak hoops; original paint/UVs/uniform proportions, restrained plum tint')
