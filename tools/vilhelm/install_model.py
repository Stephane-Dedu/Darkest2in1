"""Install only the private Vilhelm pack into a stopped DD2, with a complete pre-change backup."""
import argparse
import hashlib
import json
import shutil
import subprocess
import time
import zipfile
from pathlib import Path

parser = argparse.ArgumentParser(description=__doc__)
parser.add_argument('--model', type=Path, required=True)
parser.add_argument('--portrait', type=Path, required=True)
parser.add_argument('--flame', type=Path, required=True)
parser.add_argument('--game', type=Path, required=True)
parser.add_argument('--backup', type=Path, required=True)
args = parser.parse_args()
if subprocess.check_output(['powershell', '-NoProfile', '-Command',
        "Get-CimInstance Win32_Process -Filter \"Name='Darkest Dungeon II.exe'\" | Select-Object -ExpandProperty ProcessId"], text=True).strip():
    raise RuntimeError('Close DD2 before installing')
game = args.game.resolve()
if not (game / 'Darkest Dungeon II.exe').is_file():
    raise ValueError('Not a DD2 installation')
source = args.model.resolve()
document = json.loads((source / 'sir_vilhelm.gltf').read_text())
names = ['sir_vilhelm.gltf'] + [b['uri'] for b in document['buffers']] + [i['uri'] for i in document['images']]
files = {}
for name in names:
    path = (source / name).resolve()
    if source not in path.parents or not path.is_file():
        raise ValueError('Invalid or missing model dependency: ' + name)
    files[name] = path
if (source/'motion.json').is_file(): files['motion.json']=source/'motion.json'
files['portrait.png'] = args.portrait.resolve()
files['vfx/blackflame.png'] = args.flame.resolve()
if not all(p.is_file() for p in files.values()):
    raise ValueError('Missing portrait or native flame atlas')
dest = (game / 'PrivateHeroModels/sir_vilhelm').resolve()
if game not in dest.parents:
    raise ValueError('Installation escapes the game directory')
backup = args.backup.resolve()
backup.mkdir(parents=True, exist_ok=True)
snapshot = backup / time.strftime('vilhelm-before-%Y%m%d-%H%M%S.zip')
with zipfile.ZipFile(snapshot, 'w', zipfile.ZIP_DEFLATED) as archive:
    for path in dest.rglob('*'):
        if path.is_file():
            archive.write(path, path.relative_to(dest).as_posix())
record = {}
for name, path in files.items():
    target = dest / name
    target.parent.mkdir(parents=True, exist_ok=True)
    shutil.copy2(path, target)
    digest = lambda p: hashlib.sha256(p.read_bytes()).hexdigest()
    if digest(path) != digest(target):
        raise IOError('Installed hash mismatch: ' + name)
    record[name] = digest(target)
(backup / 'installed-model.json').write_text(json.dumps(record, indent=2))
print(f'Installed {len(files)} private files; backup {snapshot}', flush=True)
