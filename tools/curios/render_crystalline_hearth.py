"""Render a native stone hearth with one generated mineral component.

The authored meat renderer is hidden in both states. Used hides ten complete
upper chimney components; all vertices, UVs and native paint remain fixed.
Only missing teal crystals are generated. This is art, not firewood gameplay.
Cull2 is modeled; full Unity lighting/fog/dither is not reconstructed.
"""
from pathlib import Path
import bpy, numpy as np, json, math, sys, hashlib
from bpy_extras.object_utils import world_to_camera_view
from mathutils import Vector
import argparse
parser = argparse.ArgumentParser(description=__doc__)
for key in ('source', 'crystals', 'kit', 'out'):
    parser.add_argument('--' + key, type=Path, required=True)
a = parser.parse_args(sys.argv[sys.argv.index('--') + 1:])
source, crystals, OUT = [p.resolve() for p in (a.source, a.crystals, a.out)]
repo = Path(__file__).resolve().parents[2]
if OUT.exists() or any((p.is_relative_to(repo) for p in (source, crystals, OUT))):
    raise ValueError('Use private inputs and a fresh private output')
sha = lambda p: hashlib.sha256(Path(p).read_bytes()).hexdigest()
pins = {'mesh-3053592606538541013.npz': 'fdcb838c4e82acb1ed0345112d27bdf79f4aa08a4892e35d98a20255a42e7ac4', 'mesh-3053592607813329361.npz': 'eba4630abdbb530a297b9ddabf05c6e9ab601f69cbaad01d9c6d8d22d9f73628', 'scene.json': 'd9c55dc0ceb94f476a488b702666e69b12cf22d83ed4a462ee87c95955d7abdc'}
for n, h in pins.items():
    if sha(source / n) != h:
        raise ValueError('Complete reviewed native hearth changed: ' + n)
OUT.mkdir(parents=True)
sys.path.insert(0, str(a.kit.resolve() / 'tools'))
import bl_lib as L
doc = json.loads((source / 'scene.json').read_text())
assert len(doc['objects']) == 2 and (not doc['skipped'])
native_material = next(iter(doc['materials'].values()))
assert native_material['tint'] == [1.0, 1.0, 1.0, 1.0] and native_material['scale'] == [1.0, 1.0] and (native_material['offset'] == [0.0, 0.0])
if sha(native_material['texture']) != 'e4e21d15d2bd6fa758546efc358871f06c156ac005b2b7c21f55a838669900cf':
    raise ValueError('Full native2048 paint changed')
data = np.load(source / 'mesh-3053592606538541013.npz')
v, tri = (data['v'], data['tri'])
u, inv = np.unique(v, axis=0, return_inverse=True)
par = np.arange(len(u))

def find(x):
    while par[x] != x:
        par[x] = par[par[x]]
        x = par[x]
    return int(x)
for f in inv[tri]:
    a = find(f[0])
    for b in f[1:]:
        par[find(b)] = a
roots = np.array([find(f[0]) for f in inv[tri]])
upper = [216, 227, 321, 363, 373, 383, 389, 471, 599, 619]
hide = np.isin(roots, upper)
assert len(np.unique(roots)) == 26 and hide.sum() == 512 and (len(np.unique(tri[hide])) == 579)
assert hashlib.sha256(crystals.read_bytes()).hexdigest() == '5b587e41b69a8114bc56451336407313d2973798fc8083e3f8dd3e912f034b24'
L.reset()
place, _ = L.place_matrix(source, (0, 0, 0), rot=210, respect_active=True)
objects, low, high = L.build(source, place=place, respect_active=True)
body = next((o for o in objects if len(o.data.vertices) == 1122))
child = next((o for o in objects if len(o.data.vertices) == 1562))
child.hide_render = True
for mat in {m for o in objects for m in o.data.materials}:
    nt = mat.node_tree
    out = next((n for n in nt.nodes if n.type == 'OUTPUT_MATERIAL'))
    surface = out.inputs['Surface'].links[0].from_socket
    g = nt.nodes.new('ShaderNodeNewGeometry')
    transparent = nt.nodes.new('ShaderNodeBsdfTransparent')
    mix = nt.nodes.new('ShaderNodeMixShader')
    nt.links.new(g.outputs['Backfacing'], mix.inputs[0])
    nt.links.new(surface, mix.inputs[1])
    nt.links.new(transparent.outputs[0], mix.inputs[2])
    nt.links.new(mix.outputs[0], out.inputs[0])
