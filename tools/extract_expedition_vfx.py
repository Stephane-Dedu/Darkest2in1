"""Extract a bounded, private DD2 expedition VFX pack from an owned install.

Usage: python extract_expedition_vfx.py GAME PRIVATE_OUTPUT
Requires UnityPy, Pillow and lz4 (the local universal-modder environment supplies
them). The output must stay outside this Git checkout. No source art is bundled.
"""
from __future__ import annotations

import argparse
import hashlib
import json
import lzma
from pathlib import Path
import struct

import lz4.block
import UnityPy
from PIL import Image, ImageChops


TARGETS = {
    "city": [
        "VFX_Fire_Smoke_Small_01.prefab",
        "vfx_burningcity_fog_plane_orange_dim_01.mat",
        "vfx_burningcity_roadevent_smoke_particle_02.mat",
        "vfx_burningcity_torch_fire_soft_sim_02.mat",
    ],
    "coast": ["vfx_coast_fog_plane_01.mat", "vfx_coast_fog_plane_03.mat"],
    "farm": [
        "vfx_farm_fog_plane_01.mat", "vfx_farm_steam_fog_01.mat",
        "vfx_Steam_Smoke_Sphere_01_Prefab.prefab",
    ],
    "forest": [
        "vfx_forest_fog_plane_01.mat", "vfx_particles_forest_color_smoke.prefab",
    ],
}


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def zstring(stream):
    value = bytearray()
    while (char := stream.read(1)) not in (b"", b"\0"):
        value.extend(char)
    return value.decode()


