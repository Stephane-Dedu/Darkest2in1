# Native combat scenery extension study

Rounds 94–95, 2026-10-05. The owner approved the first four native-arena
extensions. The local pack now contains 24 RGB panoramas, six per primary region,
from different exterior, resistance, faction, creature-den, pillager and cultist
arenas. Round 95 deploys their optional loader and the private pack locally.

The previous set referenced regional texture strips. This study instead uses
the arena's actual mesh placement and painted base-colour textures, rendered
offline in Blender. The central buildings, tower, palisades and coastal shrine
remain recognizable; built-in Codex imagegen extends their sides and supplies
an open foreground walking surface. Outputs are close visual reproductions,
not guaranteed pixel-identical outpainting.

## Local outputs

Final files and full prompts/provenance are in
`C:\Users\Piral\DarkestDungeon3\local-art\combat-extensions\manifest.json`:

- `dd2_city-arena-01.png` through `dd2_city-arena-06.png`
- `dd2_farm-arena-01.png` through `dd2_farm-arena-06.png`
- `dd2_forest-arena-01.png` through `dd2_forest-arena-06.png`
- `dd2_coast-arena-01.png` through `dd2_coast-arena-06.png`

Native reference renders, scene exports and Blender files are outside the repo
under `C:\Users\Piral\.universal-modder\inspection\dd2-combat-extensions`.
Copies of the final PNGs and reference renders are served by the existing local
preview at `http://127.0.0.1:8766/combat-extensions.html`. The page compares the
new extension, corresponding native reference and first round-93 scene for
each region. Six thumbnails and previous/next arrows browse locations; walking
band and mirror controls help inspect the ground. It links to the earlier
round-93 room/corridor composition, which retains that earlier public art set.

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
    foreach ($arenaName in @('dungeon_exterior', 'resist', 'faction', 'creature_den', 'pillager', 'cultist')) {
        ..\universal-modder\.venv\Scripts\python.exe tools/export_arena_reference.py --game ..\game --output $arenaScratch --region $regionName --arena $arenaName
        if ($LASTEXITCODE -ne 0) { throw "Arena export failed" }
        $sceneFolder = Join-Path $arenaScratch $regionName
        if ($arenaName -ne 'dungeon_exterior') { $sceneFolder = Join-Path $sceneFolder $arenaName }
        & 'C:\Program Files\Blender Foundation\Blender 5.2\blender.exe' -b --python-exit-code 1 --python tools/render_arena_reference.py -- --scene "$sceneFolder\scene.json"
        if ($LASTEXITCODE -ne 0) { throw "Arena render failed" }
    }
}
```

References use `scenes_scenes_combat_arena_<region>_<arena>.bundle`
and the biome's assets/models plus shared/farm dependencies. Static-batch mesh
vertices already contain world placement; renderer firstSubMesh/subMeshCount
select each instance's geometry. Unbatched objects retain hierarchical position,
rotation and scale. Highest LOD and active scenery are exported. Native custom
materials commonly bind `_BaseMap_texture`, not `_MainTex`. Material tint,
UV tiling and alpha cutouts are retained.
Round 95 also recognizes transparency from the actual RGBA texture alpha,
covering native materials without the alpha-cutout flag. Empty MeshFilters
contribute no geometry and are skipped, rather than recorded as missing meshes.

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

## Local runtime pack

Final PNGs, manifest and README are copied separately into
`C:\Users\Piral\DarkestDungeon3\game\PrivateScenery`. The config entry
`Paths/NativeRoomSceneryFolder` defaults to this folder. Empty disables the
private pack; missing or invalid files retain the distributed public scenes.
No private image is copied into the repository or the distributable plugin data.

Files follow `dd2_<region>-arena-<NN>.png`, with two decimal digits starting at
01. The loader takes up to 12 per primary region, in filename order. It accepts
eight-bit RGB/RGBA PNGs up to 16 MB, width 1920–4096, height 720–2048 and aspect
ratio 2.6–2.7. To expand the set, add inspected scenes with the next filename and
record full prompts/reference hashes in the private manifest. The runtime reads
the PNGs directly and does not depend on manifest fields.

Room choices use the saved expedition seed and room ID, consume no gameplay RNG
and remain stable on revisits/reloads with an unchanged pack. Every six consecutive
room IDs use all six scenes; mirroring supplies additional views, not new images.
Only active-region files load. Worker IO precedes at most one Unity decode/upload
per Update with markNonReadable. The complete valid pool is adopted once and fades
in over 0.35 s, so invalid candidates cannot reshuffle rooms after loading. Clear
releases native handles and all owned generated textures on region/expedition exit.

Private panoramas draw their full floor. This retains Shroud wharves instead of
blending sand over their boards. Existing short room fades cover the corridor/room
switch. The older public fallback still blends into the common native road. DD1
areas, dungeon systems and native DD2 combat retain their existing behavior.

## Verification

All 24 distinct PNGs decoded and passed format/bounds/aspect checks: 20 are
2048x768, three 2046x768 and one 2043x770. They retain generated dimensions and are
fitted at draw time. Pack size is 46,754,480 bytes; the largest image is 2,466,193.
Each native reference resolves all exported scenery objects. Reference renders
and final outputs were inspected; two Shroud foreground corrections close gaps.
Workspace, preview and installed private copies match SHA256, as do the deployed
DLLs and unchanged public fallback files. Browser regional/scene/reference controls
were inspected. Release build and 277 Core plus 19 UI tests pass.

The headless integration test compiles the actual loader and checks main-thread
texture calls, one decode per update, completed six-scene adoption, bad-file skipping,
fade readiness, fallback and teardown, including outstanding worker IO. It uses
Unity/Addressables shims; native PNG decoding, GPU rendering and lighting are still
unverified. No game was launched and no save accessed. Once launching is allowed,
check adjacent rooms and reloads, native hero/prop scale, Tangle torch visibility,
Shroud board/sand transitions and load/memory behavior over repeated expeditions.
