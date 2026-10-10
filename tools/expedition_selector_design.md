# Approved expedition selector

The owner approved `expedition-selector-v02.png` on 2026-10-10 and requested that its art prompts be retained for later implementations.

## References and prompts

- Approved local reference: `C:\Users\Piral\DarkestDungeon3\local-art\ui-overhaul\expedition-selector-v02.png`.
- Exact original prompt and first correction: [art-prompts/expedition-selector-original.txt](art-prompts/expedition-selector-original.txt).
- Exact approved readability revision: [art-prompts/expedition-selector-readability.txt](art-prompts/expedition-selector-readability.txt).
- Built-in ImageGen produced the artwork. The reference images and paintings remain local, outside Git.

## Layout and behavior

Keep DD1's single expedition screen: quest details on the left, a connected regional landscape in the center, the hero roster on the right, and the four party slots below. The Mountain replaces the upper Darkest Dungeon landmark visually. Its existing quest IDs and campaign requirements remain intact.

Keep the current region switches beside each regional name. Sprawl/Ruins, Foetor/Warrens, Tangle/Weald and Shroud/Cove occupy the same respective hubs. Enabled extra areas continue through those switches. Change local artwork with the selected area; retain independent quests, XP, bosses and progress. Quest buttons remain directly beneath their area. Preserve scrolling, drag/drop, hero sheets, resolve restrictions and the separate provisioner step.

## Reusable art-direction prompt

Create a cohesive Darkest Dungeon II-style angular ink and gouache environment for a Darkest Dungeon I-based interface. Use fractured black silhouettes, sharp planar shading, restrained painted texture, smoky depth, bone text, charcoal panels and worn bronze accents. Keep the original screen composition and controls. Give each destination one recognizable landmark and its own restrained regional palette. Blend regions through terrain and mist into one landscape.

Use thin, open branch framing and a restrained charcoal vignette. Do not let solid black borders dominate the screen. Preserve dark panel interiors for readable text. In nearer regions, prioritize large terrain shapes, clear paths and recognizable architecture over tiny rocks, twigs, planks and rubble. Retain selected medium details and ink texture. Keep quiet space around labels and quest medallions. Do not use global blur or brighten the whole image.

For this map: a massive red-lit Mountain at the top; burning Gothic Sprawl above the golden Foetor farm; a bannered Tangle fort in the lower teal woodland; and misty Shroud docks on the blue coast to the right. The Sprawl and Mountain detail level is approved. The nearer Foetor, Tangle and shoreline need the calmer treatment described above.

When producing runtime backgrounds, omit all text, portraits, buttons, quest icons and panel frames. Draw those as live game controls. Use the approved mockup as an image reference and preserve landmark placement during edits.

## Implementation

Load the private art pack from `PrivateExpeditionArt` beside the game, or the configured `Paths.ExpeditionArtFolder`. Missing art retains the existing DD1 selector. Keep the artwork outside the public repository. The preparation tool builds registered soft-edged regional overlays and quest previews from the locally generated paintings.

This approval covers the selector's presentation. The Mountain gameplay overhaul in `overhaul_roadmap.md` is a separate design.

## DD2 destination cards (preview, round 200)

An alternative quest select, switched in the Hamlet's Regions panel or with the top-left link on either quest
screen. It borrows DD2's innkeeper region choice: one card per map position with DD2's own region painting, and
the region's quests where DD2 shows its modifier. The Mountain is a banner above the cards. Paired areas keep the
arrow beside the name. Quest details stay on the left, the roster on the right, the party below. The painted map
above remains the default.
