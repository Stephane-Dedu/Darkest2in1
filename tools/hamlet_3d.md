# Perspective Hamlet

Round271 addresses the owner's ground screenshots and removes the Sanitarium nurse.
Streets use DD2's unmodified cobblestones_base tile at4.8m, with repeat wrapping instead
of mirrored rocks. Generated1254px earth supplies small gravel, clay wear and dead leaves;
native grass remains, with finer4.2m mapping, fuller sheltered margins and187 plants grouped into irregular clumps.
The lane image already blends final ground pixels, so the second runtime paving tint
is removed. Terrain stays4096px with512px construction readbacks and cached worker bakes.
Two optional private PNGs prepare asynchronously and retain the previous native fallback.

Enlarged native samples proved the atlas UVs and alpha clipping are correct. The early
small, heavily tinted tufts exposed the painting's black basal mass and read as stumps.
Final plants are grouped, taller and brighter, with 35% planted below the soil. All 187
plants preserve route/apron/slope clearances. The runtime uses explicit alpha clipping.
No new grass art was generated. Diagnostic giant cards/readback are excluded from the build.

The static Sanitarium nurse and the blue robe-shaped foreground Ancestor figure are
removed. The latter matches the figure in the owner's road screenshot. Its stone plinth,
steps, native candles and memorial service remain. Both legacy authoring paths now omit
the figure. Eight other GLBs are byte-identical to round270. The first model refinement
caught 30 figure/detail meshes rather than the 14 old base meshes; the final recipe removes
all 30 and preserves the native candles and retained model geometry/UVs. The exposed
memorial plinth replaces its stretched tiny crop with the reviewed limestone painting
and native small paving at a consistent world scale.

Release build, 1268 Core checks and 284 UI checks pass. The final vegetation revision
passes all 72 focused Hamlet checks. A frozen-checkout catalog-path
failure was resolved by supplying the exact installed catalog, then rerunning that case.
Paint snapshots/count/wear expectations reflect the intended new art; route/apron/slope
invariants remain. A new lane-margin case checks exact agreement with the ground image.
Native unsaved ten-service/camera/release/reload/cache/resize/keyboard checks pass; no
estate selected or saved. Final evidence and deployment hashes are recorded privately in
local-art/ui-overhaul/hamlet-ground-v23. Foreign gameplay/curio/Shieldbreaker work excluded.

Street cobbles and ground grass are native 512px paint; tuft atlas is 4096px.
Generated earth and the reused v20 limestone painting are not native extraction. Ground painting is
still flatter than native dressed scenes, service plinths retain larger paving, and fuller
lighting/foliage treatment remains. No full frame-time/long-session claim. The old offline
town_preview approximation does not yet reproduce this ground; use the native captures.


Round270 replaces the forge's intersecting native cottage and slate canopy with one
continuous thick thatched roof, fitted dormer, real chimney openings and enclosed timber/
lime workshop. A higher eave reveals the anvil and recessed furnace; the sign hangs clear
of the roof. Three generated native-reference paintings supply detailed reed, stained
lime and static furnace fire. This is generated paint, not newly extracted native art.

Three offline iterations corrected the low eave, hidden sign, glass-textured hearth and
side-window placement. The final exported smithy has 5,630 triangles and ten embedded
images, re-imported for topology/bounds/paint checks and four review views. Native camp
barrel/crate geometry and UVs are retained; only the cottage is explicitly rebuilt.
Nine other service GLBs are byte-identical to v21. Ten-model decoded-pixel audit excludes
the six old DD1 detail bitmaps. Prepared inputs exactly match rendered texture files.

Asset-only round: normal267 plugin unchanged; no new C# test claim. Native unsaved fixture
passes ten services, camera, full release/reload/re-entry, Embark cache/resume, resize,
keyboard and still-camera/smoke. No estate selected or saved; no live OS input. Inspect
native-overview.png, native.log, native-buffer-probe.json and deployment.json in
local-art/ui-overhaul/hamlet-smithy-v22 for final visual, allocation and install evidence.

Remaining work: wagon end grain/wheels, service-specific facade wear, remaining old
service architecture and broader lighting. Thatch repetition and straight roof side
edges can still improve. The furnace painting is static. Package is 164.2 MB/64 hashed
files and still duplicates embedded images in buffers; no full performance claim.
NPC/upgrade exteriors, saved transactions and forced-reload portrait defects remain.


Round269 refines the forge, Nomad Wagon and Guild crest. The forge has a thick slate
canopy, correctly oriented small firebrick courses, recessed hearth, faceted iron anvil
and working-face detail. The wagon has worn plum canvas without the repeated atlas,
fitted door boards, open stair treads and enclosed side/rear walls. The Guild crest has
tapered blades, guards, pommels and a steel-edged shield. Three new generated paintings
use native DD2 references; an earlier cracked iron painting was rejected. Native cottage,
Hoarder and camp component geometry/UVs are preserved. Seven source GLBs are byte-identical.

