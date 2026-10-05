"""Blender background script for export_arena_reference.py's private output.

Run blender -b --python tools/render_arena_reference.py -- --scene <scene.json>.
Native base-colour textures and arena geometry are retained; materials, lighting,
camera and sky are approximate. This is not an in-game screenshot.
"""
import argparse
import json
import math
import sys
from pathlib import Path

import bpy
from mathutils import Matrix, Vector


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--scene", type=Path, required=True)
    parser.add_argument("--width", type=int, default=1536)
    parser.add_argument("--height", type=int, default=864)
    args = parser.parse_args(sys.argv[sys.argv.index("--") + 1:])
    doc = json.loads(args.scene.read_text(encoding="utf-8"))
    folder = args.scene.parent
    bpy.ops.object.select_all(action="SELECT")
    bpy.ops.object.delete(use_global=False)
    mats = {}
    for key, data in doc["materials"].items():
        mat = bpy.data.materials.new(data["name"])
        mat.use_nodes = True
        nodes = mat.node_tree.nodes
        nodes.clear()
        output = nodes.new("ShaderNodeOutputMaterial")
        emission = nodes.new("ShaderNodeEmission")
        tint = data["tint"]
        rgb = (tint["r"], tint["g"], tint["b"], 1)
        if data["texture"]:
            tex = nodes.new("ShaderNodeTexImage")
            tex.image = bpy.data.images.load(str(folder / data["texture"]), check_existing=True)
            tint_node = nodes.new("ShaderNodeMixRGB")
            tint_node.blend_type = "MULTIPLY"
            tint_node.inputs[0].default_value = 1
            tint_node.inputs[2].default_value = rgb
            mat.node_tree.links.new(tex.outputs["Color"], tint_node.inputs[1])
            mat.node_tree.links.new(tint_node.outputs[0], emission.inputs["Color"])
            uv = nodes.new("ShaderNodeTexCoord")
            mapping = nodes.new("ShaderNodeVectorMath")
            mapping.operation = "MULTIPLY_ADD"
            mapping.inputs[1].default_value = (data["scale"]["x"], data["scale"]["y"], 1)
            mapping.inputs[2].default_value = (data["offset"]["x"], data["offset"]["y"], 0)
            mat.node_tree.links.new(uv.outputs["UV"], mapping.inputs[0])
            mat.node_tree.links.new(mapping.outputs[0], tex.inputs["Vector"])
            if data["cutout"]:
                transparent = nodes.new("ShaderNodeBsdfTransparent")
                mix = nodes.new("ShaderNodeMixShader")
                threshold = nodes.new("ShaderNodeMath")
                threshold.operation = "GREATER_THAN"
                threshold.inputs[1].default_value = .4
                mat.node_tree.links.new(tex.outputs["Alpha"], threshold.inputs[0])
                mat.node_tree.links.new(threshold.outputs[0], mix.inputs[0])
                mat.node_tree.links.new(transparent.outputs[0], mix.inputs[1])
                mat.node_tree.links.new(emission.outputs[0], mix.inputs[2])
                mat.node_tree.links.new(mix.outputs[0], output.inputs[0])
            else:
                mat.node_tree.links.new(emission.outputs[0], output.inputs[0])
        else:
            emission.inputs["Color"].default_value = rgb
            mat.node_tree.links.new(emission.outputs[0], output.inputs[0])
        mats[key] = mat
    # Unity Y-up to Blender Z-up, retaining screen left/right.
    change = Matrix(((1,0,0,0),(0,0,1,0),(0,1,0,0),(0,0,0,1)))
    for item in doc["objects"]:
        data = doc["meshes"][item["mesh"]]
        vertices = data["vertices"]
        tris, slots = [], []
        for i in range(item["count"]):
            sub = item["first"] + i
            for a, b, c in data["triangles"][sub]:
                tris.append((c, b, a))
                slots.append(min(i, len(item["materials"])-1))
        # Discard the unused portion of a Unity static-batch mesh.
        used = sorted(set(v for tri in tris for v in tri))
        indices = {v: i for i, v in enumerate(used)}
        transform = change @ Matrix(item["world"])
        points = [tuple(transform @ Vector((*vertices[v][:3], 1)))[:3] for v in used]
        mesh = bpy.data.meshes.new(item["name"])
        mesh.from_pydata(points, [], [tuple(indices[v] for v in tri) for tri in tris])
        mesh.update()
        obj = bpy.data.objects.new(item["name"], mesh)
        bpy.context.collection.objects.link(obj)
        for key in item["materials"]:
            mesh.materials.append(mats[key])
        uv = mesh.uv_layers.new(name="UVMap")
        for p, slot in zip(mesh.polygons, slots):
            p.material_index = slot
            for loop in p.loop_indices:
                original = used[mesh.loops[loop].vertex_index]
                uv.data[loop].uv = data["uv"][original][:2] if data["uv"] else (0,0)
    camera_data = bpy.data.cameras.new("Reference camera")
    camera = bpy.data.objects.new("Reference camera", camera_data)
    bpy.context.collection.objects.link(camera)
    camera.location = (change @ Vector((*doc["camera"]["position"], 1))).to_3d()
    camera.rotation_euler = Vector((0,1,0)).to_track_quat("-Z", "Y").to_euler()
    camera_data.sensor_fit = "VERTICAL"
    camera_data.sensor_height = 24
    camera_data.lens = 12 / math.tan(math.radians(doc["camera"]["vertical_fov"]) / 2)
    scene = bpy.context.scene
    scene.camera = camera
    scene.render.engine = "CYCLES"
    scene.cycles.samples = 16
    scene.render.resolution_x = args.width
    scene.render.resolution_y = args.height
    scene.render.resolution_percentage = 100
    scene.view_settings.view_transform = "Standard"
    scene.world.use_nodes = True
    background = scene.world.node_tree.nodes.get("Background")
    sky = {"city": (.13,.075,.05,1), "farm": (.15,.13,.07,1),
           "forest": (.065,.085,.11,1), "coast": (.08,.12,.15,1)}[doc["region"]]
    background.inputs["Color"].default_value = sky
    background.inputs["Strength"].default_value = .6
    scene.render.image_settings.file_format = "PNG"
    scene.render.filepath = str(folder / "arena-reference.png")
    bpy.ops.wm.save_as_mainfile(filepath=str(folder / "arena-reference.blend"))
    bpy.ops.render.render(write_still=True)


if __name__ == "__main__":
    main()
