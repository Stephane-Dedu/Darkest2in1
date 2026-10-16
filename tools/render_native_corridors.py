"""Render DD2 road kits as continuous side-view scenes, with native textured ground.

Blender: --python tools/render_native_corridors.py -- KIT OUTPUT [--only city-01 ...]
Python/Pillow: tools/render_native_corridors.py KIT OUTPUT --compose-only
Game artwork and render passes must stay outside Git. The source kit is never modified.
"""
import argparse
import hashlib
import json
import math
import random
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))
from render_native_rooms import PALETTES, FLOORS, digest
from scenery_camera import ENVIRONMENT_SCALE, create as stage_camera, floor as stage_floor, settings as camera_settings, configure_materials

ROADS = {"city": "road_city_straight_single", "farm": "road_farm_straight_single",
         "forest": "road_forest_straight_single", "coast": "road_coast_straight_single_01"}
W, H = 4096, 768

# Low native scenery omitted by the tall-wall selection. These retain their own
# painted UVs and give the lane an uneven edge instead of an endless flat floor.
VERGES = {
    "forest": ("forest_tile_11", "forest_tile_05", "forest_tile_brambles_01"),
    "farm": ("farm_tile_05", "farm_tile_06", "farm_tile_12", "farm_tile_20"),
    "coast": ("coast_tile_14", "coast_tile_10", "coast_tile_07"),
}

GROUND_DETAILS = {
    "forest": ("roads/road_forest_brambles_diagonal_quad_01", "forest_road_roots_01", .43, (.45,.95)),
    "city": ("arenas/scenes_scenes_combat_arena_city_urgent_repairs", "road_rough_patch_darker", .57, (.72,.95)),
    "farm": ("roads/farm_tile_01", "farm_footpath", .44, (.65,.95)),
    "coast": ("roads/road_coast_diagonal_corner_01", "coast_road_01_mix_alt", .47, (.50,.90)),
}

# Region-specific near scenery. Use only selected native materials, without the
# broad driving foundations or unsupported hanging decorations.
INFILL = {
    "city": (("city_decor_05", "city_decor_03", .35,.50),
             ("city_decor_05", "city_decor_01_transp", .28,.42)),
    "farm": (("farm_tile_33_crops", "farm_large_crops_sway", .32,.46),
             ("farm_tile_34_crops", "farm_large_crops_sway", .26,.34),
             ("farm_tile_35_crops", "farm_sunflowers", .25,.38),
             ("farm_tile_04", "farm_tree_02_big", .45,.62)),
    "coast": (("coast_tile_09", "coast_boats", .40,.55),
              ("coast_tile_12", "coast_boats", .35,.50),
              ("coast_tile_11", "coast_", .33,.45)),
}
LITTER = {
    "city": (("exp/camp_crate", "camp_props", .75,1.20),
             ("exp/camp_barrel", "camp_props", .75,1.20)),
    "farm": (("farm_tile_05", "farm_shrubs_and_tufts", .30,.50),
             ("farm_tile_12", "farm_shrubs_and_tufts", .30,.50),
             ("farm_tile_10", "farm_fences_and_walls", .40,.60)),
    "coast": (("coast_tile_07", "coast_deadfish", .18,.25),
              ("coast_tile_14", "coast_deadfish", .22,.35),
              ("coast_tile_10", "coast_posts", .18,.25)),
}


def native_parts(objects, material_fragment):
    """Retain selected native faces/UVs from combined road meshes."""
    import bmesh
    import bpy
    retained = []
    for obj in objects:
        bm = bmesh.new()
        bm.from_mesh(obj.data)
        rejected = [face for face in bm.faces if material_fragment not in obj.data.materials[face.material_index].name]
        bmesh.ops.delete(bm, geom=rejected, context="FACES")
        bmesh.ops.delete(bm, geom=[v for v in bm.verts if not v.link_faces], context="VERTS")
        if bm.faces:
            bm.to_mesh(obj.data)
            obj.data.update()
            retained.append(obj)
        else:
            bpy.data.objects.remove(obj, do_unlink=True)
        bm.free()
    return retained


def wall_asset(name, size):
    # The Tangle's hill tiles contain only the dark driving foundation removed
    # by configure_materials. Counting their bounds leaves empty spans in a wall.
    return (("_tile_" in name or "_decor_" in name) and "node" not in name
            and not name.startswith(("road", "forest_tile_hill_", "forest_tile_base_"))
            and size[1] >= 6 and size[0] <= 40)


