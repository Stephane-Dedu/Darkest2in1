# Expedition terrain studies

The installed map is accepted335. Round338 produced private studies, with no
deployment or game launch. Hamlet remains parked333. Full checkpoint:
`local-art/ui-overhaul/expedition-native-landforms-v04/CONTINUE.md`, outside
the repository. Heavy sources, renders, masks and prompts are on D:.

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

Next work should inspect the holdout's material and ground/card footprints,
separating useful scenery coverage from incidental ground before changing the
blend. Compare at all five camera destinations. Do not enlarge masks or add fog
to conceal joins. If the paint direction survives this review, a higher-detail
crop treatment and authorized unsaved native QA are still required. No new
painting has been accepted, installed, or seen in game.
