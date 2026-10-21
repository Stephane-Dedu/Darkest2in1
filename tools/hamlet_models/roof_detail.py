"""Give reviewed roof paintings physical courses, chipped slate and reed edges.

Blender entry point for a separate private ten-model library. Only the Abbey and
Smithy roofs change. Native parts, terraces and the accepted masonry stay intact.
"""
import argparse
import bisect
import math
import random
import sys
from pathlib import Path

import bpy
import numpy as np
from mathutils import Vector
from mathutils.bvhtree import BVHTree

sys.path.insert(0, str(Path(__file__).resolve().parent))
import geometry as G
import workshops as W


class Courses:
    """Collect closed pieces into one mesh/material instead of hundreds of nodes."""
    def __init__(self, cutouts=()):
        self.vertices, self.faces, self.uvs = [], [], []
        self.cutouts = cutouts

    def piece(self, points, texcoords, thickness):
        # Clip each bundle before combining. Boolean subtraction on intersecting
        # closed bundles can silently discard entire neighbours.
        polygons = [list(zip(points, texcoords))]
        for x0, z0, x1, z1 in self.cutouts:
            result = []
            for poly in polygons:
                inside = poly
                for axis, edge, sign in [(0, x0, 1), (0, x1, -1), (2, z0, 1), (2, z1, -1)]:
                    def split(keep):
                        clipped = []
                        for a, b in zip(inside, inside[1:]+inside[:1]):
                            da, db = sign*(a[0][axis]-edge), sign*(b[0][axis]-edge)
                            ia, ib = da*keep >= 0, db*keep >= 0
                            if ia:
                                clipped.append(a)
                            if ia != ib:
                                t = da/(da-db)
                                clipped.append((tuple(p+(q-p)*t for p, q in zip(a[0], b[0])),
                                                tuple(p+(q-p)*t for p, q in zip(a[1], b[1]))))
                        return clipped
                    outside = split(-1)
                    retained = split(1)
                    if len(outside) >= 3:
                        result.append(outside)
                    inside = retained
                    if len(inside) < 3:
                        break
            polygons = result
        for polygon in polygons:
            self.closed([p for p, uv in polygon], [uv for p, uv in polygon], thickness)

    def closed(self, points, texcoords, thickness):
        start = len(self.vertices)
        n = len(points)
        self.vertices += points + [(x, y-thickness, z) for x, y, z in points]
        faces = [list(range(n)), list(range(n, n*2))[::-1]]
        faces += [[i, (i+1) % n, (i+1) % n+n, i+n] for i in range(n)]
        for face in faces:
            self.faces.append([start+i for i in face])
            self.uvs.append([texcoords[i % n] for i in face])

    def mesh(self, name, material):
        ob = G.mesh(name, self.vertices, self.faces, material, self.uvs)
        # Individual pieces can have opposite slope orientations.
        import bmesh
        bm = bmesh.new()
        bm.from_mesh(ob.data)
        bmesh.ops.recalc_face_normals(bm, faces=list(bm.faces))
        bm.to_mesh(ob.data)
        bm.free()
        return ob


def uv(rect, u, v):
    x0, y0, x1, y1 = rect
    # Image rectangles use top-left pixels; the mesh's v runs uphill.
    return ((x0+(x1-x0)*u)/1254, 1-(y1-(y1-y0)*v)/1254)


def surface(profile):
    """Map distance along a pitched roof to its original depth/height profile."""
    distances = [0.]
    for a, b in zip(profile, profile[1:]):
        distances.append(distances[-1]+math.dist(a, b))

    def at(s):
        i = min(len(profile)-2, max(0, bisect.bisect_right(distances, s)-1))
        t = (s-distances[i])/(distances[i+1]-distances[i])
        return tuple(a+(b-a)*t for a, b in zip(profile[i], profile[i+1]))

    return at, distances[-1]


def lower_deck(ob, distance):
    """Old solidified sheets may extend above their profile after axis conversion."""
    for vertex in ob.data.vertices:
        p = ob.matrix_world@vertex.co
        p.z -= distance
        vertex.co = ob.matrix_world.inverted()@p


def roof_height(ob):
    """Fit against the exported outer surface, including its solidified thickness."""
    bpy.context.view_layer.update()
    tree = BVHTree.FromPolygons([ob.matrix_world@v.co for v in ob.data.vertices],
                               [list(p.vertices) for p in ob.data.polygons])

    def at(x, z, fallback):
        hit, _, _, _ = tree.ray_cast(Vector((x, z, 50)), Vector((0, 0, -1)))
        return hit.z if hit is not None else fallback
    return at


