"""Prepare private dossier thumbnails from the owner's native DD2 biome UI art.

No generation, repaint, stretch, game launch, installation or save access.
Outputs stay outside the repository and the game installation. Compare the
360x120 UI fit before accepting any of these narrow crops of portrait artwork.
"""
import argparse
import hashlib
import json
from pathlib import Path

from PIL import Image
import UnityPy


# The native portrait supplies a finite painted band. Preserve its original
# pixels; the UI downsamples657x219 into360x120 without further aspect cropping.
REGIONS = {
    'city': ('dd2_city', 80),
    'farm': ('dd2_farm', 340),
    'forest': ('dd2_forest', 260),
    'coast': ('dd2_coast', 180),
    'cave': ('dd2_cave', 340),
}


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def prepare(game, output):
    game, output = game.resolve(), output.resolve()
    repo = Path(__file__).resolve().parents[2]
    if output.is_relative_to(repo) or output.is_relative_to(game):
        raise ValueError('Extracted art must stay outside the repository and game installation')
    if output.exists() and any(output.iterdir()):
        raise ValueError('Use a new output folder to preserve earlier source evidence')
    bundles = game / 'Darkest Dungeon II_Data/StreamingAssets/aa'
    missing = [name for name in REGIONS if not (bundles / f'biome_{name}_ui_assets_all.bundle').is_file()]
    if missing:
        raise ValueError('Missing native UI bundles: ' + ', '.join(missing))
    candidate, references = output / 'candidate', output / 'native-reference'
    candidate.mkdir(parents=True, exist_ok=True)
    references.mkdir(parents=True, exist_ok=True)
    record = {'status': 'Offline candidate; not installed or natively verified.',
              'ai_generated_art': False, 'resized_or_repainted': False,
              'source_size': [657, 1148], 'output_size': [657, 219], 'ui_size': [360, 120],
              'source_orientation': 'Upright native Sprite.image; top-left crop coordinates.',
              'recipe_sha256': sha(Path(__file__)), 'regions': {}}
    for region, (zone, top) in REGIONS.items():
        bundle = bundles / f'biome_{region}_ui_assets_all.bundle'
        # One small UI bundle per region; never scan/decompress a model bundle.
        env = UnityPy.Environment(path='')
        env.load_file(str(bundle))
        found = []
        for obj in env.objects:
            if obj.type.name != 'Sprite':
                continue
            sprite = obj.read()
            if sprite.m_Name == 'biome_bg_' + region:
                found.append((obj.path_id, sprite))
        if len(found) != 1:
            raise ValueError(f'{region}: expected exactly one native biome background sprite')
        path_id, sprite = found[0]
        image = sprite.image.convert('RGBA')
        if image.size != (657, 1148) or image.getchannel('A').getextrema() != (255, 255):
            raise ValueError(f'{region}: native dimensions/opacity changed; inspect before cropping')
        source = references / f'biome_bg_{region}.png'
        image.save(source)
        rect = (0, top, 657, top + 219)
        cropped = image.crop(rect).convert('RGB')
        target = candidate / f'preview-{zone}.png'
        cropped.save(target)
        if Image.open(target).size != (657, 219) or target.stat().st_size > 16 * 1024 * 1024:
            raise ValueError(f'{region}: invalid runtime preview image')
        record['regions'][zone] = {
            'bundle': bundle.name, 'bundle_sha256': sha(bundle),
            'sprite_name': sprite.m_Name, 'sprite_path_id': path_id,
            'source_png_sha256': sha(source), 'crop': list(rect),
            'output_png_sha256': sha(target), 'output_bytes': target.stat().st_size,
            'source_pixel_sha256': hashlib.sha256(image.tobytes()).hexdigest(),
            'cropped_rgb_pixel_sha256': hashlib.sha256(cropped.tobytes()).hexdigest(),
        }
    record['limits'] = [
        'These are narrow detail crops of native portrait paintings, not complete landscape illustrations.',
        'The installed selector retains its existing art until separate native UI review.',
        'The Mountain and legacy DD1 dossier images are unchanged.',
    ]
    (output / 'provenance.json').write_text(json.dumps(record, indent=2), encoding='utf-8')
    print(json.dumps(record, indent=2))


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--game', type=Path, required=True)
    parser.add_argument('--out', type=Path, required=True)
    args = parser.parse_args()
    prepare(args.game, args.out)
