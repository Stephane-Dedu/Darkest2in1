# Perspective Hamlet

`Look.Hamlet3D` is an experimental presentation flag, off by default. The town's **3D town** button enables it;
**Painted town** immediately releases its camera, meshes, materials, render textures and Addressables handles.
The choice persists in the mod config, not the estate. With the flag off, the approved private hillside pack
remains the first choice, followed by the original installed DD1 town. Missing models or a 45-second load
timeout also retain that fallback. Switching off and back on retries a failed load.

`Dd2/HamletStage` owns a perspective camera and a render texture matching the window and native HDR format.
Its fixed 16:9 composition follows the virtual UI canvas through window resizing. It copies visual components from
installed Addressables assets into a distant scene; it never instantiates native road nodes, shops, triggers,
inventory behaviours or animators. Native meshes and textures stay in the owner's install. Skinned geometry
is baked once into owned static meshes. DD2's native shader/material copies retain texture mapping, depth and
cutouts, with road-specific lighting, fog, distance darkening and dithering disabled. Shared native objects
are never edited. Any temporary camera shader globals are restored immediately after rendering.

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
Leaving the town releases the stage after three frames without a draw request; returning loads it again.

For native QA, launch with debug keys enabled and `-dd3-hamlet3d-preview -dd3-hamlet3d-check`.
The fixture uses an unsaved campaign, selects no estate path, suppresses persistence only for that object
and blocks real expedition embark. It injects IMGUI events without desktop input: ten service clicks and
Escape closes; zoom, pan and reset; painted rollback/resource release; reload and another world click;
footer Embark; stage cleanup; Back to Hamlet and another successful scene load. The preview flag overrides
the feature setting only in memory. Close the owned test process and relaunch without QA flags for play.

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
