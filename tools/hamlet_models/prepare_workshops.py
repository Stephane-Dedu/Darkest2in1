"""Prepare private, reviewed Hamlet workshop texture inputs without resampling paint."""
import argparse
import hashlib
import json
import shutil
from pathlib import Path
from PIL import Image


def prepare(source, paintings, out, study='workshops'):
    source, paintings, out = [p.resolve() for p in (source, paintings, out)]
    repo = Path(__file__).resolve().parents[2]
    if out.exists() or out == source or out == repo or repo in out.parents:
        raise ValueError('Choose a new private output outside Git.')
    names = {'workshops':['iron_v21', 'cloth_v21', 'brick_v21'],
             'smithy':['thatch_v22', 'plaster_v22', 'hearth_v22']}[study]
    for name in names:
        with Image.open(paintings/(name+'.png')) as image:
            if min(image.size) < 1024: raise ValueError('Expected reviewed full-resolution painting: '+name)
            image.verify()
    out.mkdir(parents=True)
    shutil.copytree(source/'textures', out/'textures')
    records = json.loads((source/'texture-sources.json').read_text())
    for name in names:
        path=paintings/(name+'.png');shutil.copy2(path,out/'textures'/path.name)
        with Image.open(path) as image:size=list(image.size)
        records[name]={'source':str(path),'size':size,'sha256':hashlib.sha256(path.read_bytes()).hexdigest(),
                       'kind':'Reviewed generated DD2-reference painting; original prompts beside private paintings'}
    (out/'texture-sources.json').write_text(json.dumps(records,indent=2))
    print('Prepared three reviewed',study,'paintings and retained source textures.')


if __name__=='__main__':
    parser=argparse.ArgumentParser(description=__doc__)
    for key in ['source','paintings','out']:parser.add_argument('--'+key,type=Path,required=True)
    parser.add_argument('--study',choices=['workshops','smithy'],default='workshops')
    args=parser.parse_args();prepare(args.source,args.paintings,args.out,args.study)
