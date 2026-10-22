"""Fit a complete DD2 Tangle tent to the Survivalist's existing campsite.

Blender authoring only. Keep native paint/UVs/proportions and the retained camp
equipment. Replace the low-detail authored thatch-mapped shelter, poles and ropes.
"""
import argparse
import hashlib
import json
import sys
from pathlib import Path

import bpy
import numpy as np

sys.path.insert(0,str(Path(__file__).resolve().parent))
import geometry as G
import workshops as W

PREFIXES=('Full depth ridge tent','Left tent flap','Right tent flap',
          'Tent ridge pole','Ridge beam','Tent guy rope','Tent sewn ridge seam',
          'Tent driven peg','Rolled bedroll')


def fit(asset,yaw):
    removed=[o for o in bpy.data.objects if o.type=='MESH' and o.name.startswith(PREFIXES)]
    assert len(removed)==21,'Authored shelter layout changed'
    names=[o.name for o in removed]
    for o in removed:bpy.data.objects.remove(o,do_unlink=True)
    objects=G.native(asset,pos=(0,.035,0),width=8.7,rotation=yaw,shade=1.4)
    assert len(objects)==1
    points=np.array([tuple(o.matrix_world@v.co) for o in objects for v in o.data.vertices])
    low,high=points.min(0),points.max(0)
    assert low[0]>=-4.4 and high[0]<=4.4 and low[1]>=-3.1 and high[1]<=3.1
    assert low[2]>=0 and high[2]<=5.4,'Keep the native shelter within the campsite height budget'
    (G.OUT/'camp-fit.json').write_text(json.dumps({'asset':asset,'yaw':yaw,'width':8.7,
        'removed_authored_meshes':names,'native_bounds_blender':[low.tolist(),high.tolist()],
        'original_native_UVs_proportions_paint':True,'new_bitmaps':False},indent=2))


def run(args):
    sys.path.insert(0,str(args.kit.resolve()/'tools'))
    import bl_lib as L
    def refine():
        G.LIB=L
        fit(args.asset,args.yaw)
    folder=args.kit.resolve()/'exp'/args.asset;doc=json.loads((folder/'scene.json').read_text())
    files={folder/'scene.json'}
    for ob in doc['objects']:
        files.add(folder/('mesh-'+ob['mesh']+'.npz'))
        files.update(folder/doc['materials'][m]['texture'] for m in ob['materials'])
    W.run(args,refiners={'camping_trainer':refine},detail='v47',
          design='Native Tangle canvas shelter, original UVs/proportions; retained Survivalist equipment')
    (args.out/'camp-native-sources.json').write_text(json.dumps({
        str(p.resolve().relative_to(args.kit.resolve())):hashlib.sha256(p.read_bytes()).hexdigest()
        for p in files},indent=2))


if __name__=='__main__':
    p=argparse.ArgumentParser(description=__doc__)
    for n in ('source','out','kit'):p.add_argument('--'+n,type=Path,required=True)
    p.add_argument('--asset',choices=('forest_tent_01','forest_tent_02'),default='forest_tent_02')
    p.add_argument('--yaw',type=float,default=0)
    p.add_argument('--resolution',type=int,default=1100)
    p.add_argument('--samples',type=int,default=16)
    run(p.parse_args(sys.argv[sys.argv.index('--')+1:]))
