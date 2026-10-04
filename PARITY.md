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
- [x] Stagecoach: recruits (`stage_coach.building.json` number_of_recruits_upgrades base 2, roster_size_upgrades base 9), drag to roster. In game (round 26, test estate, no stagecoach upgrades): 2 recruits, "The roster is full (9/9)".
- [?] Abbey / Tavern: activities, slots, prices, side effects (`abbey.building.json`, `tavern.building.json`: gambling loss, missing, quirks).
- [?] Sanitarium: quirk treatment / lock, disease cure (`sanitarium.building.json`).
- [?] Blacksmith: DD1 window (port layout), per-class equipment trees (`upgrades/heroes/<class>.upgrades.json`) as DD2 buffs.
- [ ] Guild window: still our own layout; DD1's (guild.layout + port's GuildHeroWindow: hero slot, verbose frame, skill rows with requirement icons/cost frames) like the new Blacksmith.
- [?] Survivalist: camping skill purchase grid (`camping_trainer.building.json`).
- [x] Nomad Wagon: stock count from `number_of_trinkets_upgrades` (base 2; seen in game round 26). Rarity: DD1 `rarity_generation_table` weights 6/5/4/2/1 (very rare 1 in 18). Was: a fixed list giving very rare and rare 1 in 7 each. Now Buildings.WagonRarities + Hamlet.WagonRarity (HamletTests.TheWagonOffersRaritiesWithDd1sWeights; the weights can't be watched in one visit).
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
- [x] Recruits arrive with DD1-style quirks. In game (round 26): Bonel (Man-at-Arms) "Last Stand, Nervous", Veci (Leper) two quirks.

## Trinkets
- [ ] Nomad Wagon hint text ("You stash N trinkets. Equip them from a hero's sheet...") is drawn over the keeper art (round 26 screenshot); DD1 has no such line there.
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
- [x] DD1 scene shown as painted: DD2's per-arena colour grading (ColorAdjustments, ColorLookup, ChannelMixer, ColorCurves, LiftGammaGain, ShadowsMidtonesHighlights, SplitToning, WhiteBalance) is held off during DD1-backdrop fights like the blur effects (tonemapping, bloom, vignette kept). Round 16: "holding off 59 DD2 post effect(s): ColorAdjustments, ..."; the red at the top of the hall wall is DD1's own art (`dungeons/crypts/crypts.entrance_room_wall.png` has a red sky), and the screenshot now matches that file.
- [?] Surprise: DD1 chances by room/corridor (`surprise_*_base_chance` 10%/10%) plus the light band's increases.
- [?] Surprise by knowledge: DD1 `surprise_known_*` (scouted room/corridor: party -1.0 = never, monsters 0.25), `surprise_ambush_*` (party 1.0 = always, monsters 0.0), light band added, both sides capped at `surprise_max_*_surprised_chance` 0.65 except an ambush's "always". Was: unknown 10%/10% everywhere, monsters uncapped. Now Crawl.SurpriseChances (CrawlTests.SurpriseFollowsDd1ByKnowledge). In game: battles in scouted rooms never open with "Ambush! The heroes are surprised!".
- [x] Corpses: DD1 leaves a corpse only for monsters whose info.darkest has a `death_class` of type corpse (128 of 252 classes) and not on a crit or bleed/blight/burn kill. Was: monsters with no death_class (maggots, spiders) still left their DD2 stand-in's corpse. Now Core Dd1Corpse + CorpseRule asks the DD1 monster drawn over the stand-in (CorpseTests). In game (round 27, Shift+F10): maggot_A "slain (DD1 gives it no corpse)", its rank emptied; skeleton_defender_A kept its corpse; 0 exceptions.
- [?] Corpse look: DD1's corpse classes (`monsters/corpse/corpse_A-D`, `corpse_large_A-C`) have no art of their own (art.darkest: deathfx death_corpse_medium, corpse health bar): the corpse is the slain monster's own `dead` pose, kept in the rank. Was: the monster stayed standing (round 27) or vanished (round 28: a cutthroat's rank went empty) because a corpse-leaving kill raises no EventCombatActorDeath — DD2 changes the actor's class to its death class (EventActorChangeClass, internal) and can rebuild its CombatActorBhv. Now Dd1MonsterView re-finds the actor by guid (Rebind) and watches DD2's actor library for a `_corpse` class (CheckCorpse) → dead pose. Not seen in game: debug kills outside a skill aren't resolved until combat moves on, and the test fights rolled maggots/spiders. Check: Shift+F10 (now leaves the front enemy at 1 HP) on a skeleton/cultist/brigand, finish it with a hero skill; expect log "[dd1art] X: corpse: lies in its dead pose" and the monster lying in its dead pose. Round 29: still not seen — Shift+F10 now picks the front enemy (lowest TeamPosition; F10's actor order isn't rank order), but scripted hero turns didn't land the killing blow (the active hero's skills were buffs/back-rank; the Bloodletter sat at 0/41 standing). Needs a hand-played kill, or a test hook that runs a hero skill on the front enemy.
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
- [?] Death stress on the party: DD1 `death_party_stress_chance` 0.5 × `death_party_stress_damage` 12 (of 100) per ally; DD2's own `on_ally_death` stress trigger (stress_triggers_data_export: +1 of 10 to each observing hero, 100%) applies in its combat — about the same (DD1 averages 0.6 points). Combat stays DD2's. In game: a hero dying in a fight gives each ally 1 stress.

## Loot & homecoming
- [?] DD1 results screen (`raid_results`): quest completed/abandoned backgrounds, heroes, items.
- [?] Gold/heirlooms home, gems sold, trinkets kept, quest rewards, resolve XP, zone XP and levels.
- [x] Full pack on loot: DD1's loot scroll (port ScrollEventLoot.cs / InventoryItem.OnPointerClick) keeps what doesn't fit: click a loot slot to take it, shift+click a pack item outside a fight to drop one (never a quest item), Pass can't leave a quest item. Was: greyed and lost, no way to drop, curio overflow only a log line. Now the same on battle and curio scrolls, the pack stays in view while loot waits (Crawl.TakeLeftBehind/Discard/CanLeave, QuestGoalTests.FullPackLootCanBeTakenAfterDroppingSomething). In game (round 19, Shift+F11 full pack): 4 spoils left on the scroll, 8 shift+clicks dropped a torch stack, clicking the gold took it into the pack; 0 exceptions.
- [x] DD2 effects over DD1 monsters: DD1 monsters carry their own effects in their sprites (`monsters/<family>/fx`, attack anims). Was: only the stand-in's meshes were hidden, so its DD2 particles / VFX Graph stayed (round 19: a red mist around the Bloodletter). Now Dd1MonsterView.ModelRenderers hides ParticleSystemRenderer and VFXRenderer under the stand-in as well. In game (round 20, ambush, 3 DD1 cultists over fanatic/cultist stand-ins): hidden vfx_blood_dripping_from_face, vfx_blade_burning_antic, vfx_fire_particle_small_on_awake... and DD2's death dots on the kill; the DD1 sprites show clean; 0 exceptions.

## Town events
- [?] Weekly town events (`campaign/town_events`): crier panel, effects on buildings/prices/recruits.
- [?] Returning-dead event: DD1 dead_recruit ("From Beyond", week ≥ 15, ≥ 3 dead, base.town_events.events.json): 3 fallen heroes wait at the stagecoach, only ONE can be hired back. Was: never rolled. Now rolled; the fallen are offered with their level and quirks, labelled "From Beyond" in red, and hiring one leaves the others in the graveyard (HamletTests.FromBeyondOffersThreeFallenHeroesAndOnlyOneReturns). In game: needs a week-15+ estate with 3 dead; check the stagecoach rows and that the hired hero leaves the graveyard.
- [ ] Plot-quest events (plot_quest_town_invasion_0, plot_quest_crow_trinket, trinket_retention_add_from_storage) not rolled: their plot quests (plot_town_invasion_0 in the `town` dungeon, plot_crow_trinket / plot_trinket_retention_N in the Weald) run on DD1's hand-made maps — after the plot-maps item.
- [user] Arena events and the Butcher's Circus (`arena.town_events.events.json`) are DLC content: goes with the DLC question.

## Plot quests & the Darkest Dungeon
- [?] Boss quests per zone; DD1 bosses → DD2 boss configs (zones.json).
- [?] Darkest Dungeon quest chain (`campaign/quest/plot`), DD art per quest folder, resolve 5 gate.
- [ ] Hand-made plot maps: DD1 `quest.plot_quests.json` `map_name` — Darkest Dungeon parts 1-4 (DD_map1-4), the town invasion (town_invasion_0), the crow quests (crow_map1), the tutorial — load DD1's own `maps/<name>.dm` (binary data, magic 0xB101: header, object table, pre-order field table, data; `map.static_dynamic.static_save` is a nested file with the areas' doors and tile positions, `map.static_dynamic.areas` the tiles' content/traps/mash_name). Mod: generates a random map for them. Split:
  - [x] a) Core reader for DD1's binary files: Dd1Binary (round 21). Reads all six plot maps and their nested static_save (Dd1BinaryTests: DD_map1's rooA → corA doors, tile content, mash_name dd_quest_1_mash_07, mappos (28,1)). Core only, no in-game part.
  - [x] b) .dm → DungeonMap: PlotMap (round 22). Rooms at their mappos, corridors between their end doors, square codes (1 battle, 3 trap, 4 obstacle, 6 room curio, 7 hall curio, 8 hunger, 10 room treasure; 9/13 secret room/door), named battles kept as MashName, entrance and boss room from entrance_id/final_room_id, props drawn from the zone tables. PlotMapTests: DD_map1 15 rooms / 18 corridors / 3 hunger squares / Shuffler room, DD_map4's 28-square corridor with 3 obstacles, town invasion, crow lair, DD_map2/3 whole and connected. Core only.
  - [x] b2) Named battles: a room/square with a MashName fights DD1's `named:` mash row of that name, the level picked like a rolled fight's (weald 1/3/5 crow_1 = crow_A/B/C), else a rolled one (BattleLoot.NamedEncounter, Crawl.FightMonsters). PlotMapTests.SetFightsAreDd1sNamedMashRows: DD_map1's mash_07 room fights shrouded/warlord/harpy/harpy. Core only (in game with c).
  - [?] b5) Plot-map monsters' DD2 stand-ins (data/monsters.json, round 24): cultist_orgiastic → cultist_herald/fanatic_blind_shaman, cultist_shrouded → cultist_evangelist, cultist_warlord → fanatic_flayer, cultist_harpy → cultist_cherub, totem_attack/guard → coven_cauldron, templar_melee(_mb) → coven_custode, templar_ranged(_mb) → lost_battalion_knight, errant_flesh_bat → courtier_swarm, errant_flesh_dog → shared_dog_rabid, cyst → coven_matres_sequelae, cell_white → shared_lost_soul, cell_battle → coven_mothers_spitter, crow → beastmen_rot_claw; all have DD1 anims and pass SkillMatchTests (role + size). The nest (crow's lair) is a boss piece (no turns, life_link crow, tag boss): that fight falls back to DD2's table like every DD1 boss. In game (after c): a Darkest Dungeon fight draws DD1's cultists/templars over these. Round 25: cultist_orgiastic_D seen in game over fanatic_blind_shaman (DD_map1); the other families not yet.
  - [ ] b3) Secret rooms (code 9) behind a secret-door square (code 13): DD_map4 rooC, town_invasion_0 rooA. Left out of the map for now.
  - [ ] b4) DD1's corridors can bend (town invasion); the crawl map draws hall squares on a straight line between the rooms (CrawlUi.HallPos).
  - [x] c) Plot quests with a map_name embark on DD1's map (PlotQuest/QuestOffer.MapName from `quest.map_name`, Embark.Create → PlotMap). Goal curios: PlotMap.PlaceGoal puts them in the code-6 rooms held by a non-numbered set fight (heuristic matching the Unity port's copies: DD_map2's beacons behind the 3 minibosses, DD_map3's teleporter behind dd_quest_3_teleport). PlotMapTests.TheDarkestDungeonEmbarksOnDd1sOwnMaps. In game (round 25, Shift+F4 on the test estate): "Level 6 Kill Boss — Slay Shuffler" on DD_map1, Darkest Dungeon room art; F3 walked into corH's set fight dd_quest_1_mash_01 = [cultist_orgiastic_D ×2] drawn as DD1 sprites over fanatic_blind_shaman; 0 exceptions. Parts 2-4 not walked in game.
