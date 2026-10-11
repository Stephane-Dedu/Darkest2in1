"""Inspect private DD1 exteriors and DD2 parts without copying game art into Git."""
import argparse
import json
from pathlib import Path
from PIL import Image, ImageDraw, ImageFont

BUILDINGS = {
    'abbey': 'Abbey', 'tavern': 'Tavern', 'sanitarium': 'Sanitarium',
    'guild': 'Guild', 'blacksmith': 'Blacksmith', 'camping_trainer': 'Survivalist',
    'stage_coach': 'Stage Coach', 'nomad_wagon': 'Nomad Wagon',
    'graveyard': 'Graveyard', 'statue': "Ancestor's Memoirs",
}


def references(kit, out):
    kit, out = Path(kit).resolve(), Path(out).resolve()
    repo = Path(__file__).resolve().parents[2]
    if out == repo or repo in out.parents:
        raise ValueError('Private game art must stay outside the repository.')
    out.mkdir(parents=True, exist_ok=True)
    font_path = kit.parent / 'dd2-native-kit/fonts/AlegreyaSans-Regular.ttf'
    font = ImageFont.truetype(str(font_path), 26)
    records = {}
    for state in ['level01', 'level02', 'level03', 'locked']:
        sheet = Image.new('RGB', (2000, 1000), '#14171c')
        draw = ImageDraw.Draw(sheet)
        for i, (key, label) in enumerate(BUILDINGS.items()):
            file = kit / 'assembled' / f'town_{key}_{state}.png'
            if key == 'graveyard':
                file = kit / 'assembled/town_graveyard.png'
            if not file.exists() and state == 'locked' and key in ('stage_coach', 'statue'):
                file = kit / 'assembled' / f'town_{key}_level01.png'
            image = Image.open(file).convert('RGBA')
            records.setdefault(key, {})[state] = {'source': str(file), 'size': list(image.size)}
            image = image.crop(image.getbbox())
            image.thumbnail((370, 430))
            x, y = i % 5 * 400, i // 5 * 500
            sheet.paste(image, (x + (400 - image.width) // 2, y + 445 - image.height), image)
            draw.text((x + 20, y + 463), label, fill='#d7ccb4', font=font)
        sheet.save(out / f'dd1-{state}-references.png')
    (out / 'dd1-sources.json').write_text(json.dumps(records, indent=2), encoding='utf-8')


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--kit', type=Path, required=True)
    parser.add_argument('--out', type=Path, required=True)
    args = parser.parse_args()
    references(args.kit, args.out)
