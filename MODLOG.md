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

## First in-game session checklist (when DD2 is on D:)
1. Install BepInEx 5.4.23.5 into D:, launch windowed, check LogOutput.log for "Darkest Dungeon 3 0.2.0 loaded" and
   "[session] DD1 content loaded".
2. Main menu: the Hamlet button shows; new estate in slot 1; screens render; DD1 art loads (town_bg.png).
3. Embark: watch `[run]` lines. Does DRIVING start without hero select? Does the roster swap (`[party]`) work?
4. Crawl: walk, hit a fight, `[combat]` lines; does DD2 return to DRIVING after RESULTS? Does the overlay come back?
5. Leave: does ABANDON → MAIN_MENU work without DD2's end-of-run screens? Homecoming log right?
6. Risky assumptions to verify: arena names, trinket rarity tags, quirk library access, IMGUI clicks not leaking.

## Next steps
1. After the reinstall: install BepInEx 5.4.23.5 into D:, launch windowed (`-screen-fullscreen 0 -screen-width 1600
   -screen-height 900`), confirm `BepInEx/LogOutput.log` shows "Darkest Dungeon 3 0.2.0 loaded".
2. Slice 1: start a normal DD2 run, press F9 on the road → a DUNGEON-source fight in `combat_arena_catacombs_cultist`
   with the run's party; confirm it ends back on the road. Screenshot + log.
3. Slice 2: our crawl UI (uGUI canvas, DD1 corridor PNGs from the DD1 install) driven by `Core.Expedition.Crawl`,
   with hall/room fights going to DD2 combat and back. `IParty` implemented over DD2 `ActorInstance`s.
4. Slice 3: Hamlet entry from the main menu; embark builds the DD2 party from `HeroRecord`s; homecoming writes back.