def merge_entries(previous, updated):
    """A selective re-render replaces its records, retaining other completed scenes."""
    replacements = {entry["short"]: entry for entry in updated}
    result = [replacements.pop(entry["short"], entry) for entry in previous]
    return result + list(replacements.values())


def ground_patch_paint(nt, painted, folder, data, shade, uv_scale=.43, coverage=(.45,.95), phase=0):
    """Break driving-road repeats with irregular patches of native regional paint."""
    import bpy
    texture = nt.nodes.new("ShaderNodeTexImage")
    texture.image = bpy.data.images.load(str((folder / data["texture"]).resolve()), check_existing=True)
    uv = nt.nodes.new("ShaderNodeTexCoord")
    mapping = nt.nodes.new("ShaderNodeVectorMath")
    mapping.operation = "MULTIPLY_ADD"
    # A different, larger world period prevents the root and road repeats from
    # lining up. Keep sharp native ink; only the irregular patch boundaries blend.
    mapping.inputs[1].default_value = (uv_scale, uv_scale, 1)
    mapping.inputs[2].default_value = (.173+phase*.113, .417+phase*.071, 0)
    nt.links.new(uv.outputs["UV"], mapping.inputs[0])
    nt.links.new(mapping.outputs[0], texture.inputs["Vector"])
    tint = nt.nodes.new("ShaderNodeMixRGB")
    tint.blend_type = "MULTIPLY"
    tint.inputs[0].default_value = 1
    tint.inputs[2].default_value = tuple(c * shade for c in data["tint"][:3]) + (1,)
    nt.links.new(texture.outputs["Color"], tint.inputs[1])
    position = nt.nodes.new("ShaderNodeNewGeometry")
    noise = nt.nodes.new("ShaderNodeTexNoise")
    noise.inputs["Scale"].default_value = .19
    noise.inputs["Detail"].default_value = 1.3
    coordinates = position.outputs["Position"]
    if phase:
        offset = nt.nodes.new("ShaderNodeVectorMath")
        offset.operation = "ADD"
        offset.inputs[1].default_value = (phase*13.17,phase*7.31,0)
        nt.links.new(coordinates, offset.inputs[0])
        coordinates = offset.outputs[0]
    nt.links.new(coordinates, noise.inputs["Vector"])
    patches = nt.nodes.new("ShaderNodeMapRange")
    patches.interpolation_type = "SMOOTHSTEP"
    patches.inputs["From Min"].default_value = .34
    patches.inputs["From Max"].default_value = .60
    nt.links.new(noise.outputs["Fac"], patches.inputs["Value"])
    depth = nt.nodes.new("ShaderNodeSeparateXYZ")
    nt.links.new(position.outputs["Position"], depth.inputs[0])
    depth_weight = nt.nodes.new("ShaderNodeMapRange")
    depth_weight.interpolation_type = "SMOOTHSTEP"
    depth_weight.inputs["From Min"].default_value = 1
    depth_weight.inputs["From Max"].default_value = 16
    depth_weight.inputs["To Min"].default_value = coverage[0]
    depth_weight.inputs["To Max"].default_value = coverage[1]
    nt.links.new(depth.outputs["Y"], depth_weight.inputs["Value"])
    weight = nt.nodes.new("ShaderNodeMath")
    weight.operation = "MULTIPLY"
    nt.links.new(patches.outputs[0], weight.inputs[0])
    nt.links.new(depth_weight.outputs[0], weight.inputs[1])
    mix = nt.nodes.new("ShaderNodeMixRGB")
    nt.links.new(weight.outputs[0], mix.inputs[0])
    nt.links.new(painted, mix.inputs[1])
    nt.links.new(tint.outputs[0], mix.inputs[2])
    return mix.outputs[0]


