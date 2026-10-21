"""Refine the accepted v26 Smithy roof underlay and cut-edge paint in Blender.

No new images or native mesh edits. Nine other service GLBs remain byte-identical.
"""
import argparse
import sys
from pathlib import Path

import bpy
from mathutils import Vector

sys.path.insert(0, str(Path(__file__).resolve().parent))
import workshops as W


def raise_underlay(name, amount):
    ob = bpy.data.objects[name]
    transform = ob.matrix_world
    inverse = transform.inverted()
    for vertex in ob.data.vertices:
        point = transform@vertex.co
        point.z += amount
        vertex.co = inverse@point


def reed_edges(name):
    ob = bpy.data.objects[name]
    uv = ob.data.uv_layers.active
    points = [ob.matrix_world@v.co for v in ob.data.vertices]
    # Every extruded edge has a top/bottom pair at the same horizontal position.
    # Export splits vertices at UV seams, so group by position, not vertex index.
    columns = {}
    key = lambda p: (round(p.x, 5), round(p.y, 5))
    for point in points:
        columns[key(point)] = max(columns.get(key(point), point.z), point.z)
    changed = 0
    for face in ob.data.polygons:
        normal = ob.matrix_world.to_3x3()@face.normal
        if abs(normal.z) > .08:
            continue  # Preserve the complete reviewed painting on upper surfaces.
        coordinates = [uv.data[li].uv.copy() for li in face.loop_indices]
        a, b = max(((a, b) for a in coordinates for b in coordinates),
                   key=lambda pair: (pair[1]-pair[0]).length_squared)
        edge = b-a
        if edge.length < .000001:
            continue
        if edge.x < 0 or (abs(edge.x) < .000001 and edge.y < 0):
            edge.negate()
        edge.normalize()
        # Offset across the sampled grain, including edges whose U is constant.
        across = type(edge)((-edge.y, edge.x))
        for li in face.loop_indices:
            point = points[ob.data.loops[li].vertex_index]
            depth = min(.07, max(0, columns[key(point)]-point.z))
            uv.data[li].uv -= across*(depth*.26)
        coordinates = [uv.data[li].uv.copy() for li in face.loop_indices]
        a, b, c = coordinates[:3]
        area = abs((b.x-a.x)*(c.y-a.y)-(b.y-a.y)*(c.x-a.x))*.5
        if area < 1e-9 and face.area > 1e-8:
            # Triangulating a bent bundle also makes thin vertical end-cap
            # triangles entirely from top vertices; these have no depth pair.
            tangent = Vector((-normal.y, normal.x, 0)).normalized()
            centre = sum((points[ob.data.loops[li].vertex_index] for li in face.loop_indices), Vector())/len(face.loop_indices)
            paint = sum(coordinates, Vector((0., 0.)))/len(coordinates)
            for li in face.loop_indices:
                delta = points[ob.data.loops[li].vertex_index]-centre
                uv.data[li].uv = (paint.x+delta.dot(tangent)*.14, paint.y+delta.z*.14)
        changed += 1
    assert changed > 50, 'Expected physical reed side faces.'
    print('REED_EDGE_UVS', name, changed, flush=True)


def smithy():
    # Surface fitting introduced in v26 makes the old large collision offset
    # unnecessary. Keep a small clearance beneath the overlapping closed bundles.
    raise_underlay('Smithy continuous thatched roof', .22)
    raise_underlay('Smithy dormer fitted roof', .16)
    reed_edges('Smithy overlapping reed bundles')
    reed_edges('Smithy dormer reed bundles')


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    for name in ['source', 'out', 'kit']:
        parser.add_argument('--'+name, type=Path, required=True)
    parser.add_argument('--resolution', type=int, default=900)
    parser.add_argument('--samples', type=int, default=16)
    args = parser.parse_args(sys.argv[sys.argv.index('--')+1:])
    W.run(args, refiners={'blacksmith': smithy}, detail='v27',
          design='v27: closely fitted thatch underlay and non-collapsed reed edge UVs')
