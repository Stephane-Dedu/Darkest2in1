"""Fit Tavern sign supports and old lamp backplates to the actual facades.

Blender entry point. Read an accepted private ten-model library and write a new
one. Existing native meshes, UVs and painted materials are retained.
"""
import argparse
import sys
from pathlib import Path

import bpy
from mathutils import Vector

sys.path.insert(0, str(Path(__file__).resolve().parent))
import geometry as G
import workshops as W


def tavern():
    signs = [o for o in bpy.data.objects if o.type == 'MESH' and o.name.startswith((
        'Sign suspension chain', 'Tankard handle', 'Tankard sign',
        'Tavern brass sign', 'Tavern sign bracket'))]
    assert len(signs) == 8, 'Expected the eight original sign components'
    for ob in signs:
        ob.matrix_world.translation += Vector((0, 1.3, 0))

    plates = sorted([o for o in bpy.data.objects if o.type == 'MESH'
                     and o.name.startswith('Tavern lantern backplate')],
                    key=lambda o: o.matrix_world.translation.x)
    assert len(plates) == 2, 'Expected two old lamp backplates'
    for ob, target in zip(plates, [(-2.1, -1.72, 3.45), (2.4, -2.18, 3.45)]):
        points = [ob.matrix_world@v.co for v in ob.data.vertices]
        centre = Vector(tuple((min(p[k] for p in points)+max(p[k] for p in points))*.5
                              for k in range(3)))
        ob.matrix_world.translation += Vector(target)-centre

    G.beam('Tavern sign diagonal brace', (-2.6, 2.85, -1.67),
           (-2.6, 3.2, -2.65), .065, G.ink())
    # Both suspension links need a physical attachment, not just the central arm.
    G.beam('Tavern sign chain crossbar', (-2.84, 3.2, -2.7),
           (-2.36, 3.2, -2.7), .055, G.ink())


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    for name in ['source', 'out', 'kit']:
        parser.add_argument('--'+name, type=Path, required=True)
    parser.add_argument('--resolution', type=int, default=900)
    parser.add_argument('--samples', type=int, default=12)
    args = parser.parse_args(sys.argv[sys.argv.index('--')+1:])
    W.run(args, refiners={'tavern': tavern}, detail='v30',
          design='Tavern sign, suspension links and lamp backplates fitted to physical walls')
