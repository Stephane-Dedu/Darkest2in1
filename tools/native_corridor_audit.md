# Native corridor art audit, round216

## October 14 stage-framing update

The latest pack replaces the distant arena framing and mismatched foreground floor with a close,
hero-calibrated room/corridor camera and level native ground. All 32 room landmarks are re-rendered at
the crawl's scale, and 24 corridors now have a 4096x768 lateral view. Native Tangle dark foundations
and low wall bases no longer produce the former hard ground platforms. See `corridor_art.md` for
the camera contract, bounded wide-image allocation, emitter rebuild and native motion fixture.

The ordinary four-region native scenario passed actor travel, torch, curios, traps, obstacles, reversal,
room fights and same-scene return. A second native pass checked continuous forward/reverse/stop routes
and frame-by-frame crop continuity in all four regions. Private captures and a 38-second movement recording
are under `local-art/scenery-contact-v04`. Native profile hashes remained unchanged; no estate was selected.

Remaining limits are the baked plate's depth/parallax, flat water and approximate lighting. The walking
ground is deliberately level while native background terrain may rise behind it. More diffuse textures
are not needed for these fixes; fully physical foreground occlusion needs a separate rendering layer or
live native scenery, and water needs its shader/effect data.

## Round233 update

All 24 road panoramas are rebuilt with continuous ground below distant geometry, faded skirt/skybox/ground bases
and native lane posts/lamps moved behind the party. This fixes the opaque ground pass that caused the broad
horizontal stripe. Shroud water colour is an explicit approximation: its native ocean material has no diffuse
texture in the export. Existing 32 typed room PNGs are preserved.

Native extracted smoke/fire simulation sheets, mist/steam masks and sparks now animate the crawl and its in-place
battle backdrop. Fire positions in six Sprawl room scenes come from native fire-mesh bounds projected through
the room camera. Effects use the existing private quest-map pack; five atlases are shared and require no new
diffuse extraction or generated art. The panorama crop is frozen during combat while effect time continues.
See `corridor_art.md` for rebuilding, private provenance and opt-in native acceptance fixtures.

Remaining limits: approximate deferred lighting/exposure/bloom and fire light wash, still water, no independent
foreground depth, and the deliberately small bounded pan. More diffuse textures will not supply those systems.
Recover the ocean shader/noise/depth controls and native lighting/material passes for a further fidelity pass.
The unsaved Driver fixture exercises real native actors and gameplay actions, but does not establish pointer/
controller interaction, normal skill-kill balance, saved-campaign reload or long-session/frame-time performance.

## Round231 update

Whole-scene reflection/repetition is removed: one bounded panorama now preserves authored perspective and heading
orientation. The corridor source files need no re-extraction for this defect. The 8% overscan provides a small pan,
not a full physical hallway's worth of unique scenery. A longer lateral native render or layered live scene would
allow more travel; do not restore reflected full-scene tiles to manufacture it.

Native room arenas are reframed and supplied with native foreground ground by `tools/render_native_rooms.py`.
Boss, entrance, encounter, quiet and treasure purposes select separate scenery. Native city/forest exports lacked
foreground ground; Shroud piers stopped above the crawl footline. The renderer now extends existing native ground
materials, rather than requiring generated textures. Shroud's empty cultist scenery is replaced with Chirurgeon.
See `corridor_art.md` and MODLOG/PARITY for installed state and native acceptance.

Remaining visual gaps still concern ground-card boundaries, flat lighting, baked foreground props and static
atmosphere/water. Recovering native shader/emission/VFX data and depth-separated passes would help. More diffuse
textures or generated art are not required for the primary four regions at this stage.

## Original round216 findings

The first 24 native corridors are installed in the active DD2 private pack and
have been rendered by the real loader/panorama renderer in an unsaved native
window. They replace eight generated corridors. Battle arena art is unchanged.
Sources, prepared PNGs, manifests and screenshots stay outside Git.

## Four playable regions

