"""Fit forged hinges and ring plates to the Abbey's existing recessed oak door.

Blender-only recipe. Reuse reviewed workshop iron; preserve wall/door geometry,
native components, the service palette and the complete ten-model footprint.
"""
import argparse
import math
import sys
from pathlib import Path

import bpy

sys.path.insert(0, str(Path(__file__).resolve().parent))
import architecture as A
import geometry as G
import workshops as W


def metal(ob, bright=False):
    return W.paint(ob, W.iron(bright), (.16, .18, .84, .82))


def stud(name, x, y, z, radius=.023):
    ob = G.cylinder(name, (x, y, z), radius, .035, W.iron(True), 6)
    ob.rotation_euler.x = math.pi/2
    return metal(ob, True)


def abbey():
    old = [o for o in bpy.data.objects if o.type == 'MESH' and o.name.startswith((
        'Abbey great portal iron strap', 'Abbey great portal strap rivet',
        'Abbey great portal forged ring'))]
    assert len(old) == 8, 'Expected two straps, four rivets and two rings'
    for ob in old:
        bpy.data.objects.remove(ob, do_unlink=True)
    x, y, width, height = -2.55, 1.32, 1.65, 2.9
    # The oak face is at -4.07. Physical straps sit on it, clear of the stone reveal.
    front, back = -4.155, -4.066
    for row, level in enumerate((.22, .55)):
        yy = y+height*level
        for side in (-1, 1):
            hinge = x+side*.70
            profile = [(0,-.075),(.11,-.064),(.21,-.029),(.43,-.038),
                       (.53,-.071),(.64,0),(.53,.073),(.43,.041),
                       (.21,.032),(.11,.069),(0,.078)]
            shape = [(hinge-side*u, yy+v) for u,v in profile]
            metal(A.prism('Abbey forged leaf strap', shape, front, back, W.iron()))
            # Separate knuckles and projecting pins give each leaf a real hinge.
            metal(G.cylinder('Abbey hinge pin', (hinge,yy,-4.100), .034,.25,W.iron(),8))
            for offset in (-.085,.085):
                metal(G.cylinder('Abbey hinge knuckle', (hinge,yy+offset,-4.100),
                                 .045,.042,W.iron(True),8), True)
            for distance in (.14,.37):
                stud('Abbey hammered strap stud', hinge-side*distance, yy, front-.008)
    for side in (-1,1):
        xx = x+side*.165; yy = y+height*.42
        shape = [(xx-.075,yy-.08),(xx+.070,yy-.08),(xx+.083,yy+.065),
                 (xx,yy+.15),(xx-.080,yy+.07)]
        metal(A.prism('Abbey ring escutcheon',shape,-4.145,-4.066,W.iron()))
        stud('Abbey ring pivot',xx,yy+.034,-4.158,.029)
        bpy.ops.mesh.primitive_torus_add(major_radius=.088,minor_radius=.017,
            major_segments=12,minor_segments=5,location=G.p((xx,yy-.057,-4.18)),
            rotation=(math.pi/2,0,0))
        ob=bpy.context.object;ob.name='Abbey hanging iron ring';metal(ob,True)


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    for name in ['source','out','kit']:
        parser.add_argument('--'+name,type=Path,required=True)
    parser.add_argument('--resolution',type=int,default=1100)
    parser.add_argument('--samples',type=int,default=16)
    args=parser.parse_args(sys.argv[sys.argv.index('--')+1:])
    W.run(args,refiners={'abbey':abbey},detail='v37',
          design='Abbey split forged hinges, hammered studs and suspended ring plates on existing oak')
