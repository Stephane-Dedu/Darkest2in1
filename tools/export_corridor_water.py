"""Export the owner's DD2 ocean maps for private offline corridor rendering.

python tools/export_corridor_water.py <game> <private-output>
Only reads installed Addressables bundles. No extracted files belong in Git.
"""
import argparse
import json
from pathlib import Path

from render_native_rooms import digest


def export(game, output):
    import UnityPy

    game, output = Path(game).resolve(), Path(output).resolve()
    repo = Path(__file__).resolve().parents[1]
    if output == repo or repo in output.parents or output == game or game in output.parents:
        raise ValueError("Keep extracted ocean resources outside the repository and game")
    aa = game / "Darkest Dungeon II_Data/StreamingAssets/aa"
    bundles = ("biome_coast_assets_all.bundle", "biome_common_assets_all.bundle",
               "characters_shared_assets_all.bundle")
    env = UnityPy.Environment(path="")
    for name in bundles:
        env.load_file(str(aa / name))
    output.mkdir(parents=True, exist_ok=True)
    records = []
    for obj in env.objects:
        if obj.type.name != "Material":
            continue
        material = obj.read()
        if material.m_Name != "vfx_biome_common_water_ocean":
            continue
        properties = obj.read_typetree()["m_SavedProperties"]
        record = {"name": material.m_Name, "path_id": obj.path_id,
                  "floats": dict(properties["m_Floats"]), "colors": dict(properties["m_Colors"]),
                  "textures": {}}
        for key, binding in material.m_SavedProperties.m_TexEnvs:
            if key not in ("_CloudMaskTexture", "_FoamTexture"):
                continue
            texture = binding.m_Texture.read()
            image = texture.image
            name = material.m_Name + "-" + key + ".png"
            image.save(output / name)
            record["textures"][key] = {"file": name, "name": texture.m_Name,
                "path_id": binding.m_Texture.m_PathID, "size": list(image.size),
                "scale": [binding.m_Scale.x, binding.m_Scale.y],
                "offset": [binding.m_Offset.x, binding.m_Offset.y], "sha256": digest(output / name)}
        if set(record["textures"]) != {"_CloudMaskTexture", "_FoamTexture"}:
            raise ValueError("Native ocean material is missing its cloud/foam maps")
        records.append(record)
    if len(records) != 1:
        raise ValueError(f"Expected one native ocean material, found {len(records)}")
    result = {"bundles": {name: digest(aa / name) for name in bundles}, "materials": records,
              "notice": "Private game assets; keep outside Git and releases"}
    (output / "materials.json").write_text(json.dumps(result, indent=2) + "\n", encoding="utf-8")
    print("EXPORTED native ocean cloud/foam maps", output, flush=True)


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("game", type=Path)
    parser.add_argument("output", type=Path)
    args = parser.parse_args()
    export(args.game, args.output)
