"""Render the complete private native roadside post without repainting it.

Blender: --python this_file -- --source <export> --native-tools <kit/tools>
--out <new private folder>. Unity lighting and effects are not reconstructed.
"""
import argparse
import json
import math
from pathlib import Path
import sys

import bpy


def render(source, native_tools, output):
    source, native_tools, output = [Path(p).resolve() for p in (source, native_tools, output)]
    repo = Path(__file__).resolve().parents[2]
    if any(p.is_relative_to(repo) for p in (source, output)) or output.exists():
        raise ValueError('Use private native assets and a new private render folder')
    doc = json.loads((source / 'scene.json').read_text())
    if len(doc['objects']) != 1 or doc['skipped'] or doc['objects'][0]['name'] != 'common_prop_lamppost':
        raise ValueError('Use the complete audited native post export')
    sys.path.insert(0, str(native_tools))
    import bl_lib as native
    native.reset()
    place, _ = native.place_matrix(source, (0, 0, 0), rot=0, respect_active=True)
    objects, lo, hi = native.build(source, place=place, respect_active=True)
    if sum(len(o.data.vertices) for o in objects) != 551 or sum(len(o.data.polygons) for o in objects) != 485:
        raise ValueError('Native post geometry is incomplete or changed')
    for obj in objects:
        for material in obj.data.materials:
            tree = material.node_tree
            output_node = next(n for n in tree.nodes if n.type == 'OUTPUT_MATERIAL')
            original = output_node.inputs['Surface'].links[0].from_socket
            geometry = tree.nodes.new('ShaderNodeNewGeometry')
            transparent = tree.nodes.new('ShaderNodeBsdfTransparent')
            mix = tree.nodes.new('ShaderNodeMixShader')
            tree.links.new(geometry.outputs['Backfacing'], mix.inputs[0])
            tree.links.new(original, mix.inputs[1])
            tree.links.new(transparent.outputs[0], mix.inputs[2])
            tree.links.new(mix.outputs[0], output_node.inputs['Surface'])
    center = (lo + hi) / 2
    extent = max(hi[0] - lo[0], hi[2] - lo[2]) * 1.18
    distance = max(12, extent * 2)
    native.camera((center[0], center[1] - distance, center[2] + distance * math.tan(math.radians(6))),
                  tuple(center), ortho=float(extent), res=(1024, 1024))
    bpy.context.scene.render.threads_mode = 'FIXED'
    bpy.context.scene.render.threads = 2
    output.mkdir(parents=True)
    native.render(output / 'post-0.png', samples=4, transparent=True)


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--source', type=Path, required=True)
    parser.add_argument('--native-tools', type=Path, required=True)
    parser.add_argument('--out', type=Path, required=True)
    args = parser.parse_args(sys.argv[sys.argv.index('--') + 1:])
    render(args.source, args.native_tools, args.out)
