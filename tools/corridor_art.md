# Private DD2 corridor scenery

## Current pack: native DD2 roads, round216

The active install now uses 24 panoramas made from the owner's DD2 road, decor,
skirt and skybox assets: six each for Sprawl (`city`), Foetor (`farm`), Tangle
(`forest`) and Shroud (`coast`). These replace the eight generated corridors
described below. No generated artwork was used in this pack. Room panoramas and
their manifest are unchanged; the new battle-arena exports are not installed.

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

## Traversal and loading

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
