# Private DD2 corridor scenery

## Enclosed verges, round245, October 15, 2026

The v06 pass improves all six Tangle, Foetor and Shroud corridors. Their rear wall rows
move three native units closer without changing the stage camera, hero scale, floor UVs
or travel. An independently seeded low verge row breaks the long flat floor boundary.
Tangle plants retain only native foliage faces and original UVs from three combined meshes.
Foetor uses roadside scrub and occasional well/fence details. Shroud uses rope posts and
small dead-fish debris while retaining its ocean view. Large black barnacle mounds and
low-detail cliff cards were rejected in the private comparison.

Distant Tangle skirts retain their native dark earth banks. Removing that material from
raised woodland had left rectangular tree bases hanging above the floor. Near foundations
remain excluded; the distant banks blend into the foreground floor and existing depth haze.
Ground contact also receives broad, restrained value variation over the original paint.
No new bitmap artwork or AI-generated textures are used. Sprawl's 24 image files are
byte-identical to v05. All 18 verge passes leave the y680 footline clear.

Rebuild with the commands below. For an existing v05 pass directory copied to a fresh
private folder, rerender `ground wall road` for farm/forest/coast and `far vfar` for forest.
Ground must be rerendered when geometry changes because its contact shadows use that geometry.
Recompose the complete pack afterward. Native source assets are never modified.

Private renders, comparisons and exact installation rollback are under
`local-art/scenery-verges-v06`. The art-only installer changes 49 files and verifies
143 retained scenery/plugin files, including rooms, HamletModels and Sprawl. Existing
decoded-image limits and 36 MiB current-hall depth budget are unchanged. Six composition
and two camera cases pass. Native fixture acceptance is recorded in MODLOG/PARITY.

## Native depth layers, round244, October 14, 2026

The v05 corridor pass keeps the extracted DD2 meshes, painted textures, native UVs and
round240 stage camera. DD1's separate backdrop/middle/wall composition is the reference
for movement and hero readability. The 24 canonical 4096x768 PNGs remain complete fallback
images. Each has optional RGBA companions named `.depth-back.png`, `.depth-middle.png`
and `.depth-front.png`. They travel at 18%, 48% and 100% of the original bounded panorama pan,
respectively, with all three registered at the hall centre. Reversal preserves orientation.
Ground, walls and their existing fire anchors move together. No whole architecture repeats.

Native floor paint now receives local geometry contact over three world units, rendered
at 12 samples, with a restrained 20% foreground reduction during composition. The Tangle's
empty foundation-only hill tiles no longer occupy visible wall spans. Foliage bases above
discarded driving foundations are lowered to the ground; buried roots retain native placement.
The ground horizon fades over 56 source pixels. The former 160-pixel fade left blue haze
under nearby roots and palisades, hiding their contact shadows and making them appear to hover.

`CorridorDepthArt` reads only the requested corridor's companions on a worker and uploads
at most one image per update, sharing that budget with the existing panorama loader. The
steady decoded companion budget is 36 MiB, with no second region-wide set or render target.
All three depths must validate and upload before drawing. A missing/broken set keeps the
complete plate. On adoption, the depths start aligned to that plate and settle into their
stable parallax over 0.35 seconds. Translucent GUI/battle covers use the complete image to
avoid independently faded walls brightening or doubling. Leaving the region releases the set.

Hero locomotion uses the actual frame's rendered camera displacement. `CorridorSceneMotion`
shares the discrete-step easing and continuous pan with scenery, particles and gait. Each native
rig converts that virtual-pixel distance through its fitted leg length and facing angle. Phase
has no separate speed filter or 0.1-second time cap. Planted contacts follow ground displacement
while the upper body blends from idle. Forward travel pans left from either hall entrance,
preserving the painted UV orientation. Stops, reverse input, tile carry, room changes and newly
adopted art are covered by regression tests; transition baselines do not become giant strides.
The optional imported hero also uses a signed distance clock, avoiding a complemented pose
on reversal. Its source clip still needs a separate native stride/motion audit.

