# Hamlet building models

Round295 Core material review: local-art/ui-overhaul/hamlet-material-review-v50/CONTINUE.md.
ModelReviewExport uses the actual Core glTF reader, palette, tint, contact bands
and world placement.41 graded paint variants; native_model_review.py binds unlit
geometry through town_preview --native-models. Geometry/UV/winding/paint/alpha and
packed-scene audits PASS, render compared with native294. One Guild standard resize
and remaining shader differences are approximate. Tool-only; normal293/camp292
art unchanged, game CLOSED. Fresh usage/baseline next296; no pending restore.

Round294 offline ground review: local-art/ui-overhaul/hamlet-native-paint-v49/CONTINUE.md.
native_paint_capture.py prepares a PRIVATE frozen capture fixture, then converts
the game's three4096px maps with exact decoded pixels/row origin. town_preview.py
--native-ground <capture> uses them without duplicate grade/tint, rejects stale
Core source stamps/altered paint, and keeps reviewed model textures. Core layout
exact; packed Blender render viewed. Normal293/camp292 art restored byte-exact,
game CLOSED, no capture fixture installed. Buildings still render too dark offline;
next295 align their material review with native evidence. Fresh usage first.

Round293 accepted: checkpoint local-art/ui-overhaul/hamlet-paving-light-v48/CONTINUE.md.
Paving retains existing foundation contact shade and doorway glow at full stone
coverage. Earth/valley paint exact; road margins retain ground pixels. No new art,
geometry, material batch or palette change. Two regressions reproduced the old
loss; Release/1287 Core/native lifecycle/fresh overview/buffer PASS. Runtime293,
unchanged camp292 art,254 renderers, game CLOSED. Fresh usage/baseline next294.

Round292 accepted: checkpoint local-art/ui-overhaul/hamlet-camp-v47/CONTINUE.md.
Raw models-scaled/; baked HamletModels-scaled-reviewed/. Native Tangle double-peak
tent replaces21 coarse shelter/rope/bedroll pieces. Original2048 paint/UVs/uniform
proportions retained; eight camp equipment/flame meshes and nine other services exact.
Horizontal service footprint retained, intentional taller5.3m tent. Small6.4m study
rejected; final width8.7m. Native lifecycle/fresh overview/buffer PASS,254 renderers,
runtime291 unchanged, game CLOSED. Fresh usage/baseline next293; shared colours retained.

Round291 accepted: checkpoint local-art/ui-overhaul/hamlet-cliff-form-v46/CONTINUE.md.
Bounded angular cliff relief outside fixed routes/building pads/boundaries, cached
shared grid, unchanged painting/assets/constructed geometry. Release/1285 Core
checks/native lifecycle/fresh overview/buffer PASS. Runtime291 installed, same
v45 medical/model package,258 renderers, game CLOSED. Fresh usage/baseline next292.
Survivalist canvas still uses a small thatch strip; native Tangle tents are in kit.

Round290 accepted: checkpoint local-art/ui-overhaul/hamlet-medical-review-v45/CONTINUE.md.
Raw ../hamlet-medical-sign-v44/models-reviewed; baked v45/HamletModels-reviewed.
Native rope-bound medical cross retains original UV/2048 paint/proportions; nine
other raw/baked services exact. Native lifecycle/fresh overview/buffer PASS,
runtime283 unchanged, game CLOSED. Fresh usage/baseline before next291.
Wider terrain/cliff form/lighting remain; preserve service colour harmony.

Round290 prepared, native QA PENDING: local-art/ui-overhaul/hamlet-medical-sign-v44/CONTINUE.md.
models-reviewed/ is offline only; accepted289 v43 remains installed, runtime283,
game CLOSED. Native hospital rope-bound medical cross replaces two flat primitives.
Four views, retained geometry/paint, native UV/shape/pixel/bounds audits PASS.
Nine other GLBs exact. No new generation. Fresh usage and QA baseline, then package,
contact bake/native lifecycle/fresh overview/probe before installing/accepting.

Round289: Blender moulding_paints.py --source <v42/models-reviewed> --out
<new private> --kit <dd2-native-kit>. Supply unchanged textures/abbey_wall_v40.png.
182 legacy window-surround/verge/sill pieces use varied whole stone-face windows,
oriented along each member with finite end caps. Geometry/factors/native paint and
all other UVs retained; seven other raw/baked services exact. Accepted raw
v43/models-reviewed and baked HamletModels-reviewed. Twelve views, geometry/paint/
UV/contact and native lifecycle/fresh overview/buffer checks PASS. Runtime283
unchanged. Evidence hamlet-moulding-paints-v43. No new bitmap generation.

Round288: Blender trim_paints.py --source <v41/models-reviewed> --out <new private>
--kit <dd2-native-kit>. Supply unchanged reviewed textures/abbey_wall_v40.png.
Eight whole stone faces keep painted edge contrast; interior-only study rejected.
Remap fitted limestone across three services, preserving geometry/factors/native
art and all non-trim UVs. Accepted v42/models-reviewed and HamletModels-reviewed.
Twelve views, paint/geometry/UV/contact and native lifecycle/overview/allocation
reviewed. No new bitmap or runtime change. Evidence hamlet-trim-paints-v42.

Round287: Blender roof_paints.py --source <v40/models-reviewed> --out <new private>
--kit <dd2-native-kit>. Supply reviewed textures/slate_v41.png and clay_v41.png.
Each1254px atlas has twelve4x3 faces, with3.5% inset per cell. Refit existing UV
sample rectangles to larger painted faces; keep all course geometry/underlaps.
Abbey cut sides receive finite mapping/dark slate. Accepted v41/models-reviewed
and baked HamletModels-reviewed. Twelve views, geometry/paint/contact and native
lifecycle/overview/allocation reviewed; runtime283 unchanged. Private prompts and
native references/provenance stay beside the art in hamlet-roof-paints-v41.

Round286: Blender facade_paints.py --source <v39/models> --out <new private output>
--kit <dd2-native-kit>. Supply reviewed textures/<service>_wall_v40.png for Abbey,
Guild and Sanitarium. Accepted raw v40/models-reviewed; baked HamletModels-reviewed.
Three1254px native-reference paintings; all geometry, native textures and trim
retained. Three Sanitarium UV layouts fitted; exposed platform top uses native
paving. Seven other services exact. Twelve views, geometry/paint/contact audits
and native lifecycle/overview/allocation reviewed. Runtime283 unchanged.
Generation prompts, native reference paths, rejected studies and provenance stay
private alongside the paintings in hamlet-facade-paints-v40.

