"""Extract unmodified DD2 material panels for the private architectural studies."""
import argparse
import hashlib
import json
import shutil
from pathlib import Path
from PIL import Image


def prepare(source, kit, out):
    source, kit, out = (p.resolve() for p in (source, kit, out))
    repo = Path(__file__).resolve().parents[2]
    if out == source or out == repo or repo in out.parents:
        raise ValueError('Choose a new private output outside Git.')
    if out.exists():
        raise ValueError('Output already exists; preserve earlier studies.')
    out.mkdir(parents=True)
    shutil.copytree(source/'textures', out/'textures')
    records = json.loads((source/'texture-sources.json').read_text())
    atlas = kit/'exp/_tex/tex--7451768689147877690-1024x1024.png'
    for name, rect in {
        'beam_native': (641, 669, 710, 683),
        'lime_native': (810, 850, 886, 906),
        'cutstone_native': (837, 866, 877, 886),
    }.items():
        with Image.open(atlas) as image:
            image.crop(rect).save(out/'textures'/f'{name}.png')
        records[name] = {'source': str(atlas), 'crop': list(rect), 'kind': 'Native DD2 crop, original pixels',
                         'source_sha256': hashlib.sha256(atlas.read_bytes()).hexdigest()}
    path=source/'road_rough_patch.png'
    with Image.open(path) as image:
        rect=tuple(round(t*s) for t,s in zip([.18,.18,.82,.82],[image.width,image.height]*2))
        image.crop(rect).save(out/'textures/paving_native.png')
    records['paving_native']={'source':str(path),'crop':list(rect),'kind':'Native road centre, original pixels',
                              'source_sha256':hashlib.sha256(path.read_bytes()).hexdigest()}
    for name in ['slate_v18', 'red_v18', 'ashlar_v18']:
        file = out/'textures'/f'{name}.png'
        with Image.open(file) as im: size = list(im.size)
        records[name] = {'source': str(source/'textures'/file.name), 'kind': 'Generated painting, built-in ImageGen',
                         'prompt_file': str(source/'prompts.md'), 'size': size,
                         'sha256': hashlib.sha256(file.read_bytes()).hexdigest()}
    (out/'texture-sources.json').write_text(json.dumps(records, indent=2))
    print('Extracted native beam, lime and dressed-stone panels without resampling.')


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    for name in ['source', 'kit', 'out']: parser.add_argument('--'+name, type=Path, required=True)
    args = parser.parse_args()
    prepare(args.source, args.kit, args.out)