- [user] Heroes who finish a Darkest Dungeon quest: DD1 `tutorial_popup_quest_restriction_darkest_dungeon_description` — "they may choose to never go back, or simply..." depending on the game mode and options (not in the data files). Goes with the game-mode question.
- [?] Darkest Dungeon quest flags (`quest.plot_quests.json`): `is_surprise_enabled` false, `is_scouting_enabled` false, `is_roster_stress_cleared_on_completion` true. Was: none. Now on PlotQuest/QuestOffer; Crawl skips surprise (ambushes aside) and scouting, Homecoming clears every roster hero's stress after a win (QuestGoalTests.DarkestDungeonHasNoSurpriseOrScoutingAndAWinClearsStress). In game: needs a Darkest Dungeon quest.
- [?] Darkest Dungeon failure buff: `roster_buffs_to_apply_on_failure` darkest_dungeon_failure_roster_resolve_xp (resolve_xp_bonus_percent +100%) to the whole roster when a party of resolve ≥ 5 fails. Was: nothing. Now carried by plot offers and applied by Homecoming as a pending buff (QuestGoalTests.FailingTheDarkestDungeonWithSeasonedHeroesInspiresTheRoster). Difference left: DD1 keeps it until a quest is completed ("quest_complete"), the mod's pending buffs last one expedition. In game: needs a failed Darkest Dungeon run.
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