Three exported-model review passes caught rotated chimney UVs, oversized stair grain and
the original wagon's open sides. Final GLBs pass re-imported topology/bounds/image checks;
the complete ten-model audit finds none of the six old DD1 detail bitmaps. Asset-only:
normal267 plugin unchanged, no new C# test claim. See private native.log and deployment.json
for exact final native acceptance and installation, rather than inferring it from renders.

Private evidence and reproducible inputs: local-art/ui-overhaul/hamlet-workshops-v21.
The remaining native cottage thatch is softer than the new canopy, forge roof joins need
further architectural work, and wagon end-board grain remains large. Broader lighting,
NPC/upgrade exteriors, saved transactions and long-session memory are unfinished. The
packager still duplicates embedded image payloads; this round does not fix that cost.


Round268 responds to the owner's screenshot of bland shared brickwork and mismatched DD1
details. Abbey/Guild/Sanitarium now use separate paintings: worn limestone, warm masonry with
broken plaster, and cool damp limewash respectively. Nine native-reference paintings include
those walls, slate/clay roofs, continuous wood and three service emblems. Original DD2 atlases
remain style references; generated paint is not described as native extraction. Low-resolution
wood/plaster strips are replaced on the rebuilt services, stone trim uses a selected worn block,
and paving is finer. UVs retain metric courses and follow each timber's long axis.

Guild banner, forge sign and wagon eye have new DD2-style paintings. Window grain comes from
DD2's Academic house, with physical rose tracery replacing the Abbey's DD1 sprite. Pixel-hash
inspection of all ten exported GLBs finds none of the six legacy DD1 detail inputs. Five GLBs
are unchanged; Abbey adds720 triangles (9393 total), all models remain under45k. Two iterations
were rendered after GLB re-import. The source preparer reproduces ten selected textures exactly.

Asset-only change: the installed round267 plugin is unchanged, and gameplay WIP is excluded.
Native unsaved fixture passes ten world services, Escape, camera, full release/reload/re-entry,
Embark park/cache/resume, resize, keyboard and still-camera/smoke. No estate selected or saved.
Private package: ten models,61 hashed files,117.5MB. The existing packager duplicates embedded
image payloads in mesh buffers; package size/long-session memory remain optimization work.

Evidence, originals, prompts, provenance, both mesh iterations, audit, native captures and fresh
baseline/deployment records: local-art/ui-overhaul/hamlet-material-v20. Further quality work:
forge roof/hood/anvil, wagon joinery, physical Guild crest, bespoke facade wear and more native
lighting integration. Do not claim final DD2 production-art parity from texture detail alone.

Round267 combines a fuller town with rebuilt Abbey, Guild and Sanitarium architecture.
Fifteen grounded native DD2 lots, a windmill and three streets use shared terrain pads and
vegetation clearance. Native decorative building paint retains up to2048px; cached terrain,
lanes and valley paint use4096px (192MiB retained managed paint, plus GPU textures).

The three services now have cut door/window recesses, physical arch stones, sagged thick roofs
and side windows. The Abbey has its DD1 rose and a native Sprawl tower. The Guild has a plaster/
timber upper hall, oxblood dormers and sword standards. The Sanitarium combines a uniformly
scaled native Academic house with a stone turret, green casement and apothecary porch.
Seven other models match the previous approved package. Abbey's terrain pad is widened.

Private v19 materials combine native wood, plaster, cut stone and courtyard paving with the
v18 generated ashlar/slate/red-roof paintings. The v17 generated masonry was rejected and is
not installed. Optional v17 sky/cliff paintings remain, with native fallback. Provenance and
prompts stay outside Git. All generated art remains under visual review: the new masonry is
still too regular beside native DD2 facades, and the other seven services need further review.

Frozen HEADa2914aa plus owned Hamlet files excludes concurrent gameplay/Shieldbreaker changes.
Release,1267 Core and284 UI tests pass. Unsaved native fixture passes ten services, Escape,
pan/zoom/reset, full release/reload/re-entry, Embark park/cache/resume, resize/restore, keyboard
and still-camera/smoke.208 renderers; zero repeated buffer allocations over5.032sec. This does
not establish full frame-time/long-session performance or saved campaign transactions.
Evidence and guarded deployment scripts: local-art/ui-overhaul/hamlet-material-v19.
Protected estates1/3 were never accessed. See that folder's CONTINUE.md for current deployment.
The following round266 notes are historical where they conflict with this pass.

Round266 follows the owner's request for a more cinematic, less sparse Hamlet. The reference is the
town view at 25.5 seconds in DD1's installed `video/epilog.ogv`: low, overlapping roofs, light sky and a
small estate on a distant ridge. The native overview is
`local-art/ui-overhaul/hamlet-cinematic-v16/native-overview.png`. Reference frames, failed studies,
source hashes, isolated build, QA logs and rollback are alongside it.

