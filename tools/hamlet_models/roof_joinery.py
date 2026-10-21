"""Fit individual roof courses and joins to the accepted Guild and Sanitarium.

Private Blender authoring only. Retains native components and the other eight
verified service GLBs, and reuses the reviewed slate, clay, oak and iron paint.
"""
import argparse
import math
import random
import sys
from pathlib import Path

import bpy
from mathutils import Vector

sys.path.insert(0, str(Path(__file__).resolve().parent))
import geometry as G
import roof_detail as R
import workshops as W


# Interior tile faces only. The upper edge of the painted first course contains
# large shadows from the preceding course; sampling those onto separate pieces
# makes false rectangular holes across an otherwise fully covered roof.
CLAY = ((177, 99, 224, 158), (269, 102, 321, 163), (361, 104, 445, 158),
        (510, 99, 605, 148), (665, 111, 711, 163), (766, 109, 848, 158),
        (925, 110, 1009, 155), (1090, 107, 1142, 154),
        (123, 245, 206, 315), (287, 244, 381, 313),
        (470, 242, 509, 294), (565, 240, 642, 309))


class Tiles(R.Courses):
    def mesh(self, name, material):
        ob = super().mesh(name, material)
        # Extruded sides need two-dimensional paint, including end-cap triangles.
        # Their top/bottom UVs would otherwise occupy the same line.
        uv = ob.data.uv_layers.active
        ob.data.materials.append(G.material('Tile cut-edge charcoal v28', (.10,.09,.09,1),
                                             'slate_v20', .4))
        for face in ob.data.polygons:
            if abs(face.normal.z) > .08:
                continue
            face.material_index = 1
            tangent = Vector((-face.normal.y, face.normal.x, 0)).normalized()
            centre = sum((ob.data.vertices[i].co for i in face.vertices), Vector())/len(face.vertices)
            paint = sum((uv.data[i].uv for i in face.loop_indices), Vector((0., 0.)))/len(face.vertices)
            for li in face.loop_indices:
                delta = ob.data.vertices[ob.data.loops[li].vertex_index].co-centre
                uv.data[li].uv = paint+Vector((delta.dot(tangent)*.15, delta.z*.15))
        return ob


def paint(clay=False):
    return G.material('Guild aged clay courses v28' if clay else 'Service weathered slate v28',
                      (.94, .88, .85, 1) if clay else (.92, .94, .98, 1),
                      'red_v20' if clay else 'slate_v20', .5)


def pitched(name, old_name, x, z, length, width, base, rise, clay=False, along_x=True, cutouts=()):
    old = bpy.data.objects[old_name]
    height = R.roof_height(old)
    # Keep the original solid deck close beneath the courses so sloping verges
    # and existing gables continue to meet, without a hollow roof sandwich.
    R.lower_deck(old, .12)
    old.data.materials.clear()
    old.data.materials.append(G.material('Clay underlap v28' if clay else 'Slate underlap v28',
                                       (.18, .13, .12, 1) if clay else (.13, .14, .16, 1),
                                       'red_v20' if clay else 'slate_v20', .4))
    for face in old.data.polygons:
        face.material_index = 0
    profile = [(-.5*width, 0), (-.36*width, .13*rise), (-.23*width, .35*rise),
               (-.10*width, .71*rise), (0, rise)]
    at, distance = R.surface(profile)

    def point(t, s, side, lift):
        d, h = at(s)
        xx, zz = (x+t, z+side*d) if along_x else (x+side*d, z+t)
        return xx, height(xx, zz, base+h-.13*(1-(2*t/length)**2))+lift, zz

    rng = random.Random(name)
    tiles = Tiles(cutouts)
    rectangles = CLAY if clay else R.SLATES
    rows = max(2, round(distance/(.38 if clay else .34)))
    step = distance/rows
    for side in [1, -1]:
        for row in range(rows):
            tail, head = row*step, min(distance+.008, (row+1)*step+.10)
            t = -length/2
            first = True
            while t < length/2-.025:
                width_tile = rng.uniform(.34, .50)
                if first and row % 2:
                    width_tile *= .55
                first = False
                width_tile = min(width_tile, length/2-t)
                rect = rectangles[rng.randrange(len(rectangles))]
                outline = [(0, .055), (.12, .008), (.57, rng.uniform(-.018, .018)),
                           (.95, .025), (1, .09), (1, 1), (0, 1)]
                points = [point(t+u*width_tile, tail+(head-tail)*v, side, .025+.042*(1-v))
                          for u, v in outline]
                tiles.piece(points, [R.uv(rect, u, max(0, v)) for u, v in outline], .026)
                t += width_tile-.004
    tiles.mesh(name+' fitted overlapping tiles', paint(clay))

    caps = Tiles()
    t = -length/2
    while t < length/2-.02:
        end = min(t+.35, length/2)
        # A folded saddle, with its edges actually seated against both pitches.
        pts = [point(tt, distance-abs(offset), 1 if offset < 0 else -1, .045)
               for tt in [t, end] for offset in [-.12, 0, .12]]
        for indices in [(0, 1, 4, 3), (1, 2, 5, 4)]:
            caps.piece([pts[i] for i in indices],
                       [R.uv(rectangles[int((t+length)*11) % len(rectangles)], u, v)
                        for u, v in [(0, 0), (0, 1), (1, 1), (1, 0)]], .025)
        t += .32
    caps.mesh(name+' fitted ridge saddles', paint(clay))
    return height


