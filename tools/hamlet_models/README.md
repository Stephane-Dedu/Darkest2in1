# Hamlet building models

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