Round285: Blender oak_planks.py --source <v37/models-reviewed> --out <new private>
--kit <dd2-native-kit>. Supply reviewed v21 reproduction textures/wood_v20.png.
Six planks clipped to the existing pointed oak backing use continuous900x80 grain
strips; physical/paint aspect and variable plank length agree. Preserve iron284.
Raw source v39/models; baked package v39/HamletModels-reviewed. Review export views,
close detail, coverage/original surfaces/UVs/bounds, contact bake and native scene.
No new image generation, runtime change or builds. Native acceptance recorded in v39.

Round284 accepted: Blender abbey_iron.py --source <v35/models> --out <new private
folder> --kit <dd2-native-kit>. Prepare textures/iron_v21.png from the reviewed v21
workshop painting. Raw source v37/models-reviewed, accepted baked package
v38/HamletModels-reviewed. Fitted split hinges/studs/ring plates retain original oak,
724 other Abbey meshes/UVs and nine other models. Native full fixture/fresh overview/
allocation PASS; runtime283 unchanged,256 renderers. Evidence/checkpoint v38.


Round283: LayoutExport <private-json-path> --full-cliff-tile exports square UVs for
hamlet-cliff-v17.png. Omit the flag for the native farm_eroded_hill atlas fallback.
The private painting uses full-image Repeat and its original shared grade. Never
apply the native atlas crop to an already seamless painting. Runtime283, raw/baked
service models remain v35. Evidence and native full view are in hamlet-cliff-sampling-v36.

Round282 native Tavern sign: Blender native_sign.py --source <v33/models> --out
<new private folder> --kit <dd2-native-kit>. The native inn_lvl_02_prefab sign replaces
nine primitive sign parts. Keep its original proportions/UVs and wall support.
Package raw GLBs, then bake contact with the round281 recipe. Nine other raw and
baked models remain exact. Accepted v35/models and v35/HamletModels-reviewed; folder
hamlet-cliff-relief-v35 also holds rejected native cliff studies, not installed.

Round281 static contact visibility: first run package.py on accepted raw GLBs. Then run
Blender `bake_contact.py --source <raw package> --out <new private package> --models
<service ids> --rays 16 --radius 1.0`. All ten ids are required for the accepted whole-town
pack. Never bake on top of an already baked package. Original geometry/UV/material/paint
bytes are retained; appended scalar accessors referenced by primitive extras describe
visibility. The runtime uses the existing three facets, with open/partial/deep bands.
The native shader ignores vertex colour, so this metadata is consumed explicitly.
Compare in game; the ordinary raw-GLB Blender preview does not show these shadows.
Evidence v34: HamletModels-study and HamletModels-final are earlier drafts; accepted
is HamletModels-reviewed. Audit prefixes/paint, synthetic probes, native lifecycle and
final overview before accepting. Source topology edits require a fresh bake.

Round280 Guild masonry: run `guild_masonry.py --source <v30/models-final> --out <new private folder>
--kit <local-art/dd2-native-kit> --resolution 900 --samples 16` under background Blender.
Prepare the output textures/abbey_wall.png using reviewed v21/reproduction-materials source;
v33/paint-provenance.json records its hash. Native parts/UVs and nine GLBs retained.
Re-import/four-view, bounds and native acceptance precede installation. Runtime279 shared
palette applies to new limestone materials. Final private source v33/models.

Round278 Tavern supports: run `supports.py --source <v29/models-final> --out <new private folder>
--kit <local-art/dd2-native-kit> --resolution 900 --samples 12` under background Blender.
No additional textures. Nine exact GLBs, native components/UVs preserved. Final v30 source
is models-final; models-study lacks the chain crossbar. Export/re-import, four views and
native acceptance required. Do not reuse old deployment baselines for a new round.

Round277 native lamps: run `lanterns.py --source <v28/models-final> --out <new private folder>
--kit <local-art/dd2-native-kit> --resolution 900 --samples 16` under background Blender.
It selects `common_prop_lamp_01/02` from `common_story_assist_prefab`, records extraction
hashes and changes four services. No new texture files needed. The native helper is
loaded freshly per service to avoid stale Blender material references. Accepted source:
v29/models-final. Independent lamp geometry/UV audit, four views and native checks precede
acceptance. The initial models-study and models folders remain superseded drafts.

Round276 Guild/Sanitarium roofs: run `roof_joinery.py --source <v27/models-accepted>
--out <new private folder> --kit <local-art/dd2-native-kit> --resolution 900 --samples 16`
under background Blender. Copy reviewed `red_v20.png`, `slate_v20.png`, `wood_v20.png`
and `iron_v21.png` to the output's `textures` first. Accepted source is v28/models-final.
Four exported/re-imported views, roof-coverage audit and actual native checks precede
acceptance. Eight exact GLBs and native component geometry/UVs retained. See v28/CONTINUE.md.

Round275 Smithy roof finish: `roof_finish.py --source <v26/models-final> --out <new private folder>
--kit <local-art/dd2-native-kit> --resolution 900 --samples 16` under background Blender.
No extra textures needed; it reuses the packed reviewed painting. Accepted v27 library:
`models-accepted`. Re-import/four-view and native checks are required before installation.
Private `edge-audit.json` verifies preserved upper/lower paint/course geometry and reduction
of collapsed side UVs. Nine other service GLBs remain byte-identical. See v27/CONTINUE.md.

Round274 roof courses: evidence in `local-art/ui-overhaul/hamlet-roofs-v26`.
Run Blender `--background --python roof_detail.py -- --source <v25/models>
--out <new private folder> --kit <local-art/dd2-native-kit> --resolution 900 --samples 16`.
Prepare the output's `textures/` with the unchanged reviewed `slate_v20.png` and
`thatch_v22.png`; exact sources/hashes are in v26/texture-sources.json.
Final library is `models-final`; earlier iterations are reviews, not installation sources.
Re-import/topology/packed paint/native-part checks run in the shared pipeline, then
four views and native Hamlet acceptance. Run package.py on the verified final library.
The recipe fits the original exported roof thickness and clips reed pieces before
combining them. Do not boolean a self-intersecting bundle mesh: it can lose coverage.
Private coverage-audit.json compares a rejected negative control to the final roof.

Round273 Abbey/Sanitarium masonry: evidence in `local-art/ui-overhaul/hamlet-masonry-v25`.
Prepare a new private output `textures/` directory containing the reviewed v20
`abbey_wall.png`, `sanitarium_wall.png`, `wood_v20.png` and native `window_grain.png`.
The exact inputs/hashes are recorded in v25/texture-sources.json. Run Blender with
`--background --python masonry_detail.py -- --source <v24/models-final> --out <new folder>
--kit <local-art/dd2-native-kit> --resolution 900 --samples 16`, then package.py.
Final v25 source is `models`; `models-first` is the earlier review without rear windows.
The authoring pass preserves all native meshes/UVs and eight other GLBs, and keeps
both modified buildings' outer bounds. Re-import and inspect all four views, then
the native Hamlet. No new image generation or runtime binary is needed for this pass.

