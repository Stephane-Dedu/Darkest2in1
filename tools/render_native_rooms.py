"""Blender: render private DD2 arenas for the crawl camera, supplying native foreground ground.

blender -b --python tools/render_native_rooms.py -- <kit> <output> [--only coast_dungeon_interior ...]
Requires the owner's extracted kit and its bl_lib.py; no game artwork is distributed.
Then run Python with Pillow/NumPy and the same arguments plus --compose-only.
"""
import argparse
import hashlib
import json
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))
from scenery_camera import ENVIRONMENT_SCALE, create as stage_camera, floor as stage_floor, settings as camera_settings, configure_materials
from room_floor import source as floor_source, paint as floor_paint

SCENES = {"boss-01": "dungeon_interior", "entrance-01": "dungeon_exterior",
          "battle-01": "resist", "battle-02": "faction", "battle-03": "military", "battle-04": "cultist",
          "quiet-01": "gaunt", "treasure-01": "pillager"}
OVERRIDES = {"coast": {"battle-04": "gaunt_chirurgeon"}}  # Shroud cultist export contains no encounter landmark.
PALETTES = {
    "city": (1.5, (22, 10, 8), (120, 52, 26), (84, 40, 26)),
    "farm": (1.5, (34, 30, 18), (128, 112, 60), (96, 86, 48)),
    "forest": (2.4, (10, 14, 22), (40, 58, 70), (34, 52, 60)),
    "coast": (1.7, (12, 20, 28), (64, 92, 104), (52, 76, 88))}
FLOORS = {
    "city": ("roads/road_city_straight_single", "road_city_cobblestone_01"),
    "farm": ("roads/road_farm_straight_single", "road_farm_01"),
    "forest": ("roads/road_forest_straight_single", "forest_road_01"),
    "coast": ("arenas/scenes_scenes_combat_arena_coast_dungeon_interior", "coast_road_02_boards_combat")}


