"""Native painted room ground, without repeating driving lanes or prop atlases.

Only Blender shader composition is authored here. Colour and ink come from the
owner's extracted region textures; all resulting artwork stays outside Git.
"""
import json
import math

PAINTS = {
    "city": ("arenas/scenes_scenes_combat_arena_city_urgent_repairs", "road_rough_patch_darker", 18),
    "farm": ("roads/farm_tile_01", "farm_footpath", 22),
    "forest": ("roads/road_forest_brambles_diagonal_quad_01", "forest_road_roots_01", 22),
    "coast": ("roads/road_coast_diagonal_corner_01", "coast_road_01_mix_alt", 22),
}
CORRIDOR_PAINTS = dict(PAINTS, farm=("roads/road_farm_crops_4way", "farm_road_variation_01", 12))
CORRIDOR_PERIODS = {"city": 12, "farm": 12, "forest": 11, "coast": 12}


def source(kit, region, arena=None, corridor=False):
    folder, name, period = (CORRIDOR_PAINTS if corridor else PAINTS)[region]
    if corridor:
        period = CORRIDOR_PERIODS[region]
    # Beach encounters have sand under their original landmarks. Extending
    # them with pier boards creates a conspicuous material seam at the feet.
    if region == "coast" and arena in ("resist", "gaunt_chirurgeon"):
        folder = f"arenas/scenes_scenes_combat_arena_coast_{arena}"
        name, period = "coast_road_sand_02_combat", 22
    folder = kit / folder
    document = json.loads((folder / "scene.json").read_text())
    key, data = next((k, v) for k, v in document["materials"].items() if v["name"] == name)
    path = (folder / data["texture"]).resolve()
    if not path.is_file():
        raise FileNotFoundError(path)
    return folder, key, data, path, period


def paint(ground, folder, data, shade, period, phase, corridor=False):
    """Blend independently mapped native paint in broad, irregular world patches.

    The original eight-unit lane repeat is not sampled. Periods differ, and
    offsets change with the arena, so distinctive marks cannot form a tile grid.
    Noise controls joins and contact shading only; it supplies no replacement art.
    """
    import bpy
    material = bpy.data.materials.new("room_native_" + data["name"])
    material.use_nodes = True
    ground.data.materials.clear()
    ground.data.materials.append(material)
    nt = material.node_tree
    nt.nodes.clear()
    out = nt.nodes.new("ShaderNodeOutputMaterial")
    emission = nt.nodes.new("ShaderNodeEmission")
    nt.links.new(emission.outputs[0], out.inputs[0])
    position = nt.nodes.new("ShaderNodeNewGeometry").outputs["Position"]
    orientation = nt.nodes.new("ShaderNodeVectorRotate")
    orientation.rotation_type = "AXIS_ANGLE"
    orientation.inputs["Axis"].default_value = (0, 0, 1)
    orientation.inputs["Angle"].default_value = -math.pi / 2
    nt.links.new(position, orientation.inputs["Vector"])
    image = bpy.data.images.load(str((folder / data["texture"]).resolve()), check_existing=True)

    def sample(scale, offset):
        mapping = nt.nodes.new("ShaderNodeVectorMath")
        mapping.operation = "MULTIPLY_ADD"
        mapping.inputs[1].default_value = (1 / scale, 1 / scale, 0)
        mapping.inputs[2].default_value = (*offset, 0)
        nt.links.new(orientation.outputs[0], mapping.inputs[0])
        texture = nt.nodes.new("ShaderNodeTexImage")
        texture.image = image
        coordinates = mapping.outputs[0]
        nt.links.new(coordinates, texture.inputs["Vector"])
        return texture.outputs["Color"]

    def mix(a, b, scale, offset):
        coordinates = nt.nodes.new("ShaderNodeVectorMath")
        coordinates.operation = "ADD"
        coordinates.inputs[1].default_value = (offset, phase * 7.137, 0)
        nt.links.new(position, coordinates.inputs[0])
        noise = nt.nodes.new("ShaderNodeTexNoise")
        noise.inputs["Scale"].default_value = scale
        noise.inputs["Detail"].default_value = 1.2
        nt.links.new(coordinates.outputs[0], noise.inputs["Vector"])
        boundary = nt.nodes.new("ShaderNodeMapRange")
        boundary.interpolation_type = "SMOOTHSTEP"
        boundary.inputs["From Min"].default_value = .48 if corridor else .43
        boundary.inputs["From Max"].default_value = .52 if corridor else .57
        nt.links.new(noise.outputs["Fac"], boundary.inputs["Value"])
        blend = nt.nodes.new("ShaderNodeMixRGB")
        nt.links.new(boundary.outputs[0], blend.inputs[0])
        nt.links.new(a, blend.inputs[1])
        nt.links.new(b, blend.inputs[2])
        return blend.outputs[0]

    a = sample(period, (.137 + phase * .173, .419))
    b = sample(period * 1.37, (.713, .197 + phase * .137))
    c = sample(period * .83, (.381 + phase * .079, .831))
    # Boards are rigid objects: blending differently scaled plank grids makes
    # their edges split and double. Retain one mapping for a coherent pier.
    painted = a if corridor and data["name"].startswith("coast_road_") else mix(
        mix(a, b, .13, phase * 11.73), c, .087, 37 + phase * 3.17)
    tint = nt.nodes.new("ShaderNodeMixRGB")
    tint.blend_type = "MULTIPLY"
    tint.inputs[0].default_value = 1
    tint.inputs[2].default_value = tuple(x * shade for x in data["tint"][:3]) + (1,)
    nt.links.new(painted, tint.inputs[1])
    ao = nt.nodes.new("ShaderNodeAmbientOcclusion")
    ao.inputs["Distance"].default_value = 3
    ao.samples = 8
    contact = nt.nodes.new("ShaderNodeMapRange")
    contact.inputs["To Min"].default_value = .45
    nt.links.new(ao.outputs["AO"], contact.inputs["Value"])
    multiply = nt.nodes.new("ShaderNodeMixRGB")
    multiply.blend_type = "MULTIPLY"
    multiply.inputs[0].default_value = 1
    nt.links.new(tint.outputs[0], multiply.inputs[1])
    nt.links.new(contact.outputs[0], multiply.inputs[2])
    nt.links.new(multiply.outputs[0], emission.inputs["Color"])
