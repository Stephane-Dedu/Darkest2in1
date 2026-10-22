"""Bake private per-triangle architectural visibility without changing painted assets.

Run with Blender. Appends a scalar accessor referenced by primitive extras to the
runtime package. Original buffer bytes, geometry, UVs and texture files stay exact.
Only opaque, non-light surfaces occlude the short hemisphere rays. The runtime uses
its existing three material facets, so this does not add renderers or texture maps.
"""
import argparse
import hashlib
import json
import math
import shutil
import sys
import time
from pathlib import Path

import numpy as np
from mathutils import Matrix, Quaternion, Vector
from mathutils.bvhtree import BVHTree


def read_accessor(doc, buffers, index):
    a = doc['accessors'][index]
    if 'sparse' in a:
        raise ValueError('Sparse accessors are unsupported')
    view = doc['bufferViews'][a['bufferView']]
    dtype = {5121: '<u1', 5123: '<u2', 5125: '<u4', 5126: '<f4'}[a['componentType']]
    width = {'SCALAR': 1, 'VEC2': 2, 'VEC3': 3, 'VEC4': 4}[a['type']]
    size = np.dtype(dtype).itemsize
    offset = view.get('byteOffset', 0)+a.get('byteOffset', 0)
    return np.ndarray((a['count'], width), dtype=dtype,
                      buffer=buffers[view['buffer']], offset=offset,
                      strides=(view.get('byteStride', width*size), size)).copy()


def is_light(material):
    basis = material.get('pbrMetallicRoughness', {}).get('baseColorFactor', [1, 1, 1, 1])
    emission = material.get('emissiveFactor', [0, 0, 0])
    strength = material.get('extensions', {}).get('KHR_materials_emissive_strength', {}).get('emissiveStrength', 1)
    return max(basis[:3]) >= .001 and max(emission)*strength > max(basis[:3])*1.1


def planar_visibility(indices, points, shade):
    """Share area-weighted visibility over each connected flat surface.

    A broad wall must not gain a diagonal shadow merely from its triangulation.
    Keep folded faces and separate pieces independent, even when they touch.
    """
    parent = list(range(len(indices)))
    normals, areas, edges = [], [], {}

    def root(i):
        while parent[i] != i:
            parent[i] = parent[parent[i]]
            i = parent[i]
        return i

    for i, tri in enumerate(indices):
        a, b, c = [points[int(v)] for v in tri]
        cross = (b-a).cross(c-a)
        areas.append(cross.length/2)
        normals.append(cross.normalized())
        keys = [tuple(round(v, 6) for v in p) for p in (a, b, c)]
        for j in range(3):
            edge = tuple(sorted((keys[j], keys[(j+1) % 3])))
            for previous in edges.get(edge, []):
                if normals[i].dot(normals[previous]) > .999999:
                    parent[root(i)] = root(previous)
            edges.setdefault(edge, []).append(i)
    totals = {}
    for i, area in enumerate(areas):
        group = root(i)
        weighted, total = totals.get(group, (0, 0))
        totals[group] = (weighted+float(shade[i])*area, total+area)
    result = shade.copy()
    for i in range(len(indices)):
        weighted, total = totals[root(i)]
        if total > 1e-12:
            result[i] = weighted/total
    return result, len(totals)


