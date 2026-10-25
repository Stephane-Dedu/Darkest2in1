# Trinket selection and descriptions

The Nomad Wagon uses a four-column stock grid and a fixed reading panel. Clicking
stock selects an item; only Purchase spends gold. The existing Hamlet price and
purchase rules remain authoritative, including discounts and the final balance
check. Unaffordable stock remains inspectable. Selection follows the item ID when
stock changes, retains duplicate copies and clears when sold out or changing estate.

The panel uses DD2's addressable `ui_trinket_slot`, ability selection frame,
`ui_tooltip` nine-slice, divider and button banner. No new private artwork is
required. It shares Dunkel headings, Alegreya body text, DD2 rarity colours and
the regular/large native tooltip preference with equipment tooltips.

Effect text keeps the source wording and order. Trigger labels before a colon
occupy their own line, with the effect indented below. Numeric ratios remain
intact; signs do not determine benefit/harm colours. Long descriptions scroll
inside the fixed panel without shrinking the effect font. The item title, price
and purchase button stay visible. Selecting different content resets the scroll.
Inventory and equipped-item hover tips anchor beside their slots rather than
tracking the pointer. Classic trinket descriptions and restrictions remain supported.

Round352 validation: isolated clean HEAD2d865af plus the eleven owned source/test
inputs, Release build and all296 UI tests pass. DD2 stayed closed under the owner's
instruction. Native rasterization, large-tooltip appearance, mouse/controller
interaction, purchase feedback and smaller-window acceptance remain unverified.
Private receipt/checkpoint: `local-art/trinket-ui-v01/` and
`D:/CodexArtifacts/DarkestDungeon3/trinket-ui-v01/`. No save access or art changes.
