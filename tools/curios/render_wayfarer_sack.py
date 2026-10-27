"""Render a complete native sack, then adapt only its missing opened mouth.

The native stage runs in Blender with --native-tools, --source and --out.
The assembly stage runs in ordinary Python with --spec and --out. All native
and generated art stays private. The open mouth is a raster adaptation;
native open-mouth geometry and the full Unity shader are not reconstructed.
"""
from pathlib import Path
import argparse
import hashlib
import json
import math
import sys


def native(args):
    import bpy
    import numpy as np
    source, output = args.source.resolve(), args.out.resolve()
    repo = Path(__file__).resolve().parents[2]
    if source.is_relative_to(repo) or output.is_relative_to(repo) or output.exists():
        raise ValueError('Use private source and a new private output')
    digest = lambda p: hashlib.sha256(p.read_bytes()).hexdigest()
    if digest(source / 'scene.json') != '1994222d99e0b340adc5961021b2bb9d2f9115917cd3ffcfadd231e6c18e7378' \
            or digest(source / 'mesh-6.npz') != 'd107e5af218e034274faa887a0e000f6cc35e56255de34e047f5e984fd699416':
        raise ValueError('Complete reviewed source changed')
    doc = json.loads((source / 'scene.json').read_text())
    if len(doc['objects']) != 1 or doc['objects'][0]['name'] != 'inn_embark_sack(Clone)':
        raise ValueError('Expected the complete native sack renderer')
    material = next(iter(doc['materials'].values()))
    texture = Path(material['texture'])
    if digest(texture) != '46e2121320c040736892766abbac2dac57ae424f828f5bcd871e7712addfa8b8':
        raise ValueError('Full native paint changed')
    sys.path.insert(0, str(args.native_tools.resolve()))
    import bl_lib as library
    library.reset()
    place, _ = library.place_matrix(source, (0, 0, 0), rot=0, respect_active=True)
    objects, low, high = library.build(source, place=place, respect_active=True)
    if len(objects) != 1 or (len(objects[0].data.vertices), len(objects[0].data.polygons)) != (475, 640):
        raise ValueError('Complete native geometry changed')
    for mat in objects[0].data.materials:
        nodes = mat.node_tree
        final = next(n for n in nodes.nodes if n.type == 'OUTPUT_MATERIAL')
        surface = final.inputs['Surface'].links[0].from_socket
        geometry = nodes.nodes.new('ShaderNodeNewGeometry')
        transparent = nodes.nodes.new('ShaderNodeBsdfTransparent')
        mix = nodes.nodes.new('ShaderNodeMixShader')
        nodes.links.new(geometry.outputs['Backfacing'], mix.inputs[0])
        nodes.links.new(surface, mix.inputs[1])
        nodes.links.new(transparent.outputs[0], mix.inputs[2])
        nodes.links.new(mix.outputs[0], final.inputs['Surface'])
    center = (low + high) / 2
    extent = max(high[0] - low[0], high[2] - low[2]) * 1.18
    distance = max(10, extent * 2)
    library.camera((center[0], center[1] - distance,
                    center[2] + distance * math.tan(math.radians(6))),
                   tuple(center), ortho=float(extent), res=(768, 768))
    bpy.context.scene.render.threads_mode = 'FIXED'
    bpy.context.scene.render.threads = 2
    bpy.context.scene.cycles.seed = 0
    output.mkdir(parents=True)
    library.render(output / 'native-reference.png', samples=4, transparent=True)
    record = {'source': str(source), 'vertices': 475, 'triangles': 640,
              'complete_geometry_UVs_paint_tint_retained': True,
              'Cull2_opaque_modeled_not_full_shader': True,
              'native_reference_sha256': digest(output / 'native-reference.png'),
              'source_files': {str(p): digest(p) for p in
                               (source / 'scene.json', source / 'mesh-6.npz', texture)}}
    (output / 'provenance.json').write_text(json.dumps(record, indent=2))


def assemble(args):
    import numpy as np
    from PIL import Image
    from compose_state_patch import compose
    spec_path, output = args.spec.resolve(), args.out.resolve()
    repo = Path(__file__).resolve().parents[2]
    spec = json.loads(spec_path.read_text())
    paths = {key: (spec_path.parent / spec[key]).resolve() for key in
             ('native_reference', 'generated_mouth_edit', 'state_mask')}
    if output.is_relative_to(repo) or output.exists() or spec_path.is_relative_to(repo) \
            or any(p.is_relative_to(repo) for p in paths.values()):
        raise ValueError('Use private inputs and a new private output')
    closed = Image.open(paths['native_reference'])
    edited = Image.open(paths['generated_mouth_edit'])
    if closed.mode != 'RGBA' or edited.mode != 'RGBA':
        raise ValueError('Preserve original RGBA')
    crop, size, offset = [tuple(spec[k]) for k in ('component_crop', 'component_size', 'component_offset')]
    if len(crop) != 4 or len(size) != 2 or len(offset) != 2 \
            or not (0 <= crop[0] < crop[2] <= edited.width and 0 <= crop[1] < crop[3] <= edited.height) \
            or min(size) <= 0 or min(offset) < 0 \
            or offset[0] + size[0] > closed.width or offset[1] + size[1] > closed.height \
            or abs(size[0] / (crop[2] - crop[0]) - size[1] / (crop[3] - crop[1])) > .001:
        raise ValueError('Review explicit uniformly fitted mouth placement')
    part = edited.crop(crop).resize(size, Image.Resampling.LANCZOS)
    fade_rows = spec['bottom_alpha_fade_rows']
    if not 1 <= fade_rows <= size[1]:
        raise ValueError('Invalid local collar transition')
    pixels = np.asarray(part).copy()
    fade = np.clip((size[1] - 1 - np.arange(size[1])) / fade_rows, 0, 1)
    pixels[:, :, 3] = np.rint(pixels[:, :, 3] * fade[:, None]).astype('uint8')
    used = closed.copy()
    used.paste((0, 0, 0, 0), tuple(spec['clear_old_neck']))
    used.alpha_composite(Image.fromarray(pixels), offset)
    protected = spec['fixed_native_rows_from']
    if not np.array_equal(np.asarray(closed)[protected:], np.asarray(used)[protected:]):
        raise ValueError('Opened mouth changed the fixed lower native body')
    output.mkdir(parents=True)
    used.save(output / 'local-mouth-edit.png')
    record = compose(paths['native_reference'], output / 'local-mouth-edit.png',
                     paths['state_mask'], output / 'pair')
    record.update({'component_crop': crop, 'component_size': size, 'component_offset': offset,
                   'bottom_alpha_fade_rows': fade_rows, 'fixed_native_rows_from': protected,
                   'fixed_native_lower_body_exact': True, 'native_open_geometry_claim': False,
                   'whole_generated_edit_used': False, 'spec_sha256': hashlib.sha256(spec_path.read_bytes()).hexdigest(),
                   'public_assembler_sha256': hashlib.sha256(Path(__file__).read_bytes()).hexdigest()})
    (output / 'provenance.json').write_text(json.dumps(record, indent=2))


if __name__ == '__main__':
    argv = sys.argv[sys.argv.index('--') + 1:] if '--' in sys.argv else sys.argv[1:]
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('stage', choices=('native', 'assemble'))
    parser.add_argument('--source', type=Path)
    parser.add_argument('--native-tools', type=Path)
    parser.add_argument('--spec', type=Path)
    parser.add_argument('--out', type=Path, required=True)
    args = parser.parse_args(argv)
    (native if args.stage == 'native' else assemble)(args)
