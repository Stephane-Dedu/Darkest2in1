# Perspective Hamlet

The 3D Hamlet is the standard view. Round239 removes painted/hybrid/DD1 town drawing completely.
Cold entry and retry show an opaque torch page until the actual town has rendered; failure offers Retry
scenery or Main menu. The retired view flags/switches remain removed. Camera motion and Reset view remain.

Quest selection, hero sheets and provisioning retain one existing scene and its render texture, with its
camera and particles parked. Return resumes that scene. An actual departure releases it after three unused
frames. Expedition results prewarm the town behind the results page. A resize while parked requires a new
render before presentation. The cache does not read estate files or start loading on a map-only preview.

Round239 Release + 1,141 Core + 188 UI tests pass, including three lifecycle regressions. The updated unsaved
native fixture checks cache identity, parking/resume and cold torch gating. Native acceptance of these changes
is pending; no game launched during this round. Private evidence: `local-art/ui-overhaul/hamlet-return-cache-v12`.
Round238 default map and estate torch checks also remain pending. Historical painted rollback/selector-cleanup
instructions below are superseded by this section and the latest handoff.

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
