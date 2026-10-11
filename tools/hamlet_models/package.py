"""Unpack the verified private GLBs into the runtime's external glTF format, sharing identical PNGs."""
import argparse
import hashlib
import io
import json
import struct
from pathlib import Path
from PIL import Image


def package(source, out):
    source, out = source.resolve(), out.resolve()
    repo = Path(__file__).resolve().parents[2]
    if out == repo or repo in out.parents:
        raise ValueError('Private game-derived models must stay outside Git.')
    checks = json.loads((source/'verified.json').read_text())
    models = json.loads((source/'models.json').read_text())['models']
    if len(models) != 10 or set(checks) != set(models):
        raise ValueError('Package the complete verified model set.')
    out.mkdir(parents=True, exist_ok=True)
    manifest = {'version': 1, 'models': {}, 'files': {}}

    def write(name, data):
        (out/name).write_bytes(data)
        manifest['files'][name] = hashlib.sha256(data).hexdigest()
        return name

    def png(data):
        with Image.open(io.BytesIO(data)) as image:
            image.verify()
        return write('paint-'+hashlib.sha256(data).hexdigest()[:24]+'.png', data)

    for key, record in models.items():
        data = (source/record['glb']).read_bytes()
        if hashlib.sha256(data).hexdigest() != checks[key]['sha256']:
            raise ValueError('Re-verify changed model: '+key)
        magic, version, length = struct.unpack_from('<4sII', data)
        size, kind = struct.unpack_from('<I4s', data, 12)
        if magic != b'glTF' or version != 2 or length != len(data) or kind != b'JSON':
            raise ValueError('Invalid GLB: '+key)
        doc = json.loads(data[20:20+size])
        bin_size, bin_kind = struct.unpack_from('<I4s', data, 20+size)
        binary = data[28+size:28+size+bin_size]
        if bin_kind != b'BIN\0' or len(doc['buffers']) != 1 or len(binary) != bin_size:
            raise ValueError('Expected one embedded binary buffer: '+key)
        doc['buffers'][0]['uri'] = write(key+'.bin', binary)
        images = []
        for entry in doc.get('images', []):
            view = doc['bufferViews'][entry['bufferView']]
            start = view.get('byteOffset', 0)
            payload = binary[start:start+view['byteLength']]
            if entry.get('mimeType') != 'image/png':
                raise ValueError('Expected painted PNG: '+key)
            images.append(payload)
            entry.pop('bufferView')
            entry['uri'] = png(payload)
        # glTF emission exports may be RGB while alpha lives in the base-colour image. Give the
        # environment shader one RGBA paint map without changing emission RGB or the original UVs.
        for material in doc.get('materials', []):
            base = material.get('pbrMetallicRoughness', {}).get('baseColorTexture')
            emission = material.get('emissiveTexture')
            if not base or not emission or material.get('alphaMode', 'OPAQUE') == 'OPAQUE':
                continue
            base_index = doc['textures'][base['index']]['source']
            emission_record = doc['textures'][emission['index']]
            paint = Image.open(io.BytesIO(images[emission_record['source']])).convert('RGBA')
            alpha = Image.open(io.BytesIO(images[base_index])).convert('RGBA')
            if paint.size != alpha.size:
                raise ValueError('Paint/alpha size mismatch: '+key+'/'+material['name'])
            paint.putalpha(alpha.getchannel('A'))
            encoded = io.BytesIO(); paint.save(encoded, format='PNG')
            doc['images'].append({'uri': png(encoded.getvalue()), 'mimeType': 'image/png'})
            doc['textures'].append({**emission_record, 'source': len(doc['images'])-1})
            emission['index'] = len(doc['textures'])-1
        doc['asset']['extras'] = {'source_glb_sha256': checks[key]['sha256'], 'private': True}
        write(key+'.gltf', json.dumps(doc, separators=(',', ':')).encode('utf-8'))
        manifest['models'][key] = {'file': key+'.gltf', 'triangles': record['triangles'],
                                  'source_sha256': checks[key]['sha256']}
    (out/'manifest.json').write_text(json.dumps(manifest, indent=2), encoding='utf-8')
    print('Packaged', len(models), 'models;', len(manifest['files']), 'files;',
          round(sum((out/name).stat().st_size for name in manifest['files'])/1e6, 1), 'MB')


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--source', required=True, type=Path)
    parser.add_argument('--out', required=True, type=Path)
    args = parser.parse_args()
    package(args.source, args.out)
