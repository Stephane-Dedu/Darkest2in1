# Shieldbreaker 3D prototype

The model uses DD2 Hellion's native skeleton and bind poses. The revised costume follows
Shieldbreaker's assembled DD1 combat art: cropped gold chest wraps, exposed midriff,
loose trousers, green sash/ties, cream headcloth around a dark helmet, left-side armour,
and an olive shield with silver crescent plates and her green serpent emblem. The donor's
boot geometry remains. Her spear follows the right wrist; the shield is strapped to the
left forearm, with no left hand. DD1's promotional art sometimes mirrors her.

The runtime replaces only Shieldbreaker's isolated presentation mesh. It retains the
native shader, material property system, three-quarter corridor angle and existing
procedural walk. Chest-relative arm targets provide an idle, a spear thrust and a shield
raise. Offensive skills share the thrust in this prototype. Death and the lower body
still use the donor's native animation. This is an early mesh and animation pass, with
proportions, cloth, materials and individual skill animations still to refine.

## Build private assets

Run from the mod repository in PowerShell. Requires `uv`, UnityPy/Pillow and Blender 5.2.
The output must stay outside the repository because it contains derived game assets.

```powershell
$sbModels = 'C:/Users/Piral/.universal-modder/inspection/shieldbreaker-refine-20261010/model'
$sbBlender = 'C:/Program Files/Blender Foundation/Blender 5.2/blender.exe'
uv run --with UnityPy python tools/shieldbreaker/build_model.py --game 'C:/Users/Piral/darkestwithdlc/game' --output $sbModels --blender $sbBlender
dotnet run --project tools/shieldbreaker/ModelAudit -- $sbModels "$sbModels/donor.json"
dotnet run --project tools/shieldbreaker/ModelAudit -- --reference 'C:/Users/Piral/darkestwithdlc/game/dlc/702540_shieldbreaker/heroes/shieldbreaker' "$sbModels/reference"
& $sbBlender --background "$sbModels/shieldbreaker.blend" --python-exit-code 1 --python tools/shieldbreaker/preview_walk.py -- $sbModels
uv run --with pillow python tools/shieldbreaker/preview_outputs.py $sbModels
```

If DD1 is installed separately, add `--dd1 <DD1 root>` to the builder. It unpacks the
owned DLC's combat atlas into the private `reference` subfolder. `design_mesh.py` owns
the model's shaped surfaces and texture projections; `model_blender.py` owns the native
export, rig and preview. Painted face, mask, helmet, chest, belt, shield and armour tiles
come from the original art. Broad cloth shapes, arm/torso anatomy and gripping fingers
are volumetric geometry. The pants projection fills empty fields in the original bent
pose while retaining its gold texture and smaller ink creases.

Rotated Spine atlas regions unpack clockwise by 90 degrees, matching the Core reader's
corner mapping. Exported custom normals are also read back for preview, so smooth skin
does not turn into flat shaded triangles during the check.

The audit uses the production model reader and the actual `CorridorWalkCycle` code.
It compares every native bone name and bind matrix, validates mesh/texture bounds,
checks rigid weapon weights and verifies that attack/defence return to idle. It also
writes gait samples for the optional Blender walk preview. The preview approximates
the Unity pose solver and does not reproduce DD2's shader, lighting or combat timeline.

`shieldbreaker.blend` retains editable authoring objects and the rig. Still renders
use a mesh read back from the exported JSON rather than the authoring objects.
The optional reference audit assembles her original combat pose with the production
Spine reader. `preview_outputs.py` makes pose/walk sheets, a walk GIF and a side-by-side
costume comparison when that reference is present.

## Install while DD2 is stopped

Copy only `shieldbreaker.json`, `shieldbreaker_base.png` and `shieldbreaker_ink.png`
to `<active DD2 game>/PrivateHeroModels`. Build/deploy the plugin normally. Leave the
donor dump, original textures, Blender scene and previews in the private work folder.
Keep all generated assets out of Git and release packages.

The BepInEx configuration adds `Paths/HeroModelFolder`, which defaults to that private
folder, and `Look/Shieldbreaker3D`, which defaults to `true`. Set the latter to `false`
and restart DD2 to restore her DD1 sprites in combat and her flat DD1 figure in the
corridor. Missing or incompatible assets use the same fallback. Faded Memory encounters
retain their DD1 sprite presentation regardless of this setting.

## Native checks still required

The implementation was built without launching. The owner later authorized a launch
and requested Shieldbreaker in a roster. Estate 2 now contains Hachet; native logs confirm
the revised mesh and corridor rig bind. Final palette, scale and animation timing still
need visual review. Use estate 2 with the project's backup/restore procedure and check:

- Corridor idle, forward/backwards walk, turns, room transitions and camp framing.
- A normal Hellion beside her, to verify that shared assets remain unchanged.
- Pierce and a ranged skill, plus Serpent's Sway and an incoming hit, for pose timing.
- Spear grip and shield attachment through the walk and attack.
- Entering/leaving combat and a second encounter, for material restoration and tint.
- DD1 art in a Faded Memory encounter and after disabling `Shieldbreaker3D`.

The loader logs `[shieldbreaker-3d]` when a model binds or falls back. A different
Hellion mesh revision requires rebuilding the private pack from the active install.
