# Private Hamlet artwork

The optional live perspective renderer is documented in `tools/hamlet_3d.md`. Its feature flag defaults off;
this painted pack remains the default town and the immediate **Painted town** rollback.

The Hamlet accepts the owner's approved DD1/DD2 hillside v05 art as a private pack. The default folder is
`<DD2>/PrivateHamletArt`; `Paths.HamletArtFolder` in the BepInEx config can select another folder. An empty
setting or a missing/rejected pack retains the installed DD1 town. Restart DD2 after replacing a pack.

Prepare it from the private draft folder with Python + Pillow:

```powershell
python tools/prepare_hamlet_art.py C:/Users/Piral/DarkestDungeon3/local-art/ui-overhaul C:/Users/Piral/darkestwithdlc/game/PrivateHamletArt
```

The script uses the clean scene, `hamlet-hillside-render-v05/final-anchors.json` and the ten service buildings'
visible masks. It crops masks, emits raw top-down alpha for CPU picking and PNGs for locked/hover feedback,
then writes the manifest. The mock UI, annotations and obsolete v03 upgrade variants are not installed.
The script refuses a destination inside this Git repository. Do not distribute the derived game artwork.

`manifest.json` version 1 describes a 1920x1080 plate and all ten service IDs. Each building has a cropped
rectangle, depth order and relative paths to its `.mask`, `-tint.png` and `-outline.png`. Raw masks contain
one byte per pixel, row-major from the top left. Picking uses alpha >=48 and reverses the depth order;
occluded portions are already removed by the compositor. Every service, valid bounds and exact mask byte
counts are required. Missing files, duplicate/unknown services, path escapes or incompatible canvases reject
the pack together. PNG decoding and texture-size failures also keep the original town.

Session reads the manifest/masks on its existing data worker. PNGs join the campaign loading queue, which
reads files off-thread and uploads at most one texture per frame. The torch waits for that queue. Both UI
paths use the same existing building dispatch, unlock rules, services, roster, inventory and campaign data.
The hybrid exterior dims locked buildings; unlocked buildings share the approved v05 appearance. Separate
upgrade-level exterior paintings and plot/event sky paintings are not present in this pack. No campaign
progress or town-event rules are changed. The footer moves Embark below the buildings.

For native visual checks, explicitly launch `-dd3-hamlet-preview` with debug keys enabled. It creates a
transient estate without selecting a save path and suppresses persistence only for that object. It can open
building windows and the existing unsaved expedition preview; that selector blocks real embark. Close
preview returns to DD2's menu. Do not open the ordinary estate picker in automated checks.

Adding `-dd3-hamlet-check` dispatches IMGUI events inside the transient overlay to click real visible-mask
pixels for all ten services, close each window, then press the footer Embark button. It logs each result.
It sends no desktop keys/mouse and needs no real campaign. Capture the native window with `um win shot`.
After testing, close the owned test PID and relaunch without either flag for ordinary play.

Private implementation evidence is under `local-art/ui-overhaul/hamlet-native-v05/`. Source art, generated
cutout prompts and per-building masks remain alongside the v05 compositor outside Git.
