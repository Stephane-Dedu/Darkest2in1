# Hamlet architecture and paint

The Hamlet retains DD1's service silhouettes. DD2 supplies the rendering language:
large painted planes, irregular ink shadows, worn edge catches and restrained colour.
The current v20 study separates their material identities and replaces legacy DD1 bitmap details. Native-game acceptance
is tracked separately in MODLOG and PARITY; a Blender render is not an in-game result.

## Architecture

| Service | Primary recognition | Construction |
|---|---|---|
| Abbey | Left projecting portal, amber rose, three lancets, rear bell tower | Broad nave, stepped buttresses, sagged slate roof, native Sprawl tower at uniform scale |
| Guild | Oxblood dormered roof, sword standards and crossed blades | Stone ground floor, plaster/timber upper hall, recessed iron-bound door, smaller armory wing |
| Sanitarium | Round stone tower, timber jetty, pointed slate cap, green glass | Uniformly scaled native Academic house, separate octagonal turret and projecting apothecary porch |
| Blacksmith | Sweeping thatched roof, unequal chimneys, small dormer, open anvil bay | One thick roof over the enclosed workshop and sheltered working area; brick hearth, lime infill and timber framing |

Service doors must remain visually distinct at the normal Hamlet camera distance.
Entrances and windows use cut recesses, a dark backing, physical jambs and sills.
Roof silhouettes include a dipped ridge, flared lower slopes and visible thickness.
Do not distort an entire native building to fit a prescribed box. Uniformly scale
native components, or rebuild the required architecture around their original UVs.

## Materials

| Material | Shape and colour | Placement |
|---|---|---|
| Abbey | Worn limestone, lime remnants, varied repair stones, dark fractured joints | Metric 5.5 by 3.6-unit wall painting; cropped individual stones for trim |
| Guild | Warm sandstone, old brick repairs and broad broken taupe plaster | Metric 5.5 by 4.2-unit lower wall; selected plaster area on upper infill |
| Sanitarium | Cold green-grey limewash, thin rubble courses and damp mineral streaks | Metric 4.8-unit walls; continuous cylindrical tower map, cropped upper infill |
| Slate | Charcoal/plum, cleft mineral planes, chipped selective edges and repair slates | Abbey and Sanitarium; courses run from each eave uphill |
| Red roof | Worn oxblood clay, scraped surfaces, soot and mixed repair ages | Guild; avoid uniform red fill or bright orange |
| Timber | Long angular grain, dark fissures and selected weathered splinters | Painted at full resolution; follows each beam/door's long axis |
| Iron | Near black, small dull edge catches | Hinges, straps, finials and mullions |
| Glass | Small amber areas; restrained green for the Sanitarium | Recessed behind tracery; never broad luminous walls |

The nine v20 paintings (three walls, two roofs, wood, three emblems) use native DD2
atlases as references. They are generated art, not native extracted textures. Exact
prompts, originals, provenance and two mesh iterations stay in the private v20 folder.
The old DD1 Guild banner, forge sign and wagon eye are replaced by new paintings.
Window grain comes from DD2's Academic house; the Abbey rose uses that glass behind
physical six-lobed tracery. Whole native building atlases retain their original UVs.

Before reusing any texture, inspect it against native DD2 art and on the exported mesh
at both close range and the actual Hamlet camera. Check value grouping, irregular
painted shapes, middle-scale wear, edge definition, UV direction, repetition and alpha.
A tiny strip blurred across a door is unacceptable even when its source is native.
Increasing resolution alone does not satisfy this gate. Keep separate architectural
identities; common trim can share paint, but all main walls must not use one brick tile.
Floor/stair tops use native courtyard paving at a smaller scale than v19.

## Review and delivery

`architecture.py` exports three rebuilt and two detail-repainted GLBs, re-imports those files, checks
triangle counts and bounds, decodes every embedded image and renders four views.
It preserves the five other verified source GLBs byte-for-byte. The authoring budget
is 45,000 triangles per rebuilt service; material batching remains the runtime's job.

Keep the stone foundation and stairs inside the service's grounded terrain pad.
Runtime fits the longest horizontal model dimension to `Site.Width`, so changing
depth also changes the apparent height and width. Review in the full town after
every substantial silhouette change. Verify all ten service clicks, scene release,
reload and cached return before marking an installed round complete.


## Workshop finish, v21

