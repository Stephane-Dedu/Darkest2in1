# DD1 parity checklist

One line per DD1 system or behaviour. Combat itself stays DD2's by design: "parity" means DD1's systems around it.
DD1 paths are relative to `C:\Program Files (x86)\Steam\steamapps\common\DarkestDungeon`.

Legend: `[x]` done and seen working in game · `[ ]` missing or different from DD1 (what differs) · `[?]` built, not
yet verified in game (what to look at) · `[user]` needs a decision from the user · `[blocked]` with the reason.

Built 2026-10-03 (loop round 0) from DD1's data (`shared/rules.json`, `campaign/`, `dungeons/`, `raid/`, `props/`,
`curios/`, `loot/`, `trinkets/`, `upgrades/`, `localization/`) and MODLOG's open items.

## Hamlet & buildings
- [x] Hamlet opens from DD2's main menu (estate picker, saves per slot) — playtest 1.
- [?] Town scene: `campaign/town/town.layout.darkest` positions, building Spine skeletons at their upgrade level, hover highlight — look at building art/levels.
- [?] Building windows: DD1 backgrounds, keeper art, nameplate, upgrade button + panel (`building.layout`, `upgrade.layout`, `building_verbose_*`) — check text fits.
- [?] Upgrades: trees and costs from `upgrades/buildings/*.upgrades.json`, heirloom costs, % upgraded.
- [?] Stagecoach: recruits (`stage_coach.building.json`: recruit count, experienced recruits), roster size, drag to roster.
- [?] Abbey / Tavern: activities, slots, prices, side effects (`abbey.building.json`, `tavern.building.json`: gambling loss, missing, quirks).
- [?] Sanitarium: quirk treatment / lock, disease cure (`sanitarium.building.json`).
- [?] Blacksmith: DD1 window (port layout), per-class equipment trees (`upgrades/heroes/<class>.upgrades.json`) as DD2 buffs.
- [ ] Guild window: still our own layout; DD1's (guild.layout + port's GuildHeroWindow: hero slot, verbose frame, skill rows with requirement icons/cost frames) like the new Blacksmith.
- [?] Survivalist: camping skill purchase grid (`camping_trainer.building.json`).
- [?] Nomad Wagon: stock by rarity table (`nomad_wagon.building.json` rarity_generation_table), prices, buy.
- [ ] Graveyard: not DD1's look (DD1: `buildings/graveyard` list of the dead with cause of death and week, scrollable).
- [ ] Memorial (statue): DD1's journal/portraits page (`buildings/statue`: boss portraits, backer journal) missing.
- [?] Heirloom exchange (`campaign/heirloom_exchange`): rates, window.
- [?] Estate bar: gold/heirlooms at DD1 positions, nav buttons (realm inventory, activity log, town event).
- [ ] Activity log: not DD1's look (DD1: `activity_log/*` week bars, entry backdrops for hero activity, level-up, upgrades, raid success/failure banners).
- [?] End of week: idle stress relief, activity returns, missing heroes, new recruits/wagon/quests.

## Roster, quirks, diseases
- [?] Hero resolve levels from `roster.variables.json` thresholds; resolve XP per quest length.
- [ ] Quirk limits: DD1 caps 5 positive / 5 negative / 3 diseases / 3 locked each (`rules.json` quirks_max_*) — not enforced (no reference in code).
- [ ] Negative quirk auto-lock: DD1 `quirk_chance_to_lock_negative` / `quirk_negative_locked_after_turn_count` — not done.
- [ ] Disease after a quest: DD1 `disease_after_quest_min_chance`, `disease_max_chance`, `disease_after_quest_min_resolve_level` — not rolled.
- [ ] Dismissing a hero: DD1 `dismissed_hero_stress_penalties` (other heroes take stress) — not applied.
- [?] Quirk gain/loss after quests and from curios (DD1 quirk library → DD2 quirk ids).
- [?] Recruits arrive with DD1-style quirks (bug: DD2 quirk library empty at the menu — fixed? check new recruits have quirks).

## Trinkets
- [?] Trinket Inventory: DD1 window (sort, unequip all, shift-sell at 15%, grid, tooltips with effects).
- [?] Equip on the hero sheet: two slots, class-only trinkets refused.
- [ ] Trinket retention on a party wipe: DD1 keeps/loses carried trinkets per option (`trinket_retention` log entry) — not handled.
- [ ] Trinket-equip warning before embarking: DD1 `trinkets_equipped_warning_min_percent` / `_dungeon_min_difficulty` — missing.

## Quest board
- [?] Quests per zone from `campaign/quest/quest.generation.json` tables, lengths, difficulties, rewards.
- [?] Every quest goal finishable (gather / activate / cleanse / explore / kill boss) — EveryDd1QuestCanBeFinished test; check in game.
- [?] Boss plot quests at zone levels 2/4/6; the Darkest Dungeon chain from level 6.
- [?] Resolve bands refuse quests (MaxResolveFor), DD needs resolve 5.

## Provisioning
- [?] Provisioner stock and prices by quest length (`campaign/provision/provision.json`), class-specific free items, drag to buy.
- [ ] `provision_hp_heal`: eating food out of combat heals 5% HP in DD1 — check the inventory "use food" heal.
- [?] Low-food / no-torch embark warnings.

## Map generation
- [?] Room/corridor maps from `dungeons/<zone>/<zone>.dungeon.json` + `maps/` sizes; quest curio placement; boss room.
- [?] Corridor contents (battle/trap/obstacle/curio/hunger) distribution per zone.