The camera targets (12,12,20), offset (2,36,-112), with a 37.5-degree vertical FOV. Larger service
footprints fill the frame, and the Survivalist moves inward with a corrected stair landing. The
original dark native cliff paint and texture scale remain, with darker rock/paving and stronger
light/shadow grading on building facets. Three native Valley cottages fill roof gaps. Four native
Sprawl wall/tower assemblies form the small distant estate; they have no service ID or picking bounds.
The former huge Mountain and starry Camp sky are removed. Only a cropped cloudy sky from the installed
DD1 town background is used on a distant plane; every building is still 3D. The existing image cache
loads it before readiness, and owned sky geometry/materials release with the stage. Missing source art
uses a neutral sky.

The previously approved round242 private package was still absent from the install. This pass installs
its Abbey, Guild, Sanitarium and Tavern trim with the other six unchanged models. The 54 hashed assets
are in `local-art/ui-overhaul/hamlet-surface-v15/HamletModels`; no extracted models/paint belong in Git.
The estate title/controls are smaller, hints appear on control hover, and the roster header has a dark
backing against the brighter sky. World input follows the actual title/control bounds.

Release + 1,267 Core + 284 UI checks pass in a frozen HEAD+Hamlet build, excluding the other agent's
gameplay WIP. Native 1600x900 checks pass all ten world services, Escape, pan/zoom/reset, full release/
reload/re-entry, quest-selector cache/park/resume, 1440x900 resize/restore, keyboard and still-camera/
smoke controls. Final 1920x1080 overview is inspected. 189 renderers, four chimney plumes, native HDR
target; five-second buffer probes show zero repeated reallocations. This is buffer stability, not a
full frame-time benchmark. Unsaved fixture, no selected estate; native profiles/config/test estate
restored after QA.

The forced full resource-release/resize stress fixture can leave roster portrait slots black; a fresh
launch displays them correctly. The cause is not established by this art pass. Saved transactions,
controller comfort, arbitrary text/window sizes, long-session performance, NPC/upgrade exteriors and
bespoke painting/sculpting of inferred architecture remain open. This pass does not claim DD2
production-art parity. Earlier Mountain/camera/package-install directions below are historical.

The 3D Hamlet is the standard view. Round239 removes painted/hybrid/DD1 town drawing completely.
Cold entry and retry show an opaque torch page until the actual town has rendered; failure offers Retry
scenery or Main menu. The retired view flags/switches remain removed. Camera motion and Reset view remain.

Quest selection, hero sheets and provisioning retain one existing scene and its render texture, with its
camera and particles parked. Return resumes that scene. An actual departure releases it after three unused
frames. Expedition results prewarm the town behind the results page. A resize while parked requires a new
render before presentation. The cache does not read estate files or start loading on a map-only preview.

Round241 accepts the visual pass in an unsaved native preview. Broken ledges and grounded outcrops,
service-yard props, restrained roof/wall tint bands, localized entrance/contact paint, quieter paving,
50 smaller grass clumps and two distant mist layers improve depth while preserving the existing GLBs,
service positions, routes and camera. The original painted UVs and warm window paint remain intact.

Release + 1,145 Core + 190 UI tests and 53 Hamlet cases pass. The native fixture passes services, controls,
full release/reload, cache identity and park/resume, live resize, keyboard and still-camera/smoke checks.
The final overview was inspected, with zero repeated render-buffer allocations in a five-second probe.
This is buffer stability, not a frame-time or long-session benchmark. Private evidence and packed offline
scene: `local-art/ui-overhaul/hamlet-set-dressing-v13`.

Round239's black IMGUI fill covered its Canvas flame. The opaque background now belongs to the torch Canvas,
below the flame; IMGUI labels/retry controls remain above it. A native cold capture verifies the visible flame
and opaque margins. `check_loading_capture.py <private-window.png>` reproduces that screenshot assertion.
The failed and fixed captures are retained privately. Both Hamlet preview aliases work with measure_native.ps1.

Real saved results/transactions, default map/provisioner/estate-picker torch fixtures, custom facade/NPC/upgrade
art, controller/arbitrary text/window sizes and full performance remain open. Historical painted rollback and
selector-cleanup instructions below are superseded by this section and the latest handoff.

`Dd2/HamletStage` owns a perspective camera and a render texture matching the window and native HDR format.
Its fixed 16:9 composition follows the virtual UI canvas through window resizing. It copies visual components from
installed Addressables assets into a distant scene; it never instantiates native road nodes, shops, triggers,
inventory behaviours or animators. Native meshes and textures stay in the owner's install. Skinned geometry
is baked once into owned static meshes. Buildings and scenery retain DD2's native Environment materials,
texture mapping, depth and cutouts, with road-specific lighting, fog, distance darkening and dithering disabled.
Owned terrain uses the installed URP Unlit shader and the Hamlet camera uses the installed featureless renderer;
see round236 below. Shared native objects are never edited. Any temporary camera shader globals are restored
immediately after rendering.