Forge firebricks use world-height courses at a 3.5-unit period so imported local axes
cannot turn them vertical. Thin slate canopy, warm brick, black recess and worn metal
distinguish the workshop. Caravan cloth maps once along its full length; narrow boards
and physical tread gaps define the joinery. Native cottage/Hoarder atlases retain their
original UVs. The three new paints are generated reference paintings, not extracted art.
Review all sides: a convincing front can conceal missing side walls. Inspect roof joins
and texture scale in the town before accepting another reuse. Native thatch and large
wagon end grain remain candidates for a better fitted architectural finish.

## Smithy reconstruction, v22

Replace the cottage/canopy collision with one continuous roof, real chimney openings,
a fitted dormer and closed side gables. Keep the eave high enough to expose the anvil
and furnace at the town camera. Side windows belong between braces, with actual cut
recesses. A sign must hang clear of the roof rather than disappear behind it.

Dense grey-brown reed, stained lime and warm small-course firebrick give the smithy a
different material identity from the civic services. Thatch follows the roof surface
distance; plaster uses a shared world projection. Avoid shrinking painted straw into
many noisy repetitions. The hearth uses a purpose-made static painting behind separate
coal geometry; the native window atlas is appropriate for glass, not fire.

The three new paintings are generated, with native DD2 references retained privately.
Judge them against those references and the full-town capture, not resolution alone.
Native camp props retain their original geometry and UVs; only the old cottage mesh
is explicitly replaced. Nine other service GLBs remain byte-identical to v21.


## Ground, v23

Use small worn native cobbles for streets, larger flags for building aprons and distinct
earth/grass between them. Keep original paint detail at plausible world scale. Avoid
mirrored stone courses and a second material tint across blended lane edges. Grass must
leave entrances and stairs clear; upright cards require explicit alpha clipping and the
reviewed atlas UVs. Generated earth is a missing-material supplement, not extracted art.
Bury the black basal third of the native grass painting; group blades rather than scatter
small isolated dots. Check a full-size diagnostic sample before blaming the atlas or shader.
The Sanitarium has no nurse and the central memorial retains its stonework without the
robe-shaped figure. Review ground and silhouettes at the native town camera.


## Service terraces, v24

Use native cobbles for horizontal courts and intact painted courses for retaining walls.
Crop foundation_panel_06 to pixels (5,88)-(507,293); its cap and loose rubble do not tile
as a wall. Keep courtyard paving at a 6.2-unit world period, retaining stone at 3.8 by
1.8, accounting for each service's runtime fit. Connecting stairs use the street's
4.8-unit cobbles and one world-space phase across treads. A physical block gets one
painted stone, not a miniature brick wall. Keep main facade material identities distinct.
Graveyard soil reuses the reviewed generated earth at a 5-unit period. Native source
pixels remain unmodified except cropping; generated earth is labelled separately.
Inspect re-imported GLBs and the native town, including the routes joining the terraces.


## Fitted masonry, v25

Build long jambs from separate fitted stone courses. Give arch heads individual
voussoirs, projecting hood moulds and sills with shadowed drip edges. Buttresses
should taper through real ashlar courses, with outward-sloping weathering caps.
Sample individual detailed stones from the reviewed painting; do not stretch a
tiny strip over a whole pier or map a miniature brick wall onto each physical block.
Corbels under the eaves connect roof and wall without hiding the windows.

The Sanitarium's green glass, pale weathered lime, turret and small cellar apertures
keep it distinct from the Abbey. Map cylindrical plaster after world transforms;
avoid recurring horizontal bands. Check backs as well as fronts, and retain the
native house/tower UVs. Added depth must stay within the established service footprint.


## Physical roof courses, v26

Slate and thatch need different edge construction. Slate uses thin overlapping
pieces, restrained corner chips, staggered joints and folded ridge saddles.
Reed bundles have broad grouped grain, thicker lower ends and uneven cut tips.
Keep the pitch, dipped ridge and flared eaves; avoid uniform teeth or unrelated
random noise. Use whole reviewed slate faces and reed clumps at useful resolution.

Fit courses to the exported outer roof surface, including solidified thickness.
Retain a painted shadow underlay. Cut individual bundles around chimney/dormer
openings before merging; booleans on overlapping solids can remove valid neighbours
while still exporting a technically valid mesh. Review roof coverage from both sides.
Combine pieces by material to keep renderers bounded. Preserve service footprint,
native components and the accepted wall/terrace work.


## Reed edge finish, v27

Keep the thatch underlay close to its fitted outer bundles, leaving only clearance.
A low second sheet reads as a floating rim. Side UVs need finite painted area across
thickness; repeating a single texture line produces stretched streaks. Bent n-gons
also triangulate into small vertical end caps made entirely from upper vertices.
Give those caps a planar local mapping while preserving the main roof painting.


