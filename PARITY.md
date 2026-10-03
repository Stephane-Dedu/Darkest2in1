# DD1 parity checklist

One line per DD1 system or behaviour. Combat itself stays DD2's by design: "parity" means DD1's systems around it.
DD1 paths are relative to `C:\Program Files (x86)\Steam\steamapps\common\DarkestDungeon`.

Legend: `[x]` done and seen working in game · `[ ]` missing or different from DD1 (what differs) · `[?]` built, not
yet verified in game (what to look at) · `[user]` needs a decision from the user · `[blocked]` with the reason.

Built 2026-10-03 (loop round 0) from DD1's data (`shared/rules.json`, `campaign/`, `dungeons/`, `raid/`, `props/`,
`curios/`, `loot/`, `trinkets/`, `upgrades/`, `localization/`) and MODLOG's open items.

## Hamlet & buildings
- [x] Hamlet opens from DD2's main menu (estate picker, saves per slot) — playtest 1.
- [x] Town scene: `campaign/town/town.layout.darkest` positions, building Spine skeletons, roster, estate bar — round 1 screenshot (Estate 2, week 49).
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
- [?] Quirk limits: DD1 caps 5 positive / 5 negative / 3 diseases (`rules.json` quirks_max_*); over the cap a new quirk replaces a random unlocked one of its kind (`shared/character/quirkreplaced.png`), none gained if all are locked. Was: no cap. Now QuirkLimits (Dd1Campaign.QuirkLimits) in town activities and on the DD2 actor for curio quirks (QuirkLimitTests). In game: a capped hero gaining a quirk logs "[quirks] ... replaces ... (DD1 quirk limit)".
- [?] Locked quirk cap: DD1 `quirks_max_locked_positive` 3. Was: the Sanitarium locked any number. Now Hamlet.WhyCantLock refuses a fourth (HamletTests.AtMostThreeLockedPositiveQuirks); the quirk row greys out with the reason on hover. In game: a hero with 3 locked quirks can't lock a fourth. (`_negative` 3 belongs to the negative auto-lock rule, still missing.)
- [blocked] Negative quirk auto-lock: DD1 `quirk_chance_to_lock_negative` 0.25 / `quirk_negative_locked_after_turn_count` 2 — when the roll happens and what the "turn" counts is in darkest.exe, not in the data or the Unity port.
- [blocked] Disease after a quest: DD1 has the numbers (`disease_after_quest_min_chance` 0.05, `disease_max_chance` 0.32, `disease_hero_disease_resist_weight` 0.33, from resolve 2) but not the formula; it is in darkest.exe, not in the data or the Unity port.
- [blocked] Dismissing a hero: DD1 `dismissed_hero_stress_penalties` [{upper_level 4: 5}, {12: 10}, {1000000: 20}] — what "upper_level" counts (resolve? weeks? roster size?) isn't in the data or the Unity port.
- [?] Quirk gain/loss after quests and from curios (DD1 quirk library → DD2 quirk ids).
- [?] Recruits arrive with DD1-style quirks (bug: DD2 quirk library empty at the menu — fixed? check new recruits have quirks).

## Trinkets
- [?] Trinket Inventory: DD1 window (sort, unequip all, shift-sell at 15%, grid, tooltips with effects).
- [?] Equip on the hero sheet: two slots, class-only trinkets refused.
- [user] Trinket retention on a party wipe: DD1 makes it a game option ("keep_battle_quest_fail_trinkets": "what happens to carried trinkets when you have a total party wipe") with plot quests to win them back — which behaviour to use.
- [?] Trinket-equip warning before embarking: DD1 asks "Your party is not fully outfitted with trinkets. Really embark?" (`town_provision_no_trinkets_equipped`) when under `trinkets_equipped_warning_min_percent` 0.5 of the trinket slots are filled on quests of difficulty ≥ 3. Was: no warning. Now Embark.TrinketWarning + the provisioner's confirm (QuestGoalTests.TrinketWarningOnHarderQuestsWithFewTrinkets); the low-food warning uses DD1's `town_provision_not_enough_food_confirm_format` too. In game: a veteran quest with few trinkets asks before embarking.

