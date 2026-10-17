# DD2 typography

Round257 starts with trinkets and makes DD2's installed fonts the shared text default.
No font bytes, extracted sprites or native prefab data belong in Git.

| Role | Installed face | Size at 1080p |
|---|---|---|
| Headings and item names | NDDunkelD-Bold | Explicit layout size; item title 120% of tooltip body |
| Body and effects | AlegreyaSans-Regular | Tooltip 24px, or 30px with DD2's larger-tooltip option |
| Item interaction hint | AlegreyaSans-Regular | 80% of tooltip body |
| Trinket Inventory title | NDDunkelD-Bold | 34px |

`ExpeditionSkin.NativeFont` holds addressable font handles, waits for the native
catalogue and loads type independently of the scenery/node art. Styles refresh their font
after asynchronous readiness. Do not synthesize bold over the native bold face.
`Gui.Text` and `TextHeight` use the same native style inside and outside campaign panels.
The old DD1 bitmap line-height multipliers no longer apply. Short fixed rows fit by width;
prose wraps. Activity/memorial caches track the actual body-font identity, and path effects
measure the font they draw with. Hero names also measure the native heading face.

The native `TooltipCanvasElement` prefab sets regular 24px / large 30px, 40px background padding,
no automatic font shrinking and no added character/line spacing. Native `ItemDescription`
uses Dunkel at 120% for titles and 80% for interaction text. The trinket renderer applies
those sizes, 20px per-side padding and the installed `ui_tooltip` frame. Nine-slicing retains
its authored borders with the native 200px-per-unit sprite against the default 100px canvas
reference. Colours come from the installed `Localization/Sources/colors.txt`, including
item name/effect/type/hint and DD2 rarity colours. DD1 memory rarity tiers retain their data.

Tooltips grow from 460 to 640px before using a clipped, wheel-scrollable viewport. The body
and title never shrink to fit a long description. Scroll is captured before underlying
inventory scroll views, at the item's hover point. New content resets scroll; duplicate
local/shared tooltip passes in one frame retain it. Localized item names strip presentation
tags before appearing in headings, sorting or sell confirmation.

Private evidence is in `local-art/typography-v01`: exact bundle/prefab metadata and hashes,
font source references, 191 installed trinket descriptions, 382 regular/large Pillow layout
checks, a contact sheet, normal-source manifest and fresh installed backup. The layout checks
report no horizontal overflow; they are an offline font-metrics approximation, not Unity
rendering. Geometry tests cover screen corners, long-content scrolling and nine-slice atlas
orientation/border thickness. Full Release, 1,250 Core and 236 UI checks pass.

Native visual acceptance remains `[?]`. This uses DD2's source fonts through IMGUI, which
does not reproduce TMP's SDF material or inline token sprites. Check actual rasterization,
hover/scroll/large-tooltip option, smaller window scaling, localized fallback and the full
campaign/expedition layout when the owner permits DD2 to launch. The current instruction is
to keep DD2 closed until an explicit OK. No launch, input or estate access in this round.
