# Clickable 3D Hamlet: camera and background research

Research date: 2026-10-12. Design study only. This note does not change the game, camera configuration, assets, interactions, or saves.

## Recommended direction

Use a composed hillside overlook as the default Hamlet view. Keep the familiar DD1 arrangement legible, give each service an exposed entrance and recognizable roof silhouette, and connect the terraces to a larger valley. Put most new detail into the near ground and playable buildings. Use simpler, quieter shapes behind them.

I would prioritize camera framing, background continuity, and separation between buildings before adding more texture detail. The owner's screenshots already have many contrasting stone and vegetation marks. More marks will not solve overlapping silhouettes or the exposed edge of a plateau.

This is a project recommendation drawn from the evidence below, not a published universal recipe. There is no evidence here for one correct Hamlet field of view, pitch, transition duration, or mountain scale. Those need comparison renders with this scene and its actual HUD.

## What the sources establish

| Source | Directly supported finding | Limit of the evidence |
|---|---|---|
| [Chris Bourassa, Red Hook creative and art director, original interview, 2024](https://www.gamedeveloper.com/design/how-a-theme-of-a-road-trip-from-hell-would-shape-darkest-dungeon-ii) | Bourassa describes DD2 environment art beginning as 2D drawings, followed by geometry that supports the drawing. He describes hand-painted character textures and lighting that preserves volume, readability and the comic-book appearance. He also connects visible horizons to the intended sense of scope. | Firsthand developer testimony in a publisher-hosted interview. It explains DD2's direction, not the best composition for a stationary town menu. It does not describe every exported material or render pass. |
| [Robert Yang, original GDC 2018 lighting slides](https://media.gdcvault.com/gdc2018/presentations/Yang_Robert_LevelDesignWorkshop.pdf) | Slides 22–30 discuss fill light, fixed-view composition, lighting important routes, and separating setpiece silhouettes. The suggested process establishes global mood before local gameplay lighting. | General lighting instruction. No Hamlet-specific camera settings or usability study. |
| [Xbox Accessibility Guideline 102: contrast](https://learn.microsoft.com/en-us/gaming/accessibility/xbox-accessibility-guidelines/102) | Microsoft recommends separating important information from backgrounds and other elements through contrast, outlines, backing panels and configurable presentation. It gives numeric requirements for text and information-bearing UI. | The numeric UI contrast requirements should not be applied indiscriminately to every decorative wall, tree or painted stone. |
| [Xbox Accessibility Guideline 112: UI navigation](https://learn.microsoft.com/en-us/gaming/accessibility/xbox-accessibility-guidelines/112) | Microsoft recommends predictable focus movement, consistent actions and relative ordering, keyboard/controller access, an easy return route, and an alternative list when map navigation requires scrolling. | It does not prescribe a particular town layout or force a visible permanent list for every small hub. |
| [Xbox Accessibility Guideline 117: visual distractions and motion](https://learn.microsoft.com/en-us/gaming/accessibility/xbox-accessibility-guidelines/117) | Microsoft recommends controls for moving UI backgrounds and secondary camera motion. It identifies reading over a moving background as a barrier and suggests text backing when movement remains. | This supports configurable restrained motion. It does not establish an optimum animation speed for this project. |
| [Unity Cinemachine Position Composer](https://docs.unity3d.com/Packages/com.unity.cinemachine@3.1/manual/CinemachinePositionComposer.html) | Unity documents desired screen-space placement, frame limits, damping and areas where the camera does not reframe for minor target movement. | These are available camera concepts, not evidence that the mod needs this package or must use a tracking camera. |
| [Unity physical camera introduction](https://docs.unity.com/en-us/engine/6000.5/manual/cameras/physical/introduction) | Unity explains that focal length and sensor size determine field of view, and that different rendered aspect ratios affect fitting. | A focal length alone is insufficient to reproduce framing. The source gives no ideal focal length for a clickable hub. |
| [Epic Sky Atmosphere documentation](https://dev.epicgames.com/documentation/en-us/unreal-engine/sky-atmosphere-component-in-unreal-engine) | Epic documents artistic control of aerial perspective and height fog, along with a custom skydome approach. | Useful background-depth concepts. This Unity mod does not need to copy Unreal's renderer or simulate a planetary atmosphere. |

The [GDC 2024 listing for Marielle Fox's DD2 environment talk](https://gdcvault.com/play/1034192/Facing-Our-Failures-Creating-3D) confirms a relevant Red Hook presentation about the transition from 2D to 3D. Only the listing was inspected here. No detailed pipeline claims in this note rely on an unviewed talk.

## Comparable game hubs

### StarCraft II's Hyperion

Blizzard documents recurring visits to the bridge, laboratory, armory and cantina. Its Hyperion preview assigns upgrades and recruitment to specific rooms and describes opening laboratory upgrades by clicking the console. These are physical destinations with distinct service roles. See [Blizzard's Hyperion tour article](https://news.blizzard.com/en-gb/article/9977449/poll-hyperion-tour) and the [original Hyperion preview, Spanish-language page](https://news.blizzard.com/es-mx/article/13620/adelanto-del-hiperion).

Applicable interpretation: make the Blacksmith, Guild, Abbey, Sanitarium and Tavern readable as places, then use a focused service view after selection. A complete simulated village is not necessary for the menu to feel inhabited. One convincing entrance, sign, light and sound identity can communicate a service.

Limit: these pages support room roles and interaction, not exact camera paths, lens choices, or reasons Blizzard chose its composition. This is not a recommendation to replace the Hamlet overlook with disconnected room screens.

### XCOM 2's Avenger

2K describes configuring and building Avenger rooms to add campaign capabilities. The official XCOM channel's 2016 "Tour the Avenger" video presents full camera control as another way to inspect the headquarters. See [2K's XCOM 2 description](https://store.2k.com/game/buy-xcom-2-pc?drm=Xbox) and the [official XCOM video](https://www.youtube.com/watch?v=Hthzrzcb1NE).

Applicable interpretation: separate routine management from optional inspection. The default Hamlet view should make services easy to find. A later inspection mode could allow restrained pan or zoom without becoming a prerequisite for opening the Guild.

Limit: the first-party text does not specify the normal Avenger camera algorithm. The official video's description establishes an inspection feature, not evidence that a freely movable camera improves routine menu navigation.

### Darkest Dungeon II as the art reference

Bourassa's account favors a drawn composition with supporting depth and an intentional horizon. For our Hamlet, the useful lesson is to compose the roofs, terraces, valley and skyline together. A collection of DD2 meshes does not automatically produce a DD2 image. The camera and value structure still need art direction. This is our application of the [firsthand Red Hook interview](https://www.gamedeveloper.com/design/how-a-theme-of-a-road-trip-from-hell-would-shape-darkest-dungeon-ii), rather than a claim that DD2 has a comparable clickable town hub.

## Seven principles for the next iteration

The evidence appears above. The following are project proposals to test offline, not changes already installed.

1. **Keep a stable overview.** Compose one home shot and preserve its visual ordering when leaving a building. Avoid automatic orbit, cursor-driven camera sway, or hover zoom in the ordinary management view. Optional camera inspection can be a separate mode. This uses the fixed-view composition idea and the motion guidance without imposing a new navigation burden.

2. **Choose the camera by visible services.** The current camera is already fairly frontal: about 18.75 degrees down. Test a modest rise towards 22–25 degrees to separate paths and roofs while retaining facades. A lower 15-degree test compresses the terraces; a 32-degree test reveals more ground and background seams. Compare focal length and distance while keeping projected town size similar. A more cinematic shot fails if the Sanitarium becomes a sliver behind another roof. Record vertical or horizontal FOV explicitly, along with aspect ratio and camera position. These are observations from the private comparison below, not general angle rules.

3. **Make the town belong to terrain beyond its boundary.** Continue the main road beyond the settlement and bend it into a valley. Break the plateau outline with sloping ground, retaining embankments and a wooded lower edge. Place a near ridge behind the upper terrace, then quieter distant ridges and a broad mountain silhouette. Mist belongs mainly in gaps and low ground. Keep the immediate entrances and stairs clear. These layers are a suggested visual construction, not evidence that a large continuous landscape or volumetric fog is required.

4. **Give detail a hierarchy.** Keep facade linework and entrance features clear. Give approach roads enough texture to read direction and grade. Reduce the density and contrast of repeated wall joints and vegetation farther back. Keep background silhouettes broad. Check a reduced-size grayscale image before deciding that higher-resolution terrain is needed. Preserve readable shadows with restrained fill; do not brighten whole building textures into self-illuminated objects.

5. **Use a few strong identity cues for each destination.** Preserve the DD1 relative placement where practical. Give each service its own silhouette or prop cue: Abbey tower, Blacksmith forge and chimney, Guild training frontage, Sanitarium medical sign, Tavern clustered windows. Keep props near their owner building. Repeated paving and retaining-wall materials should connect the settlement, while dirt and grass blend between road margins. These specific cues are proposed Hamlet art direction, not a reconstruction of another game's implementation.

6. **Treat visibility and selection as separate review checks.** An entrance must be visible, but its clickable region also needs predictable ownership. Decorative foreground branches, smoke and wall trim should not obscure or steal selection. Hover or keyboard focus should identify one building with a restrained edge cue plus a readable name plate. Do not use color alone. The focused view should leave room for the service panel. Unity's [Deoccluder documentation](https://docs.unity3d.com/Packages/com.unity.cinemachine@3.1/manual/CinemachineDeoccluder.html) treats blocked sightlines as a camera-quality problem; that supports auditing occlusion, not automatically moving the camera every time a tree crosses the view.

7. **Keep transitions brief, optional and reversible.** Test an overview-to-service shot only after selection, never during hover. Prefer a simple travel direction with little or no camera rotation, return to the same overview, and allow instant transitions for reduced motion. Unity documents both [cuts and eased blends](https://docs.unity3d.com/Packages/com.unity.cinemachine@3.1/manual/CinemachineBlending.html); neither is a universal best choice. Persistent text needs a still, backed reading area. Keyboard focus should follow the visual arrangement and the same back action should work throughout.

## Offline comparison completed

Six private 1600×900 Cycles renders use the accepted packed `hamlet-grass-v07.blend`. A–E retain its assets, layout, material colors and lighting. F additionally tests background/value changes. Blender camera coordinates map to Unity as `(X, height, depth)` from Blender `(X, depth, height)`. All FOV values below are **vertical** at 16:9. Distance is in existing scene units; it has no transferable meaning for another game.

| Candidate | Down pitch / yaw / vertical FOV / distance | Observed result |
|---|---|---|
| A. Current | 18.75° / 1.62° / 42° / 112 | Familiar frontal terraces. Bright stone dominates; peak overlaps the Abbey tower. |
| B. Higher overlook | 25° / 4° / 34° / 143 | More ground and roofs. Exposes backdrop joins and crops the peak. Useful upper bound, not preferred default. |
| C. Planning view | 32° / 9° / 40° / 120 | Clear route inspection, weaker facade presence, more visible outer terrain edges. |
| D. Low cinematic | 15° / 3° / 30° / 155 | Compresses service layers and emphasizes wall height. Better starting point for a selected-service shot. |
| E. Modest overlook | 22° / 3° / 36° / 136 | Better road/roof separation with restrained perspective. Original peak still clips; camera alone does not fix scenery. |
| F. E + background/value blockout | Same as E | Mountain offset preserves its scale/depth, clears the Abbey silhouette and fits the peak. Quieter stone and continuous ridge placeholders test distance separation. Ridge paint, woodland silhouettes and a believable outward road remain production work. |

E/F use Unity camera position approximately `(20.60,63.95,-108.92)` and target `(14,13,17)`. This is an offline candidate, not a replacement setting. Test near 22 degrees/36 degrees first; adjust against actual HUD, full building bounds and small windows before adopting it.

The private review page compares A with every alternative, with optional grayscale and measured HUD occupied-area guides from the accepted native capture. The right roster begins at x1293 of1600; the footer at y799 of900. Guides also cover the title, town controls and left shortcuts. They are rectangles, not a native UI simulation. A representative open service panel and resized native views were **not** tested in this round.

Each view has ten clear projected doorway-apron proxies inside a conservative normalized safe rectangle, `0.055<x<0.79`, `0.14<y<0.88`. Raycasts check sightlines to those proxies. This is preliminary geometry evidence only: it does not establish full-building visibility, real picking, readable signs or correct UI dispatch.

F multiplies stone emission RGB by `(0.70,0.73,0.76)` without changing building materials. It moves the native mountain +28 units horizontally and -5 in height, retaining its depth and scale. Distant native scenery receives a shared depth palette; repeated camp treeline planes are hidden and two broad continuous terrain shoulders replace their banded composition. Ground blends towards the distant palette. The shoulders are deliberately untextured composition placeholders, **not final DD2-quality terrain or a finished backdrop**. No generated art is needed for this test. A proper follow-up should use painted ridge silhouettes and native woodland/rock details, with higher detail confined near town.

Private output: `local-art/ui-overhaul/hamlet-camera-study-v01/` contains `index.html`, six PNGs, packed `proposal.blend`, camera/door-proxy manifest, source hash, scripts and logs. The accepted source blend remains unchanged. No game launch, save access, runtime/config change, build or deployment took place.

Verification: six decoded 1600×900 renders and all60 door-proxy checks pass; the accepted blend's SHA256 matches the pre-study manifest. Baseline/E/F were inspected together in color and grayscale. The review page's actual script passes all five alternative choices, image/summary changes and HUD/grayscale toggles in Node with DOM stubs. Browser layout was not observed; Codex queued the file-opening request. See private `checks.json` and `review-sheet.jpg`. These checks verify the study, not native interaction or final art quality. F still has plain ridge surfaces, dark facades and a visible outer-ground transition; it is evidence for the proposed composition, not an accepted replacement landscape.

## Proposed iteration and acceptance

Prioritize the work in this order: (1) compose the modest overview with actual HUD; (2) connect the landscape and separate mountain/Abbey silhouettes; (3) establish facade/road/wall value hierarchy; (4) refine hover/name feedback and optional service views; (5) add local dressing, restrained smoke and ambient audio. Do not let new decoration compete with service destinations.

These acceptance checks are proposed for the later iteration:

- Every available service can be identified without panning, including at the smallest supported window size.
- An entrance and its approach remain visible beneath the expected HUD and largest intended text scale.
- The hovered building can be distinguished in grayscale and its name plate remains readable over both bright stone and dark trees.
- Silhouettes remain distinct when the image is reduced to a small thumbnail.
- The main road and terraces explain how the town connects, and no major landscape edge looks like a cut model base.
- Background haze separates distances without washing out the immediate buildings or hiding the mountain.
- Service views, return views and keyboard focus preserve a stable spatial relationship.
- Reduced-motion presentation works with no camera drift, pan, zoom or animated text backing.

Only a later interaction test can establish whether selecting services is easier. Static renders can establish framing and visual coherence, but cannot prove click accuracy, animation comfort or controller navigation quality.

## Implementation follow-up, round232

The owner authorized implementation. The modest overlook, Mountain offset, quieter stone, connected painted
valley/outward road, native woodland, backed names, keyboard focus and optional camera/smoke motion are now
installed. Native unsaved service/control/rollback/resize/keyboard/still-camera checks pass at1600x900 and during
1440x900 resize. See `tools/hamlet_3d.md` and private `local-art/ui-overhaul/hamlet-overlook-v08/native-overview.png`.
This verifies the implementation and inspected framing; easier selection, motion comfort, controller navigation,
other text/window sizes and saved-campaign interactions remain separate claims.
