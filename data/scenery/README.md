# Regional room backgrounds

Eight original room panoramas generated with Codex's built-in imagegen on 2026-10-05, at the owner's request.
Installed DD2 scenery served as palette, setting and rendering references. No extracted game textures are included.
The complete prompts, dimensions and SHA256 hashes are recorded in `manifest.json`.

| Region | Scene 01 | Scene 02 |
| --- | --- | --- |
| Sprawl (`dd2_city`) | Ruined civic courtyard | Burned market square |
| Foetor (`dd2_farm`) | Blighted farmstead | Diseased orchard |
| Tangle (`dd2_forest`) | Abandoned military camp | Ruined watchtower clearing |
| Shroud (`dd2_coast`) | Coastal fishing landing | Shipwreck and beacon cliff |

Images are opaque RGB PNGs, approximately 2048x768 (8:3); the first Foetor scene is 2046x768.
The build deploys these files into the plugin's `data/scenery` folder. Each expedition loads only its region's two
images. Room IDs and the saved expedition seed select a stable scene/orientation without advancing gameplay RNG.
Consecutive room IDs alternate scenes. Mirroring adds orientations; these are two distinct scenes per region,
not a unique generated image for every room.

The upper scene is painted behind the existing heroes and props. The lower ground blends into the native regional
road, tinted to the same palette. Open native corridors lead into rooms through the existing short fade; scenery
does not block movement or add doors. Missing room PNGs retain the native composition. Legacy DD1 regions and
native DD2 combat retain their existing presentation.

Generation and offline composition were inspected. In-game hero/prop scale, transitions, loading and memory still
require a DD2 session once the owner's standing no-launch instruction is lifted.
