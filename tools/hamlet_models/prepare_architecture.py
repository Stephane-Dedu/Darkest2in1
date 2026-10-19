"""Extract unmodified DD2 material panels for the private architectural studies."""
import argparse
import hashlib
import json
import shutil
from pathlib import Path
from PIL import Image, ImageOps


def prepare(source, kit, out, paintings):
    source, kit, out = (p.resolve() for p in (source, kit, out))
    repo = Path(__file__).resolve().parents[2]
    if out == source or out == repo or repo in out.parents:
        raise ValueError('Choose a new private output outside Git.')
    if out.exists():
        raise ValueError('Output already exists; preserve earlier studies.')
    paintings=paintings.resolve()
    names=['abbey_wall','guild_wall','sanitarium_wall','guild_banner','forge_sign','nomad_eye','slate_v20','red_v20','wood_v20']
    for name in names:
        path=paintings/f'{name}.png'
        with Image.open(path) as image:
            image.verify()
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
    if not (out/'textures/paving_native.png').exists():
        raise ValueError('Source must include the reviewed native courtyard paving.')
    for name in names:
        file=paintings/f'{name}.png';shutil.copy2(file,out/'textures'/file.name)
        with Image.open(file) as im:size=list(im.size)
        records[name]={'source':str(file),'kind':'Reviewed DD2-reference painting; source prompts remain alongside private paintings',
                       'size':size,'sha256':hashlib.sha256(file.read_bytes()).hexdigest()}
    atlas=kit/'exp/valley_academic_house/tex--566446899398678041.png';rect=(410,600,473,655)
    with Image.open(atlas) as image:
        ImageOps.grayscale(image.crop(rect)).convert('RGBA').save(out/'textures/window_grain.png')
    records['window_grain']={'source':str(atlas),'crop':list(rect),'kind':'Native DD2 glass; grayscale for material tint',
                             'source_sha256':hashlib.sha256(atlas.read_bytes()).hexdigest()}
    for name in ['abbey_rose','statue_stone']:
        (out/'textures'/f'{name}.png').unlink(missing_ok=True);records.pop(name,None)
    (out/'texture-sources.json').write_text(json.dumps(records, indent=2))
    print('Prepared independent service surfaces, redesigned emblems and native glass; DD1 rose/statue inputs removed.')


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    for name in ['source', 'kit', 'out', 'paintings']: parser.add_argument('--'+name, type=Path, required=True)
    args = parser.parse_args()
    prepare(args.source, args.kit, args.out, args.paintings)