## Service roof joins, v28

Use smaller, irregular physical courses at the service camera scale. Retain the
Guild's red clay identity and the Sanitarium's slate turret, with the native
Academic's house as the nearby detail reference. Use individual painted tile faces,
excluding baked shadows from neighbouring courses; these create false holes when
remapped onto new pieces. Dark painted cut edges retain the game's graphic seams.
Fit each course to the actual exported deck, including solidified thickness. Check
clearance with rays as well as four rendered angles. Keep original thin hip bindings
when they close seams and define model bounds. Fold ridge saddles onto both pitches;
seat dormer aprons against the curved roof and support long eaves with timber rafters.
Geometry and paint must read at the native town camera before accepting a candidate.


## Native entrance lamps, v29

Use DD2's assistance lamp geometry and painted glass at uniform scale. The square
amber lamp fits Guild/Tavern timber architecture; the narrower round pale lamp fits
Sanitarium/Caravan entries. Preserve cage, chain, ring and original UVs. Match the
new model at the actual town camera; source resolution alone is not acceptance.
Supports must meet physical walls or overhead beams. Check the native geometry:
old placeholder positions can be metres ahead of the facade. The Tavern's open
passage needs clear space, so its lamp now attaches beside the door. Use a diagonal
bracket where a lamp sits outside the porch jamb. These are painted light sources;
dynamic lighting and wider scene illumination remain separate work.


## Physical fixture support, v30

Move complete sign assemblies together, including relief, rim and chains. Each
suspension link must meet an actual crossbar. Trace inherited wall backplates when
replacing lamps; leaving them at old placeholder coordinates creates floating props
in the passage. Keep native painted lamps unchanged and check support placement in
side and town views, not only the flattering front angle.


## Shared service colours, round279

The owner requires harmony across interactive buildings. Use a shared cool limestone
and lime family, restrained warm timber, cool slate and muted clay. Plum cloth and
oxblood banners remain small service accents. Keep warm windows/fire and dark ink.
HamletPalette.BuildingSurface applies the family finish to actual material paint;
new material names must retain a recognizable family term. Mixed native atlases
need gentle whole-paint grading because roofs, walls and lights share one image.
Judge the buildings together at the actual town camera, not isolated swatches.


## Bonded service stonework, v33

Use individual arch courses and alternating corner stones at the building's scale.
Return corner courses onto both wall faces. Keep joints visible and seat back edges
inside the existing wall. Stone samples may share the reviewed limestone painting
while each service keeps its own wall composition. Retain the shared palette and
avoid adding a new saturation or brightness rule for every building. Inspect side
views before treating a hidden pane as broken; a neighbouring roof may occlude it.


## Architectural contact shade, v34

Shade the eave undersides and enclosed reveals from nearby geometry while retaining
material colour. Use short opaque-surface visibility rays, preserve painted lights,
and exclude alpha cards as opaque occluders. Connected coplanar faces share one
area-weighted value; raw triangle-centre results can create diagonal wall patches.
Keep a control plane and a covered plane in the bake check. Reuse the three existing
facet shades rather than adding renderer/material variants for every occlusion level.
This is static contact depth. Directional lighting and shadows between buildings
remain separate work. Judge it at the actual town camera and keep the palette279.


## Painted native fixtures, v35

Prefer a complete native painted assembly when its original scale, palette and
function fit a service. Preserve the crest, links and crossbar together, fit a
physical arm to the wall, and compare its UVs/proportions with the native source.
A source asset being native does not establish suitability: distant cliff cards
and sparse rock meshes in the same study did not meet the central-terrace detail
need. Keep those studies out of the installed scene.


## Whole paintings and atlas windows, v36

Keep texture format and projection together. The native eroded-hill atlas needs its
rock-only rectangular crop, corresponding world aspect and mirrored boundary. A
seamless square painting needs its full image, Repeat and equal world periods.
Reusing the atlas metadata silently discards painted detail and creates alternating
mirrored columns. Match terrain and outcrop projection; keep the same service colours
while comparing from the actual camera. This corrects sampling, not cliff silhouette.


## Fitted entrance iron, v37/v38

Give each oak leaf its own tapered hinge straps. Reuse the workshop metal paint
for dark iron and restrained rubbed edges. Rings need plates and pivots; verify
all supports in depth and seat backs into the oak. Preserve the arch, plank grain
and service colours. Compare textured triangles rather than vertex indices after
glTF re-export; legitimate reindexing can change vertex counts without changing paint.
