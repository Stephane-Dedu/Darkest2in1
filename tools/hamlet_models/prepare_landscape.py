"""Export installed DD2 surface textures for a private, reproducible offline town preview."""
import argparse
import json
from pathlib import Path

import UnityPy


def main(game,out):
    repo=Path(__file__).resolve().parents[2]
    out=out.resolve()
    if out==repo or repo in out.parents: raise ValueError('Game textures must stay outside Git.')
    out.mkdir(parents=True,exist_ok=True)
    bundles=game/'Darkest Dungeon II_Data/StreamingAssets/aa'
    names=['biome_common_assets_all.bundle','biome_valley_common_assets_all.bundle']
    env=UnityPy.load(*[str(bundles/name) for name in names])
    wanted={'road_rough_patch','farm_ground_hill_transition','bricks','foundation_panel_06'}
    found={}
    for obj in env.objects:
        if obj.type.name!='Material': continue
        material=obj.read()
        if material.m_Name not in wanted or material.m_Name in found: continue
        for name,tex in material.m_SavedProperties.m_TexEnvs:
            if name not in ['_BaseMap_texture','_BaseMap','_MainTex'] or not tex.m_Texture.m_PathID: continue
            image=tex.m_Texture.read().image
            image.save(out/(material.m_Name+'.png'))
            found[material.m_Name]={'property':name,'texture_pathid':tex.m_Texture.m_PathID,'size':list(image.size)}
            break
    if set(found)!=wanted: raise ValueError('Missing native surface materials: '+str(wanted-set(found)))
    (out/'ground-source.json').write_text(json.dumps({'bundles':names,'textures':found},indent=2))
    print('Exported four native painted surfaces to',out)


if __name__=='__main__':
    parser=argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--game',type=Path,required=True)
    parser.add_argument('--out',type=Path,required=True)
    args=parser.parse_args(); main(args.game,args.out)
