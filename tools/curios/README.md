# Curio rework: design catalog v01

This is an art proposal for every curio identity found in the installed DD1 data, plus the mod's Faded Memory.
It does not install new art or change curio behavior. The private visual catalog is
`local-art/ui-overhaul/curio-design-v01/index.html`, beside this repository's parent directory.

## Coverage

| Scope | Entries | Meaning |
|---|---:|---|
| Base campaign | 60 | Ordinary, tutorial, secret-room and event curios in the base library |
| Quest objects | 10 | Additional gather/activation identities in the base quest definitions |
| Installed DLC definitions | 27 | Crimson Court and Color of Madness design coverage; runtime support is separate |
| Legacy art | 6 | Existing art folders without a matching live library/quest definition; no encounter invented |
| Owner exception | 1 | Faded Memory keeps its explicitly requested DD1 appearance |
| **Total** | **104** | Each identity has an original proposal and used-state description |

`author_designs.py` is the editable source; `designs.json` is its generated, asset-free manifest.
`catalog.py` independently inventories the installed data and rejects missing, extra or duplicate design IDs.
The private inventory records original sprite aliases, supply IDs and quest goals. Shared sprite aliases do
not merge behavior: Thanks Chest, tutorials and quest objects retain their separate identities.
Quest regions come from the actual goal lists; Animalistic Shrine belongs to Warrens/Foetor.

## Shared visual direction

Keep DD1's immediate object recognition and DD2's painted surfaces. At corridor scale, a chest must first read
as a chest, a tomb as a tomb, and an oyster as a shell. Use one dominant silhouette, one secondary detail and
small ground contact. Avoid surrounding every prop with crates, rocks and candles. Preserve a dark cavity,
a broad mid-value body and a few pale edges so the prop reads against both bright and dark scenery.

| Region family | Materials and shapes | Avoid |
|---|---|---|
| Sprawl / Ruins | Chipped devotional stone, scorched books, crooked timber, iron locks, dull brass | Full buildings resized into desktop props |
| Foetor / Warrens | Swollen casks, rough crate boards, butcher frames, stained cloth, dark meat | Replacing human remains with food props |
| Tangle / Weald | Bent roots, split bark, worn canvas, moss in joints, bound branch frames | Growth hiding the searchable opening |
| Shroud / Cove | Wet dark timber, salt-white crust, fish forms, carved marine stone, cool worn edges | The same circular fish shrine for every marine curio |
| Crimson Court | Courtier eggs, thin gilded trim, wine cloth, insect invasion of court furniture | Reusing the nurse as a finished Court NPC |
| Farmstead | Crooked farm furniture, restrained cyan/violet crystal fractures | Permanent bloom washing out native painted detail |
| Mountain / occult | Broken black stone, pale engraved edges, controlled red tendrils | Roadside Study buildings standing in for small altars |

Use regional material variations on the same ordinary chest/pack/crate families. Keep substantial identity
differences for coffins, oysters, statues, torture devices and people; those need new geometry. Fit props to the
current DD1 placement and party footline before adding depth. A coherent corridor matters more than the number
of decorative pieces.

## Interaction contract

Build separate `closed`, `active` and `open` presentations to match the current `CrawlUi.DrawProp` contract.
Lids, doors, flaps, shells and objective seals must separate cleanly for state changes. Idle motion belongs to
cloth, water or flame; solid masonry stays still. Hover can use a narrow warm edge and the native action glyph,
without a large glow or revealing a random outcome. Use a generous interaction mask around the principal body,
not around every dangling rope or transparent web thread.

The catalog proposes visual resolutions, not new outcome rules. Existing curio effects, supply consumption,
loot, journal entries, RNG, actor selection, quest progress and saved location state remain authoritative.
Different effects may need different art variants; never infer the effect from an animation or roll again for art.
Used props keep their anchor and footprint, making return travel readable. Keyed Court chests have distinct
plate shapes as well as ribbon colors. Faded Memory's spectral DD1 appearance and encounter rules are preserved;
its catalog sources describe only an optional secondary frame.

## Source audit and current limits

There are 39 source-reference groups with rendered previews and exact object/material provenance in the private
`audit.json`. Most textures are 2048 pixels; a few small props use 1024. Resolution alone does not prove a useful
model. The source audit checks geometry, static-batch face ranges, texture existence and dimensions, then renders
the chosen objects for visual inspection. It rejected or replaced these misleading candidates:

- `pickup_chest` is a two-triangle road icon. The design uses the actual 3D camp coffer.
- The old inn-scene table/knife references expose combined furniture geometry. Direct `inn_table.fbx` export
  provides separate table and dagger meshes.
- Guardian's object named `altar` is large shrine architecture. A compact Chirurgeon slab supplies altar-base
  proportions; the cultist/Shambler body, sigil and torch socket remain bespoke work.