Rebuild privately:

```powershell
blender -b --threads 8 --python-exit-code 1 --python tools/render_native_corridors.py -- <kit> <passes>
python tools/render_native_corridors.py <kit> <passes> --compose-only
python tools/compose_corridor_depth.py <passes> <depth-pack>
python tools/review_corridor_depth.py <before-pack> <depth-pack> <private-review> --video
python -m unittest discover -s tests -p corridor_depth_test.py
```

Keep all art outside Git. Preserve room PNGs, HamletModels and `scenery-emitters.json` when
installing the 24 canonical PNGs, 72 companions and their manifest. Layer names cannot enter
the deterministic corridor variant pool. Stage projection, gameplay walking coordinates,
room art and emitter positions are retained. Private provenance, before/after views, end/centre crops for every
variant and a motion comparison are under `local-art/scenery-depth-v05`.

The pack and shared plugin are installed with a verified rollback zip. The other 93 private
scenery files are unchanged. Release, 1,167 Core and 203 UI tests pass on HEAD plus this round;
the shared snapshot including independent Shieldbreaker work passes 1,170 Core and 203 UI tests.
Six composition and two camera cases pass. Native travel/reversal/torch/interaction,
same-scene battle return and upload timing remain pending. The owner's launch pause ends
October 14 at 19:38:14 UTC /21:38:14 Paris. These are painted 2.5D depths behind actors;
foreground actor occlusion, native water shaders and deferred lighting remain separate work.

## Stage framing and wider travel, October 14, 2026

The current private pack contains 24 corridors at 4096x768 and 32 rooms at 2880x1080.
`scenery_camera.py` gives both renderers a shared 6-degree stage camera, calibrated to the
crawl's four-unit/400px bodies and y680 feet. Corridors compensate for the runtime 8% overscan
before rendering. Native scenery is doubled from its original character scale. The independent
Foetor/Tangle image enlargements and uneven driving-road tiles in the actor lane are removed.
One level native material surface, with the same world UV mapping, runs beneath rooms and corridors.
Forest dark tile foundations are excluded; wall bases blend over .6 world units and distant skirts over 3.

The wider corridor provides about 2,227 virtual pixels of bounded lateral travel, instead of 154.
Complete architecture remains unreflected and is drawn once. The loader accepts wider images only
for discovered corridor filenames, with a 4096x768 decoded-pixel ceiling; existing room formats remain
valid. A wide corridor occupies approximately the same texture memory as the previous 2880x1080 plate.
The frame follows corridor progress, so travel per square varies with corridor length. This is still a
baked perspective plate; independent foreground occlusion, water shading and native deferred lighting
remain approximations.

Use a fresh private output directory for a camera, geometry or material change. `--bands wall` can
re-render just the wall pass when the camera/layout and other passes are unchanged. Recompose afterward.
Rebuild fire metadata after rebuilding rooms: `prepare_scenery_emitters.py` understands both camera versions
and applies the room manifest's environment scale. Assets and QA evidence stay in
`C:\Users\Piral\DarkestDungeon3\local-art\scenery-contact-v04`.

```powershell
python -m unittest discover -s tests -p scenery_camera_test.py
blender -b --python-exit-code 1 --python tools/verify_scenery_camera.py -- <private-kit>
```

`-dd3-crawl-motion-check` is a separate unsaved native fixture. It walks ordinary map routes through
six empty squares, stops and reverses in each region, with real hero models. Every frame checks the
actual panorama crop for jumps at tile crossings. Hunger still runs; the fixture resumes an interrupted
route through the public API and carries enough food. It never selects or persists an estate.
This complements the regular travel/torch/interaction/battle/return fixture, rather than replacing it.

## Continuous ground and moving atmosphere, round233