def digest(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def compose(output):
    import numpy as np
    from PIL import Image
    manifest = json.loads((output / "render-manifest.json").read_text())
    w, h = 2880, 1080
    for entry in manifest["files"]:
        target = output / entry["file"]
        _, top, bottom, haze = PALETTES[entry["region"].removeprefix("dd2_")]
        gradient = np.linspace(0, 1, h)[:, None, None] ** .8
        image = Image.fromarray((np.array(top) * (1 - gradient) + np.array(bottom) * gradient)
                               .astype(np.uint8).repeat(w, 1), "RGB").convert("RGBA")
        for band, fog in (("ground", .32), ("far", .5), ("mid", .22), ("near", 0)):
            if band not in entry["passes"]:
                continue
            path = output / "render-passes" / f"{entry['short']}-{band}.png"
            if not path.is_file():
                raise FileNotFoundError(path)
            layer = Image.open(path).convert("RGBA")
            if layer.size != (w, h):
                raise ValueError(f"Invalid render pass: {path}")
            if fog:
                pixels = np.array(layer).astype(np.float32)
                strength = fog
                if band == "ground":
                    # Suppress the far texture frequency and plane's distant edge,
                    # while leaving the actor footline and foreground native ink sharp.
                    strength = fog * np.clip((680 - np.arange(h) / 1.5) / 200, 0, 1)[:, None, None]
                    pixels[..., 3] *= np.clip((np.arange(h) / 1.5 - 450) / 80, 0, 1)[:, None]
                pixels[..., :3] = pixels[..., :3] * (1 - strength) + np.array(haze) * strength
                layer = Image.fromarray(pixels.clip(0, 255).astype(np.uint8), "RGBA")
            image.alpha_composite(layer)
        image.convert("RGB").save(target)
        with Image.open(target) as check:
            if check.size != (w, h) or check.mode != "RGB" or target.stat().st_size > 16 * 1024 * 1024:
                raise ValueError(f"Invalid output: {target}")
            check.load()
        entry["sha256"] = digest(target)
    (output / "native-rooms-manifest.json").write_text(json.dumps(manifest, indent=2) + "\n", encoding="utf-8")
    print(f"Prepared {len(manifest['files'])} native room scenes in {output}")


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("kit", type=Path)
    parser.add_argument("output", type=Path)
    parser.add_argument("--only", nargs="*")
    parser.add_argument("--compose-only", action="store_true")
    args = parser.parse_args(sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else sys.argv[1:])
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

    # Validate every selected dependency before publishing any files.
    sources, floors = [], {}
    for region in PALETTES:
        for role, kind in SCENES.items():
            kind = OVERRIDES.get(region, {}).get(role, kind)
            short = f"{region}_{kind}"
            if args.only and short not in args.only:
                continue
            folder = kit / "arenas" / f"scenes_scenes_combat_arena_{short}"
            if not (folder / "scene.json").is_file():
                raise FileNotFoundError(folder)
            floors[(region, kind)] = floor_source(kit, region, kind)
            sources.append((region, role, kind, folder))
    output.mkdir(parents=True, exist_ok=True)
    temp = output / "render-passes"
    temp.mkdir(exist_ok=True)
    entries = []
    w, h = 2880, 1080
    for region, role, kind, folder in sources:
        target = output / f"dd2_{region}-arena-{role}.png"
        shade, top, bottom, haze = PALETTES[region]
        floor_folder, floor_key, floor_data, floor_texture, period = floors[(region, kind)]
        floor_shade = shade * .35 if "sand_" in floor_data["name"] else shade
        phase = list(SCENES).index(role) + 1
        completion = temp / f"{region}-{kind}-complete.json"
        recipe = {"scene": digest(folder / "scene.json"), "floor": digest(floor_texture),
                  "renderer": digest(Path(__file__)), "floor_tool": digest(Path(__file__).with_name("room_floor.py")),
                  "camera": digest(Path(__file__).with_name("scenery_camera.py")), "phase": phase, "floor_period": period}
        if not completion.is_file() or json.loads(completion.read_text()) != recipe:
            completion.unlink(missing_ok=True)
            for band in ("ground", "far", "mid", "near"):
                (temp / f"{region}-{kind}-{band}.png").unlink(missing_ok=True)
            L.reset()
            objects, lo, hi = L.build(folder, place=np.diag([ENVIRONMENT_SCALE]*3+[1]), respect_active=True, shade=shade)
            if not objects:
                raise ValueError(f"Empty native arena: {folder}")
            # The uniform albedo lift used for dark timber overexposes the
            # Shroud's pale sand. Preserve its native grain/UVs under a lower
            # exposure, also used by the foreground extension.
            for material in bpy.data.materials:
                if material.name.startswith("coast_road_sand_") and material.use_nodes:
                    for node in material.node_tree.nodes:
                        if node.type == "MIX_RGB" and node.blend_type == "MULTIPLY":
                            tint = node.inputs[2].default_value
                            node.inputs[2].default_value = tuple(v * .35 for v in tint[:3]) + (1,)
            for obj in objects:
                distance = float(np.mean([c[1] for c in obj.bound_box]))
                obj["band"] = "far" if distance > 90 else "mid" if distance > 28 else "near"
            # Driving lanes and board atlases are not seamless room floors. Keep
            # native arena geometry/UVs and extend its foreground with regional paint.
            ground = stage_floor(L, floor_folder, floor_key, floor_data, shade=shade, band="ground")
            floor_paint(ground, floor_folder, floor_data, floor_shade, period, phase)
            stage_camera(L, res=(w,h))
            for band in ("ground", "far", "mid", "near"):
                visible = False
                for obj in bpy.data.objects:
                    if obj.type == "MESH":
                        obj.hide_render = band != "ground" and obj.get("band") != band
                        obj.visible_camera = obj.get("band") == band
                        visible = visible or obj.visible_camera
                if not visible:
                    continue
                path = temp / f"{region}-{kind}-{band}.png"
                L.render(path, samples=8, transparent=True)
            completion.write_text(json.dumps(recipe))
        entries.append({"file": target.name, "region": "dd2_" + region, "role": role.split("-")[0],
                        "short": region + "-" + kind,
                        "passes": [band for band in ("ground", "far", "mid", "near") if (temp / f"{region}-{kind}-{band}.png").is_file()],
                        "native_arena": folder.name, "source_scene_sha256": digest(folder / "scene.json"),
                        "floor_material": floor_data["name"], "floor_texture_sha256": digest(floor_texture),
                        "floor_periods": [period, period * 1.37, period * .83], "floor_phase": phase,
                        "floor_shade": floor_shade,
                        "size": [w, h]})
        print(f"READY {target.name}", flush=True)
    manifest = {"version": 3, "generated_art": False, "source_kit": str(kit),
                "render_tool_sha256": digest(Path(__file__).resolve()),
                "camera_tool_sha256": digest(Path(__file__).with_name("scenery_camera.py")),
                "floor_tool_sha256": digest(Path(__file__).with_name("room_floor.py")),
                "foreground_plane": {"center_blender": [0, 60, -.04], "size": [500, 300],
                                     "mapping": "three regional paint samples with independent periods and irregular world joins"},
                "camera": camera_settings(), "environment_scale": ENVIRONMENT_SCALE,
                "virtual_scene": [1920, 720], "hero_foot_y": 680,
                "limits": ["Native base-colour/tint with approximate depth haze; no native lighting or animated VFX",
                           "Native regional paint on an authored foreground plane; approximate contact shading and distance haze",
                           "Room purpose selects an environment, not the actual DD2 encounter or boss"],
                "files": entries}
    (output / "render-manifest.json").write_text(
        json.dumps(manifest, indent=2) + "\n", encoding="utf-8")
    print(f"Prepared {len(entries)} native room scenes in {output}", flush=True)


if __name__ == "__main__":
    main()
