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
