# DD2 item map

Research snapshot, round 172, 2026-10-09. The requested per-item catalogue reads the current
installation, not the older exported tables under dd2-decomp/data. It supports the future
curio/shop/camping work in [overhaul_roadmap.md](overhaul_roadmap.md).

## Coverage and evidence

The scan of 553 CSV files found 615 Item records and 602 unique IDs:

| Native type | Records | Unique IDs | Adaptation work |
|---|---:|---:|---|
| combat | 85 | 85 | Native skill targets/effects, combat transfer and explicit new curio interactions |
| rest | 72 | 70 | Camping targets, grouped effects, use limits and expiry |
| currency | 109 | 109 | Currency/resource/quest roles; many are not usable supplies |
| stage_coach_upgrade | 118 | 107 | Equipment, pets and progression objects; separate from consumables |
| trinket | 211 | 211 | Equip conditions, effects, duration and existing trinket UI |
| memory | 20 | 20 | Native persistent hero bonuses, separate from the proposed memory curio |

Thirteen IDs have multiple records, including two rest items and eleven stagecoach/pet items.
The catalogue preserves every source variant. Folder/name scope is a hint, not runtime load
precedence or proof that a DLC is owned. Editor entries also remain visible.

Enumeration is complete for installed `Item` blocks in this snapshot. Individual behaviour
review is ongoing. Scene props, addressable art, localized names and the current run's unlocked
inventory are outside this scan. No save files or game processes are needed.

Private output on this machine:

`C:\Users\Piral\rea-workbench\evidence\round172\dd2-items-v2\`

- `index.html`: searchable item browser with type/use filters and expandable effect evidence.
- `catalogue.json`: all item fields, constructor bindings, skill payload bindings, loot ancestry,
  evidence records, unresolved references and source SHA-256 hashes.
- `items.csv`: one row per source definition with use contexts, targets, stack limits, costs,
  duration and quest identifiers. Blank fields mean unspecified in data, not zero or false.
- `summary.json`: counts for comparison after an installation update.

The snapshot retains 7,624 supporting records. All explicit active ItemDefinition bindings
resolve to a record of the expected kind. That check establishes data linkage, not successful
runtime application. Same-ID candidates and exact-cell links are visibly labelled as research
navigation. They may include unrelated records with the same ID.

## Rebuild privately

From the mod repository, with Python 3.10 or newer:

```powershell
python tools/map_dd2_items.py `
  --excel 'C:\Users\Piral\darkestwithdlc\game\Darkest Dungeon II_Data\StreamingAssets\Excel' `
  --output 'C:\Users\Piral\rea-workbench\evidence\dd2-items-NEW'
