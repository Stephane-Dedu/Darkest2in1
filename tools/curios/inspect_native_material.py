"""Inspect a native material against the properties of its resolved shader.

Unity materials retain obsolete saved values. A saved _Cutoff does not prove
that the current shader uses it. This read-only tool separates shader-declared
properties from other saved properties before a native-art adaptation.
Reports and native sources must stay outside the repository. Never reads saves.
"""
import argparse
import hashlib
import json
from pathlib import Path

import UnityPy


def inspect(game, bundles, material_name, bundle_index=None):
    aa = game / 'Darkest Dungeon II_Data/StreamingAssets/aa'
    env = UnityPy.Environment(path='')
    loaded = []

    def load(name):
        if Path(name).name != name or not name.endswith('.bundle'):
            raise ValueError('Use explicit native bundle filenames')
        if name not in loaded:
            env.load_file(str(aa / name))
            loaded.append(name)

    for name in bundles:
        load(name)
    matches = [obj for obj in env.objects
               if obj.type.name == 'Material' and obj.read().m_Name == material_name]
    if len(matches) != 1:
        raise ValueError('Material must resolve uniquely in the supplied bundles')
    reader = matches[0]
    tree = reader.read_typetree()
    shader_ptr = reader.read().m_Shader
    dependency = None
    try:
        shader = shader_ptr.read_typetree()
    except FileNotFoundError:
        if not bundle_index or not shader_ptr.m_FileID:
            raise ValueError('Supply a private UnityFS bundle-directory index to resolve the shader')
        external = shader_ptr.assetsfile.externals[shader_ptr.m_FileID - 1].path
        cab = external.rsplit('/', 1)[-1].lower()
        records = json.loads(bundle_index.read_text())['bundles']
        candidates = [item['bundle'] for item in records
                      if any(entry['path'].lower() == cab for entry in item['entries'])]
        if len(candidates) != 1:
            raise ValueError('Shader dependency must resolve to exactly one indexed bundle')
        load(candidates[0])
        dependency = {'cab': cab, 'bundle': candidates[0]}
        shader = shader_ptr.read_typetree()
    form = shader['m_ParsedForm']
    declared = {prop['m_Name']: prop for prop in form['m_PropInfo']['m_Props']}
    saved = tree['m_SavedProperties']
    floats = dict(saved['m_Floats'])
    colors = dict(saved['m_Colors'])
    values = {**dict(saved.get('m_TexEnvs', [])), **dict(saved.get('m_Ints', [])),
              **floats, **colors}
    active = {}
    for name, prop in declared.items():
        if name not in values:
            continue
        active[name] = {'description': prop['m_Description'], 'value': values[name],
                        'attributes': prop['m_Attributes']}
    stale = {name: value for name, value in values.items() if name not in declared}
    keywords = tree.get('m_ValidKeywords', tree.get('m_ShaderKeywords', []))
    cutoffs = {name: item for name, item in active.items()
               if 'cutoff' in item['description'].lower() or 'alphaclip' in name.lower()}
    return {
        'material': material_name, 'shader': form['m_Name'], 'shader_dependency': dependency,
        'valid_keywords': keywords, 'declared_property_count': len(declared),
        'declared_saved_properties': active, 'saved_properties_not_declared': stale,
        'declared_cutoff_properties': cutoffs,
        'source_material_path_id': reader.path_id, 'source_shader_path_id': shader_ptr.m_PathID,
        'bundles': {name: hashlib.sha256((aa / name).read_bytes()).hexdigest() for name in loaded},
        'notice': 'Values outside the shader property list may be stale; absence from that list alone does not prove absence from compiled uniforms. Shader-declared properties and keywords are source evidence, not a complete reconstruction of lighting, wind, dithering or compiled branches. Inspect the relevant path and compare natively before acceptance.',
        'recipe_sha256': hashlib.sha256(Path(__file__).read_bytes()).hexdigest(),
        'read_only': True, 'native_game_QA': False,
    }


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--game', type=Path, required=True)
    parser.add_argument('--bundle', action='append', required=True)
    parser.add_argument('--material', required=True)
    parser.add_argument('--bundle-index', type=Path)
    parser.add_argument('--out', type=Path, required=True)
    args = parser.parse_args()
    repo = Path(__file__).resolve().parents[2]
    if args.out.resolve().is_relative_to(repo) or args.out.exists():
        parser.error('Use a new private report path outside the repository')
    report = inspect(args.game, args.bundle, args.material, args.bundle_index)
    args.out.parent.mkdir(parents=True, exist_ok=True)
    args.out.write_text(json.dumps(report, indent=2))
    print(json.dumps({'material': report['material'], 'shader': report['shader'],
                      'declared_cutoff_properties': report['declared_cutoff_properties'],
                      'report': str(args.out)}))


if __name__ == '__main__':
    main()
