"""Repaint the shared authored masonry in a private Hamlet package, preserving geometry and native atlases."""
import argparse
import hashlib
import json
from pathlib import Path
import shutil
from PIL import Image


def repaint(source, out, masonry):
    source, out, masonry = source.resolve(), out.resolve(), masonry.resolve()
    repo = Path(__file__).resolve().parents[2]
    if any(p == repo or repo in p.parents for p in [source, out, masonry]):
        raise ValueError('Keep all model/paint inputs and output outside Git.')
    if out.exists():
        raise ValueError('Choose a new output folder.')
    manifest = json.loads((source/'manifest.json').read_text())
    sha = lambda p: hashlib.sha256(p.read_bytes()).hexdigest()
    for name, expected in manifest['files'].items():
        if Path(name).name != name or sha(source/name) != expected:
            raise ValueError('Invalid source package: '+name)
    with Image.open(masonry) as image:
        size = image.size
        image.verify()
    if min(size) < 1024 or max(size) > 8192:
        raise ValueError('Use an authored 1024–8192px masonry painting.')
    docs = {p.name: json.loads(p.read_text()) for p in source.glob('*.gltf')}
    old = set()
    for doc in docs.values():
        for material in doc.get('materials', []):
            if material.get('name') == 'DD2 carved masonry':
                index = material['pbrMetallicRoughness']['baseColorTexture']['index']
                old.add(doc['images'][doc['textures'][index]['source']]['uri'])
    if len(old) != 1 or not old.issubset(manifest['files']):
        raise ValueError('Expected one shared masonry painting, separate from native atlases.')
    shutil.copytree(source, out)
    name = 'paint-'+sha(masonry)[:24]+'.png'
    shutil.copy2(masonry, out/name)
    changed = []
    for filename, doc in docs.items():
        replaced = False
        for image in doc.get('images', []):
            if image['uri'] in old:
                image['uri'] = name
                replaced = True
        if replaced:
            (out/filename).write_text(json.dumps(doc, separators=(',', ':')))
            changed.append(filename)
    for previous in old:
        (out/previous).unlink()
        del manifest['files'][previous]
    manifest['files'][name] = sha(out/name)
    for filename in changed:
        manifest['files'][filename] = sha(out/filename)
    (out/'manifest.json').write_text(json.dumps(manifest, indent=2))
    for filename, expected in manifest['files'].items():
        assert sha(out/filename) == expected, filename
    for filename, expected in json.loads((source/'manifest.json').read_text())['files'].items():
        if filename not in old and filename not in changed:
            assert sha(out/filename) == expected, filename
    record = {'source': str(source), 'masonry': str(masonry), 'size': size, 'replacement': name,
              'replaced': sorted(old), 'changed_models': changed,
              'notice': 'New AI-painted masonry; native models/atlases remain private. Geometry and UVs unchanged.'}
    (out.parent/'repaint-verification.json').write_text(json.dumps(record, indent=2))
    print(json.dumps(record, indent=2))


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    for flag in ['source', 'out', 'masonry']:
        parser.add_argument('--'+flag, type=Path, required=True)
    args = parser.parse_args()
    repaint(args.source, args.out, args.masonry)
