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
- 3D hero models (`Dd2/HeroStage.cs`) rendered black off-screen. Likely cause found (2026-10-03): DD2's
  DeferredRenderPass draws `FilteringSettings(opaque, DeferredRenderFeature.layerMask)` and its lights can require
  `cullingMask == layerMask` (exclusiveLayerMasking); HeroStage had put the heroes on private layer 31. Now they stay
  on "Characters" (CombatActorBhv.SetForegroundCharacterLayer uses it), camera and lights use the feature's mask
  (read by reflection), and the stage reads its picture back once loaded: too dark -> DD2 flat art (logged).
  New option key `Look.Dd2HeroModelsInDungeon` (on).

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

## Toggle chosen by the user (2026-10-03): DD2 regions as DD1-style zones — built (see Needs in-game check)
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

## End-to-end audit (2026-10-03) and the user's list — plan awaiting the user's choices
Bugs found: DD2 quirk/item libraries are empty at the main menu → recruits get no quirks, the Nomad Wagon sells
nothing (all 7 heroes on Estate 2 have no quirks); hero-only trinkets go to other classes (Vestal holds the Jester's
Busker's Haul); fonts drawn at 50-65% of DD1's native size without mipmaps (small, thin); stress pips draw as white
blocks.
Feasibility notes (decomp):
- Corpses: `ActorInstance.Kill(deathType, sourceType, ...)` → `GetIsDeathClassValid` decides the death class (corpse);
  SourceType.DOT = bleed/blight kill; crits from `ApplyHealthDamage(isCrit)`.
- Results view: `CombatPresentationBhv.SetNextGameMode` sets RESULTS; for our fights go straight to DRIVING.
- Combat retreat: DD2 has it: `CombatBhv.AttemptRetreat()` (can fail; `IsRetreatInvalid`), `BattleResult.m_IsRetreat`,
  CombatRules.RetreatEffects.
- DD2 actors are Unity Animator models (no Spine): DD1 monsters can't become real DD2 actors without Unity-built asset
  bundles; fal 3D conversion impractical (and the fal MCP rejected its token this session). Variety option: translate
  each rolled DD1 encounter into the closest DD2 enemy types per slot.

## Needs in-game check (the user was at the PC; no launches before 10:15 on 2026-10-03)
- [ ] Fonts at the new sizes: nothing overflows its box (Hamlet roster, building windows, quest scroll, results).
- [ ] Provisioner: items sit in DD1's cells; click/drag to buy, right-click/click to return, drag within the pack to
      reorder, drag a stack back to the shelf; the arrangement carries into the dungeon pack.
- [ ] Quest select: drag a roster hero onto a party slot (replaces / fills), drag between slots (swap), drag a party
      hero back onto the roster (leaves); clicks still toggle.
- [ ] Abbey / Tavern: heroes dropped into DD1's painted arch slots; locked slots show DD1's overlay; red hint for a
      refused hero; click or drag back to the roster cancels with a refund (event prices included).
- [ ] Sanitarium: dropping a hero opens the quirk choice (lock positives left, treat negatives right / diseases);
      cancel refunds the treatment.
- [ ] Guild / Blacksmith / Survivalist: dropping a hero on the panel selects them.
- [ ] Dungeon pack: arranged slots, drag to reorder, drag a supply onto a hero to use it; holy water blesses 3 fights.
- [ ] Corpse rule: a monster killed by a crit or bleed/blight leaves no corpse (log line "[combat] ... no corpse");
      other kills still leave one; boss phases untouched.
- [ ] Results view skipped: after a win, straight to the corridor + DD1 spoils (log "straight back to the dungeon");
      no leftover results scene, next fight starts normally. Setting Look.SkipDd2ResultsView.
- [ ] Retreat button (top left, DD1 art) during our fights: 70% (+5%/try), one try per round; success → DD2 retreat
      (2 stress each) and the party falls back (room → corridor square it came from; hall → one square back),
      fight still there; failure → announcement. Hidden where DD2 forbids retreat.
- [ ] Walking: hold D/→ to walk continuously (hallway scrolls, ~1.2 s per square), A/← backs up at half speed;
      stops at fights/obstacles/curios/rooms; heroes bob; map clicks still auto-walk with the per-square slide.
- [ ] In a room, D/A/W/S take the exit on that side of the map.
- [ ] Trinket pictures load at the menu (log "[items] N item resources available"); names from DD2's localization.
- [ ] DD1 hero sheet (roster click): layout over shared/character art, quirks, stats, equipment, trinket slots
      (drag from the realm inventory, swap, click/drag away to unequip, class-only trinkets refused), skills with
      DD2 icons (locked / brought / mastered), camping skills, resistances, diseases, prev/next, dismiss (2 clicks).
- [ ] Realm inventory from the estate bar's "Trinkets" and from the sheet.
- [ ] Nomad Wagon: DD1 grid with trinket pictures, prices, hover details, click to buy.
- [ ] Dungeon HUD: equipped DD2 skills in the five banner slots, DD1 weapon/armour pictures, trinket pictures.
- [ ] Monster variety: log "[combat] DD1 encounter [...] -> ..." then DD2 spawns that exact line-up
      (custom BattleConfiguration "dd3_dd1_encounter" registered at fight start). Check: enemies appear in order,
      two-rank enemies fit, champions (_b) at champion level, loot after the win matches the DD1 monsters,
      a retreat then re-engage meets the same group, bosses still use zones.json configs.
- [ ] DD1 hallway strip: far/mid parallax behind the walls' gaps, doors and end walls at both ends; walking by
      keys and by map route is continuous (no 0.3 s slides); stops at fights/curios/traps; fade into a room at
      the last door and out of a room into a hallway; room exits by arrows fade too.
- [ ] DD1 monsters in fights (Look.Dd1MonstersInFights): log "[dd1art] N DD1 monsters to draw", "drawing with
      UI/Default", "bound N/N". Each stand-in's 3D model hidden only once DD1's art draws; idle loop, held attack
      pose on its skill, defend pose when hit, death animation. Check position/size on the DD2 model, facing
      (mirrored to face the heroes), skill zoom-ins (DD2 may present skills with other objects), torch dimming,
      UI overlap (drawn over DD2's combat UI). If anything throws, DD2's models come back (logged).
- [ ] DD2 regions (estate bar "Regions"): switching one on puts 2 quests (+ its boss when due) on this week's
      board, off removes them; quest map stacks them in a left column; a region expedition uses its DD1 zone's
      maps/curios/art, DD2 natives fight (no DD1 translation), boss room = the lair boss config; zone XP and
      level per region; boss tiers at levels 2/4/6.
- [ ] Hero models in the dungeon: log "[stage] hero layer N (Characters), deferred mask 0x..." then
      "[stage] hero models: ... brightness X -> shown / too dark". If shown: lit, right size/place, bob while walking.
- [ ] Play test 2 (fixes): walking stays where it stops, mouse-hold walks (right of 1210 / left of 200), props
      scroll, minimap drag, DD1 stat column, right-click sheets (dungeon read-only; prep editable), no carriage
      (fade, DD1 scene held until the fight is ready).
- [ ] DD1 audio: log "[audio] DD1 banks loaded: master + N/19" (or the FMOD error per bank, e.g. ERR_FORMAT if
      DD2's FMOD 2.02 can't read DD1's format-103 banks) and "[audio] N DD1 events listed in .../dd1_audio_events.txt".
      Town/exploration/camp/battle music, zone ambience, footsteps, room transitions, combat start/victory/retreat,
      button clicks, building doors; DD2 music/SFX VCAs down on our screens, SFX back in fights.
- [ ] DD1 backdrop in fights: log "[backdrop] DD1 scene behind the fight: N arena renderers hidden ...". Check the
      floor line at the heroes' feet, actors in front, nothing important hidden (markers, VFX).
- [ ] DD1 monster effects: attack pose + its fx on the monster + targchestfx on targets; death fx.
- [ ] Play test 3 fixes: DD1 monsters upright (rendered into a texture in LateUpdate, top-left GL matrix, shown by
      OnGUI), sized to the DD2 stand-in's body (log "drawn, ... DD2 body N px, DD1 height N, scale N"); hero
      models: "[stage] renderer R, layer L, post P: brightness B" for each setup, then "best: ..." and "lit" or the
      flat-art fallback; DD1 audio through FMOD core from the FSB5s ("[audio] DD1 sounds: N samples").
- [ ] Estate 1 repair on next load (backup estate_1.json.before_repair_20261003): quirks given, wagon stocked.
- [ ] Play test 4 (user: blur, items tab, corridor pose, mixed background, traps, spell mismatch):
      - fights: "[backdrop] holding off N DD2 post effect(s): DepthOfField, ..." and sharp DD1 monsters/backdrop;
        "[backdrop] hid N more DD2 scenery renderers" when DD2 adds scenery mid-fight; DD2 skill VFX still visible;
      - the bag tab under the map tab opens the inventory (DD1 tab_placement 672,252);
      - corridor heroes in DD2's combat stance (animator default state), facing the walking direction
        (Look.Dd2HeroModelPose = neutral brings back the road idle);
      - traps: a spotted trap stops the party; the prompt shows the selected hero's chance (class trap stat + 40%
        spotted - difficulty); click the trap or Disarm; DD1 trap and per-zone disarm sounds;
      - DD1 monsters: "[dd1art] X: skills: dd2=dd1, ..., dd2=unused" per monster; stand-ins never use "unused"
        skills; the banner/inspection show each DD1 monster's own skill names, the inspection its own name;
        new stand-ins (cultist_evangelist/herald, shared_spider_*, shared_dog_rabid, coven_hateful_virago,
        beastmen_rot_claw, shared_lost_soul_chirurgeon) spawn and are drawn over;
      - "[patch] N patch classes applied" and no "[patch] ... not applied".
- [ ] Play test 5 (user: corridor portraits, DD2 spells on DD1 mobs, trinkets menu, weaponsmith):
      - corridor: "[stage] hero models: N visible samples ... shown" (combat pose via ActorBhv.Show()); if it logs
        "trying the road pose", the combat pose failed and idle_neutral is used;
      - DD1 monster skills: "[dd1art] X: skills: DD1's crossbow_shot on arbalist_hip_shot (3-7 x0.50, ...)" per
        monster; the fight runs (no exception from GetResource/ActorDataSkill), banners show DD1 names, damage and
        effects are DD1's (blight/bleed dots, stun, push/pull, stress, horror); Darkness skills dim our torch.
        Gameplay.Dd1MonsterSkills=false restores DD2's skills under DD1 names if it breaks;
      - Trinket Inventory: estate-bar button, DD1 layout, sorts, unequip-all, shift-click sell question, tooltips
        with DD2 effects, drag to/from a hero sheet;
      - Blacksmith: hero slot, verbose column, frame rows, cost frames, purchase; nameplate + upgrade button in all
        building windows.

## DD1 monster skills as DD2 data (2026-10-03)
- DD2 data: per-element CSV texts live in ResourceDatabaseText (LibraryDataContainerCsv<T>.GetResourceDatabase(),
  ResourceText.m_Name/m_Data = the lines inside element_start/element_end). New elements: new ActorDataStats(id,
  text) / ActorDataEffects / ActorDataSkill, Init(id), Library<string,T>.Instance.AddLibraryElement(e, overrideCSV).
  ActorDataSkill.InitInternal finds its stats/effects by m_ActorDataStatsId/m_ActorDataEffectsId or its own id.
- An actor's skills: ActorInstance.m_CombatSkills (SkillInstance.m_SkillId, private, writable). Presentation is
  looked up by skill id in ResourceDatabaseSkills (ResourceDatabaseAddressable<,>.GetResource(string resourceId,...)),
  patched so "dd3__<base>__..." ids use the base skill's resource.
- Enemies pick skills in ActorControllerRandom.SelectSkilIId from GetValidSkillTargetEntries (weighted by
  m_RandomSelectChance); AI graphs (ResourceActor.m_AiGraph) exist for some (bosses).
- DD1 effects -> DD2 (Dd1SkillToDd2.Map): dotPoison/dotBleed -> skill_dot_{small,medium,large,massive}_{blight,bleed};
  dotStress -> dot_horror_*; stun -> add_1_stun; push/pull -> move_knockback_N/move_pull_N; stress N -> stress_damage_
  round(N/10); shuffletarget -> move_shuffle(_50pct); stat buffs/buff_ids -> weak/vulnerable/blind/strength/dodge/
  block/speed/crit tokens; tag -> add_1_vulnerable; heal -> heal_15/25pct; torch_decrease -> our Crawl.Darken.
  Unmapped (no DD2 counterpart): stress-received and stun-resist debuffs, conditional "vs marked" bonuses, summons,
  low-chance diseases.

## Parity loop, round 1 (2026-10-03, branch claude/practical-wright-hicri0)
- GOTCHA: never Harmony-patch a method of a generic class instantiated over reference types (e.g.
  `ResourceDatabaseAddressable<ResourceDatabaseSkills, ResourceSkillBase>.GetResource`): Mono shares that code across
  every `ResourceDatabaseAddressable<X, Y>`, and the patched copy hard-wires the patched instantiation's type
  arguments, so DD2 loaded hero ResourceActors as ResourceSkillBase ("Unable to load asset of type
  ResourceSkillBase from .../grave_robber.asset"), EventGameTypeStarted threw and the run start hung. Patch a
  non-generic override instead (here `ResourceDatabaseSkills.GetFallbackResourceId`).
- The fight backdrop's setup needs the heroes' model renderers; they can take well over 3 s to load (room fights),
  so it now waits 30 s (arena/scenery/camera 15 s) before leaving DD2's arena up.
- DD2 keeps adding renderers to an actor after the fight starts: hiding the stand-in once wasn't enough.
- tools/test_enter_dungeon.sh: Embark plate now at (800,770) in the 1600x900 window; arrival = "[stage] hero models".

## Parity loop, round 2 (2026-10-03)
- Loot trinkets are rolled at the drop (LootDrop.ResolveTrinket + Crawl.TrinketOfRarity, set by the Driver to
  Dd2Catalog.RandomTrinket); the pack key is "trinket:<dd2 id>"; ItemArt.Stack draws it (DD1's unknown-trinket art
  for an unresolved rarity). Homecoming still resolves leftover rarity tokens.
- F11 (fight here) + F10 chain several fights in place for testing; trinket drops are rare (none in 4 fights).

## Parity loop, round 3 (2026-10-03)
- DD1's entry scouting (`scouting_enter_dungeon_scout_chance`, `_quest_item_`) and `scouting_chance_scout_treasure` are
  0.0 in shared/rules.json: nothing to implement (only buffs raise them).
- Surprise: DD1 has three cases (unknown / known = scouted / ambush) plus the light band; radiant light already adds
  +25% monsters surprised. The Unity port doesn't implement surprise rolls (attributes only), so DD1's caps were read
  from rules.json; an ambush's 1.0 stays "always".

## Parity loop, round 4 (2026-10-03)
- DD1's post-quest disease roll has values (`disease_after_quest_min_chance` 0.05, `disease_max_chance` 0.32,
  `disease_hero_disease_resist_weight` 0.33, from resolve 2) but no formula in the data or the Unity port: left as
  [ ] until the formula is known. Quirk limits were fully specified, so done instead (QuirkLimits).
- Curio quirks go straight onto the DD2 actor (Dd2Party.AddDd1Quirk); the cap is applied there against the actor's
  QuirkContainer instances and the estate record's LockedQuirks.

## Parity loop, round 5 (2026-10-03)
- Retreat stress already matched: DD1 combat_retreat_stress 20/100 = DD2's retreat penalty 2/10 per hero.
- Camp ambush: DD1 ambush_torch_reduction -100 after camp_restore_torch 100 → the ambush fight is in the dark; the
  Driver carries the expedition light into DD2's torch at fight start.

## Parity loop, rounds 6-13 (2026-10-03)
- Done (Core + tests): trinkets need a pack slot (Inventory.TryTake), 3 locked positive quirks (Hamlet.WhyCantLock),
  eating provisions (provision_hp_heal), DD1's trinket warning (Embark.TrinketWarning) and provisioning questions,
  abandon stress (quest.exit_penalty.json, verified in game: 1/10 -> 3/10), Darkest Dungeon retreat sacrifice and
  no-abandon part 4, DD quest flags (no surprise/scouting, roster stress cleared), town display states.
- Not in DD1's data (darkest.exe only): post-quest disease formula, negative quirk auto-lock timing, dismissal
  penalty's "upper_level" → [blocked]. Game-option dependent → [user]: trinket retention on wipe, DD return refusal.
- DD1 localization holds the UI wording: town_provision_*, retreat_confirm_raid_question, retreat_raid_tooltip,
  retreat_raid_party_kill_darkestdungeon_confirm_question, realm_inventory_*, action_verbose_body_<building>_<class>.

## Parity loop, rounds 14-15 (2026-10-03)
- DD1 town skeletons: slot order active (flat silhouette, a few px larger) → idle → light/smoke; the hover is the
  silhouette behind the building (an outline), never the silhouette alone.
- DD2 draws ambient arena effects (embers) with VFX Graph: VisualEffect components rendered by "VFXRenderer"
  (Unity.VisualEffectGraph.Runtime.dll) — hide by type name like ParticleSystemRenderer.

## Next steps
1. The audit plan above, in the order the user picks.
2. Build the DD2-regions toggle (plan above).
2. Toggle upgrades (the user's "propose upgrades on a toggle"): DD2 zones crawled DD1-style with bosses. Proposal in
   the session summary; Estate.Toggles and Hamlet.ToggledZones already exist (quest board takes extra zones).
2. Remaining DD1 bits: plot/arena/returning-dead town events; Darkest Dungeon quest chain verified in game; DD1
   afflictions/virtues vs DD2 meltdown/resolve.

## Next (user request 2026-10-03 ~08:50) — top priority, before the DD2-regions toggle
1. DD1 room-to-room travel like DD1 (user: "still image by image, no walking animation"). Port the Unity port's
   model (C:\Users\Piral\csharpdd\Darkest-Dungeon-Unity\Assets\Scripts\Raid):
   - Corridor = one continuous strip: sectors 720 wide (RaidHallway: 2 border sectors each side, door sectors at the
     ends, endhall walls at ±(ActiveSectorCount*360-80)); parallax layers `<zone>.corridor_bg.png` (far) and
     `<zone>.corridor_mid.png` (mid) behind `corridor_wall.NN`; foreground_top/bottom in front.
   - RaidPartyController: party has a real x position; speed 40 units/s (canvas units), walking back at half speed;
     blocked at passage walls; hero walk animation runs while moving and stops smoothly. Sector contents trigger
     when the party reaches them. Map-click travel should walk continuously too (no 0.3 s slides).
   - Rooms: one `room_wall.<type>.png` with a short walkable passage; transitions fade (ScreenFader.Fade/Appear).
   - Heroes: use DD2's combat look (HeroStage renders DD2 models off-screen but they come out black — fix that,
     and play a walk/idle clip while moving); keep the DD2 sprite fallback.
2. DD1 monsters inside DD2 combat (plan the user pasted): add animation playback to Core/Dd1/Spine.cs; draw each
   monster as a camera-facing quad redrawn on the GPU every frame; attach it to the DD2 stand-in from
   data/monsters.json and hide the stand-in's model; idle→combat, skill→attack_<skill>+fx, hit→defend, death→dead;
   darken by torch level. Prototype: skeleton_arbalist in a test fight on Estate 2 (after 10:15 only).
3. Then the DD2-regions toggle. Research done: region names are in DD2's .mo files as "key\x04English"
   (biome_name_City The Sprawl, Farm The Foetor, Forest The Tangle, Cave The Sluice; the coast is the Shroud);
   enemy display names are keyed by actor id (fanatic_librarian Librarian, plague_eater_harvest_table Harvest Child,
   lost_battalion_boss_dreaming_general Dreaming General, cultist_exemplar Exemplar). DD1 boss quests: kill_boss,
   length 2, at zone levels 2/4/6 with difficulty 1/3/5, resolve XP 4/8/16. Plan: a Core ZoneBase registry
   (zones.json "dd1_zone") used by MapGen.Find, Props, QuestTables, HeirloomTypes, BattleLoot, Loot, CurioResolver,
   QuestGoals, CrawlUi art; regions skip the bestiary (DD2 natives fight); region boss quests; estate toggle panel.

### Loop round 17-18 (2026-10-04)
- DD failure buff: `quest.plot_quests.json` roster_buffs_to_apply_on_failure + roster_buff_on_failure_minimum_party_resolve_level (5) now carried by PlotQuest/QuestOffer; Homecoming gives every roster hero the buff as a PendingBuff (one expedition; DD1 keeps it until a *completed* quest — noted in PARITY).
- Death stress: DD2 `stress_triggers_data_export` on_ally_death = +1 (of 10) to each observing hero at 100%; DD1 is 0.5 × 12/100. Left to DD2's combat.
- "From Beyond" (dead_recruit): rolled now. `HeroRecord.FromGraveyard` marks fallen heroes offered in `Estate.Recruits`; `Hamlet.Recruit` takes one out of the graveyard and drops the other fallen offers; `RefreshWeek` clears the flag on unclaimed ones. `Hamlet.StartTownEvent(rng)` is public (applies the current event's on-visit effects) so tests can force an event.
- DD2 side: a hero brought back gets a fresh DD2 actor next embark (`Dd2Heroes.BuildParty` always `CreateActor`s), so no dead-actor reuse.
- No debug key forces a town event; checking events in game needs a real week roll.
- Round 19, full pack on loot (DD1 port ScrollEventLoot.cs + InventoryItem.OnPointerClick: shift+click a raid-inventory item when peaceful = drop one, not quest_item; loot slots click to take; Close refuses while a quest_item remains).
  Core: `CurioReport.LeftBehind`, `Crawl.TakeLeftBehind(list, index, taken)`, `Crawl.Discard(key)`, `Crawl.CanLeave(leftBehind)`.
  UI: greyed spoils/curio items are clickable, the pack shows instead of the map while loot waits (`CrawlUi.LootWaiting`), the hint sits under the scroll (it hid behind Continue inside it).
- DD1 sound samples (dd1_audio_samples.txt): `gen_item_discard` (drop), `ui_dun_loot_take_{gold,heirloom,jewelry,provisions,all,...}` (take). `Dd1Audio.Play("/a/b/c")` maps to sample `a_b_c` by default.
- Debug: Shift+F11 fills the pack with torches, then fights here (full-pack loot test).
- Gotcha again: Git Bash heredocs eat `\\n` in Python strings; write patch scripts with the Write tool.
- WinDrive: "key 0x10 down" + "click" + "key 0x10 up" reaches Unity IMGUI as Event.shift.
- Round 20: DD2 stand-ins carry ambient VFX under their actor (fanatic_flayer: vfx_blood_dripping_from_face, vfx_blood_BB_Face_01/Drip_01, vfx_blade_burning_antic, vfx_fire_particle_small_on_awake, vfx_bright_spark...; every enemy: vfx_shared_death_dots_particle_01 on death). They are ParticleSystemRenderer / VFXRenderer, which the mesh-only stand-in hiding missed. Dd1MonsterView.ModelRenderers now includes them (DD1 sprites are drawn on their own DD3Monsters quad, not under actors). Dd1Backdrop still keeps effects under ActorBhv for the heroes' skills.
- Round 19's "red mist" was this, on a live Bloodletter (brigand_blood has a dead anim; an upright sprite = alive). Last round's log was overwritten by the relaunch: grab log lines before relaunching.
- Round 21: DD1's hand-made maps are `maps/*.dm` (DD_map1-4, crow_map1, town_invasion_0, tutorial_crypts; also NG+/bloodmoon copies under modes/ and dlc/). Format = DD1's binary save format:
  - header 16 × u32: [0] magic 0xB101, [2] header length 64, [5] object count, [6] object table offset, [11] field count, [12] field table offset, [14] data length, [15] data offset.
  - object table, 16 B each: parent, field index, direct children, all children. Field table, 12 B each: name hash, data offset, info (bit 0 object, bits 2-10 name length incl. null, bits 11-30 object index — bit 31 is set on some fields: mask 0xFFFFF).
  - fields are pre-order; data = name + null, then the value: bools unaligned (1 byte), ints/floats/strings/vectors aligned to 4 in the data block; strings and nested files are length-prefixed.
  - map: `base_root.map.{entrance_id, final_room_id (area id hashes), static_dynamic.{static_save (nested file), areas.<rooX|corX>.tiles.tileN.{content, trap, mash_name, mash_type, light, knowledge}}}`; static_save: `areas.<name>.{id, kind 0 room/1 corridor, door0-7.area_to, tiles.tileN.{type, obstacle, door_to.area_to, mappos (2 floats)}}`.
  - crow_map1 is a single room (the crow's lair).
- The Unity port ships its own conversion of DD_map1-4 (Assets/Resources/Data/Maps/*.bytes, its Dungeon.Write format): a cross-check only; the mod reads the user's .dm files.