# Whole individual slate faces from the reviewed painting, including their wear.
SLATES = ((106, 23, 212, 131), (264, 26, 391, 132), (492, 27, 565, 124),
          (627, 28, 814, 140), (845, 28, 914, 136), (934, 29, 1082, 131),
          (72, 166, 204, 262), (246, 167, 324, 260), (366, 167, 482, 264),
          (530, 169, 649, 259), (684, 169, 801, 263), (812, 174, 942, 263))


def slate_roof(name, x, z, length, width, base, rise, along_x=True):
    W.remove(name+' individual ridge tile')
    old = bpy.data.objects[name+' painted roof']
    fitted = roof_height(old)
    old.data.materials.clear()
    old.data.materials.append(G.material('Slate underlap shadow v26', (.033, .030, .035, 1), emission=.3))
    for face in old.data.polygons:
        face.material_index = 0
    lower_deck(old, .15)
    courses = Courses()
    rng = random.Random(name)
    profile = [(-.5*width, 0), (-.36*width, .13*rise), (-.23*width, .35*rise),
               (-.10*width, .71*rise), (0, rise)]
    at, distance = surface(profile)

    def point(t, s, side, lift):
        depth, height = at(s)
        sag = .13*(1-(2*t/length)**2)
        xx, zz = (x+t, z+side*depth) if along_x else (x+side*depth, z+t)
        y = fitted(xx, zz, base+height-sag)+lift
        return xx, y, zz

    # Diminishing courses stay the same material scale on the small porch.
    count = max(2, round(distance/.39))
    step = distance/count
    for side in [1, -1]:
        for row in range(count):
            tail = row*step
            head = min(distance+.015, tail+step+.12)
            t = -length/2
            first = True
            while t < length/2-.02:
                w = rng.uniform(.40, .62)
                if first and row % 2:
                    w *= .56
                first = False
                w = min(w, length/2-t)
                if w < .035:
                    break
                rect = SLATES[rng.randrange(len(SLATES))]
                # Slightly chipped corners and a displaced heel give a real eave
                # silhouette without turning every tile into a broken tooth.
                jitter = rng.uniform(-.023, .023)
                outline = [(0, .05), (.10, 0), (.53, rng.uniform(-.025, .012)),
                           (.94, .015), (1, .075), (1, 1), (0, 1)]
                points = []
                texcoords = []
                for u, v in outline:
                    s = tail+(head-tail)*v+jitter*(1-v)
                    points.append(point(t+u*w, s, side, .026+.052*(1-v)))
                    texcoords.append(uv(rect, u, max(0, v)))
                courses.piece(points, texcoords, .035)
                t += w-.006
    mat = G.material('Abbey individual weathered slate v26', (.92, .93, .96, 1), 'slate_v20', .5)
    courses.mesh(name+' fitted slate courses', mat)
    # Folded ridge saddles have a roof profile, unlike the former square beams.
    ridge = Courses()
    for i, t in enumerate(np.arange(-length/2, length/2-.02, .38)):
        end = min(t+.41, length/2)
        pts = []
        for tt in [t, end]:
            for offset in [-.145, 0, .145]:
                sag = .13*(1-(2*tt/length)**2)
                y = base+rise-sag+.10-abs(offset)*.9
                pts.append((x+tt, y, z+offset) if along_x else (x+offset, y, z+tt))
        for indices in [(0, 1, 4, 3), (1, 2, 5, 4)]:
            ridge.piece([pts[j] for j in indices],
                        [uv(SLATES[i % len(SLATES)], u, v) for u, v in [(0, 0), (0, 1), (1, 1), (1, 0)]], .025)
    ridge.mesh(name+' folded ridge caps', mat)


def abbey():
    slate_roof('Abbey sweeping nave', 0, .5, 8.7, 6.7, 5.45, 2.8)
    slate_roof('Abbey porch', -2.55, -3.25, 2.9, 3.1, 4.8, 2.3, False)


REED_ROWS = ((12, 213), (234, 432), (458, 648), (680, 865), (902, 1090))


