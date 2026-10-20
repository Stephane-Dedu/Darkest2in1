"""Extract DD2's unmodified worn cobblestone paint for the private Hamlet ground pack."""
import argparse
import hashlib
import json
from pathlib import Path
import UnityPy

def prepare(game,out):
    out=out.resolve();repo=Path(__file__).resolve().parents[2]
    if out==repo or repo in out.parents:raise ValueError('Keep native paint outside Git.')
    out.mkdir(parents=True,exist_ok=True)
    bundles=game/'Darkest Dungeon II_Data/StreamingAssets/aa'
    names=['biome_common_assets_all.bundle','biome_city_assets_all.bundle']
    env=UnityPy.load(*[str(bundles/n) for n in names])
    for obj in env.objects:
        if obj.type.name!='Material':continue
        material=obj.read()
        if material.m_Name!='cobblestones_base':continue
        for prop,tex in material.m_SavedProperties.m_TexEnvs:
            if prop not in ['_BaseMap','_BaseMap_texture','_MainTex'] or not tex.m_Texture.m_PathID:continue
            im=tex.m_Texture.read().image;path=out/'hamlet-cobbles-v23.png';im.save(path)
            record={'material':material.m_Name,'size':list(im.size),'property':prop,
                    'texture_pathid':tex.m_Texture.m_PathID,'bundles':names,
                    'sha256':hashlib.sha256(path.read_bytes()).hexdigest(),'kind':'Unmodified native DD2 pixels'}
            (out/'ground-provenance.json').write_text(json.dumps(record,indent=2))
            print('Extracted native cobblestones:',path);return
    raise ValueError('Native cobblestone paint unavailable.')

if __name__=='__main__':
    p=argparse.ArgumentParser(description=__doc__)
    p.add_argument('--game',type=Path,required=True);p.add_argument('--out',type=Path,required=True)
    a=p.parse_args();prepare(a.game,a.out)