Round219 replaces the ten service placeholders with the verified v02 building library. Install its private
runtime package under `Paths.NativeRoomSceneryFolder/HamletModels`; see [the packaging steps](hamlet_models/README.md).
The package includes all ten exteriors, their original UVs and paint, DD1 architectural details and service
emblems. Disk parsing and SHA256 verification run on a worker. Unity uploads one texture per frame, then builds
one service per frame. Node transforms and painted base/emission factors survive import; primitives are batched
by material and two-sided cloth retains back faces. Every uploaded texture, mesh and material is owned by the
stage and released on rollback. No native shop behaviours or campaign save paths are instantiated.

Round220 reshapes `Core/Presentation/HamletLayout` around the installed DD1 Hamlet layout and assembled
level03 art. The Survivalist stands on a high left cliff, the Sanitarium behind the Tavern, the graveyard
below the Abbey, and the Guild above the right-hand forge. Coach, statue and wagon occupy the lower square.
Service foundations span 24.5 world units vertically. The bounded landscape is 124x105 rather than 300x300;
service centres span 53 units in depth rather than 64. Larger Abbey/Sanitarium footprints restore their
landmark roles. A lower, more frontal perspective retains space for the right roster and footer.

`HamletTerrain` bakes faceted soil and rock faces, closed edge skirts, lanes, stair treads, the irregular
square and retaining courses on the model-loading worker. All ten foundations use rotated rectangular
pads sized from v02 model bounds; doors connect to one road network. `HamletGroundwork` combines paving
and masonry before Unity upload, avoiding per-tread objects/renderers. Native painted soil, cultist rock
paint and foundation cobbles distinguish landscape from paths. Their texture copies repeat without
changing shared native resources, and their handles/textures release with rollback. Valley scenery and
camp props follow the new ground; the enlarged native sky covers the more frontal camera. The loaded road
plates supply a material and are hidden once the terrain is ready. No extracted game art is committed.

World picking selects the nearest original primitive bounds and calls the existing `HamletUi.Open` dispatch.
Picking keeps those smaller bounds even when material batching combines a whole building into a few renderers.
Unlocks, treatments, stock, upgrades, roster and campaign rules remain those of the ordinary Hamlet.
The wheel zooms, middle-drag pans, Reset view restores the overview, and opening/closing a service smoothly
moves the camera. Navigation, roster/footer and modal overlays block world input over their pixels.
Quest selection retains the town without rendering. Leaving the town/selector flow releases the stage after
three unused frames. Returning from actual expedition results prewarms before the town becomes visible.

For native QA, launch with debug keys enabled and `-dd3-hamlet3d-preview -dd3-hamlet3d-check`.
The fixture uses an unsaved campaign, selects no estate path, suppresses persistence only for that object
and blocks real expedition embark. It injects IMGUI events without desktop input: ten service clicks and
Escape closes; zoom, pan and reset; explicit full scene release; automatic reload and another world click;
footer Embark; cache identity with parked rendering/particles; Back to Hamlet and immediate scene resume. The preview flag overrides
camera motion only in memory. Close the owned test process and relaunch without QA flags for play.

Private round219 package, captures and logs are under `local-art/ui-overhaul/hamlet-ingame-v02/`, outside Git.
Pre-change plugin/config snapshots are named `hamlet-v02-plugin` and `hamlet-v02-config` in the local
universal-modder backup store. The first native run passed the complete check. A brighter terrain/front-wagon
framing pass then passed all ten clicks, controls, rollback, reload and Embark cleanup. The owner stopped
launches for 30 minutes before its final return-to-town check. The QA process was closed; DD2 was not relaunched.
The owner renewed the pause during round221. Do not launch any game before 2026-10-11 17:47:37 UTC /
19:47:37 Paris unless the owner changes that instruction.

Round220 was checked entirely offline, including after that pause elapsed. Release and all 1030 Core +
182 UI tests pass; five layout/geometry cases check grounded rotated footprints/doors, connected paths,
compact extent, relative building elevations and finite nondegenerate terrain/stair/wall meshes.
`tools/hamlet_models/LayoutExport` writes the actual Core geometry and camera for `town_preview.py` to
render with the ten hash-verified GLBs. Private evidence: `local-art/ui-overhaul/hamlet-landscape-v03/`.
The 1920x1080 preview and packed Blender document establish composition, not native lighting, picking,
frame time or saved-campaign acceptance. Repeat the unsaved native fixture for the new layout when testing
resumes. Prior round219 native results apply to its older terrain. Immediate Painted town rollback remains.

Round221 responds to the dark filler and distant Mountain in the owner's offline screenshot. `HamletPalette`
shares the grading and atlas windows with the preview. Soil now samples the painted top of DD2's
`farm_ground_hill_transition`, leaving out its grass fringe. Cliffs sample the rock-only upper strip of
`cultist_barricade_rocks`, leaving out the skulls. Repeatable GPU copies sample these windows without
editing shared game textures. Larger world periods and a lighter facet shade reduce small black detail.
Owned native materials enable the verified `_COLOR_CONTROLS` keyword with `_UseColorControl`, `_Brightness`
and `_Saturation`. Vertex texture painting is disabled on these copies; its native channel defaults can
otherwise obscure the paint. Four static native military rubble clusters dress the cliff bases.

