"""Fit larger reviewed painted faces to existing physical Hamlet roof courses.

Blender authoring only. Private textures/slate_v41.png and clay_v41.png each hold
twelve reviewed faces in a four-column, three-row atlas. Preserve roof geometry,
underlaps, native art and wall/entrance work; re-import and review in the game.
"""
import argparse
import json
import sys
from pathlib import Path

import bpy
from mathutils import Vector

sys.path.insert(0, str(Path(__file__).resolve().parent))
import geometry as G
import roof_detail as R
import roof_joinery as J
import workshops as W


def fit(service):
    materials = {
        'Abbey individual weathered slate v26': ('slate', R.SLATES),
        'Service weathered slate v28': ('slate', R.SLATES),
        'Guild aged clay courses v28': ('clay', J.CLAY),
    }
    images = {}
    for kind in {kind for kind, _ in materials.values()}:
        path = G.OUT / 'textures' / (kind + '_v41.png')
        image = bpy.data.images.load(str(path), check_existing=True)
        assert tuple(image.size) == (1254, 1254)
        images[kind] = image
    matched = {}
    for material in bpy.data.materials:
        if material.name not in materials:
            continue
        kind, rectangles = materials[material.name]
        matched[material] = (kind, rectangles)
        nodes = [n for n in material.node_tree.nodes if n.type == 'TEX_IMAGE']
        assert 1 <= len(nodes) <= 2
        for node in nodes:
            node.image = images[kind]
        material.name = material.name.replace('v26', 'v41').replace('v28', 'v41')
    assert matched, service
    records = []
    for ob in bpy.data.objects:
        if ob.type != 'MESH':
            continue
        slots = {i: matched[m] for i, m in enumerate(ob.data.materials) if m in matched}
        if not slots:
            continue
        uv = ob.data.uv_layers.active
        counts = [0] * 12
        edge_count = 0
        for face in ob.data.polygons:
            if face.material_index not in slots:
                continue
            kind, rectangles = slots[face.material_index]
            points = [uv.data[i].uv.copy() for i in face.loop_indices]
            centre = sum(points, Vector((0., 0.))) / len(points)
            old = [(x0 / 1254, 1-y1 / 1254, x1 / 1254, 1-y0 / 1254)
                   for x0, y0, x1, y1 in rectangles]
            def distance(rect):
                return max(rect[0]-centre.x, 0, centre.x-rect[2])**2 + max(rect[1]-centre.y, 0, centre.y-rect[3])**2
            index = min(range(12), key=lambda i: distance(old[i]))
            assert distance(old[index]) < 1e-6, (ob.name, centre)
            x0, y0, x1, y1 = old[index]
            # Inset from every painted cell boundary. The physical mesh already
            # supplies its chipped silhouette and shadowed overlaps.
            col, row = index % 4, index // 4
            rect = ((col+.035)/4, 1-(row+.965)/3, (col+.965)/4, 1-(row+.035)/3)
            counts[index] += 1
            area = abs(sum(a.x*b.y-b.x*a.y for a,b in zip(points,points[1:]+points[:1]))) / 2
            if area < 1e-10 and service == 'abbey':
                # The Abbey's thin cut sides inherited a collapsed UV line.
                # Give these facets finite local paint and a dark cut-edge finish.
                edge = G.material('Abbey slate cut edges v41', (.20,.21,.23,1), 'slate_v41', .4)
                if edge not in ob.data.materials[:]:
                    ob.data.materials.append(edge)
                face.material_index = ob.data.materials[:].index(edge)
                normal = ob.matrix_world.to_3x3() @ face.normal
                tangent = Vector((-normal.y, normal.x, 0)).normalized()
                coords = [ob.matrix_world @ ob.data.vertices[ob.data.loops[i].vertex_index].co for i in face.loop_indices]
                middle = sum(coords, Vector()) / len(coords)
                for li, v in zip(face.loop_indices, coords):
                    delta = v-middle
                    u, w = .5+delta.dot(tangent)/.65, .5+delta.z/.65
                    uv.data[li].uv = (rect[0]+u*(rect[2]-rect[0]), rect[1]+w*(rect[3]-rect[1]))
                edge_count += 1
            else:
                for li, p in zip(face.loop_indices, points):
                    u, v = (p.x-x0)/(x1-x0), (p.y-y0)/(y1-y0)
                    assert -.02 <= u <= 1.02 and -.02 <= v <= 1.02
                    uv.data[li].uv = (rect[0]+u*(rect[2]-rect[0]), rect[1]+v*(rect[3]-rect[1]))
        records.append({'mesh': ob.name, 'faces_per_atlas_cell': counts, 'finite_cut_edge_faces': edge_count})
    (G.OUT / (service+'-roof-fit.json')).write_text(json.dumps(records, indent=2))


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    for name in ('source', 'out', 'kit'):
        parser.add_argument('--'+name, type=Path, required=True)
    parser.add_argument('--resolution', type=int, default=1100)
    parser.add_argument('--samples', type=int, default=16)
    args = parser.parse_args(sys.argv[sys.argv.index('--')+1:])
    W.run(args, refiners={k: lambda k=k: fit(k) for k in ('abbey','guild','sanitarium')},
          detail='v41', design='Large painted slate/clay faces on retained roof courses and finite Abbey cut edges')