Round272 terrace recipe and evidence: `local-art/ui-overhaul/hamlet-terraces-v24/`.
Run `prepare_terraces.py --game <DD2 install> --out <new private model folder>
--earth <reviewed hamlet-earth-v23.png>` with the UM Python environment. It extracts
native cobbles and foundation_panel_06, crops the intact courses, and records provenance.
The supplied earth is the existing generated painting, not native extraction.
Then run Blender with `--background --python terraces.py -- --source <v23/models-reviewed>
--out <same new folder> --kit <local-art/dd2-native-kit> --resolution 900 --samples 12`.
Package with `package.py --source <new folder> --out <private HamletModels folder>`.
Six services change authored UVs/materials only; four GLBs remain exact. The recorded
v24 final library is `models-final` (a graveyard-only refinement follows `models`).
Native components retain geometry and UVs. Validate the complete town before installation.

Round271 ground/nurse pass: `local-art/ui-overhaul/hamlet-ground-v23/CONTINUE.md`.
Run `prepare_ground.py --game <DD2 install> --out <private folder>` through the UM Python
environment to extract unmodified cobblestones. The optional runtime files are
`PrivateScenery/HamletMaterials/hamlet-cobbles-v23.png` and `hamlet-earth-v23.png`.
Earth was generated with the built-in image tool; exact prompt/original stay in v23.
Run `remove_nurse.py` through Blender with v22/models as `--source`, a new private `--out`,
and `--kit ../local-art/dd2-native-kit`, then `package.py`. Copy reviewed `abbey_wall.png` and the native v23 cobble tile as `cobbles_native.png`
into the output textures folder; retain source provenance beside the new models.
The stone memorial remains interactive; eight other GLBs remain exact. The older town_preview
ground recipe is approximate and predates this pass; final evidence must be native.

Round270 rebuilds the smithy around one continuous thatched roof. Evidence, paintings,
provenance and deployment status: `local-art/ui-overhaul/hamlet-smithy-v22/CONTINUE.md`.
The native cottage is replaced explicitly; native barrel/crate geometry and UVs remain.
The other nine source GLBs are preserved exactly. Reproduce into a new private folder:

```powershell
python tools/hamlet_models/prepare_workshops.py --study smithy --source ../local-art/ui-overhaul/hamlet-workshops-v21/models --paintings ../local-art/ui-overhaul/hamlet-smithy-v22/paintings --out ../local-art/ui-overhaul/hamlet-next/models
& 'C:/Program Files/Blender Foundation/Blender 5.2/blender.exe' -b --threads 4 --python-exit-code 1 --python tools/hamlet_models/smithy.py -- --source ../local-art/ui-overhaul/hamlet-workshops-v21/models --out ../local-art/ui-overhaul/hamlet-next/models --kit ../local-art/dd2-native-kit --resolution 1100 --samples 16
python tools/hamlet_models/package.py --source ../local-art/ui-overhaul/hamlet-next/models --out ../local-art/ui-overhaul/hamlet-next/HamletModels
```

Three reviewed generated paintings supply reed thatch, soot-stained lime and a static
furnace recess. They reference DD2's cottage/Academic materials and DD1's smithy silhouette.
The exported GLB is re-imported and rendered from four angles before native review.
The existing `workshops.py` command retains its v21 defaults.

Round269 workshop package and evidence: `local-art/ui-overhaul/hamlet-workshops-v21`.
Use `prepare_workshops.py` with `--source` v20/models, `--paintings` v21/paintings and
a new private `--out`. Then run `workshops.py` through Blender with the same source,
output and `--kit ../local-art/dd2-native-kit`, followed by the ordinary `package.py`.
The preparer copies the three reviewed paintings without resampling. The Blender pass
refines three models, preserves seven GLBs exactly, checks native geometry/UV retention,
re-imports exports and renders four views each. Consult CONTINUE.md before deployment.

Earlier package notes follow.

Round268's installed material package is `local-art/ui-overhaul/hamlet-material-v20/HamletModels`:
ten models,61 hashed files,117.5MB. Three services have independent wall/roof/wood finishes;
Guild/forge/wagon emblems are repainted and DD1 glass is replaced. The Abbey rose is
physical tracery. Five other GLBs stay unchanged. `design_system.md` records the review gate.

Reproduce in a new private output (the material preparer rejects existing directories):

```powershell
python tools/hamlet_models/prepare_architecture.py --source ../local-art/ui-overhaul/hamlet-material-v19/models --paintings ../local-art/ui-overhaul/hamlet-material-v20/paintings --kit ../local-art/dd2-native-kit --out ../local-art/ui-overhaul/hamlet-next/models
& 'C:/Program Files/Blender Foundation/Blender 5.2/blender.exe' -b --threads 4 --python-exit-code 1 --python tools/hamlet_models/architecture.py -- --source ../local-art/ui-overhaul/hamlet-material-v19/models --out ../local-art/ui-overhaul/hamlet-next/models --kit ../local-art/dd2-native-kit --resolution 900 --samples 12
python tools/hamlet_models/package.py --source ../local-art/ui-overhaul/hamlet-next/models --out ../local-art/ui-overhaul/hamlet-next/HamletModels
```

The exporter re-imports each GLB, checks topology/bounds/embedded images and renders four
views. Native full-town review is still required. The v17 masonry repaint experiment is
superseded; its tool remains for reproducibility. Sky/cliff private files from v17 remain.
Native ten-service/lifecycle checks passed, and the final1920x1080 town view was inspected.
Zero repeated render-buffer allocations over5.028sec. The normal plugin is unchanged.
Native evidence, source hashes, baseline and deployment status are in the v20 folder.
See [the Hamlet guide](../hamlet_3d.md) for checks and remaining quality limitations.

Round266 installs the approved private finish package and reframes the scene from DD1's town cinematic:
larger services, lower camera, darker supporting cliffs, native Valley roof infill and a small Sprawl
estate on the rear ridge. The oversized Mountain is replaced by a cloudy native DD1 sky. Final native
capture/build/source hashes: `local-art/ui-overhaul/hamlet-cinematic-v16`. See [the Hamlet guide](../hamlet_3d.md)
for camera values, lifecycle/interaction checks and remaining art limits. No new generated art.

The approved finish pass preserves the original dark native cliffs and improves road margins and four
building exteriors. `finish.py` imports the verifiedv02 GLBs and adds fitted Abbey
coping/ridge/buttress caps, Sanitarium drip/hip courses, Guild doorway trim and Tavern sign/lantern details.
It checks the architectural envelopes, re-imported topology and embedded paint; six GLBs remain unchanged.
All models, extracted paint and packed Blender documents stay outside Git.

