# Perspective Hamlet

`Look.Hamlet3D` is an experimental presentation flag, off by default. The town's **3D town** button enables it;
**Painted town** immediately releases its camera, meshes, materials, render textures and Addressables handles.
The choice persists in the mod config, not the estate. With the flag off, the approved private hillside pack
remains the first choice, followed by the original installed DD1 town. Missing models or a 45-second load
timeout also retain that fallback. Switching off and back on retries a failed load.

`Dd2/HamletStage` owns a perspective camera and a 1920x1080 render texture. It copies visual components from
installed Addressables assets into a distant scene; it never instantiates native road nodes, shops, triggers,
inventory behaviours or animators. Native meshes and textures stay in the owner's install. Skinned geometry
is baked once into owned static meshes. DD2's native shader/material copies retain texture mapping, depth and
cutouts, with road-specific lighting, fog, distance darkening and dithering disabled. Shared native objects
are never edited. Any temporary camera shader globals are restored immediately after rendering.

The scene contains the Hoarder, Field Hospital/nurse, level-three inn, Watchtower, cathedral entrance,
coach, camp shelter, graves and Altar of Hope. A shorter inn component forms the forge. DD1's installed
blacksmith anvil/forge, guild banners and abbey windows are atlas-mapped onto fixed world surfaces. These
details move in perspective with the buildings. Continuous sloped native foundation paving, camp/street props,
Valley scenery and native mountain/sky assets connect the locations. The source road plates supply a fallback
material and are hidden after the shared ground is ready. No extracted game art is committed.

World picking selects the nearest service renderer bounds and calls the existing `HamletUi.Open` dispatch.
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

Private captures, native logs and the pre-change plugin/config backup are under
`local-art/ui-overhaul/hamlet-3d-v01/`, outside Git. The exterior is a first static composition: NPC idles,
upgrade-dependent geometry, special event skies and further building-specific texture work remain art scope.
Service transactions and saved-campaign embark need separate campaign acceptance; this preview checks the
presentation, routing and resource lifecycle.

Round217 adds a separate [offline building model library](hamlet_models/README.md): ten DD1-referenced
exteriors with native DD2 parts/surfaces, GLB and editable Blender outputs, four camera angles and exported-file
validation. Private gallery: `local-art/ui-overhaul/hamlet-buildings-v01/gallery.html`. These first model studies
have not replaced the stage's native scenery. Runtime import/material/placement and further art fitting remain.
