"""Fit reviewed service wall paintings to the accepted Hamlet architecture.

Blender authoring only. Paint inputs are private and named <service>_wall_v40.png
under the output textures directory. Native atlases, geometry and fitted stone
trim remain intact. UV changes are limited to three Sanitarium wall meshes.
Re-import and compare with native art before acceptance.
"""
import argparse
import hashlib
import json
import math
import sys
from pathlib import Path

import bpy

sys.path.insert(0, str(Path(__file__).resolve().parent))
import geometry as G
import workshops as W


def wall_paint(service):
    path = G.OUT / 'textures' / (service + '_wall_v40.png')
    image = bpy.data.images.load(str(path), check_existing=True)
    assert min(image.size) >= 1024, 'Review a full resolution painting'
    changed = []
    prefixes = ('Hamlet ' + service + ' wall v20', 'Hamlet smoke stained lime')
    if service == 'sanitarium':
        prefixes += ('Sanitarium fitted lime v25', 'Sanitarium tower uninterrupted lime v25')
    for material in bpy.data.materials:
        # Only authored structural wall material. Fitted trim and native mixed
        # atlases deliberately keep their existing paint and UVs.
        if not material.name.startswith(prefixes):
            continue
        nodes = [n for n in material.node_tree.nodes if n.type == 'TEX_IMAGE']
        # glTF imports can separate albedo and painted emission image nodes.
        assert 1 <= len(nodes) <= 2
        assert all(n.image and tuple(n.image.size) == (1254, 1254) for n in nodes)
        for node in nodes:
            node.image = image
        changed.append(material.name)
        material.name = material.name.replace('v20', 'v40').replace('v25', 'v40')
    if service == 'sanitarium':
        # Map one whole painting around the tower, seam on the house side.
        # The previous 1.35 repeats and vertical period could duplicate damage.
        tower = bpy.data.objects['Sanitarium round stair tower']
        points = [tower.matrix_world @ v.co for v in tower.data.vertices]
        low, high = min(v.z for v in points), max(v.z for v in points)
        for face in tower.data.polygons:
            points = [tower.matrix_world @ tower.data.vertices[tower.data.loops[i].vertex_index].co
                      for i in face.loop_indices]
            us = [(math.atan2(v.y + 2, v.x + 3) / math.tau) % 1 for v in points]
            if max(us) - min(us) > .5:
                us = [u + 1 if u < .5 else u for u in us]
            for li, v, u in zip(face.loop_indices, points, us):
                tower.data.uv_layers.active.data[li].uv = (.02 + .96 * u, .02 + .96 * (v.z - low) / (high - low))
        paving = next(m for m in bpy.data.materials if m.name.startswith('Service worn native paving v24'))
        for name in ('Sanitarium house stone undercroft', 'Sanitarium stone porch'):
            ob = bpy.data.objects[name]
            points = [ob.matrix_world @ v.co for v in ob.data.vertices]
            low = [min(v[k] for v in points) for k in range(3)]
            span = [max(v[k] for v in points) - low[k] for k in range(3)]
            if name.endswith('undercroft'):
                ob.data.materials.append(paving)
            for face in ob.data.polygons:
                n = ob.matrix_world.to_3x3() @ face.normal
                terrace = name.endswith('undercroft') and n.z > .95
                if terrace:
                    face.material_index = len(ob.data.materials) - 1
                elif abs(n.z) > .7:
                    continue
                axis = 0 if abs(n.y) >= abs(n.x) else 1
                for li in face.loop_indices:
                    v = ob.matrix_world @ ob.data.vertices[ob.data.loops[li].vertex_index].co
                    ob.data.uv_layers.active.data[li].uv = (v.x / 3, v.y / 3) if terrace else (
                        .02 + .96 * (v[axis] - low[axis]) / span[axis],
                        .02 + .96 * (v.z - low[2]) / span[2])
    assert changed, 'No authored wall materials matched ' + service
    (G.OUT / (service + '-paint-fit.json')).write_text(json.dumps({
        'source': str(path), 'sha256': hashlib.sha256(path.read_bytes()).hexdigest(),
        'size': list(image.size), 'replaced_materials': changed,
        'geometry_native_and_trim_retained': True,
        'wall_uvs_retained_except': ['Sanitarium round stair tower', 'Sanitarium house stone undercroft',
                                   'Sanitarium stone porch'] if service == 'sanitarium' else [],
        'undercroft_top_reuses_native_paving': service == 'sanitarium',
    }, indent=2))


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    for name in ['source', 'out', 'kit']:
        parser.add_argument('--' + name, type=Path, required=True)
    parser.add_argument('--services', default='abbey,guild,sanitarium')
    parser.add_argument('--resolution', type=int, default=1100)
    parser.add_argument('--samples', type=int, default=16)
    args = parser.parse_args(sys.argv[sys.argv.index('--') + 1:])
    services = args.services.split(',')
    assert len(set(services)) == len(services)
    assert set(services) <= {'abbey', 'guild', 'sanitarium'}
    W.run(args, refiners={key: lambda key=key: wall_paint(key) for key in services},
          detail='v40', design='Reviewed broad-facet wall painting; fitted architecture and shared palette retained')