## Quest board
- [x] Quest map (`campaign/town/quest_select`): zones, quest icons, party slots, roster — round 1 screenshot; embark works.
- [?] Quests per zone from `campaign/quest/quest.generation.json` tables, lengths, difficulties, rewards.
- [?] Every quest goal finishable (gather / activate / cleanse / explore / kill boss) — EveryDd1QuestCanBeFinished test; check in game.
- [?] Boss plot quests at zone levels 2/4/6; the Darkest Dungeon chain from level 6.
- [?] Resolve bands refuse quests (MaxResolveFor), DD needs resolve 5.

## Provisioning
- [?] Provisioner stock and prices by quest length (`campaign/provision/provision.json`), class-specific free items, drag to buy.
- [?] Eating from the pack: DD1 lets a hero eat a provision in the dungeon for `provision_hp_heal` 5% max HP. Was: food did nothing (UseSupply knew only holy water). Now Crawl.UseSupply eats it, not at full health (CrawlTests.EatingAProvisionHealsFivePercent). In game: click food with a wounded hero selected → "+5% health". (`max_provisions_before_full` 4: reset point unknown, not applied.)
- [?] Low-food / no-torch embark warnings.

## Map generation
- [?] Room/corridor maps from `dungeons/<zone>/<zone>.dungeon.json` + `maps/` sizes; quest curio placement; boss room.
- [?] Corridor contents (battle/trap/obstacle/curio/hunger) distribution per zone.

## Crawl
- [x] Walking the DD1 hallway with DD1 art; hunger checks through DD2 actors — playtests 1-3; rooms, map and HUD in round 1 screenshots.
- [?] Light: 6 per new square / 1 per visited (`tile_light_loss`), torch +25, light bands (`darkness`): scouting, surprise, loot, stress.
- [x] Scouting on entering the dungeon: DD1's `scouting_enter_dungeon_scout_chance` and `_quest_item_scout_chance` are 0.0 in `shared/rules.json` (only buffs raise them): no entry scouting, as the mod does (round 3).
- [x] Scouting treasure: `scouting_chance_scout_treasure` is 0.0 in DD1's rules: nothing to do (round 3).
- [?] Hallway stress per step (`hallway_stress`), starvation (`hallway_hunger_starve_HPdmg`), meals (`meals_table`).
- [?] Traps: DD1 disarm chance (class trap stat + 40% spotted − difficulty), spotted traps block, trap effects (`props/trap_definitions.json`), sounds.
- [?] Obstacles: shovel clears; without one, DD1 damages and stresses the party and drops light.
- [?] Curios: DD1 interaction (click, drag any item, right item works), results (`curios/curio_type_library.csv`), quest curios.
- [?] Corridor fights stop the party; room fights on entry; retreating backs off one square.
- [?] Map (minimap) drag/click travel, room exits by keys.
- [x] Abandoning a quest: DD1 `campaign/quest/quest.exit_penalty.json` fail_penalty.stress_damage 20 on every hero ("The heroes will suffer the stress of defeat..."). Was: none. Now Homecoming adds 2 DD2 points (HamletTests.AbandoningAQuestCostsTheStressOfDefeat); the retreat button uses DD1's question and "Abandon Quest". Round 10 in game: heroes left at 1/10, came back 3/10 on the Retreat results.
- [?] Plot quests' retreat rules: DD1 `quest.plot_quests.json` — Darkest Dungeon parts 1-3 `retreat_party_kill_count` 1 (a random hero dies covering the retreat; DD1's question "The fiends are closing in..."), part 4 `can_retreat` false (no Abandon button). Was: any quest abandoned for free. Now PlotQuest/QuestOffer carry them, Homecoming.RetreatSacrifices kills the hero, CrawlUi hides/asks (QuestGoalTests.DarkestDungeonRetreatCostsAHero). Offers already on a saved board get the fields at the next weekly board. In game: needs a Darkest Dungeon quest (zone level 6).