def ground_contact(ground, detail_paint=None, shade=1, paint_settings=None):
    """Native floor paint with local geometry contact, independent of missing DD2 lights."""
    import bpy
    for slot in ground.material_slots:
        mat = slot.material.copy()
        slot.material = mat
        nt = mat.node_tree
        emission = next(n for n in nt.nodes if n.type == "EMISSION")
        painted = emission.inputs["Color"].links[0].from_socket
        if detail_paint:
            painted = ground_patch_paint(nt, painted, *detail_paint, shade, **(paint_settings or {}))
        ao = nt.nodes.new("ShaderNodeAmbientOcclusion")
        ao.inputs["Distance"].default_value = 3
        ao.samples = 8
        strength = nt.nodes.new("ShaderNodeMapRange")
        strength.inputs["To Min"].default_value = .40
        nt.links.new(ao.outputs["AO"], strength.inputs["Value"])
        # Broad, restrained value variation breaks the uniform tile bands while
        # retaining the original painted detail, hue and room/corridor UV scale.
        position = nt.nodes.new("ShaderNodeNewGeometry")
        variation = nt.nodes.new("ShaderNodeTexNoise")
        variation.inputs["Scale"].default_value = .12
        variation.inputs["Detail"].default_value = 2
        nt.links.new(position.outputs["Position"], variation.inputs["Vector"])
        stain = nt.nodes.new("ShaderNodeMapRange")
        stain.inputs["To Min"].default_value = .78
        stain.inputs["To Max"].default_value = 1.02
        nt.links.new(variation.outputs["Fac"], stain.inputs["Value"])
        contact = nt.nodes.new("ShaderNodeMath")
        contact.operation = "MULTIPLY"
        nt.links.new(strength.outputs[0], contact.inputs[0])
        nt.links.new(stain.outputs[0], contact.inputs[1])
        multiply = nt.nodes.new("ShaderNodeMixRGB")
        multiply.blend_type = "MULTIPLY"
        multiply.inputs[0].default_value = 1
        nt.links.new(painted, multiply.inputs[1])
        nt.links.new(contact.outputs[0], multiply.inputs[2])
        nt.links.new(multiply.outputs[0], emission.inputs["Color"])


def ocean_material(folder, variant):
    """Bake restrained native cloud/foam detail instead of a flat black sea.

    This is an offline approximation, not a reconstruction of Unity's animated
    water shader. The native maps and palette stay private beside their provenance.
    """
    import bpy
    document = json.loads((folder / "materials.json").read_text())
    data = next(m for m in document["materials"] if m["name"] == "vfx_biome_common_water_ocean")
    mat = bpy.data.materials.new("crawl_shroud_native_water")
    mat.use_nodes = True
    nt = mat.node_tree
    nt.nodes.clear()
    out = nt.nodes.new("ShaderNodeOutputMaterial")
    emission = nt.nodes.new("ShaderNodeEmission")
    nt.links.new(emission.outputs[0], out.inputs[0])
    position = nt.nodes.new("ShaderNodeNewGeometry")

    def sample(key, scale, offset):
        record = data["textures"][key]
        path = folder / record["file"]
        if digest(path) != record["sha256"]:
            raise ValueError(f"Native water texture hash mismatch: {path}")
        mapping = nt.nodes.new("ShaderNodeVectorMath")
        mapping.operation = "MULTIPLY_ADD"
        mapping.inputs[1].default_value = (*scale, 1)
        mapping.inputs[2].default_value = (*offset, 0)
        nt.links.new(position.outputs["Position"], mapping.inputs[0])
        texture = nt.nodes.new("ShaderNodeTexImage")
        texture.image = bpy.data.images.load(str(path), check_existing=True)
        texture.image.colorspace_settings.name = "Non-Color"
        nt.links.new(mapping.outputs[0], texture.inputs["Vector"])
        return texture.outputs["Color"]

    cloud_a = sample("_CloudMaskTexture", (1/28, 1/19), (variant*.173, variant*.071))
    cloud_b = sample("_CloudMaskTexture", (1/43, -1/31), (.37+variant*.053, .19))
    cloud = nt.nodes.new("ShaderNodeMath")
    cloud.operation = "MULTIPLY"
    nt.links.new(cloud_a, cloud.inputs[0])
    nt.links.new(cloud_b, cloud.inputs[1])
    intensity = nt.nodes.new("ShaderNodeMapRange")
    intensity.inputs["To Min"].default_value = .018
    intensity.inputs["To Max"].default_value = .12
    nt.links.new(cloud.outputs[0], intensity.inputs["Value"])
    water = nt.nodes.new("ShaderNodeMixRGB")
    water.blend_type = "MULTIPLY"
    water.inputs[0].default_value = 1
    color = data["colors"]["_ColorA"]
    water.inputs[1].default_value = (color["r"], color["g"], color["b"], 1)
    nt.links.new(intensity.outputs[0], water.inputs[2])
    foam = sample("_FoamTexture", (1/38, 1/9), (.19+variant*.127, variant*.091))
    foam_weight = nt.nodes.new("ShaderNodeMath")
    foam_weight.operation = "MULTIPLY"
    nt.links.new(foam, foam_weight.inputs[0])
    nt.links.new(cloud.outputs[0], foam_weight.inputs[1])
    foam_mix = nt.nodes.new("ShaderNodeMixRGB")
    nt.links.new(foam_weight.outputs[0], foam_mix.inputs[0])
    nt.links.new(water.outputs[0], foam_mix.inputs[1])
    color = data["colors"]["_FoamColor"]
    foam_mix.inputs[2].default_value = (color["r"]*.14, color["g"]*.14, color["b"]*.14, 1)
    nt.links.new(foam_mix.outputs[0], emission.inputs["Color"])
    return mat


