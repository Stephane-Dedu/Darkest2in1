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
No AI-generated textures are used. The native roots and stubble in `roads/_tex/`
provide local ground detail. Native model paint and UVs are never repainted.

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
