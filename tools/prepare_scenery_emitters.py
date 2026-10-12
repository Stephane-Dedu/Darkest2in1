"""Project native Sprawl fire meshes into the installed room camera; writes private anchor metadata.
Blender: --python tools/prepare_scenery_emitters.py -- KIT ROOMS_MANIFEST OUTPUT
Reads the exported native transforms/meshes; never changes the kit or generates textures.
"""
import argparse
import json
import sys
from pathlib import Path

sys.path.insert(0,str(Path(__file__).resolve().parent))
from render_native_rooms import digest


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("kit",type=Path)
    parser.add_argument("rooms_manifest",type=Path)
    parser.add_argument("output",type=Path)
    args = parser.parse_args(sys.argv[sys.argv.index("--")+1:])
    repo = Path(__file__).resolve().parents[1]
    if args.output.resolve() == repo or repo in args.output.resolve().parents:
        raise ValueError("Native metadata stays outside Git")
    sys.path.insert(0,str(args.kit/"tools"))
    import numpy as np
    import bpy
    import bl_lib as L
    from mathutils import Vector
    from bpy_extras.object_utils import world_to_camera_view
    L.reset()
    cam = L.camera((0,-8.3,.74),(0,10,.74),fov=38,res=(2880,1080))
    cam.data.shift_y = .31
    bpy.context.view_layer.update()
    scenes, evidence = {}, []
    for entry in json.loads(args.rooms_manifest.read_text())["files"]:
        if entry["region"] != "dd2_city":
            continue
        folder = args.kit/"arenas"/entry["native_arena"]
        doc = json.loads((folder/"scene.json").read_text())
        anchors = []
        for obj in doc["objects"]:
            name = obj["name"].lower()
            if obj["lod_skip"] or not obj["active"] or not obj["enabled"] or "fire" not in name or "campfire" in name:
                continue
            mesh = np.load(folder/f"mesh-{obj['mesh']}.npz")
            vertices = np.c_[mesh["v"],np.ones(len(mesh["v"]))] @ (L.CHANGE @ np.array(obj["world"])).T
            points = [world_to_camera_view(bpy.context.scene,cam,Vector(v[:3])) for v in vertices]
            points = [p for p in points if p.z > .1]
            if not points:
                continue
            left,right = min(p.x for p in points)*1920,max(p.x for p in points)*1920
            top,bottom = (1-max(p.y for p in points))*720,(1-min(p.y for p in points))*720
            x,y,w,h = (left+right)/2,(top+bottom)/2,right-left,bottom-top
            if x < 0 or x > 1920 or y < 0 or y > 600 or w < 8 or h < 8 or w > 450 or h > 450:
                continue
            if any(abs(x-a[0]) < 30 and abs(y-a[1]) < 30 for a in anchors):
                continue
            anchor = [round(x,3),round(y,3),round(w,3),round(h,3)]
            anchors.append(anchor)
            evidence.append({"file":entry["file"],"native_mesh":obj["name"],"anchor":anchor,"source_scene_sha256":digest(folder/"scene.json")})
        if anchors:
            scenes[entry["file"]] = anchors[:16]
        print(entry["file"],len(anchors),flush=True)
    args.output.parent.mkdir(parents=True,exist_ok=True)
    args.output.write_text(json.dumps({"version":1,"scenes":scenes,"evidence":evidence,
        "source_kit":str(args.kit),"tool_sha256":digest(Path(__file__)),
        "limits":["Native mesh bounds anchor a 2D simulation sheet; native fire mesh shaders and indirect lighting are approximated"]},indent=2)+"\n",encoding="utf-8")


if __name__ == "__main__":
    main()
