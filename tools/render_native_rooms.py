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
        for band, fog in (("far", .5), ("mid", .22), ("near", 0)):
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
                pixels[..., :3] = pixels[..., :3] * (1 - fog) + np.array(haze) * fog
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
        folder, name = FLOORS[region]
        floor_folder = kit / folder
        document = json.loads((floor_folder / "scene.json").read_text())
        key, material = next((k, v) for k, v in document["materials"].items() if v["name"] == name)
        texture_path = (floor_folder / material["texture"]).resolve()
        if not texture_path.is_file():
            raise FileNotFoundError(texture_path)
        floors[region] = (floor_folder, key, material, texture_path)
        for role, kind in SCENES.items():
            kind = OVERRIDES.get(region, {}).get(role, kind)
            short = f"{region}_{kind}"
            if args.only and short not in args.only:
                continue
            folder = kit / "arenas" / f"scenes_scenes_combat_arena_{short}"
            if not (folder / "scene.json").is_file():
                raise FileNotFoundError(folder)
            sources.append((region, role, kind, folder))
    output.mkdir(parents=True, exist_ok=True)
    temp = output / "render-passes"
    temp.mkdir(exist_ok=True)
    entries = []
    w, h = 2880, 1080
    for region, role, kind, folder in sources:
        target = output / f"dd2_{region}-arena-{role}.png"
        shade, top, bottom, haze = PALETTES[region]
        floor_folder, floor_key, floor_data, floor_texture = floors[region]
        if not (temp / f"{region}-{kind}-near.png").is_file():
            L.reset()
            objects, lo, hi = L.build(folder, place=np.diag([ENVIRONMENT_SCALE]*3+[1]), respect_active=True, shade=shade)
            if not objects:
                raise ValueError(f"Empty native arena: {folder}")
            for obj in objects:
                distance = float(np.mean([c[1] for c in obj.bound_box]))
                obj["band"] = "far" if distance > 90 else "mid" if distance > 28 else "near"
            # Combat exports omit the foreground ground or end their pier before the crawl's footline.
            # Extend the same region's installed ground material under the actors, below existing geometry.
            stage_floor(L, floor_folder, floor_key, floor_data, shade=shade, band="near")
            stage_camera(L, res=(w,h))
            for band, fog in (("far", .5), ("mid", .22), ("near", 0)):
                visible = False
                for obj in bpy.data.objects:
                    if obj.type == "MESH":
                        obj.hide_render = obj.get("band") != band
                        visible = visible or not obj.hide_render
                if not visible:
                    continue
                path = temp / f"{region}-{kind}-{band}.png"
                L.render(path, samples=8, transparent=True)
        entries.append({"file": target.name, "region": "dd2_" + region, "role": role.split("-")[0],
                        "short": region + "-" + kind,
                        "passes": [band for band in ("far", "mid", "near") if (temp / f"{region}-{kind}-{band}.png").is_file()],
                        "native_arena": folder.name, "source_scene_sha256": digest(folder / "scene.json"),
                        "floor_material": floor_data["name"], "floor_texture_sha256": digest(floor_texture),
                        "size": [w, h]})
        print(f"READY {target.name}", flush=True)
    manifest = {"version": 2, "generated_art": False, "source_kit": str(kit),
                "render_tool_sha256": digest(Path(__file__).resolve()),
                "camera_tool_sha256": digest(Path(__file__).with_name("scenery_camera.py")),
                "foreground_plane": {"center_blender": [0, 60, -.04], "size": [500, 300], "world_uv_repeat_units": 8},
                "camera": camera_settings(), "environment_scale": ENVIRONMENT_SCALE,
                "virtual_scene": [1920, 720], "hero_foot_y": 680,
                "limits": ["Native base-colour/tint with approximate depth haze; no native lighting or animated VFX",
                           "Native road/deck material extended on an authored foreground plane",
                           "Room purpose selects an environment, not the actual DD2 encounter or boss"],
                "files": entries}
    (output / "render-manifest.json").write_text(
        json.dumps(manifest, indent=2) + "\n", encoding="utf-8")
    print(f"Prepared {len(entries)} native room scenes in {output}", flush=True)


if __name__ == "__main__":
    main()
