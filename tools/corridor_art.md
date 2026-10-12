# Private DD2 corridor scenery

## Current renderer and native rooms, round231

Complete corridor plates are drawn once. The previous renderer tiled whole perspective scenes with alternating
reflection, producing bilateral roofs, duplicated buildings and mismatched perspective. This was confirmed against
the unreflected source and reproduced by observing two draws in the actual UI method. The replacement uniformly
overscans 8%, pans within the image from physical corridor progress, and preserves source orientation on reversal.
The y=680 source floor anchor stays fixed and UVs stay within the source. Walking props retain their physical travel;
this small panorama pan is deliberately slower. Native tiled fallback scenery remains unchanged.

Rooms support canonical purpose names, such as `dd2_city-arena-boss-01.png` and `dd2_city-arena-battle-01.png`.
Allowed purposes are `boss`, `entrance`, `battle`, `quiet` and `treasure`. Private room discovery prioritizes these
files and keeps the existing 12-image cap. Selection uses expedition seed, room ID and original room content;
clearing, scouting and taking a curio do not change it. Secret rooms use treasure scenery; guarded curios and
treasures use encounter scenery. A missing boss scene falls back to a regional encounter. Missing purposes use
legacy numbered private art or distributed scenery, and invalid files are filtered before selection. Native
compositions keep their orientation. These choices change artwork only; they do not spawn a DD2 lair boss.

`tools/render_native_rooms.py` renders eight rooms per region from the owner's native kit: lair interior for boss,
lair exterior for entrance, resistance/faction/military/cultist for battles, gaunt for quiet rooms and pillager for
treasure rooms. Shroud substitutes Chirurgeon for its empty cultist export. The native combat camera has a 0.31
vertical lens shift for the crawl footline. A plane extends the region's native cobblestone, farm, forest or pier
ground material beneath the actors; existing arena geometry remains above it. The result is not the untouched
combat-camera export. It uses base color, tint and approximate depth haze; lighting and animated VFX remain absent.

Rebuild outside Git, using Blender with NumPy and the extracted kit's `bl_lib.py`, then Python with Pillow/NumPy:

```powershell
blender -b --threads 8 --python-exit-code 1 --python tools/render_native_rooms.py -- <kit> <private-output>
python tools/render_native_rooms.py <kit> <private-output> --compose-only
```

Use a fresh output directory when changing geometry/camera/material settings; completed near passes are cached.
The manifest records scene/floor/output hashes, camera and ground fitting. With DD2 stopped, back up PrivateScenery,
archive the old generic arena PNGs outside its top level, then install the 32 purpose-named PNGs and
`native-rooms-manifest.json`. Preserve all corridor PNGs, HamletModels and prior provenance. All game art stays private.
Current private output: `C:\Users\Piral\DarkestDungeon3\local-art\scenery-single-scene-v02\rooms`.

`-dd3-scenery-check` runs the existing unsaved native fixture through all corridor and room scenes in all four
regions. The actual loader and both actual draw methods are used. Corridor pan traverses both ends; full-body DD1
reference figures and gold y=680 marks show framing. No estate is chosen or persisted. This checks art presentation,
not native 3D actor contact, room-transition gameplay, torch interactions or battle return. Remove the flag for play.

## Previous native DD2 road pack, round216

The active install now uses 24 panoramas made from the owner's DD2 road, decor,
skirt and skybox assets: six each for Sprawl (`city`), Foetor (`farm`), Tangle
(`forest`) and Shroud (`coast`). These replace the eight generated corridors
described below. No generated artwork was used in this pack. Room panoramas and
their manifest were unchanged in round216; round231 replaces room art as described above.

Sources remain in `local-art/dd2-native-kit/corridor-renders/`. Prepare them with
a Python environment containing Pillow:

```powershell
python tools/prepare_native_corridors.py <private-kit> <private-output>
```

The script validates all 24 sources before writing, uniformly fits them to the
live scene and writes canonical PNGs plus `corridors-manifest.json`. Output must
stay outside Git. Current pack: `C:\Users\Piral\DarkestDungeon3\local-art\corridors-native-v01`.
Its manifest records source/output SHA256, fitting, source paths and limitations.
The independently maintained kit extraction/render scripts are untouched.

The live scene is 1920x720 with hero feet at y=680 (source y1020 in 2880x1080).
Raw Foetor and Tangle left this band below the textured road. Foetor is uniformly
enlarged 1.08x and Tangle 1.15x, centred horizontally and anchored at the top;
surplus is cropped below. Sprawl/Shroud retain 1.00x. This preserves aspect and
moves road under the footline without painting it.

