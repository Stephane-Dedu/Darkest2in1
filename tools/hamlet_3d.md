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

Round219 replaces the ten service placeholders with the verified v02 building library. Install its private
runtime package under `Paths.NativeRoomSceneryFolder/HamletModels`; see [the packaging steps](hamlet_models/README.md).
The package includes all ten exteriors, their original UVs and paint, DD1 architectural details and service
emblems. Disk parsing and SHA256 verification run on a worker. Unity uploads one texture per frame, then builds
one service per frame. Node transforms and painted base/emission factors survive import; primitives are batched
by material and two-sided cloth retains back faces. Every uploaded texture, mesh and material is owned by the
stage and released on rollback. No native shop behaviours or campaign save paths are instantiated.

`Core/Presentation/HamletLayout` places the coach and market below the workshops, then Guild and Abbey on
higher terraces. The hillside rises 15 world units with level building foundations. A curved road and separate
approaches follow that surface, with downhill retaining walls and stone steps. A higher three-quarter camera,
native Valley trees/rocks, camp props and mountain/sky assets provide depth. The loaded road plates supply a
material and are hidden once the terrain is ready. No extracted game art is committed.

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
Do not launch before 2026-10-11 16:56:44 UTC / 18:56:44 Paris unless the owner changes that instruction.

NPC idles, upgrade/event exteriors, the coarse Ancestor sculpt, individual trim painting and final lighting
still need art work. Player.log also repeats URP RTHandle reallocations while the staged and native cameras
render different targets; performance tuning remains. Saved-campaign transactions/embark need separate
acceptance. This preview checks presentation, routing and resource lifecycle without selecting an estate.
