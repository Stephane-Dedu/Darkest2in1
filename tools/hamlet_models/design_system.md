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
