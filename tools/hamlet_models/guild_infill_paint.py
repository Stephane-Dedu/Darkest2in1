"""Fit a reviewed uninterrupted lime painting to Guild infill, keeping architecture.

Blender-only. Native roofs, oak, standards, glass, stone courses and foundations
retain. The new private painting is generated from native DD2 material references,
not an extracted texture. Review through Core and the native town before reuse.
"""
import argparse,hashlib,json,sys
from pathlib import Path
import bpy,numpy as np
sys.path.insert(0,str(Path(__file__).resolve().parent))
import geometry as G
import workshops as W


def build(paint):
    assert len([o for o in bpy.data.objects if o.type=='MESH'])==647,'Use accepted330 raw source'
    image=bpy.data.images.load(str(paint.resolve()),check_existing=True);width,height=image.size
    assert width>=1500 and height>=750 and abs(width/height-2)<.01
    lime=bpy.data.materials['Hamlet smoke stained lime'];nodes=[n for n in lime.node_tree.nodes if n.type=='TEX_IMAGE']
    assert 1<=len(nodes)<=2 and all(n.image and tuple(n.image.size)==(1254,1254) for n in nodes)
    for node in nodes:node.image=image
    lime.name='Guild uninterrupted lime v88'
    targets=sorted([o for o in bpy.data.objects if o.type=='MESH' and (lime in list(o.data.materials) or 'gable infill' in o.name)],key=lambda o:o.name)
    assert len(targets)==12
    records=[]
    for index,ob in enumerate(targets):
        points=np.array([tuple(ob.matrix_world@v.co) for v in ob.data.vertices]);lo=points.min(0);span=np.maximum(np.ptp(points,axis=0),.0001)
        previous=[m.name for m in ob.data.materials]
        if lime not in list(ob.data.materials):
            ob.data.materials.clear();ob.data.materials.append(lime)
        uv=ob.data.uv_layers.active;fields=[]
        normal_matrix=ob.matrix_world.to_3x3().inverted().transposed()
        for face in ob.data.polygons:
            if ob.data.materials[face.material_index]!=lime:continue
            n=normal_matrix@face.normal
            if abs(n.z)>.85:axes=(0,1);periods=(7.4,7.4)
            else:axes=(0 if abs(n.y)>=abs(n.x) else 1,2);periods=(7.4,3.7)
            fractions=np.array([span[k]/period for k,period in zip(axes,periods)])
            assert (fractions<=.960001).all(),(ob.name,fractions)
            # Finite fields share physical paint frequency. Different face/member
            # offsets avoid the former repeated half-atlas crop and wrapped gables.
            seed=(index*7+int(np.argmax(np.abs(n)))+(3 if sum(n)<0 else 0))%13
            offset=.02+(.96-fractions)*np.array([seed/12,((seed*5)%13)/12])
            for li in face.loop_indices:
                p=ob.matrix_world@ob.data.vertices[ob.data.loops[li].vertex_index].co
                uv.data[li].uv=offset+np.array([(p[k]-lo[k])/period for k,period in zip(axes,periods)])
            fields.append({'face':face.index,'axes':axes,'fractions':fractions.tolist(),'offset':offset.tolist()})
        coords=np.array([tuple(uv.data[li].uv) for face in ob.data.polygons if ob.data.materials[face.material_index]==lime for li in face.loop_indices]);assert coords.min()>=.019999 and coords.max()<=.980001
        records.append({'name':ob.name,'previous_materials':previous,'new_material':lime.name,'fields':fields})
    (G.OUT/'guild-infill-paint.json').write_text(json.dumps({'source_texture':str(paint.resolve()),
      'source_sha256':hashlib.sha256(paint.read_bytes()).hexdigest(),'decoded_size':[width,height],
      'members':records,'material_factors_preserved':True,'geometry_changed':False,
      'new_generated_paint':True,'post_generation_raster_edits':False,'horizontal_vertical_source_period':[7.4,3.7],
      'UVs_stay_inside_finite_field':True,'native_assets_changed':False},indent=2),encoding='utf-8')


if __name__=='__main__':
    p=argparse.ArgumentParser(description=__doc__)
    for name in ('source','out','kit','paint'):p.add_argument('--'+name,type=Path,required=True)
    p.add_argument('--resolution',type=int,default=900);p.add_argument('--samples',type=int,default=12)
    args=p.parse_args(sys.argv[sys.argv.index('--')+1:])
    if args.out.exists():raise ValueError('Keep previous studies; use a fresh private output.')
    W.run(args,refiners={'guild':lambda:build(args.paint)},detail='v88',
          design='Continuous detailed lime infill with finite metric fields; native oak/roof/standards, accepted330 construction, stone base and nine services retained')
