# Hamlet architecture and paint

The Hamlet retains DD1's service silhouettes. DD2 supplies the rendering language:
large painted planes, irregular ink shadows, worn edge catches and restrained colour.
The current v20 study separates their material identities and replaces legacy DD1 bitmap details. Native-game acceptance
is tracked separately in MODLOG and PARITY; a Blender render is not an in-game result.

## Architecture

| Service | Primary recognition | Construction |
|---|---|---|
| Abbey | Left projecting portal, amber rose, three lancets, rear bell tower | Broad nave, stepped buttresses, sagged slate roof, native Sprawl tower at uniform scale |
| Guild | Oxblood dormered roof, sword standards, crossed blades and entrance weapon stand | Stone ground floor, plaster/timber upper hall, recessed iron-bound door, smaller armory wing; native DD2 swords/shield on shared oak |
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


## Paint per physical plank, v39

A detailed timber painting can still fail when the entire sheet is stretched across
a door. Broad painted splits become oversized holes. Fit separate planks inside
the existing outline, retain a backing, and sample continuous grain per member.
Match the strip's pixel aspect to the physical board; shorten its U range for
shorter boards. Small kerfs and preserved local wear provide age while keeping
the oak structurally readable. Share existing paint and colour, retain fitted iron,
and compare close detail and the actual town camera before accepting.


## Painted form and service wall harmony, v40

DD2 detail uses confident seams, broad painted facets, occasional decisive cracks
and quiet surface areas. Uniform photographic mottling can hide the form at town
scale. Compare with the complete native inn and Academic's house before accepting
generated paint. Share a restrained grey stone/lime palette across services while
keeping distinct finishes. Preserve authored trim and native atlases unless a
separate comparison supports replacement.

A painting's edge wear is lost if UVs sample only its quiet centre. Fit the useful
paint to the physical wall height; map a circular tower once with the join against
the house. Give the exposed platform top paving instead of vertical limewash.
Inspect all service views and the actual town camera, not just texture swatches.


## Painted tile faces at useful resolution, v41

Physical roof courses need useful painted face samples. Enlarging a tiny patch
from a complete roof painting preserves neither brushwork nor detail. Review
larger separate slate/clay faces against the native Academic/Inn roofs, retain
the existing fitted geometry, and map each old sample rectangle into an inset
atlas cell. Keep seams/shadows from adjacent painted courses out of the face.

Respect the shared slate/clay colour families and material factors. Give thin
cut sides a finite local paint area; a collapsed UV line stretches one pixel
over the edge. Review reimported models, original geometry/UV/material retention
and the actual native town before accepting. Keep older underlaps/native art.


## Fitted stone and painted edge hierarchy, v42

Reusing a good wall painting does not automatically produce good architectural
trim. Quiet interior-only crops remove its painted edge hierarchy and can make
the geometry read as bare grey blocks. Review whole stone faces with controlled
rim highlights and ink seams, fit them to individual courses, and retain common
stone material factors. Stable per-mesh choices avoid identical wear on every
block. Preserve all architecture and native art; check noncollapsed UVs, close
views and the whole town before accepting a shared finish.


## Legacy mouldings, round289

Window surrounds, deep sills and roof verges share the unchanged reviewed wall286
stone painting with the fitted trim. Eight varied complete stone-face windows keep
the ink and edge catches; orient the painting along each short physical member.
Map end caps on their own face axes rather than collapsing the long-axis UV.
The 182 recorded pieces retain geometry and material factors. Whole native atlases,
the three independent wall finishes, roof courses and entry oak/iron stay exact.
No new bitmap is generated. Native full-town review is required after model views.
Terrain/cliff form, directional scene lighting and small signs still need review.


## Native medical detail, round290

The Sanitarium's flat mint emblem is replaced by DD2's115-triangle rope-bound
hospital cross, isolated from the native hospital mesh by its complete UV island.
Keep the original2048 painting, native proportions and UVs. A rigid yaw faces the
approach; the back seats against the wall. Retain green glass and shared service
colours. Four reimported views, exact native UV/pixel/triangle-edge checks and the
full native town view precede acceptance. Source/candidate provenance stays private.


## Cliff support, round291

Keep the reviewed cliff painting and shared service colours. Bounded angular
relief belongs on steep natural slopes, outside roads, building aprons and the
outer terrain boundary. Cache one position per grid coordinate to keep adjoining
facets closed. Preserve paving, stairs and masonry exactly. Shape comparison and
native whole-town inspection are required; finer surface relief does not establish
full scene lighting or native DD2 production parity.


## Native campsite silhouette, round292