def compose(output):
    import numpy as np
    from PIL import Image
    manifest = json.loads((output / "render-manifest.json").read_text())
    for entry in manifest["files"]:
        region = entry["region"].removeprefix("dd2_")
        _, top, bottom, haze = PALETTES[region]
        gradient = np.linspace(0, 1, H)[:, None, None] ** .8
        image = Image.fromarray((np.array(top) * (1-gradient) + np.array(bottom) * gradient)
                                .astype(np.uint8).repeat(W, 1)).convert("RGBA")
        # Ground is behind the distant scenery, never an opaque card over its foundations.
        for band, fog in (("ground", .22), ("sky", .55), ("vfar", .60), ("far", .38), ("wall", .06), ("road", 0)):
            path = output / "passes" / f"{entry['short']}-{band}.png"
            if not path.is_file():
                continue
            layer = Image.open(path).convert("RGBA")
            if layer.size != (W, H):
                raise ValueError(path)
            pixels = np.asarray(layer).astype(np.float32).copy()
            if band == "ground":
                rows = np.flatnonzero(pixels[...,3].max(axis=1) > 0)
                if len(rows):
                    fade = np.clip((np.arange(H)-rows[0])/160,0,1)[:,None]
                    pixels[...,3] *= fade*fade*(3-2*fade)
            if band == "sky":
                # Exported skybox cards end in a horizontal cut. Blend their lower edge into
                # the continuous ground/water, retaining the skyline's original pixels.
                opaque = pixels[...,3] > 240
                bottoms = np.max(np.where(opaque,np.arange(H)[:,None],-1),axis=0)
                fade = np.clip((bottoms[None,:]-np.arange(H)[:,None])/140,0,1)
                pixels[...,3] *= fade*fade*(3-2*fade)
            if fog:
                # Fade the ground haze continuously toward the foreground.
                strength = fog if band != "ground" else fog * np.clip((H-np.arange(H)) / (H*.65), 0, 1)[:, None, None]
                pixels[..., :3] = pixels[..., :3] * (1-strength) + np.array(haze) * strength
            layer = Image.fromarray(pixels.clip(0, 255).astype(np.uint8))
            image.alpha_composite(layer)
        image = image.convert("RGB")
        target = output / entry["file"]
        image.save(target)
        if target.stat().st_size > 16*1024*1024:
            raise ValueError(f"Oversized panorama: {target}")
        entry["sha256"] = digest(target)
        entry["fit_zoom"] = 1
        print("COMPOSED", target.name, flush=True)
    manifest["composition_tool_sha256"] = digest(Path(__file__).resolve())
    (output / "native-corridors-manifest.json").write_text(json.dumps(manifest, indent=2)+"\n", encoding="utf-8")


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("kit", type=Path)
    parser.add_argument("output", type=Path)
    parser.add_argument("--only", nargs="*")
    parser.add_argument("--compose-only", action="store_true")
    parser.add_argument("--ocean", type=Path, help="Private ocean maps from export_corridor_water.py")
    parser.add_argument("--bands", nargs="+", choices=("ground","sky","vfar","far","wall","road"),
                        help="Re-render only these passes with the same layout; preserve other completed passes")
    args = parser.parse_args(sys.argv[sys.argv.index("--")+1:] if "--" in sys.argv else sys.argv[1:])
    kit, output = args.kit.resolve(), args.output.resolve()
    repo = Path(__file__).resolve().parents[1]
    if output == repo or repo in output.parents:
        raise ValueError("Private game artwork must stay outside Git")
    if args.compose_only:
        compose(output)
        return
    if args.ocean and args.bands and "ground" not in args.bands:
        raise ValueError("Ocean resources require a ground re-render")
    sys.path.insert(0, str(kit / "tools"))
    import bpy
    import numpy as np
    import bl_lib as L
    configure_materials(L)
    src = kit / "roads"
    output.mkdir(parents=True, exist_ok=True)
    passes = output / "passes"
    passes.mkdir(exist_ok=True)
    entries = []
    for region, road in ROADS.items():
        shade = PALETTES[region][0]
        sizes, bottoms = {}, {}
        for folder in sorted(src.iterdir()):
            if region not in folder.name or not (folder / "scene.json").is_file():
                continue
            lo, hi = L.bbox(folder)
            size = hi-lo
            if np.all(np.isfinite(size)) and size.max() < 1e6:
                sizes[folder.name], bottoms[folder.name] = size, float(lo[1])
        walls = [n for n,s in sizes.items() if wall_asset(n,s)]
        distant = [n for n,s in sizes.items() if "skirt" in n and "skybox" not in n and s[1] >= 6 and s[0] <= 60]
        skies = [n for n in sizes if n.startswith("skybox") and "long" in n] or [n for n in sizes if n.startswith("skybox")]
        floor_folder, floor_name = FLOORS[region]
        folder = kit / floor_folder
        document = json.loads((folder / "scene.json").read_text())
        key, floor_data = next((k,v) for k,v in document["materials"].items() if v["name"] == floor_name)
        detail_path, detail_name, uv_scale, coverage = GROUND_DETAILS[region]
        detail_folder = kit / detail_path
        detail_data = next(m for m in json.loads((detail_folder / "scene.json").read_text())["materials"].values()
                           if m["name"] == detail_name)
        detail_paint = (detail_folder, detail_data)
        for variant in range(1,7):
            short = f"{region}-{variant:02d}"
            if args.only and short not in args.only:
                continue
            seed = variant*1000 + len(region)
            rng = random.Random(seed)
            rw = sizes[road]
            placements = []
            if args.bands or args.ocean and region == "coast" or not (passes / f"{short}-complete.json").is_file():
                L.reset()
                def put(asset, x, z, rotation, band, light, scale=1, ground=False):
                    matrix, _ = L.place_matrix(src / asset, (x, bottoms[asset], z), rotation, scale=scale)
                    # The distant Tangle skirts carry raised trees on native
                    # earth banks. Retain those banks behind the walking floor;
                    # removing them leaves rectangular roots hanging in midair.
                    drops = L.DROP_MATS[:]
                    if region == "forest" and band in ("far", "vfar"):
                        L.DROP_MATS[:] = [name for name in drops if name != "forest_ground_dark"]
                    try:
                        objects, lo, _ = L.build(src / asset, matrix, shade=light)
                    finally:
                        L.DROP_MATS[:] = drops
                    fragment = "foliage" if region == "forest" else "coast_deadfish" if asset == "coast_tile_07" else None
                    if ground and fragment:
                        objects = native_parts(objects, fragment)
                        if objects:
                            lo = np.min([v.co[:] for obj in objects for v in obj.data.vertices],axis=0)
                    if ground and (not objects or lo[1] < 1):
                        raise ValueError(f"Empty verge or scenery entering the actor lane: {asset}")
                    # Some native foliage sits above a discarded driving foundation.
                    # Keep buried roots as authored; lower only raised bases to the lane.
                    sink = float(lo[2]+.12) if objects and (ground or region == "forest" and lo[2] > 0) else 0
                    if sink:
                        for obj in objects:
                            for vertex in obj.data.vertices:
                                vertex.co.z -= sink
                    placements.append({"asset": asset, "x": x, "z": z, "rotation": rotation, "band": band,
                                       "scale": scale, "grounding_offset": -sink})
                    for obj in objects:
                        obj["band"] = band
                        if band in ("far", "vfar", "wall"):
                            # Skirt meshes have a straight bottom. Fade only their lowest 3 world units,
                            # joining the continuous native ground without blurring the skyline.
                            for slot in obj.material_slots:
                                mat = slot.material
                                if mat.get("crawl_base_fade"):
                                    continue
                                mat["crawl_base_fade"] = True
                                nt = mat.node_tree
                                out = next(n for n in nt.nodes if n.type == "OUTPUT_MATERIAL")
                                surface = out.inputs["Surface"].links[0].from_socket
                                position = nt.nodes.new("ShaderNodeNewGeometry")
                                separate = nt.nodes.new("ShaderNodeSeparateXYZ")
                                nt.links.new(position.outputs["Position"], separate.inputs[0])
                                fade = nt.nodes.new("ShaderNodeMapRange")
                                fade.interpolation_type = "SMOOTHSTEP"
                                fade.inputs["From Min"].default_value = -.1
                                fade.inputs["From Max"].default_value = .6 if band == "wall" else 3
                                nt.links.new(separate.outputs["Z"], fade.inputs["Value"])
                                mix = nt.nodes.new("ShaderNodeMixShader")
                                transparent = nt.nodes.new("ShaderNodeBsdfTransparent")
                                nt.links.new(fade.outputs[0], mix.inputs[0])
                                nt.links.new(transparent.outputs[0], mix.inputs[1])
                                nt.links.new(surface, mix.inputs[2])
                                nt.links.new(mix.outputs[0], out.inputs["Surface"])
                    return objects
                # Use one continuous actor lane rather than two rows of uneven driving
                # tiles. Their foundations and props were crossing the heroes' feet.
                def row(pool, z, band, light, start=-75, end=75, gap=(-3,1)):
                    pool = pool[:]
                    rng.shuffle(pool)
                    x, index = start, 0
                    while x < end and pool:
                        name = pool[index % len(pool)]
                        index += 1
                        rotation = rng.choice([0,0,180] if band == "wall" else [0,180])
                        size = sizes[name]
                        put(name, float(x+size[0]/2), float(z+size[2]/2), rotation, band, light)
                        x += float(size[0])+rng.uniform(*gap)
                wall_depth = max(2.8,rw[0]/2-4) if region == "city" else rw[0]/2-(4 if region in VERGES else 1)
                row(walls, wall_depth, "wall", shade)
                row(distant or walls, rw[0]/2+22, "far", shade*.8, gap=(-6,0))
                row(distant or walls, rw[0]/2+48, "vfar", shade*.6, -110,110,(-8,0))
                def planted_parts(asset, fragment, x, near_depth, rotation, scale, band, light):
                    # Whole driving tiles include broad discarded foundations.
                    # Anchor the retained tree/foliage itself, after rotation,
                    # so its roots actually close the visible ground near the lane.
                    part_folder = kit / asset if "/" in asset else src / asset
                    objects, _, _ = L.build(part_folder, shade=light)
                    objects = native_parts(objects, fragment)
                    vertices = [v for obj in objects for v in obj.data.vertices]
                    if not vertices:
                        raise ValueError(f"No native {fragment} in {asset}")
                    xyz = np.array([v.co[:] for v in vertices])
                    center = (xyz.min(0) + xyz.max(0)) / 2
                    xyz -= center
                    a = math.radians(rotation)
                    turn = np.array([[math.cos(a), math.sin(a), 0],
                                     [-math.sin(a), math.cos(a), 0], [0, 0, 1]])
                    xyz = xyz @ turn.T * scale
                    lo, hi = xyz.min(0), xyz.max(0)
                    xyz += np.array([x - (lo[0]+hi[0])/2, near_depth-lo[1], -.12-lo[2]])
                    for vertex, point in zip(vertices, xyz):
                        vertex.co = point
                    for obj in objects:
                        obj["band"] = band
                    placements.append({"asset": asset, "parts": fragment, "x": x, "z": near_depth,
                                       "rotation": rotation, "band": band, "scale": scale,
                                       "bounds": [xyz.min(0).tolist(), xyz.max(0).tolist()],
                                       "scene_sha256": digest(part_folder / "scene.json")})
                if region in INFILL:
                    infill_rng = random.Random(seed+31871)
                    def infill_row(pool, depth, gap, band, light):
                        x, previous = -82.0, None
                        while x < 82:
                            item = infill_rng.choice([p for p in pool if p != previous])
                            previous = item
                            name, fragment, low, high = item
                            planted_parts(name, fragment, x, infill_rng.uniform(*depth),
                                          infill_rng.choice((0,180))+infill_rng.uniform(-25,25),
                                          infill_rng.uniform(low,high), band, light)
                            x += infill_rng.uniform(*gap)
                    infill_row(INFILL[region], (4.0,7.0) if region == "farm" else (3.2,5.0),
                               {"city":(12,20),"farm":(6,10),"coast":(8,13)}[region], "wall", shade*.90)
                    infill_row(LITTER[region], (1.8,4.0), (3.2,6.0) if region == "farm" else (5.0,9.0),
                               "road", shade*(.88 if region == "farm" else .65))
                if region == "forest":
                    woodland_rng = random.Random(seed+91237)
                    trees = (("forest_tile_06", "forest_tree_large_02", .48, .68),
                             ("forest_tile_brambles_02", "forest_tree_large_02", .40, .58),
                             ("forest_tile_01", "forest_trees_03", .48, .72),
                             ("forest_tile_02", "forest_trees_05", .50, .74))
                    x = -82.0
                    previous_tree = None
                    while x < 82:
                        tree = woodland_rng.choice([t for t in trees if t != previous_tree])
                        previous_tree = tree
                        name, fragment, low, high = tree
                        scale = woodland_rng.uniform(low,high)
                        planted_parts(name, fragment, x, woodland_rng.uniform(3.2,6.0),
                                      woodland_rng.choice((0,180))+woodland_rng.uniform(-35,35),
                                      scale, "wall", shade*.94)
                        x += woodland_rng.uniform(3.2,5.8)
                    # Small native undergrowth fills the gaps between the trunks,
                    # rather than repeating one full-width strip of giant ferns.
                    x = -80.0
                    while x < 80:
                        planted_parts("forest_tile_11", "foliage", x, woodland_rng.uniform(1.8,5.0),
                                      woodland_rng.uniform(0,360), woodland_rng.uniform(.12,.25), "road", shade*.88)
                        x += woodland_rng.uniform(2.8,6.0)
                # Keep this RNG independent: adding a clump must not shuffle the
                # architecture or distant skyline. A shallow lane-edge row is
                # behind the actors, with a clear level path at depth zero.
                if region in VERGES:
                    verge_rng = random.Random(seed+73019)
                    x = -80.0
                    previous = None
                    while x < 80:
                        pool = [name for name in VERGES[region] if name != previous]
                        name = verge_rng.choice(pool)
                        previous = name
                        scale = verge_rng.uniform(.45,.70) if region != "farm" else verge_rng.uniform(.65,.90)
                        rotation = verge_rng.choice((0,180))
                        depth = verge_rng.uniform(3.0,4.5)
                        put(name, x, depth+float(sizes[name][2])*scale/2, rotation, "road", shade*.92,
                            scale=scale, ground=True)
                        x += float(sizes[name][0])*scale+verge_rng.uniform(1.5,5.0)
                if skies:
                    matrix, _ = L.place_matrix(src/skies[0], (0,-6,150), keep_vfx_mats=True)
                    objects, _, _ = L.build(src/skies[0], matrix, shade=shade, keep_vfx_mats=True)
                    for obj in objects:
                        obj["band"] = "sky"
                # Double native scenery to match the crawl's four-unit hero bodies.
                # Mesh vertices are already in world space in this export.
                for obj in bpy.data.objects:
                    if obj.type == "MESH":
                        for vertex in obj.data.vertices:
                            vertex.co *= ENVIRONMENT_SCALE
                ground = stage_floor(L, folder, key, floor_data, shade=shade,depth_end=16 if region == "coast" else 210)
                paint_settings = {"uv_scale":uv_scale,"coverage":coverage,"phase":0 if region == "forest" else variant}
                ground_contact(ground, detail_paint, shade, paint_settings)
                if region == "coast":
                    # The Shroud's far ground is open water, not an endless wooden pier.
                    # The base-colour kit omits this shader's cloud/foam maps.
                    # An optional private export supplies a restrained baked surface;
                    # old packs can still be reproduced with the flat-colour fallback.
                    if args.ocean:
                        sea = ocean_material(args.ocean.resolve(), variant)
                    else:
                        sea = bpy.data.materials.new("crawl_shroud_water_colour")
                        sea.use_nodes = True
                        nt = sea.node_tree
                        nt.nodes.clear()
                        out = nt.nodes.new("ShaderNodeOutputMaterial")
                        emission = nt.nodes.new("ShaderNodeEmission")
                        emission.inputs["Color"].default_value = tuple((c/255)**2.2 for c in (9,13,16))+(1,)
                        nt.links.new(emission.outputs[0], out.inputs[0])
                    # Ocean behind the pier; retain the common planked walking lane.
                    bpy.ops.mesh.primitive_plane_add(size=1, location=(0,166,-.06))
                    water = bpy.context.object
                    water.scale = (500,300,1)
                    water["band"] = "ground"
                    water.data.materials.append(sea)
                stage_camera(L, corridor=True, res=(W,H))
                for band in ("ground","sky","vfar","far","wall","road"):
                    if args.bands and band not in args.bands:
                        continue
                    visible = False
                    for obj in bpy.data.objects:
                        if obj.type == "MESH":
                            # Hide other scenery from the ground camera, but let its
                            # geometry cast local contact into the original floor paint.
                            obj.hide_render = band != "ground" and obj.get("band") != band
                            obj.visible_camera = obj.get("band") == band
                            visible = visible or obj.visible_camera
                    if visible:
                        L.render(passes/f"{short}-{band}.png", samples=12 if band == "ground" else 4, transparent=True)
                (passes/f"{short}-placements.json").write_text(json.dumps(placements, indent=2), encoding="utf-8")
                (passes/f"{short}-complete.json").write_text(json.dumps({"camera":camera_settings(True),"environment_scale":ENVIRONMENT_SCALE}))
            entries.append({"file": f"dd2_{region}-corridor-{variant:02d}.png", "short": short,
                            "region": "dd2_"+region, "seed": seed, "size": [W,H],
                            "road_scene_sha256": digest(src/road/"scene.json"),
                            "floor_material": floor_name, "floor_texture_sha256": digest((folder/floor_data["texture"]).resolve()),
                            "placements_sha256": digest(passes/f"{short}-placements.json")})
            entries[-1]["ground_detail_material"] = detail_data["name"]
            entries[-1]["ground_detail_texture_sha256"] = digest((detail_folder/detail_data["texture"]).resolve())
            entries[-1]["ground_detail_world_period"] = 8/uv_scale
            entries[-1]["ground_detail_phase"] = 0 if region == "forest" else variant
            if region == "coast" and args.ocean:
                resources = args.ocean.resolve()
                entries[-1]["ocean_resources"] = str(resources)
                entries[-1]["ocean_resources_sha256"] = digest(resources / "materials.json")
                entries[-1]["ocean_phase"] = variant
                entries[-1]["ocean_render"] = "Baked native cloud/foam maps; no animated water shader"
            print("READY", short, flush=True)
    manifest = {"version": 4, "generated_art": False, "source_kit": str(kit),
                "tool_sha256": digest(Path(__file__).resolve()), "files": entries,
                "camera_tool_sha256": digest(Path(__file__).with_name("scenery_camera.py")),
                "changes": ["Shared hero-calibrated room/corridor stage camera", "4096x768 lateral view without architecture repetition",
                            "Continuous level native ground with shared world UVs", "Native environment doubled for four-unit heroes",
                            "Native floor paint with three-unit local geometry contact",
                            "Broad subtle floor value variation preserves the native paint and shared UV scale",
                            "Discard empty Tangle foundations and ground raised foliage bases",
                            "Closer rear scenery and irregular native foliage/rubble verges behind the actor lane",
                            "Near Tangle trunks rooted from retained mesh bounds and small irregular undergrowth",
                            "Native Tangle root paint breaks parallel road strips at a different world period",
                            "Region-specific rubble, earthen footpath and worn-board paint with independent variant patch phases",
                            "Closer Sprawl masonry, Foetor crop banks and Shroud wreckage with low native roadside details"],
                "camera": camera_settings(True), "environment_scale":ENVIRONMENT_SCALE,
                "limits": ["Unlit native albedo with approximate depth haze; native deferred lighting is not reproduced"]}
    manifest_path = output/"render-manifest.json"
    if args.ocean:
        manifest["changes"].append("Native ocean cloud/foam detail replaces the Shroud's flat water colour")
    if args.only and manifest_path.is_file():
        previous = json.loads(manifest_path.read_text())
        if previous.get("camera") != manifest["camera"] or previous.get("source_kit") != str(kit):
            raise ValueError("Partial re-render must use the same camera and source kit; use a fresh output folder")
        manifest["files"] = merge_entries(previous["files"],entries)
    manifest_path.write_text(json.dumps(manifest,indent=2)+"\n", encoding="utf-8")


if __name__ == "__main__":
    main()
