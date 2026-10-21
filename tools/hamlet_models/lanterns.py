"""Replace six primitive Hamlet lamps with uniformly scaled native DD2 lamps.

Blender-only private asset recipe. Uses the extracted common_story_assist_prefab,
retains the painted UVs and adds wall brackets. No generated art or runtime edits.
"""
import argparse
import hashlib
import importlib.util
import json
import sys
from pathlib import Path

import bpy

sys.path.insert(0, str(Path(__file__).resolve().parent))
import geometry as G
import workshops as W


def replace(service, variant, count):
    # Each service import starts with a fresh Blender scene. A fresh native
    # loader avoids cached materials referring to objects from the previous one.
    spec = importlib.util.spec_from_file_location('hamlet_native_lamp_loader', G.KIT/'tools/bl_lib.py')
    library = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(library)
    G.LIB = library
    old = [o for o in bpy.data.objects if o.type == 'MESH' and o.name.startswith('Lantern glowing glass')]
    assert len(old) == count, (service, 'unexpected source lamp count', len(old))
    centres = []
    for ob in old:
        points = [ob.matrix_world@v.co for v in ob.data.vertices]
        centres.append(tuple((min(p[k] for p in points)+max(p[k] for p in points))*.5 for k in range(3)))
    W.remove('Lantern glowing glass', 'Lantern iron', 'Lantern cap')
    if service == 'Tavern':
        # The old lamps sat well ahead of both facades, including one in the
        # open passage. These positions follow the native tower/door walls.
        centres = [(-2.1, -2.0, 2.8), (2.4, -2.46, 2.8)]
    selected = 'common_prop_lamp_'+variant
    for i, (x, z, y) in enumerate(sorted(centres)):
        lamps = G.native('common_story_assist_prefab', pos=(x, y-.28, z), width=.30,
                         include=[selected], shade=1.4)
        assert len(lamps) == 1, 'Expected one selected native lamp mesh'
        ob = lamps[0]
        ob.name = service+' native lamp '+str(i+1)+' / '+selected
        bpy.context.view_layer.update()
        points = [ob.matrix_world@v.co for v in ob.data.vertices]
        top = max(p.z for p in points)
        # Chain, ring, cage and original painted glass are retained. The bracket
        # meets the chain's upper endpoint instead of leaving it floating.
        anchor_x = -.55 if service == 'Sanitarium' else x
        reach = .50 if service == 'Caravan' else .35
        G.beam(service+' lantern wall bracket', (x, top, z), (anchor_x, top, z+reach), .045, G.ink())
        G.beam(service+' lantern bracket brace', (x, top-.02, z+.02),
               (anchor_x, top-.27, z+reach), .035, G.ink())


def run(args):
    folder = args.kit.resolve()/'exp/common_story_assist_prefab'
    doc = json.loads((folder/'scene.json').read_text())
    chosen = [o for o in doc['objects'] if o['name'] in ('common_prop_lamp_01','common_prop_lamp_02')]
    assert len(chosen) == 2
    files = {folder/'scene.json', args.kit.resolve()/'tools/bl_lib.py'}
    for ob in chosen:
        files.add(folder/('mesh-'+ob['mesh']+'.npz'))
        for key in ob['materials']:
            files.add(folder/doc['materials'][key]['texture'])
    records = {str(p.relative_to(args.kit.resolve())):hashlib.sha256(p.read_bytes()).hexdigest() for p in files}
    W.run(args, refiners={
        'guild': lambda: replace('Guild', '01', 1),
        'sanitarium': lambda: replace('Sanitarium', '02', 1),
        'tavern': lambda: replace('Tavern', '01', 2),
        'nomad_wagon': lambda: replace('Caravan', '02', 2),
    }, detail='v29', design='Native DD2 painted lanterns, original proportions/UVs and fitted wall brackets')
    (args.out/'lantern-sources.json').write_text(json.dumps({
        'files_sha256':records, 'selection':[o['name'] for o in chosen],
        'source':'common_story_assist_prefab', 'width':.30, 'shade':1.4,
        'new_bitmaps':False, 'lanterns':6, 'other_services_byte_identical':6,
    }, indent=2))


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    for name in ['source', 'out', 'kit']:
        parser.add_argument('--'+name, type=Path, required=True)
    parser.add_argument('--resolution', type=int, default=900)
    parser.add_argument('--samples', type=int, default=16)
    run(parser.parse_args(sys.argv[sys.argv.index('--')+1:]))