python -m unittest discover -s tools -p test_map_dd2_items.py -v
```

Choose a new or empty directory outside every Git repository and outside the installed tables.
Existing evidence is never overwritten. The committed files are the original extractor,
viewer template, synthetic tests and these authored notes. Native tables, generated catalogues,
decompiled code and art stay private and out of releases.

## Native contracts inspected

Private C# paths below are relative to `C:\Users\Piral\dd2-decomp\IronCrown`.
These are read-only semantic notes; the extractor does not copy decompiled implementation.

| Contract | Evidence | Consequence for the mod |
|---|---|---|
| Costs resolve separately from use behaviour | `Assets.Code.Item/ItemDefinition.cs`, constructor; `Assets.Code.Cost/CostDefinition.cs` | Retain currency and quantity. A cost alone does not mean a shop sells the item. |
| Main effects, limited effects and conditions may use a matching item ID | ItemDefinition constructor and FetchDefinitions | Explicit links alone miss valid effects. Preserve each effect list and duplicate values. |
| Pair and party effect groups have separate lists and limits | ItemDefinition; `Assets.Code.UI.Widgets/RestWidgetBhv.cs`, ApplyEffects/ApplyItemCombinationEffects/ApplyItemPartyEffects | Do not flatten an inn item into a single hero buff or drop adverse outcomes. |
| A native skill takes precedence over the item's direct skill-effect/external-buff fallback | ItemDefinition constructor | Skill-backed items need ActorDataSkill and its payloads, not just Item fields. |
| Skill stats/effects accept explicit IDs, otherwise use the skill ID | `Assets.Code.Skill/ActorDataSkill.cs`, constructor | Preserve rank, friendly/self targeting, conditions, cooldown and free-action fields alongside effects. |
| Inn use limits are per actor and count item-ID and tag history separately | `Assets.Code.Actor/ActorInstance.cs`, GetIsItemUnderUseLimit/RecordItemUseHistory | Stack size, possession limit and use budget are different rules. |
| Inn boundaries clear histories under their arrival/party conditions | ActorInstance.OnInnVisitStart/OnInnVisitEnd | Camping requires an explicit replacement reset boundary and persisted history. |
| Inn application affects selected heroes, each selected pair, then party effects | RestWidgetBhv.ApplyEffects | Multi-target cost and use-budget charging need deliberate handling. Chance modifiers may suppress effects. |
| Stores draw from configured loot tables | `Assets.Code.Map.Triggers/TriggerStoreBhv.cs`, GetLootIds/GetLootTableItems; StoreInventory | Store roots also live in serialized scene components. CSV ancestry alone cannot prove shop availability. |

The native item catalogue retains fields for conditions, forced/random targets, action skills,
use contexts, buy/sell visibility, discardability, possession/stack limits, slots, tags, duration,
quest IDs and upgrades. Full raw rows preserve empty columns and repeated entries. This matters
for parallel loot arrays and repeated limited-effect choices.

Loot entries preserve their quantity, weight, type, tag and condition at the same column index.
Parent-table traversal retains variants and handles cycles. A zero-weight entry remains visible
as evidence. Weights are not displayed as drop percentages. Conditions, unlocks, mode/DLC load
order and actual shop root selection still need evaluation before stocking the provisioner.

## Reviewed starter examples

These are summaries of installed base definitions and linked skills/effects, not new curio rules.
Target conditions and runtime modifiers still apply. See each catalogue entry for full evidence.

| Item ID | Native use/effect summary | Important distinction |
|---|---|---|
| bandages | Friendly combat item, one target; removes bleed and has a 5-HP heal effect | DD1 supply ID is `bandage`, singular; the native skill has a bleeding-target condition. |
| antivenom | Friendly combat item; removes blight and has a 5-HP heal effect | Same spelling as DD1 does not imply the same action or price. |
| medicinal_herbs | Friendly combat item; removes bleed, blight and burn | Review its status conditions; it is not a generic exploration antidote. |
| holy_water | Friendly combat item; removes negative tokens and combo | DD1 supply/quest holy water and DD2 combat holy water need separate identities. |
| laudanum | Friendly combat item; stress relief and horror removal | Target stress/horror conditions are part of usability. |
| smoke_bomb | Enemy-targeting combat item; applies blind tokens | A distraction curio interaction would be a new authored rule. |
| whiskey_barrel | Four-target inn item with stress, quirk and pair-affinity effects | Preserve adverse outcomes and repeated entries; not a pure party heal. |

The data review does not certify these effects in a live mod expedition. Per-item runtime checks
should accompany each implemented adapter rather than claiming the whole catalogue is playtested.

## Existing mod boundaries and proposed adapter

`Core/Dd2Data/Dd2Tables.cs` currently maps classes, skills, quirks and trinkets, not all items.
`Core/Expedition/Inventory.cs` loads DD1 inventory definitions and a fixed provisioner supply list.
`Core/Expedition/CurioResolver.cs` matches a curio's explicit item interaction and consumes from the
expedition pack. DD1 references are `inventory/base.supply.inventory.items.darkest`,
`curios/curio_type_library.csv` and the Unity port's `Raid/Contents/CurioInteraction.cs` and
`Raid/Props/Curio.cs`.

Before adding stock, define an item identity containing source and native ID. Preserve legacy
DD1 save IDs with a migration or adapter; new DD2 items must not overwrite same-name supplies.
Keep native effects and proposed expedition interactions in separate records. A proposed rule
should name its context, legal targets, item cost, outcome, duration, reward and persistence state.

Core should own pack quantities, shopping and curio/camp rules. The DD2 bridge should adapt
approved native effects and combat equipment, preserving unsupported effects as an explicit gap.
Use the existing item text/icon layer for presentation after checking coverage. Keep native
quest resources, pets, currencies and memories out of ordinary supply stock unless individually
designed for it. No production adapter or stock change is enabled by this research round.

## Next review slices

1. Review one medical supply end to end, including its native skill conditions and DD1 supply
   crosswalk; design one regional curio and a campaign price.
2. Trace chosen shop roots from serialized store components and evaluate unlock/DLC/mode filters.
3. Review one inn item with all target groups and expiry rules, then settle the camping cap scope.
4. Extend the review to every proposed stock item. Each row retains native evidence, the proposed
   interaction and a separate runtime verification state.

Enumeration does not commit the project to importing every native mechanic. The private catalogue
is the lookup for those decisions, and the roadmap retains the owner's larger feature ideas.