- Hero Shrine is a chapel, not a statue. Statues use stone-material references and need sculpted bodies.
- A hanging-cage export used only black backing. A textured chain supplies metal reference; the iron maiden
  still needs a human-shaped shell, doors, hinges and spikes.
- Moss, banners, tendrils and the nurse are explicitly material/shape/pose references. They require new meshes
  or adapted sections. Their existing node placement and identity are not a finished curio.

Twelve native prop assemblies demonstrate scale and grouping: chest, bones, books, food cart, fountain, tent,
fish carcass, bookshelf, sacrificial slab, egg cluster, cargo crates and an occult altar base. They are source
studies, not completed sculptures. The catalog labels them accordingly. Offline renders use native base color
and tint through an emission preview shader; native lighting, cutout sorting, VFX, animation and picking remain
unverified. No game launch or save access is part of this design pass.

## Build order

1. **Ordinary searchable containers:** coffer, strongbox, chest, crate, sack and travel pack. Produce convincing
   interiors and closed/open variants once, then add tutorial, keyed, trapped, objective and regional fittings.
2. **Books, bones and abandoned camps:** native book piles/shelves, skeletons, tent and fire ring. These provide
   recognizable shapes with modest adaptation work.
3. **Altars and fountains:** a compact devotional base, sacrificial slab and basin family, with dedicated occult,
   cleansing and quest variants. Keep the Shambler socket bespoke.
4. **Distinct bodies:** tombs, urn, iron maiden, empty armor, effigy, fish idol, oyster, coral and figurehead. Sculpt
   each silhouette rather than stretching unrelated props.
5. **DLC sets and people:** Court furniture/eggs, Farmstead crystal props, Blood Shrews and Miller's Wife. New NPC
   bodies/rigs are a separate production task. Legacy-only designs wait for an actual encounter assignment.

Before production, review the complete catalog by region and the assembly sheet together. The first acceptance
batch should cover a locked chest, books, bones, fountain and tent in actual corridors at normal gameplay zoom,
with hover, supply drop, use, reload and return travel. Do not mark runtime acceptance from these offline images.

## Proposed implementation seam

Keep the existing curio rules and location IDs. Resolve presentation by exact curio ID, with an optional regional
variant and explicit shared-family references. Put new art behind one reversible feature flag with DD1 art as the
fallback for missing/unready assets. Do not infer identities from sprite aliases or introduce save migrations.
Keep loading/caching and disposal separate from gameplay resolution; a presentation lookup must not consume RNG.
Choose real 3D props or pre-rendered layered states after checking corridor perspective/occlusion. This catalog
does not make that runtime choice or add a feature flag yet.

## Reproduce locally

Use the local asset-pipeline Python (numpy, Pillow, UnityPy) and Blender. All exported models, textures, fonts,
images and generated catalogs stay outside Git. Only this authored manifest, tools and notes are versioned.

```powershell
$curioPython = '..\universal-modder\.venv\Scripts\python.exe'
$curioKit = '..\local-art\dd2-native-kit'
$curioOut = '..\local-art\ui-overhaul\curio-design-v01'
$curioGame = 'C:\Users\Piral\darkestwithdlc\game'
$curioBlender = 'C:\Program Files\Blender Foundation\Blender 5.2\blender.exe'

# Extra direct models absent from the initial kit. This writes only to the private output.
& $curioPython "$curioKit/tools/export_prefab.py" --game $curioGame "$curioOut/extra" `
  biome_common_assets_all.bundle,biome_common_models_platform_high_assets_all.bundle `
  chirurgeon_node_altar.fbx guardian_node_altar.fbx oasis_node_02_no_moss.fbx `
  the_inn_banner_01.fbx courtier_node_moss.fbx
& $curioPython "$curioKit/tools/export_prefab.py" --game $curioGame "$curioOut/extra" `
  biome_inn_assets_all.bundle inn_table.fbx

& $curioPython tools/curios/author_designs.py
& $curioPython tools/curios/catalog.py --game $curioGame --kit $curioKit --out $curioOut
& $curioBlender -b --python tools/curios/render_parts.py -- --kit $curioKit --out $curioOut
& $curioBlender -b --python tools/curios/render_studies.py -- --kit $curioKit --out $curioOut
# Rebuild HTML so the newly rendered studies appear.
& $curioPython tools/curios/catalog.py --game $curioGame --kit $curioKit --out $curioOut
& $curioPython tools/curios/review.py --out $curioOut
```

`render_parts.py --refresh key1 key2` refreshes selected images after source changes. `review.py` checks all 104
cards, local image targets, native chest/table/knife geometry and nonempty transparent renders. It creates
`source-sheet.jpg`, `assembly-studies.jpg` and `artifact-check.json` for the private review.