mat = bpy.data.materials.new('Used whole upper chimney groups invisible')
mat.use_nodes = True
nt = mat.node_tree
nt.nodes.clear()
tr = nt.nodes.new('ShaderNodeBsdfTransparent')
out = nt.nodes.new('ShaderNodeOutputMaterial')
nt.links.new(tr.outputs[0], out.inputs[0])
body.data.materials.append(mat)
height = 1.9
width = height * 1024 / 1536
local = [(-width / 2, 1.5, 0.0), (width / 2, 1.5, 0.0), (width / 2, 1.5 + height, 0.0), (-width / 2, 1.5 + height, 0.0)]
W = L.CHANGE @ place
vertices = [(W @ np.array([*p, 1]))[:3].tolist() for p in local]
me = bpy.data.meshes.new('ONLY_generated_crystal_card')
me.from_pydata(vertices, [], [(0, 1, 2, 3)])
card = bpy.data.objects.new('ONLY_generated_crystal_component', me)
bpy.context.collection.objects.link(card)
uv = me.uv_layers.new()
for i, p in enumerate(((0, 0), (1, 0), (1, 1), (0, 1))):
    uv.data[i].uv = p
image = bpy.data.images.load(str(crystals), check_existing=False)
assert tuple(image.size) == (1024, 1536)
mat = bpy.data.materials.new('Generated crystals alpha exact RGB grade')
mat.use_nodes = True
nt = mat.node_tree
nt.nodes.clear()
tex = nt.nodes.new('ShaderNodeTexImage')
tex.image = image
mul = nt.nodes.new('ShaderNodeMixRGB')
mul.blend_type = 'MULTIPLY'
mul.inputs[0].default_value = 1
mul.inputs[2].default_value = (0.65, 0.65, 0.65, 1)
em = nt.nodes.new('ShaderNodeEmission')
tr = nt.nodes.new('ShaderNodeBsdfTransparent')
mix = nt.nodes.new('ShaderNodeMixShader')
out = nt.nodes.new('ShaderNodeOutputMaterial')
nt.links.new(tex.outputs['Color'], mul.inputs[1])
nt.links.new(mul.outputs[0], em.inputs['Color'])
nt.links.new(tex.outputs['Alpha'], mix.inputs[0])
nt.links.new(tr.outputs[0], mix.inputs[1])
nt.links.new(em.outputs[0], mix.inputs[2])
nt.links.new(mix.outputs[0], out.inputs[0])
card.data.materials.append(mat)
center = (low + high) / 2
extent = max(high[0] - low[0], high[2] - low[2]) * 1.15
distance = max(12, extent * 2)
camera = L.camera((center[0], center[1] - distance, center[2] + distance * math.tan(math.radians(10))), tuple(center), ortho=float(extent), res=(1024, 1024))
scene = bpy.context.scene
scene.render.threads_mode = 'FIXED'
scene.render.threads = 2
scene.cycles.seed = 0
for state in ('closed', 'used'):
    for i in np.where(hide)[0]:
        body.data.polygons[int(i)].material_index = 1 if state == 'used' else 0
    for generated in (False, True):
        card.hide_render = state == 'used' or not generated
        name = state if generated else state + '-native-body'
        L.render(OUT / (name + '.png'), samples=4, transparent=True)
bpy.context.view_layer.update()
fixed_ids = np.setdiff1d(np.arange(len(v)), np.unique(tri[hide]))
points = np.array([[q.x * 1024, (1 - q.y) * 1024] for q in (world_to_camera_view(scene, camera, body.matrix_world @ body.data.vertices[int(i)].co) for i in fixed_ids)])
cp = [world_to_camera_view(scene, camera, Vector(p)) for p in vertices]
bounds = [max(0, math.floor(min((p.x * 1024 for p in cp))) - 3), max(0, math.floor(min(((1 - p.y) * 1024 for p in cp))) - 3), min(1024, math.ceil(max((p.x * 1024 for p in cp))) + 3), min(1024, math.ceil(max(((1 - p.y) * 1024 for p in cp))) + 3)]
record = {'uninstalled_unaccepted_art_only': True, 'native_stone_body': [1122, 933], 'native_UV_fullpaint_tint_fixed': True, 'authored_meat_child_hidden_both_states': [1562, 2224], 'whole_upper_chimney_groups_hidden_used': upper, 'hidden_upper_native_vertices': 579, 'hidden_upper_native_triangles': 512, 'fixed_lower_native_vertices': len(fixed_ids), 'fixed_lower_native_triangles': int((~hide).sum()), 'generated_component_only': str(crystals), 'working_component_RGB': 0.65, 'source_alpha_original_exact': True, 'card_source_UV_full_bitmap': [0, 0, 1024, 1536], 'card_local_height': height, 'card_local_width': width, 'native_depth_occlusion': True, 'projected_card_domain': bounds, 'fixed_geometric_foot': [(points[:, 0].min() + points[:, 0].max()) / 2, points[:, 1].max()], 'fit_pack_binding_native_QA': False, 'outputs': {p.name: hashlib.sha256(p.read_bytes()).hexdigest() for p in OUT.glob('*.png')}}
record.update(recipe_sha256=sha(__file__), native_source_hashes=pins, native_paint_sha256=sha(native_material['texture']), generated_component_sha256=sha(crystals))
(OUT / 'provenance.json').write_text(json.dumps(record, indent=2))
print(json.dumps(record))
