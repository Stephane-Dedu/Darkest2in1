# Pre-rendered 3D expedition map

`Look.ExpeditionMap3D` is an experimental presentation flag, off by default. With it on, the quest selector
draws a private pre-rendered overview of a valley built from installed DD2 scenery in place of the painted map,
and clicking a region's scenery, or one of its quest markers, flies toward it. The selector's **3D map / Painted map** button (top right of the
map, shown only when the pack is usable) switches immediately; the choice persists in the mod config, not the
estate. A missing image, a missing or invalid `map3d.json`, or the flag off all retain the painted map.

This is a first implementation with still frames, not a live scene: the overview image grows about the region's
ground point and cross-fades into that region's own closer frame over 0.6 s. The frames are rendered outside
the game with the same camera direction, so the move reads as an approach. A live perspective stage in the
manner of `Dd2/HamletStage` is the possible next step; see `tools/hamlet_3d.md` for that pattern.

## Pack (in `Paths.ExpeditionArtFolder`, beside `map.png`; private, never committed)
| File | Purpose |
|---|---|
| `map3d.png` | 1920x1080 overview, no interface |
| `map3d-zoom-<location>.png` | closer frame per map position: `crypts`, `warrens`, `weald`, `cove`, `darkestdungeon` |
| `map3d.json` | per position: `hub`, `label`, `click` (x, y, w, h), `zoomScale`, `zoomHub`, `zoomLabel` in 1920x1080 pixels |

Positions are keyed by the legacy map location, as the paired regions are (Sprawl/Ruins at `crypts`, Foetor/Warrens
at `warrens`, Tangle/Weald at `weald`, Shroud/Cove at `cove`). The scenery is always the DD2 region's; a DD1
area selected with the switch arrow shows its own name and quests over the same scenery, without an overlay.
`label` is the top-left the selector passes to its ordinary name plate, so level, XP rule, switch arrow, lock
text and quest markers are unchanged. `click` rectangles are registered after the plates and markers and must
stay inside the map (x 535..1530, y 115..825) so they cannot take clicks from the dossier, roster or party bar.
Name-plate and click positions can be tuned in the JSON without a rebuild.

`Core/Presentation/ExpeditionMap3D` parses and validates the layout and owns the move's geometry (tested in
`ExpeditionMap3DTests`): geometric growth, eased travel of the ground point, and a clamp that never uncovers the
screen. `Runtime/ExpeditionMapArt` loads the images with the existing size and aspect checks. `Ui/EmbarkUi.Map3D`
draws the background, the mode switch, the plates and the hotspots. Zoomed, only that position's plate shows;
**Back to the map**, Escape or a right-click on the scenery flies back out. Closer frames are decoded one per
repaint while the overview is up.

The pack is built privately by `local-art/ui-overhaul/expedition_map3d_scene_v06.py` (Blender, from the DD2
native kit) and `expedition_map3d_pack_v06.py`; scripts, renders and images stay outside Git and releases.
For QA use the existing `-dd3-expedition-preview` fixture: there the switch changes only an in-memory choice.
