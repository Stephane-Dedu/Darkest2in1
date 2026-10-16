# Native room floors

Rooms use one full-frame 2880 x 1080 PNG. `RoomPanoramaUi` draws it once at
1920 x 720. The camera places the heroes' feet at y680. Private native rooms
retain their orientation, including on revisits, and do not consume gameplay RNG.

The previous room extension repeated a complete road texture every eight world
units. Some of those textures contain wheel tracks, skulls or a board atlas.
Repeating them across a broad plane produced a visible grid behind the arena.
This was baked into the room images, rather than caused by the runtime draw call.

`render_native_rooms.py` now keeps the native arena meshes and their original UVs,
but uses `room_floor.py` for the missing ground. Its regional paint sources are
Sprawl rubble, Foetor footpath, Tangle roots and Shroud worn boards. Three samples
have independent world periods and offsets. Broad irregular joins prevent their
distinctive marks from lining up. Arena-specific phases vary the floor without
changing the camera or stretching the completed image. Local geometry supplies
contact shading; distance haze removes the far plane's hard horizontal edge.
Noise controls joins and shading only. It supplies no replacement artwork.

From the owner's extracted native kit, render into a private directory outside
the repository, then compose the PNGs with Python/Pillow/NumPy:

```powershell
& $blender -b --threads 6 --python-exit-code 1 --python tools/render_native_rooms.py -- $kit $output
& $python tools/render_native_rooms.py $kit $output --compose-only
```

Use `--only city_resist farm_faction forest_faction coast_faction` to check one
scene per region first. Render completion records include source, camera and
renderer hashes, so interrupted or stale passes are not silently reused.
The manifest records every native source texture and the floor mapping periods.
Keep all extracted art, render passes, screenshots and install backups outside Git.

Validate all 32 PNGs against the loader's 8:3, 8-bit RGB/RGBA and 16 MiB limits.
Inspect full scenes and foreground crops, then check them through DD2's actual
room loader and draw path. The existing unsaved scenery preview can display all
eight rooms in each region with DD1 reference bodies at y680. Live DD2 actor
contact and real exploration/battle transitions need separate native acceptance.

Install only the 32 `dd2_<region>-arena-<role>-NN.png` files and their room
manifest while DD2 is stopped. Back up those targets and hash-check every other
scenery file, plugin artifact and config. A corridor pack must preserve the
currently installed rooms, rather than restore its own older room copies.

These are baked backgrounds. Native Unity lighting, animated water and arena VFX
are not reconstructed by this render tool. Room role chooses scenery, not the
original DD2 encounter or boss.
