"""Give the Guild fitted portal courses and bonded corner stones.

Blender-only recipe. Reuses the reviewed Abbey limestone painting so the Guild
and neighbouring services share one material language. Native parts stay intact.
"""
import argparse
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))
import geometry as G
import masonry_detail as M
import workshops as W


def guild():
    M.remove_frames('Guild armoured portal')
    M.ring('Guild coursed portal', -.65, .45, -2.53, 1.65, 2.65, .20)
    # Bonded L-shaped quoins turn both corners. Alternating lengths avoid the
    # stacked vertical strips of the earlier trim, without obscuring the banners.
    for side, x in [(-1, -3.4), (1, 2.1)]:
        for end, z in [(-1, -2.5), (1, 3.1)]:
            for row in range(7):
                y = .61+row*.43
                front, flank = (.49, .28) if row % 2 == 0 else (.29, .47)
                M.block('Guild bonded face quoin',
                        (x-side*(front/2-.055), y, z+end*.025),
                        (front, .408, .13), row+int(side+end))
                M.block('Guild bonded return quoin',
                        (x+side*.025, y, z-end*(flank/2-.055)),
                        (.13, .408, flank), row+2+int(side+end))
    # The wing window shares the same dressed stone and drip edge as the portal.
    M.remove_frames('Guild wing casement')
    M.ring('Guild armory casement', 3, 1.8, -1.67, .8, 1.25, .115)


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    for name in ['source', 'out', 'kit']:
        parser.add_argument('--'+name, type=Path, required=True)
    parser.add_argument('--resolution', type=int, default=900)
    parser.add_argument('--samples', type=int, default=16)
    args = parser.parse_args(sys.argv[sys.argv.index('--')+1:])
    W.run(args, refiners={'guild': guild}, detail='v33',
          design='Guild fitted limestone portal, bonded corner courses and armory window surround')
