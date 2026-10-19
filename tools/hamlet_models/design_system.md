# Hamlet architecture and paint

The Hamlet retains DD1's service silhouettes. DD2 supplies the rendering language:
large painted planes, irregular ink shadows, worn edge catches and restrained colour.
The current v19 study rebuilds the Abbey, Guild and Sanitarium. Native-game acceptance
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
| Ashlar | Calm blue-gray block faces, selective pale chipped edges, black corner wedges | Structural walls and plinths; authored UV period 3.8 model units |
| Slate | Muted charcoal purple, broad staggered shingles, dark overlap shadows | Abbey and Sanitarium; courses run from each eave uphill |
| Red roof | Dusty oxblood and clay, same shingle scale as slate | Guild; avoid bright red or pink wall panels |
| Timber | Native DD2 painted wood and black joints | Posts, braces, door planks and eaves |
| Plaster | Native cracked lime panel, without window or brace pixels | Upper halls and turret infill |
| Iron | Near black, small dull edge catches | Hinges, straps, finials and mullions |
| Glass | Small amber areas; restrained green for the Sanitarium | Recessed behind tracery; never broad luminous walls |

The v18 roof and ashlar paintings are newly generated from native reference crops.
Their source images, exact prompts and outputs stay in the private v18 folder.
Native timber, glass grain, emblems and complete prefab atlases retain their existing
pixels. The former tiny plaster crop included a brace and produced false black stripes;
v19 uses a clean native panel with a 2.3 by1.7-unit projection. Timber follows the long axis of each member; terrace and stair tops use native courtyard paving. Increasing bitmap dimensions does not establish quality: inspect
painted shape, repeat seams, scale on the mesh and visibility at the game camera.

## Review and delivery

`architecture.py` exports three replacement GLBs, re-imports those files, checks
triangle counts and bounds, decodes every embedded image and renders four views.
It preserves the seven other verified source GLBs byte-for-byte. The authoring budget
is 45,000 triangles per rebuilt service; material batching remains the runtime's job.

Keep the stone foundation and stairs inside the service's grounded terrain pad.
Runtime fits the longest horizontal model dimension to `Site.Width`, so changing
depth also changes the apparent height and width. Review in the full town after
every substantial silhouette change. Verify all ten service clicks, scene release,
reload and cached return before marking an installed round complete.
