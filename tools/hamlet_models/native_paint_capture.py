"""Prepare a private native-paint capture fixture or convert its exact RGBA outputs.

This changes only a frozen private source snapshot. Build/deploy/run/restore that
fixture with the normal owned QA process; never ship its binaries. No game assets
or runtime paint belong in Git. The actual game supplies all captured pixels.
"""
import argparse
import hashlib
import json
from pathlib import Path


def private(path):
    path = path.resolve()
    repo = Path(__file__).resolve().parents[2]
    if path == repo or repo in path.parents:
        raise ValueError('Use a private directory outside Git.')
    return path


def prepare(snapshot, out):
    snapshot, out = private(snapshot), private(out)
    if out.exists():
        raise ValueError('Use a new capture directory; preserve earlier evidence.')
    source = snapshot / 'src/DarkestDungeon3/Dd2/HamletStage.GroundPaint.cs'
    text = source.read_text(encoding='utf-8')
    anchor = '        _groundPaint ??= _groundPaintTask.GetAwaiter().GetResult();'
    if text.count(anchor) != 1 or 'native-paint-capture' in text:
        raise ValueError('Unexpected source or fixture already prepared.')
    # Forward slashes are valid in Windows .NET paths and avoid C# escape mistakes.
    target = json.dumps(out.as_posix())
    injection = '''
        // Private native-paint-capture fixture, never deploy as the normal plugin.
        if (_groundUpload == 0 && System.Environment.GetCommandLineArgs().Contains("-dd3-hamlet3d-preview"))
        {
            string target = __TARGET__;
            if (!System.IO.File.Exists(System.IO.Path.Combine(target, "capture.json")))
            {
                System.IO.File.WriteAllBytes(System.IO.Path.Combine(target, "ground.rgba"), _groundPaint.Ground);
                System.IO.File.WriteAllBytes(System.IO.Path.Combine(target, "lanes.rgba"), _groundPaint.Lanes);
                System.IO.File.WriteAllBytes(System.IO.Path.Combine(target, "valley.rgba"), _groundPaint.Valley);
                System.IO.File.WriteAllText(System.IO.Path.Combine(target, "capture.json"),
                    "{\\\"version\\\":1,\\\"size\\\":" + _groundPaint.Size + ",\\\"encoding\\\":\\\"RGBA8_sRGB\\\",\\\"origin\\\":\\\"bottom-left\\\"}");
            }
        }
'''.replace('__TARGET__', target)
    out.mkdir(parents=True)
    candidate = text.replace(anchor, anchor + injection)
    record = {'source': str(source), 'before': hashlib.sha256(text.encode()).hexdigest(),
              'after': hashlib.sha256(candidate.encode()).hexdigest(), 'private_fixture_only': True,
              'paint_sources': {p.relative_to(snapshot).as_posix(): hashlib.sha256(p.read_text(encoding='utf-8').encode()).hexdigest()
                                for p in sorted((snapshot/'src/DarkestDungeon3.Core/Presentation').glob('Hamlet*.cs'))}}
    source.write_text(candidate, encoding='utf-8', newline='\n')
    (out / 'fixture.json').write_text(json.dumps(record, indent=2), encoding='utf-8')
    print('Prepared private capture fixture. Restore the normal plugin after QA:', out)


def convert(folder):
    from PIL import Image
    folder = private(folder)
    record = json.loads((folder / 'capture.json').read_text())
    if record != {'version': 1, 'size': 4096, 'encoding': 'RGBA8_sRGB', 'origin': 'bottom-left'}:
        raise ValueError('Unexpected native paint contract.')
    if (folder / 'manifest.json').exists():
        raise ValueError('This capture is already converted; preserve its evidence.')
    files = {}
    for name in ('ground', 'lanes', 'valley'):
        raw = (folder / (name + '.rgba')).read_bytes()
        if len(raw) != record['size'] ** 2 * 4:
            raise ValueError('Incomplete captured paint: ' + name)
        path = folder / (name + '.png')
        image = Image.frombytes('RGBA', (record['size'], record['size']), raw)
        image.transpose(Image.Transpose.FLIP_TOP_BOTTOM).save(path)
        # Format conversion only: undo the row origin and compare all native bytes.
        decoded = Image.open(path).transpose(Image.Transpose.FLIP_TOP_BOTTOM).tobytes()
        assert decoded == raw, name
        files[path.name] = {'sha256': hashlib.sha256(path.read_bytes()).hexdigest(),
                            'rgba_sha256': hashlib.sha256(raw).hexdigest()}
    record['files'] = files
    record['paint_sources'] = json.loads((folder / 'fixture.json').read_text())['paint_sources']
    record['notice'] = 'Exact native ground paint only; offline model lighting remains approximate.'
    (folder / 'manifest.json').write_text(json.dumps(record, indent=2), encoding='utf-8')
    print('Converted all three native maps; decoded pixels and row orientation exact.')


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    sub = parser.add_subparsers(dest='command', required=True)
    prep = sub.add_parser('prepare')
    prep.add_argument('--snapshot', type=Path, required=True)
    prep.add_argument('--out', type=Path, required=True)
    conv = sub.add_parser('convert')
    conv.add_argument('folder', type=Path)
    args = parser.parse_args()
    if args.command == 'prepare': prepare(args.snapshot, args.out)
    else: convert(args.folder)