To reproduce the approved fitted models, use a new private output folder and the existing verifiedv02
library. Run `finish.py` through Blender, then package its output with the ordinary packager:

```powershell
& 'C:\Program Files\Blender Foundation\Blender 5.2\blender.exe' --background --factory-startup --threads 8 --python-exit-code 1 --python tools/hamlet_models/finish.py -- --source 'C:/Users/Piral/DarkestDungeon3/local-art/ui-overhaul/hamlet-buildings-v02' --out 'C:/Users/Piral/DarkestDungeon3/local-art/ui-overhaul/hamlet-buildings-finished' --kit 'C:/Users/Piral/DarkestDungeon3/local-art/dd2-native-kit'
& 'C:/Users/Piral/DarkestDungeon3/universal-modder/.venv/Scripts/python.exe' tools/hamlet_models/package.py --source 'C:/Users/Piral/DarkestDungeon3/local-art/ui-overhaul/hamlet-buildings-finished' --out 'C:/Users/Piral/DarkestDungeon3/local-art/ui-overhaul/hamlet-buildings-finished/HamletModels'
```

The approved package is already prepared at `local-art/ui-overhaul/hamlet-surface-v15/HamletModels`: ten
models, 54 hashed files, 59.7 MB. Its original native capture is `native-dark-rocks.png`. Round266 finally
installs it with a verified isolated plugin build; the earlier review had restored the older package.
Capture fresh installed hashes before deployment and deploy only while DD2 is stopped. The following
sections describe earlier model studies; their rollback/flag directions are superseded by the Hamlet guide.

Ten separate 3D building studies, based on DD1's fully repaired `town_<building>_level03` exteriors.
The graveyard uses `town_graveyard`. These are private asset authoring outputs, not replacements for the
runtime models in round215. The existing `Look.Hamlet3D` flag and painted fallback are unchanged.

The owner's installed DD1 artwork is the architectural reference. Its private reference kit preserves
the Spine setup pose, original atlas regions and all three upgrade states. Fronts, roof silhouettes,
entrances and service emblems follow those references. DD1's unseen sides and backs are inferred.
The DD2 kit supplies actual painted meshes, roof/stone/timber surfaces and props. Geometry fills the
missing architecture. The buildings have depth, pitched/curved roofs and distinct sides and backs;
there is no full-building picture mounted on a camera-facing plane.

The first pass establishes these service shapes and source parts:

| Location | DD1 reference | DD2 parts and authored structure |
|---|---|---|
| Abbey | Raised church, steep slate nave, rear bell tower, pointed windows and front stair | Sprawl `city_tower_02`; new nave, porch, buttresses, rose tracery, lancets and steps |
| Tavern | Unequal timbered wings, red roofs, tall chimney, warm windows and entrance sign | `inn_lvl_03_prefab`; chimney, threshold, barrels, crates, lanterns and hanging sign |
| Sanitarium | Stone lower floor, timber upper floor, round stair turret, pointed roof and green glass | New main hall, round tower, timber frame, entrance/stairs; DD2 hospital nurse |
| Guild | Tall stone hall, red roof, two sword standards, armoured door and lower side wing | New hall, wing, crest ironwork, crossed swords and shield; DD1 standards, DD2 camp axe |
| Blacksmith | Low thatched workshop, tall/short chimneys, open hearth, anvil and sign | `farm_cottage`, new open forge bay, chimneys, hearth, anvil, wheel and plank pile; native crates/barrel |
| Survivalist | Brown ridge tent, open flaps, storage and campfire | New full-depth tent, ropes and bedroll; DD2 chest, barrel, crate, axe and firepit |
| Stage Coach | Enclosed passenger carriage, wheels, lanterns and travel trunks | Native embark coach with its actual wheels/lanterns; reception sign and waiting storage |
| Nomad Wagon | Plum barrel-roof caravan, eye crest, front door/steps, wheels and side shop awning | New caravan, curved roof, axles/spokes, wood steps, supported awning/tassels; DD1 eye crest and native stock chests |
| Graveyard | Raised burial terrace, retaining wall and clustered headstones | Native grave groups with added stone thickness; new masonry terrace, stair, gateposts and spear fence |
| Ancestor statue | Bare head, long coat/cape, crossed hands on a staff and stepped plinth | New faceted figure, sleeves/hands, face, staff and base; DD1 carved-stone weathering and native candles |

These are first models of the repaired exteriors. Lower upgrade/locked variants, NPC animation, collision,
LODs and native runtime materials/placement remain subsequent work. Generic doors, stone repetition and
some small sculpted details need further art fitting. The renders establish geometry and service identity;
they do not establish in-game acceptance.

The second pass (`v02`) keeps the first gallery for comparison and adds native DD2 painted meshwork:
the Abbey's carved shrine entrance, the Academic house for the Sanitarium, Sprawl gateway masonry and
individual inn roof/wing components for the Guild, and Hoarder cart joinery for the Nomad Wagon.
Fitted parts retain their original UV coordinates and atlas textures. Broad native cloth panels replace
the tent/wagon's thatch-derived fabric; wider painted timber panels replace the tiny wood sample.
Neutral masonry, corner courses, stitching, ropes, forge tools and small service emblems add detail.
The Ancestor gains carved facial features, hands and coat seams with gray stone rather than brick-patterned
clothing. Its sculpt remains coarse compared with DD2's finished character art. Repeating masonry/roof
samples and some inferred architecture still need individual painting and fitting.

This is closer to DD2's painted direction, not a claim of identical production quality. The offline material
conversion preserves native paint brightness and alpha cutouts; it does not reproduce DD2's native lighting,
fog, outline shaders or animation. Both private galleries remain available. Round219 installs v02 through the 3D town flag.

`package.py` converts the verified GLBs to the runtime's external JSON glTF/buffers/PNG format. It preserves
node transforms, UVs, painted factors and alpha thresholds, deduplicates PNGs and carries base alpha into RGB
emission paint maps when needed. It requires all ten verified model hashes and refuses output inside Git.
`GltfModel` now reads base/emission factors and emissive strength. `HamletStage.Models` applies them to owned
DD2 environment materials, restores the authored front, sizes each exterior to its terrace and batches by
material while retaining separate picking bounds. All resources release with `Look.Hamlet3D` rollback.
Native check scope and remaining acceptance are in [the Hamlet guide](../hamlet_3d.md).

To package the existing v02 output, run from the repository:

```powershell
& 'C:\Users\Piral\DarkestDungeon3\universal-modder\.venv\Scripts\python.exe' tools/hamlet_models/package.py --source 'C:\Users\Piral\DarkestDungeon3\local-art\ui-overhaul\hamlet-buildings-v02' --out 'C:\Users\Piral\DarkestDungeon3\local-art\ui-overhaul\hamlet-ingame-v02\HamletModels'
```

