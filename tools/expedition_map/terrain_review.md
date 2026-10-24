# Expedition terrain studies

The installed map is accepted339; the runtime remains335. Hamlet remains
parked333. Full checkpoint:
`local-art/ui-overhaul/expedition-scenery-mask-v05/CONTINUE.md`, outside the
repository. Heavy sources, renders, masks and prompts are on D:. The earlier
338 studies below remain rejected.

## Native source findings

Six complete Valley, Farm and Forest driving/node-framing groups were rendered
from inside and outside their original arrangements. Valley hills and Forest
fringes are mostly distant cards; Farm contains meat silhouettes. They provide
regional framing, but not the connected, detailed slopes missing from this map.

Four main-menu mountain variants were exported completely. The foreground
variants have painted bodies and inverted outline shells. The reference helper
renders both sides, so the shells obscure the body and produce a black preview.
Applying the exported material's `_Cull=2` setting with a back-face transparent
shader reveals the original paint while retaining both meshes and their UVs.
Excluding the outline gives a useful comparison, but removes native geometry.
This is an offline approximation, not observation of the native shader.

Two whole-map foreground-mountain studies retained source geometry/UVs, uniform
scale and paint. Both are rejected: the large placement competes with the
regions, and deeper burial leaves disconnected dark wedges. A separate native
Shroud bank test also exposes an awkward card edge. Do not install these images
or repeat those placements. The source geometry is valid; the compositions fail.

A fifth raw Common hill export remains incomplete because its mesh references
the Farm bundle. It is excluded from the studies. Do not count it as a complete
export or infer its placed prefab materials from the raw `env_black` assignment.

## Targeted painting study

After these comparisons, one built-in imagegen edit tested a connected painted
surface for the custom valley. The generated image is1672x941, not native4K.
It remains a draft. The original full edit changes scenery pixels and must never
replace the installed map directly.

A terrain-only pass and native-scenery holdout were rendered from the unchanged
335 scene. A private compositor fits the draft to the4K map only on selected
far slopes. It excludes the visible scenery holdout, a projected12m road band,
the original terrain silhouette and the foreground. Both composite studies
preserve the logical layout, every protected pixel and all pixels outside their
mask. About17% of pixels change. The output dimensions do not increase the
generated painting's actual detail.

Overview detail and palette are more promising than the scattered forms. Actual
camera zoom crops expose rectangular patches of retained soil beside some
scenery. Feathering reduces the edges but does not meet the quality bar. Pixel
retention alone does not establish a coherent visual result.

At the end of338, material/ground coverage and all five camera destinations
still needed review. No338 painting was accepted or installed.

## Accepted339 terrain crops

The padded scenery holdout preserved blocks of old soil beside objects. The
original map already contains the foreground once; blending it again through
foreground alpha also creates halos. The new compositor changes the terrain's
background contribution through the original, unpadded native coverage.

Two terrain-only imagegen crops supply connected rock and earth detail. Their
actual sizes are1403x1121 and1473x1068; the installed container is3840x2160.
Native scenery was a separate style reference. Opaque native pixels, road
pixels, outside-scope pixels and the logical layout remain exact. About19.66%
of map pixels change. All269 scenery placements, native geometry, UVs and paint
remain. Baked procedural motes may follow the painting; live VFX stay unchanged.

Five destination crops and native overview/city captures passed visual review.
The unsaved native map fixture passed fixed controls, retargeting, close views,
overview return, paired regions and effect/provisioner cleanup. No estate was
selected or saved. Its50.05-second recording mainly shows a stationary city and
late overview; fixture transition evidence comes from the log. A subsequent
native uninstall callback raised `KeyNotFoundException: IMainInstaller`. The
cause remains unresolved; this art pass changes no runtime code.

The public renderer reproduces all four raw reference passes pixel for pixel.
Their PNG byte hashes differ because Blender embeds timestamps and timings.
The public terrain compositor reproduces the accepted PNG byte for byte.
Normal plugin, configuration and native profile files were restored exactly
before accepting only the new map. DD2 is closed, with no pending restore.

This remains screen-space terrain painting, with approximate antialiased edge
reconstruction and no new terrain parallax. Never install the raw338 edit or
rerun the already-used335/339 restore scripts. The owner's later three-hour
launch restriction takes priority: no DD2 launch before2026-10-24 18:25:40 UTC
(20:25:40 Europe/Paris). Resume with a fresh usage and repository check.
