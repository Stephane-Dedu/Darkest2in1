# MODLOG: DarkestDungeon3 (DD1 meta-game inside DD2)

Journal for the mod. Anything not written here is lost at the next context compaction.

## Goal
Play Darkest Dungeon 1's full loop inside Darkest Dungeon 2:
- the Hamlet (buildings, roster, upgrades, weeks, town events);
- expedition planning (quest select, provisions shop, party of 4);
- DD1-style dungeon map: rooms + corridors, crawled square by square (light, hunger, curios, traps, scouting, camping);
- zones Ruins / Weald / Warrens / Cove (later Darkest Dungeon).

Heroes are DD2's. Combat is DD2's.
Later, behind toggles: DD2 biomes as extra DD1-style expedition zones, etc.

Hard constraint from the user: **do not read or reuse `C:\Users\Piral\Documents\DD1inDD2`** (old project, bad code).

## Paths
| What | Where |
|---|---|
| DD2 install | `E:\DarkestDungeonII` (Epic, `.egstore`) |
| DD2 managed code | `E:\DarkestDungeonII\Darkest Dungeon II_Data\Managed\IronCrown.dll` (all game logic; Assembly-CSharp is 26 KB) |
| DD2 data tables | `...\StreamingAssets\Excel\*.csv` (517 CSVs, plus `dlc_catacombs`, `dlc_dul_cru`, `expedition`, `kingdom` subfolders) |
| DD2 saves + log | `C:\Users\Piral\AppData\LocalLow\RedHook\Darkest Dungeon II\` (`SaveFiles\`, `Player.log`) |
| DD1 install | `C:\Program Files (x86)\Steam\steamapps\common\DarkestDungeon` (copy also on `E:\SteamLibrary\...`) |
| DD2 decomp (never commit) | `C:\Users\Piral\dd2-decomp\IronCrown\` (ilspycmd -p) |
| Mod repo | `C:\Users\Piral\DarkestDungeon3\mod\` (git) |
| Toolkit | `C:\Users\Piral\DarkestDungeon3\universal-modder\` (`PYTHONUTF8=1 bin/um ...`) |

## Recon (2026-10-02)
- `um scan`: Unity 2022.3.16f1, **Mono** backend, company RedHook. No anti-cheat. No loader installed.
- Knowledge base has nothing on Darkest Dungeon or Unity Mono yet: we're first.
- DD2 build: Epic. DLC data present: `dlc_catacombs`, `dlc_dul_cru`, `kingdom` (Kingdoms mode).
- DD2 namespaces of interest: `Assets.Code.Combat.Test`, `Assets.Code.Kingdom*`, `Assets.Code.Mod`,
  `Assets.Code.UI.Mods`, `Assets.Code.Map.*`, `Assets.Code.Run`, `Assets.Code.Roster`, `Assets.Code.Inn`.
- DD1 data is loose text: `campaign/` JSON (quest generation, provisions, roster, town, buildings),
  `dungeons/<zone>/*.mash.darkest` (encounter tables), `*.props.darkest` (curios), and PNG art for everything
  (town backdrop, building icons and portraits, corridor walls, room walls).
  Ruins = `dungeons/crypts`. DLC folders: musketeer, crimson_court, arena_mp.
- DD1's dungeon layout generator is in the exe (not data). Quest JSON gives dungeon, type (explore / cleanse /
  gather / inventory_activate / kill boss), length 1-3 and difficulty. **We reimplement the generator.**

## Route
Pattern 1 from mashup-mods ("port the content"): a **BepInEx 5 + HarmonyX plugin** in DD2.
- Our code owns the DD1 meta-game: state, save, UI (Hamlet, quest select, provisions, dungeon map, corridors).
- It reads DD1 data and PNGs from the user's own DD1 install at runtime. Nothing from DD1 is shipped.
- When a fight starts, it hands DD2 a party of DD2 heroes and a DD2 encounter. Afterwards it reads back
  HP / stress / deaths / loot.
- Why not data-only: DD2 has no Hamlet or room/corridor map to configure. The meta-game has to be new code.

## Decompiling DD2 (gotcha)
- `ilspycmd 9.1` **stack-overflows** on IronCrown.dll (infinite recursion in
  `CSharpConversions.UserDefinedExplicitConversion`). Only ~60 files get written and the exit code lies.
- ilspycmd 10+/11 won't install on this machine (needs the .NET 10 SDK tool format; only SDK 8.0.422 here).
- Fix: a small .NET 8 console app using the **ICSharpCode.Decompiler 11.1.0.9782** library, decompiling
  type by type on a 1 GB-stack thread, with a wrapper that resumes after a crash. Source:
  `scratchpad/dd2decomp/Program.cs` (copy into `mod/tools/` later). Result: 3041 types, 0 failures, ~4 min.

## How DD2 combat starts and ends (from the decomp)
- Data: `BattleConfigurationDefinition` (CSV `battle_configuration_data_export`), rolled from tables
  (`LibraryBattleConfigurationTables.RollBattleConfiguration(tableId, RandomIdentifier.COMBAT, list)`).
- Start (see `Assets.Code.Map.Triggers/TriggerCombatBhv.StartCombat`):
  1. `var s = new CombatScenarioData(battleConfigId, arenaSceneName, CombatSource.DUNGEON, partyActorGuids);`
  2. `Singleton<GameModeMgr>.Instance.SetMode(GameModeType.COMBAT, isLoad:false);`
  3. When the previous mode exits: `Singleton<GameTypeMgr>.Instance.SetCombatScenario(s, isLoad:true);`
     (or `ClearCombatScenario(next)`: GameTypeMgr.Update installs `m_NextCombatScenarioData` next frame).
- `CombatSource` is a CustomEnum with flags (canReturnToDriving, isEndOnPartyDead, loot tables). Useful:
  `DUNGEON`, `AMBUSH`, `CAMP_AMBUSH`, `DEBUG`.
- End: `CombatPresentationBhv.SetNextGameMode` → `RESULTS` → back to `DRIVING`. Kingdom siege goes to `INN`.
  Party wipe and game over are handled in `CombatPresentationBhv.CheckForEndOfCombat`. That's the place to
  patch so a fight returns to *our* dungeon layer.
- Game modes (`Assets.Code.Game/GameModeType`): DRIVING (scene MainScene), COMBAT (scene `combat` + an arena
  scene), RESULTS (`combat_results`), INN, HERO_SELECT, EMBARK, MAIN_MENU, ALTAR_OF_HOPE, CINEMATIC...
- Arena scenes (addressables catalog): per region `catacombs, cave(s), city, coast, farm, forest, mountain,
  tundra, valley`, each with variants `_faction _cultist _gaunt _resist _creature_den _inn_defense ...`, and
  **`_dungeon_interior` / `_dungeon_exterior`** for city, coast, farm, forest, tundra.
- DD2 already has DD1 mechanics we can drive: torch (= DD1 light), stress, quirks and diseases, combat items,
  trinkets, loot.

## Zone mapping (first draft, will live in a JSON config, not code)
| DD1 zone | DD2 arena family | Note |
|---|---|---|
| Ruins (`crypts`) | `catacombs_*` / `city_dungeon_interior` | catacombs is DLC data, present here |
| Weald | `forest_dungeon_*` | |
| Warrens | `farm_dungeon_interior` | swine → Foetor |
| Cove | `coast_dungeon_*` | |
| Darkest Dungeon | `mountain_*` | endgame |

## Design (v0)
- We live inside a real DD2 **run** (so RosterManager, torch, inventory, loot and combat all work) but
  replace the driving/road layer with our DD1 dungeon UI.
- Our hamlet holds persistent DD1-style hero records (class, resolve level, skills, quirks, stress, trinkets).
  At embark we build DD2 actors from them. At return we write the results back.
- Corridors v0: a 2D DD1-style side view (DD1 corridor/room PNGs loaded from the user's DD1 install +
  DD2 hero art). A DD2 3D-actor corridor is a later upgrade.

## Core library (game-independent, unit tested): `src/DarkestDungeon3.Core`
| Part | DD1 source of truth |
|---|---|
| `Dd1/DarkestFile` parser | `.darkest` record format |
| `Dungeon/MapGenerator` | `scripts/map_generator.darkest` (44 blocks: zone × size × quest type) |
| `Dungeon/ZoneProps` | `dungeons/<zone>/<zone>.props.darkest` (curios, treasures, traps, obstacles) |
| `Campaign/QuestBoard` | `campaign/quest/quest.generation.json`, `number.quest.generation.json`, `campaign/progression/progression.json` |
| `Expedition/ItemCatalog` | `inventory/base.*.inventory.items.darkest` (prices, stack limits) |
| `Expedition/CrawlRules` + `Crawl` | `shared/rules.json` (light loss 6/1 per square, hallway stress, hunger, scouting, surprise, darkness bands, backtrack ambush) |

- DD1 → DD2 stress: DD1 affliction at 100, DD2 meltdown at 10, so 10 DD1 stress = 1 DD2 point, with
  probabilistic rounding (2 DD1 → 20% chance of +1).
- Map gen gotcha: per-room nudges made bent corridors cross. Fixed by nudging whole rows/columns (straight
  corridors, still variable length). DD1 configs sometimes list fewer corridors than rooms−1 (long kill_boss: 14/11),
  so the spanning tree comes first and the corridor count is a target.
- Not yet: curio outcomes (`curios/curio_type_library.csv`), trap effects from `props/trap_definitions.json`,
  gather/activate quest items, camping, loot tables, plot (boss) quests, buildings and town activities.
- DD2 hero classes (recruitable): flagellant, grave_robber, hellion, highwayman, jester, leper, man_at_arms,
  occultist, plague_doctor, runaway, vestal, bounty_hunter (Kingdoms data).
- DD2 dev levers: launch flags `-allowEditorPrefs` (reads `StreamingAssets/editor_prefs.txt`) and `-consoleEnabled`.
  Prefs can also be set from code (`TextBasedEditorPrefsBaseType.X`).

## Open questions
- [x] How does DD2 start a battle? → CombatScenarioData + GameModeMgr.SetMode(COMBAT). Any party, config, arena.
- [ ] How are DD2 heroes (ActorInstance) created and put in the party (RosterManager)? Persist across runs?
- [ ] What does a run need to exist? (RunBhv, BiomeManager, map generation.) Can we start one without the road?
- [ ] Main-menu entry point for "DD1 Campaign".
- [ ] Epic build: does `Darkest Dungeon II.exe` run standalone, or only through the Epic launcher?
- [x] BepInEx: current is **5.4.23.5** (2026-02-08), `BepInEx_win_x64_5.4.23.5.zip`.

## Lab
- DD2 saves backed up: `C:\Users\Piral\.universal-modder\backups\dd2-saves\20261002-070440.zip`
  (restore: `um backup restore dd2-saves`).
- BepInEx 5.4.23.5 installed into `E:\DarkestDungeonII` on 2026-10-02 (`winhttp.dll` currently renamed to
  `winhttp.dll.off` from the vanilla-launch test).

## BLOCKER (2026-10-02): the E: drive is failing
- E: is a Storage Spaces "Simple" virtual disk (no redundancy) on one Seagate ST1000VX000 HDD (`Harddisk1`).
  The System log has **`disk` event 7 ("bloc défectueux")** on `\Device\Harddisk1\DR1` 4×/day for 10+ days.
- Symptoms: unzip stalled writing a DLL; DD2 hung at 17 MB with an unkillable thread in an `Executive` wait;
  vanilla DD2 needed 2+ min just to reach "Discovering subsystems"; disk shows 0% idle with ~0 B/s.
- Measured: 0.52 MB/s sequential single-threaded read (a healthy HDD does 100-150 MB/s); robocopy /MT:4 got
  31 KB/s. A full copy of the 5.4 GB game would take ~3 h and may hit unreadable sectors.
- User chose "copy to D:". The copy is infeasible at that speed, so it was stopped. `D:\DarkestDungeonII` holds a
  **partial** copy (BepInEx core + 21 Managed DLLs + our plugin), **not a working game**.
- Builds no longer need the game folder: reference DLLs live in `C:\Users\Piral\dd2-decomp\refs` (+ `refs\bepinex`),
  wired through `RefsDir` / `GameRefs` / `BepInExRefs` in `Directory.Build.props`.
- Need a healthy install (D: = Samsung 840 PRO SSD, 102 GB free). C: has only 16 GB free.

## Status (2026-10-02, loop tick 1, while waiting for the D: reinstall)
Built and unit-tested in Core (48 tests): Hamlet (buildings, Abbey/Tavern activities with DD1 side effects,
Sanitarium, Stagecoach with experienced recruits, Nomad Wagon, upgrade trees, end-of-week), curios
(`curios/curio_type_library.csv`, 60 curios, item interactions), loot tables (`loot/*.json`), traps
(`props/trap_definitions.json`), effects (`effects/*.effects.darkest`), camping (`raid/camping` + DLC, meals,
skills, ambush), quest goals + boss plot quests + Darkest Dungeon chain (`campaign/quest/*`), provisioner
(`campaign/provision/provision.json`), embark, DD1 names/quirk polarity/trinket prices (`Dd1Lore`).
Plugin (compiles against the decomp, NOT yet run): `Dd2/Dd2.cs` (roster/actor/torch access), `Dd2Party` (IParty over
ActorInstance), `Dd2Catalog` (IHeroCatalog from DD2 libraries), `Dd2Heroes` (Hamlet record ⇄ DD2 actor, roster
swap via `m_Entries`), `Dd2Combat` (fight launcher + end detection), `data/zones.json` (zone → DD2 factions,
arenas, bosses; 46 refs validated against DD2 CSVs).
- DD2 data tables cached locally at `C:\Users\Piral\dd2-decomp\data\Excel` (copied from the dying E: at 12 KB/s).
- DD2 APIs used (from decomp): `ActorInstance.ApplyHealthDamage/ApplyHealthHeal/ApplyStressDamage/ApplyStressHeal`,
  `HpRaw/CurrentHpMax/Stress/StressMax`, `QuirkContainer.Add/RemoveAllInstances/GetInstances`,
  `GetTrinketInventory().AddItems`, `LibraryActors.LibraryActorsInstance.CreateActor(classId)`, `SetActorName`,
  `RunValues.ChangeValue(RunValueType.TORCH)`, `RosterEntry(classId, guid).SetRosterStatus(PARTY)`,
  `LibraryBattleConfigurationTables.RollBattleConfiguration`. DD2's own `Cheats.cs` is a good API reference.
- Judgment calls: DD resolve floor 5; DD part 1 opens when any zone hits level 6; Runaway borrows Arbalest camp
  skills; Brigand Cannon = pillager antiquarian table (weak fit); Ancestor's Heart = Mountain boss body table.

## Status (2026-10-02, end of session 1)
- Commits: plugin scaffold (F8/F9 debug keys, mode logging) → Core: map generator → campaign + quest board →
  crawl rules → homecoming + saves. 23 unit tests pass (`dotnet test tests/DarkestDungeon3.Core.Tests`).
- The plugin builds but has **never run in game**: blocked on the failing E: drive.
- The user is reinstalling DD2 to `D:\DarkestDungeonII` (matches `GameDir` in `Directory.Build.props`).
  The old partial copy was renamed `D:\DarkestDungeonII.partial-copy` (safe to delete once the reinstall works).
- Judgment calls to revisit: resolve XP per quest (2/4/6 by length, not in DD1 data), trap damage per trap id
  (hardcoded, should come from `props/trap_definitions.json`), gather/activate quests fall back to explore rules.

## Playable loop (compiled, untested in game) — plugin 0.2.0
`Runtime/Session` (DD1 content on a worker thread, saves at `<persistentDataPath>/DarkestDungeon3/estate_N.json`),
`Runtime/Driver` (Phase: Off → Hamlet → Embarking → Crawling ⇄ Fighting → Homecoming), `Dd2/Dd2Run` (host run:
SKIP_VALLEY, SKIP_PROLOGUE, DRIVING_DISABLE_COMBATS, no intro/tutorials; ends with ABANDON → MAIN_MENU),
`Ui/*` (IMGUI on a 1920x1080 virtual canvas + uGUI input blocker). Main-menu button "The Hamlet" → slot picker.

## Playtest 1 (2026-10-02, DD2 v2.04 at C:\Users\Piral\DarkestDungeon3\game) — what works
Main menu button → estate picker → Hamlet → Embark → host run → DD2 party of our 4 heroes (names, HP, stress) →
crawl with DD1 art → hunger/starvation through DD2 actors → hall fight launched in DD2 combat (lost_battalion_mash_218,
combat_arena_forest_dungeon_exterior, torch carried over). Fixed: IDLE→PARTY roster entries, tutorial gold, crawl UI
null-ref, low-food confirm. NOT yet verified: return from combat to the crawl (user closed the game mid-fight).
User verdict: "works but serious UI issues (poorly designed)". Chosen fix: **DD1 look** (see memory).

## DD1-look UI spec (next session: rebuild CrawlUi first, then Hamlet, Embark, Camp/curios)
Source: DD1 `scripts/layout/screen.raid.darkest`, `panel.*.darkest`, `pannel.inventory.darkest`. Virtual 1920x1080.
- Scene 0..720: hall segments 720x720 (`dungeons/<z>/<z>.corridor_wall.0N.png` is the opaque main layer incl. floor;
  `corridor_door.basic.png` at corridor ends; `foreground_top.01` at y0 and `foreground_bottom.01` at y619 on top;
  bg/mid layers sit behind the opaque wall, skip them). Draw segments at x -120 / 600 / 1320 (current in centre).
  Rooms/entrance: 1920x720 (`room_wall.<kind>.png`, `entrance_room_wall.png`). Mockup: scratchpad/ui/mock2.png.
- Heroes: x = 788, 620, 452, 284 (rank 1..4), feet y 680. Art: DD2 `ResourceActor.GetPortraitIconByType(Color|Story)`
  (Sprite → `GUI.DrawTextureWithTexCoords` with sprite.textureRect UVs). Bars: `overlays/health_pip_*`,
  `stress_pip_*` (10 pips), selection `overlays/selected_1.png`, shadow `charactershadow_med.png`.
- Torch: `overlays/torch.png` (900x188) at (510,28); gauge offset (26,89) size 400x4 each side; flame
  `torch_flame.png` at (960,100).
- Bottom HUD y 720..1080: `panel_transition.png` at (0,710); `side_decor.png` at (0,720) and mirrored at (1670,720);
  `panel_banner.png` at (207,720) [portrait (32,32), name (272,38) colour 177,161,108, class y 76 colour 154,152,143,
  skills (280,35) spacing 76]; `panel_hero.png` at (240,856) [HP (130,11) #c00000, stress (130,40), stats (60,72),
  equipment (238,0), trinkets (453,0)]; `panel_map.png` / `panel_inventory.png` at (960,720) [map at (4,40),
  clip 16..665 x 19..340, tabs (672,252) 48x90, home button (677,24), inventory grid 8 cols from (20,28) step 80x160,
  icons `panels/icons_equip/<type>/inv_<type>+<id>.png` 72x144].
- Map icons `panels/icons_map/`: room_{entrance,empty,battle,boss,curio,treasure,unknown}.png 64px, hall_{clear,dim,
  dark,door}.png and marker_{battle,curio,hunger,obstacle,trap,secret}.png 24px, indicator.png = party. Lay rooms at
  40 px per fine unit; space hall icons evenly between room edges.
- Quest info (12,20) 300x150; `panels/retreat_button.png`, `quest_return_to_hamlet.png`, `overlays/quest_complete.png`.
- Curio/obstacle prompt: DD1 "sidebar scroll" at (1348,200) (Investigate at -152,240; Pass at 75,240; item slot -34,230).
  Curio art exists per curio in `props/shared/curios/<id>/`. Camp respite scroll at (732,60).
- DD1 behaviour to copy: fights start automatically on entering a battle square/room (drop the Fight button);
  walk with D/→ and A/←, click map rooms/halls to walk there.
- Fonts: BMFont text `.fnt` + `.tga` pages in `fonts/` (`dwarvenaxe-{m,l,xl}` headings, `ubuntu{,_m,_s}` body).
  Needs a small TGA decoder + glyph renderer (draw each glyph with DrawTextureWithTexCoords).

## DD1-look redesign, done (2026-10-02/03): crawl → Hamlet → Embark → camp/curios
All four screens now use DD1's own art and layout files, read from the user's DD1 install at runtime.
- **Spine 2.1** (`Core/Dd1/Spine.cs`, `SpineRaster.cs`): DD1's town buildings (`fx/town_<b>_level0N`), curios
  (`props/shared/curios/<id>/<id>.skel`), obstacles and traps are Spine 2.1.27 binary skeletons + libgdx atlases.
  We read the setup pose (bones → world transforms; region/mesh/skinned-mesh attachments; atlas `rotate: true` means
  the page holds the image turned 90° CCW) and rasterize it on the CPU (bilinear, 2x2 supersampling). Tests cover all
  190 town/curio animations. Plugin side: `Runtime/SpineArt.cs` bakes over frames (40 ms budget) with alpha hit tests.
  Slot conventions: buildings `idle` / `active` (hover highlight) / `light_*` / `smoke*` (left out);
  curios `closed` / `active` / `open` (after use).
- **Town layout** (`Core/Campaign/Town/TownLayout.cs`): `town.layout.darkest` `.pos` is the projection of `.pos3d`
  through the camera (960,322,-1251), f=1251; screen y = 862 − pos.y; draw back to front by pos3d z. The 2D town is
  sky (`town_bg.png`) + `fx/town_ground` skeleton (1920x836, origin bottom centre at y 1082) + buildings at scale 1.
  The loose PNGs (`town_backdrop`, `town_left_cliff` …) belong to the 3D mode: unused. Building level from upgrade
  fraction (0.33/0.66), `_locked` before it opens.
- **DD2 resources at the main menu** (`Dd2/ActorResources.cs`): `Singleton<ResourceDatabaseActors>` only exists in a
  run. Outside one, load `ResourceActor` locations by DLC labels (`DLCManager.GetAllOwnedDLCLabels`) keyed by
  `TryGetAssetName`, then `Addressables.LoadAssetAsync` the few we need. Portraits/story art work in the Hamlet.
- Embark: `campaign/town/quest_select` (map spots in `quest_select.layout.darkest`, shifted 120 px left so cove clears
  the roster) and `campaign/town/provision`. Camp: `props/shared/campfire.png`, `raid/camping/skill_icons`, DD1 skill
  names from `localization/heroes.string_table.xml` (English block). Scrolls: `scrolls/*.png` at the
  `screen.raid.darkest` positions (sidebar 1348,200; result 1342,140; meal/respite 732,60).
- The player's locale is French: format numbers with `Gui.Num` (invariant), never `:N0`/`:0.#`.
- 3D hero models (`Dd2/HeroStage.cs`) render black off-screen even with the main camera's renderer, fog off and
  lights: shipped disabled (`Look.HeroModelsInDungeon`). Heroes use DD2 Story art (`Look.HeroArtInDungeon`).

## DD1 systems completed (2026-10-03)
- **Battle loot** (`Core/Expedition/BattleLoot.cs`): a won fight rolls the DD1 encounter it stands for from
  `dungeons/<zone>/<zone>.<level>.mash.darkest` (hall/room/boss lists), then each monster's `loot:` codes from
  `monsters/**/<class>.info.darkest` draw from DD1's loot tables. DD2's own Victory loot is skipped during our fights
  (`NoDd2LootInOurFights` prefix on `LootManager.ShowLoot`, calls `onFinished`). Spoils show on DD1's loot scroll.
- **Results-screen NRE**: the road scene's `CombatResultsPresentationBhv` copy threw in `OnGameModeEnterComplete` every
  fight (DD2 uploaded a crash report each time). `RoadResultsCopyStaysQuiet` skips it for the road scene.
- **CAMP_AMBUSH** sends DD2 to its Inn/Embark after results: camp ambushes now use `CombatSource.AMBUSH`.
- **Buffs into DD2 combat** (`Dd2/FightBuffs.cs`): DD1 camp/town buffs (`shared/buffs/*.json`) become DD2 single-stat
  buffs (picked from `buff_data_export`: sharpness charm = dmg, heartseeker = crit, wolfsblood = speed, hale draught =
  max HP, banter = resists, ancestors coat = damage taken) added with `BuffContainer.TryAdd(..., SourceType.STORY,
  "dd3_fight", ...)` when COMBAT has entered, removed with `RemoveAllInstances(i => i.SourceId == "dd3_fight", ...)`
  when the fight ends. Core counts DD1's `combat_end` battles. DD2 has no stress-taken stat: a prefix on
  `ActorInstance.ApplyStressDamage` scales stress (rolled to whole points) during our fights.
- **Surprise**: DD2's AMBUSH is cosmetic. The side that got the drop gets `extra_initiative_1_round_buff`; enemies
  spawn a little after COMBAT enters, so the buff waits for them (coroutine on Driver).
- **Blacksmith**: DD1 per-class trees (`upgrades/heroes/<class>.upgrades.json`); ranks become DD2 permanent buffs
  (distinct ids stacked; same id doesn't stack) with `SourceType.CLASS` at party build.
- **Guild**: DD2 class skills from `ResourceActor.m_StartingCombatSkills` (known) / `m_AdditionalCombatSkills` (learn).
  Learn = `SkillInstance.SetIsUnlocked()`; master = `actor.UnlockSkill(SkillUtils.GetUnlockFromSkillId(id + "_u"))`
  (the `_u` skill's unlock lists the base id, as DD2's Inn does); loadout = `UnequipAllPossibleCombatSkills()` then
  `SetBaseCombatSkillIdsEquipped`. Prices from the fuzzy-matched DD1 skill tree.
- **Survivalist**, **heirloom exchange**, **town events** (`campaign/town_events`, crier panel; plot/arena/dead-recruit
  events not rolled), **hero resolve** on `roster.variables.json` thresholds (not the dungeons' table), resolve-XP
  bonus, idle stress relief, gems sold and dungeon trinkets kept at homecoming, DD1 **results screen**.
- **Bosses**: the Necromancer plot quest appears at Ruins level 2; its boss room starts DD2's Dreaming General fight
  (`config:forest_dungeon_3`). F10 cannot finish that fight (cadaver stage): fight bosses for real when testing.

## Playtest tools
- `tools/test_hamlet.sh`: rebuild, relaunch windowed 1600x900, open Estate 2's Hamlet.
- `tools/test_enter_dungeon.sh`: same, then Embark → first Ruins quest → first four heroes → Provision → Embark.
- `tools/walk_until_curio.sh [x y]`: walk (optionally from a map click) until a curio, winning fights on the way.
- `tools/fight_once.sh`: head for the nearest battle room (F3), handle prompts on the way, win the fight (F10).
- Debug keys: F3 walk to the nearest battle room (boss first), F4 test estate only (open buildings, gold, first
  smith/guild upgrades, a town event, Ruins level 2 + new quest board), F5 make camp, F6 curio art preview, F7 hero-stage
  dump, F8 state dump, F10 win the fight (not bosses), F11 fight here (alternating surprise). Window px = virtual × 0.8333.
- Estate 2 is the test estate (poor: test scripts buy no supplies). Never test on Estate 1.

## Toggle chosen by the user (2026-10-03): DD2 regions as DD1-style zones — research done, not built
DD2 v2.04 data for five extra zones (ids proposed: dd2_city, dd2_farm, dd2_forest, dd2_cave, dd2_coast):
- Battles (BattleConfigurationTables): `<faction>_mashes_normal` (apprentice), `_hard` / `_normal_champions`
  (veteran), `_hard_champions` / `_brutal_champions` (champion). Natives: city = fanatic, farm = plague_eater,
  forest = lost_battalion, cave = swine, coast = coastal.
- Arenas (all exist): `combat_arena_<city|farm|forest|coast>_dungeon_exterior` (halls) / `_interior` (rooms);
  caves: `combat_arena_cave_faction` / `combat_arena_caves_faction` (halls), `combat_arena_cave_cultist` (boss).
- Bosses (BattleConfigurations): city `city_dungeon_3_a` (the Librarian), coast `coast_dungeon_3` (Leviathan),
  farm `farm_dungeon_3` (Harvest Table), forest `forest_dungeon_3` (Dreaming General, already the Necromancer's
  stand-in), caves: no lair boss in v2.04, use `cultist_guardian_biome_3_boss_1` (Cult Exemplar) / `_biome_2_boss_*`.
- Region display names: not found in DD2's .mo files yet (English isn't there); fall back to our own names.
Plan:
1. zones.json entries with a `dd1_zone` field (the DD1 zone whose maps, curio props, quest tables, heirloom types,
   corridor/room art and mash loot it borrows: city→crypts, farm→warrens, forest→weald, cave→warrens, coast→cove).
2. Core: one place resolving zone → DD1 base zone (Dd1Campaign), used by MapGen.Find, Props, QuestTables,
   HeirloomTypes, BattleLoot mash, Art (corridor/room/door), quest map spots (extra zones lined up at the bottom).
3. Plot quests per region: kill_boss at zone level 2 with DD1-like rewards.
4. Hamlet "Estate options" panel with per-region toggles (Estate.Toggles "zone.<id>"; Hamlet.ToggledZones exists,
   the quest board already accepts extra zones).

## Next steps
1. Build the DD2-regions toggle (plan above).
2. Toggle upgrades (the user's "propose upgrades on a toggle"): DD2 zones crawled DD1-style with bosses. Proposal in
   the session summary; Estate.Toggles and Hamlet.ToggledZones already exist (quest board takes extra zones).
2. Remaining DD1 bits: plot/arena/returning-dead town events; Darkest Dungeon quest chain verified in game; DD1
   afflictions/virtues vs DD2 meltdown/resolve.