Copy the resulting `HamletModels` folder into the configured private scenery folder only while DD2 is
stopped, after backing up any previous package. Verify all `manifest.json` file hashes after copying.
Current package: ten models, 54 hashed files, 59.5 MB, plus its manifest. It is installed privately in the
owner's active `game/PrivateScenery/HamletModels`. Source feature default stays off; local config is on.

## Rebuild privately

Requires the existing DD2 native kit, DD1 reference kit, Blender 5.2 with NumPy, and Python with Pillow.
Use the supplied local paths or equivalent private paths on another owner's machine.

```powershell
$hamletPython = 'C:\Users\Piral\DarkestDungeon3\universal-modder\.venv\Scripts\python.exe'
$hamletBlender = 'C:\Program Files\Blender Foundation\Blender 5.2\blender.exe'
$hamletKit = 'C:\Users\Piral\DarkestDungeon3\local-art\dd2-native-kit'
$hamletDd1 = 'C:\Users\Piral\DarkestDungeon3\local-art\dd1-reference-kit'
$hamletOutput = 'C:\Users\Piral\DarkestDungeon3\local-art\ui-overhaul\hamlet-buildings-v02'
& $hamletPython tools/hamlet_models/prepare.py --kit $hamletKit --dd1 $hamletDd1 --out $hamletOutput
& $hamletBlender --background --factory-startup --threads 8 --python-exit-code 1 --python tools/hamlet_models/render.py -- --kit $hamletKit --out $hamletOutput --detail-pass v02
& $hamletBlender --background --factory-startup --threads 8 --python-exit-code 1 --python tools/hamlet_models/verify.py -- --kit $hamletKit --out $hamletOutput
& $hamletPython tools/hamlet_models/gallery.py --kit $hamletKit --out $hamletOutput --previous 'C:\Users\Piral\DarkestDungeon3\local-art\ui-overhaul\hamlet-buildings-v01'
```

`render.py --only abbey,guild` rebuilds selected buildings. Every model gets four 1024px camera renders,
a textured GLB and a packed, editable Blender document. Source inputs use Unity X/right, Y/up, Z/back axes.
The GLB exporter uses glTF's Y-up convention; re-import into Blender restores the authoring coordinates.
The presentation floor/lights/camera are excluded from the GLB. Inspect the packed `.blend` to edit parts.
For the original architecture, select `--detail-pass v01` and a separate output directory. Quick previews
can use `--views hero --resolution 512 --samples 8`; restore all four views before verification/gallery creation.

`verify.py` re-imports every GLB, compares topology and bounds, requires finite geometry with real depth,
checks embedded texture data, compares painted base/emission factors and native alpha thresholds with the packed authoring materials, and renders
the exported file itself. Current Blender Mix nodes preserve the glass/cloth tints that legacy MixRGB nodes lose
in Blender 5's glTF exporter. Native emission is converted to explicit Principled emission and glTF's
`KHR_materials_emissive_strength`, retaining painted brightness above one. `verified.json` records the results and SHA256 per model. The gallery rejects a
GLB changed after verification. Its opening pictures and model contact sheet use these re-imported renders.
Other gallery buttons show the authoring scene from the front, three-quarter, rear and side cameras.

Private outputs include `gallery.html`, `hamlet-building-models-v02.png`, `dd1-to-dd2-comparison-v02.png`,
`models.json`, `texture-sources.json`, `dd1-sources.json` and each building's model/render directory.
With `--previous`, the gallery also copies the previous exported renders for its First pass buttons and
creates `before-after-v02.png`. This comparison needs no browser server or network connection.
No exported model, game texture, reference image or packed Blender file belongs in Git or a release.
The scripts never read saves, launch DD2 or install the package themselves. The independent corridor and
Shieldbreaker pipelines are untouched.

