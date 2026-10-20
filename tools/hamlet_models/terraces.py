"""Refine service paving and retaining stone from reviewed native DD2 panels.

Blender entry point. Supply a private verified ten-model source and an output textures
folder with cobbles_native.png, retaining_native.png, foundation_panel_06.png and the
reviewed earth_v23.png painting. The earth is generated art retained from round271.
Native components retain their original geometry and UVs. Authored vertices stay in place.
"""
import argparse
import sys
from pathlib import Path

import bpy

sys.path.insert(0,str(Path(__file__).resolve().parent))
import workshops as W

WIDTHS={'abbey':25,'guild':16,'sanitarium':18,'tavern':20,'blacksmith':17,'graveyard':10}
SURFACES={
    'abbey':('Abbey raised terrace','Entry stair'),
    'guild':('Guild terrace',),
    'sanitarium':('Sanitarium terrace','Entry stair'),
    'tavern':('Stone foundation','Entry stair'),
    'blacksmith':('Stone foundation',),
    'graveyard':('Stone foundation','Entry stair'),
}
BLOCKS=('Graveyard retaining wall','Graveyard gatepost','Gatepost pointed cap')
STONES=((150,250,214,285),(218,247,302,291),(351,246,404,289),(330,155,391,182))


def refine(key):
    objects=[o for o in bpy.data.objects if o.type=='MESH']
    points=[o.matrix_world@v.co for o in objects for v in o.data.vertices]
    span=max(max(v[i] for v in points)-min(v[i] for v in points) for i in (0,1))
    scale=WIDTHS[key]/span
    paving=W.G.material('Service worn native paving v24',(.70,.74,.71,1),'cobbles_native',.4)
    wall=W.G.material('Service native retaining courses v24',(.92,.99,.96,1),'retaining_native',.4)
    single=W.G.material('Service native individual cut stone v24',(.92,.99,.96,1),'foundation_panel_06',.4)
    changed=[]
    for ob in objects:
        if ' / ' in ob.name:continue
        discrete=key=='graveyard' and ob.name.startswith(BLOCKS)
        terrace=ob.name.startswith(SURFACES[key])
        solid=key=='abbey' and ob.name.startswith('Abbey terrace parapet')
        solid |= key=='tavern' and ob.name.startswith(('Tapered masonry chimney','Chimney cap'))
        if not (discrete or terrace or solid):continue
        changed.append(ob.name)
        before=[v.co.copy() for v in ob.data.vertices]
        if discrete:
            # One physical block gets one painted stone, not a miniature brick wall.
            rect=STONES[sum(map(ord,ob.name))%len(STONES)]
            W.paint(ob,single,(rect[0]/512,1-rect[3]/512,rect[2]/512,1-rect[1]/512))
        else:
            ob.data.materials.clear()
            ob.data.materials.append(wall);ob.data.materials.append(paving)
            uv=ob.data.uv_layers.active
            for face in ob.data.polygons:
                normal=(ob.matrix_world.to_3x3()@face.normal).normalized()
                top=terrace and normal.z>.65
                face.material_index=1 if top else 0
                for li in face.loop_indices:
                    co=ob.matrix_world@ob.data.vertices[ob.data.loops[li].vertex_index].co
                    if top:
                        # Runtime fits each service independently. Keep the same stone
                        # size after that fit instead of repeating once per source box.
                        uv.data[li].uv=(co.x*scale/6.2,co.y*scale/6.2)
                    else:
                        along=co.x if abs(normal.y)>=abs(normal.x) else co.y
                        uv.data[li].uv=(along*scale/3.8,co.z*scale/1.8)
        assert all(a==b.co for a,b in zip(before,ob.data.vertices))
    assert changed, 'No authored terrace matched '+key
    if key=='graveyard':
        soil=W.G.material('Graveyard worked earth v24',(.56,.59,.49,1),'earth_v23',.4)
        earth=bpy.data.objects.get('Burial earth')
        assert earth is not None
        earth.data.materials.clear();earth.data.materials.append(soil)
        for face in earth.data.polygons:
            face.material_index=0
            for li in face.loop_indices:
                co=earth.matrix_world@earth.data.vertices[earth.data.loops[li].vertex_index].co
                earth.data.uv_layers.active.data[li].uv=(co.x*scale/5,co.y*scale/5)
    print('TERRACE',key,'runtime scale',round(scale,4),'authored objects',len(changed),flush=True)


if __name__=='__main__':
    parser=argparse.ArgumentParser(description=__doc__)
    for name in ['source','out','kit']:parser.add_argument('--'+name,type=Path,required=True)
    parser.add_argument('--resolution',type=int,default=900)
    parser.add_argument('--samples',type=int,default=12)
    args=parser.parse_args(sys.argv[sys.argv.index('--')+1:])
    W.run(args,refiners={k:lambda k=k:refine(k) for k in WIDTHS},detail='v24',
          design='v24: native paving at consistent town scale, retaining courses and individual painted stones')
