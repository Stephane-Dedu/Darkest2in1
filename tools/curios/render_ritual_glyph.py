"""Render a complete private native Coven glyph card with its smooth alpha.

Blender: --python this_file -- --source <export> --native-tools <kit/tools>
--out <new private folder>. This models native Cull2/transparent base paint,
not Unity colour controls, lighting, fog, depth fading or distortion.
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
        raise ValueError('Use private native assets and a new private output')
    doc = json.loads((source / 'scene.json').read_text())
    if len(doc['objects']) != 1 or doc['skipped'] or doc['objects'][0]['name'] != 'coven_node_glyph':
        raise ValueError('Use the complete audited native glyph export')
    sys.path.insert(0, str(native_tools))
    import bl_lib as native
    native.reset()
    place, _ = native.place_matrix(source, (0, 0, 0), rot=180, respect_active=True)
    objects, lo, hi = native.build(source, place=place, respect_active=True)
    if sum(len(o.data.vertices) for o in objects) != 4 or sum(len(o.data.polygons) for o in objects) != 2:
        raise ValueError('Native glyph geometry is incomplete or changed')
    for obj in objects:
        for material in obj.data.materials:
            tree = material.node_tree
            out = next(n for n in tree.nodes if n.type == 'OUTPUT_MATERIAL')
            emission = next(n for n in tree.nodes if n.type == 'EMISSION')
            texture = next(n for n in tree.nodes if n.type == 'TEX_IMAGE')
            geometry = tree.nodes.new('ShaderNodeNewGeometry')
            front = tree.nodes.new('ShaderNodeMath')
            front.operation = 'SUBTRACT'
            front.inputs[0].default_value = 1
            tree.links.new(geometry.outputs['Backfacing'], front.inputs[1])
            alpha = tree.nodes.new('ShaderNodeMath')
            alpha.operation = 'MULTIPLY'
            tree.links.new(texture.outputs['Alpha'], alpha.inputs[0])
            tree.links.new(front.outputs[0], alpha.inputs[1])
            transparent = tree.nodes.new('ShaderNodeBsdfTransparent')
            mix = tree.nodes.new('ShaderNodeMixShader')
            tree.links.new(alpha.outputs[0], mix.inputs[0])
            tree.links.new(transparent.outputs[0], mix.inputs[1])
            tree.links.new(emission.outputs[0], mix.inputs[2])
            tree.links.new(mix.outputs[0], out.inputs['Surface'])
    center = (lo + hi) / 2
    extent = max(hi[0] - lo[0], hi[2] - lo[2]) * 1.18
    distance = max(12, extent * 2)
    native.camera((center[0], center[1] - distance, center[2] + distance * math.tan(math.radians(6))),
                  tuple(center), ortho=float(extent), res=(720, 720))
    bpy.context.scene.render.threads_mode = 'FIXED'
    bpy.context.scene.render.threads = 2
    output.mkdir(parents=True)
    native.render(output / 'glyph-native-alpha.png', samples=4, transparent=True)


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--source', type=Path, required=True)
    parser.add_argument('--native-tools', type=Path, required=True)
    parser.add_argument('--out', type=Path, required=True)
    args = parser.parse_args(sys.argv[sys.argv.index('--') + 1:])
    render(args.source, args.native_tools, args.out)