def flash(name, x, height):
    mat = G.material('Guild weathered lead flashing v28', (.30, .29, .27, 1), 'iron_v21', .4)
    strips = Tiles()
    # Each cheek gets lapped metal following the slope, plus a broad apron at
    # its lower edge. This closes the transition instead of leaving a raw cut.
    for side in [-1, 1]:
        xx = x+side*.55
        for i in range(5):
            a = -2.52+i*.25
            b = min(a+.29, -1.23)
            pts = [(u, height(u, d, 7.5)+.08, d)
                   for u, d in [(xx, a), (xx+side*.12, a), (xx+side*.12, b), (xx, b)]]
            strips.piece(pts, [(.25,.25),(.36,.25),(.36,.39),(.25,.39)], .018)
    pts = [(u, height(u, d, 7.5)+.085, d)
           for u, d in [(x-.66,-2.56),(x+.66,-2.56),(x+.66,-2.35),(x-.66,-2.35)]]
    strips.piece(pts, [(.12,.32),(.84,.32),(.84,.44),(.12,.44)], .018)
    strips.mesh(name+' stepped lead flashing', mat)


def guild():
    W.remove('Guild red hall individual ridge tile', 'Guild armory individual ridge tile',
             'Guild roof dormer individual ridge tile')
    height = pitched('Guild hall', 'Guild red hall painted roof', -.65, .3, 6.3, 6.5, 6.7, 3.4,
                     clay=True, cutouts=[(x-.55,-2.48,x+.55,-1.23) for x in [-2.12,.85]])
    for i, x in enumerate([-2.12, .85]):
        old = 'Guild roof dormer painted roof'+('.001' if i else '')
        pitched('Guild dormer '+str(i+1), old, x, -1.85, 1.6, 1.38, 8.46, .9, True, False)
        flash('Guild dormer '+str(i+1), x, height)
    pitched('Guild armory', 'Guild armory painted roof', 3, .7, 5.2, 2.7, 3.8, 1.8, along_x=False)
    # The exposed rafters explain how the deep eaves are supported. Local oak
    # grain follows every timber, including the old fascia and gable verge.
    for ob in list(bpy.data.objects):
        if ob.type == 'MESH' and ' / ' not in ob.name and ('eave fascia' in ob.name or 'verge coping' in ob.name):
            if ob.name.startswith(('Guild red hall', 'Guild roof dormer')):
                W.paint(ob, W.wood(), (.02,.07,.98,.93), True)
    for x in [-3.35,-2.5,-1.6,-.65,.3,1.2,2.05]:
        for side in [-1, 1]:
            sag = .13*(1-(2*(x+.65)/6.3)**2)
            W.timber('Guild exposed eave rafter', (x,6.48-sag,.3+side*2.72),
                     (x,6.56-sag,.3+side*3.18), .11)


def turret():
    old = bpy.data.objects['Sanitarium swept witch cap']
    # The tower was resized around its original base in the accepted model.
    # Match its twelve actual facets rather than using a round cone outside it.
    x, z, radius = -3., -2., 1.86*.86
    base, rise = 1.1+(7.3-1.1)*.91, 2.7*.91
    profile = [(radius*r, rise*h) for r, h in [(1,0),(.91,.13),(.69,.34),(.40,.66),(.13,.90),(0,1)]]
    at, distance = R.surface(profile)
    R.lower_deck(old, .035)
    old.data.materials.clear()
    old.data.materials.append(G.material('Turret dark slate underlap v28', (.12,.14,.17,1),'slate_v20',.4))
    for face in old.data.polygons:
        face.material_index = 0
    # Retain the existing slender hip bindings. They seal the facet seams and
    # preserve the original eave footprint used by the runtime model fit.
    tiles = Tiles()
    rng = random.Random(28276)
    rows = 10
    step = distance/rows
    for row in range(rows):
        tail, head = row*step, min(distance-.03, (row+1)*step+.09)
        r, _ = at(tail)
        pieces = max(1, round((2*r*math.sin(math.pi/12))/.32))
        for facet in range(12):
            a, b = (facet+.5)*math.tau/12, (facet+1.5)*math.tau/12
            for j in range(pieces):
                rect = R.SLATES[rng.randrange(len(R.SLATES))]
                outline = [(0,.04),(.12,0),(.57,rng.uniform(-.012,.018)),(.95,.025),(1,.07),(1,1),(0,1)]
                pts = []
                for u, v in outline:
                    t = (j+u)/pieces
                    rad, h = at(tail+(head-tail)*v)
                    xx = rad*((1-t)*math.cos(a)+t*math.cos(b))
                    zz = rad*((1-t)*math.sin(a)+t*math.sin(b))
                    pts.append((x+xx, base+h+.018+.035*(1-v), z+zz))
                tiles.piece(pts, [R.uv(rect,u,max(0,v)) for u,v in outline], .023)
    tiles.mesh('Sanitarium turret fitted slate courses', paint())


def sanitarium():
    turret()
    W.remove('Sanitarium porch individual ridge tile')
    pitched('Sanitarium porch', 'Sanitarium porch painted roof', .45, -2.7, 2.1, 2.8, 3.95, 1.65, along_x=False)


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    for name in ['source','out','kit']:
        parser.add_argument('--'+name, type=Path, required=True)
    parser.add_argument('--resolution', type=int, default=900)
    parser.add_argument('--samples', type=int, default=16)
    args = parser.parse_args(sys.argv[sys.argv.index('--')+1:])
    W.run(args, refiners={'guild':guild,'sanitarium':sanitarium}, detail='v28',
          design='v28: fitted clay/slate courses, dormer flashing, ridge saddles and eave joinery')
