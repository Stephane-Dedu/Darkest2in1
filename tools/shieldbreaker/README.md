# Shieldbreaker 3D prototype

The model remodels DD2 Hellion's actual body mesh, skeleton and bind poses. Her sculpted
torso, arms, face, right hand, native cloth topology and boots are retained. Hair, feathers,
the long skirt, bone jewellery and left hand are removed. The trousers are widened and
recoloured; the pelvis is clipped from the native skirt with interpolated UVs/skin weights.
The costume follows
Shieldbreaker's assembled DD1 combat art: cropped gold chest wraps, exposed midriff,
loose trousers, green sash/ties, cream headcloth around a dark helmet, left-side armour,
and an olive shield with silver crescent plates and her green serpent emblem. Her spear follows the right wrist; the shield is strapped to the
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
$sbModels = 'C:/Users/Piral/.universal-modder/inspection/shieldbreaker-donor-20261010/model'
$sbBlender = 'C:/Program Files/Blender Foundation/Blender 5.2/blender.exe'
uv run --with UnityPy python tools/shieldbreaker/build_model.py --game 'C:/Users/Piral/darkestwithdlc/game' --output $sbModels --blender $sbBlender
dotnet run --project tools/shieldbreaker/ModelAudit -- $sbModels "$sbModels/donor.json"
dotnet run --project tools/shieldbreaker/ModelAudit -- --reference 'C:/Users/Piral/darkestwithdlc/game/dlc/702540_shieldbreaker/heroes/shieldbreaker' "$sbModels/reference"
& $sbBlender --background "$sbModels/shieldbreaker.blend" --threads 4 --python-exit-code 1 --python tools/shieldbreaker/preview_walk.py -- $sbModels
uv run --with pillow python tools/shieldbreaker/preview_outputs.py $sbModels
```

If DD1 is installed separately, add `--dd1 <DD1 root>` to the builder. It unpacks the
owned DLC's combat atlas into the private `reference` subfolder. `native_body.py` selects
and recolours the native body surfaces. `design_mesh.py` owns costume geometry and paint
projections; `model_blender.py` owns export, rig and preview. Mask, helmet, chest, belt,
shield and armour paint comes from DD1. Native anatomy retains DD2's skin weights, normals,
smooth outline tangents, vertex colours and ink. The runtime retains the separate native
outline material when replacing the body material.

Rotated Spine atlas regions unpack clockwise by 90 degrees, matching the Core reader's
corner mapping. Both the native mesh and Blender authoring surfaces use outward triangle
cross products aligned with their normals. Do not reverse their winding on export or
preview. The old converter did, and the native renderer culled the new painted fronts.
The audit rejects that pack. Exported custom normals are also read back for preview.
Palette values are sRGB paint colours converted to Blender's linear pixel buffer before
saving. Painted `_Ink` tiles retain dark creases instead of being filled white.

The audit uses the production model reader and the actual `CorridorWalkCycle` code.
It compares every native bone name and bind matrix, validates mesh/texture bounds,
checks winding, rigid weapon weights and verifies that attack/defence return to idle. It also
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

The previous costume pack bound in native logs and estate 2 contains the owner's test
recruit Hachet. The native-body remodel passes offline export/rig/pose/walk checks and is
installed. DD2 was reopened with the owner's standing launch permission, but this pack's
appearance and animation timing still need visual review. For agent tests, use an isolated
estate-2 entry with the project's backup/restore procedure; the normal picker reads protected
slots and must be avoided. Check:

- Corridor idle, forward/backwards walk, turns, room transitions and camp framing.
- A normal Hellion beside her, to verify that shared assets remain unchanged.
- Pierce and a ranged skill, plus Serpent's Sway and an incoming hit, for pose timing.
- Spear grip and shield attachment through the walk and attack.
- Entering/leaving combat and a second encounter, for material restoration and tint.
- DD1 art in a Faded Memory encounter and after disabling `Shieldbreaker3D`.

The loader logs `[shieldbreaker-3d]` when a model binds or falls back. A different
Hellion mesh revision requires rebuilding the private pack from the active install.