def bake(folder, key, rays, radius):
    started = time.time()
    path = folder/(key+'.gltf')
    doc = json.loads(path.read_text())
    buffers = [bytearray((folder/b['uri']).read_bytes()) for b in doc['buffers']]
    nodes = doc['nodes']
    parents = {child: i for i, n in enumerate(nodes) for child in n.get('children', [])}
    matrices = {}

    def world(i):
        if i in matrices:
            return matrices[i]
        n = nodes[i]
        if 'matrix' in n:
            local = Matrix(np.array(n['matrix']).reshape(4, 4).T.tolist())
        else:
            q = n.get('rotation', [0, 0, 0, 1])
            local = Matrix.LocRotScale(Vector(n.get('translation', [0, 0, 0])),
                                       Quaternion((q[3], q[0], q[1], q[2])),
                                       Vector(n.get('scale', [1, 1, 1])))
        matrices[i] = world(parents[i])@local if i in parents else local
        return matrices[i]

    mesh_nodes = {}
    for i, n in enumerate(nodes):
        if 'mesh' in n:
            assert n['mesh'] not in mesh_nodes, 'Use the same single-instance contract as the runtime'
            mesh_nodes[n['mesh']] = i
    vertices, faces, records = [], [], []
    for mi, mesh in enumerate(doc['meshes']):
        matrix = world(mesh_nodes[mi])
        normal_matrix = matrix.to_3x3().inverted().transposed()
        for prim in mesh['primitives']:
            assert prim.get('mode', 4) == 4
            assert 'hamlet_contact_shade' not in prim.get('extras', {})
            attributes = prim['attributes']
            points = [matrix@Vector(v) for v in read_accessor(doc, buffers, attributes['POSITION'])]
            normals = [normal_matrix@Vector(n) for n in read_accessor(doc, buffers, attributes['NORMAL'])]
            indices = read_accessor(doc, buffers, prim['indices']).reshape(-1, 3)
            material = doc.get('materials', [])[prim['material']] if 'material' in prim else {}
            name = (mesh.get('name', '')+' '+nodes[mesh_nodes[mi]].get('name', '')).lower()
            # Native painted lamps/candles put their glow and cage in one albedo
            # atlas. Preserve their existing appearance as well as explicit lights.
            light = is_light(material) or any(token in name for token in ('native lamp', 'candle', 'flame'))
            if not light and material.get('alphaMode', 'OPAQUE') == 'OPAQUE':
                start = len(vertices)
                vertices.extend(points)
                faces.extend(tuple(int(i)+start for i in tri) for tri in indices)
            records.append((prim, points, normals, indices, light))
    tree = BVHTree.FromPolygons(vertices, faces, all_triangles=True, epsilon=0)
    directions = []
    for i in range(rays):
        z = math.sqrt((i+.5)/rays)
        a = i*math.pi*(3-math.sqrt(5))
        directions.append((math.sqrt(1-z*z)*math.cos(a), math.sqrt(1-z*z)*math.sin(a), z))
    offset = .006
    values, tested, planar_groups = [], 0, 0
    for prim, points, normals, indices, light in records:
        shade = np.ones(len(indices), dtype='<f4')
        if not light:
            for i, tri in enumerate(indices):
                a, b, c = [int(v) for v in tri]
                centre = (points[a]+points[b]+points[c])/3
                normal = (normals[a]+normals[b]+normals[c]).normalized()
                if normal.length < .5:
                    continue
                tangent = normal.cross(Vector((0, 0, 1) if abs(normal.z) < .9 else (1, 0, 0))).normalized()
                bitangent = normal.cross(tangent)
                origin = centre+normal*offset
                blocked = sum(tree.ray_cast(origin, tangent*x+bitangent*y+normal*z, radius)[0] is not None
                              for x, y, z in directions)
                shade[i] = 1-blocked/rays
                tested += 1
        shade, groups = planar_visibility(indices, points, shade)
        planar_groups += groups
        binary = buffers[0]
        binary.extend(b'\0'*((-len(binary)) % 4))
        start = len(binary)
        binary.extend(shade.tobytes())
        view = len(doc['bufferViews'])
        doc['bufferViews'].append({'buffer': 0, 'byteOffset': start, 'byteLength': shade.nbytes})
        accessor = len(doc['accessors'])
        doc['accessors'].append({'bufferView': view, 'componentType': 5126,
                                 'type': 'SCALAR', 'count': len(shade),
                                 'min': [float(shade.min())], 'max': [float(shade.max())]})
        prim.setdefault('extras', {})['hamlet_contact_shade'] = accessor
        values.extend(shade.tolist())
    doc['buffers'][0]['byteLength'] = len(buffers[0])
    (folder/doc['buffers'][0]['uri']).write_bytes(buffers[0])
    path.write_text(json.dumps(doc, separators=(',', ':')))
    record = {'triangles': len(values), 'tested': tested, 'planar_groups': planar_groups,
              'open': sum(v >= .75 for v in values), 'recess': sum(.4 <= v < .75 for v in values),
              'deep': sum(v < .4 for v in values), 'seconds': round(time.time()-started, 2)}
    print(key, record, flush=True)
    return record


def run(args):
    source, out = args.source.resolve(), args.out.resolve()
    repo = Path(__file__).resolve().parents[2]
    if out.exists() or out == repo or repo in out.parents:
        raise ValueError('Use a new private directory outside Git')
    manifest = json.loads((source/'manifest.json').read_text())
    sha = lambda p: hashlib.sha256(p.read_bytes()).hexdigest()
    assert all(Path(n).name == n and sha(source/n) == h for n, h in manifest['files'].items())
    assert set(args.models) <= set(manifest['models'])
    shutil.copytree(source, out)
    records = {key: bake(out, key, args.rays, args.radius) for key in args.models}
    manifest['files'] = {n: sha(out/n) for n in manifest['files']}
    (out/'manifest.json').write_text(json.dumps(manifest, indent=2))
    (out.parent/(out.name+'-contact.json')).write_text(json.dumps({
        'source': str(source), 'rays': args.rays, 'radius': args.radius,
        'geometry_and_UV_changes': False, 'new_textures': False, 'models': records}, indent=2))


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--source', required=True, type=Path)
    parser.add_argument('--out', required=True, type=Path)
    parser.add_argument('--models', nargs='+', required=True)
    parser.add_argument('--rays', type=int, default=16)
    parser.add_argument('--radius', type=float, default=1.0)
    args = parser.parse_args(sys.argv[sys.argv.index('--')+1:])
    if args.rays < 4 or args.rays > 128 or args.radius <= .01:
        raise ValueError('Use 4-128 rays and a positive contact radius')
    run(args)