def bundle_nodes(path):
    """Read only a UnityFS directory, without expanding its texture data."""
    with path.open("rb") as stream:
        if zstring(stream) != "UnityFS":
            return []
        version = struct.unpack(">I", stream.read(4))[0]
        zstring(stream)
        zstring(stream)
        size, compressed, expanded, flags = struct.unpack(">QIII", stream.read(20))
        if version >= 7:
            stream.seek((stream.tell() + 15) // 16 * 16)
        if flags & 128:
            stream.seek(size - compressed)
        data = stream.read(compressed)
        if (flags & 63) in (2, 3):
            data = lz4.block.decompress(data, uncompressed_size=expanded)
        elif (flags & 63) == 1:
            data = lzma.decompress(data)
        elif flags & 63:
            raise ValueError(f"Unsupported UnityFS directory compression: {path}")
        count = struct.unpack_from(">i", data, 16)[0]
        offset = 20 + count * 10
        count = struct.unpack_from(">i", data, offset)[0]
        offset += 4
        result = []
        for _ in range(count):
            offset += 20
            end = data.index(0, offset)
            result.append(data[offset:end].decode())
            offset = end + 1
        return result


def runtime_pack(document, out):
    """Convert native shader masks into a small, straight-alpha GUI pack.

    Native sheets use opaque Texture2D alpha and _RGBAlpha=1. We keep source
    RGB and copy its red mask into alpha; DD2's HDR shader, distortion, depth
    fade and camera fade are intentionally not reconstructed by this pack.
    """
    textures = {t["name"]: t for t in document["textures"]}
    materials = {(m["region"], m["name"]): m for m in document["materials"]}

    def image(name):
        return Image.open(out / textures[name]["file"]).convert("RGBA")

    files = {}

    def publish(file, source, limit=1024, mask=None, keep_alpha=False):
        value = image(source)
        value.thumbnail((limit, limit), Image.Resampling.LANCZOS)
        conversion = "preserve native RGBA"
        if not keep_alpha:
            alpha = value.getchannel("R")
            conversion = "preserve RGB; alpha = native texture red channel"
            if mask:
                gradient = image(mask).resize(value.size, Image.Resampling.BILINEAR)
                alpha = ImageChops.multiply(alpha, gradient.getchannel("R"))
                conversion += " multiplied by native gradient mask red channel"
            value.putalpha(alpha)
        value.save(out / file)
        files[file] = {
            "file": file, "size": list(value.size), "sha256": sha(out / file),
            "source_texture": textures[source], "conversion": conversion,
            "source_gradient_mask": textures[mask] if mask else None,
            "alpha_range": list(value.getextrema()[3]),
        }

    publish("mist.png", "tiling_fractal_05", mask="VFX_Mask_HalfOval_01")
    publish("fire.png", "T_VFX_Sim_Fire_Soft_Loop_03")
    publish("smoke.png", "T_VFX_Sim_Smoke_Torch_slow_loop_03")
    publish("ember.png", "vfx_spark_01", keep_alpha=True)
    publish("gas.png", "vfx_common_smoke_softwash_01")

    city = document["prefabs"][0]
    fire = next(p for p in city["particles"] if "fire_plasm" in p["name"])
    smoke = next(p for p in city["particles"] if "smoke_slow" in p["name"])
    ember = next(p for p in city["particles"] if "spark_flying" in p["name"])
    gas = next(p for p in document["prefabs"] if p["region"] == "farm")["particles"][0]
    forest = next(p for p in document["prefabs"] if p["region"] == "forest")["particles"][0]
    coast = materials[("coast", "vfx_coast_fog_plane_01")]

    def gradient(particle):
        return particle["color"]["gradient"]["maxGradient"]["key0"]

    def effect(id, file, columns, rows, fps, source, native, animation):
        record = next((p for p in document["prefabs"] if p["source"] == source), None)
        if record is None:
            record = next(m for m in document["materials"] if m.get("source") == source)
        return {"id": id, "file": file, "columns": columns, "rows": rows,
                "frames": columns * rows, "fps": fps, "source": source,
                "source_bundle": record["source_bundle"], "source_path_id": str(record["path_id"]),
                "row_order": "top-left row-major", "animation": animation,
                "native": native}

    effects = [
        effect("mist", "mist.png", 1, 1, 0, coast["source"],
               {"color_1": coast["colors"]["_Color1"],
                "color_2": coast["colors"]["_Color2"],
                "opacity": coast["floats"]["_Alpha"],
                "fog_opacity": coast["floats"]["_Fog_Alpha"],
                "pan_uv_per_second": [-0.1, 0], "tiling": [1.5, 1.0]},
               "Pan/fade a masked native fog texture; no flipbook"),
        effect("fire", "fire.png", 8, 8, fire["uv"]["fps"], city["source"],
               {"particle": fire["name"], "tint": gradient(fire),
                "material": fire["materials"][0], "random_start_normalized": [0, .9375],
                "life_seconds": 60, "shader_rgb_alpha": True},
               "Loop 64 native simulation frames at 30 FPS; random initial phase"),
        effect("smoke", "smoke.png", 8, 8, smoke["uv"]["fps"], city["source"],
               {"particle": smoke["name"], "tint": gradient(smoke),
                "material": smoke["materials"][0], "random_start_normalized": [0, .9375],
                "life_seconds": 60, "shader_rgb_alpha": True},
               "Loop 64 native simulation frames at 20 FPS; random initial phase"),
        effect("ember", "ember.png", 2, 2, 0, city["source"],
               {"particle": ember["name"], "start_color": ember["initial"]["startColor"]["maxColor"],
                "material": ember["materials"][0], "life_seconds_range": [2, 3],
                "size_unity_range": [.05, .12], "emission_per_second_range": [2, 10],
                "stored_color_gradient_enabled": False},
               "Choose one of four native spark shapes per particle; drift upward/fade, no FPS animation"),
        effect("gas", "gas.png", 2, 2, 0,
               next(p["source"] for p in document["prefabs"] if p["region"] == "farm"),
               {"tint": gradient(gas), "material": gas["materials"][0],
                "life_seconds": gas["initial"]["startLifetime"]["scalar"],
                "emission_per_second": gas["emission"]["rateOverTime"]["scalar"],
                "alpha_peak": .8627451062, "alpha_peak_at_life": 20104 / 65535,
                "sheet_frame_curve_constant": .9998999834},
               "Choose a static cloud quadrant per particle, expand/drift/fade over 5s; no native flipbook"),
        effect("forest", "gas.png", 2, 2, 0,
               next(p["source"] for p in document["prefabs"] if p["region"] == "forest"),
               {"tint": gradient(forest), "material": forest["materials"][0],
                "life_seconds": forest["initial"]["startLifetime"]["scalar"],
                "emission_per_second": forest["emission"]["rateOverTime"]["scalar"],
                "alpha_peak": .8627451062, "alpha_peak_at_life": 20104 / 65535,
                "sheet_frame_curve_constant": .9998999834},
               "Same native cloud texture as Farm, with native Forest particle colors; no native flipbook"),
    ]
    manifest = {
        "format_version": 1, "no_generated_art": True,
        "effects": effects, "files": list(files.values()),
        "decoded_rgba_bytes_no_mips": sum(v["size"][0] * v["size"][1] * 4 for v in files.values()),
        "uv_convention": "PNG top-left row-major; Unity GUI UV y = 1 - (row + 1) / rows",
        "limitations": [
            "IMGUI straight-alpha approximation; native HDR, bloom, depth/camera fade and distortion are absent",
            "Row-major orientation inferred from upright decoded sheets and Unity UV grid; not live-captured native particle timing",
            "Native Foetor steam is warm brown/pink; toxic green is an authored map tint, not observed in these native prefabs",
            "Six effects share five textures; Gas and Forest use the same native 2x2 shape atlas with different particle colors",
        ],
        "source_bundle_sha256": document["bundles"],
    }
    (out / "vfx-manifest.json").write_text(json.dumps(manifest, indent=2))
    return manifest


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("game", type=Path)
    parser.add_argument("output", type=Path)
    args = parser.parse_args()
    aa = args.game.resolve() / "Darkest Dungeon II_Data/StreamingAssets/aa"
    out = args.output.resolve()
    repo = Path(__file__).resolve().parents[1]
    if out == repo or repo in out.parents:
        parser.error("Extracted game art must stay outside the Git checkout")
    if not aa.is_dir():
        parser.error("The selected game has no Addressables directory")
    out.mkdir(parents=True, exist_ok=True)
    raw = out / "source-textures"
    raw.mkdir(exist_ok=True)
    evidence = out / "serialized-evidence"
    evidence.mkdir(exist_ok=True)
    cab = {}
    for path in aa.rglob("*.bundle"):
        for node in bundle_nodes(path):
            cab[node.lower()] = path
    (out / "bundle-cab-index.json").write_text(json.dumps(
        {key: str(value.relative_to(aa)) for key, value in cab.items()}, indent=2))
    document = {"game": str(args.game.resolve()), "no_generated_art": True,
                "materials": [], "prefabs": [], "textures": [], "bundles": {}}
    exported = {}
    for region, targets in TARGETS.items():
        primary = aa / f"biome_{region}_assets_all.bundle"
        env = UnityPy.load(str(primary))
        loaded = {primary}

        def resolve(pointer):
            if pointer.m_FileID:
                external = pointer.assetsfile.externals[pointer.m_FileID - 1]
                file = cab.get(Path(external.path).name.lower())
                if file is not None and file not in loaded:
                    env.load_file(str(file))
                    loaded.add(file)
            return pointer.read()

        def material(pointer):
            value = resolve(pointer)
            record = {"region": region, "name": value.m_Name,
                      "path_id": pointer.m_PathID, "textures": []}
            material_bundle = cab.get(value.object_reader.assets_file.name.lower())
            record["source_bundle"] = str(material_bundle.relative_to(aa)) if material_bundle else None
            tree = value.object_reader.read_typetree()
            (evidence / f"{region}-{value.m_Name}-material.json").write_text(
                json.dumps(tree, indent=2))
            record["floats"] = dict(tree["m_SavedProperties"]["m_Floats"])
            record["colors"] = dict(tree["m_SavedProperties"]["m_Colors"])
            try:
                shader = resolve(value.m_Shader)
                record["shader"] = shader.m_ParsedForm.m_Name
            except Exception as exc:
                record["shader_unresolved"] = str(exc)
            for slot, tex in value.m_SavedProperties.m_TexEnvs:
                if not tex.m_Texture.m_PathID:
                    continue
                image_value = resolve(tex.m_Texture)
                tkey = f"{image_value.object_reader.assets_file.name}:{tex.m_Texture.m_PathID}"
                if tkey not in exported:
                    image = image_value.image.convert("RGBA")
                    name = f"{image_value.m_Name}-{tex.m_Texture.m_PathID}.png"
                    image.save(raw / name)
                    exported[tkey] = name
                    document["textures"].append({
                        "name": image_value.m_Name, "file": f"source-textures/{name}",
                        "path_id": tex.m_Texture.m_PathID, "size": list(image.size),
                        "source_bundle": str(cab[image_value.object_reader.assets_file.name.lower()].relative_to(aa)),
                        "alpha_range": list(image.getextrema()[3]),
                        "sha256": sha(raw / name),
                    })
                record["textures"].append({
                    "slot": slot, "file": f"source-textures/{exported[tkey]}",
                    "scale": [tex.m_Scale.x, tex.m_Scale.y],
                    "offset": [tex.m_Offset.x, tex.m_Offset.y],
                })
            document["materials"].append(record)
            return record

        for target in targets:
            matches = [(path, pointer) for path, pointer in env.container.items()
                       if path.endswith(target)]
            if len(matches) != 1:
                raise ValueError(f"Expected one source for {target}, found {len(matches)}")
            source, pointer = matches[0]
            if pointer.type.name == "Material":
                record = material(pointer)
                record["source"] = source
                print(region, target, [t["file"] for t in record["textures"]], flush=True)
                continue
            prefab = {"region": region, "source": source, "path_id": pointer.m_PathID,
                      "source_bundle": str(primary.relative_to(aa)), "particles": []}

            def walk(go, parent=""):
                name = f"{parent}/{go.m_Name}" if parent else go.m_Name
                components = list(go.m_Components)
                particle = next((c for c in components if c.type.name == "ParticleSystem"), None)
                renderer = next((c for c in components if c.type.name == "ParticleSystemRenderer"), None)
                if particle is not None:
                    tree = particle.read_typetree()
                    safe = name.replace("/", "_")
                    (evidence / f"{region}-{safe}-particle.json").write_text(json.dumps(tree, indent=2))
                    rec = {"name": name, "path_id": particle.m_PathID, "active": bool(go.m_IsActive),
                           "duration": tree["lengthInSec"], "simulation_speed": tree["simulationSpeed"],
                           "looping": tree["looping"], "initial": tree["InitialModule"],
                           "uv": tree["UVModule"], "emission": tree["EmissionModule"],
                           "velocity": tree["VelocityModule"], "noise": tree["NoiseModule"],
                           "color": tree["ColorModule"], "materials": []}
                    if renderer is not None:
                        rec["materials"] = [material(p)["name"] for p in renderer.read().m_Materials
                                            if p.m_PathID]
                    prefab["particles"].append(rec)
                for child in go.m_Transform.read().m_Children:
                    walk(child.read().m_GameObject.read(), name)

            walk(pointer.read())
            document["prefabs"].append(prefab)
            print(region, target, len(prefab["particles"]), "particle systems", flush=True)
        for bundle in loaded:
            document["bundles"][str(bundle.relative_to(aa))] = sha(bundle)
    (out / "native-vfx-evidence.json").write_text(json.dumps(document, indent=2))
    print("Extracted", len(document["textures"]), "native source textures", flush=True)
    manifest = runtime_pack(document, out)
    print("Prepared", len(manifest["effects"]), "effects from", len(manifest["files"]),
          "private RGBA textures; decoded bytes", manifest["decoded_rgba_bytes_no_mips"], flush=True)


if __name__ == "__main__":
    main()