Fit a complete native Tangle canvas tent with uniform scale and rigid facing.
Retain its original painted atlas, UVs, poles and ropes. Face the plain cloth
toward the service approach while keeping the native decorated side intact.
Remove the replaced authored shelter, floating seam/peg details and intersecting
bedroll. Retain the original equipment and fire arrangement. Judge scale in the
whole native town: the initial6.4m study passed interaction but looked too small.
The accepted8.7m shelter remains inside the old horizontal footprint with a taller
silhouette. Keep shared service colour grading and verify four views/source paint,
proportions, retained geometry and the native town before accepting.


## Ground light continuity, round293

Surface quality includes its surroundings. Carry the same existing foundation
contact shade and warm entrance pools through both earth and paving. Mixing raw
stone over lit earth erases the lighting at complete coverage. Keep open-road
albedo, original paint, building palette, exact ground pixels outside the road
and smooth margins. Verify actual shaded approaches, open road centres and fully
paved lamp pools; this is contact continuity, not directional cast shadows.


## Native ground as the review reference, round294

Use the game's baked4096px ground/lane/valley maps for composition review. Capture
only through a frozen private preview fixture, restore normal binaries/profiles,
then convert RGBA to PNG without changing decoded pixels. Preserve row origin,
sRGB and clamp sampling. Apply no second tint, vertex shade or emission reduction.
Core source stamps and image hashes reject stale/changed maps. This fixes the
historical ground approximation; model lighting and other native shader behaviour
still require a native comparison. A good offline image alone is insufficient.


Round295 Core material review: local-art/ui-overhaul/hamlet-material-review-v50/CONTINUE.md.
ModelReviewExport uses the actual Core glTF reader, palette, tint, contact bands
and world placement.41 graded paint variants; native_model_review.py binds unlit
geometry through town_preview --native-models. Geometry/UV/winding/paint/alpha and
packed-scene audits PASS, render compared with native294. One Guild standard resize
and remaining shader differences are approximate. Tool-only; normal293/camp292
art unchanged, game CLOSED. Fresh usage/baseline next296; no pending restore.
Use Core's colour families and contact hierarchy when reviewing service art.
The previous diffuse authoring preview made approved buildings look too dark.
Do not repaint accepted surfaces to compensate for a mismatched preview. Preserve
native geometry/UVs and explicit resize/shader limits; inspect the actual game.


## Abbey eave pinnacles, v54

DD1's Abbey has four slim pinnacles above its front buttresses. Reuse complete
native Sprawl corner columns, with alternating original paint and small height
variation. Keep their uniform proportions, full native UVs and the rear belfry's
existing1024px material. Seat their feet inside the existing roof/buttresses;
the tips remain below the belfry and the service bounds stay unchanged. Broad
nave, left rose portal and three amber lancets retain the recognition hierarchy.
Do not add a separate colour family or duplicate material batches for this trim.
The ground field must be rebaked against the new service source identity.

## Sanitarium rubble base, v55

DD1's Sanitarium has rough masonry below its cracked plaster. Native Academic-house
foundation fragments supply the painted relief in three staggered courses. Retain
their original UVs,2048px paint and uniform proportions, and the existing house
material. Their hidden black backing faces are unsuitable above ground; exclude
those faces and let the original solid tower close the relief. Preserve inked
edges on the painted faces. Keep the added stones within the existing service
bounds, below the green lancet, and rebake contact and the matching ground field.


## Continuous plaster at architectural scale, v56

Native art can fail when its sampling scale changes. Tiny wall samples from a
mixed atlas turned thin native cracks into thick black bands on the tall turret.
Reject that result before deployment. A missing continuous wall painting can be
generated using the native house as reference, but inspect the physical model,
all sides and the actual town before accepting it. Prefer broad value facets,
restrained thin cracks and localized exposed stone over fine all-over scratches.

The reviewed2:1 sheet describes7.5m by4.2m of wall. Sample the porch/undercroft at
that scale, unwrap the lower turret once with its seam against the house, and
use an intact middle band on the timber-framed upper stage. Keep caps finite.
Retain the native roof/oak/green glass and existing shared stone material factors.
Keep generated art/provenance separate from extracted native sources. Original
geometry, unaffected UVs, decoded native paints and matching ground-field guards
must still pass; this improves plaster finish, not complete production-art parity.


## Native canvas with fitted supports, v57

Use actual cloth geometry when a native source has useful folds and seams.
The Assistance canopy's upper faces fit the Nomad with one rigid rotation and
uniform scale. Its lower curtains would hide the existing shop and are excluded.
Keep the original 2048px atlas and UVs. Explicit muted plum and oak material
factors preserve the caravan's colour cue while using shared cloth/timber grading.
Retain original pixels and distinguish those factors from native source colours.

Roof replacement includes its supports. Fit wood end panels to the new curved
profile, seat all four hoops on the living body and support the eye-sign posts.
Inspect front, side and rear views. Brace bevels must not expand the service
envelope. The awning uses the same native painting with a finite clean crop.
Retain the entrance, lamps, shop windows and original bounds. Review in the
actual town and refresh matching contact/shadow data before acceptance. Older
authored body wood and wider lighting remain separate quality gaps.