The Mountain moves from Z123 to96 and width100 to174. Independent height/depth scales keep its base behind
the Abbey while raising the visible peak. It uses a graded native Environment material instead of the
previous Sprite material. The camera rises slightly and widens from40 to42 degrees. All ten projected
building bounds still fit left of the roster and above the footer in the offline render. The v02 building
package, routes and service foundations are unchanged. Private v04 PNG, packed scene, geometry, surface
provenance and three extra native prop exports are in `local-art/ui-overhaul/hamlet-landscape-v04/`.
The preview approximates native color controls and facet shading; native shader/picking acceptance
remains pending. No game was launched and no estate was accessed during this pass.

Round223 establishes native acceptance after the timed launch pause expired. The unsaved fixture passes
all ten service clicks/Escape, pan/zoom/reset, painted rollback/resource release, reload/re-entry click,
Embark cleanup, return, and live 1600x900 -> 1440x900 -> 1600x900 resizing. Every service retains a clickable
point at both sizes. Private captures/logs, the final offline scene and composition are under
`local-art/ui-overhaul/hamlet-immersion-v05/`. No estate path was selected or saved.

Native `_COLOR_CONTROLS` did not visibly lift the black atlas areas in this environment variant.
Construction now bakes tint, luminance saturation and linear brightness into bounded owned texture copies:
512px for terrain/rubble and 1024px for the Mountain. Readback occurs once per distinct paint, never during
rendering; CPU pixels are discarded after upload. Neutral encoding, lifted blacks/highlights and saturation
are covered by five Core cases. Cliff crop height grows from24% to46%, retaining only the skull-free top.
Terrain UVs preserve the rectangular crop's texel proportions. Three native material tint bands restore
directional facet shading without relying on the shader's unused vertex colours. Paths receive warmer paint.

The Survivalist ascent winds across the cliff instead of climbing in one steep line. The graveyard shares
the civic ascent, with a separate path down to the Sanitarium. One additional Core case bounds all stair
grades at0.9 rise/run. Low broken walls frame the forecourt. Four native Valley leaf banks and two native
Coast cliff ledges soften the outer terrain; no native node behaviours are instantiated. Four small owned
particle plumes rise from the actual Tavern, Sanitarium and forge chimney caps. Their texture, shared material
and systems release with the town; no extracted VFX or game scripts are required.

Native allocation diagnosis: the original1920x1080 LDR target shared a URP renderer with DD2's1600x900 HDR
camera, causing roughly480 RTHandle reallocations/sec. Matching size removed depth churn; copying allowHDR
alone left roughly240 colour reallocations/sec because the target was still LDR. DD2's main-menu camera
is untagged, so Camera.main is null. Find the active native view, copy its renderer/HDR setting and select
the actual format through URP's own format selector. Cache that selection and follow window resizing.
The final1600x900 B10G11R11_UFloatPack32 target records zero repeated allocations in five-second probes
after reload/return. `tools/hamlet_models/measure_native.ps1 -HamletProcessId <owned-preview-pid>` reproduces
the log measurement, rejects a non-preview process and requires the3D town to have rendered first.
This establishes buffer stability, not a full frame-time or long-session memory benchmark.

NPC idles, upgrade/event exteriors, the coarse Ancestor sculpt, individual trim painting and final lighting
still need art work. Saved-campaign transactions/embark need separate acceptance. This preview checks
presentation, routing and resource lifecycle without selecting an estate. Painted town remains immediate rollback.

Round225 addresses building glow, the isolated plateau and fading road/wall paint. The GLB author's emission
was a preview aid for native albedo, often1.2-1.55. `BuildingTint` normalizes that painted-only channel and
uses ordinary base colour for architecture; genuinely brighter glass/flame keeps its light. Owned1024px
paint copies modestly desaturate building textures without changing their private models or shared native assets.

`HamletLandscape` extends the town's exact boundary into a continuous lower valley. Ten background placements
reuse seven Valley tree groups and three low Camp treelines. Camp strips keep their original painted silhouette
material and alpha through an owned Sprite material; grading the entire strip made pale overlapping rectangles
in the first native iteration, which was rejected. The camera, service sites, game dispatch and HDR target are retained.

Native rough-road stone now paints paths, tileable `bricks` paints the terrace faces, and
`foundation_panel_06` paints the retaining courses and stairs. These are installed common-bundle textures,
with dark ink joints and restrained shadow lift. Surface copies retain1024px paving/cliffs and512px masonry.
The soil gets a one-time1024px world-space bake of native earth/gravel and broad damp/dry stains.
All source images/exports, comparison captures and the packed offline scene stay private under
`local-art/ui-overhaul/hamlet-landscape-v06/`.

