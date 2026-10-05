"""Export an owner's DD2 arena for private offline reference rendering.

Requires UnityPy and numpy. Output contains game assets: keep it outside Git and
releases. Only export active static scenery, highest LOD, and base-colour textures.
This is a composition reference, not a reproduction of DD2's Unity shaders/VFX.
"""
import argparse
import json
from pathlib import Path

import numpy as np
import UnityPy
from UnityPy.helpers.MeshHelper import MeshHandler


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--game", type=Path, required=True)
    parser.add_argument("--output", type=Path, required=True)
    parser.add_argument("--region", choices=["city", "farm", "forest", "coast"], required=True)
    args = parser.parse_args()
    repo = Path(__file__).resolve().parents[1]
    if args.output.resolve().is_relative_to(repo):
        parser.error("Private game assets must be exported outside the mod repository.")
    bundles = args.game / "Darkest Dungeon II_Data/StreamingAssets/aa"
    out = args.output / args.region
    out.mkdir(parents=True, exist_ok=True)
    scene_name = f"scenes_scenes_combat_arena_{args.region}_dungeon_exterior.bundle"
    env = UnityPy.load(str(bundles / scene_name))
    scene_objects = list(env.objects)
    # Cross-referenced materials and meshes, including shared terrain pieces.
    for name in dict.fromkeys([args.region, "common", "farm"]):
        for suffix in ["assets_all", "models_platform_high_assets_all"]:
            path = bundles / f"biome_{name}_{suffix}.bundle"
            if path.exists():
                env.load_file(str(path))
    transforms = {}
    filters = {}
    for obj in scene_objects:
        if obj.type.name == "Transform":
            d = obj.read()
            transforms[d.m_GameObject.m_PathID] = d
        elif obj.type.name == "MeshFilter":
            d = obj.read()
            filters[d.m_GameObject.m_PathID] = d.m_Mesh
    matrices = {}

    def world(t):
        key = t.m_GameObject.m_PathID
        if key in matrices:
            return matrices[key]
        q = t.m_LocalRotation
        x, y, z, w = q.x, q.y, q.z, q.w
        rot = np.array([
            [1-2*(y*y+z*z), 2*(x*y-z*w), 2*(x*z+y*w), 0],
            [2*(x*y+z*w), 1-2*(x*x+z*z), 2*(y*z-x*w), 0],
            [2*(x*z-y*w), 2*(y*z+x*w), 1-2*(x*x+y*y), 0],
            [0, 0, 0, 1]], dtype=float)
        s = t.m_LocalScale
        rot[:3, :3] *= np.array([s.x, s.y, s.z])
        p = t.m_LocalPosition
        rot[:3, 3] = [p.x, p.y, p.z]
        if t.m_Father.m_PathID:
            rot = world(t.m_Father.read()) @ rot
        matrices[key] = rot
        return rot

    def active(t):
        if not t.m_GameObject.read().m_IsActive:
            return False
        return not t.m_Father.m_PathID or active(t.m_Father.read())

    skip_lod = set()
    for obj in scene_objects:
        if obj.type.name == "LODGroup":
            lods = obj.read_typetree()["m_LODs"]
            for lod in lods[1:]:
                skip_lod.update(r["renderer"]["m_PathID"] for r in lod["renderers"])
    meshes, materials, objects, missing = {}, {}, [], []

    def material(ptr):
        key = str(ptr.m_PathID)
        if key in materials:
            return key
        m = ptr.read()
        tree = ptr.read_typetree()
        floats = dict(tree["m_SavedProperties"]["m_Floats"])
        colors = dict(tree["m_SavedProperties"]["m_Colors"])
        texture = None
        scale, offset = {"x": 1, "y": 1}, {"x": 0, "y": 0}
        for name, tex in m.m_SavedProperties.m_TexEnvs:
            if name in ["_MainTex", "_BaseMap", "_BaseMap_texture"] and tex.m_Texture.m_PathID:
                image_obj = tex.m_Texture.read()
                texture = f"tex-{tex.m_Texture.m_PathID}.png"
                image_obj.image.save(out / texture)
                scale = {"x": tex.m_Scale.x, "y": tex.m_Scale.y}
                offset = {"x": tex.m_Offset.x, "y": tex.m_Offset.y}
                break
        tint = colors.get("_BaseMap_ColorTint", colors.get("_Color_Tint", {"r": 1,"g": 1,"b": 1,"a": 1}))
        if "_BaseMap_Tiling" in colors:
            tiling = colors["_BaseMap_Tiling"]
            scale, offset = {"x": tiling["r"], "y": tiling["g"]}, {"x": tiling["b"], "y": tiling["a"]}
        materials[key] = {"name": m.m_Name, "texture": texture, "tint": tint,
                          "scale": scale, "offset": offset,
                          "cutout": "transp" in m.m_Name.lower() or floats.get("_IsAlphaCutout", 0) > 0}
        return key

    for obj in scene_objects:
        if obj.type.name != "MeshRenderer" or obj.path_id in skip_lod:
            continue
        d = obj.read()
        go = d.m_GameObject.read()
        name = go.m_Name
        # Unity VFX texture sheets need their own shader simulation; exclude them.
        if not d.m_Enabled or any(s in name.lower() for s in ["vfx", "fogsheet", "scorch", "lamp_inner_glow"]):
            continue
        transform = transforms[go.object_reader.path_id]
        if not active(transform):
            continue
        try:
            if any("vfx" in p.read().m_Name.lower() for p in d.m_Materials):
                continue
            mesh_ptr = filters[go.object_reader.path_id]
            mesh = mesh_ptr.read()
            mesh_key = str(mesh_ptr.m_PathID)
            if mesh_key not in meshes:
                handler = MeshHandler(mesh)
                handler.process()
                meshes[mesh_key] = {"name": mesh.m_Name, "vertices": handler.m_Vertices,
                                    "uv": handler.m_UV0 or [],
                                    "triangles": list(handler.get_triangles())}
            batch = obj.read_typetree()["m_StaticBatchInfo"]
            batched = batch["subMeshCount"] > 0
            first = batch["firstSubMesh"] if batched else 0
            count = batch["subMeshCount"] if batched else len(mesh.m_SubMeshes)
            slots = [material(p) for p in d.m_Materials]
            objects.append({"name": name, "mesh": mesh_key, "first": first, "count": count,
                            "materials": slots,
                            "world": (np.eye(4) if batched else world(transform)).tolist()})
        except Exception as ex:
            missing.append({"name": name, "error": str(ex)})
    document = {"region": args.region, "source": scene_name,
                "meshes": meshes, "materials": materials, "objects": objects, "missing": missing,
                "camera": {"position": [0, .74, -8.3], "vertical_fov": 38},
                "notice": "Private game assets. Offline material/camera approximation; no Unity VFX or post processing."}
    (out / "scene.json").write_text(json.dumps(document, separators=(",", ":")), encoding="utf-8")
    print(f"{args.region}: {len(objects)} scenery objects, {len(materials)} materials, {len(missing)} unresolved")
    for entry in missing[:8]:
        print(entry)


if __name__ == "__main__":
    main()