def reed_courses(name, left, right, profiles, pos, seed, cutouts=()):
    rng = random.Random(seed)
    courses = Courses(cutouts)
    for side, profile in enumerate(profiles):
        at, distance = surface(profile)
        rows = max(2, round(distance/.70))
        step = distance/rows
        for row in range(rows):
            tail = row*step
            head = min(distance, tail+step+.17)
            t = left
            first = True
            while t < right-.02:
                w = rng.uniform(.48, .83)
                if first and row % 2:
                    w *= .58
                first = False
                w = min(w, right-t)
                if w < .035:
                    break
                y0, y1 = REED_ROWS[rng.randrange(len(REED_ROWS))]
                x0 = rng.randrange(0, 1020)
                rect = (x0, y0, x0+220, y1)
                outline = [(0, .15)]
                # Five uneven reed points, not a sawtooth of identical triangles.
                for k in range(1, 11):
                    outline.append((k/11, rng.uniform(-.09, .015) if k % 2 else rng.uniform(.06, .17)))
                outline += [(1, .10), (1, .54), (1, 1), (0, 1), (0, .54)]
                points, texcoords = [], []
                for u, v in outline:
                    d, h = at(tail+(head-tail)*v)
                    # Raised lower bundles taper into the next overlapping course.
                    lift = .055+.09*(1-v)+.022*math.sin(u*math.pi)
                    points.append(pos(t+u*w, d, h+lift, side))
                    texcoords.append(uv(rect, u, max(0, v)))
                courses.piece(points, texcoords, .07)
                t += w-.018
    mat = G.material('Smithy packed reed courses v26', (.79, .78, .73, 1), 'thatch_v22', .45)
    return courses.mesh(name, mat)


def smithy():
    W.remove('Smithy ragged reed eave')
    old = bpy.data.objects['Smithy continuous thatched roof']
    fitted = roof_height(old)
    # The existing closed, cut-through deck remains as shadow beneath the bundles.
    old.data.materials.clear()
    old.data.materials.append(G.material('Packed reed underlap v26', (.38, .34, .27, 1), 'thatch_v22', .35))
    for face in old.data.polygons:
        face.material_index = 0
    lower_deck(old, .27)
    profiles = [[(-3.35, 3.65), (-2.75, 3.76), (-1.70, 4.05), (-.55, 4.95), (.90, 6.18)],
                [(3.90, 3.43), (3.52, 3.53), (2.75, 4.02), (1.72, 5.20), (.90, 6.18)]]

    def pos(x, z, y, side):
        y -= .12*(1-((x+.45)/4.2)**2)
        return x, max(y, fitted(x, z, y-.055)+.055), z

    reed_courses('Smithy overlapping reed bundles', -4.65, 3.75, profiles, pos, 726,
                 [(1.03, -.46, 2.07, .46), (-2.665, 1.985, -1.935, 2.615),
                  (-2.32, -1.815, -1.12, -.685)])
    # Keep the joined side gables, verge bindings and workshop architecture.
    for ob in bpy.data.objects:
        if ob.type == 'MESH' and ob.name.startswith('Smithy ridge spar'):
            for vertex in ob.data.vertices:
                p = ob.matrix_world@vertex.co
                p.z += .07
                vertex.co = ob.matrix_world.inverted()@p
    dormer = bpy.data.objects['Smithy dormer fitted roof']
    dormer_height = roof_height(dormer)
    dormer.data.materials.clear()
    dormer.data.materials.append(G.material('Packed reed underlap v26', (.38, .34, .27, 1), 'thatch_v22', .35))
    for face in dormer.data.polygons:
        face.material_index = 0
    lower_deck(dormer, .20)
    # Dormer ridge runs in depth; reed grain therefore runs across the building.
    profiles = [[(-.78, 5.30), (0, 6.34)], [(.78, 5.30), (0, 6.34)]]
    reed_courses('Smithy dormer reed bundles', -1.95, -.68, profiles,
                 lambda z, x, y, side: (-1.72+x, max(y, dormer_height(-1.72+x, z, y-.055)+.055), z), 1826)


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    for name in ['source', 'out', 'kit']:
        parser.add_argument('--'+name, type=Path, required=True)
    parser.add_argument('--resolution', type=int, default=900)
    parser.add_argument('--samples', type=int, default=16)
    args = parser.parse_args(sys.argv[sys.argv.index('--')+1:])
    W.run(args, refiners={'abbey': abbey, 'blacksmith': smithy}, detail='v26',
          design='v26: fitted slate courses, folded ridge caps and overlapping reed bundles')
