# Generated DD2 corridor scenery

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