With DD2 stopped, back up `PrivateScenery`, then copy the 24 canonical PNGs and
their corridor manifest to `C:\Users\Piral\darkestwithdlc\game\PrivateScenery`.
Replace old same-name corridor files; retain all room PNGs and `manifest.json`.
Do not leave old corridor variants numbered 07+ in the pool unintentionally.
The existing naming, bounds, selection and loader contract below is unchanged.
Increasing the pool from two to six can change a continuing expedition's art
selection, but does not change its map or gameplay state.

Rollback backup (complete original 35-file folder):
`C:\Users\Piral\.universal-modder\backups\dd3-scenery-before-native-corridors-20261011\20261011-133022.zip`.
Stop DD2, move the current private folder aside, then restore the complete backup;
overlaying it would leave native variants 03-06 behind. Prior generated sources
and prompts remain in `local-art/corridors`.

With debug keys enabled, `-dd3-corridor-preview` opens an unsaved native-menu
scenery fixture; add `-dd3-corridor-check` to cycle all 24 choices automatically.
It uses the actual loader and panorama draw, forward/reverse travel, gold y=680
footline marks and full-body DD1 reference figures. It requires Phase.Off and
no selected Save or SavePath. It opens/creates no estate, performs no campaign
persistence and sends no OS input. Close returns to the menu. Do not leave these
flags active for normal play.

Round216 native 1600x900 checks loaded/adopted six corridors per region and
rendered every choice. Private logs/captures are in the pack's `native-check/`
and `native-check-v02/`. The initial revision used portrait crops; only v02 is
useful for full-body framing. Release and 995 Core + 182 UI tests pass. This
fixture does not establish native 3D actor contact, curio input, room entry or
battle handoff with the new art. Those remain live-quest acceptance. See
[native_corridor_audit.md](native_corridor_audit.md) for art gaps/extraction order.

## Historical round171 generated pack

Round 171 adds a separate private panorama pool for hallways in the Sprawl,
Foetor, Tangle and Shroud. Rooms retain their own arena-extension pool.

The owner pack contains eight new panoramas, two per region, generated with
Codex's built-in imagegen using native exterior and resistance arena reference
renders. These continue the same regional materials and ink marks into lateral
streets, lanes, forest tracks and coastal paths. Four initial candidates received
a localized ground correction to remove their black lower border. All final
images have a continuous walking surface beneath the hero footline.

## Private files and provenance

The images and full prompts, correction prompts, reference paths and SHA256
hashes live outside Git at:

`C:\Users\Piral\DarkestDungeon3\local-art\corridors\manifest.json`

Installed PNG copies go to `game\PrivateScenery`, with provenance in
`corridors-manifest.json`. Existing room files and their manifest are retained.
The shared `Paths/NativeRoomSceneryFolder` setting controls both private pools.

Use `dd2_<region>-corridor-<NN>.png`, for example `dd2_city-corridor-01.png`.
Region suffixes are city, farm, forest and coast. Numbers are 01–99; the first
12 files in ordinal filename order are loaded for each pool. Images must meet
the existing room pack bounds: RGB/RGBA eight-bit PNG, up to 16 MB, width
1920–4096, height 720–2048 and aspect 2.6–2.7. Inspect opacity and the entire
lower walking band before adding a file. Source-derived art stays private;
never commit these images or reference renders or include them in a release.

## Historical round171 traversal and loading (superseded by round231 for complete panoramas)

The expedition seed and corridor ID select a scene and mirror without consuming
gameplay RNG. One hallway keeps the same panorama from end to end. Consecutive
IDs cycle through the available images. Revisit/reload remains stable while the
pack is unchanged; walking direction and tile changes never select a new image.

The full panorama, including its floor, scrolls at the same rate as the props.
Adjacent repeats alternate reflection, joining the same source edge at each
boundary. The existing physical camera preserves the location when reversing.
Room fades cover entry/exit, and arrival cues remain above the panorama.
Corridors do not paste the old native road over the generated floor.

The existing active-region worker reads bounded PNGs; the main thread uploads
at most one texture per update. Both completed pools are adopted once, with a
0.35-second fade. Invalid files are skipped before selection. Missing corridor
art keeps the native scenery; generated corridors can also draw if a native
Addressables texture is unavailable. Teardown releases both pools.

DD1 references: installed `crypts.corridor_wall.00.png` through `.05.png`, and
Unity `RaidHallway.LoadHallway` / `RaidHallwayView.UpdateEnviroment` for ordered
physical segments and regional layers. These new DD2 images are an adaptation
requested by the owner, rather than a claim of identical DD1 artwork.

## Verification

Core tests cover regional discovery, separate bounded pools, deterministic
selection, diversity and reload. The actual linked runtime loader test covers
one upload per update, bad headers, completed adoption, fade, fallback and
teardown. The actual linked panorama draw is checked for coverage, matching
source edges, floor alignment, reversed/mirrored travel, scaled/letterboxed
canvases and restoring draw state on failure. Native playtest observations
and outstanding checks are recorded in MODLOG.md and PARITY.md.