## Crawl
- [x] Walking the DD1 hallway with DD1 art; hunger checks through DD2 actors — playtests 1-3.
- [?] Light: 6 per new square / 1 per visited (`tile_light_loss`), torch +25, light bands (`darkness`): scouting, surprise, loot, stress.
- [ ] Scouting on entering the dungeon: DD1 `scouting_enter_dungeon_scout_chance` (+ `_quest_item_scout_chance`) — missing; only room-entry scouting.
- [ ] Scouting treasure: DD1 `scouting_chance_scout_treasure` reveals curios — check.
- [?] Hallway stress per step (`hallway_stress`), starvation (`hallway_hunger_starve_HPdmg`), meals (`meals_table`).
- [?] Traps: DD1 disarm chance (class trap stat + 40% spotted − difficulty), spotted traps block, trap effects (`props/trap_definitions.json`), sounds.
- [?] Obstacles: shovel clears; without one, DD1 damages and stresses the party and drops light.
- [?] Curios: DD1 interaction (click, drag any item, right item works), results (`curios/curio_type_library.csv`), quest curios.
- [?] Corridor fights stop the party; room fights on entry; retreating backs off one square.
- [?] Map (minimap) drag/click travel, room exits by keys.
- [ ] Abandoning the expedition from the map: DD1 gives every hero stress on retreat-from-quest — check rule and button.

## Camping
- [?] Camp with firewood: respite points (`camp_start_camping_points`), DD1 camping skills per class, meal, torch restore (`camp_restore_torch`).
- [?] Night ambush (`ambush_camping_base_chance`); DD1 `ambush_torch_reduction` (torch drops on ambush) missing from code.

## Fights (around DD2 combat)
- [x] Fights launch in DD2 combat with our party and DD1 encounter translations; return to the crawl — playtests.
- [x] DD1 monsters drawn over their DD2 stand-ins (user saw them; HP bars/blight issues reported then fixed).
- [?] DD1 monsters fight with their DD1 skills (generated DD2 skills: ranks, damage, crit, effects) — new; check fights run.
- [?] DD1 scene behind fights (no DD2 arena, fog/DoF/blur held off).
- [?] Surprise: DD1 chances by room/corridor (`surprise_*_base_chance`); "known" (scouted) variants not used.
- [ ] Surprise when the room/corridor was scouted: DD1 `surprise_known_*` chances — not used.
- [?] Corpses: no corpse after a crit or DoT kill (CorpseRule).
- [?] Combat retreat: 70% + 5% per try (`combat_retreat_chance`, `_bonus_chance_per_attempt`), one try per round.
- [ ] Retreat stress: DD1 `combat_retreat_stress` on success — check what the retreat applies.
- [?] Torch level carried into DD2's torch (DD2's own darkness effects stand in for DD1's monster bonuses).
- [?] Battle loot from DD1 encounter `loot:` codes; DD2 loot skipped.
- [?] Camp/town buffs carried into fights as DD2 buffs.

## Afflictions, virtues, deaths
- [ ] Afflictions/virtues: DD1 resolve test at 100 stress (`affliction_*`, virtue 25%, `affliction_severity_table`) — mod uses DD2's meltdown at 10. Needs a design (DD2 has no afflictions).
- [?] Heart attack at 200 stress → DD2 meltdown/death's door stands in.
- [?] Death's door and deathblow from DD2; the dead go to the graveyard with cause/week.
- [ ] Death stress on the party: DD1 `death_party_stress_chance/damage` — DD2's own death stress applies? check.

## Loot & homecoming
- [?] DD1 results screen (`raid_results`): quest completed/abandoned backgrounds, heroes, items.
- [?] Gold/heirlooms home, gems sold, trinkets kept, quest rewards, resolve XP, zone XP and levels.
- [ ] Inventory value cap: DD1 stack sizes and 16-slot pack with "drop items" when full on loot — check loot overflow prompt.

## Town events
- [?] Weekly town events (`campaign/town_events`): crier panel, effects on buildings/prices/recruits.
- [ ] Plot, arena and returning-dead events not rolled.

## Plot quests & the Darkest Dungeon
- [?] Boss quests per zone; DD1 bosses → DD2 boss configs (zones.json).
- [?] Darkest Dungeon quest chain (`campaign/quest/plot`), DD art per quest folder, resolve 5 gate.
- [ ] Heroes who finish a DD quest refuse to go again (DD1 `hero_final_dd` activity log entry) — check rule.
- [ ] Town background after DD quests: `campaign/town/town_bg_post_dd_1..3.png` not used.

## Audio
- [x] DD1 music/ambience/SFX play from the FSB5 banks (user commented on volumes; lowered).
- [?] Footsteps per zone, curio/trap/door/loot/UI sounds, building sounds.

## UI look
- [?] Crawl HUD: DD1 hallway, hero row, stat column, inventory bag tab (672,252), minimap.
- [?] Hero sheet (`shared/character`), right-click everywhere.
- [?] Fonts at DD1's native sizes (nothing overflows).
- [?] Quest select / provisioner / loot scroll / camp screens.
- [?] Corridor heroes as DD2 3D models in the combat pose (ActorBhv.Show), portrait fallback.
- [user] DD1 game modes (Radiant / Darkest / Stygian: `modes/`, new_game_plus_* rules) — which, if any.
- [user] DD1 DLC content (Crimson Court, Color of Madness, Shieldbreaker: `dlc/`) — in scope or not.
