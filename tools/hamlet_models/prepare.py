"""Prepare private painted surface samples and DD1 architectural details for the 3D studies."""
import argparse
import json
from pathlib import Path
from PIL import Image, ImageOps
from references import references


def prepare(kit, dd1, out):
    kit, dd1, out = map(lambda p: Path(p).resolve(), (kit, dd1, out))
    references(dd1, out)
    textures = out / 'textures'
    textures.mkdir(exist_ok=True)
    sources = {}
    swatches = {
        'stone': ('_tex/tex--7451768689147877690-1024x1024.png', (817, 890, 935, 978), 0),
        'plaster': ('_tex/tex--7451768689147877690-1024x1024.png', (797, 620, 835, 670), 0),
        'timber': ('_tex/tex--7451768689147877690-1024x1024.png', (83, 660, 103, 708), 0),
        'roof_red': ('_tex/tex--7451768689147877690-1024x1024.png', (595, 58, 640, 182), 90),
        'roof_slate': ('valley_academic_house/tex--566446899398678041.png', (1420, 288, 1650, 382), 0),
        'thatch': ('_tex/tex--4376918372443305789-1024x512.png', (70, 266, 398, 331), 0),
    }
    for name, (file, rect, angle) in swatches.items():
        source = kit / 'exp' / file
        image = Image.open(source).convert('RGBA').crop(rect)
        if angle:
            image = image.rotate(angle, expand=True)
        if name=='roof_red':
            # Exclude the dormer's painted timber frame from the repeating shingle sample.
            image=image.crop((0,0,74,45))
        image.putalpha(255)
        image.save(textures / f'{name}.png')
        sources[name] = {'source': str(source), 'crop': list(rect), 'rotation': angle, 'kind': 'DD2 painted surface'}
        if name=='roof_red':
            sources[name]['after_rotation_crop']=[0,0,74,45]
    canvas=Image.open(textures/'thatch.png').convert('L')
    ImageOps.colorize(canvas,'#171717','#bdbdbd').convert('RGBA').save(textures/'canvas.png')
    sources['canvas']={**sources['thatch'],'kind':'DD2 painted surface, desaturated for cloth tint'}
    details = {
        'guild_banner': ('guild', (.18, .415, .30, .665)),
        'forge_sign': ('blacksmith', (.065, .43, .235, .56)),
        'nomad_eye': ('nomad_wagon', (.395, .05, .625, .21)),
        'abbey_rose': ('abbey', (.23, .38, .315, .46)),
    }
    for name, (building, rect) in details.items():
        source = dd1 / 'fx' / f'town_{building}_level03' / 'idle.png'
        image = Image.open(source).convert('RGBA')
        crop = tuple(round(v * (image.width if i % 2 == 0 else image.height)) for i, v in enumerate(rect))
        image.crop(crop).save(textures / f'{name}.png')
        sources[name] = {'source': str(source), 'crop': list(crop), 'kind': 'DD1 service detail'}
    source=dd1/'fx/town_abbey_level03/idle.png'
    image=Image.open(source).convert('RGBA')
    rect=tuple(round(v*(image.width if i%2==0 else image.height)) for i,v in enumerate((.45,.54,.535,.645)))
    ImageOps.grayscale(image.crop(rect)).convert('RGBA').save(textures/'window_grain.png')
    sources['window_grain']={'source':str(source),'crop':list(rect),'kind':'DD1 painted window, desaturated for glass tint'}
    source=dd1/'fx/town_statue_level03/idle.png'
    image=Image.open(source).convert('RGBA')
    rect=tuple(round(v*(image.width if i%2==0 else image.height)) for i,v in enumerate((.55,.34,.65,.50)))
    image=image.crop(rect).convert('RGB')
    ImageOps.grayscale(image).convert('RGBA').save(textures/'statue_stone.png')
    sources['statue_stone']={'source':str(source),'crop':list(rect),'kind':'DD1 statue carved-stone weathering'}
    (out / 'texture-sources.json').write_text(json.dumps(sources, indent=2), encoding='utf-8')
    print(f'Prepared {len(sources)} private painted textures in {textures}')


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--kit', type=Path, required=True)
    parser.add_argument('--dd1', type=Path, required=True)
    parser.add_argument('--out', type=Path, required=True)
    args = parser.parse_args()
    prepare(args.kit, args.dd1, args.out)
