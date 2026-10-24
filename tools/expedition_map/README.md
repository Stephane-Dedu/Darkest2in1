# Native expedition valley

These recipes rebuild the private single-image expedition map from the owner's
extracted DD2 kit. They write no interface, game installation or save files.
Source geometry, UVs and base-colour images remain native. Terrain construction,
lighting, material brightness and atmosphere approximate a distant map view.

`render.py` uses Blender, NumPy and a Pillow build compatible with Blender's
embedded Python. Pass a private dependency directory with `--vendor` if needed.
`compose.py` uses ordinary Python with Pillow and NumPy. Keep outputs and
dependencies on a drive with space. The renderer uses float32 terrain fields
and thirty1024px tiles to bound memory. The final3840x2160 RGB PNG fits the
existing selector's4096x2304 and16MiB limits.

```powershell
blender -b --python-exit-code 1 --python tools/expedition_map/render.py -- `
  --kit D:/private/dd2-native-kit --out D:/private/expedition/render `
  --vendor D:/private/blender-python --scale 2 --samples 16
python tools/expedition_map/compose.py `
  --kit D:/private/dd2-native-kit --render D:/private/expedition/render `
  --layout C:/private/selector/map3d.json --out D:/private/expedition/pack
```

Use the currently installed logical layout as input. Its bytes are copied
unchanged, so quest anchors, hit areas and camera destinations remain stable.
`provenance.json` records the rendered passes, logical layout and recipe hashes.
The source kit, its own extraction provenance and all derived art stay private.
The base render/composer use no AI-generated textures. Accepted339 adds two
private terrain paintings through the separate protected compositor below.
The native roots and stubble in `roads/_tex/` provide local ground detail.
Native model paint and UVs are never repainted.

Compare the pack with the previous overview and native asset references,
including the pale Foetor farmhouse. Inspect it at the actual menu scale and at
the existing camera destinations. Verify output size and file limits before
installing with DD2 closed and after backing up the matching plugin/art pair.
Only `map3d.png` changes in the installed private selector pack. The existing
layout and VFX atlases retain their bytes; old zoom images remain unused.

Run the authorized unsaved `-dd3-expedition-check` fixture for native evidence.
It checks fixed quest controls, mid-flight retarget/reverse, paired regions,
effect release/reload and provisioning return. It selects no estate and guards
Persist. Record captures separately, close only its owned PID, restore the QA
baseline once, then install the accepted pair while closed. Never run a stale
restore over a later accepted installation.

The scene remains a flat render with a camera transform. It has no terrain
parallax or particle depth occlusion. Native HDR, distortion and fog shaders are
approximated. The four live smoke plumes, regional low mist and twelve embers
are bounded by the expedition UI; dungeon atmosphere shares the source atlases
but retains its own density and behaviour.

## Terrain painting without scenery halos

Add `--terrain-plates` to the full-resolution renderer to produce
`overview/terrain-only.png`, `terrain-depth.png`, `native-scenery-holdout.png`
and `roads-mask.png`. The normal beauty/depth passes retain the original scene.
The holdout uses the same camera, meshes, UVs, materials and samples, with only
the ground temporarily replaced by a holdout shader. No scenery padding is used.

Compose a second map with the terrain-only beauty/depth passes and the original
range/mountain passes. Use that empty plate as the paint target. Native scenery
is a supporting style reference, never part of the image being repainted.
Regional crops retain more real painting detail than enlarging a full-map edit.
Check their actual dimensions and compare every camera destination.

`terrain_paint.py --spec D:/private/paint.json --out D:/private/paint-pack`
fits the crops and applies their background delta through the native coverage.
It preserves opaque native scenery, the road mask and every outside-scope pixel.
It copies the logical layout unchanged and enforces the runtime PNG limit.
The spec has paths `base`, `old_terrain`, `coverage`, `terrain_alpha`,
`roads_mask`, `layout`, and a `paintings` list. Each painting has `side`
(`left` or `right`), `path`, and `rect` as source-pixel `[x0,y0,x1,y1]`.
Paths may be absolute or relative to the spec. Crops must not overlap.

This is display-space reconstruction of the background, not new3D geometry.
Antialiased edges approximate the final compositing; baked procedural motes can
follow the paint. Native live VFX remain separate. Compare native UI captures
before acceptance. Round339's two private paintings improve the connected
slopes while retaining the original scenery and the lower335 atmosphere.

## Native dossier paintings

`native_dossier.py --game C:/private/DD2 --out D:/private/dossier-study`
reads five small `biome_<region>_ui_assets_all.bundle` files. It extracts the
native `biome_bg_*` sprites and writes five candidate `preview-dd2_*.png` files.
Original657x1148 portrait references remain beside the provenance. The outputs
are657x219 RGB bands of those same pixels, with no resize, repaint or new fog.
The dossier draws them at360x120 with `ScaleAndCrop`; these3:1 bands fit exactly.
No art is installed by this tool. Existing output evidence is never overwritten.

Round340 compared ordinary map crops, dedicated native3D cluster views and
the original biome paintings at menu scale. The crops were crowded; isolated
clusters read as miniatures against a blank background. Those studies remain
rejected. The biome paintings are pending native UI review after the owner's
launch pause. Their narrow bands preserve native brushwork, but omit much of
the original portrait composition. Mountain and DD1 thumbnails retain.
