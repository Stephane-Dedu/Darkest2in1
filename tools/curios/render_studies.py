"""Render preliminary 3D assemblies from audited native props. No finished bespoke models are implied."""
import argparse
import json
import sys
from pathlib import Path

import numpy as np

argv = sys.argv[sys.argv.index("--") + 1:]
parser = argparse.ArgumentParser(description=__doc__)
parser.add_argument("--kit", type=Path, required=True)
parser.add_argument("--out", type=Path, required=True)
args = parser.parse_args(argv)
sys.path.insert(0, str(args.kit / "tools"))
import bl_lib as L

# key, target width, Unity X/Z position, Y height, yaw. Grounding and scaling use actual mesh bounds.
STUDIES = {
    "heirloom_chest": [("camp_chest", 2.2, 0, 0, 0, 8), ("candles", .75, -1.35, .25, 0, 0)],
    "pile_of_bones": [("bones", 2.4, .1, 0, 0, -10), ("skulls", 1.35, -.65, -.2, .08, 15)],
    "stack_of_books": [("books", 1.2, -.7, 0, 0, -10), ("books", 1, .5, .1, 0, 20),
                       ("books", .8, 0, -.55, 0, 40)],
    "dinner_cart": [("meat_cart", 2.5, 0, 0, 0, 0), ("meat", .7, 1.3, -.25, 0, -20)],
    "holy_fountain": [("fountain", 3, 0, 0, 0, 0), ("candles", .7, -1.4, -.4, 0, 0)],
    "travellers_tent": [("tent", 3.1, 0, .6, 0, 0), ("sack", .7, 1.35, -.7, 0, -15),
                        ("firepit", .85, -.75, -1.45, 0, 0)],
    "giant_fish_carcass": [("fish", 3.1, 0, 0, 0, -10), ("bones", 1.15, 1.1, -.4, 0, -20)],
    "bookshelf": [("bookcase", 2.1, 0, .2, 0, 0), ("books", 1, 1.1, -.35, 0, 25)],
    "sacrificial_stone": [("surgeon_table", 2.6, 0, 0, 0, 0), ("meat", .7, -.5, -.8, 0, 15),
                          ("candles", .65, 1.4, .1, 0, 0)],
    "cc_egg_cluster": [("eggs", 1.8, -.5, 0, 0, 0), ("eggs", 1, .85, -.25, 0, 70)],
    "shipment_crates": [("crate", 1.45, -.7, .15, 0, -8), ("crate", 1.15, -.7, .15, 1.15, 6),
                        ("crate", 1.15, .75, -.25, 0, 12), ("barrel", .8, 1.45, .4, 0, 0)],
    "shamblers_altar": [("surgeon_table", 2.3, 0, 0, 0, 0), ("tentacles", 1.8, -.95, .1, 0, -15),
                        ("candles", .75, 1.35, -.25, 0, 0)],
}

out = args.out / "concepts"
out.mkdir(exist_ok=True)
manifest = {}
for key, placements in STUDIES.items():
    L.reset()
    lower, upper = [], []
    for source, width, x, z, height, yaw in placements:
        folder = args.out / "parts" / source
        lo, hi = L.bbox(folder)
        scale = width / max(float(hi[0] - lo[0]), float(hi[2] - lo[2]))
        transform, _ = L.place_matrix(folder, (x, height, z), rot=yaw, scale=scale)
        objects, lo, hi = L.build(folder, place=transform)
        assert objects, source
        lower.append(lo)
        upper.append(hi)
    lo, hi = np.min(lower, axis=0), np.max(upper, axis=0)
    center = (lo + hi) / 2
    radius = float(np.linalg.norm(hi - lo)) / 2
    L.camera(tuple(center + [radius * .35, -radius * 3.3, radius * .65]), tuple(center),
             fov=38, res=(1000, 760))
    L.render(out / (key + ".png"), samples=8, transparent=True)
    manifest[key] = {"status": "Native prop assembly study; custom sculpture and moving parts remain to build.",
                     "placements": placements}
    print("CURIO_STUDY", key, flush=True)
(out / "studies.json").write_text(json.dumps(manifest, indent=2), encoding="utf-8")