## Camping
- [?] Camp with firewood: respite points (`camp_start_camping_points`), DD1 camping skills per class, meal, torch restore (`camp_restore_torch`).
- [?] Night ambush (`ambush_camping_base_chance` 0.33, less the camp skills' reductions).
- [?] Ambush in the dark: DD1 `ambush_torch_reduction` -100 — a camp ambush snuffs the torch, the fight starts in darkness. Was: relit to 100, fought in full light. Now Crawl.BreakCamp applies it (CampingTests). In game: after "The camp is ambushed in the night!" the fight's torch reads 0.

## Fights (around DD2 combat)
- [x] Fights launch in DD2 combat with our party and DD1 encounter translations; return to the crawl; DD2 results view skipped; DD1 spoils scroll — round 1 (log "straight back to the dungeon").
- [x] DD1 monsters drawn over their DD2 stand-ins, stand-in models hidden — round 1 screenshot (Bone Militia/Defender/Arbalist/Courtier). Was broken: DD2 adds model parts after the fight starts, they stayed visible in front of the DD1 art; now re-scanned twice a second.
- [x] DD1 monsters fight with their DD1 skills (generated DD2 skills) — round 1: Bone Courtier cast "Tempting Goblet", no exceptions, fight won; round 2: 4 fights (skeletons, cultists, madman), 0 exceptions. Was broken: the presentation alias patched the generic ResourceDatabaseAddressable<,>.GetResource and broke loading heroes (run start hung); now ResourceDatabaseSkills.GetFallbackResourceId.
- [x] DD1 scene behind fights (no DD2 arena, fog/DoF/blur held off) — round 1 screenshots (room and hall). Was broken: it gave up after 3 s when hero models were slow to load ("not set up (no hero models yet)"), leaving DD2's arena: the user's "mix of DD1 and DD2"; now waits up to 30 s.
- [x] DD2 arena particles (red embers) over the DD1 scene: DD2 draws ambient effects with VFX Graph (VisualEffect → VFXRenderer); the setup only hid ParticleSystemRenderers. Now both are hidden at setup (skill effects spawn later and stay). Round 15, combat_arena_forest_dungeon_exterior: "433 ambient particle/VFX renderers hidden", no embers in the screenshot.
- [ ] DD2's colour grading tints the DD1 scene (red cast in the forest-exterior arena); DD1 shows its art untinted.
- [?] Surprise: DD1 chances by room/corridor (`surprise_*_base_chance` 10%/10%) plus the light band's increases.
- [?] Surprise by knowledge: DD1 `surprise_known_*` (scouted room/corridor: party -1.0 = never, monsters 0.25), `surprise_ambush_*` (party 1.0 = always, monsters 0.0), light band added, both sides capped at `surprise_max_*_surprised_chance` 0.65 except an ambush's "always". Was: unknown 10%/10% everywhere, monsters uncapped. Now Crawl.SurpriseChances (CrawlTests.SurpriseFollowsDd1ByKnowledge). In game: battles in scouted rooms never open with "Ambush! The heroes are surprised!".
- [?] Corpses: no corpse after a crit or DoT kill (CorpseRule).
- [?] Combat retreat: 70% + 5% per try (`combat_retreat_chance`, `_bonus_chance_per_attempt`), one try per round.
- [?] Retreat stress: DD1 `combat_retreat_stress` 20 (of 100) = DD2's own retreat penalty of 2 stress each (of 10): same amount (round 5). In game: a successful retreat costs each hero 2 stress.
- [?] Torch level carried into DD2's torch (DD2's own darkness effects stand in for DD1's monster bonuses).
- [x] Battle loot from DD1 encounter `loot:` codes; DD2 loot skipped — round 1 log "[loot] room fight (...): took 2 bust, very_common trinket, 1 skeleton_key, 1 portrait".
- [?] Loot trinkets: DD1 rolls the trinket when it drops (loot tables give a rarity: `loot/*.json` "trinket" + rarity) and the spoils show it; the mod carried "trinket:very_common" until homecoming (spoils showed a label). Now rolled at the drop (Crawl.TrinketOfRarity → a DD2 trinket of the mapped rarity), shown with its picture and tooltip in spoils, curio results and the pack (BattleLootTests.LootTrinketsAreRolledWhenTheyDrop). In game: no trinket dropped in 4 fights (round 2); look for a trinket picture on the spoils scroll.
- [?] Trinkets take a pack slot in DD1 (one per slot, no stacking). Was: trinket loot skipped the room check. Now Inventory.TryTake for curio and battle loot (BattleLootTests.TrinketsNeedAPackSlot). In game: with a full pack, a trinket on the spoils scroll is greyed and left behind.
- [?] Camp/town buffs carried into fights as DD2 buffs.

## Afflictions, virtues, deaths
- [user] Afflictions/virtues: DD1's resolve test at 100 stress (`affliction_*`, virtue 25%, `affliction_severity_table`) vs DD2's meltdown at 10 stress (combat stays DD2's). Options: keep DD2's meltdown; or roll DD1's affliction/virtue when DD2 stress maxes and map it to DD2 quirks/buffs. Your call.
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
- [user] Heroes who finish a Darkest Dungeon quest: DD1 `tutorial_popup_quest_restriction_darkest_dungeon_description` — "they may choose to never go back, or simply..." depending on the game mode and options (not in the data files). Goes with the game-mode question.
- [?] Darkest Dungeon quest flags (`quest.plot_quests.json`): `is_surprise_enabled` false, `is_scouting_enabled` false, `is_roster_stress_cleared_on_completion` true. Was: none. Now on PlotQuest/QuestOffer; Crawl skips surprise (ambushes aside) and scouting, Homecoming clears every roster hero's stress after a win (QuestGoalTests.DarkestDungeonHasNoSurpriseOrScoutingAndAWinClearsStress). In game: needs a Darkest Dungeon quest.
- [ ] Darkest Dungeon failure buff: `roster_buffs_to_apply_on_failure` darkest_dungeon_failure_roster_resolve_xp when the party's resolve ≥ `roster_buff_on_failure_minimum_party_resolve_level` 5 — not applied.
- [?] Town background by DD1's display states (`campaign/town/town_render_data.json`): after returning from Darkest Dungeon part N `town_bg_post_dd_N`, during the stress-heal town events their own. Was: always `town_bg.png`. Now TownRenderData + Estate.LastReturnPlotId (QuestGoalTests.TownBackgroundFollowsDd1DisplayStates); round 13 in game: regular sky still right. In game: after a Darkest Dungeon part, or in week with in_activity_buff_stress_heal_*. (Time-of-day tints `colours/town_screen_colour_*`: not applied.)
- [x] Town hover highlight: DD1's building skeletons (`fx/town_<b>_levelNN`) hold an `active` slot — a flat silhouette a few pixels larger than the `idle` art, drawn before it — so the hovered building shows a pale outline. Was: the hover picture left `idle` out, every hovered building became a flat blob. Now TownLayout.HoverSlot keeps it (SpineAdditiveTests); round 14 screenshot: the hovered Nomad Wagon shows the wagon with its outline. (Also: genuinely additive Spine slots now brighten instead of painting over, SpineRaster.Add.)

## Audio
- [x] DD1 music/ambience/SFX play from the FSB5 banks (user commented on volumes; lowered).
- [?] Footsteps per zone, curio/trap/door/loot/UI sounds, building sounds.

## UI look
- [?] Crawl HUD: DD1 hallway, hero row, stat column, inventory bag tab (672,252), minimap.
- [?] Hero sheet (`shared/character`), right-click everywhere.
- [?] Fonts at DD1's native sizes (nothing overflows).
- [?] Quest select / provisioner / loot scroll / camp screens.
- [x] Corridor heroes as DD2 3D models in the combat pose (ActorBhv.Show), portrait fallback — round 1 screenshot, log "4343 visible samples -> shown".
- [user] DD1 game modes (Radiant / Darkest / Stygian: `modes/`, new_game_plus_* rules) — which, if any.
- [user] DD1 DLC content (Crimson Court, Color of Madness, Shieldbreaker: `dlc/`) — in scope or not.