`tools/render_native_corridors.py` rebuilds six variants per primary region from the existing road kit. The former
opaque soil pass was drawn over distant rows, cutting their foundations into a straight stripe. Ground now sits
underneath those rows. Native road material fills distant ground; skirt bases and ground/skybox horizons blend
gradually. Shroud distant ground uses dark water colour because the exported ocean shader has no diffuse map.
Sprawl road lamps and Shroud road posts move behind the actor lane. These remain backdrop props; they do not
provide foreground actor occlusion. Camera, road scale and y680 fitting are retained.

```powershell
blender -b --threads 8 --python-exit-code 1 --python tools/render_native_corridors.py -- <kit> <private-output>
python tools/render_native_corridors.py <kit> <private-output> --compose-only
blender -b --python-exit-code 1 --python tools/prepare_scenery_emitters.py -- <kit> <rooms-manifest> <private-output>/scenery-emitters.json
```

Blender needs NumPy; composition runs separately in Python with Pillow/NumPy. Use fresh render output after
geometry/material changes. A completed road pass caches that variant. Private manifests record source/output
hashes, placement lists, camera, material provenance and render/composition tool hashes. With DD2 stopped and
PrivateScenery backed up, copy only the 24 canonical corridor PNGs, their manifest and `scenery-emitters.json`;
preserve the 32 room PNGs and HamletModels. Current private output/evidence:
`C:\Users\Piral\DarkestDungeon3\local-art\scenery-atmosphere-v03`.

DungeonAtmosphere reuses the quest-map pack at `ExpeditionArtFolder/vfx/`: five private PNG atlases, six effect
descriptors, about 13.25MiB decoded RGBA. Sprawl smoke/embers, Foetor steam, Tangle haze and Shroud mist use
visual seeds and continuous time without gameplay RNG. Effects follow the same bounded crop, clip at the 720px
scene edge and draw behind props/actors. Torch level reduces visibility. Native fire-mesh bounds anchor animated
Sprawl flames and a gentle light wash in six room scenes (16 anchors). The two lair scenes have no accepted
visible fire meshes; no arbitrary flames are added. Missing/invalid metadata or atlases retain static scenery.
`extract_expedition_vfx.py` converts coverage-only mist/cloud textures to white RGB with native red-channel alpha
(and the native half-oval fog mask). GUI tint supplies colour; keeping the mask in RGB as well had darkened it
twice. Fire/smoke simulation RGB and spark RGBA are retained. This remains an authored straight-alpha adaptation
of the native textures, rather than a reproduction of DD2's shader.

In-place combat captures a particle-free base and freezes its crop/anchors. Every LateUpdate redraws the base
plus current effects into the same backdrop RT before native cameras render. Particles stay behind actors and
continue through cover/return without trails or a new RT each frame. The shared atlas loader uploads at most one
image per frame and releases after use; region changes discard emitter metadata with scenery.

`-dd3-crawl-scenery-check` creates an unsaved campaign and uses ordinary Driver APIs for native heroes, torch,
travel, curios, traps, obstacles, reversal, room battles and return in all four regions. No estate is selected or
persisted. Victories use debug damage/kills and native turn-pass events: this checks scenery and lifecycle,
not skill kills or encounter balance. The first battle is pinned to `fanatic_mash_211` to cover a Pit Fighter
surviving debug damage at zero HP; the fixture finishes remaining enemies with the existing native debug-kill
helper. `-dd3-scenery-check` separately cycles every installed panorama
with reference figures. QA flags are opt-in and require DebugKeys; remove them for play.

Native deferred lighting/HDR/bloom, water animation and foreground depth remain approximated. Existing diffuse
textures are sufficient; later water/lighting work needs native shader/effect reconstruction rather than generated
textures. Pointer/controller interaction and long-session performance need broader acceptance beyond Driver actions.

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
combat-camera export. Its PNG uses base color, tint and approximate depth haze; round233 supplies runtime VFX.

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
