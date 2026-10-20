"""Prepare native DD2 terrace paint and the previously reviewed private earth painting."""
import argparse
import hashlib
import json
import shutil
from pathlib import Path

import UnityPy
from PIL import Image


def prepare(game,out,earth):
    out=out.resolve();repo=Path(__file__).resolve().parents[2]
    if out==repo or repo in out.parents:raise ValueError('Keep private paint outside Git.')
    if (out/'verified.json').exists():raise ValueError('Preserve the verified study.')
    with Image.open(earth) as image:image.verify()
    textures=out/'textures';textures.mkdir(parents=True,exist_ok=True)
    bundles=game/'Darkest Dungeon II_Data/StreamingAssets/aa'
    names=['biome_common_assets_all.bundle','biome_city_assets_all.bundle']
    env=UnityPy.Environment(path="")
    for name in names:env.load_file(str(bundles/name))
    required={'cobblestones_base':'cobbles_native','foundation_panel_06':'foundation_panel_06'}
    records={}
    for obj in env.objects:
        if obj.type.name!='Material':continue
        material=obj.read();name=required.get(material.m_Name)
        if name is None:continue
        for prop,tex in material.m_SavedProperties.m_TexEnvs:
            if prop not in ['_BaseMap_texture','_BaseMap','_MainTex'] or not tex.m_Texture.m_PathID:continue
            image=tex.m_Texture.read().image;path=textures/(name+'.png');image.save(path)
            records[name]={'material':material.m_Name,'size':list(image.size),'bundles':names,
                           'texture_pathid':tex.m_Texture.m_PathID,'kind':'Unmodified native DD2 pixels'}
            break
    assert set(required.values())==set(records),'Required native panels unavailable'
    rect=(5,88,507,293)
    with Image.open(textures/'foundation_panel_06.png') as image:
        image.crop(rect).save(textures/'retaining_native.png')
    records['retaining_native']={'source':'foundation_panel_06.png','crop':rect,
                                 'kind':'Native intact courses; cap and loose rubble excluded'}
    shutil.copy2(earth,textures/'earth_v23.png')
    records['earth_v23']={'source':str(earth.resolve()),'kind':'Reviewed generated earth from round271; not native'}
    for name,record in records.items():
        record['sha256']=hashlib.sha256((textures/(name+'.png')).read_bytes()).hexdigest()
    (out/'texture-sources.json').write_text(json.dumps(records,indent=2))
    print('Prepared native terrace paint and retained reviewed earth')


if __name__=='__main__':
    parser=argparse.ArgumentParser(description=__doc__)
    for name in ['game','out','earth']:parser.add_argument('--'+name,type=Path,required=True)
    args=parser.parse_args()
    prepare(args.game,args.out,args.earth)