The owner's native screenshots revealed a shallow entrance being treated as stairs: four oversized treads
stretched their height-based UVs across long top faces. Stairs now require a grade above0.18 as well as0.45
rise. Gentle roads remain ramps. Real treads and landings are a separate surface using one course material;
the continuous cobbled lane does not run through them. Box UVs use width/depth/height for their actual face,
and all paving branches share world UVs with the square. A continuous diagonal wall projection avoids
per-triangle atlas flips at cliff corners. It trades some side-face scale variation for uninterrupted courses.
Six Core cases check valley continuity, finite unoccupied surroundings, building light treatment, road/stair/wall
UV contracts, retained stone contrast and ground variation. The existing black-lift test now explicitly tests
a requested grading operation, independently of the intentionally darker current stone palette.

Final native1600x900 unsaved fixture passes all ten service clicks/Escape, pan/zoom/reset, painted rollback
with complete resource release, reload/re-entry, Embark/cleanup/return and1440x900 resize/restore.
The140 mesh renderers and four chimney plumes retain the same HDR target. Final probe records zero repeated
URP allocations over5.024 seconds. Screenshots confirm the repaired entrance and separate stair flights.
Release and all1044 Core+182 UI tests pass; both DLL/PDB pairs match installed hashes. Owned QA PID76868 is
closed. No estate selected/read/saved. The packed offline scene contains all scenery with no missing exports.
Saved transactions, finer art/variants and long-session/frame-time acceptance remain separate.

Round226 adds native grass on unused ground. `HamletVegetation.Cover` fades the ground paint away from
route edges and rotated building footprints, including their entrance aprons. The one-time world bake is
now2048px, with512px source readbacks and a256x256 clearance field. Native `grass_dry` supplies short turf;
the surrounding valley shares its restrained olive paint. Stone paths and cliffs retain round225's materials.

The native Valley/Foetor/Tangle grass tiles reviewed are512px. A sharper4096px Foetor gate atlas contains
three usable reed/grass silhouettes. Reviewed UV rectangles exclude the neighbouring gate, bell and masonry.
The original atlas is bound directly, retaining its resolution and alpha without an extracted runtime pack.
170 low tufts on gentle terrain share one owned mesh/material. Two angled planes per tuft give depth;
native wind is disabled because these owned planes do not carry the source wind weights. Grass never
participates in picking. Both new Addressables handles, the mesh and owned paint release on rollback/exit.

Three Core cases verify walking/entrance clearance, grounded placement and valid cropped geometry.
Release and1068 Core+182 UI pass. Private v07 preview/packed scene includes every scenery export.
After the owner approved closing the ordinary session, deployed with matching DLL/PDB hashes. Native unsaved
QA PID86776 passes alpha/crop review, all ten services/Escape, controls, rollback/resource release, re-entry,
Embark/cleanup/return and1600x900->1440x900->1600x900. Final141 renderers/four chimney systems retain the native
HDR target;0 repeated URP allocations over5.029 seconds. No warning/error during acceptance. QA closed, no
ordinary game relaunched and no estate selected/read/saved. Evidence: `local-art/ui-overhaul/hamlet-grass-v07/`.

Round232 implements the approved round230 camera/background study. The home view uses a22-degree downward
pitch,3-degree yaw and36-degree vertical FOV, retaining the ten DD1 service sites. The full-size Mountain is
offset right/down from the Abbey. Selection uses a smaller6% eased zoom; hovering does not move the camera.
Reset restores the home view immediately. Quieter cliff/retaining paint leaves the original ink joints and
building albedo intact. Building hover lift is restrained and the native name banner has a dark backing.

The main road now reaches the town boundary and continues as a graded, painted ribbon into the valley.
The surrounding mesh has denser rings, broad irregular wooded shoulders and fourteen native Valley groups,
replacing the three Camp treeline cards. Root banks are buried using nine height samples across each footprint.
Near-town soil/gravel/grass treatment continues across the boundary; distance haze affects only the outer
paint. The additional2048px owned valley map uses512px source reads and a256x256 clearance field at construction,
with no per-frame readback or bake. It and all scenery resources release on painted rollback/exit.

Tab/Shift-Tab cycle services in screen order; arrows choose a service in that screen direction. Enter/Space
opens the focused service through existing dispatch; Escape closes it. Pointer input returns to mouse hover.
World navigation is blocked under the header, roster, footer and active overlays. The new Camera motion button
persists `Look.HamletCameraMotion` (defaulttrue). Off keeps a still overview and stops/clears chimney smoke;
explicit pan/zoom/reset remain available. The unsaved fixture overrides both feature/motion choices in memory.
`Look.Hamlet3D` remains defaultfalse; Painted town is still immediate rollback.

Release and1097 Core+188 UI tests pass. After the final ridge-height refinement, all42 Hamlet tests pass.
The1600x900 native unsaved fixture passes ten service clicks/Escape, pan/zoom/reset, painted rollback/resource
release, reload/re-entry, Embark/cleanup/return,1440x900 resize/restore, keyboard focus/Enter/back and motion
off/on. It verifies unchanged camera position while a service is open with motion disabled and stopped/resumed
smoke. Native overview and a service panel were inspected.145 mesh renderers,168 grass clumps and four plumes
retain the matched HDR target; zero repeated URP allocations over5.055 seconds. This is buffer stability,
not a full frame-time or long-session memory benchmark. Controller navigation, arbitrary window/text sizes,
saved transactions, NPC/exterior variants and final art/lighting remain separate acceptance.