| Region | Installed | Fit | Remaining weakness | Best next work |
|---|---:|---:|---|---|
| Sprawl |6|1.00x|Straight maroon band where distant ground cards end; reflected houses/roofs are conspicuously symmetrical; baked lane lamps lack flame/VFX.|Extend existing ground slabs under the far row; compose quieter segment ends; recover native fire/emission and fog settings.|
| Foetor |6|1.08x|Raw road sat above the party's feet; corrected. Flat farm/flesh lighting, distant hard edge and mirrored flesh arches remain.|Adjust camera/road geometry in the next render rather than relying on the crop; use existing ground/skirt meshes for the join and native material/light values.|
| Tangle |6|1.15x|Raw footline was nearly black below the road; corrected. Trees/base slabs still merge into dark masses and the floor remains darker than other regions.|Review base-colour/material multiplication and ground coverage; recover a readable path using existing road meshes and lighting.|
| Shroud |6|1.00x|Mooring posts/candles are baked into the lane, so actors always draw over them; hard sea/ground edges, static water and flat lighting.|Move lane props behind the path or export RGBA foreground/depth passes; add foreground rendering before claiming occlusion; recover water/fog effects.|

The six variants shuffle scenery while retaining the same road surface per
region. Reflection gives matching edges mathematically but creates obvious
visual twins in architecture/flesh arches. Longer segments or deliberately
quiet ends would improve this without new textures. All panoramas are static
single planes: no independent parallax, foreground depth, flames or water motion.

No generated replacement textures are currently needed for these four regions.
Their native buildings, vegetation, road surfaces and skyboxes are already
present. Fix composition, material interpretation and layers first. DD2 lighting
and VFX are not reproduced by unlit base-colour renders and baked haze/soil;
extracting more diffuse textures alone will not fix that difference.

## What to extract next

1. Resolve material dependencies used by selected road/ground/sky meshes:
   colour tints, emission masks, fog/light controls and any secondary maps the
   native shader uses. Record names/values before choosing an offline equivalent.
   Round215's live Hamlet also found road `_FOG`, `_HASLIGHTING_ON`,
   `_NEARDARK_ON`, distance darkening and dithering; blindly applying these at a
   different camera origin can produce black slabs.
2. Export foreground-only transparent passes and depth/masks from existing
   Shroud posts and Sprawl lamps. A background PNG cannot reproduce actor
   occlusion; this requires a renderer change or repositioning those props.
3. Extend existing far-ground rows and reframe the camera at the actual y680
   footline. This is a render/layout task using available assets. Check repeat
   joins at several scroll positions.
4. For future biomes, resolve empty geometry dependencies before generating
   substitutes. Road-index counts are below. "Unresolved" counts assets with
   any unresolved references; it is not a count of missing diffuse textures,
   and some empty prefabs can be VFX-only.

| Road biome | Indexed assets | Empty renderer exports | Assets with unresolved references |
|---|---:|---:|---:|
| City |82|2|36|
| Farm |110|3|11|
| Forest |82|0|2|
| Coast |69|4|19|
| Valley |83|4|6|
| Caves |63|1|27|
| Tundra |46|44|46|
| Other |20|0|6|
| Total |555|58|153|

Valley's four panoramas remain uninstalled: no Valley campaign region plan
exists in the loader. Caves and Tundra have no prepared corridor set or campaign
region plan here. Tundra needs a dependency-aware re-export of meshes/materials.
Mountain's three empty organ arena renders need scene/bundle dependencies
resolved. The reported plain lower third in Sprawl and Tangle **arena** renders
needs the floor supplied separately by the game; these arenas were not installed
by this change. Mountain gameplay/new region plans remain separate work.

Generation is a fallback for genuinely absent authored material after native
recovery, e.g. a connecting ground transition unavailable in source assets.
Keep additions distinct in provenance; this pack contains none.

## Evidence and acceptance limits

`local-art/corridors-native-v01/corridors-manifest.json` records source/output
hashes, 2880x1080 dimensions, fit factors and foot-band luma. Installed copies
hash-match. Both native checks logged all 24 choices after their pool was ready,
through the actual renderer with forward/reverse scrolling. V02 places full-body
DD1 reference figures at the crawl anchors/y=680: framing evidence, not a test
of the animated DD2 party. No save was selected or changed.

After the second check had logged PASS, native shutdown logged the previously
observed `KeyNotFoundException: IMainInstaller` in `Globals.InvokeUninstallCallbacks`
and `GameInstaller.OnUninstallFinished`. No scenery exception was logged. Its
cause is unverified; do not describe the whole session as exception-free.

Still test in a real estate2 quest: native 3D feet/size; walking/reversal at both
ends; curio/trap/obstacle visibility/input; high/low torch; room entry and the
in-place battle snapshot/return; repeated expeditions and memory use. Previous
generated-pack quest acceptance cannot establish these for the new art.