## Native timber grain at member scale, v58

Use native plank and beam paint where its broad facets and ink edges survive
the actual game camera. Sample individual members rather than whole sheets of
unrelated boards. Match the sample aspect to the physical face and follow the
long grain through the member's local transform. Keep end faces inside finite
samples. Vary source rectangles on adjacent long boards so damage does not form
a repeating pattern. Wheel rims need a cylindrical unwrap with a finite seam.

The Nomad's body, doorway, stair and wheel joinery now share its canopy's native
oak material and unchanged tint. Original2048 pixels are retained. This removes
the previous noisy painting and one material batch while preserving geometry,
placement and the common service palette. The original DD1 eye crest remains
a separate art gap; this pass does not claim the whole town has native art parity.


## Merchant identity from native art, v59

Check the native crest library before repainting a service symbol. DD2 already
has a Nomad Wagon crest that includes the merchant eye and a painted caravan.
Keep the full sprite, its original alpha and aspect. Fit the sign geometry to
that aspect, with a rigid board on the existing suspension, rather than stretching
the painting over the previous cloth grid. Use shared timber grading with the
source's ochre/red wood. Check the lower edge against the canopy and entrance,
preserve the model envelope and inspect the sign at actual town scale.


## Shared oak, different architectural roles, v60

Use the same native oak family and muted factor across interactive services.
Choose samples for the job: broken ends can suit a wagon but read as holes in
a fitted church door. Quieter cart boards retain broad DD2 facets and ink grain;
continuous sill paint suits long eaves. Preserve the portal fit, ironwork and
dressed-stone arch. Keep pixels intact, map at face aspect and check the actual
town. A connected native facade is not a ready-made portal. Avoid distorting
its paired windows or repainting it just to add more ornament.


## Continuous native roof painting, v61

Preserve painted course irregularity over the complete roof slope. A dense grid
of identical individually chipped tiles creates repetition even at high pixel
resolution. Use one finite, fully painted native panel per slope, mapped with
distance along its curve. Keep silhouette and physical ridge detail; avoid blank
atlas regions and neighbouring roof shadows. Original red clay can supply slate
shapes, but its material tint must be explicit and checked through native DD2
grading beside the other service buildings. Do not accept a Blender-only colour
match: the v61 cyan study passed geometry checks and still failed town harmony.
Use muted slate, warm oak and local clay accents without making every service
the same colour. Prefer shared material families and retain readable identities.


## Roof course scale and colour harmony, v62

Fit native roof detail to each architectural role. One cropped section can make
tiles oversized on a tall pitch. Two different intact sections can join at an
existing deck row, giving more courses without repeating a motif or adding dense
geometry. Keep source pixels, inspect the join, vary dormer samples and retain
physical ridge detail. A bright isolated clay study can clash with the town:
check beside native Tavern paint, muted Abbey slate and cream plaster at normal
zoom. Keep Guild red clay, armory slate and their readable silhouette, with muted
families rather than identical colours on every interactive building.


## Native timber chosen for its role, v63

Share the oak family across service framing, eaves and fitted joinery, then choose
source paint for the shape. Quiet cart-board interiors suit small broad members;
continuous sills suit long beams. A whole doorway needs opening-board paint: a
quiet cart sample can be blank at that scale and a broken plank can look like a
hole. Preserve physical joints/iron, follow local grain and fit source aspect.
Keep original pixels; compensate different source values with explicit material
factors and judge colour harmony in native DD2. Inspect membership before replacing
an old material: leather grips can share a material named oak. Retain them until
their own surface can be reviewed.


## Native shingles on a swept cap, v64

Keep the recognizable turret profile and physical hips while replacing a repeated
photographic chip with a continuous native weathered field. Match course scale
to the cap and porch. Arc-length mapping follows the curve; place the single
angular join beneath a rear hip and limit each triangle's source span. Opposite
porch slopes must read uphill. Native coastal paint can fit Hamlet architecture
without marine overlays; use an explicit muted slate finish and compare in town.
Preserve original pixels and physical ridge detail. Add material slots before
restoring face indices: otherwise Blender can silently erase the cut-edge mask.


## Native hanging cloth, v66

Keep cloth and rigid heraldic signs distinct. A native sprite can provide folds,
torn contour and ropes with more legible brushwork than a stretched legacy crop.
Preserve full original RGBA/aspect on simple supported planes; retain clear space
around the service's physical identity crest. Compare actual native town size and
colour. Ivory cloth with a small muted accent can sit beside warm clay/cool slate
without another dominant colour. Do not mistake opaque grey atlas surroundings
for transparency; isolate native faces/UVs before reusing combined props artwork.
