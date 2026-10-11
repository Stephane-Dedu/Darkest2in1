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
fog, outline shaders or animation. Both private galleries remain available; neither replaces the live town.

For integration, `src/DarkestDungeon3.Core/Presentation/GltfModel.cs` currently reads JSON glTF with external `.bin` and image
files. It does not read these packed GLBs or apply their painted color factors. Convert the package or extend
that loader, fit the materials to DD2's native shaders, and verify axes/scale before replacing HamletStage parts.
Keep `Look.Hamlet3D` and the painted rollback throughout that work.

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
The scripts never read saves, launch DD2 or change the installed game. The independent corridor and
Shieldbreaker pipelines are untouched.

Additional public visual reference: [Red Hook's DD1 media gallery](https://www.darkestdungeon.com/darkest-dungeon/media/).
Building-specific shape decisions above come from the installed art, not from third-party reconstructions.
