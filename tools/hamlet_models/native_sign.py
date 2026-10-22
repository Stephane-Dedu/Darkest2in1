"""Replace the Tavern's primitive sign with DD2's painted inn crest.

Blender entry point. Retain native proportions/UVs and attach the complete sign
assembly to the fitted wall arm; keep the reviewed service palette.
"""
import argparse
import hashlib
import json
import sys
from pathlib import Path

import bpy

sys.path.insert(0, str(Path(__file__).resolve().parent))
import geometry as G
import workshops as W


def tavern():
    old = [o for o in bpy.data.objects if o.type == 'MESH' and o.name.startswith((
        'Sign suspension chain', 'Tankard handle', 'Tankard sign',
        'Tavern brass sign', 'Tavern sign bracket', 'Tavern sign chain crossbar'))]
    assert len(old) == 9, 'Expected the fitted primitive sign assembly'
    for ob in old:
        bpy.data.objects.remove(ob, do_unlink=True)
    # The native sign includes its crossbar, hanging links and shield. Keep it
    # intact instead of painting a flat emblem onto a new board. Its top is3.189.
    G.native('inn_lvl_02_prefab', pos=(-2.6, 2.1, -2.7), width=1.0,
             include=['the_inn_sign_01'], shade=1.4)
    G.beam('Tavern native sign wall arm', (-2.6, 3.15, -1.67),
           (-2.6, 3.15, -2.7), .065, G.ink())
    # Round278's diagonal brace still meets the arm beside the crossbar.


def run(args):
    sys.path.insert(0, str(args.kit.resolve()/'tools'))
    import bl_lib as L
    def refine():
        G.LIB = L
        tavern()
    folder = args.kit.resolve()/'exp/inn_lvl_02_prefab'
    doc = json.loads((folder/'scene.json').read_text())
    chosen = [o for o in doc['objects'] if o['name'] == 'the_inn_sign_01']
    assert len(chosen) == 1
    files = {folder/'scene.json', args.kit.resolve()/'tools/bl_lib.py'}
    for ob in chosen:
        files.add(folder/('mesh-'+ob['mesh']+'.npz'))
        for key in ob['materials']:
            files.add(folder/doc['materials'][key]['texture'])
    records = {str(p.relative_to(args.kit.resolve())):hashlib.sha256(p.read_bytes()).hexdigest() for p in files}
    W.run(args, refiners={'tavern': refine}, detail='v35',
          design='Native DD2 painted inn crest, complete suspension and fitted wall arm')
    (args.out/'sign-sources.json').write_text(json.dumps({
        'files_sha256': records, 'source': 'inn_lvl_02_prefab/the_inn_sign_01',
        'width': 1.0, 'shade': 1.4, 'new_bitmaps': False,
        'native_proportions_and_UVs_retained': True,
    }, indent=2))


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    for name in ['source', 'out', 'kit']:
        parser.add_argument('--'+name, type=Path, required=True)
    parser.add_argument('--resolution', type=int, default=900)
    parser.add_argument('--samples', type=int, default=16)
    run(parser.parse_args(sys.argv[sys.argv.index('--')+1:]))
