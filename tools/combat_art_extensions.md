# Native combat scenery extension study

Round 94, 2026-10-05. Four private 2048x768 RGB panoramas extend the installed
DD2 exterior dungeon arenas for the Sprawl, Foetor, Tangle and Shroud. These
are a comparison study; the installed mod still uses round 93's room set.

The previous set referenced regional texture strips. This study instead uses
the arena's actual mesh placement and painted base-colour textures, rendered
offline in Blender. The central buildings, tower, palisades and coastal shrine
remain recognizable; built-in Codex imagegen extends their sides and supplies
an open foreground walking surface. Outputs are close visual reproductions,
not guaranteed pixel-identical outpainting.

## Local outputs

Final files and full prompts/provenance are in
`C:\Users\Piral\DarkestDungeon3\local-art\combat-extensions\manifest.json`:

- `dd2_city-arena-01.png`
- `dd2_farm-arena-01.png`
- `dd2_forest-arena-01.png`
- `dd2_coast-arena-01.png`

Native reference renders, scene exports and Blender files are outside the repo
under `C:\Users\Piral\.universal-modder\inspection\dd2-combat-extensions`.
Copies of the final PNGs and reference renders are served by the existing local
preview at `http://127.0.0.1:8766/combat-extensions.html`. The page compares the
new extension, native reference and first round-93 scene for each region. It
also provides a walking-band guide and links back to the corridor composition.

The generated files reproduce source-game artwork and remain private. Do not
commit them, native textures, meshes, reference renders or Blender scenes, or
include them in releases. The repo contains only tools and workflow notes.
Original generated candidates remain in Codex's generated_images directory.

## Reproduce the reference renders

Requires an owned DD2 install, UnityPy, numpy and Blender. Export outside the
mod repo. The exporter refuses an output directory inside its repository.

```powershell
$arenaScratch = 'C:\Users\Piral\.universal-modder\inspection\dd2-combat-extensions'
foreach ($regionName in @('city', 'farm', 'forest', 'coast')) {
    ..\universal-modder\.venv\Scripts\python.exe tools/export_arena_reference.py --game ..\game --output $arenaScratch --region $regionName
    if ($LASTEXITCODE -ne 0) { throw "Arena export failed" }
    & 'C:\Program Files\Blender Foundation\Blender 5.2\blender.exe' -b --python-exit-code 1 --python tools/render_arena_reference.py -- --scene "$arenaScratch\$regionName\scene.json"
    if ($LASTEXITCODE -ne 0) { throw "Arena render failed" }
}
```

References use `scenes_scenes_combat_arena_<region>_dungeon_exterior.bundle`
and the biome's assets/models plus shared/farm dependencies. Static-batch mesh
vertices already contain world placement; renderer firstSubMesh/subMeshCount
select each instance's geometry. Unbatched objects retain hierarchical position,
rotation and scale. Highest LOD and active scenery are exported. Native custom
materials commonly bind `_BaseMap_texture`, not `_MainTex`. Material tint,
UV tiling and alpha cutouts are retained.

The installed `scenes_scenes_combat.bundle` defines `InLine Cam - Default`
under `InLine Cam` at Unity position 0,0.74,-8.3 with vertical FOV 38 and identity
rotation. The renderer uses this pose and a 16:9 frame. It exports 86 Sprawl,
83 Foetor, 325 Tangle and 235 Shroud scenery objects with zero unresolved
references. VFX objects/materials and lamp glow meshes are deliberately excluded;
their custom Unity shaders otherwise render as opaque sheets in Blender.

This preserves composition and original texture marks. Blender emission materials
and a region-coloured sky approximate presentation; Unity's lighting, fog,
particles, animated fire/water, outlines and post processing are not reproduced.
The image generator continues the coarse ink contours and angular surfaces from
these references rather than introducing a different rendering style. The first
Shroud candidate left holes under the traversal band; a localized foreground
edit joined the planks while preserving the background. Both prompts and the
first candidate path are recorded in the local manifest.

## Verification and next use

All four PNGs decoded and passed exact 2048x768 RGB checks. Project/preview copies
match SHA256. Reference renders and each generated output were visually inspected;
the browser comparison loads regional images and switches to the native reference.
Release build and 274 Core plus 18 UI tests pass. No game process or save access.

Before adopting these in gameplay, inspect hero/prop scale, darkness, walking
surface and room/corridor ground joins. The Shroud study uses wooden boards;
the existing corridor uses sand, so runtime adoption needs a coherent ground
transition. The Tangle remains deliberately dark like its source reference and
needs native visibility checks. Four scenes means one distinct scene per region,
not a unique background for every room. Wider sets can use other native arenas
or controlled side extensions after selecting this direction.