Private native logs/captures, final packed Blender preview, source/install hashes and semantic config comparison:
`local-art/ui-overhaul/hamlet-overlook-v08/`. Offline shaders remain approximate; native captures establish
appearance. The isolated compile snapshot preserves concurrent tracked hero code already in the shared build;
that code is outside the Hamlet commit. Four installed DLL/PDB hashes match the verified Release files.
Config only gains the default motion setting. Pre-deploy plugin and config backups are universal-modder
`hamlet-before-overlook-v08/20261012-134633.zip` and `hamlet-before-overlook-config-v08/20261012-135139.zip`.
Owned QA PID95980 is closed, DD2 remains stopped, and no estate was selected/read/saved.

Round236 repairs material fidelity before further building sculpting. Building paint copies preserve the source
atlas aspect and native resolution up to2048px on the longest edge; small images are never enlarged to a square.
Grading retains alpha, including zero-alpha native road backing meshes. Model materials use the installed
Environment shader's `_____IsAlphaCutout` property and `_____ISALPHACUTOUT_ON` keyword. Owned texture uploads
use4x anisotropy. The ten v02 GLBs, sites, door connections and camera composition remain the same.

Native captures isolated a colour mismatch: binding URP Unlit alone left the town blue/grey because the main-menu
renderer still ran its LUT feature with camera post-processing disabled. The Hamlet camera now selects the
installed `Universal_Featureless_ForwardRenderer` by name in the pipeline's existing renderer list. No native
camera, renderer feature or pipeline array is changed. Only owned terrain/paving/cliff/masonry materials bind
the explicitly loaded `Packages/com.unity.render-pipelines.universal/Shaders/Unlit.shader`; buildings, sky,
Mountain and vegetation retain their native shaders. Missing renderer/shader falls back to Painted town.
The new shader Addressables handle releases with the existing stage resources. Matching native HDR/window
targets remain. Temporary ambient globals are neutral white and restored after the owned camera renders.

