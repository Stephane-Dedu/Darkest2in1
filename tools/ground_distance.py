"""Native ground transitions behind the corridor's clear walking lane.

Only masks/placement are authored. Colour and surface marks come from extracted
DD2 paint. The distance field is in world metres, not screen-space bands.
"""
import json
from pathlib import Path

PATCHES = {
    "city": ("roads/farm_node_story_assist", "farm_ground_dark", 9, 1.0),
    "farm": ("roads/farm_tile_01", "farm_footpath", 10, 1.0),
    "forest": ("roads/road_forest_brambles_diagonal_quad_01", "forest_road_roots_03", 9, .85),
    "coast": ("arenas/scenes_scenes_combat_arena_coast_resist", "coast_road_sand_02_combat", 14, .22),
}
SOILS = {r: ("roads/forest_mid_ground", "forest_ground_dark") if r == "forest"
         else ("roads/farm_node_story_assist", "farm_ground_dark") for r in PATCHES}
TRANSITIONS = {r: (4, 14) if r == "coast" else (6, 28) for r in PATCHES}
LANE_COVERAGE = {"city": (.55, 1.0), "farm": (.18, .90), "forest": (.06, .78)}


def source(kit, folder, name):
    folder = Path(kit) / folder
    document = json.loads((folder / "scene.json").read_text())
    data = next(m for m in document["materials"].values() if m["name"] == name)
    texture = (folder / data["texture"]).resolve()
    if not texture.is_file():
        raise FileNotFoundError(texture)
    return data, texture


def describe(kit, region):
    import hashlib
    folder, name, period, exposure = PATCHES[region]
    _, patch = source(kit, folder, name)
    _, soil = source(kit, *SOILS[region])
    return {"profile": "native-ground-depth-v23", "patch_material": name,
            "patch_sha256": hashlib.sha256(patch.read_bytes()).hexdigest(),
            "soil_sha256": hashlib.sha256(soil.read_bytes()).hexdigest(),
            "patch_period": period, "patch_exposure": exposure,
            "lane_detail_coverage": LANE_COVERAGE.get(region),
            "distance_transition_starts_at_world_depth": 3,
            "distance_transition": list(TRANSITIONS[region])}


def paint(nt, base, position, kit, region, shade, phase):
    import bpy

    def math(op, a, b=None):
        node = nt.nodes.new("ShaderNodeMath")
        node.operation = op
        for i, value in enumerate((a, b)):
            if value is None:
                continue
            if isinstance(value, (int, float)):
                node.inputs[i].default_value = value
            else:
                nt.links.new(value, node.inputs[i])
        return node.outputs[0]

    def ramp(value, low, high, out_low=0, out_high=1):
        node = nt.nodes.new("ShaderNodeMapRange")
        node.interpolation_type = "SMOOTHSTEP"
        node.clamp = True
        node.inputs["From Min"].default_value = low
        node.inputs["From Max"].default_value = high
        node.inputs["To Min"].default_value = out_low
        node.inputs["To Max"].default_value = out_high
        nt.links.new(value, node.inputs["Value"])
        return node.outputs[0]

    def mix(a, b, weight):
        node = nt.nodes.new("ShaderNodeMixRGB")
        nt.links.new(weight, node.inputs[0])
        nt.links.new(a, node.inputs[1])
        nt.links.new(b, node.inputs[2])
        return node.outputs[0]

    shifted = nt.nodes.new("ShaderNodeVectorMath")
    shifted.operation = "ADD"
    shifted.inputs[1].default_value = (phase*17.31, phase*9.17, 0)
    nt.links.new(position, shifted.inputs[0])
    noise = nt.nodes.new("ShaderNodeTexNoise")
    noise.inputs["Scale"].default_value = .085
    noise.inputs["Detail"].default_value = 1.3
    nt.links.new(shifted.outputs[0], noise.inputs["Vector"])
    separate = nt.nodes.new("ShaderNodeSeparateXYZ")
    nt.links.new(position, separate.inputs[0])
    depth = separate.outputs["Y"]
    lane_guard = ramp(depth, 3, 6)
    # Wavy boundaries avoid replacing one repeated strip with another. The
    # independent lane guard keeps the distance transition behind the actors.
    distance = math("ADD", depth, math("MULTIPLY", noise.outputs["Fac"], 12))
    recede = math("MULTIPLY", lane_guard, ramp(distance, *TRANSITIONS[region]))
    # Wet sand needs distinct deposits, not a translucent wash that makes
    # intact boards look blurred. Earth/root transitions can stay softer.
    islands = (ramp(noise.outputs["Fac"], .48, .54, 0, .98) if region == "coast"
               else ramp(noise.outputs["Fac"], .36, .66, .10, .95))

    def texture(folder, name, period, exposure):
        data, path = source(kit, folder, name)
        mapping = nt.nodes.new("ShaderNodeVectorMath")
        mapping.operation = "MULTIPLY_ADD"
        mapping.inputs[1].default_value = (1/period, 1/period, 0)
        mapping.inputs[2].default_value = (.173+phase*.137, .417+phase*.071, 0)
        nt.links.new(position, mapping.inputs[0])
        tex = nt.nodes.new("ShaderNodeTexImage")
        tex.image = bpy.data.images.load(str(path), check_existing=True)
        nt.links.new(mapping.outputs[0], tex.inputs["Vector"])
        tint = nt.nodes.new("ShaderNodeMixRGB")
        tint.blend_type = "MULTIPLY"
        tint.inputs[0].default_value = 1
        tint.inputs[2].default_value = tuple(v*shade*exposure for v in data["tint"][:3])+(1,)
        nt.links.new(tex.outputs["Color"], tint.inputs[1])
        return tint.outputs[0]

    soil = texture(*SOILS[region], 18, 1)
    if region in LANE_COVERAGE:
        # Native floor marks occur in groups, with breathing room between them.
        # The same field continues through the actor lane and the background;
        # this is colour only, never displacement or a camera-dependent fade.
        low, high = LANE_COVERAGE[region]
        base = mix(soil, base, ramp(noise.outputs["Fac"], .42, .60, low, high))
    patch = texture(*PATCHES[region])
    result = mix(base, patch, math("MULTIPLY", recede, islands))
    # Distant painted soil replaces dense repeated motifs before the horizon;
    # nearby detail and the texture's original orientation remain intact.
    distant = math("MULTIPLY", recede, ramp(distance, 32, 100, 0, .78))
    return mix(result, soil, distant)
