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
        walls = [n for n,s in sizes.items() if ("_tile_" in n or "_decor_" in n) and "node" not in n
                 and not n.startswith("road") and s[1] >= 6 and s[0] <= 40]
        distant = [n for n,s in sizes.items() if "skirt" in n and "skybox" not in n and s[1] >= 6 and s[0] <= 60]
        skies = [n for n in sizes if n.startswith("skybox") and "long" in n] or [n for n in sizes if n.startswith("skybox")]
        floor_folder, floor_name = FLOORS[region]
        folder = kit / floor_folder
        document = json.loads((folder / "scene.json").read_text())
        key, floor_data = next((k,v) for k,v in document["materials"].items() if v["name"] == floor_name)
        for variant in range(1,7):
            short = f"{region}-{variant:02d}"
            if args.only and short not in args.only:
                continue
            seed = variant*1000 + len(region)
            rng = random.Random(seed)
            rw = sizes[road]
            placements = []
            if args.bands or not (passes / f"{short}-complete.json").is_file():
                L.reset()
                def put(asset, x, z, rotation, band, light):
                    matrix, _ = L.place_matrix(src / asset, (x, bottoms[asset], z), rotation)
                    objects, _, _ = L.build(src / asset, matrix, shade=light)
                    placements.append({"asset": asset, "x": x, "z": z, "rotation": rotation, "band": band})
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
                row(walls, rw[0]/2-1, "wall", shade)
                row(distant or walls, rw[0]/2+22, "far", shade*.8, gap=(-6,0))
                row(distant or walls, rw[0]/2+48, "vfar", shade*.6, -110,110,(-8,0))
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
                if region == "coast":
                    # The Shroud's far ground is open water, not an endless wooden pier.
                    # Its native ocean shader has no diffuse map in this export: use the
                    # region's dark water colour here, with native moving mist at runtime.
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
                            obj.hide_render = obj.get("band") != band
                            visible = visible or not obj.hide_render
                    if visible:
                        L.render(passes/f"{short}-{band}.png", samples=4, transparent=True)
                (passes/f"{short}-placements.json").write_text(json.dumps(placements, indent=2), encoding="utf-8")
                (passes/f"{short}-complete.json").write_text(json.dumps({"camera":camera_settings(True),"environment_scale":ENVIRONMENT_SCALE}))
            entries.append({"file": f"dd2_{region}-corridor-{variant:02d}.png", "short": short,
                            "region": "dd2_"+region, "seed": seed, "size": [W,H],
                            "road_scene_sha256": digest(src/road/"scene.json"),
                            "floor_material": floor_name, "floor_texture_sha256": digest((folder/floor_data["texture"]).resolve())})
            print("READY", short, flush=True)
    manifest = {"version": 4, "generated_art": False, "source_kit": str(kit),
                "tool_sha256": digest(Path(__file__).resolve()), "files": entries,
                "camera_tool_sha256": digest(Path(__file__).with_name("scenery_camera.py")),
                "changes": ["Shared hero-calibrated room/corridor stage camera", "4096x768 lateral view without architecture repetition",
                            "Continuous level native ground with shared world UVs", "Native environment doubled for four-unit heroes"],
                "camera": camera_settings(True), "environment_scale":ENVIRONMENT_SCALE,
                "limits": ["Unlit native albedo with approximate depth haze; native deferred lighting is not reproduced"]}
    (output/"render-manifest.json").write_text(json.dumps(manifest,indent=2)+"\n", encoding="utf-8")


if __name__ == "__main__":
    main()
