"""Render an anchored Tangle curio web from complete native DD2 components.

Retarget every native web face through four painted frame attachments while
preserving its UVs and source paint. The complete frame stays fixed. Adapt web
tint and visibility for a readable interactive object; mask only its centre
after use. Render-only ground occlusion buries native skirts. No generation.
Keep all native assets, anchor data and rendered output outside the repository.
"""
from pathlib import Path
import argparse, json, math, sys, hashlib
import bpy
import numpy as np
from bpy_extras.object_utils import world_to_camera_view
from mathutils import Vector

def validate_anchor(data, record):
    face = int(record['face_ids'])
    weights = np.array(record['barycentric'], dtype=float)
    if not 0 <= face < len(data['tri']) or weights.shape != (3,) or (not np.isfinite(weights).all()) or (weights < 0).any() or (abs(weights.sum() - 1) > 1e-06):
        raise ValueError('Invalid painted attachment')
    indices = data['tri'][face]
    if not np.allclose(weights @ data['v'][indices], record['positions'], atol=1e-06, rtol=0) or not np.allclose(weights @ data['uv'][indices], record['uv'], atol=1e-06, rtol=0):
        raise ValueError('Painted attachment no longer matches native geometry/UVs')

def main():
    parser = argparse.ArgumentParser(description=__doc__)
    for key in ('kit', 'frame', 'web', 'anchors', 'out'):
        parser.add_argument('--' + key, type=Path, required=True)
    args = parser.parse_args(sys.argv[sys.argv.index('--') + 1:])
    frame, web, out = (args.frame.resolve(), args.web.resolve(), args.out.resolve())
    repo = Path(__file__).resolve().parents[2]
    if any((path.is_relative_to(repo) for path in (frame, web, args.anchors.resolve(), out))) or out.exists():
        raise ValueError('Use private inputs and a new output folder outside Git')
    sys.path.insert(0, str(args.kit.resolve() / 'tools'))
    import bl_lib as L
    fdoc = json.loads((frame / 'scene.json').read_text())
    tree = next((o for o in fdoc['objects'] if o['name'] == 'barricade_coven_trees'))
    fv = np.load(frame / ('mesh-' + tree['mesh'] + '.npz'))['v']
    wdoc = json.loads((web / 'scene.json').read_text())
    wd = np.load(web / ('mesh-' + wdoc['objects'][0]['mesh'] + '.npz'))
    v, uv, tri = (wd['v'], wd['uv'], wd['tri'])
    if not fdoc['source'].endswith('/barricade_coven_trees_02.fbx') or len(fdoc['objects']) != 2 or fdoc.get('skipped'):
        raise ValueError('Use the complete reviewed native frame')
    if not wdoc['source'].endswith('/common_cobweb_02.fbx') or len(wdoc['objects']) != 1 or wdoc.get('skipped'):
        raise ValueError('Use the complete reviewed native web')
    if (len(v), len(tri)) != (111, 92):
        raise ValueError('Native web topology changed')
    frame_v = frame_t = 0
    for obj in fdoc['objects']:
        if not np.array_equal(np.array(obj['world']), np.eye(4)):
            raise ValueError('Native frame relative pose changed')
        data = np.load(frame / ('mesh-' + obj['mesh'] + '.npz'))
        frame_v += len(data['v'])
        frame_t += len(data['tri'])
    if (frame_v, frame_t) != (2809, 2554):
        raise ValueError('Native frame topology changed')
    anchors = json.loads(args.anchors.read_text())
    source_ids = anchors['source']
    frame_ids = anchors['targets']
    if len(source_ids) != 4 or len(frame_ids) != 4:
        raise ValueError('Exactly four reviewed painted attachments required')
    for record in source_ids:
        validate_anchor(wd, record)
    for record in frame_ids:
        item = next((o for o in fdoc['objects'] if o['name'] == record['object']))
        validate_anchor(np.load(frame / ('mesh-' + item['mesh'] + '.npz')), record)
    s = np.array([a['positions'] for a in source_ids])
    t = np.array([a['positions'] for a in frame_ids])
    t[:, 2] += 0.025
    A = []
    b = []
    for (x, y, _), (u, w, _) in zip(s, t):
        A.extend([[x, y, 1, 0, 0, 0, -u * x, -u * y], [0, 0, 0, x, y, 1, -w * x, -w * y]])
        b.extend([u, w])
    H = np.r_[np.linalg.solve(A, b), 1].reshape(3, 3)
    q = np.c_[v[:, :2], np.ones(len(v))] @ H.T
    assert (q[:, 2] > 0).all()
    mapped = np.zeros_like(v)
    mapped[:, :2] = q[:, :2] / q[:, 2, None]
    basis = lambda p: np.c_[p[:, 0], p[:, 1], p[:, 0] * p[:, 1], np.ones(len(p))]
    z_target = basis(v) @ np.linalg.solve(basis(s), t[:, 2])
    z_source = basis(v) @ np.linalg.solve(basis(s), s[:, 2])
    mapped[:, 2] = z_target + (v[:, 2] - z_source) * 0.15
    B = np.zeros((4, len(v)))
    for row, record in enumerate(source_ids):
        B[row, tri[record['face_ids']]] = record['barycentric']
    residual = t - B @ mapped
    correction = B.T @ np.linalg.solve(B @ B.T, residual)
    mapped += correction.astype(np.float32)
    assert np.max(np.abs(B @ mapped - t)) < 1e-05
    source_area = np.cross(v[tri[:, 1], :2] - v[tri[:, 0], :2], v[tri[:, 2], :2] - v[tri[:, 0], :2])
    placed_area = np.cross(mapped[tri[:, 1], :2] - mapped[tri[:, 0], :2], mapped[tri[:, 2], :2] - mapped[tri[:, 0], :2])
    folds = int(((np.abs(source_area) > 1e-08) & (source_area * placed_area < 0)).sum())
    if folds:
        raise ValueError('Painted attachment retargeting folded native faces')
    L.reset()
    place, _ = L.place_matrix(frame, (0, 0, 0), respect_active=True)
    world_web = (np.c_[mapped, np.ones(len(mapped))] @ (L.CHANGE @ place).T)[:, :3]
    L.reset()
    _, lo, hi = L.build(frame, place=place, respect_active=True)
    lo = np.minimum(lo, world_web.min(0))
    hi = np.maximum(hi, world_web.max(0))
    center = (lo + hi) / 2
    extent = max(hi[0] - lo[0], hi[2] - lo[2]) * 1.15
    distance = max(12, extent * 2)
    out.mkdir(parents=True)
    records = []
    for state in ('frame-reference', 'native-tint-reference', 'closed', 'used'):
        L.reset()
        objects, _, _ = L.build(frame, place=place, respect_active=True)
        assert len(objects) == 2 and sum((len(o.data.polygons) for o in objects)) == 2554
        for o in objects:
            for material in o.data.materials:
                for node in material.node_tree.nodes:
                    if node.bl_idname == 'ShaderNodeMath' and node.operation == 'GREATER_THAN':
                        node.inputs[1].default_value = 0.5
        if state != 'frame-reference':
            made, _, _ = L.build(web, respect_active=True)
            assert len(made) == 1 and len(made[0].data.polygons) == 92
            obj = made[0]
            ids = np.unique(tri)
            obj.data.vertices.foreach_set('co', world_web[ids].astype(np.float32).reshape(-1))
            mat = obj.data.materials[0]
            nt = mat.node_tree
            for node in nt.nodes:
                if node.bl_idname == 'ShaderNodeMath' and node.operation == 'GREATER_THAN':
                    node.inputs[1].default_value = 0.24699999392032623
                if node.bl_idname == 'ShaderNodeMixRGB' and node.blend_type == 'MULTIPLY':
                    node.inputs[2].default_value = (0, 0, 0, 1) if state == 'native-tint-reference' else (0.38, 0.46, 0.4, 1)
            mix = next((n for n in nt.nodes if n.bl_idname == 'ShaderNodeMixShader'))
            base_alpha = mix.inputs[0].links[0].from_socket
            tc = next((n for n in nt.nodes if n.bl_idname == 'ShaderNodeTexCoord'))
            separate = nt.nodes.new('ShaderNodeSeparateXYZ')
            nt.links.new(tc.outputs['UV'], separate.inputs[0])
            quad = np.array([source_ids[i]['uv'] for i in (0, 2, 3, 1)])
            area = sum((quad[i, 0] * quad[(i + 1) % 4, 1] - quad[(i + 1) % 4, 0] * quad[i, 1] for i in range(4)))
            orientation = np.sign(area)
            inside = base_alpha
            for j, a in enumerate(quad):
                edge = quad[(j + 1) % 4] - a
                dx = nt.nodes.new('ShaderNodeMath')
                dx.operation = 'SUBTRACT'
                dx.inputs[1].default_value = float(a[0])
                nt.links.new(separate.outputs['X'], dx.inputs[0])
                dy = nt.nodes.new('ShaderNodeMath')
                dy.operation = 'SUBTRACT'
                dy.inputs[1].default_value = float(a[1])
                nt.links.new(separate.outputs['Y'], dy.inputs[0])
                px = nt.nodes.new('ShaderNodeMath')
                px.operation = 'MULTIPLY'
                px.inputs[1].default_value = float(edge[1] * orientation)
                nt.links.new(dx.outputs[0], px.inputs[0])
                py = nt.nodes.new('ShaderNodeMath')
                py.operation = 'MULTIPLY'
                py.inputs[1].default_value = float(edge[0] * orientation)
                nt.links.new(dy.outputs[0], py.inputs[0])
                cross = nt.nodes.new('ShaderNodeMath')
                cross.operation = 'SUBTRACT'
                nt.links.new(py.outputs[0], cross.inputs[0])
                nt.links.new(px.outputs[0], cross.inputs[1])
                gate = nt.nodes.new('ShaderNodeMath')
                gate.operation = 'GREATER_THAN'
                gate.inputs[1].default_value = float(-0.001 * np.linalg.norm(edge))
                nt.links.new(cross.outputs[0], gate.inputs[0])
                combine = nt.nodes.new('ShaderNodeMath')
                combine.operation = 'MULTIPLY'
                nt.links.new(inside, combine.inputs[0])
                nt.links.new(gate.outputs[0], combine.inputs[1])
                inside = combine.outputs[0]
            nt.links.new(inside, mix.inputs[0])
            if state == 'used':
                mix = next((n for n in nt.nodes if n.bl_idname == 'ShaderNodeMixShader'))
                original = mix.inputs[0].links[0].from_socket
                texcoord = next((n for n in nt.nodes if n.bl_idname == 'ShaderNodeTexCoord'))
                sub = nt.nodes.new('ShaderNodeVectorMath')
                sub.operation = 'SUBTRACT'
                sub.inputs[1].default_value = (0.772, 0.54, 0)
                nt.links.new(texcoord.outputs['UV'], sub.inputs[0])
                div = nt.nodes.new('ShaderNodeVectorMath')
                div.operation = 'DIVIDE'
                div.inputs[1].default_value = (0.06, 0.07, 1)
                nt.links.new(sub.outputs['Vector'], div.inputs[0])
                length = nt.nodes.new('ShaderNodeVectorMath')
                length.operation = 'LENGTH'
                nt.links.new(div.outputs['Vector'], length.inputs[0])
                mask = nt.nodes.new('ShaderNodeMath')
                mask.operation = 'GREATER_THAN'
                mask.inputs[1].default_value = 1
                nt.links.new(length.outputs['Value'], mask.inputs[0])
                mul = nt.nodes.new('ShaderNodeMath')
                mul.operation = 'MULTIPLY'
                nt.links.new(original, mul.inputs[0])
                nt.links.new(mask.outputs[0], mul.inputs[1])
                nt.links.new(mul.outputs[0], mix.inputs[0])
        ground_z = float((L.CHANGE @ place @ np.array([0, 1.0, 0, 1]))[2])
        bpy.ops.mesh.primitive_plane_add(size=80, location=(0, 0, ground_z))
        floor = bpy.context.object
        floor.name = 'RenderOnlyGroundOccluder'
        material = bpy.data.materials.new('GroundHoldout')
        material.use_nodes = True
        nt = material.node_tree
        nt.nodes.clear()
        hold = nt.nodes.new('ShaderNodeHoldout')
        output = nt.nodes.new('ShaderNodeOutputMaterial')
        nt.links.new(hold.outputs[0], output.inputs['Surface'])
        floor.data.materials.append(material)
        cam = L.camera((center[0], center[1] - distance, center[2] + distance * math.tan(math.radians(6))), tuple(center), ortho=float(extent), res=(1024, 1024))
        scene = bpy.context.scene
        scene.render.threads_mode = 'FIXED'
        scene.render.threads = 2
        scene.cycles.seed = 0
        path = out / (state + '.png')
        L.render(path, samples=4, transparent=True)
        if state == 'frame-reference':
            im = bpy.data.images.load(str(path), check_existing=False)
            pixels = np.array(im.pixels[:]).reshape(1024, 1024, 4)
            ys, xs = np.where(np.flipud(pixels[:, :, 3]) > 0.5)
            low = np.array([xs.min(), ys.min()], dtype=float)
            high = np.array([xs.max() + 1, ys.max() + 1], dtype=float)
        records.append({'state': state, 'file': path.name, 'sha256': hashlib.sha256(path.read_bytes()).hexdigest(), 'foot': [(low[0] + high[0]) / 2, high[1]], 'body_landmark_y': low[1]})
    dependencies = [args.anchors.resolve()]
    for folder, doc in ((frame, fdoc), (web, wdoc)):
        dependencies.append(folder / 'scene.json')
        dependencies += [folder / ('mesh-' + key + '.npz') for key in doc['meshes']]
        dependencies += [(folder / m['texture']).resolve() for m in doc['materials'].values() if m.get('texture')]
    (out / 'provenance.json').write_text(json.dumps({'art_id': 'cosmic_spiderweb_tangle', 'curio_id': 'cosmic_spiderweb', 'region': 'dd2_forest', 'source_web_vertices': 111, 'source_web_triangles': 92, 'frame_vertices': 2809, 'frame_triangles': 2554, 'painted_source_anchors': source_ids, 'painted_frame_anchors': frame_ids, 'frame_anchor_positions': t.tolist(), 'homography_xy': H.tolist(), 'source_web_geometry_retargeted': True, 'max_anchor_correction': float(np.linalg.norm(correction, axis=1).max()), 'painted_anchor_max_error': float(np.max(np.abs(B @ mapped - t))), 'all_faces_UVs_and_native_paint_retained': True, 'frame_geometry_paint_tint_and_pose_fixed': True, 'native_world_ground_height': 1.0, 'ground_occlusion_render_only': True, 'web_native_tint': [0, 0, 0, 1], 'web_adapted_tint': [0.38, 0.46, 0.4, 1], 'web_cutoff': 0.24699999392032623, 'frame_cutoff': 0.5, 'used_mask_uv_center': [0.772, 0.54], 'used_mask_uv_radii': [0.06, 0.07], 'used_changes_shader_visibility_only': True, 'support_domain_uv': [[source_ids[i]['uv'] for i in (0, 2, 3, 1)]], 'support_domain_epsilon_uv': 0.001, 'unsupported_outer_silk_masked_in_both_states': True, 'original_bitmap_pixels_unedited': True, 'source_geometry_retargeted_without_face_folds': True, 'files': [{'path': str(path), 'sha256': hashlib.sha256(path.read_bytes()).hexdigest()} for path in sorted(set(dependencies))], 'recipe_sha256': hashlib.sha256(Path(__file__).read_bytes()).hexdigest(), 'generated_art': False, 'installed': False, 'native_QA': False, 'selected_pair': False, 'outputs': records, 'notice': 'Offline candidate: complete native frame and retargeted native web, using four painted attachments. Web palette adapted for visibility; unsupported outer silk masked in both states. Used UV visibility mask opens the centre. Ground holdout buries native skirts without deleting geometry. Original bitmap pixels and UVs unedited. This is not a shipped encounter, native used animation or full lighting/wind/dither reconstruction. Native QA remains pending.'}, indent=2))
    print('SUPPORTED_NATIVE_WEB_PAIR_READY')
if __name__ == '__main__':
    main()