Additional public visual reference: [Red Hook's DD1 media gallery](https://www.darkestdungeon.com/darkest-dungeon/media/).
Building-specific shape decisions above come from the installed art, not from third-party reconstructions.

## Preview the compact DD1 landscape without launching a game

Round220's town layout and terrain live in Core. `LayoutExport` exports their actual geometry, including
the paths, stairs and masonry used by Unity. `prepare_landscape.py` reads six surface textures from the
owner's installed common/Valley/Foetor bundles. `town_preview.py` checks all ten v02 GLB hashes and places them
at those same sites, with the same perspective camera. This is an offline composition check: Blender
does not reproduce DD2's shaders, lighting, picking or UI. Its surrounding scenery table mirrors the
stage; update it when changing the stage's props. Native sky/trees use the existing private kit exports.

From the repository, using the path variables above:

```powershell
$hamletLandscape = 'C:\Users\Piral\DarkestDungeon3\local-art\ui-overhaul\hamlet-landscape-v04'
& $hamletPython tools/hamlet_models/prepare_landscape.py --game 'C:\Users\Piral\darkestwithdlc\game' --out $hamletLandscape
& $hamletPython "$hamletKit/tools/export_prefab.py" --game 'C:\Users\Piral\darkestwithdlc\game' "$hamletLandscape/exp" 'biome_common_assets_all.bundle,biome_common_models_platform_high_assets_all.bundle,biome_valley_common_assets_all.bundle' 'barricade_military_decor_rocks_01.fbx' 'barricade_military_decor_rocks_02.fbx' 'barricade_military_decor_rocks_03.fbx'
dotnet run --project tools/hamlet_models/LayoutExport -c Release -- "$hamletLandscape/layout.json"
& $hamletBlender --background --factory-startup --threads 8 --python-exit-code 1 --python tools/hamlet_models/town_preview.py -- --models $hamletOutput --kit $hamletKit --layout "$hamletLandscape/layout.json" --out $hamletLandscape --blend
```

Outputs: `hamlet-landscape-v04.png`, packed `hamlet-landscape-v04.blend`, `layout.json`, projected service
bounds in `composition.json`, and private native surface provenance in `ground-source.json`. Terrain
geometry, game textures and packed documents all stay outside Git. The guide at `tools/hamlet_3d.md`
records the runtime deployment and which acceptance checks remain.

Round221 exports `HamletPalette` alongside geometry so surface tints, brightness, saturation and atlas
windows agree with the runtime settings. Soil samples native painted earth; cliffs use a larger rock-only
strip. The Mountain has independent height/depth scales and a closer/larger position. `--name` selects
the output PNG/Blender basename. Native shader math and terrain vertex shading remain approximate in Blender.

Round223 adds winding Survivalist stairs, one shared civic ascent, low forecourt walls, four Valley leaf
banks and two native outer cliff ledges. `LayoutExport` also writes the crop-corrected terrain UVs and updated
palette. For a new private output directory, run the surface/rock exports above, then export
`coast_skirt_cliffs_mdl_01.fbx` with `biome_coast_assets_all.bundle,biome_coast_models_platform_high_assets_all.bundle,biome_common_assets_all.bundle`
to that directory's `exp/`. Use `--name hamlet-immersion-v05` for the current preview basename.
The runtime bakes lifted paint once and uses three cliff tint bands; the Blender material approximates
those colours. Four runtime chimney plumes are not part of the offline render or v02 model package.

Round236 keeps the original ten v02 models and improves runtime texture fidelity instead of regenerating them.
The six landscape inputs are `road_rough_patch`, `farm_ground_hill_transition`, `farm_eroded_hill`,
`foundation_panel_06`, `grass_dry` and `farm_gate_node_assets_grass`. The eroded-rock atlas replaces the former
brick cliff texture; its rock-only window/aspect and the shared palette are exported with the actual Core UVs.
`LayoutExport` also writes256-square `ContactShade` and `EdgeWear` fields, alongside grass and stain fields,
so the offline ground follows the runtime's bank/footprint contact and worn road margins. Distant woodland
grading and valley haze follow Core. Use `--name hamlet-fidelity-v09` and a private output directory to create
the current preview and packed Blender document. Source images and `ground-source.json` stay outside Git.

Native rendering now preserves building atlas proportions up to2048px, correct native cutouts and transparent
backing materials. The terrain uses the installed URP Unlit shader, and only the Hamlet camera uses the native
featureless renderer to avoid the main-menu LUT. Blender does not reproduce that renderer; inspect native
captures before accepting colour or alpha changes. See `tools/hamlet_3d.md` for lifecycle and acceptance limits.

Round237 enlarges the native Mountain to a wider, buried backdrop. The runtime and LayoutExport share width500,
pivot(-12,-78,112), heightScale.52 and depthScale.15; building sites and camera are unchanged. Fifteen Tangle
assemblies from `forest_tile_01/04/12` enclose the town with gnarled trees, roots, debris and broken palisades.
The exported `Wildwood` palette and Valley `Wilderness` field match the runtime grade/outer-ground darkening;
the preview drops `forest_ground_dark` backing faces as the runtime does. All three exports are already in
the private kit. Use `--name hamlet-mountain-v10` for the private PNG and packed scene. Six Mountain placement
studies in that output folder precede the combined Mountain/boundary view.

Round238 cuts a steeper eastern face beyond the workshop foundations in Core's actual terrain. Its native
rock surface is classified by slope; prefab shelf experiments were rejected. Service pads and walking routes
retain their existing heights, with a Core case checking the exposed eastern rock. Use `--name hamlet-cliff-v11`
for the current private preview/packed scene. This round also promotes Hamlet/embark3D to their standard views;
see `tools/hamlet_3d.md` and `tools/expedition_map3d.md`. The final native checks await the owner's launch pause.
The native unsaved fixture now checks live resize/restore as well as all service/control/rollback routes.
`measure_native.ps1 -HamletProcessId <owned-preview-pid> -Seconds 5` checks repeated URP allocations;
the measured town must already be rendered. Current native evidence and packed preview are in
`local-art/ui-overhaul/hamlet-immersion-v05/`. See `tools/hamlet_3d.md` for the HDR target fix and limits.

Round225's current preview uses `local-art/ui-overhaul/hamlet-landscape-v06/`. Run `prepare_landscape.py`
again for four native surfaces: `road_rough_patch`, `farm_ground_hill_transition`, `bricks` and
`foundation_panel_06`. They replace the earlier paving/cliff atlas choices. `HamletPalette` retains dark
joints rather than lifting the entire surface to grey. Roads and cliff copies are 1024px; retaining stone
and soil use512px. A1024px owned world map combines earth, gravel and the Core stain field at construction.
`LayoutExport` supplies a128x128 sample field for the offline approximation. No texture is generated per frame.

`HamletLandscape` supplies the surrounding valley mesh and ten background placements to both renderers.
Its first ring matches the playable terrain boundary; seven native Valley tree groups and three low Camp
treelines dress the outer slopes. The original Camp silhouette paint/alpha avoids pale rectangular backdrops.
The surrounding scenery and native sky are already in the kit. `--name hamlet-landscape-v06 --blend` writes
the latest composition and packed document. Offline lighting/material conversion remains approximate.

Building preview emission is no longer added into all native surface tints. Runtime paint is normalized
and slightly desaturated; actual lantern/glass/forge materials keep their warm light. The original verified
model package is unchanged. Paths use one world projection across branches and the square. Gentle slopes
remain paved ramps; real stair flights have separate stone treads and level landings. Their top/side UVs
use actual face dimensions, and cobbles no longer intersect the stair blocks. Cliff UVs use one diagonal
world projection rather than switching atlas direction at every changing triangle normal.

Round226's grass preview is `local-art/ui-overhaul/hamlet-grass-v07/`. `prepare_landscape.py` now exports six
surfaces, adding native512px `grass_dry` and4096px `farm_gate_node_assets_grass` from the common/Foetor bundles.
LayoutExport writes the shared Core grass mask,170 placements and batched silhouette mesh. The2048px world
paint combines earth/gravel with the turf only outside paths/building aprons. Runtime binds the original
4096px tuft atlas directly; offline renders use the same reviewed UV crops and olive palette. Source pixels
and all PNG/Blender output stay outside Git. The native shaders remain the appearance oracle.

For a new output folder, also copy the existing private scenery `exp/` cache from v06, or repeat the native
rock/Coast exports documented above. Pass absolute paths, or let town_preview resolve its kit/model roots,
so Blender's glTF importer cannot change the meaning of subsequent texture paths. Use
`--name hamlet-grass-v07 --blend`. The packed preview contains all scenery and has no missing exports.

Round232's current preview is `local-art/ui-overhaul/hamlet-overlook-v08/`. `LayoutExport` exports the22-degree
pitch/36-degree vertical-FOV camera, absolute Mountain placement, surrounding valley bounds and256x256 samples
of stain/grass/approach-road/haze. `town_preview.py` bakes those native surface sources into an owned2048px valley
image. All fourteen woodland placements use existing Valley kit exports; their root banks are grounded across
their footprints. The main road reaches the boundary and continues across the valley without a separate plate.
Use the same six surface exports and existing `exp/` cache, then export a new `layout.json` and pass
`--name hamlet-overlook-v08 --blend`. The final packed scene contains all scenery. Native color conversion still
differs from Blender; use the native overview/service captures and checks recorded in `tools/hamlet_3d.md` for
runtime acceptance. Preview scripts do not install files, launch the game or read estates.


Native material review recipe, from a frozen HEAD snapshot with owned tools copied:
1. `dotnet run --project tools/hamlet_models/ModelReviewExport -c Release -- <private baked package> <new private review>`
2. `python tools/hamlet_models/native_model_review.py <review>`
3. `dotnet run --project tools/hamlet_models/ModelReviewExport -c Release -- --grade <review>`
4. `python tools/hamlet_models/native_model_review.py <review> --finish`
5. Blender town_preview.py with matching `--layout`, `--native-ground`,
   `--native-models <review>`, `--resources <existing native landscape cache>` and
   a fresh `--out`. Keep raw `--models` for source-hash verification.

This is private art-review output. No game deployment is needed. Core validates
native2048px paint extents; the one oversized Guild standard currently uses a
recorded PIL bilinear approximation of GPU sampling. Other source pixels retain
their dimensions. Shader/camera comparison in DD2 remains the final acceptance.

Guild frontage authoring uses `guild_frontage.py` with `--source <verified raw
library> --out <new private library> --kit <kit> --native-root <private exports>`.
Export the original `forest_weapon_sword_01.fbx`, `forest_weapon_sword_02.fbx`
and `barricade_military_decor_shield_01.fbx` with the kit's exporter, loading the
Forest/Common assets and high-model bundles together. Keep that export outside
Git. The script replaces the tiny camp axe with four native props on an oak
stand, retains original prop proportions/UVs/paint and the Guild's existing
horizontal bounds, and copies nine other verified GLBs exactly. Native battlefield
skulls, cyan scraps and weapon-rack geometry are excluded. Package raw output,
bake contact once and review all sides/full town/native controls before accepting.

`shadow_field.py` is an offline baker. Run it with Blender and
`--layout <matching LayoutExport JSON> --review <ModelReviewExport review.json>
--out <new private directory>`. `--size 1024 --direction .60 .45 .55 --disc 3
--samples 4 --floor .6` bakes opaque service/terrain occlusion into a grayscale
data PNG/NPZ, `ground-shadow.gray`, `scene-reference.bin` and `manifest.json`.
The direction uses Unity Y-up; PNG top row is MaxZ, raw row zero is MinZ. Core,
reader and source-package guards reject stale inputs. `--self-test` checks an
actual procedural occluder and ray orientation. Generated fields remain private;
the baker does not deploy or launch the game. Its export metadata records that
no native acceptance has happened at generation time.

Round298 accepts the optional runtime package under `PrivateScenery/HamletShadows`:
copy only the manifest, raw gray bytes and scene reference after checking the
matching native model/layout inputs. Core verifies source GLB hashes, complete
terrain/site arrays and topology, bounds, raw length/SHA and reference SHA. Mono
may differ from the offline CLR by up to0.1mm per numeric coordinate; topology
must match exactly. The accepted native maximum was0.032mm. Missing/stale/damaged
optional fields fall back to the existing paint rather than blocking the torch.

The worker shades daylight in the existing ground/paving images before adding
doorway light. The field identity participates in the paint cache. White/missing
fields preserve existing bytes; source textures and valley paint stay unchanged.
No additional GPU texture, material batch or per-frame ray is introduced. Alpha
scenery, trees, background cottages and building façade cast lighting are excluded.
Rebake after service geometry/layout changes; do not judge those omissions as
complete DD2 shadow parity. Native evidence/checkpoint: hamlet-native-shadows-v53.

`abbey_pinnacles.py --source <verified raw library> --out <new private library>
--kit <native kit>` restores the four front Abbey pinnacles seen in DD1. It takes
complete disconnected corner columns from native `city_tower_02`, retaining their
UVs,1024px paint and uniform proportions. The rear belfry's existing materials are
reused; no new decoded paint/material is added. Columns rise through the eave
above the existing buttresses without changing service bounds/normalization.
Package the new Abbey, bake its contact scalars and rebake the guarded ground
field against the new complete package. Preserve nine other baked services exactly.
Private recipe and evidence: hamlet-abbey-roof-v54; `art/` uses linked D: storage.

`sanitarium_plinth.py` accepts the same source/out/kit arguments. It fits three
staggered courses of native Academic-house rubble around the existing tower.
Retained painted faces keep their original2048px atlas, UVs and uniform proportions.
Black backing faces are excluded because they were hidden in the source ground;
the original solid tower closes the relief. Reuse the existing house material.
Check all sides, original bounds, retained geometry/paint and matching shadow data
before native acceptance. Private final inputs/output: hamlet-sanitarium-plinth-v55/art/final.


`plaster_finish.py --source <verified raw library> --out <new private library>
--kit <native kit> --paint <reviewed2:1 wall painting>` fits the Sanitarium's four
authored plaster surfaces. Keep broad painted facets and thin cracks legible at
the town camera. Do not enlarge tiny native window-side samples over the turret;
the rejected study produced thick black ribbons. A new continuous painting was
compared against the native Academic house, then reimported in four model views
and verified in the native town. All original geometry/native paint/material
factors and other nine services stay intact. The complete1774x887 generated image,
prompt, provenance and native evidence are private in hamlet-native-plaster-v56.
Package/contact/guarded-shadow rebake follow the existing pipeline. Keep older
native-atlas/grain studies out of the game; accepted raw is art/models-accepted/.


`caravan_canvas.py --source <verified raw library> --out <new private library>
--kit <native kit>` fits the native Assistance wagon's upper cloth shell and four
oak hoops to the Nomad caravan. Original atlas, selected UVs and uniform scale
stay intact. The hanging side curtains are omitted; the shop body/windows stay.
Explicit plum/oak factors retain the DD1 colour cue under shared material grading.
The old awning reuses a finite native cloth rectangle; fitted end caps and ten
short supports use native oak paint. Check rear views for a floating eye sign,
and bury brace ends within existing hoop thickness to preserve the exact bounds.
Package only the revised Nomad, bake contact and refresh the matching shadow field.
Other nine raw/baked services stay exact. Private final source/evidence is
hamlet-caravan-canopy-v57/art/final; earlier root studies were never installed.


`caravan_joinery.py --source <verified raw library> --out <new private library>
--kit <native kit>` replaces the old wood painting on109 authored Nomad members.
It reuses the302 native Assistance atlas/oak material with finite aspect-fitted
samples along each member's grain; torus rims unwrap once. Seven original plank/
beam samples vary long boards. No raster edits, added geometry or material.
All175 meshes, the canopy and native parts stay intact. Inspect all four views,
audit finite UVs and preserve exact bounds/normalization. Package only the Nomad,
rebake contact and refresh the matching shadow field. Private source/evidence:
hamlet-caravan-joinery-v58/art/models-reviewed. The first study is superseded.


`nomad_crest.py --source <verified raw library> --out <new private library>
--kit <native kit>` replaces the Nomad's DD1 eye cloth with DD2's native Nomad
Wagon crest. Use its complete427x464 sprite, original pixels/alpha/aspect and
a rigid hanging quad. Keep the existing crossbar/posts and other174 meshes.
The source is private sprites2d/inn_crests/inn_crest_nomadwagon.png. Review all
four views and in-game placement, retain exact bounds, bake contact and refresh
the guarded shadow field. Evidence hamlet-nomad-sign-v59/art/models-reviewed/.


`abbey_native_oak.py --source <verified raw library> --out <new private library>
--kit <native kit>` maps original DD2 Assistance2048 paint to the15 retained
portal/eave wood members. It imports caravan_joinery's finite sampler and uses
the Nomad oak factor. Quieter boards suit fitted doors; continuous sill paint
suits eaves. Retain all759 geometries and other744 UVs/materials. Audit finite
UVs and all four views before native checking. Contact/field must match. Evidence
hamlet-abbey-portal-v60/art/models-reviewed; initial study is superseded.


`native_roof_paint.py --source <verified raw library> --out <new private library>
--kit <native kit>` fits two finite sections of the original DD2 inn2048 roof
painting to the Abbey's curved decks. It removes the two old dense tile grids,
raises the existing decks .15m, and remaps retained physical ridge saddles. Native
red clay is explicitly tinted to slate with .62/.88/.91, reviewed in DD2. No paint
edits. Keep other architecture/paint and nine raw/baked services exact; audit source
bounds, every retained material primitive and finite UVs. Rebuild matching contact
and field. Evidence hamlet-abbey-roof-v61/art/models-reviewed; early per-tile/blank
crop/cyan studies are rejected. Blender colour alone is not acceptance evidence.


`guild_native_roof.py --source <verified raw library> --out <new private library>
--kit <native kit>` fits five finite sections of the original DD2 inn2048 painting
to four curved Guild roofs. The tall hall joins two different intact sections at
an existing row for more courses; other roofs use single panels. Remove four dense
tile meshes, raise their original closed decks.12m, retain47 ridge saddles and
dormer flashing/ironwork. Muted clay and Abbey slate factors require native colour
review. Keep other paint/geometry, source envelope and nine services; audit every
material primitive, finite UVs, contact and matching field. Evidence v62/models-reviewed;
the coarse and bright-clay studies are superseded. No generated or edited pixels.


`guild_native_oak.py --source <verified raw library> --out <new private library>
--kit <native kit>` fits original Assistance2048 paint to163 framing/eave members
and native inn opening-board paint to one fitted doorway. Shared frame oak factor;
separate source-compensating door factor. Finite aspect-fitted samples, no pixels
edited/upscaled. Keep all624 geometries, roof/native frontage/iron and two leather
grips; audit164 UV/material bindings, both decoded paint sources, source envelope,
all material primitives, contact and matching field. Evidence
hamlet-guild-joinery-v63/art/models-reviewed. Flat-door/cart-stripe studies are
superseded. Review native town appearance; a material called oak can be leather.


`sanitarium_native_roof.py --source <verified raw library> --out <new private library>
--kit <native kit>` fits original coast_buildings_03 shingle paint to the retained
swept turret and porch. One finite arc-length cap wrap with rear seam; finite
porch/ridge projection. Remove two grids and restore deck heights.035/.12m. Keep
hips/finial, native house/plinth, plaster, green panes and other geometry. Preserve
original2048 pixels and shared muted slate factor; audit cone seam spans, finite
UVs, both material masks, retained paints/geometry/source envelope, every material
primitive, contact and matching field. Native town review is required. Evidence
hamlet-sanitarium-roof-v64/art/models-reviewed; rejected mask study is superseded.


`guild_native_standards.py --source <verified raw library> --out <new private library>
--kit <native kit>` replaces two DD1 cloth grids with complete native Olde Guild
Hall banners. Preserve original347x572 RGBA/full UVs/aspect on two planes, existing
rods and physical sword crest; explicit muted ivory/teal finish. Retain622 other
geometries/UVs/paints, envelope/scale and nine services. Prove decoded pixels/alpha,
factor/emission/both bindings, all material primitives, contact and matching field.
Review four model views and native town colours. Evidence hamlet-guild-standards-v66/
art/models-reviewed; paired framed Crossed Swords study is superseded.


`sanitarium_native_oak.py --source <verified raw library> --out <new private library>
--kit <native kit>` maps native Assistance oak onto36 cornice/eave/bracket/post
members and a92x152 native inn door field onto the retained doorway. Shared frame
and source-compensating door factors match the existing oak family. Keep all444
geometries/15469tri,407 other UVs/paint, native architecture and nine services.
Audit both original2048 sources, factors/emission/bindings, member-specific finite
UVs, source envelope/placed geometry, contact and matching field; inspect four views
and native town. Evidence hamlet-sanitarium-joinery-v67/art/models-reviewed.
The dark Academic-door study is rejected and was never installed.


`smithy_native_oak.py --source <verified raw library> --out <new private library>
--kit <native kit>` fits108 structural/window/roof-binding members to native
Assistance oak. Six fitted door boards share one finite native inn field via
world coordinates. Retain199meshes/17590tri,85 other UVs/materials, work props,
anvil sign/native architecture/thatch/fire/iron and nine services. Audit both
original2048 sources/factors/emission/114 bindings, member-specific finite UVs
and continuous six-board projection, source envelope/placed geometry, contact
and matching field. Four model views/native town review required. Evidence
hamlet-smithy-joinery-v68/art/models-reviewed; per-board stripe study rejected.


`smithy_workprops.py --source <accepted raw library> --out <new private library>
--kit <native kit>` fits twelve retained Blacksmith props. Shroud2048 supplies
the intact cut end and vertical bark panels; Assistance2048 supplies the wheel,
hammer, hub and eight spokes. The wheel rim unwraps once. Native pixels remain
unchanged; explicit muted factors fit the town family. Retain all 199 geometries /
17590 triangles, 187 other UVs/materials, architecture/iron/fire/sign and nine
services. Audit decoded pixels/factors/bindings, finite fields, native cap source
coverage, wheel wrap, source envelope/Core placement, contact and matching field.
Four model views and actual native town review are required. Evidence is in
hamlet-smithy-workprops-v70; the striped-stump study was never installed.


`abbey_native_shingles.py --source <accepted raw library> --out <new private
library> --kit <native kit>` replaces the Abbey's coarse inn-clay roof field with
original coast_buildings_03 grey shingles. Three finite fields fit the two curved
decks and 31 retained physical ridge caps. Share the Sanitarium's explicit slate
factor, preserve 757 meshes / 18704 triangles, 753 other UVs/materials and nine
services. Audit original RGBA/factors/bindings, all finite UV triangles, source
envelope/Core placement, contact/matched field; review all four views and actual
native town capture. Evidence: hamlet-abbey-shingles-v71/art/models-reviewed/.
