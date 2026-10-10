"""Render an extracted native body for donor selection; all output stays private."""
from pathlib import Path
import sys

output = Path(sys.argv[sys.argv.index('--') + 1]).resolve()
repo = Path(__file__).resolve().parents[2]
if output == repo or repo in output.parents:
    raise ValueError('Derived game assets must be written outside the repository')

# Reuse mesh/bone helpers without executing costume authoring or export.
source=Path(__file__).with_name('model_blender.py').read_text()
exec(source.split('# Keep the costume authoring')[0],globals())
hero=sys.argv[sys.argv.index('--')+2]
body=part('native_'+hero,D['vertices'],[tuple(D['triangles'][i:i+3]) for i in range(0,len(D['triangles']),3)],
          [[(b,w) for b,w in zip(ii,ww) if w>.00001] for ii,ww in zip(D['indices'],D['weights'])],uv=D['uv'])
for loop in body.data.loops: body.data.uv_layers.active.data[loop.index].uv=D['uv'][loop.vertex_index]
for poly in body.data.polygons: poly.use_smooth=True
body.data.normals_split_custom_set_from_vertices(D['normals'])
mat=bpy.data.materials.new('Native body paint'); mat.use_nodes=True
nodes=mat.node_tree.nodes; links=mat.node_tree.links
base=nodes.new('ShaderNodeTexImage'); base.image=bpy.data.images.load(str(OUT/f'tex_{hero}_col.png'))
ink=nodes.new('ShaderNodeTexImage'); ink.image=bpy.data.images.load(str(OUT/f'tex_{hero}_ink.png'))
mix=nodes.new('ShaderNodeMixRGB'); mix.blend_type='MULTIPLY'; mix.inputs[0].default_value=.85
links.new(base.outputs['Color'],mix.inputs[1]); links.new(ink.outputs['Color'],mix.inputs[2])
shader=nodes.get('Principled BSDF'); links.new(mix.outputs[0],shader.inputs['Base Color'])
shader.inputs['Roughness'].default_value=1
body.data.materials.append(mat)
scene=bpy.context.scene; scene.render.engine='CYCLES'; scene.cycles.samples=16
scene.render.resolution_x=720; scene.render.resolution_y=840; scene.render.resolution_percentage=100
scene.world.color=(.25,.25,.25); scene.view_settings.view_transform='Standard'
def point(o,target):
    forward=(Vector(target)-o.location).normalized(); right=forward.cross(Vector((0,1,0))).normalized(); up=right.cross(forward)
    o.rotation_euler=Matrix((right,up,-forward)).transposed().to_euler()
bpy.ops.object.camera_add(location=(220,120,450)); cam=bpy.context.object; point(cam,(0,100,0))
cam.data.type='ORTHO'; cam.data.ortho_scale=235; scene.camera=cam
for pos,energy in [((-200,300,400),1600000),((200,180,-200),500000)]:
    bpy.ops.object.light_add(type='AREA',location=pos); lamp=bpy.context.object; lamp.data.energy=energy; lamp.data.size=200; point(lamp,(0,100,0))
scene.render.filepath=str(OUT/'native.png'); bpy.ops.render.render(write_still=True)
bpy.ops.wm.save_as_mainfile(filepath=str(OUT/'native.blend'))
