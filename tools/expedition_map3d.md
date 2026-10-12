# Expedition map camera and native atmosphere

`Look.ExpeditionMap3D` enables the privately rendered DD2 valley. It stays off by default; the owner's local
choice is retained. The **3D map / Painted map** button switches immediately and stores its choice in config,
never the estate. Missing overview/layout files retain the painted selector. Missing effects retain a usable
map and camera.

Selecting a quest or clicking scenery now approaches the region through one persistent image. The old
overview-enlargement followed by a separate closer-frame fade is removed. No image is loaded, decoded or
replaced on a quest click. A critically damped camera keeps its current position and velocity when retargeted
mid-flight. Escape, right-click on scenery or **Back to the map** reverses toward the overview without clearing
the selected quest. Time is unscaled, bounded after a stalled frame, and the image always covers the canvas.

All region names, paired switches and quest rows keep their overview screen positions throughout movement.
They stay visible and clickable; selecting another quest retargets immediately. Scenery hit rectangles and
atmosphere use the camera transform. The dossier, roster, hero sheet and party/provisioner retain their existing
input and campaign rules. Region XP, quests and bosses remain independent.

## Private inputs

Files live in `Paths.ExpeditionArtFolder`, outside Git and releases:

| File | Purpose |
| --- | --- |
| `map3d.png` | Widescreen overview, no interface; currently 1920x1080 |
| `map3d.json` | Per legacy location: `hub`, `label`, `click`, `zoomScale`, `zoomHub`, `zoomLabel` |
| `vfx/vfx-manifest.json` | Bounded native atlas metadata and source provenance |
| `vfx/mist.png`, `fire.png`, `smoke.png`, `ember.png`, `gas.png` | Five private RGBA textures shared by six effect IDs |

`map3d-zoom-*.png` files from older packs are no longer loaded. They can remain privately for rollback.
Layout coordinates use the 1920x1080 UI canvas regardless of source-image resolution. `label` is the bottom
centre of the fixed quest row; `zoomHub` is the desired scenery destination. `zoomLabel` is read only for older
layout compatibility. The map viewport excludes the left dossier, right roster and lower party bar.

Locations pair Sprawl/Ruins at `crypts`, Foetor/Warrens at `warrens`, Tangle/Weald at `weald`, Shroud/Cove at
`cove`, and the Mountain at `darkestdungeon`. Switching to a DD1 destination retains the same valley scenery
and displays that area's own name and quests. Extra quests still scroll in rows of four, with DD1 length rings,
quest-type badges and selection glow.

## Native asset extraction

Run using the local universal-modder Python environment with UnityPy, Pillow and lz4:

```powershell
python tools/extract_expedition_vfx.py 'C:/Users/Piral/darkestwithdlc/game' 'C:/Users/Piral/DarkestDungeon3/local-art/expedition-vfx-v01'
```

The extractor rejects output beneath this Git checkout. It reads installed Addressables only, resolves bundle
dependencies, records hashes/material/particle evidence, and exports a bounded runtime pack. It never accesses
saves. Copy only the manifest and five top-level PNGs into the private `vfx` folder, after backing up the plugin
and selector pack and checking DD2 is stopped.

| Region | Source and map use |
| --- | --- |
| Sprawl | City `VFX_Fire_Smoke_Small_01.prefab`: 64-frame 8x8 fire at 30FPS, smoke at 20FPS, four spark variants; six localized fires/smoke plumes and twenty rising embers |
| Foetor | Farm `vfx_Steam_Smoke_Sphere_01_Prefab.prefab`: four static soft-wash cloud variants drifting/fading in native warm brown/pink |
| Shroud | Coast `vfx_coast_fog_plane_01.mat`: native fractal fog masked with the native half-oval gradient, drifting softly around the harbour |
| Tangle | Forest `vfx_particles_forest_color_smoke.prefab`: the Farm cloud sheet with native blue-green forest tint and restrained drifting particles |

Frame grids are top-left row-major; Unity GUI UV y is `1-(row+1)/rows`. Zero FPS means a static atlas variant,
not a stopped animation. Source material `_RGBAlpha=1` is approximated by copying the red mask into alpha;
fog also uses the native gradient. Exact names, path IDs, source bundle/texture hashes and native settings are
in the private manifest/evidence. No source textures or serialized game data belong in Git.

REA evidence for the matching installed `IronCrown.dll` establishes managed artifact identity only. Particle
settings come from shipped prefab/material serialization and texture inspection with UnityPy. No claim is
made that REA observed shader execution or that the native particle playback has been reproduced exactly.

## Ownership and limits

`Core/Presentation/ExpeditionMapCamera` owns camera motion; `ExpeditionMap3D` validates geometry;
`ExpeditionMapVfx` validates atlas metadata and selects frame UVs. `Runtime/ExpeditionMapEffects` reads files
off-thread, uploads at most one unique texture per frame on Unity's thread, and makes textures non-readable.
The six installed effect IDs share five images, approximately 13.25MiB decoded without mipmaps. Leaving the
map for provisioning, painted mode or another screen releases all five after three unused frames. Re-entry
reloads asynchronously. A discarded file-read task owns no Unity objects and cannot repopulate a later pack.

`Ui/ExpeditionMapAtmosphere` draws 52 deterministic quads under the quest controls, clipped to the scenery
viewport. It neither handles input nor consumes gameplay RNG. Native fire/smoke frames and native masks are
reused; particle trajectories, density, scaling, mist/smoke/ember tints and fading are adapted for the small map.
The overview already has baked haze/glow/motes, so added effects are deliberately restrained.

This remains a camera over a render, not a live perspective scene: close views can look softer, there is no
terrain parallax or particle depth occlusion, and IMGUI omits native HDR/bloom/distortion/depth fade. Removing
baked fog/sparks requires a clean overview re-render with atmosphere separated; the existing kit is enough.
No generated textures are needed for this pass. A later live map could use the owned-stage pattern in
`tools/hamlet_3d.md`; preserve its independent renderer and resource ownership.

## Verification and rollback

Core tests cover uninterrupted retarget/reversal, frame-rate consistency, invalid/stalled clocks, coverage,
layout validation and atlas playback. UI tests link the real effect loader and check shared-texture ownership,
one upload per frame, main-thread Unity calls, invalid dimensions, stale workers and cleanup/re-entry. The
test shim checks scheduling, not GPU rendering.

`-dd3-expedition-check` opens the existing unsaved preview and dispatches real IMGUI events without desktop
input. It requires the private map and all six effect IDs, then checks fixed actual quest rectangles, mid-flight
retarget/reversal, both paired-area directions, painted rollback/resource release/reload, provisioning cleanup
and return. It never selects a save path; Persist is guarded and real embark is blocked. Close the exact QA PID
and restore a normal launch without preview/check flags after recording the result. Native visual acceptance
must be recorded separately from offline tests.

Rollback: switch **Painted map**, set `Look.ExpeditionMap3D=false`, or restore the pre-change plugin/private
pack backup with DD2 stopped. No save migration or estate edit is involved.
