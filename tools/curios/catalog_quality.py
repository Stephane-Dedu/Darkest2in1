"""Audit private curio art coverage without mistaking proposals for installed art."""
import argparse
from collections import Counter
import hashlib
import html
import json
from pathlib import Path

from PIL import Image


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def catalog(inventory, selection, output):
    inventory, selection, output = inventory.resolve(), selection.resolve(), output.resolve()
    if output.is_relative_to(Path(__file__).resolve().parents[2]):
        raise ValueError('Private catalog must stay outside the repository')
    if output.exists() and any(output.iterdir()):
        raise ValueError('Use a new report folder')
    entries = json.loads(inventory.read_text(encoding='utf-8-sig'))
    if len({e['id'] for e in entries}) != len(entries):
        raise ValueError('Duplicate inventory identity')
    known = {e['id']: e for e in entries}
    selected = json.loads(selection.read_text(encoding='utf-8-sig'))
    pairs = {}
    for relative in selected['pairs']:
        folder = (selection.parent / relative).resolve()
        metadata = json.loads((folder / 'provenance.json').read_text())
        key = metadata['id']
        if key not in known or key in pairs or known[key]['scope'] == 'owner-exception':
            raise ValueError('Unknown, duplicate or protected candidate identity')
        width = metadata['proposed_runtime_width']
        size = metadata['size']
        if not 100 <= width <= 400 or width * size[1] / size[0] > 430.001:
            raise ValueError('Candidate exceeds native display bounds')
        if metadata['padding'] != 16 or metadata['foot_anchor'] != [size[0] / 2, size[1] - 16]:
            raise ValueError('Candidate does not match the crawl foot contract')
        images = {}
        for state in ('closed', 'used'):
            path = folder / (key + '-' + state + '.png')
            with Image.open(path) as image:
                if image.mode != 'RGBA' or list(image.size) != size or image.getchannel('A').getextrema()[0] != 0:
                    raise ValueError('Missing consistent RGBA state pair')
            if sha(path) != metadata['states'][state]['output_sha256']:
                raise ValueError('Candidate PNG changed after fitting')
            if any(abs(a - b) > .51 for a, b in zip(metadata['foot_anchor'], metadata['states'][state]['registered_foot'])):
                raise ValueError('Candidate floor registration drifted')
            images[state] = {'file': str(path), 'sha256': sha(path)}
        pairs[key] = {'art_id': key, 'pair_folder': str(folder), 'width': width, 'size': size, 'states': images,
                      'state_note': metadata.get('state_note'), 'native_verified': False, 'installed': False}
    aliases = selected.get('aliases', {})
    for key, canonical in aliases.items():
        if key not in known or key in pairs or canonical not in pairs or known[key]['scope'] == 'owner-exception':
            raise ValueError('Invalid shared-art alias')
        if known[key].get('sprite', key) != known[canonical].get('sprite', canonical):
            raise ValueError('Shared art must retain the audited original sprite identity')
    native = {}
    studies = selected.get('native_studies') or [selected['native_study']]
    for relative in studies:
        native_path = (selection.parent / relative).resolve()
        native_doc = json.loads(native_path.read_text())
        for entry in native_doc['entries']:
            entry = dict(entry, file=str(native_path.parent / entry['file']))
            if entry['id'] in native:
                raise ValueError('Duplicate native source identity')
            native[entry['id']] = entry
    if set(native) - known.keys():
        raise ValueError('Unknown native source identity')
    for entry in native.values():
        if sha(Path(entry['file'])) != entry['output_sha256']:
            raise ValueError('Native render changed after source audit')
        for record in entry['files']:
            if sha(Path(record['path'])) != record['sha256']:
                raise ValueError('Native source dependency changed')
    records = []
    for key, source in known.items():
        status = ('owner_exception_retained' if source['scope'] == 'owner-exception' else
                  'offline_pair_candidate' if key in pairs or key in aliases else
                  'native_body_needs_used_state' if key in native else 'pending')
        record = {'id': key, 'name': source['name'], 'scope': source['scope'], 'region': source['region'], 'status': status}
        if key in pairs or key in aliases:
            record.update(pairs[aliases.get(key, key)])
            if key in aliases:
                record['shared_original_sprite_alias'] = True
        elif key in native:
            record['native_closed_file'] = native[key]['file']
        records.append(record)
    document = {'status': 'Offline candidates only. No installation, native QA or gameplay change.',
                'inventory_sha256': sha(inventory), 'selection_sha256': sha(selection),
                'recipe_sha256': sha(Path(__file__)), 'counts': dict(Counter(r['status'] for r in records)),
                'scopes': dict(Counter(r['scope'] for r in records)), 'entries': records}
    output.mkdir(parents=True)
    (output / 'coverage.json').write_text(json.dumps(document, indent=2), encoding='utf-8')
    cards = []
    for record in sorted(records, key=lambda r: (r['status'] != 'offline_pair_candidate', r['status'], r['id'])):
        images = ''
        if 'states' in record:
            for state, image in record['states'].items():
                images += '<figure><img loading="lazy" src="' + Path(image['file']).as_uri() + '"><figcaption>' + state + '</figcaption></figure>'
        elif 'native_closed_file' in record:
            images = '<figure><img loading="lazy" src="' + Path(record['native_closed_file']).as_uri() + '"><figcaption>native closed body only</figcaption></figure>'
        cards.append('<article><h2>' + html.escape(record['name']) + '</h2><p>' + html.escape(record['id'] + ' / ' + record['region']) +
                     '</p><p>' + html.escape(record['status']) + '</p><div>' + images + '</div></article>')
    page = '<!doctype html><meta charset="utf-8"><title>Curio art coverage</title><style>body{background:#161719;color:#e0d8ca;font:16px system-ui;margin:32px}main{display:grid;grid-template-columns:repeat(auto-fit,minmax(400px,1fr));gap:20px}article{background:#242529;padding:18px;border:1px solid #50483b}h2{font-size:20px}article div{display:flex;align-items:end;gap:10px}figure{margin:0;flex:1;min-width:0}img{width:100%;height:260px;object-fit:contain}figcaption{color:#aaa}</style><h1>Curio art coverage</h1><p>' + html.escape(document['status']) + '</p><p>' + html.escape(str(document['counts'])) + '</p><main>' + ''.join(cards) + '</main>'
    (output / 'index.html').write_text(page, encoding='utf-8')
    print(json.dumps(document['counts']))
    return document


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--inventory', type=Path, required=True)
    parser.add_argument('--selection', type=Path, required=True)
    parser.add_argument('--out', type=Path, required=True)
    args = parser.parse_args()
    catalog(args.inventory, args.selection, args.out)
