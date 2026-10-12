"""Render the catalog's verified native source parts with the existing private kit's Blender helpers."""
import argparse
import json
import sys
from pathlib import Path

import numpy as np

argv = sys.argv[sys.argv.index("--") + 1:]
parser = argparse.ArgumentParser(description=__doc__)
parser.add_argument("--kit", type=Path, required=True)
parser.add_argument("--out", type=Path, required=True)
parser.add_argument("--refresh", nargs="*", default=[], help="Re-render named source keys even when their PNG exists.")
args = parser.parse_args(argv)
sys.path.insert(0, str(args.kit / "tools"))
import bl_lib as L

renders = args.out / "renders"
renders.mkdir(exist_ok=True)
evidence = json.loads((args.out / "audit.json").read_text())["parts"]
for key, part in evidence.items():
    dest = renders / (key + ".png")
    if dest.exists() and key not in args.refresh:
        continue
    L.reset()
    objects, lo, hi = L.build(args.out / "parts" / key)
    if not objects:
        raise ValueError("No renderable native source for " + key)
    center = (lo + hi) / 2
    radius = float(np.linalg.norm(hi - lo)) / 2
    L.camera((center[0] + radius * .38, center[1] - radius * 3.25, center[2] + radius * .72), tuple(center), fov=38, res=(640, 640))
    L.render(dest, samples=4, transparent=True)
    print("CURIO_PART", key, len(objects), flush=True)