Both paint readback paths explicitly set and restore `GL.sRGBWrite` for the active colour space. This follows
Unity's [Graphics.Blit documentation](https://docs.unity3d.com/2022.3/Documentation/ScriptReference/Graphics.Blit.html)
and [GL.sRGBWrite contract](https://docs.unity3d.com/2022.3/Documentation/ScriptReference/GL-sRGBWrite.html).
The native fixture additionally requires the installed terrain shader and transparent backing materials.
`-dd3-hamlet3d-paint-audit` optionally shows a raw baked-ground thumbnail in this unsaved fixture only.

Cliffs use the native `Farm/Props/farm_eroded_hill` paint rather than repeated dressed bricks. Its960x760
rock-only window excludes the atlas silhouette/bottom; vertical UV scale uses that window's texel aspect.
The12m world period preserves readable fractures without the rejected Coast crop's huge strokes. Paving
brightness is reduced while retaining its ink joints. A256-square construction field supplies bounded bank/
building contact darkening and gravel wear around routes/aprons to the2048px ground bake. Open ground remains
unshaded; no per-frame painting/readback is added. Distant native woodland has a cooler, quieter grade and
the valley haze reaches its existing cap sooner. All native art remains private and is loaded from the install.

Private evidence and the packed final offline scene are under `local-art/ui-overhaul/hamlet-fidelity-v09/`.
Native screenshots are the appearance reference; Blender still approximates native rendering. Full DD2 art
parity remains open: custom cliff silhouettes, finer facade/roof trim, NPCs and upgrade/event exteriors need
further work. Controller comfort, arbitrary window/text sizes, saved transactions/real embark and full frame-
time/long-session memory acceptance remain separate. Keep `Look.Hamlet3D` defaultfalse and Painted town rollback.

Final round236 Release and all 1,135 Core + 188 UI tests pass. The unsaved native fixture passes ten service
clicks/Escape, pan/zoom/reset, painted rollback/full resource release, reload/re-entry, Embark/cleanup/return,
1600x900 ->1440x900 ->1600x900 resize/restore, keyboard focus/Enter/back and still-camera/smoke toggles. The
shader and transparent-backing assertions pass; the final native overview was inspected. Native grass, rock and
earth bind successfully, and the offline export resolves all six surface sources. The 145 mesh renderers,
168 grass clumps and four chimney plumes retain the native HDR target. Zero repeated URP reallocations over
5.053 seconds establishes buffer stability, not frame-time performance. All 15 owned source files match the
frozen build; both installed DLL/PDB pairs match, and config is byte-identical
to its pre-pass snapshot. Pre-deploy backups: universal-modder `hamlet-before-fidelity-v09/20261013-142649.zip`
and `hamlet-before-fidelity-config-v09/20261013-142650.zip`. Owned QA PID25176 is closed, no ordinary game was
relaunched, no estate was selected/read/saved and no OS input was sent. Concurrent Shieldbreaker sources remain
preserved in the shared build and outside the Hamlet commit.

Round237 responds to the Mountain looking small and isolated, with too much scenery visible behind its sides.
Six offline placements compared width, height and buried-base framing using the same native mesh and material.
The final placement changes width174 ->500, ground pivot(28,-41,96) ->(-12,-78,112), heightScale.85 ->.52 and
depthScale.34 ->.15. Because normalization uses width, actual height grows from about86 ->151 world units.
The broad lower mass sinks beyond the civic ridge; its front bound remains aroundZ80, behind all ten service
sites and the town's Z69 boundary. The cropped peaks and outer slopes give an enclosing backdrop in the home
view. The camera, services, feature preference and immediate Painted town rollback remain.
`LayoutExport` uses the same placement. Private studies, full packed scene and native evidence belong under
`local-art/ui-overhaul/hamlet-mountain-v10/`. The native screenshot remains the appearance reference.

The owner's follow-up asks for hostile outer vegetation so the Hamlet reads as the sheltered, buildable area.
Fifteen native Tangle decor assemblies replace seven near-town Valley groups and extend around the flanks,
rear and low foreground. `forest_tile_01/12` carry gnarled trees, vines, roots and broken palisades;
`forest_tile_04` supplies two low debris banks. Seven distant Valley groups remain. `Wildwood` desaturates
the Tangle's cyan toward dark grey/green. Native black forest backing is hidden, retaining the continuous
owned ground. Root banks use the existing nine-sample burial. The256-square Valley export includes Wilderness,
a continuous outer-ground darkening field that leaves the town boundary and approach readable. This affects
construction paint only. Two Core cases check service/road clearance, all-sided containment and paint continuity.

The first Mountain-only native run passed pointer/camera/rollback/resize but failed keyboard Enter while the
owner was using the live preview. The normal UI deliberately clears keyboard focus on mouse activity; the
fixture previously admitted live mouse events between its synthetic Tab and Enter. Only the active synthetic
3D check now consumes live pointer/key events and records their count. After PASS or failure, ordinary preview
input resumes. No product input rule, desktop focus or OS input is changed. Keep the failed run's log as evidence.

Final round237 Release and all1,137 Core+188 UI pass. The full unsaved native fixture passes services/Escape,
controls, rollback/full release, reload, Embark/cleanup/return, live resize/restore, keyboard and still-camera/
smoke checks. It isolates33 live input events; no exception in the completed acceptance log. Native overview
inspected:153 renderers,168 grass clumps, four chimney systems, native HDR target. Zero repeated URP allocations
over5.036sec. Both DLL/PDB pairs and eight owned source files match; config is unchanged. Pre-deploy backups:
`hamlet-before-mountain-v10/20261013-191604.zip` and `hamlet-before-mountain-config-v10/20261013-191605.zip`.
Owned final QA PID25580 closed, DD2 stopped and no estate accessed. This round retains the presentation flag;
the owner's subsequent request to promote both3D views and remove mode switches belongs to the next round.

Round238 promotes both3D presentations and adds an eastern cliff. Runtime mode fields, both view switches and
the preview's mode overrides are removed. Deprecated config entries are consumed and deleted at startup;
the interrupted native boot verifies that only those two keys disappear, with all other values retained.
The header keeps camera motion and reset in the vacated space. Retry scenery clears a failed stage before
re-requesting it. The returning-estate torch gate requests the real stage and waits for Ready; failures show
retry/cancel, and cancelled loads release the stage through the existing unused-frame lifecycle. New estates
and expedition resumes keep their own entry routes. Automatic painted loading/recovery is retained internally.

The right terrain now drops steeply beyond the workshop pads through an irregular eastern face. Native rock
paint follows actual slope classification; the rejected added Coast shelves are absent. All service foundations
and approaches keep their existing heights. The offline packed preview shows the face without moving the camera.

Release and all 1,138 Core + 188 UI tests pass; 19 layout/landscape cases pass after the cliff cut. All 13 owned
source files and both installed DLL/PDB pairs match. Before the owner stopped launches, the default-view fixture
opened all ten services without any feature override or error. Its run was interrupted before final lifecycle,
resize, keyboard and motion acceptance; no final native cliff screenshot exists. The standard embark map and
read-only torch-prewarm fixtures have not run. They must pass before claiming full native round238 acceptance.

No game launch before 2026-10-13 19:26:19 UTC /21:26:19 Europe/Paris, per the owner's 1h20 pause requested at
18:06:19 UTC. Owned QA PID29076 was closed immediately. No estate accessed or OS input sent. Private evidence
and guarded future launch helper: `local-art/ui-overhaul/hamlet-embark-default-v11/`; `acceptance.json` explicitly
records partial native status. Pre-change backups: `hamlet-before-default-v11/20261013-195821.zip` and
`hamlet-before-default-config-v11/20261013-195822.zip`. The final build was deployed with DD2 stopped during
offline work. Broader controller, saved transactions, bespoke art and long-session/frame-time work remains.
