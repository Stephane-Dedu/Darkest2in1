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
- Round 22: `.dm` square content codes, matched against DD1's raid saves (Steam userdata/<id>/262060/remote/profile_N/**/persist.map.json, same binary format, tiles also carry `curio_prop`/`trap` = DD1 string hash h*53+c of the prop id): 0 empty, 1 battle, 3 trap, 4 obstacle, 6 room curio (+ battle if mash_name), 7 hall curio, 8 hunger check (no prop), 9 secret room, 10 room treasure, 13 secret door (a corridor square whose door_to is a type-1 side door into the secret room), 14 room (rare, a curio). `mash_type` 3 = has mash_name, 5 = none.
- Static tiles: type 3 room, 2 corridor end (door to a room), 1 corridor square. A corridor's tile0 / last tile door_to = its two rooms. mappos units = the mod's X/Y units.
- DD_map4's 3 obstacles carry a static `obstacle` hash (-697426505) that matches no prop name in props/shared; PlotMap falls back to the zone's obstacle table (rubble).
- The port's DD_map1.bytes room6_mid = ancestors_knapsack + mash_09's four cultists = .dm rooI (code 6 + dd_quest_1_mash_09): code 6 is a guarded room curio/treasure.
- The user's DD1 saves are read-only references: never write there.
- Round 23: DD1 `named:` mash rows (`.name X .chance N .types ...`) live in the zone level files next to hall/room/boss rows; a name can repeat per level with level-matched monsters (weald.1/3/5 crow_1 = nest_A crow_A / nest_B crow_B / nest_C crow_C) and summon_mash repeats 10× in one level (weighted pick). BattleLoot keeps them by zone → level → name.
- Level choice (BattleLoot.Level): the exact difficulty's file when the zone has it (town.6 for the town invasion), else the nearest to min(difficulty,5). Regular quests (1/3/5) are unchanged.
- Probe trick: a throwaway xunit test with ITestOutputHelper + `dotnet test --filter ... --logger "console;verbosity=detailed"` prints Core facts quickly (delete it after).
- Round 24: Darkest Dungeon monster families (cultist_orgiastic/shrouded/warlord/harpy, totem_attack/guard, templar_melee/ranged + _mb minibosses in their own folders, errant_flesh_bat/dog, cyst, cell_white/battle) and crow got stand-ins. Picked from a probe listing every DD2 enemy with the same size whose usable skills fit the DD1 role (Dd1MonsterSkills.Role/Compatible/Allowed), then by theme. nest_A-C: no skills, 0 turns/round, life_link crow, tag boss.
- monsters.json is hand-formatted (one aligned line per family): edit it as text, not with json.dump (that rewrote 365 lines).
- Round 25: plot quests carry `quest.map_name` (QuestOffer.MapName); Embark.Create loads PlotMap when maps/<name>.dm exists (only the Darkest Dungeon's parts reach the board; town invasion / crow are event-generated, tutorial not offered). Goal curios: DD1 goals inventory_activate_beacons (curio beacon ×3, starting items beacon_light ×3) and activate_teleporter (curio teleporter ×1); the .dm has no goal marker, PlotMap.PlaceGoal uses the code-6 rooms whose set fight isn't `<quest>_mash_NN` (miniboss/teleport rows), as the port has them.
- Boss fights stay DD2's (Driver.StartFight: kind Boss → Zones.Plan with quest.BossId), so the final room needs no mash.
- Debug: Shift+F4 (test estate only) opens the Darkest Dungeon: crypts zone XP 100000 (level 6), roster resolve ≥ 5, new board. Back up estate_2.json first (LocalLow/RedHook/Darkest Dungeon II/DarkestDungeon3/estate_2.json) and restore after: the test scripts expect the usual board.
- Quest map: the Darkest Dungeon node is at client (1082,156). Crawl map squares are 20 px client with gaps: click a square's centre (or use F3).
- Round 26: in-game checks on the test estate (week 60): stagecoach 2 recruits / roster 9 (DD1 bases), recruits have quirks, wagon 2 trinkets. Hamlet click points (client px): Nomad Wagon (900,690), Stage Coach (262,640), building window close X (1252,137).
- The wagon's trinkets are DD2 items (Dd2Catalog.RandomTrinket by DD1 rarity), not DD1 trinket ids; its rarity now follows nomad_wagon.building.json rarity_generation_table.
- Flaky tests fixed: ZoneBase.Extra was a static Dictionary written by every ZoneEncounters.Load; xunit loads RegionTests and ZoneEncounterTests in parallel ("Operations that change non-concurrent collections must have exclusive access"). Now a ConcurrentDictionary.
- Round 27: DD1 death_class rows (monsters/<family>/<class>/<class>.info.darkest): `death_class: .monster_class_id corpse_A|B|C|D|corpse_large_A|B|C .is_valid_on_crit False .type "corpse"` on 128 of 252 classes; is_valid_on_bleed/blight/burn_dot unset (= false) except the Ancestor's special classes. Ruins level 1: maggot_A, spider_spitter_A, spider_webber_A have none.
- DD2: a killed stand-in's ActorDataId becomes its death class right away (lost_battalion_foot_soldier → ..._corpse); CorpseRule vetoes it in GetIsDeathClassValid.
- Debug: Shift+F10 strikes only the first living enemy with an ordinary (non-crit) blow — for corpse checks. F11 at the entrance rerolls the encounter each fight.
- Round 28 (decomp): ActorInstance.Kill → EventActorDeath(isDeathClassValid); ActorInstance's own handler then triggers EventActorChangeClass(guid, old, m_DeathActorDataId) (same guid) and spawns m_DeathFront/BackActorClassIds — no EventCombatActorDeath for a corpse-leaving death. EventActorChangeClass is `internal` (CS0122 from the plugin): watch Dd2Api.Actor(guid).ActorDataId instead. DD2 may destroy and rebuild the CombatActorBhv (the cutthroat's case): re-find it by guid.
- DD1 corpses: corpse_A-D / corpse_large_A-C have only info.darkest (hp 7, 0 turns, life_time .alive_round_limit 3) and art.darkest (commonfx .deathfx death_corpse_medium, health_bar corpse); the picture is the dead monster's own `dead` sprite.
- Test-driving gotchas: debug damage outside a skill (F10/ApplyHealthDamage) isn't resolved into a corpse/class change until combat moves on; a fight whose enemies all died still waits for the current hero's action before DD2 calls victory; F11 at the same spot rerolled the same encounter 5 times in a row (round 28). Shift+F10 now leaves the front enemy at 1 HP for a hero to finish.
- Round 29: CombatPresentationBhv.AllActors order isn't rank order (Shift+F10 hit a back skeleton); Shift+F10 now takes the lowest TeamPosition. A monster left at HpRaw-1 showed "0/41" and stayed up. Scripted skill clicks (skill bar client y 756, x 550/630/710/790/880; enemy rank 1 ≈ (930,470)) only work when the active hero has a front-reaching attack — unreliable for corpse checks. DD1 health bar inspect panel shows the DD1 name (Bone Rabble) with the DD2 type underneath (Cadaver = lost battalion foot soldier's type, alive).
- Round 30 (user request: DD1's introduction/cinematics/characters on a new estate): DD1's new game lives in `scripts/starting_save/persist.*.json` (plain JSON with trailing commas; persist.roster.json: Reynauld crusader [warrior_of_light, kleptomaniac, god_fearing — god_fearing is NEGATIVE in DD1], Dismas highwayman [hard_noggin, known_cheat, quick_reflexes], stress 10, hp 33/23, skills; persist.estate.json wallet; persist.raid.json the old "tutorial" raid). `scripts/starting_roster.darkest` names every class's DD1 hero (Barristan, Reynauld, Dismas, Junia, Paracelsus...). Names: localization/names.string_table.xml hero_name_reynauld/dismas. Cinematics: video/house_of_ruin.ogv, old_road.ogv, epilog.ogv + .sub (ms start,end per line; text = miscellaneous.string_table.xml str_vo_house_of_ruin_0..28, str_vo_old_road_0..15).
- Estate slots: the picker offers slots 1-3 (UiRoot), plates at client y 475 (slot 2) and 667 (slot 3). To test a new estate without touching a slot, move its estate_N.json(+.bak) aside, test, delete the new one and move the originals back (cmp to confirm). estate_3.json (week 2, 4 heroes, Oct 3) is kept that way.
- Round 31: Unity's VideoPlayer in DD2's built player refuses DD1's .ogv ("VideoPlayer cannot play url"); it does play VP8 .webm (ffmpeg -an -c:v libvpx -b:v 3M -deadline realtime -cpu-used 8: Old Road 22 s / 24 MB, House of Ruin 42 s / 49 MB on this PC). ffmpeg here: WinGet Gyan.FFmpeg (CinematicCache finds PATH or %LOCALAPPDATA%/Microsoft/WinGet/Packages/**/ffmpeg.exe). DD1's .ogv streams: Ogg Skeleton (fishead) + Theora + Vorbis; Dd1Cinematic.VorbisAudio keeps the Vorbis pages → a valid .ogg for FMOD createSound(CREATESTREAM). UnityEngine.VideoModule.dll had to be added to C:\Users\Piral\dd2-decomp\refs (the compile-time refs copy).
- VideoPlayer gotcha: a replaced (skipped) player's callbacks can still fire; guard every callback with `vp == current`.
- PENDING (round 31): slot 3 holds a throwaway test estate; the original estate_3.json / .bak are in the save dir as estate_3.json.r31moved / estate_3.json.bak.r31moved (and a copy in the session scratchpad estate3_keep). Restore once the game is closed — the user was at the PC (idle 0.6 s) and clicked in the game during the test.
- Round 32: DD1's opening raid = scripts/starting_save/persist.raid.json (raid_instance: id tutorial, explore, weald, difficulty 1, length 1, goal tutorial_final_room {type tutorial_room, data.room_id rooB}, reward 5000 gold + resolve_xp 2; party.heroes [1,2] = roster keys (Reynauld, Dismas), torchlight 100, inventory 2 provisions; in_area rooA) + persist.map.json (3 areas: rooA, coAB (6 squares: tile2 content 1 mash_index 0, tile4 content 7 cur=hash(travellers_tent_tutorial)), rooB (content 6, mash_index 1, cur=hash(bandits_trapped_chest))). mash_index N ↔ weald.1.mash.darkest named `tutorial_{N+1}`: tutorial_1 = brigand_cutthroat_A, tutorial_2 = brigand_blood_A + brigand_fusilier_A. `cur` = Dd1Binary.Hash(curio folder name) (h*53+byte, uint). entrance_id/final_room_id there are NOT that hash of the area names — use in_area and the goal's room_id.
- Restored estate_3.json/.bak (round 31's pending item) after the game closed; the throwaway test estate went to the scratchpad.
- Round 33: starting a DD2 run from Driver.Update the frame the cinematics ended froze on "The party sets out": Player.log `NullReferenceException at EventManager.TriggerEvent_Recursive ← EventGameTypeStarted.Trigger ← GameTypeMgr.OnGameTypeStarted ← RunBhv.StartRun` (21 DD2 listeners on EventGameTypeStarted: ActorInstance, RosterManager, StageCoach, KingdomMapManager, TutorialEventsBhv...). A normal embark (UI click, Hamlet shown a while; Player.log shows DD2's MainMenuUiScreenBhv closing first) worked on the same build. Fix (unverified): the opening goes through Driver.Embark from OnGUI after 3 s on DD1's Old Road loading screen.
- USER (2026-10-04): "don't relaunch the game until I tell you; keep fixing what can be fixed without checking in game". Until told otherwise: no launches, build with -p:Deploy=false while the game runs, mark game-facing changes [?].
- Estate 3 restored again after the round-33 test (identical to the backup).
- Round 34: plot_tutorial_crypts = progression plot quest, dungeon_level 0, explore, map tutorial_crypts.dm (8 rooms/8 corridors, entrance rooH, guarded treasures tutorial_mash_03/05/01, guarded curio tutorial_mash_04, corridor fight tutorial_mash_02, traps, curios, a hunger square, obstacles), has_statue_contents true. Offered by QuestBoard.PlotOffers now (filter: progression and (not explore or has a map)). Existing estates already list it in CompletedPlotQuests (prepaid by older builds) and won't see it again.
- Round 35: QuestBoard.PlotOffer(estate, dd1, plotQuest) builds any plot offer (used by the board and by plot_quest town events). DD2 has no bird enemy/battle; the crow's boss stand-in is config:carrion_my_wayward_son_c (shared_carrion_eater_mutated + 2 carrion eaters + shared_dog_rabid_b) in combat_arena_forest_dungeon_exterior.

## Round 36: DD1's failure resolve bonus lasts until a completed quest
- Reference: shared/buffs/base.buffs.json, darkest_dungeon_failure_roster_resolve_xp: resolve_xp_bonus_percent 1, duration_type quest_complete, duration 1. Embark used to clear all pending buffs, so retreating lost this bonus with no XP earned.
- Embark copies quest-complete buffs into the expedition and keeps them on the hero. Homecoming consumes the participating survivor's copy only after successful XP calculation. Ordinary town buffs still leave the hero at embark; heroes who stayed home keep their bonus.
- Verified: Release build with Deploy=false and all 138 Core tests pass. The regression covers two retreats, serialization, a successful short quest earning 4 XP, the next earning 2 XP, and an idle roster hero retaining the bonus. No game launch or save edits.
- Audit gaps queued in PARITY.md for separate rounds: unseen traps incorrectly roll a disarm; scouting ignores DD1's normal/critical square budget.

## Round 37: unseen traps spring without a free disarm roll
- Reference: props/trap_definitions.json spikes health -0.25; Unity port Assets/Scripts/Managers/RaidSceneManager.cs TrapEvent only tests disarmChance when handActivation is true. Crawl used the disarm roll even when the trap was unseen.
- TriggerTrap now rolls only for a deliberate spotted-trap interaction. Walked-into traps apply their damage/effects directly. Selected-hero disarming remains unchanged.
- Verified: Release build Deploy=false, all 140 tests pass. New theory covers 32 seeds for each activation type with real DD1 trap data. No game launch. Queued spotted-trap movement bypass separately in PARITY.md.

## Round 38: passing a spotted trap springs it
- Source correction: the port method is TrapEvent, not InvestigateTrap. RaidTrap.OnTriggerEnter2D always uses ActivateTrap(false); spotting allows a click to disarm but walking past still springs the trap.
- Core Step now resolves an armed trap without a disarm roll before forward movement. Retreating off its square leaves it armed. This closes the bypass in direct movement calls; the UI already stops auto-walking at spotted traps.
- Verified: Release build Deploy=false, all 142 tests pass. Regression covers backing away, springing it, and moving after resolution. No game launch.
- Next gap found from actual data: ordinary obstacles cost 5% HP, Stress 2, and 20 torchlight without a shovel; ancestor obstacles override all costs to zero. Queued separately.

## Round 39: obstacles use DD1 costs and named overrides
- Reference: props/prop_definitions.json obstacle.default_data health -0.05, torchlight -20, fail_effects [Stress 2]; effects/base.effects.darkest Stress 2 is 15 stress. obstacle_definitions.json ancestor overrides costs and effects to zero with ancestor_talk=true. Port ObstacleEvent applies these only on a bare-handed clear.
- Added ObstacleLibrary with inherited defaults and array-replacing overrides, loaded by CrawlContent. ClearObstacle uses its HP/light/effects; ancestor does not consume a shovel. Without loaded content the fallback now uses 5% HP, 15 stress, 20 light.
- Verification caught a mistaken test assumption that Stress 2 meant 20; the actual effect is 15, rounded to 1-2 DD2 points. Corrected the expectation, not the data-driven effect application. Release build Deploy=false and all 146 tests pass. No game launch.

## Round 40: scouting follows squares and critical chance
- Reference: shared/rules.json scouting_crit_success 0.50; Unity port RaidSceneManager.ScoutingEvent chooses 6 or 12 squares, ScoutingHallway spends the remaining budget across corridor tiles before revealing a room and branching. Core previously revealed all rooms within two graph edges, even behind long halls.
- CrawlRules reads the critical chance. Crawl uses normal/critical distance; DungeonMap.ScoutFrom handles branch budgets, reverse order, cycles and repeated reveals.
- Verified: Release build Deploy=false and all 154 Core tests pass. Eight new cases cover length boundaries, reverse travel, cycles and integration. No game launch.
- Found a separate defect: Begin calls normal room scouting at the entrance despite DD1's zero entry chance. Corrected PARITY's old [x] claim and queued an entry-specific rule for the next round.

## Round 41: dungeon entry uses DD1's separate scouting chance
- Reference: shared/rules.json scouting_enter_dungeon_scout_chance 0.0, distinct from scouting_chance_base 0.25. Begin was rolling the ordinary chance with the radiant-light bonus.
- CrawlRules loads ScoutEntryChance. Begin identifies dungeon entry; room scouting uses the entry chance without adding the light bonus. Subsequent room arrivals retain ordinary and critical scouting. No arbitrary sleep or game hook needed.
- Verified: Release build Deploy=false and all 155 Core tests pass. Regression checks 32 seeds with full light and a forced 100% normal room chance, plus an entry override. Round-40 integration tests now travel to a new room before testing normal/critical distance. No game launch.

## Round 42: the pack UI respects Core's food refusal
- Reference: Unity port InventoryItem.cs provision-use activation requires a selected hero with HealthRatio < 1. Crawl.UseSupply already enforces a living wounded selection. CrawlUi.UseItem bypassed any refusal by directly consuming another provision and healing.
- Removed the duplicate UI food path. Core now owns the consumption and configured heal amount; successful food use still persists and announces through the existing path.
- Verified: Release build Deploy=false and all 155 Core tests pass. Expanded food regression checks no selection, a missing hero, a dead hero and full health without consuming food. No game launch.

## Round 43: minimap preserves DD1's plot-corridor turns
- Reference: maps/town_invasion_0.dm nested static_save.areas.<corridor>.tiles.<tile>.mappos. PlotMap.Build retains these positions in HallTile.X/Y; CrawlUi.HallPos flattened them onto the line between endpoints.
- HallPos now projects the saved square positions for plot maps. The same helper places square icons, click targets and the party indicator. Generated maps retain existing fitted spacing.
- Verified: Release build Deploy=false and all 155 Core tests pass, including all plot-map readers. No artificial UI test for this direct projection. Visual verification remains [?] because launches are forbidden.

## Round 44: Nomad Wagon no longer draws stash text over the keeper
- Reference: campaign/town/buildings/nomad_wagon/nomad_wagon.layout.darkest defines grid pos 230,150, six columns, start 55,-10, offset 100,180. It has no stash-help line; round 26's screenshot showed the mod's line over the keeper.
- Removed that permanent hint. Trinket details now use the existing multiline Gui.Tip with rarity color when hovering a stock item. Buying, prices and stock remain on the existing path.
- Verified: Release build Deploy=false and all 155 Core tests pass. No game launch; visual check remains [?].

## Round 45: Guild uses DD1's hero header and description layout
- Reference: guild.layout.darkest skill_pos 44,-4, spacing 0,91 and requirement_spacing 75,0; port GuildHeroWindow/HeroOverviewWindow loads class/name/action_verbose_body_guild_<class>. EstateManagement.unity GuildOverview contains CharDescriptionFrame 236x478 and one SkillGroups column.
- Guild now reuses HeroSlot/HeroVerbose from the Blacksmith. Skills occupy a clipped scrolling column beside the description, keeping DD2 learning/mastery and five-skill choice. Removed the arbitrary 12-skill limit. Scroll resets when selecting another hero; tooltips use the shared overlay after the scroll group closes.
- Verified: Release build Deploy=false and all 155 tests pass. No game launch; visual and interaction checks are explicitly [?]. Requirement/cost decorations are a separate next round.

## Round 46: Guild rank markers and cost frames match DD1 art
- Reference: guild.layout.darkest requirement_spacing 75,0, icon_cost_offset 34,70; GuildHeroWindow.InitializeTree uses the shared upgrade states and connectors. Assets read at runtime: guild/skill_frame.png 88x88; blg_townupgrade_costframe.png 103x139; requirement_* icons/backgrounds.
- Added the skill frame, purchased connector, purchased/available/locked mastery markers and a compact 64x86 cost frame within each 91-pixel row. Missing gold keeps the available marker with red price; actual resolve/building prerequisites show locked. Hover explanations cover purchased and locked states as well as buying.
- Verified: Release build Deploy=false and all 155 Core tests pass. UI art is unverified in game; no launch.

## Round 47: DD1 graveyard records and scrolling
- Reference: graveyard.layout.darkest list height 600, entry_spacing 20. Native assets inspected: dead_hero_backdrop.png 600x118; 0_1/2..6.png 118x118 tombstones. Port GraveyardWindow/DeathRecordSlot creates scrollable records with resolve icon, portrait, name and cause.
- Replaced unclipped plain text with a 600-high clipped scroll view and native record/tombstone art. Uses DD2 class portraits, DD1 resolve titles, saved cause and week, and DD1 unknown-peril text for a missing cause. Records stay in stored order.
- Verified: Release build Deploy=false and all 155 tests pass. No game launch or save modifications; visual check remains [?].

## Round 48: Memorial video page and replay rules
- Reference: statue_media_info.json categories sorted by sort_priority, three videos, epilog access_if_plot_finished plot_darkest_dungeon_4; statue.layout.darkest list 600x580. The statue had opened the generic town log.
- Added Core Memorial catalog and access/visibility rules, with tests. Added a distinct Memorial panel using the runtime category/backdrop/button art and localized titles. House of Ruin/Old Road can replay; epilogue is locked until the final DD quest. Replays use existing CinematicPlayer; epilogue cache conversion queues in the background after unlock.
- Verified: Release build Deploy=false and all 157 tests pass. No game launch or cache generation during this session. Boss narration and collected backer journals are still separate missing work, recorded in PARITY.md.

## Round 49: Stagecoach drag ownership and recruit-detail modal

The owner interrupted general parity work with priority regressions: recruitment fails/freezes, detail portraits are missing, combat setup is slow, lighting is dark and monsters glow. The activity-log work has not been started. Earlier coach verification only showed two recruits and a full roster; it did not prove a hire.

Drag activation formerly happened only inside the source draw, after other page controls had already run. Drag.Begin now promotes from the saved press before drawing controls, reserves carried drag/release events (Drop still uses rawType), and clears capture on drop/cancel/Escape. Source presses inside scroll groups use GUIToScreenPoint for the threshold. Hamlet handles RecruitDrag on the roster before its click handlers. Recruit details are now a modal: the underlying page is disabled, Drag/roster right-clicks respect it, and GUI.enabled is restored in finally. A new event-only Unity shim compiles the actual Drag.cs; it does not simulate native controls or rendering. Two replay cases were red before the fix; six now pass, including real Hamlet.Recruit. Release build and 157 Core tests pass with Deploy=false. No DD2 launch, save write or deployment. Check native dragging, details close/cycle, invalid/cancelled drops and repeated hires with capacity in game.

## Round 50: hero detail figure above its opaque backdrop

The missing lower-left character image has a confirmed draw-order cause. DD1 shared/character/character.layout.darkest puts hero_pos at 98,700. HeroSheet drew its 220x450 picture before characterpanel_frames.png. Reading that PNG alpha channel at the overlap gives 100% coverage over alpha 128 (92,550 of 99,000 pixels fully opaque), so the next draw erased the hero. tools/check_hero_sheet.py is a read-only artwork/source-order oracle: it failed before the change and passes after moving the figure above the frames. HeroFigure also falls back to painted Story, then the embedded Story and Color portraits. All checks pass (157 Core + 6 input tests, Release Deploy=false). Native details/cycling remain unverified; no launch or save write.

Separate hardening gap: Sprite.textureRect throws for tight atlas packing (Unity 2022.3 docs), used in Art.DrawSprite and its addressable completion log. No corresponding exception in the currently available session log; tracked separately.

## Round 51: preserve hero exposure and suppress flat-art bloom

DD2 PostProcessingManager.SetEffects toggles volume active flags per camera. The backdrop previously disabled ColorAdjustments as a whole, which removes postExposure along with arena tint, but left Bloom and Vignette active. It now keeps ColorAdjustments active/exposure under native control and neutralizes its colorFilter, contrast, hueShift and saturation only. PostEffectGuard tracks original field/parameter values, re-applies held values after native skill toggles, and restores them at End. Bloom and Vignette are suppressed alongside the existing flat-art blur filters. Camera scenery masks explicitly preserve all active Light layers (DeferredRenderPass filters visible lights and has exclusive light masks).

This targets observed symptoms but cannot establish the final rendered brightness without the game. Three bridge tests exercise actual guard reflection: preserve changing exposure, hold shared/per-camera bloom despite native toggles, restore distinct originals, ignore absent parameters. Release build, 157 Core and 9 bridge/input tests pass. The owner asked about local versus remote work: fixes are now deployed locally as well as pushed; DLL SHA-256 matches the Release output. Before deployment, um backup snapshot saved the seven installed mod files at C:\Users\Piral\.universal-modder\backups\darkest2in1-plugin\20261004-133811.zip. No game/config/save files changed beyond the installed authored plugin/data; no game launch.

New priority: Hamlet first-load art latency. SpineArt rasterizes each whole image synchronously on the UI thread; the 40 ms budget only gates between images. Measure actual DD1 bakes and move CPU rasterization to a worker.

## Round 52: town art bakes leave the UI thread

Measured the real DD1 town skeletons/atlas pages with tools/SpineTiming: Stagecoach idle/active 9.7/13.7 ms, Blacksmith 24.8/46.7, Abbey 30.6/56.4, Tavern 29.7/49.8, ground 219.8; total 481.2 ms. These are .NET 8 CPU raster times, excluding PNG decoding and Unity upload, not a claim about the native game's exact timings. The old 40 ms budget checked only between images and could not interrupt the ground bake. SpineArt.Get now queues parsing and one CPU raster worker; Update alone decodes/uploads using Unity, with a 4 ms budget between non-preemptible operations. It retains the original supersampling, pixels, pivots and hit tests. Common art queues during menus without reading an estate; hover pictures queue only on hover. Each job owns its page snapshot, so ReleasePages cannot invalidate a running raster.

The linked-source test failed against the previous commit's SpineArt.Get, which returned a baked image synchronously. It now passes with the actual DD1 Stagecoach geometry and a synthetic PNG/Unity shim, checking that texture APIs stay on the pumping thread, cached requests do not rebake, uploaded rows preserve orientation and hit tests work. This is not a native renderer/decode test. Release build, 157 Core + 10 bridge/input tests pass; the local plugin is deployed. No game launch or save/config write. Native first-entry responsiveness and appearance remain [?]. Owner added a trinket priority: absent effects and DD1 policy/UI mismatch; recorded separately for the next rounds.

## Round 53: trinket effects survive cold Hamlet lookups

DD1 port Character/Trinket.ToolTip lists title, rarity, requirements and every Buff.ToolTip. Our ItemText depended on DD2's run-only Item library and cached null, preventing an item from ever acquiring effects after a cold-menu hover. A linked-source shim replay was red: no library, then a populated library, still no effects. ItemText now caches only successful native descriptions, invalidates across native library/language changes, and uses TryGet only after initialization. A worker reads authored DD2 CSV stats and source localization for cold-menu fallback, with runtime localization preferred. Common trinkets show actual resistance bonuses/penalties, HP multipliers and CRIT percentages; source {q} sprite markup becomes readable icon words. No DD2 library is initialized or modified by this fallback.

Release build, 161 Core + 11 bridge/input tests pass. File tests check actual installed trinkets and both native number units, localization fallback and rich-text conversion. This closes the permanently-empty cache and common stat descriptions; cold-menu conditional/triggered effects are a separate remaining gap (withheld rather than falsely described as unconditional). Native tooltips after a run remain the complete preferred source. No launch/save/config changes. Next: validated equip transfers, tooltip fonts/colors/grid, combat startup.

## Round 54: atomic DD1-style trinket transfers

DD1 base.entries.trinkets.json limits are not all one: 330 entries limit 1, 159 unlimited (0), one limit 3. CharEquipmentPanel and RealmInventoryWindow validate copies/class restrictions, disable unavailable destinations and swap slots. Added Core TrinketEquipment, with the mapped DD2 item's actual m_possessionLimit supplied by the catalog. It validates both ends before any mutation; stale drag sources cannot manufacture an item. Swaps return displaced items to the source slot/stash position, rejecting a reverse class/copy mismatch. Same-hero left/right moves now work. Drag payloads identify source slots, so duplicate stash icons do not all disappear during a drag. Empty left slots are represented by a null placeholder in the existing save list; WornTrinkets excludes empties from warnings, dismissal, town losses, native item application and display. The computed view is JsonIgnore; old saves still load. Unequip-all skips busy/missing/dead heroes as the DD1 port does.

Release build, 166 Core + 11 bridge/input tests pass. Five new rule tests verify conservation/refusal, unlimited versus limited copies, source/destination swaps, same-hero moves, right-only JSON roundtrip and unavailable/unrecruited heroes. One concurrent test run hit a shared MSBuild obj-file lock; its sequential rerun passed (not a code/test failure). No launch or save write; native mouse checks remain [?]. Next: DD1 tooltip fonts/colors and inventory presentation, remaining cold-menu effects and combat startup.

## Round 55: DD2 becomes the main campaign geography

Owner requested a larger refactor after round 54: DD2 areas become primary, DD1 areas remain optional through current toggles; choose backgrounds for coherent, fluid UI/UX. Updated CLAUDE.md and the loop/handoff pointers so later sessions retain this superseding direction. DD1 remains the source for expedition rules, maps, camping, loot and economy; combat remains native DD2.

Core CampaignRegions owns the four default destinations (Sprawl/Foetor/Tangle/Shroud), optional original areas and Sluice, toggle defaults, mapped unlock thresholds and versioned migration. QuestBoard now offers only enabled/unlocked regions and guarantees each open area a regular contract. Native lair tiers still use DD1's level/difficulty/reward cadence; the Darkest Dungeon chain still reads the strongest open region. The introduction and crow event can retain their DD1 story maps/objectives in a native region; legacy boss chains stay with optional original areas. Toggling does not reset XP, and duplicate story offers are filtered.

Migration is in memory on normal estate load: copy legacy XP only when higher than the native region's current XP, preserve originals, explicit toggles, completed plots and quest seeds/rewards; translate ordinary destinations, remove disabled board entries, and add reachable plot offers once. Active expedition state is outside this migration. New estates start at the current layout version. Found and removed Session.Migrate's stale automatic tutorial payout/completion, which defeated round 41's playable tutorial at the integration layer. No actual saves were touched for these tests.

Release build and 170 Core tests pass. Four new migration/default/toggle tests pass; nine earlier assertions intentionally expected DD1 defaults and were updated to the new directive. Legacy boss tests explicitly enable the original area; map assertions check its rules template, and the endgame test progresses a native region. Presentation/quest-board UI is the next round; current changes are not yet deployed. Native migration/playthrough checks remain [?].

## Round 56: paired map areas with the existing arrow controls

Owner proposed mapping two independent areas to one map position and switching with the existing arrows. The quest board now pairs Sprawl/Ruins, Foetor/Warrens, Tangle/Weald and Shroud/Cove at quest_select.layout.darkest's map positions, reusing shared/character/previous_hero.png and next_hero.png. Enabled Sluice is a third Warrens-position variant. Disabled variants disappear; enabled locked variants explain their unlock threshold. Area focus selects its first contract and retains only eligible party members. Separate map selection state never combines XP, quest identity or boss completion.

Previously DD2 areas stacked at x=130 beneath the left detail scroll. Only the focused area's contracts now appear in a clipped five-column strip from 570,650 to 1550,830; the 125..525 detail panel, map labels ending at y=599, roster starting at x=1630 and party title starting at y=860 remain clear. Contract lists scroll without a hard limit. Region settings now display all primary and original choices with the correct default-on state and are painted last with underlying inputs disabled. Added a close button and consumed Escape. Updated the menu campaign label.

Release build, 171 Core and 11 bridge/input tests pass. The new integration test uses the actual DD1 level thresholds and proves paired Ruins/Sprawl contracts and boss progress remain independent, including disabling a primary and adding Sluice. Local authored plugin/data deployed; no game launch or save write. Native arrow input, map fit, scrolling, toggles and provisioning remain [?]. Next is native region arena presentation, faction coherence and startup.

## Round 57: native DD2 region presentation and coherent encounter routes

FightPlan now marks native presentation explicitly. Dd2Combat passes it to Dd1Backdrop.Reset, which restores previous overrides and skips backdrop setup, renderer scans/culling and post-processing overrides for DD2 regions. Existing crawl art remains the exploration fallback and the loading cover; the native fight fades through over 0.5 seconds after DD2 mode entry. Optional original areas retain flat DD1 presentation. Combat logs now report synchronous roll/scenario/setup duration, elapsed native mode entry and reveal readiness. These are instrumentation, not measured native improvements yet.

Read the installed DD2 battle_configuration_data_export.Group.csv and local CombatScenarioData decomp: lair battles carry m_BackgroundSceneOverride for their native interiors, and scenario initialization honors it before additive scene loading. Existing mappings point to those native battles and scenes. Native camping now uses the same regional room faction/tier instead of global camp_mashes_master, while preserving AMBUSH source semantics. Corrected CampaignRegions.StoryRegion's event/quest ID mismatch: the event is plot_quest_crow_trinket but the actual plot is plot_crow_trinket. Tangle now has the existing carrion_my_wayward_son_c mapping so that migrated story does not become an ordinary fight.

Removed the invented Exemplar lair from newly generated Sluice boss chains. Its old explicit encounter mapping remains for saved offers. Region-option descriptions now show player-facing places/enemies/bosses rather than borrowed-template details, including DD1 variants. Release, 177 Core and 11 bridge/input tests pass, with regional ambush tests at all three difficulties plus crow/Sluice routing. Deployed locally and confirmed plugin SHA-256 matches Release. No launch or save write. Native lighting/latency/transition checks remain [?]. Next: repeated legacy setup scans and the premature monster binding deadline, then trinket presentation.

## Round 58: bounded setup polling and binding timeout after load

DD1MonsterView.Prepare started its eight-second binding deadline before DD2 entered combat. A slow arena load could exhaust it on the first bind attempt. DeferredPoll now gates binding until CurrentMode is COMBAT and the mode manager is no longer changing state; its timeout begins at the first ready poll. Binding and legacy backdrop setup retry at most every 0.1 seconds. Monster rendering avoids allocating its screen texture before any enemy is bound.

Backdrop setup previously traversed scene roots for scenery, again for ambient effects and again via a global renderer search for particle layers. One local renderer snapshot now feeds those steps. Already hidden renderers seed the dynamic scan's seen set, and that scan waits half a second after setup. New combat timing logs report the successful backdrop setup's synchronous cost and elapsed readiness delay. No native timings are claimed without a permitted playtest.

Release, 177 Core and 13 bridge/input tests pass. Two linked helper tests prove a 60-second simulated arena load does not spend the binding timeout and polling is bounded at 120 FPS, including reset for a second fight. Deployed locally; no launch/save write. Native cold-load binding, close-up scenery and timing checks remain [?]. Next: trinket tooltip colors/fonts and clipped inventory rows.

## Round 59: DD1 trinket typography, palette and clipped inventory rows

Read installed colours/base.colours.darkest and the Unity port's Trinket.ToolTip. Equipment title resolves to notable 200/180/110; body resolves to installed neutral 174/172/162, which differs from the port's older hard-coded neutral. Dd1Colours reads numeric/hex RGBA entries and resolves shared_id safely; Dd1Palette converts that owned runtime data into Unity colors. Trinket tooltips now keep the name gold, rarity on a separate colored line, class requirements and effects neutral. DD2 labels remain; epic uses DD1 very_rare and cultist harmful colors. Two actual-file/parser tests verify colors and alias-cycle fallback.

Gui equipment tooltips use Ubuntu bitmap text and measured wrapping; generic tooltips also use their actual heading/body font metrics. Found another concrete cause of apparently missing effects: CrawlUi drew trinket text in a fixed 400x60 box. That HUD path and all hero/stash/Wagon/loot paths now use the shared tooltip, measured and positioned inside the canvas. Tooltip effect availability still depends on round 53's native/cold-menu reader; complex cold-menu effects remain separately tracked.

Installed realm_inventory.layout.darkest specifies 560x525 grid viewport, 80x160 cells and scroll_max_visible_rows=4. A fourth row is partly visible rather than four full rows stretched past the panel. The stash now uses that clipped viewport and pixel scrolling, retaining original grid/arrow art. Drag/hover/sell source input is restricted to the viewport so a clipped offscreen item cannot catch a click. Release, 179 Core and 13 bridge/input tests pass; deployed and SHA-256 verified. No launch/save write. Native typography, long tooltip positioning and scrolled drag checks remain [?].

## Round 60: conditional trinket stat descriptions before native libraries exist

Audited the actual base tables with the built Core reader: among 191 base trinkets, 43 descriptions were complete, 46 partial and 102 blank. Read DD2 BuffDescription/ConditionDescription and installed condition CSV/localization templates. The native description wraps a whole buff in its condition. The cold reader now does that for supported visible skill tags, HP/stress thresholds, rank/round/turn, Flame/run values, biome/status and equipment conditions. Authored condition overrides are used only if they retain the bonus. Hidden/unknown conditions and malformed localized format strings withhold that conditional bonus rather than display it unconditionally.

CSV rank values are already one-based: native initialization subtracts one and its formatter adds it back, so the offline reader leaves them alone. Comparison templates carry leading spaces; preserve those through intermediate formatting and normalize only the finished description, preventing 'Flameis'. HP percentage thresholds multiply by 100; Flame raw values do not. The actual audit is now 46 complete, 54 partial and 91 blank. Event-triggered effects remain the main separate gap.

Release, 183 Core and 13 bridge/input tests pass. Added four actual-data cases for Melee/Ranged restrictions, 33% HP, Flame units/spacing and bad localization withholding only the conditional stat. Deployed locally; no launch/save write. Native cold-menu hover checks remain [?]. Next: triggered token/effect descriptions, followed by atlas-safe drawing and remaining campaign/UI gaps.

## Round 61: direct triggered trinket effects in the cold Hamlet

Read native ActorDataEffectDescription, EffectDescription and installed effect_data_export.Group.csv. Direct event groups use effect_tooltip_skill_effect_<event>; supported effects now show token additions, simple Stress/HP changes, quantities, chance suffixes and visible per-effect conditions. Localized token_name_<id> is preferred to icon identifiers. Effect-field whitelist prevents advanced target/duration/chance fields from becoming guessed simple effects; unsupported rows leave the description incomplete. ActorEffectTrigger routing, DOT/buff mutations and advanced conditions remain separate work.

Found native rich text contains non-.NET braces such as #{debuff} and {q}. Composite formatting previously rejected those templates. Normalize markup before formatting, preserving comparison templates' leading separator space. Adrenalizing Ash now shows Turn End: Speed (15%) and Gain On Miss: Daze (20%); Bulwark Band shows Dodge/Blind and their triggers; Gnarly Knuckles/Raven's Reach include the skill-specific +1 Stress on miss. Two new actual-data tests cover events/chances and malformed localized effect withholding. The earlier skill-condition cases now assert their descriptions are complete.

Actual base-game audit is 64 complete, 71 partial and 56 blank among 191 trinkets, compared with 43/46/102 before rounds 60-61. Release, 185 Core and 13 bridge/input tests pass; deployed and DLL SHA-256 verified. No launch/save write. Native cold-menu appearance and remaining complex effects remain [?]/[ ]. Next: remaining effect coverage, atlas-safe portraits, then persistent log/secret rooms/Memorial.

## Round 62: homecoming preserves trinket slot identity

Read DD2 ItemInventory.GetItemIds: it returns only valid occupied entries. Dd2Heroes.ReadBack passed that compact list to Homecoming, which assigned it directly and lost a right-only layout. DD1 port CharEquipmentPanel has distinct left/right slot/drop/swap handlers. Homecoming now calls Core TrinketEquipment.RestoreSlots: retain surviving original item identities/counts in their slots, leave loss gaps, fill vacancies with returned new items, and trim only trailing empty slots. No native actor or estate state changes before the existing homecoming application.

Two tests exercise actual Homecoming.Apply with a right-only item and the reconciliation of loss/new/identical-item counts. Release, 187 Core and 13 bridge/input tests pass. Deployed locally; plugin and Core DLL SHA-256 match Release. No launch/save write. Native return-cycle checks remain [?]. Current DD2 primary/pair-selector/native-arena refactor is built and pushed; remaining native validation and advanced cold-menu effects are recorded in PARITY.md.

## Round 63: DD1 quest rows beneath each paired area

Owner clarified that the area name and its quests must stay together on the DD1 map, with one arrow immediately right of the name. Round 56's separate bottom strip and two arrows did not match that direction. Read installed quest_select.layout.darkest: four icons per row, quest_button_start_offset 160,92 and spacing 76,80. Read the Unity port's DungeonPanel.DistributeQuests and QuestSlot selection/length/type frames. Restored per-area icon rows and DD1 selected-frame art; the next_hero arrow follows the measured name and cycles enabled paired areas. Removed the strip, its scrolling state, count labels and left arrow. Header clicks stop above the quest icons and register after the arrow; quest selection updates the area's focus and left scroll.

Release, 187 Core and 13 bridge/input tests pass. Deployed locally and verified both DLL SHA-256 values against Release. No launch/save write. Native arrow/quest/locked-area/provisioning checks remain [?]. Found a separate overflow gap: a second Warrens/Foetor row can intersect the Tangle header when more than four contracts are offered. Added it to PARITY.md for a following round rather than dropping contracts or undoing the owner's layout.

## Round 64: crowded quest lists stay beneath their own area

QuestBoard.PlotOffers retains unfinished DD1 boss tiers, so an original area can have more than four offers. The second Warrens/Foetor row intersected the Tangle header. The first row also ended thirteen pixels into that header with the mod's 72-pixel buttons. Moved the shared Weald/Tangle position down twenty pixels and kept every area's four first-row quest positions. Additional rows scroll locally using wheel input and installed shared/widgets scrollbar_uparrow.png / scrollbar_downarrow.png, with a row counter beside the icons. The original area-switch arrow remains beside the area name. Per-area row state clamps after offer changes; quest selection brings its row into view. Hidden rows do not register input controls.

Read-only geometry calculation from installed quest_select.layout.darkest checked all twenty visible quest controls against every other header and the detail scroll, roster and party, with no intersections. Release, 187 Core and 13 bridge/input tests pass. Deployed and verified both DLL hashes. No launch/save write. Native overflow scrolling, final-offer selection and paired-area switching remain [?].

## Round 65: atlas-safe portraits and recoverable loading

Unity 2022.3 documents Sprite.textureRect as throwing for tightly packed sprites: https://docs.unity3d.com/2022.3/Documentation/ScriptReference/Sprite-textureRect.html. Read the Unity port's RaidBannerPanel, which assigns Image.sprite rather than cropping an atlas rectangle, and DD2 ResourceActor.GetPortraitIconByType/addressable large-art fields. Art.DrawSprite and its async completion log previously used textureRect unconditionally. No matching exception exists in the available older native log, so do not claim this proves the Stagecoach freeze's cause.

SpriteArt retains direct drawing for ordinary rectangles. Tight or rotated packing uses native vertices/UVs/triangles to copy only that sprite into a cached RenderTexture, then draws through IMGUI so groups retain clipping. Found Hidden/BlitCopy in the installed unity_builtin_extra, used with blending off to preserve straight alpha. Cache copies have a 1024-pixel dimension limit and an eight-megapixel total budget, with oldest-image eviction and no GPU readback. Render target, GL matrices and sRGB-write state restore in finally; failed copies release their texture and retry after five seconds. Completion logging now reads rect, failed addressable handles release before a two-second retry, and destroyed portrait caches can reload.

Five actual linked-source tests with a command-recording shim cover tight triangle/rotated UV preservation without textureRect, rectangular bottom alignment/flipping, simulated GPU failure recovery/retry, cache limits/eviction and repaint-only work. The first run exposed cross-test use of Event.current and Texture2D.ApiThreads; UI tests now run sequentially because they share Unity-style global state. The repaired suite passes all 18, plus 187 Core tests and Release. Deployed and verified both DLL hashes. No launch/save write. Native shader color/alpha, silhouette edges, scroll clipping and initial-copy timing remain [?].

## Round 66: cold-menu trinkets retain mutually exclusive effect choices

Read the DD1 port's Trinket.ToolTip again for effect-body presentation and DD2 ActorDataEffectDescription.ProcessSourceEffect for actual choice semantics. *_apply_limit_effects CSV groups were being looked up as nonexistent event names; *_apply_limit metadata also marked them unsupported. Native positive apply limits join candidate descriptions with spaced_or_label. Support limit one only when every candidate can be described, preserving event, target title, chance and all alternatives, including penalties. Unknown candidates withhold the whole choice list so a partial list cannot imply the wrong outcomes.

Nautical Compass now shows Turn End: Block or Crit or Dodge or Strength or +1 Stress. Clenching Claws now shows Apply to Attacker When Hit: Weak (20%) or Vulnerable (20%) or Stun (10%). The first test run incorrectly expected Clenching Claws to be complete; actual data also has a separate round-start Immobilize at Speed <=2 that remains unsupported. The corrected test retains that incomplete flag and checks the supported choice line. Three actual-data tests pass, including malformed candidate localization withholding the entire group. Added authored tools/TrinketAudit for reproducible counts/item inspection without native game initialization or save access.

Audit of 191 base trinkets is 66 complete, 75 partial and 50 blank, versus 64/71/56. Release, 190 Core and 18 bridge/input tests pass. Deployed and verified both DLL hashes. No launch/save write. Native pre-run/native-run tooltip comparison remains [?]. Next: actor-stat conditions such as the missing Speed threshold.

## Round 67: actor-stat conditions in cold trinket tooltips

Read installed DD1 base.entries.trinkets.json again for buff-driven equipment data and native DD2 ConditionDescription's ACTOR_STAT_VALUE path. It replaces '+' with '_' in actor_stat_type_<stat>, uses raw comparison units and fills effect_tooltip_condition_actor_stat_value. Added that exact path, preserving localized stat labels and withholding missing/malformed templates.

Clenching Claws now includes Round Start: Immobilize (66%) when Speed is 2 or less and is complete. Seamen's Boots shows its separate Speed <=2 Block and Speed >=6 Dodge effects. Laden Lantern retains +25% Burn RES Piercing even when a malformed localized condition withholds its Speed-gated Blind penalty. Two actual-data tests check both comparison directions, raw Speed units, live stat localization and malformed-template withholding. Updated the earlier Claws test to include its newly supported condition.

The 191-item audit is now 72 complete, 73 partial and 46 blank, versus 66/75/50. Release, 192 Core and 18 bridge/input tests pass. Deployed and verified both DLL hashes. No launch/save write. Native tooltip appearance and cold/native-library comparison remain [?].

## Round 68: DOT and healing-over-time descriptions before combat

Read installed DD1 base.entries.trinkets.json and the Unity port's Trinket.ToolTip for the buff-driven equipment body. Native DD2 EffectDescription delegates DOT additions to DotDescription: sum child damage/healing/Stress base magnitudes and attach the definition's duration. The cold reader now loads dots_data_export.Group.csv, supports single additions with known turn/round duration types and integral base magnitudes, and uses native localization templates. Child critical metadata is allowed because native definition descriptions show the base amount; unknown child/definition fields, random/multiple applications and unsupported durations remain withheld.

Corrupted Bile Gland now describes Bleed/Blight/Burn 3 for 3 Turns for both Apply On Hit and Gain When Hit. Pulsing Heart describes Turn Start Regen 2 for 3 Turns (33%) or Bleed 1 for 3 Turns (66%). Three actual-data tests cover quantities/durations/alternatives, localized duration and malformed-template withholding. The first run's test incorrectly called the gland's second trigger attacker-targeted; installed data says Gain When Hit. Corrected that expectation and all 195 Core/18 bridge tests pass; Release is green. Audit improves from 72 complete/73 partial/46 blank to 81/73/37 among 191. Deployed locally and verified both DLL hashes. No launch or save access; native pre-run/native-run comparisons remain [?].

## Round 69: combat-state requirements remain attached to trinket effects

Read DD1 port Buff.ToolTip's Status-rule wrapping and DD2 ConditionDescription's TOKEN_AMOUNT/DOT_TAG_AMOUNT paths. Native nonzero checks are >0 or >=1; token EQUAL 1 also uses the presence template, while zero uses absence. Actor labels and available actor-specific native templates are retained. Other supported numeric comparisons retain raw units. Hidden or malformed conditions still withhold their conditional lines.

Foreclosure Notice shows +20% DMG with self Stealth and -25% Healing Received from Skills without it. Bone Mallet shows separate target Daze/Stun damage requirements. Carved Toy shows target Burn Debuff RES Piercing, and Dark Impulse's bleed-resistance variant includes +1 Stress at Turn Start while self has Bleed. Three actual-data tests cover both condition families, self/target, absence, localization and malformed-template withholding. All 198 Core/18 bridge tests and Release pass. Audit improves from 81/73/37 to 94 complete/63 partial/34 blank among 191. Deployed locally and verified both DLL hashes; no launch/save access. Native appearance and cold/native comparison remain [?].

## Round 70: token additions and cleansing describe their categories and quantities

Read installed DD1 trinket data/port Trinket.ToolTip and native DD2 EffectDescription/TokenDescription. Added category-token gains, named-token removal and category removal with the native singular/plural and amount-99 Remove All templates. Named-token quantity display follows m_ShowValue and native token_amount_format_*; quantity 99 uses the singular name rather than claiming 99 tokens. Priority and random-removal metadata do not add a separate native description and can now accompany supported effects. Category labels are stripped of rich markup before formatting; otherwise raw color tags would leak into the equipment body.

Jealous Whisper previously showed only its stress penalty; now it also describes positive-token gains on moving/ally movement and removal of all positive tokens on enemy movement. Searing Scripture includes negative-token cleansing and Combo removal. Severed Finger includes a positive token at Round Start while self has Bleed, and Heart-Shaped Padlock includes its random-ally healed trigger and 33% chance. Three actual-data tests cover those effects, all-token semantics, quantities, markup and malformed-template withholding. All 201 Core/18 bridge tests and Release pass. Audit improves from 94/63/34 to 99 complete/63 partial/29 blank among 191. Deployed locally and verified both DLL hashes; no launch/save access. Native tooltip comparison remains [?].

## Round 71: temporary stat buffs keep their native lifetime

Read DD1 installed shared/buffs/base.buffs.json and port Buff.ToolTip, then DD2 EffectDescription/BuffDescription's effect 'buffs' path. Added pure-stat nested buff descriptions with strict definition fields, conditions and native turn/combat/inn duration templates. Combat-end amount one uses buff_combat_end_single_duration_label; inn-start uses inn_next_biome_append_*; turn durations use skill_effect_duration_label. Unknown/recursive buffs remain withheld. Extracted shared stat reading and made malformed numeric/localized stat templates report incomplete coverage instead of displaying invalid placeholders.

Storage Room Key now includes Gain On Miss -1 Speed (3 Turns); His Prison includes Apply to Attacker When Hit +1 Blight Received (3 Turns) (33%) and retains -20% Blight RES; Cruel Intent includes +10% CRIT on miss (1 Battle), while Empty Stein includes +1% CRIT when stress healed (1 Battle). Three actual-data tests cover native lifetimes, sub-stat units/chance, localized duration and malformed stat/duration withholding. All 204 Core/18 bridge tests and Release pass. Audit improves from 99/63/29 to 100 complete/63 partial/28 blank among 191. Deployed locally and verified both DLL hashes; no launch/save access. Native tooltip comparison remains [?].

## Round 72: named-skill trinket requirements

Read installed DD1 buff localization and the Unity port's Buff.ToolTip Skill-rule branch, then native DD2 ConditionDescription/SkillDescription.GetNameText. Added SKILL conditions using skill_name_<id> and native skill-specific condition overrides when present. Plain qualifier text omits the skill-name mastery decoration: Bounty Hunter's native names contain icon_upgraded_skill, which was otherwise turning into glued 'CaltropsUpgraded skill' text rather than a clean skill name. This does not alter which skill the native effect requires.

Utility Belt now describes Caltrops +25% Debuff RES Piercing, Flashbang Blind and Hurlbat Weak. Empty Stein retains Raucous Revelry's 95% Strength or 5% Daze choices; Busker's Haul retains Play Out negative-token removal. Three actual-data tests cover all names/choices, live localization, skill-specific native overrides and malformed-condition withholding. All 207 Core/18 bridge tests and Release pass. Audit improves from 100/63/28 to 101 complete/64 partial/26 blank among 191. Deployed locally and verified both DLL hashes; no launch/save access. Native tooltip comparison remains [?].

## Round 73: resistance-bypass flags no longer hide supported descriptions

Read DD1 installed trinket buff lists/port Trinket.ToolTip and native DD2 EffectDefinition/EffectDescription/EffectApplyCombinedResult. m_IgnoreResist changes application but native EffectDescription still prints the token or buff normally, with no standalone resistance-bypass label. The cold formatter now accepts only a valid boolean for that known field. Unknown effect fields remain withheld; native combat application is unchanged.

Pristine Lure now shows Gain On Hit Taunt (50%) or Tauntx2 (25%) plus its on-miss Bleed 2 for 3 Turns. Idle Thought keeps +100% DMG and its Round End -30% DMG (1 Battle) penalty. Empty Stein now includes Barbaric Yawp -20% Debuff RES for 3 Turns and is complete; Strong Shackles retains both target and self hit-triggered Immobilize. Three actual-data tests cover choice quantities/chances, skill/lifetime and malformed-duration withholding. All 210 Core/18 bridge tests and Release pass. Audit improves from 101/64/26 to 106 complete/61 partial/24 blank among 191. Deployed locally and verified both DLL hashes; no launch/save access. Native tooltip comparison remains [?].

## Round 74: first/last turn-order requirements in cold descriptions

Read DD1 installed conditional buff data/port Buff.ToolTip and native DD2 ConditionDescription's FIRST_INITIATIVE/LAST_INITIATIVE branches. Added the native qualifier templates around supported effects; no combat turn calculation changed. Blistering Bugle includes Turn Start Vulnerable (33%) when first in turn order. Inevitable End includes Turn End +5% DMG (1 Battle) when last in turn order, and Snap Judgement includes its Turn End -6 Speed (1 Battle) under that same requirement.

Three actual-data tests cover both ordering directions, benefit/penalty, live localization and malformed-template withholding while retaining other bonuses. All 213 Core/18 bridge tests and Release pass. Audit improves from 106/61/24 to 108 complete/63 partial/20 blank among 191. Across rounds 68-74, complete cold descriptions rose from 72 to 108 and blanks fell from 46 to 20. Deployed locally and verified both DLL hashes; no launch/save access. Native tooltip comparison remains [?].

## Round 75: native visibility attempt reverted under the loop rule

Read DD1 installed trinket buff lists/port Trinket.ToolTip and native DD2 ActorDataEffectDescription/ConditionDescription. Native code skips invisible effects and returns the effect unchanged for invisible internal checks. Tried that behavior in the cold reader; Anatomical Map and Vengeful Kill List became readable. Verification failed twice on a malformed-rank test for Rotten Tomato, so code and tests were reverted as tools/parity_loop.md requires.

After reverting, identified the test's source mistake: Sources/items.txt has buff_desc_trinket_hel_rotten_tomato_02_override, which produces the Weak/rank line before either injected condition key is used. The failures do not prove a regression in condition wrapping. Recorded the visibility sub-item as blocked for this loop; any later revisit needs a fixture that exercises the relevant branches rather than an authored override. Restored baseline passes 213 Core/18 bridge tests and Release. Installed code remains round 74; no game launch, deployment of the failed attempt or save access. Coverage remains 108 complete/63 partial/20 blank.

## Round 76: extra-action descriptions retain their conditions and penalties

Read DD1 installed trinket buff lists/port Buff.ToolTip and native DD2 EffectDescription's m_AddTurn path. Added supported single extra actions through effect_tooltip_add_turn while preserving event, chance, condition and alternative grouping. Multiple/random quantities remain withheld. Smoldering Hymnal includes its killing-blow 20% extra action; Carved Bodkin includes a 20% extra action at Turn Start while self has Bleed; Snap Judgement includes its 10% Speed >=12 extra action. Pocketwatch retains Extra Action (50%) or Stun (50%). Temptation retains Extra Action or 1 DMG and -100% Deathblow RES.

Three actual-data tests cover triggers/requirements, choices/penalties and malformed-template withholding of the whole choice group. Release, 216 Core and 18 bridge tests pass. Audit is 113 complete/59 partial/19 blank among 191, versus 108/63/20. Deployed locally and verified both DLL hashes. No launch/save access; native tooltip comparison remains [?]. Round 75's visibility attempt remains blocked and reverted.

## Round 77: movement and shuffle in cold trinket descriptions

Read DD1 installed trinket data/port Buff.ToolTip and native DD2 EffectDescription/ActorDataEffectDescription. Negative m_Move uses Forward or Pull, positive uses Back or Knockback; native equipment event context determines friendly versus target wording. Added fixed distances and m_Shuffle through the native templates, preserving event/skill/chance/condition wrapping. Unsupported move ranges remain withheld. Inevitable End now includes Turn Start Forward 1; Pile of Ash retains attacker Shuffle, hit Knockback 1 and its Dodge alternatives. Barristan's Head includes its Melee Skills Knockback 1 and existing Stress penalty.

Three actual-data tests cover movement/other effects, targets/skill requirements, live localization and malformed-template withholding. Release, 219 Core and 18 bridge tests pass. Audit is 116 complete/56 partial/19 blank among 191, versus 113/59/19. Deployed locally and verified both DLL hashes. No launch/save access; native tooltip comparison remains [?]. Royal Summons already had an authored buff override and was not used to prove the new parser.

## Round 78: load the missing base trinket effect table

Read DD1 installed trinket buff lists/port Trinket.ToolTip and native DD2 LibraryBuff/LibraryActorEffectTrigger resource initialization. The cold reader's five files omitted trinkets_actor_effect_trigger_data_export.Group.csv entirely. Read-only inspection found 80 Buff, 69 ActorDataEffects, 60 Effect, 22 Condition and 44 ActorEffectTrigger blocks with no overlap against existing loaded blocks. Added this one base table, retaining the existing strict formatter and leaving DLC separate.

Storage Room Key now includes Ounce of Prevention Block and Emboldening Vapours Regen 2 for 3 Turns. His Prison includes More! MORE! Strength, Foreclosure Notice includes Shadow Fade negative-token removal, and Barristan's Head includes melee Block. Smoldering Firewood retains its ranged Burn and on-hit Burn penalty. Unsupported actor-trigger routing remains incomplete, as verified for Grim Mask/Thrilling Tablet. Three actual-data tests cover cross-table effects, requirements/penalties and conservative withholding. Release, 222 Core and 18 bridge tests pass; audit improves from 116/56/19 to 122 complete/51 partial/18 blank. Both local deployed DLL hashes match. No launch/save access; native tooltip comparison remains [?].

## Round 79: team recipients and choices from ActorEffectTrigger

Read DD1 installed trinket buff lists/port Trinket.ToolTip and native DD2 ActorEffectTriggerDefinition, ActorDataEffectDescription and AppliedEffects. ActorEffectTrigger stores event, source, target family, actor count and optional apply limit. Native tooltip precedence is effect-specific event/target/count override, event/target/count title, then event plus actor_trigger_target_type label. Added only strict base team triggers with known source, valid count/boolean and known metadata; unknown neighbors/fields remain incomplete. Normalized event names as native parsing does, including Grim Mask's On_Crit_As_Performer_To_Performer.

Reused the existing effect-group formatter for direct and trigger effects so limited choices with an unknown outcome are withheld together. Rousing Recorder targets a random ally; Standard of the Ninth each hero; Astroglass Flute keeps -1 Stress 70% or +1 Stress 30%; Dronepipe keeps random enemy Stun 33% and random ally Stun 10%. Grim Mask retains its Flame-gated ally heal and Stress/damage penalties. Three actual-data tests cover counts/targets, benefits/penalties, choices, live target localization, mixed-case fallback and withheld neighbors. The round-78 Grim Mask completeness assertion now expects the recovered trigger. Release, 225 Core and 18 bridge tests pass; audit improves from 122/51/18 to 128 complete/47 partial/16 blank. Both deployed DLL hashes match. No launch/save access; native tooltip/recipient comparison remains [?].

## Round 80: neighboring ally direction and count in trinket triggers

Read DD1 installed positional buff data/port Buff.ToolTip and native DD2 AppliedEffects neighbor traversal/ActorDataEffectDescription target labels. Added only validated front/back counts, using native adjacent/back/front localization and count. Team triggers with stray neighbor counts and alternate neighbor anchors/unknown fields stay withheld. Sneakers and Unwavering Standard now explain all allies behind; Hastening History and Insulating Insignia the ally ahead; Simple Flower the ally behind. Royal Summons retains its CRIT requirement, Undeserved Commendation its miss Stress penalty, and Profane Scroll its Hand of Light Guardedx2 and on-miss Stress.

Three actual-data tests cover recipient direction/count, event/skill requirements, penalties and live direction localization. The round-79 Sneakers test now expects its recovered Stealth line/completeness. Release, 228 Core and 18 bridge tests pass; audit improves from 128/47/16 to 135 complete/40 partial/16 blank. Both deployed DLL hashes match. No launch/save access; native tooltip/recipient comparison remains [?].

## Round 81: visible token-to-token conversion descriptions

Read DD1 installed trinket buff lists/port Trinket.ToolTip and native DD2 EffectDescription/TokenDescription. Native conversion formats from/to token names with the actual amount, independent of ShowValue; amount >=99 uses the singular token label. Added fixed visible token-to-token conversions through native templates, retaining event/chance/conditions. Unsupported ranges and DOT conversion remain withheld. This does not change the round-75 visibility code or its blocked status.

Footman's Grog now includes each-hero Vulnerable to Block and Weak to Strength at Turn Start. Blood-Smeared Calculations shows Dodge to Crit/Block to Strength at start and Crit to Dodge+/Strength to Block+ at end, with its Bleed 1 for 3 Turns. It remains incomplete because hidden effects are still withheld; Goading Gargoyle's hidden requirement is also withheld. Three actual-data tests cover team/phase/penalty, native quantity/localization and malformed-conversion withholding. Release, 231 Core and 18 bridge tests pass; audit improves from 135/40/16 to 136 complete/40 partial/15 blank. Both deployed DLL hashes match. No launch/save access; native tooltip comparison remains [?].

## Round 82: resistance-triggered benefits and damage penalties

Read DD1 installed trinket buff lists/port Buff.ToolTip and native DD2 ActorDataEffectDescription/ConditionDescription. Added only known self on_resist routing with source=target, recipient=target, count=1 and IncludeSourceActor=true. RESIST_TAG uses resist_tag_<id>; RESIST uses actor_stat_type_resistance_<id>; only BOOL qualifiers are supported. Other direct targets remain withheld.

Charred Litany now includes -2 Stress after Burn resist; Kitchen Knives Blight extra action 20%; Knitted Blanket Burn Stealth; Skeleton's Sight -2 Stress on each DOT resist. Hardened Heart retains +200% DOT resist and the 2 DMG penalty for each resisted Bleed/Blight/Burn. Sodden Sweater includes Move RES -1 Stress, but its heal crit metadata is still unsupported. Three actual-data tests cover rewards/requirements, penalties/units, live localization and malformed-condition withholding. Release, 234 Core and 18 bridge tests pass; audit improves from 136/40/15 to 139 complete/38 partial/14 blank. Both deployed DLL hashes match. No launch/save access; native tooltip comparison remains [?]. Found a separate plain-icon fallback wording gap: Daze gold/Healthup/Deflect and spaces before punctuation in authored strings. Recorded it without changing this round.

## Round 83: critical-heal metadata no longer suppresses heal descriptions

Read DD1 installed healing trinket buff lists/port Buff.ToolTip and native DD2 EffectDefinition, EffectInstance and EffectDescription. Native critical chance/multiplier affect health application; the formatter still prints base heal amount/percent without a separate critical chance/multiplier suffix. Accepted only finite valid health-effect metadata, with chance in [0,1], nonnegative multiplier and a positive multiplier when chance is positive. Unknown fields remain withheld and proc chance is still described independently.

Fate's Foreteller now retains its 20% heal on Bleed/Blight/Burn resist; Sodden Sweater its 10% heal on Bleed resist alongside Move stress relief/resist stats. Ghastly Gruel includes each-hero heal 2 on Round End at 33%, remaining incomplete for other unsupported fields. Three actual-data tests cover amounts/units/requirements/stats, no invented 5% critical tooltip suffix, live localization and malformed-heal withholding. The round-82 Sodden Sweater test now expects completeness. Release, 237 Core and 18 bridge tests pass; audit improves from 139/38/14 to 145 complete/36 partial/10 blank. Both deployed DLL hashes match. No launch/save access; native tooltip comparison remains [?].

## Round 84: readable localized names for native glyphs

Read DD1 installed equipment localization/port Trinket.ToolTip and native DD2 tokens.txt/TokenDescription. Token glyph IDs differ from visible names: token_deflect is Block+, token_daze_gold is Daze, token_dodge+ is Dodge+, token_blind-line is Blind. Added canonical aliases/current token_name localization with English fallback in Core Text and the native ItemText Plain path. Health-up reads Heal. Sprite ID parsing now retains plus/hyphen; punctuation cleanup removes stray spaces before colons/commas and around slashes without touching decimal ranges. Simple Flower now reads Gain On Stun/Daze/Move Resist: Block+, Heal 10%; Inert Indicia no longer says Daze gold.

First full verification found a real regression: Text expanded icon_upgraded_skill before named-skill cleanup, exposing mastery as skill-name text. The orchestration deployed that first build before inspecting the failing test result. DD2 was not running/started. Corrected the order by reading raw skill names and removing the decoration before glyph expansion; the second full verification passed all 240 Core tests, plus 18 bridge tests and Release. Replaced the installed DLLs with that verified build and checked both hashes. Future orchestration must inspect every test exit code before deployment. No failed build was committed/pushed. Three added tests cover authored glyphs, live localized cold/native paths, plus/hyphen/quantities/ranges. Native display remains [?]; no launch/save access. Coverage remains 145 complete/36 partial/10 blank.

## Round 85: delayed effects inside trinket buffs

Read DD1 installed trinket buff lists/port Trinket.ToolTip and native DD2 BuffDescription/DurationDescription. BuffDescription includes ActorDataEffectDescription then appends lifetime; the cold StatBuff previously refused any ActorDataEffects. Renamed it BuffEffect and allowed known nested effects through existing strict event/choice/condition and duration paths. Bound recursion to four levels and withhold the entire nested body if any part is unsupported, cyclic or too deep; RunDataStats/unknown buff fields still remain withheld.

Busker's Haul now includes Target: Battle Ballad: Turn Start: Forward 1 (1 Turn), keeping Play Out negative-token removal. The actual base table attaches this effect to Battle Ballad, not Encore. Two actual-data tests cover skill/event/lifetime and malformed-duration withholding; one authored temporary cyclic graph verifies bounded recursion and is removed afterward with its absolute parent checked. Release, 243 Core and 18 bridge tests pass. Audit stays 145 complete/36 partial/10 blank because Busker's ally exclusion/currency penalty are separate unsupported conditions. Both deployed DLL hashes match. No launch/save access; native nested tooltip comparison remains [?].

## Round 86: inventory thresholds retain native currency names and penalties

Read DD1 installed conditional trinket buffs/port Buff.ToolTip and native DD2 ConditionDescription ITEM_AMOUNT. The native path formats item_name_<id>, raw comparison, separator and effect; native gold is named Relics. Added this supported condition type without changing inventory or native application. Busker's Haul retains its +1 Stress 25% Turn End penalty below 25 Relics. Antiquarian Chalice/Carcanet/Crown retain above-50/75/100 Relics requirements on +4 Speed/+20% Max HP/full heal; their other unsupported effects stay incomplete. The initial target's generic >=35 Antiquarian condition was not their actual displayed stat threshold; the actual selected buff requirements are recorded here.

Three actual-data tests cover the low-wealth penalty, raw threshold versus percent units, localized currency names and malformed-condition withholding while retaining other skills. Updated round-85 malformed-duration test to retain the now-supported separate Stress penalty. Release, 246 Core and 18 bridge tests pass; audit improves from 145/36/10 to 145 complete/40 partial/6 blank. Both deployed DLL hashes match. No launch/save access; native inventory-threshold tooltip comparison remains [?].

## Round 87: token and DOT steals retain their named skills

Read DD1 installed trinket buff lists/port Trinket.ToolTip and native DD2 EffectDescription. Fixed TokenStealTags use category singular/plural/all templates; fixed DotStealTags use the native DOT-name template, including the 99-all Regen case. Added validated fixed quantities and withheld ranges/unknown fields. Event, skill, chance and condition wrapping remain unchanged.

Emancipation now shows Target: Punish: Steal Positive Token alongside +25% DMG below 20% HP and -20% Bleed RES. Cursed Coin shows Highway Robbery: Steal Regen alongside its -10% CRIT above 50 Relics, remaining incomplete for positive-token-count conditions. Searing Scripture remains incomplete and its steal lines remain withheld because target_has_ally_tag_not_self is a separate unsupported requirement. That condition is TAG ally/target >=1 plus SourceConditionActorType=PERFORMER and ActorIsNotSource=true; native ConditionDescription's ordinary TAG label alone does not express the source exclusion. Do not silently discard that restriction.

Three actual-data tests cover skills/benefits/penalties, native localized categories/DOT names and malformed-transfer withholding. Release, 249 Core and 18 bridge tests pass; audit improves from 145/40/6 to 146 complete/39 partial/6 blank. Both deployed DLL hashes match. No launch/save access; native transfer tooltip comparison remains [?].

## Round 88: token-category stacks and target requirements

Read DD1 installed conditional trinket buffs/port Buff.ToolTip and native DD2 ConditionDescription TOKEN_AMOUNT/TOKEN_TAG_AMOUNT. Native categories use token_<category> with the same per-stack/presence/raw threshold paths. Extended the existing formatter to visible source-independent category conditions; conditions with a source actor/exclusion and hidden conditions remain withheld.

Cursed Coin now includes +5% DMG per Positive Token alongside Highway Robbery and its high-Relics CRIT penalty. Knitted Blanket includes +1 Burn Dealt when target has Negative Token, keeping Burn-resist Stealth and its self-Burn penalty. Selfish Motivation retains +50% DMG per Negative Token and its negative-token alternatives. Scalded Skull now keeps its 33% random-ally Burn gated by >=2 Unchecked Power. Three actual-data tests cover stacks, target/self requirements, benefits/penalties, live localization and malformed-multiplier withholding; the earlier Cursed Coin completeness/remaining-stat assertions now include the recovered bonus. Release, 252 Core and 18 bridge tests pass; audit improves from 146/39/6 to 152 complete/35 partial/4 blank. Both deployed DLL hashes match. No launch/save access; native tooltip comparison remains [?]. New wording gap recorded separately: token_uc_power currently becomes Uc power, and the stock English category comparator yields has is. The initial target misstated Knitted Blanket as per-Positive damage; actual table has the target Negative Token Burn bonus recorded above.

## Round 89: readable Unchecked Power threshold wording

Read DD1 installed miscellaneous.string_table.xml buff_rule_tooltip_* and port Buff.ToolTip, plus native DD2 TokenDescription/tokens.txt and condition templates. The stock glyph token_uc_power names Unchecked Power; the token/category threshold template joins has to comparison labels already starting is. Added the canonical token alias/current token_name localization and removed only that invalid English verb sequence in Plain, shared by cold and native descriptions. Scalded Skull now reads Random Ally on Turn Start: Burn 1 (3 Turns) (33%) when Unchecked Power is 2 or more. Ordinary target has Combo and explicitly localized sentences remain intact.

Three tests cover canonical/current-language names, unchanged quantities and ordinary presence text, explicit localized templates and malformed-template withholding. Release, 255 Core and 18 bridge tests pass. Audit remains 152 complete/35 partial/4 blank. Both deployed DLL hashes match. No launch/save access; native wording comparison remains [?].

## Round 90: explicit other-ally restrictions on recovered trinket effects

Read DD1 installed target-qualified miscellaneous buff_rule_tooltip_* and port Buff.ToolTip; native DD2 ConditionDescription TAG, ConditionCalculation source GUID filtering and the separate trinket trigger table. target_has_ally_tag_not_self is TAG ally/TARGET >=1 with PERFORMER source and ActorIsNotSource=true. The cold reader withheld it; ordinary native TAG text says only Ally. Added only this visible noninverse presence pattern with a strict field allowlist, using effect_tooltip_condition_other_ally or English Other Ally: {0}. This explicitly preserves self exclusion instead of dropping it. No visibility behavior from blocked round 75 changed.

Junia's Head now includes other-ally negative-token/Combo cleansing with its Stress penalty; Searing Scripture other-ally steals alongside self cleansing/Burn RES penalty; Busker's Haul Dodge 33% with skill/delayed movement and low-Relics Stress; Ancestor's Mustache Cream extra action 50% alongside its existing authored enemy/Boss note/Horror penalty; Undeserved Commendation CRIT 15% with Command/miss penalty. Rousing Ringer's other-ally cleanse remains withheld for a separate unsupported token-category label. Annotated Textbook's limited inversion/Stress group remains withheld as a whole because inversion is still unsupported.

Two actual-data/localization tests and seven authored invalid-condition cases cover restriction, chance, penalties, explicit localization, malformed templates and rejected source/actor/inverse/hidden/unknown variants. Updated older Busker/Searing completeness assertions. Release, 264 Core and 18 bridge tests pass; audit improves from 152/35/4 to 157 complete/30 partial/4 blank. Both deployed DLL hashes match. No launch/save access; compare cold/native target wording in game once permitted.

## Round 91: native authored effect bodies before generic formatting

Read DD1 installed base.entries.trinkets.json buff lists/port Trinket.ToolTip and native DD2 EffectDescription/ActorDataEffectDescription. Native EffectDescription returns effect_skill_<id>_override before generic formatting/chance, then the outer actor formatter adds conditions. Added the same body precedence in the cold reader and extracted shared WithEffectConditions. Authored chance is not appended twice; malformed composite placeholders withhold the body; existing visibility/unknown conditions still withhold. Generic inversion remains unsupported without an authored body; no round-75 visibility change was made.

Annotated Textbook now includes Target: Other Ally: Invert 1 Negative Token (65%) or Other Ally: +1 Stress (5%), keeping Restorative healing and first-order Vulnerable penalty. Rousing Ringer includes Target: Other Ally: Remove Daze Stun alongside attacker Daze 66% below 50 Flame. Hag's Hoard retains its authored +10% Healing Received from Skills per Positive Token but is still incomplete. Anatomical Map remains blank under blocked visibility.

Three actual-data tests cover full alternatives/chances/restrictions/penalties, live authored text without duplicate 65%, malformed-body whole-choice withholding and unchanged visibility. Release, 267 Core and 18 bridge tests pass; audit improves from 157/30/4 to 159 complete/28 partial/4 blank. Both deployed DLL hashes match. No launch/save access; native authored/cold comparison remains [?]. During the round the owner asked about coherent DD2 corridor backgrounds; investigate region scenery next under the existing DD2-primary/coherence direction.

## Round 92: native DD2 regional corridors with open room approaches

Owner requested DD2 scenery from room to room, then replaced the initial DD1 passage-frame idea after seeing the preview. Read installed DD1 dungeons/* corridor_bg/mid/corridor_door assets and the Unity port RaidHallwayView; inspected installed DD2 Addressables catalog and biome bundles read-only with UnityPy outside the repo. City background/mid strips are 1024x512 RGBA, City red-smoke sky 2048x512; Farm background/mid 2048x1024 and wall 2048x512 with soft alpha; Forest sky 2048x1024, tree skirt silhouettes 2048x1024/512 with alpha; Coast cliff strip 2048x512 alpha. Roads are vertical native tileable textures, rotated 90 degrees for the horizontal walk plane at y610-720 beneath the hero feet y680. Profiles preserve native angular painted palettes, overlap behind the floor and use multiple parallax depths. No extracted assets shipped.

RegionalScenery holds primary-region profiles and Core geometry. Physical coordinates survive reverse traversal; alternating mirrors join at matching source edges. Foreground density eases into outdoor room openings instead of DD1 doors, with subtle region-specific arrival cues. Rooms share the regional palette/road, existing short room fades, DD1 props, heroes, map, torch and controls. Legacy regions retain existing art. RegionSceneryArt preloads a small Texture2D set during embark, switches only after every handle succeeds, fades in over 0.35s, retains DD1 fallback on errors and retries after 10s; released handles/stale completion generations are guarded at region/expedition/plugin teardown. No synchronous scene load or bundle-wide scan.

First Core verification found City road's internal path is not an address key; the installed catalog maps it via road_city_cobblestone_01. Corrected the key and actual-catalog test. The next verification passed; five added tests cover address keys, full viewport/seam mirroring, reverse/discrete walking continuity, invalid geometry and eased boundary symmetry. Offline preview inspection also found silhouettes ending above the floor; expanded overlaps, rebuilt and reran Core successfully. Final Release, 272 Core + 18 bridge tests pass; both installed DLL SHA256 hashes match. Four-region offline composition preview is at C:/Users/Piral/.universal-modder/inspection/dd2-corridors/preview.html, served only on localhost:8766 (exec session 20403); it is not a game screenshot. No DD2 launch or save access. Native visual/performance checks remain [?]. Owner then requested AI-generated room diversity; first four scenes generated outside repo with built-in imagegen, second set in progress. Integration is the next round.

## Round 93: AI-generated room variety with stable regional identity

Owner requested generated background diversity based on each DD2 region. Read installed DD1 weald.room_wall.clearing/handtree/gate/etc and Unity DungeonEnviromentData.RoomVariations/RaidRoomView. Used built-in imagegen, one call per distinct asset: two original panoramas per region, referencing inspected installed art for palette/environment/angular rendering, then the first generated scene for the second scene's palette and ground height. Sprawl courtyard/market, Foetor farmstead/orchard, Tangle abandoned camp/watchtower, Shroud landing/shipwreck-beacon. All eight are opaque RGB PNGs, 2048x768 except first Foetor 2046x768; the latter differs by under 0.1% in aspect and is fitted at draw time without editing the image. A strict exact-ratio inspection assertion caught this small size difference; the documented 2.6-2.7 fit tolerance and all full verification passed. Originals remain in Codex generated_images; project copies, full prompts, generator disclosure, dimensions and SHA256 hashes are in data/scenery. No source game textures/decompiled files committed.

RoomBackground uses the saved expedition seed and room ID, never RandomCounter or gameplay RNG. Consecutive IDs alternate the two scenes; mirrored orientations add views. This is two distinct scenes per region, not a unique generated image for every room. Legacy regions do not opt in. RegionSceneryArt reads only the active region's PNG pair on a worker, then decodes/uploads at most one per Update during embark on Unity's main thread with markNonReadable; successful images fade ready over 0.35s, missing/invalid files retain native art. Owned textures, pending task references and queues clear with the existing expedition lifecycle; native Addressables textures remain handle-owned.

DrawGeneratedRoom paints behind existing props/heroes and blends its lower 110 pixels into the common native road. Regional road tints fixed the offline Foetor gray/ochre mismatch and kept regional footlines coherent. Offline composition also caught a persistent half-visible panorama at corridor approaches looking ghosted; removed it and retained the existing short room fade for the scenery switch. Corridors remain clean native layers and open boundaries from round 92. Native DD2 combat is unchanged. Full generated images and four-region room/ground compositions inspected, including mirror and boundary orientations, in the existing localhost preview (not a game capture).

Two added Core tests verify stable/alternating scene+mirror selection over positive/negative/extreme seeds and the actual PNG signatures/dimensions/aspect. Release, 274 Core + 18 bridge tests pass; both installed DLLs and all eight PNGs/manifest/README hash-match. Deployed locally without a DD2 process or save access. Game-facing checks remain [?]: adjacent rooms, revisit/save reload, hero/prop scale/interactions, torch darkness, fades, load timings and memory over repeated expeditions. Preview server remains localhost:8766 (exec session 20403) and the browser tab is kept open for the owner.

## Round 94: extend native combat arena art

Owner requested closer reproductions of existing DD2 fight scenery. Exported private exterior dungeon arena references from the owned install using UnityPy 1.25.4 and Blender 5.2.2. Native arenas are assembled 3D scenes rather than single background pictures. Static-batch firstSubMesh/subMeshCount selects world-space geometry; other meshes retain hierarchy transforms. Native custom materials bind _BaseMap_texture and store tints/UV tiling separately. Highest LOD/active scenery retained; VFX shader meshes and lamp glow excluded. Correct native default InLine combat camera is 0,0.74,-8.3, identity rotation, vertical FOV38. Final references resolve all 86 city/83 farm/325 forest/235 coast exported objects. Offline presentation uses base textures/emission and approximate region sky, not native Unity lighting, effects or post processing.

Built-in imagegen extended four 16:9 references into 2048x768 RGB panoramas while keeping the buildings, tower, palisades and shrine recognizable. First Shroud foreground had holes; a localized edit joined its walking surface and retained the background. All outputs inspected/decoded; preview copies match SHA256. Full prompts, output/reference hashes and original candidate paths live with the private four-image pack at C:\Users\Piral\DarkestDungeon3\local-art\combat-extensions. Extracted references/scene exports/Blender files stay under .universal-modder/inspection, outside Git. Only converters, comparison-page source and workflow notes committed. Installed runtime/scenery remains round 93. No game process launched and no saves accessed.

Existing preview server session20403 serves localhost:8766/combat-extensions.html; native-reference comparison and regional controls inspected in the browser. Owner approved the new art style during verification and requested more native references to create a larger dungeon set. Continue with six distinct scenes per region, 24 total, preserving regional art and consistent traversal surfaces. Release + 274 Core + 18 UI tests green. Native torch/readability/hero scale/ground joins remain [?].

## Status 2026-10-05: loop resumed, round 94 complete
- Owner requested autonomous iteration on 2026-10-04. Continue tools/parity_loop.md; no DD2 launches until explicitly allowed. Build with Deploy=false. Working branch: claude/practical-wright-hicri0.
- Release build, 274 Core and 18 bridge/input tests passed; deployed locally through round 93. Both installed DLLs and all eight generated room PNGs/manifest/README hash-match the verified build/project files. Game-facing changes remain [?]. No saves changed, no game processes started; localhost preview server at port 8766 is running (exec session 20403), no pending restores.
- Built refactor: DD2 default regions, a single arrow beside each paired area name, DD1 quest rows directly beneath every area with local overflow scrolling, separate progress, native DD2 arenas/light and asynchronously loaded regional corridors with open landscape boundaries and eight generated stable room variants, bounded setup polling and timing logs. Built trinket presentation/slot/choice/actor-stat/DOT/combat-state/token-mutation/stat-buff/named-skill/resistance-flag/initiative/extra-action/movement/separate-table/team-trigger/neighbor/token-conversion/resist/heal-metadata/localized-glyph/nested-buff/inventory-threshold/token-steal/category-condition fixes and atlas-safe portrait drawing/retries. Next round 95: owner approved the native-arena extension style; build a private six-scene set per region using different native arena references and connect it to stable room selection, with existing public art as fallback; then continue remaining cold-menu effect fields/conditions or persistent activity-log weeks/art; native scene/priority-bug verification still awaits DD2 launch permission; cold tooltip coverage is 159 complete/28 partial/4 blank out of 191 base trinkets. Later candidates: secret rooms and Memorial collection/narration. Skip [user] and [blocked], including round 75 visibility.
- Private art study: four native-arena extensions and full prompts at local-art/combat-extensions; comparison tab at localhost:8766/combat-extensions.html kept open. Native references and derivatives remain outside Git. Round 95 expands the approved direction. No pending restores.
- Built but not seen in game yet, check these first once launching is allowed:
  1. Native corridors + generated rooms: each region, walk forward/reverse through both boundaries, multiple room IDs, revisit/reload, props/hero footline, torch and battle fade, missing PNG fallback and repeated expeditions. No DD1 doorway should appear after native art is ready.
  2. New estate → House of Ruin + Old Road cinematics → the Old Road loading screen (3 s) → the opening raid with Reynauld and Dismas. The first try froze on "The party sets out" (round 33); the fix routes it through Driver.Embark.
  3. The Sprawl tutorial (plot_tutorial_crypts) on a new estate's board, played on tutorial_crypts.dm. Enable Ruins and cycle the paired map arrows; XP and contracts must remain separate.
  4. The crow quest from the plot_quest_crow_trinket event (week ≥ 15), its one-room lair and the carrion boss battle.
  5. A slain corpse-leaving DD1 monster lying in its dead pose (Ctrl+F10 kills the front enemy as a skill kill).
- [user] questions still open: DLC content; game modes (heroes refusing to go back after the Darkest Dungeon); trinket retention on a party wipe.

## Round 95: six native-reference exploration scenes per region

Owner approved round 94's native-arena extension style and requested a larger set.
Selected six actual arena types per primary region: dungeon_exterior, resist, faction,
creature_den, pillager and cultist. Exported/rendered 20 additional private references
from owned Unity bundles, preserving mesh placement, painted textures and the native
default combat camera. Exporter now detects actual RGBA alpha even when native custom
materials omit the alpha-cutout flag, and skips enabled renderers with null MeshFilters
which have no Unity geometry. All 24 references resolve their exported objects; counts
and full source/output hashes are recorded in the private manifest. City pillager/cultist
scene metadata was refreshed after the empty-filter fix; their rendered geometry did
not change. This is an offline material/camera approximation, not a native game capture.

Built-in imagegen made 20 distinct extensions, one call per asset, preserving native
landmarks and regional colors/ink marks. Shroud pillager foreground had a dark gap;
one localized edit joined its board surface while keeping the arena recognizable.
Combined with the approved first four scenes: 24 unique output hashes, all RGB, 20 at
2048x768, three 2046x768 and one 2043x770, fitted with a 2.6–2.7 ratio tolerance. No image
resampling/editing scripts. Total 46,754,480 bytes; largest 2,466,193 bytes. Full prompts,
original candidates and two Shroud correction prompts remain with the private pack
at C:\Users\Piral\DarkestDungeon3\local-art\combat-extensions. Source meshes/textures,
offline references and Blender scenes remain under .universal-modder/inspection.
No native or source-derived images committed or included in release data.

DD1 crypts.props.darkest separates hall_curios and room_curios; Unity port
Assets/Scripts/Generation/DungeonGenerator.cs initializes generation RNG with its seed
and assigns room grid IDs. The art change leaves those expedition systems alone:
RegionalScenery now accepts an ordered variable-size pool and chooses only from saved
expedition seed/room ID, without advancing gameplay RNG. Every N consecutive room IDs
uses each of N scenes, then mirrored views. Stable revisit/reload behavior requires an
unchanged pack. PrivateRoomScenery discovers strict two-digit regional filenames,
takes up to 12 per region and checks byte count/PNG IHDR, unsigned dimensions, color
format and ratio before Unity allocates a texture.

Plugin Paths/NativeRoomSceneryFolder defaults to game/PrivateScenery; empty disables.
RegionSceneryArt reads only active-region files on a worker, uploads at most one image
per Update on Unity's main thread with markNonReadable, and adopts the complete valid
private pool once. Corrupt/missing files retain public/native fallback; pool adoption
fades over 0.35s and cannot reshuffle later as individual files arrive. Clear destroys
owned generated textures, releases native handles and drops pending worker/queue
references. Private rooms draw their own entire coherent floor; the Shroud's boards
are no longer faded into sand. Existing short room fades cover switches to native
corridors. Public round 93 art keeps its former road blend. Native DD2 combat unchanged.

Three Core tests cover strict bounded discovery, six/twelve-scene deterministic
assignment over extreme seeds, and real PNG bounds/corruption. A headless integration
test compiles the actual loader with Unity/Addressables shims and exercises two public
fallbacks plus seven private candidates (one corrupt), exactly six valid scenes,
one decode per update, main-thread calls, adoption/fade, bad-file skip, clear/missing/
legacy-region fallback and outstanding worker teardown. Initial UI test compilation
found a duplicate Mathf shim; consolidated its added methods into the existing shim.
Release + 277 Core + 19 UI tests pass. Shims are not GPU/Unity PNG-decoder verification.

Backed up both installed DLLs/PDBs before deployment at
C:\Users\Piral\.universal-modder\backups\darkest2in1-plugin\20261005-round95-before-052418.zip.
Deployed Release DLLs and public data normally; copied the private 24-PNG pack/manifest/
README separately to game/PrivateScenery. Both DLLs, public fallbacks and all private
workspace/preview/installed copies hash-match. No DD2 process running/launched and no
save access. Existing localhost comparison now has six per-region thumbnails, a scene
selector, previous/next arrows and source/mirror/walking-band controls. Native-source
matching and generated scenes inspected in the browser. The linked older preview.html
retains the previous round 93 public corridor/room composition.

## Status 2026-10-05: loop resumed, round 95 complete

- Owner authorized autonomous iterations. Continue tools/parity_loop.md on claude/practical-wright-hicri0. Do not launch DD2 until explicitly allowed. Build Deploy=false if a game process is running; game-facing changes remain [?].
- Round 95 Release, 277 Core and 19 UI tests passed. Locally deployed DLLs/public fallbacks hash-match. Private 24-image pack installed at game/PrivateScenery and hash-matches local-art/combat-extensions and preview copies. The default config path is bound on next plugin startup; no save/campaign migration was needed.
- Native arena references/artwork remain outside Git. Only loader/tools/test/document changes are tracked. Full prompts, arena names, source/output hashes, original generated paths and Shroud corrections are in local-art/combat-extensions/manifest.json. Six distinct images per primary region, support up to 12; mirrors reuse art.
- No game launch or save access, no pending restores. Exact plugin-assembly backup above. Local preview server port 8766, exec session 20403, still running; combat-extensions.html is the expanded pack browser and is kept open. The linked round 93 corridor preview is historical.
- Native verification when launch is allowed: embark in each primary region, walk both directions/room boundaries, six consecutive room IDs, revisit/save reload, hero/prop footline and interactions, torch darkness (particularly Tangle), battle fade, Shroud wharf/sand joins, missing-pack fallback, load timing and repeated expedition memory. Headless tests verify lifecycle/selection; they do not verify native rendering.
- Existing built systems/priority fixes remain as round 94 status: DD2 default paired regions and DD1 quest rows with single right arrow, independent progress, native arena/light, open native corridors, bounded setup polling, atlas portraits and expanded trinket effect/condition presentation. Native priority-bug verification awaits launch permission; round 75 visibility remains blocked.
- Next round 96: remaining cold-menu trinket effect fields/conditions or persistent activity-log weeks/art. Cold tooltip coverage remains 159 complete/28 partial/4 blank of 191 base trinkets. Later: secret rooms and Memorial. Skip [user]/[blocked].
- Outstanding owner choices: DLC content, game modes after the Darkest Dungeon, trinket retention on party wipe.

## Round 96: category inventory thresholds in trinket descriptions

Cold audit confirmed Cleansing Clasp was blank and three Antiquarian items partial.
Installed condition_data_export ITEM_TAG_AMOUNT defines Baubles thresholds 25/50/75/100/150.
Native ConditionDescription uses item_tag_<category>, raw comparison and
effect_tooltip_condition_item_tag_amount. DD1 base.entries.trinkets.json's buff lists
and Unity port Character/Buff.ToolTip's rule wrapping remain the presentation reference.
Added category amounts to the existing inventory-comparison path without changing units
or combat behavior. Native MULTIPLE food scaling is still withheld, rather than using
ordinary threshold text. Invisible conditions and malformed/missing templates remain guarded.

Cleansing Clasp now shows turn-start Remove 1 Negative Token above 25 Baubles. Celebrated
Chalice shows +10% CRIT above 50 Baubles alongside +4 Speed above 50 Relics; Clarifying
Carcanet +20% DMG above 75 Baubles alongside +20% Max HP above 75 Relics; Shimmering Crown
combat-start -10 Stress above 100 Baubles alongside Heal 100% above 100 Relics. Three tests
exercise actual definitions, raw thresholds, event/stat/heal wording, localized category
names, invalid placeholders and templates that omit the effect body. Audit improves from
159 complete/28 partial/4 blank to 163/25/3 among 191. Release + 280 Core + 19 UI tests pass;
both deployed DLLs hash-match. No game process, launch or save access. Native cold-menu
comparison remains [?]. Private scenery and public art remain as round 95.

## Status 2026-10-05: loop resumed, round 96 complete

- Goal remains active. Continue tools/parity_loop.md on claude/practical-wright-hicri0; no DD2 launch until explicitly allowed. No game/saves accessed, no pending restores.
- Round 96 deployed with Release, 280 Core and 19 UI tests green and both DLL hashes matching. Native menu/portrait/quest/lighting/loading/art verification still awaits launch permission; round 75 visibility stays blocked.
- Cold trinket coverage 163 complete/25 partial/3 blank of 191. Next round 97: Thrilling Tablet's visible PARTY TAG inverse/MULTIPLE requirement, token-copy fields for Caked Palette, remaining advanced conditions/effects, or persistent activity-log weeks/art. Skip [user]/[blocked].
- Private regional art remains 24 native-reference panoramas, six per region, at game/PrivateScenery and local-art/combat-extensions. Full prompts/provenance outside Git; runtime supports up to 12. Verify adjacent rooms/reload, footline/props, torch visibility, Shroud ground transitions and load/memory when launch is allowed.
- Local preview server localhost:8766 remains running, exec session 20403. Keep combat-extensions.html available. It shows the complete private pack; linked preview.html shows historical round-93 art. No pending game/save restore.
- Owner requested stopping near 95% account session usage and resuming when reset; last observed five-hour usage was 61%. Check get_usage_limits at round boundaries before doing more work. Goal stays active below that threshold. Other owner choices remain DLC content, post-Darkest-Dungeon modes and wipe trinket retention.

## Round 97: missing-party scaling for Thrilling Tablet

Both Thrilling Tablet buffs use visible TAG ally, PARTY actor, inverse True,
MULTIPLE amount one. Native ConditionCalculation counts living tagged friendly actors
against team size, falling back to four outside combat; ConditionDescription wraps
with tag_ally and effect_tooltip_condition_tag_inverse. Installed DD1 buff lists and
Unity port Character/Buff.ToolTip rule wrapping remain the presentation reference.
The cold formatter now retains +100% DMG and +100% Max HP per Missing Ally. The shared
TAG field guard rejects unknown restrictions; the new party branch requires the actual
inverse/count/source shape and leaves other-ally performer-exclusion wording intact.
No combat stats or party logic changed.

Two actual-data tests cover both bonuses, native/localized labels and malformed templates;
seven fixtures mutate actor, count, comparison, inverse flag, source/exclusion or unknown
restriction and prove no unconditional bonus appears. The first full test run found an
old assertion classifying Tablet as unsupported. Removed that obsolete assertion from
its unrelated Grim Mask test; new Tablet-specific positive and guard tests cover the
implemented behavior. The diagnostic no-build rerun confirmed this was a stale expectation,
not a runtime regression. Final Release + 289 Core + 19 UI tests green, deployed DLL hashes
match; cold audit 164 complete/25 partial/2 blank. No game launch/process or save access.

## Status 2026-10-05: loop resumed, round 97 complete

- Goal active; continue tools/parity_loop.md on claude/practical-wright-hicri0. No DD2 launch until explicitly allowed. No save access or pending restores.
- Locally deployed round 97 with Release + 289 Core + 19 UI tests green, DLL hashes matching. Native priority UI/lighting/loading/scene verification awaits launch permission. Round 75 visibility remains blocked.
- Cold audit 164 complete/25 partial/2 blank of 191. Next round 98: Caked Palette token-copy fields, then remaining advanced conditions/effects or persistent activity-log weeks/art. Anatomical Map remains the other blank because of hidden target restrictions. Skip [user]/[blocked].
- Private scenery remains 24 native-reference images, six per primary region, supported up to 12, under game/PrivateScenery and local-art/combat-extensions; full prompts/provenance and native references stay outside Git. Verify room diversity/reload, hero/prop ground band, torch visibility, Shroud joins and repeated load/memory after launch is allowed.
- Local server localhost:8766, exec session 20403, and combat-extensions.html preview remain available; preview.html retains historical round-93 art. No pending game/save restore.
- Check account session usage at round boundaries. Owner requested stopping near 95% and restarting after reset; last observed usage at round 96 end was 68%. Other owner choices remain DLC content, post-Darkest-Dungeon modes and wipe trinket retention.

## Round 98: Caked Palette token-copy effects

Native EffectDescription formats m_TokenCopyTags/Amount/Range with singular/plural/all
copy templates and token_tag category names. Amount 99 means all, as for native stealing.
EffectInstance.TransferTokens copies target tokens to performer without removing originals;
this is distinct from stealing. DD1 installed buff lists/Unity port Trinket.ToolTip remain
the presentation reference. Cold descriptions now accept bounded integral copy quantities
with zero range, use the native copy templates and retain chance/trigger/conditions.
Unknown categories, selectors, quantities and unsupported ranges remain withheld.

Caked Palette now shows Apply On Hit: Copy Positive Token and Copy All Negative Tokens
(5%). Three actual-data tests preserve both outcomes and localization, plus malformed
individual templates. Six fixtures cover valid plural count two, zero/fractional quantities,
unsupported range, unknown category and unknown selector. All other effect families retain
their previous guards. Audit 165 complete/25 partial/1 blank out of 191. Release + 298 Core
+ 19 UI tests green, deployed DLL hashes match. No game launch/process or save access.

## Status 2026-10-05: loop resumed, round 98 complete

- Goal active; continue tools/parity_loop.md on claude/practical-wright-hicri0. No DD2 launch until explicitly allowed. No save access or pending restores.
- Round 98 deployed with Release, 298 Core and 19 UI tests green and DLL hashes matching. Native priority UI/lighting/loading/scene verification awaits launch permission; round 75 visibility stays blocked.
- Cold audit 165 complete/25 partial/1 blank of 191. Next round 99: DOT-copy fields for Hag's Hoard and related items, remaining advanced effects/conditions, or persistent activity-log weeks/art. Anatomical Map remains blank because of hidden target restrictions. Skip [user]/[blocked].
- Private 24-image native-reference scenery remains installed at game/PrivateScenery and recorded in local-art/combat-extensions, with support up to 12 per region. No native/reference/generated derivative art is committed. Native room/reload/ground/torch/load/memory checks remain [?].
- Local preview server localhost:8766, exec session 20403, remains running. Keep combat-extensions.html open. Linked preview.html retains historical round 93 art. No pending game/save restore.
- Check session usage at round boundaries; owner requests stopping near 95% and resuming after reset. Last observed round 97 end usage 73%. Open owner choices remain DLC content, post-Darkest-Dungeon modes and wipe trinket retention.

## Round 99: retain disease-dependent trinket penalties

The proposed DOT-copy candidate was inaccurate: Hag's Hoard's second buff is instead
performer_is_diseased. Installed condition_data_export defines QUIRK_TAG_AMOUNT,
PERFORMER, disease, GREATER_THAN_OR_EQUAL one. ConditionCalculation counts quirks with
that tag; ConditionDescription uses QuirkDescription.GetTagString and
effect_tooltip_condition_quirk_tag_amount. LocalizationCascadingLookup resolves
quirk+disease to the native disease icon, expanded to Disease by the existing text path.
DD1 base.entries.trinkets.json buff lists and Unity port Character/Buff.ToolTip remain
the presentation reference. No DOT-copy effects appeared in the installed trinket CSVs.

Added only this proven visible self-presence condition. Native template has no count or
actor placeholders, so other actors/counts, inversions, source exclusions and unknown
restrictions stay withheld. Five completed items now keep their penalties: Hag's Hoard
-10% Max HP, Brilliant Brew -20% Move RES, Corrupting Cleaver -15% Stun RES, Hint of Home
-10% DMG and disease-resist Dark Impulse -25% Max HP, each when Disease and alongside
existing benefits. Sickening Silence still has a separate unsupported target condition.

Five actual-data cases plus localization/malformed-template checks and eleven restricted
shape mutations pass. Release + 315 Core + 19 UI tests green; both deployed DLL hashes
match. Audit 170 complete/20 partial/1 blank among 191. No game launch/process or save
access. Native comparison before/after combat initialization remains [?].

## Status 2026-10-05: loop resumed, round 99 complete

- Goal active; continue tools/parity_loop.md on claude/practical-wright-hicri0. No DD2 launch until explicitly allowed. No save access or pending restores.
- Round 99 locally deployed with Release + 315 Core + 19 UI tests green and both DLL hashes matching. Native priority UI/lighting/loading/scene verification awaits launch permission; round 75 visibility stays blocked.
- Cold audit 170 complete/20 partial/1 blank. Next round 100: remaining advanced effect/condition shapes, notably Sickening Silence's diseased target condition, skill selections or other actual incomplete trinkets, or persistent activity-log weeks/art. Anatomical Map remains blank because of hidden target restrictions. Skip [user]/[blocked].
- Private scenery remains 24 native-reference panoramas, six per primary region, at game/PrivateScenery and local-art/combat-extensions, support up to 12 per region. Full prompts/provenance/art remain outside Git. Native room/reload/ground/torch/load/memory checks remain [?].
- Local preview localhost:8766/combat-extensions.html and server exec session 20403 remain available. Linked preview.html retains historical round-93 art. No pending game/save restore.
- Last observed five-hour usage 82%; check at round boundaries. Owner requested stopping near 95% and resuming after reset. No scheduled resume was established yet. Open owner choices remain DLC content, post-Darkest-Dungeon modes and wipe trinket retention.

## Round 100: Peculiar Pods disease removal

Installed remove_1_disease_5pct uses chance .05, disease tag, remove amount one, zero
range. Buff on_hit_as_target_to_target targets the wearer. Native EffectDescription uses
quirk+disease, token_amount_format_singular (normally just the icon/name) and
effect_tooltip_remove_quirk. EffectInstance.ApplyQuirks selects matching unlocked diseases;
default QuirkRemoveRandom is true and source-only false. DD1 ordered buff lists and port
Trinket.ToolTip remain the presentation reference. Only the proven tag/count/range shape
was added; locked/source/random selectors, other tags/counts and ranges stay withheld.

Peculiar Pods now has all three lines: +25% Debuff RES Piercing, Gain When Hit: Remove
Disease (5%) and -2 Speed above 75 Flame. Existing event/chance wrapping remains intact.
Actual installed-data and localization/malformed-template tests plus seven unsupported
shape fixtures pass. Native singular quantity template is respected rather than inventing
a numeric label. Release + 324 Core + 19 UI tests green; both deployed DLL hashes match.
Audit 171 complete/19 partial/1 blank of 191. No game process/launch or save access.

## Status 2026-10-05: loop resumed, round 100 complete

- Goal active; continue tools/parity_loop.md on claude/practical-wright-hicri0. No DD2 launch until explicitly allowed, no save access or pending restores.
- Round 100 locally deployed with Release + 324 Core + 19 UI tests green and both DLL hashes matching. Native priority UI/lighting/loading/scene verification awaits launch permission; round 75 visibility stays blocked.
- Cold audit 171 complete/19 partial/1 blank. Next round 101: actual unsupported conditions, such as Heart-Shaped Padlock's Resolute requirement or Sickening Silence's diseased target, or persistent activity-log weeks/art. Hidden-outcome/visibility cases remain separate. Anatomical Map stays blank because of hidden target restrictions. Skip [user]/[blocked].
- Private 24-image native-reference scenery remains installed at game/PrivateScenery, six per primary region, support up to 12. Full art/prompts/provenance outside Git at local-art/combat-extensions. Native room/reload/ground/torch/load/memory checks remain [?].
- Local preview server localhost:8766, exec session 20403, and combat-extensions.html remain available. Linked preview.html retains historical round 93 art. No pending game/save restores.
- Last observed five-hour usage 84%; check at round boundaries. Owner requested stopping near 95% and resuming after reset; no scheduled resume established yet. Other owner choices remain DLC content, post-Darkest-Dungeon modes and wipe trinket retention.

## Round 101: Heart-Shaped Padlock's Resolute effect

condition_is_resolute is not in condition_data_export: native shared conditions live in
infernal_flame_construction_export.Group.csv. It defines OVERSTRESS/NONE/resolute/BOOL
one; ConditionCalculation compares the current overstress ID. Native ActorDataEffects
on_overstress heading is intentionally whitespace. ConditionDescription supplies
overstress_condition_resolute through effect_tooltip_condition_overstress. Cold reader
previously omitted the shared file and rejected its blank event heading. DD1 buff lists
and Unity port Trinket.ToolTip remain the presentation reference.

Load only Condition blocks from the shared construction table without overriding
ordinary definitions. Permit a blank on_overstress heading only when every effect has
one explicit overstress all-condition and no any-conditions; validate exact native
BOOL/NONE/resolute or meltdown shape before rendering. Unknown restrictions, hidden
conditions and missing data remain withheld. Event label gets a readable space after
its colon. Heart-Shaped Padlock now shows On Resolute: Add 2 Positive Tokens and the
independent Random Ally When Healed: Add 1 Positive Token (33%). No combat logic changed.

Two actual-data/localization tests and eight fixtures cover missing shared data, absent
requirements, primary-table precedence, actor/value/count/inverse/source/visibility/unknown
restrictions and malformed effect templates. Release + 334 Core + 19 UI tests green,
locally deployed DLL hashes match. Audit 172 complete/18 partial/1 blank of 191. No DD2
process/launch or save access; native before/after-initialization comparison remains [?].

## Status 2026-10-05: loop resumed, round 101 complete

- Goal active; continue tools/parity_loop.md on claude/practical-wright-hicri0. No DD2 launch until explicitly allowed; no save access or pending restores.
- Round 101 locally deployed with Release + 334 Core + 19 UI tests green and both DLL hashes matching. Native priority UI/lighting/loading/scene verification awaits launch permission; round 75 visibility stays blocked.
- Cold audit 172 complete/18 partial/1 blank. Next round 102: remaining actual advanced effect/condition fields, such as Sickening Silence's diseased target or other incomplete trinkets, or persistent activity-log weeks/art. Hidden-outcome/visibility cases remain separate; Anatomical Map stays blank. Skip [user]/[blocked].
- Private scenery remains 24 native-reference panoramas, six per primary region, installed at game/PrivateScenery, support up to 12. Native-derived art/full prompts/provenance remain outside Git at local-art/combat-extensions. Native room/reload/ground/torch/load/memory checks remain [?].
- Local preview server localhost:8766, exec session 20403, and combat-extensions.html remain available; linked preview.html retains historical round 93 art. No pending game/save restores.
- Last observed five-hour usage 87%; check at round boundaries. Owner requested stopping near 95% and resuming after reset; no scheduled resume established yet. Other owner choices remain DLC content, post-Darkest-Dungeon modes and wipe trinket retention.

## Round 102: native rank parameters in trinket effects

Installed performer_is_in_rank_1/4 uses RANK/PERFORMER/PARAMETER. ConditionDefinition
initializes at CSV number minus one and rounds it; ConditionDescription adds one and
uses comparison_equals_label, which labels rank rather than numeric threshold scaling.
Cold Compare rejected PARAMETER. DD1 shared/buffs/base.buffs.json in_rank rules and
Unity port Buff.ToolTip's SingleParam+1 provide the position-label reference.

Added a dedicated guarded rank path, allowing only integral CSV positions 1..4 and
the proven self/PARAMETER shape. Generic comparisons stay unchanged; other actors,
inversions, source exclusions, hidden and unknown restrictions stay withheld. Ghastly
Gruel now keeps Rank 1 +50% Healing Received from Skills and Rank 4 +25% Healing Given
from Skills alongside its round-end heal. Tormenting Locket keeps +10% CRIT in Ranks
1 and 4. Bloodied Branch keeps turn-start Bleed 1 (3 Turns) in Rank 4 alongside benefits.

Four actual-data/localization cases plus twelve rank/actor/restriction fixtures pass.
Old Ghastly Gruel healing test assumed the item remained incomplete with only one line;
updated those expectations while keeping heal amount/chance checks. Its broad "5%"
substring assertion then matched the legitimate +25% bonus; changed it to "(5%)".
One patch initially targeted the adjacent assertion; restored that unrelated test and
fixed the intended line. Initial failures were stale test assumptions, not a rank
implementation failure. Deployment was prematurely triggered before inspecting one
failed test result; no game was running. Final full green suite and deployment/hash
verification supersede it. Release + 350 Core + 19 UI tests green, both installed DLLs
match the verified build. Audit 175 complete/15 partial/1 blank of 191. No game launch
or save access. Native before/after-initialization comparison remains [?].

## Status 2026-10-05: loop resumed, round 102 complete

- Goal active; continue tools/parity_loop.md on claude/practical-wright-hicri0. No DD2 launch until explicitly allowed; no save access or pending restores.
- Round 102 locally deployed with Release + 350 Core + 19 UI tests green and both DLL hashes matching. Native priority UI/lighting/loading/scene verification awaits launch permission; round 75 visibility stays blocked.
- Cold audit 175 complete/15 partial/1 blank. Next round 103: actual remaining advanced fields/conditions, Sickening Silence's diseased target or persistent activity-log weeks/art. Many remaining partials involve hidden effects or restrictions; do not weaken the blocked visibility policy to improve counts. Anatomical Map stays blank. Skip [user]/[blocked].
- Private scenery remains 24 native-reference panoramas, six per primary region, installed at game/PrivateScenery with support up to 12. Full art/prompts/provenance remain outside Git at local-art/combat-extensions. Native room/reload/ground/torch/load/memory checks remain [?].
- Local preview localhost:8766/combat-extensions.html and server exec session 20403 remain available; linked preview.html retains historical round 93 art. No pending game/save restores.
- Last observed five-hour usage 92%, reset 2026-10-05 08:44:05 UTC / 10:44:05 Paris. Check at round boundaries. Owner requested stopping near 95% and resuming after reset. Created active thread heartbeat resume-darkest2in1-loop-after-quota-reset, hourly at minute 50, to check allowance and continue this authorized loop after reset without duplicate work; unchanged quota waits stay quiet. Other owner choices remain DLC content, post-Darkest-Dungeon modes and wipe trinket retention.

## Status 2026-10-05: quota pause after round 102

- Five-hour usage reached 95% at the final check. Pause the active goal at the owner's explicitly requested threshold; resume the authorized loop after allowance resets, not before. Reported reset 2026-10-05 08:44:05 UTC / 10:44:05 Paris.
- Round 102 is complete at 5b862c6, pushed, with Release + 350 Core + 19 UI tests green and deployed DLL hashes matching. No uncommitted implementation, DD2 launch, save access or pending restore. No native game verification performed.
- Resume heartbeat resume-darkest2in1-loop-after-quota-reset is ACTIVE and attached to this chat, verified in its saved automation.toml. It checks hourly at minute 50, waits quietly before reset/while allowance is unavailable, and is authorized to continue the loop from this handoff. First hourly check after this reset is approximately 10:50 Paris. Goal status resumption is controlled by the app; the automation prompt explicitly authorizes continuing the work.
- Next round 103 and constraints remain the preceding status: cold audit 175 complete/15 partial/1 blank, advanced actual conditions/effects or activity-log persistence, no weakening blocked visibility guards, no DD2 launch until explicitly allowed. Protected estates/abandoned project remain untouched.
- Private 24-image scenery remains installed, six per region, runtime support up to 12. Browser gallery combat-extensions.html was preserved again this turn, still showing Tangle/04 Creature den; no owner selection changed. Native rendering/ground/torch/load checks remain [?]. Server localhost:8766, exec session 20403, remains available. Art/provenance stay outside Git.

## Round 103: persistent weekly Activity Log

DD1 campaign/town/activity_log/activity_log.layout.darkest defines week entries, spacing
and party/hero entry areas. The Unity port persists Campaign.Logs through
WeekActivityLog and PartyActivityRecord. Our panel instead concatenated this week's
TownLog with Driver.HomecomingLog: EndWeek replaced the former and restarting lost
the latter. This also allowed a runtime return report to appear in another estate.

Core now saves per-estate ActivityWeek records containing town messages and copied
party departure/return records, including hero identity/class/death/resolve snapshots.
The four existing Guild/Survivalist/Blacksmith messages and resolved town activities
enter the journal; returns are captured in Homecoming.Report, including interrupted
expeditions returned during load. Driver records departure after the existing week
advance, while the opening remains week zero. Existing saves import only their
surviving TownLog once; no earlier history or lost runtime report is reconstructed.
The panel reads only the saved journal, newest week first, with measured wrapping.
Full DD1 week banners/party art remain a separate presentation gap.

Seven new cases pass: old-save import/reload without deduping repeated messages,
first-action import, real Blacksmith/EndWeek retention and unchanged random rolls,
victory/retreat/death snapshots with unchanged rewards, and estate/opening isolation.
Release + 357 Core + 19 UI tests pass. Deployment was gated on all green results and
absence of DD2 processes; both installed DLL hashes match the verified build. No DD2
launch or owner-save access. Native scroll/wrap/restart/estate checks remain [?].

## Status 2026-10-05: round 103 complete after quota reset

- Continue the authorized loop on claude/practical-wright-hicri0. The heartbeat authorizes work after reset even though the app still reports the earlier goal as paused; no new goal was created. No DD2 launch until explicitly allowed, no save access or pending restores.
- Round 103 locally deployed: Release + 357 Core + 19 UI tests green, both DLL hashes match. Weekly Activity Log persistence is implemented; native verification awaits launch. Next round 104: DD1 activity-log presentation/long-history rendering, using the installed artwork and Unity port; other documented safe gaps remain eligible.
- Cold audit unchanged: 175 complete/15 partial/1 blank. Round 75 visibility remains blocked; do not weaken hidden-effect guards for coverage. Private scenery unchanged: 24 panoramas, six per region, runtime supports up to 12; artwork/prompts/provenance outside Git.
- Preview localhost:8766/combat-extensions.html/server session 20403 remains untouched. Protected estates and abandoned project untouched; no game/save restore pending.
- Five-hour allowance reset and ordinary usage is available; last observed usage 8%, weekly 63%. Check at round boundaries and stop near 95% with a clean committed handoff. Existing hourly minute-50 resume heartbeat remains active; do not duplicate it.

## Round 104: DD1 Activity Log entries and visible history rendering

Read DD1 activity_log.layout.darkest (panel_size 620,550; entry vertical_spacing 20)
and inspected activitylog_bg, week_title_bar, raid_success/failure_banner and
hero_activity_entry_backdrop. Unity port WeekLogSlot presents return, town, departure;
PartyActivityRecordSlot uses saved names/classes and deadhero_portrait. Our plain
text history lacked these entries and measured every historical row on each draw.

The log now uses the native DD1 background and left viewport, week bars, success/failure
banners, town-message bands and saved party portraits/names (native DD2 class icons,
DD1 death portrait). DD1 bitmap-font measurements include long names and wrap text;
region labels use the area's display name. Layout is cached by estate/week/message
counts/font, and binary visible-row lookup draws only intersecting entries. Estate
switches reset scroll; the oldest entry remains reachable. The activity background
is drawn before the existing close control. Generic town records are still strings:
typed town actor/building/level-up illustrations and caretaker goals remain gaps.

Four actual-layout tests cover newest-first weeks and DD1 entry ordering, wrapping/
non-overlap, 1,000 saved weeks with bounded visible rows and oldest reachability, and
empty/opening bounds. All used native image sources were inspected. Release + 357
Core + 23 UI tests pass. Green-gated deployment with DD2 stopped; both deployed DLL
hashes match. Native appearance/portraits/scroll/input remain [?]; no game launch or
owner-save access, and no native assets entered Git.

## Status 2026-10-05: round 104 complete

- Continue the authorized loop on claude/practical-wright-hicri0. No DD2 launch until explicitly allowed, no save access or restore pending; app goal still reports the earlier quota pause, while the owner heartbeat authorizes this resumed work.
- Round 104 locally deployed with Release + 357 Core + 23 UI tests green and DLL hashes matching. Activity Log has saved weeks and initial DD1 presentation; native checks await launch. Next round 105: another documented missing Core/UI behavior, such as secret-room discovery/access, caretaker goals or typed town entries; inspect DD1 data/Unity references before selecting.
- Cold audit unchanged 175 complete/15 partial/1 blank. Round 75 hidden visibility remains blocked. Private 24-image scenery pack and preview localhost:8766/combat-extensions.html/session 20403 remain unchanged and outside Git.
- Last observed five-hour usage 12%, weekly 64%; check round boundaries and stop near 95% with committed handoff. Existing hourly minute-50 resume heartbeat remains active. Protected estates and abandoned project untouched.

## Round 105: saved actors on town Activity Log entries

DD1 activity_log.layout.darkest uses hero portraits beside a 440-pixel text box.
Unity port ActorActivityRecord copies Actor/HeroClass when logging an outcome;
ActivityRecordSlot reads that class without consulting the current roster. Our
saved town messages had no actor metadata, preventing accurate historical portraits.

Core now attaches copied hero ID/name/class to append-only town-message indices.
Guild/Blacksmith/Survivalist action messages include their actor; EndWeek associates
all messages emitted by each hero with that hero, including rest, activity/side
outcomes, sanitarium and missing returns. TownResults copies batch metadata and
rejects invalid indices; legacy messages retain their text with no guessed actor.
The UI uses the saved DD2 class portrait and DD1's 440-pixel body width; wrapped
height accounts for the narrower text. No gameplay outcomes/RNG changed.

Two new Core cases plus the expanded real Blacksmith check cover old-message offsets,
copied batches, invalid indices, rest/prayer/treatment/missing outcomes, exact message
association and names/classes surviving rename/removal/death/reload. One actual-layout
case checks actor versus legacy text widths and long-message height. Release + 359
Core + 24 UI tests pass. Green-gated deployment with DD2 stopped; installed DLL hashes
match. Native portrait/wrap behavior remains [?]; no game launch or save access.

## Status 2026-10-05: round 105 complete

- Continue the authorized loop on claude/practical-wright-hicri0. No DD2 launch until explicitly allowed; no save access or pending restores. The app's prior paused goal status is not programmatically resumable; this heartbeat explicitly authorizes continued work.
- Round 105 locally deployed: Release + 359 Core + 24 UI tests green; both DLL hashes match. Activity Log persistence, week/raid art and saved town actor portraits are implemented, awaiting native checks. Next round 106: remaining activity-log building-upgrade/resolve entries or another documented safe gap after DD1/Unity inspection.
- Cold audit unchanged 175 complete/15 partial/1 blank; blocked visibility guards remain. Private art: 24 installed panoramas, six per region, supports up to 12, outside Git. Preview localhost:8766/combat-extensions.html/session 20403 unchanged.
- Last observed five-hour usage 15%, weekly 65%; check each round and stop near 95% with committed handoff. Existing hourly minute-50 resume heartbeat remains active. Protected estates and abandoned project untouched.

## Round 106: resolve-level Activity Log cards

Read DD1 roster.variables.json resolve_level_thresholds (0,2,8,14,24,36,48),
inspected hero_level_up_entry_backdrop, and checked Unity port ActorActivityRecord's
ActivityType.LevelUp and ActivityRecordSlot. Return reports already included resolve
messages but stored/rendered them as generic text, losing the actor and entry kind.

Homecoming now marks the existing message's index when a surviving hero actually
levels up. It copies hero identity/class and LevelUp kind; Journal.Return copies
bounded indexed metadata into the saved raid. The UI renders that same line once
with DD1's level-up band/notable color and the saved DD2 class portrait. No name or
message matching, duplicated text, or inferred actors for older untyped records.
Resolve/XP/reward/random behavior stays in the existing rules.

Three Core cases cover two heroes with identical names receiving distinct indexed
cards, copied metadata/reload, death exclusion, retreats and maximum resolve, with
unchanged XP/random counts. One actual-layout case verifies a single typed line and
legacy fallback. Release + 362 Core + 25 UI tests pass; deployment gated on green
results and DD2 stopped. Both deployed DLL hashes match. Native appearance remains
[?], no launch or owner-save access, and native art remains outside Git.

## Status 2026-10-05: round 106 complete

- Continue the authorized loop on claude/practical-wright-hicri0. No DD2 launch until explicitly allowed; no save access or restore pending. The app goal still reports the earlier pause; the owner heartbeat authorizes continued work after reset.
- Round 106 locally deployed: Release + 362 Core + 25 UI tests green, both DLL hashes match. Activity Log now has saved history, week/raid art, town actor portraits and resolve cards, awaiting native checks. Next round 107: remaining actual building-upgrade entries/caretaker goals or another documented safe gap after DD1/Unity inspection.
- Cold audit unchanged 175 complete/15 partial/1 blank. Hidden visibility remains blocked. Private 24-image scenery and preview localhost:8766/combat-extensions.html/session 20403 unchanged and outside Git.
- Last observed five-hour usage 17%, weekly 65%; check each round and stop near 95% with committed handoff. Existing hourly minute-50 resume heartbeat remains active. Protected estates and abandoned project untouched.

## Round 107: building-upgrade records and DD1 completion percentage

DD1 upgrades/building/stage_coach.upgrades.json contains 13 levels across the three
trees. The localization str_building_upgraded_to_percent logs the building's total
completion; building_upgrade_entry_backdrop supplies its band. Checked port
Estate.BuyUpgrade/GetBuildingUpgradeRatio and UpgradableBuildingWindow: progress is
rounded (Mathf.RoundToInt), whereas our panel truncated 1/13 to 7% rather than 8%.

Successful Hamlet.BuyUpgrade now appends one town message with a copied building,
tree/code and completion-percent snapshot. Rejected/duplicate purchases return
before logging. UpgradeTrees.Percent counts only installed levels of the specific
building and rounds to even, shared by the panel and journal. Later purchases do
not rewrite historical percentages. UI uses the native building icon/name, inspected
purple band and DD1's own percentage template with formatting markers removed.
Costs, prerequisites and gameplay random sequence remain unchanged.

Three Core cases cover real purchase costs and 8%/15% progress, duplicate/rejected
transactions, reload snapshots, no random advancement, unknown keys, empty/full
buildings and rounding ties. One actual-localization layout case verifies a single
card and template/name/fallback rendering. Release + 365 Core + 26 UI tests pass.
Green-gated deployment with DD2 stopped; DLL hashes match. No launch/save access.
Native purchase/layout checks remain [?]. Found a separate presentation gap:
raid_abandon_banner exists but retreat cards currently use the defeat banner; added
it to PARITY without changing it this round.

## Status 2026-10-05: round 107 complete

- Continue the authorized loop on claude/practical-wright-hicri0. No DD2 launch until explicitly allowed; no save access or restore pending. App goal still reports the prior paused status; resumed work is authorized by the owner heartbeat.
- Round 107 deployed: Release + 365 Core + 26 UI tests green, both DLL hashes match. Saved Activity Log now includes building purchase/completion cards and matching rounded panel progress. Native UI checks await launch. Next round 108: the newly documented distinct retreat banner, then caretaker goals or another documented safe gap.
- Cold audit unchanged 175 complete/15 partial/1 blank; hidden visibility remains blocked. Private 24-image scenery and localhost:8766/combat-extensions.html/session 20403 unchanged and outside Git.
- Last observed five-hour usage 22%, weekly 66%; check round boundaries and stop near 95% with committed handoff. Existing hourly minute-50 resume heartbeat remains active. Protected estates and abandoned project untouched.

## Round 108: DD1 abandonment banner for retreat records

Inspected installed activity_log/raid_abandon_banner.png; its source hash differs
from raid_failure_banner.png. Port WeekLogSlot chooses success/failure from a boolean,
but this mod already saves complete/retreat/defeat outcomes. Retreat cards now select
the dedicated DD1 abandonment image, retaining existing victory/defeat paths.
This is an art-path correction with no rules or save changes, so no new mirror test.
Required Release + 365 Core + 26 UI checks pass. Green-gated local deployment with
DD2 stopped; both installed DLL hashes match. Native appearance remains [?]; no
launch, owner-save access or native art committed.

## Status 2026-10-05: round 108 complete

- Continue the authorized loop on claude/practical-wright-hicri0. No DD2 launch until explicitly allowed; no save access or restore pending. Owner heartbeat authorizes resumed work despite the app's earlier paused goal status.
- Round 108 locally deployed with Release + 365 Core + 26 UI tests green and both DLL hashes matching. Saved Activity Log retains weeks, actors, resolve and building cards, and distinct abandonment art. Native UI checks await launch. Next round 109: missing caretaker goals, starting with supported hero resolve achievements and saved completion, or another documented safe gap after reference inspection.
- Cold audit unchanged 175 complete/15 partial/1 blank; blocked hidden visibility stays. Private 24-image scenery and localhost:8766/combat-extensions.html/server session 20403 unchanged and outside Git.
- Last observed five-hour usage 24%, weekly 66%; check at round boundaries and stop near 95% with committed handoff. Existing hourly minute-50 resume heartbeat remains active; protected estates/abandoned project untouched.

## Round 109: persistent Caretaker roster goals

DD1 str_caretaker_goal_hero_resolve says Raise a class to Resolve Level 6.
ActivityLogWindow/LogQuestGoal in the Unity port display class goals and saved
completion checks. Added CompletedResolveGoals separate from quest IDs. Homecoming
records newly achieved/already-maxed classes before death; dismissal records a
maxed class before removing its hero. Migration recovers only evidence in the
current roster/graveyard, preserving completion thereafter without guessing prior
dismissals. These goal helpers do not alter XP/rewards/week/random state.

The right-hand 600x240 goal area from activity_log.layout.darkest now lists DD2's
recruitable classes, using native Caretaker/Roster Goals headings, resolve template
and completion-only check_mark art. Both menu check images were inspected; pending
goals are left unmarked, following port checkIcon.enabled. Cached labels/heights,
visible rows and independent estate-resetting scroll preserve responsiveness and
keep the caretaker painting below the list. Quest goals remain a separate region-
aware gap, including native lair tiers and independent optional originals.

Five Core cases cover eligibility/idempotence, old-save recovery/reload, dismissal
and trinket return, achievement on reaching six, preservation before death, and
retreat exclusion. Release + 370 Core + 26 UI tests pass. Green-gated local deployment
with DD2 stopped; DLL hashes match. No launch or owner-save access. Native goal
layout/scroll/checks remain [?]. Found another gap during migration inspection:
RepairEstate eagerly consumes NextRng on every load even with no quirks to repair;
added to PARITY and deferred rather than changing it in this round.

## Status 2026-10-05: round 109 complete

- Continue the authorized loop on claude/practical-wright-hicri0. No DD2 launch until explicitly allowed; no owner-save access or restore pending. The app goal retains the prior paused status; the owner heartbeat authorizes continued work after reset.
- Round 109 locally deployed: Release + 370 Core + 26 UI tests green; DLL hashes match. Caretaker roster achievements/list added. Native UI checks remain pending. Next round 110: fix documented eager RepairEstate RNG advancement, retaining legitimate quirk repair rolls and persisting flags. Then region-aware Caretaker quest goals.
- Cold audit unchanged 175 complete/15 partial/1 blank; blocked hidden visibility remains. Private art: 24 installed panoramas, six per region, runtime up to 12. Native-derived artwork/prompts/provenance and preview localhost:8766/combat-extensions.html/session 20403 remain outside Git and unchanged.
- Last observed five-hour usage 29%, weekly 67%; check round boundaries and stop near 95% with committed handoff. Existing hourly minute-50 resume heartbeat remains active. Protected estates and abandoned project untouched.

## Round 110: clean estate repairs preserve gameplay randomness

DD1 starting_save/persist.roster.json saves each hero's quirks; port
Hero(Estate, SaveHeroData) restores saveHeroData.Quirks rather than generating them.
Our RepairEstate allocated NextRng on every load even after all repairs were done.
It now creates one shared repair stream only when missing quirks or the existing
empty-wagon backfill path need it. Nonrandom migrations and equipment recovery
consume none. Legitimate first-time quirk and wagon rolls keep the same seed and
shared sequence. QuirksRepaired now emits a migration message whenever set, even
when all quirks already exist, allowing Session to persist that flag once.

Four Core cases verify repeated clean repair/reload with byte-identical serialized
state and identical next seed, flag-only migration without changing quirks/counter,
exact shared quirk/wagon repair rolls, and nonrandom class-restricted equipment
recovery. Release + 374 Core + 26 UI tests pass. Green-gated local deployment while
DD2 is stopped; both installed DLL hashes match. No game launch/owner-save access.
Native load remains [?]. Found the separate empty-wagon policy issue: selling the
last item produces a valid empty list, but RepairEstate currently restocks it on
reload. Added that gap without changing the stock policy in this round.

## Status 2026-10-05: round 110 complete

- Continue the authorized loop on claude/practical-wright-hicri0. No DD2 launch until explicitly allowed; no owner-save access or restore pending. Owner heartbeat authorizes work after reset despite the app's earlier paused goal status.
- Round 110 deployed: Release + 374 Core + 26 UI tests green, DLL hashes match. Clean estate repair/reload no longer consumes random state when no random repair is needed. Next round 111: preserve legitimate sold-out wagon stock across reload, after DD1/Unity save/week references. Then Caretaker town-event resolve completion and region-aware quest goals.
- Cold audit unchanged 175 complete/15 partial/1 blank; hidden visibility remains blocked. Private 24-image scenery/localhost:8766/combat-extensions.html/session 20403 unchanged and outside Git. Native rendering/performance/UI checks await launch.
- Last observed five-hour usage 32%, weekly 67%; check round boundaries and stop near 95% with committed handoff. Existing hourly minute-50 resume heartbeat remains active. Protected estates and abandoned project untouched.

## Round 111: preserve sold-out wagon stock through reload

DD1 nomad_wagon.building.json defines stock amounts and rarity generation. The port
SaveCampaignData saves every remaining item, Campaign.Load restores that list even
when empty, and Estate.ExecuteProgress calls RestockTrinkets for campaign progress.
Our RepairEstate instead filled an empty saved list on every load, including a
legitimately sold-out visit. Removed that guessed backfill. NewEstate/RefreshWeek
still use the normal DD1 stock generation; an older absent/empty list waits for the
next week rather than inventing missing purchases or advancing random state.

Two new Core cases buy every item, verify exact wallet/owned inventory, serialize/
reload/repair three times with stock/counter/wallet unchanged, and verify next-week
replenishment plus legacy absent-stock behavior. Updated round 110's combined
repair test: missing quirks retain the same repair sequence; empty wagon stock now
stays empty with no extra random roll. Release + 376 Core + 26 UI tests pass.
Green-gated local deployment while DD2 stopped; both installed DLL hashes match.
No game launch/owner-save access. Native shop/reload check remains [?]. Found another
stock discrepancy: RestockWagon discards duplicate IDs after rolling a slot, so it
can underfill DD1's stated capacity. Added that gap for a separate round.

## Status 2026-10-05: round 111 complete

- Continue the authorized loop on claude/practical-wright-hicri0. No DD2 launch until explicitly allowed; no owner-save access or restore pending. Owner heartbeat authorizes continued work after reset despite the app's earlier paused goal status.
- Round 111 deployed: Release + 376 Core + 26 UI tests green, DLL hashes match. Sold-out wagon stock persists until week advance, and clean repair/reload preserves gameplay randomness. Next round 112: fix underfilled wagon slots caused by duplicate filtering, following installed amounts and port RestockTrinkets; then Caretaker town-event resolve completion and region-aware quest goals.
- Cold audit unchanged 175 complete/15 partial/1 blank; hidden visibility remains blocked. Private 24-image scenery and preview localhost:8766/combat-extensions.html/server session 20403 unchanged/outside Git; native UI/performance/rendering checks await launch.
- Last observed five-hour usage 36%, weekly 68%; check round boundaries and stop near 95% with committed handoff. Existing hourly minute-50 resume heartbeat remains active. Protected estates and abandoned project untouched.

## Round 112: fill DD1 wagon slots with valid duplicate copies

Installed nomad_wagon.building.json number_of_trinkets_upgrades is 2/4/6/8/12.
Port NomadWagon.RestockTrinkets appends each slot's valid pick. Our RestockWagon
rolled every slot but discarded duplicate IDs, underfilling the advertised capacity.
It now keeps each valid pick. Catalogue rarity/class eligibility and null withholding
stay in place; no additional attempts/RNG calls. Existing UI renders stock by index
up to 12, and BuyTrinket removes/transfers one copy per purchase.

Seven new cases verify all installed capacities with a repeating valid catalogue,
exact bounded call/RNG counts, absent choices without invented items, and duplicate
purchases/reload one copy at a time with exact wallet/inventory. FakeCatalog's trinket
method is virtual only for this test override; product interface unchanged. Release
+ 383 Core + 26 UI tests pass. Green-gated local deployment while DD2 stopped; both
installed DLL hashes match. Native duplicate display/purchase remains [?]; no game
launch or owner-save access. Native equip limits/combat behavior unchanged.

## Status 2026-10-05: round 112 complete

- Continue the authorized loop on claude/practical-wright-hicri0. No DD2 launch until explicitly allowed; no owner-save access or restore pending. Owner heartbeat authorizes resumed work despite the app's earlier paused goal status.
- Round 112 locally deployed: Release + 383 Core + 26 UI tests green, both DLL hashes match. Wagon now retains valid duplicate slots and stays sold out across reload; clean load preserves random state. Next round 113: native idle-resolve town events, including XP threshold consistency and immediate Caretaker goal recording, after Campaign.cs/Resolve.cs reference inspection. Then region-aware Caretaker quest goals.
- Cold audit unchanged 175 complete/15 partial/1 blank; hidden visibility remains blocked. Private 24-image scenery and localhost:8766/combat-extensions.html/server session 20403 unchanged and outside Git; native UI/performance/rendering checks await launch.
- Last observed five-hour usage 39%, weekly 68%; check round boundaries and stop near 95% with committed handoff. Existing hourly minute-50 resume heartbeat remains active. Protected estates and abandoned project untouched.

## Round 113: town resolve awards keep XP and Caretaker history consistent

Installed base.town_events.events.json gives idle plague_doctor/jester/highwayman
one level; roster.variables.json thresholds are 0/2/8/14/24/36/48. The Unity port's
Campaign.IdleResolve grants the exact remaining XP via Resolve.AddExperience.
Our event changed only ResolveLevel. It now raises cumulative ResolveXp to the
awarded level's threshold, records a newly maxed class immediately, and saves a
LevelUp activity entry with actor identity. The port stores within-level XP; its
subtraction is deliberately adapted to Core's established cumulative-XP model.
Existing class/missing eligibility, event timing and cap stay in place.

Ten cases cover all six boundaries, unchanged other/missing/capped heroes, exact
event RNG, achievement/portrait persistence after reload and dismissal, and quest
XP continuing from the event boundary. First test compile used Rng.Next without
its required bound; corrected before rerun. Final Release + 393 Core + 26 UI tests
pass. Deployment was gated on all green and DD2 stopped; both DLL hashes match.
No game launch or owner-save access; native event/card/goal appearance remains [?].

## Status 2026-10-05: round 113 complete

- Continue the authorized loop on claude/practical-wright-hicri0. No DD2 launch until explicitly allowed; no owner-save access or restore pending. Owner heartbeat authorizes resumed work despite the app's earlier paused goal status.
- Round 113 deployed: Release + 393 Core + 26 UI tests green, DLL hashes match. Town-event resolve now retains cumulative XP, achievements and portrait history. Next round 114: region-aware Caretaker quest goals, reading DD1/Unity references and current native lair progression without quest-generation RNG.
- Cold audit unchanged 175 complete/15 partial/1 blank; hidden visibility blocked. Private 24-image scenery and preview localhost:8766/combat-extensions.html/server session 20403 unchanged/outside Git; native UI/performance/rendering checks await launch.
- Last observed five-hour usage 43%, weekly 69%; check round boundaries and stop near 95% with committed handoff. Existing hourly minute-50 resume heartbeat remains active. Protected estates and abandoned project untouched.

## Round 114: Caretaker goals follow independent campaign regions

DD1 quest.plot_quests.json, miscellaneous.string_table.xml caretaker strings and
Unity ActivityLogWindow/LogQuestGoal list plot identities and check CompletedPlot.
Core now projects supported progression plots plus the existing native lair tier
IDs, without constructing an offer or drawing a seed. New campaigns show 12 native
lair tiers and five story goals. Enabled optional DD1 boss chains remain separate;
completed goals from disabled regions remain visible without changing progress.
Unsupported town invasion and repeatable event quests are not invented goals.

The 600x240 Caretaker view uses the existing next_hero arrow to switch Quest/Roster
lists, each retaining its own scroll. Names/heights are cached; checks read current
saved completion. Native lairs use native boss/region/tier words, and an intro in
the Sprawl no longer claims its destination is the Ruins. Estate changes reset
the view. Six Core cases cover all default identities, separate DD1/native checks,
region toggles/history, no Sluice lair, repeated views/save with no state/RNG change,
and the actual generated PlotOffers' IDs/regions/bosses. Release + 399 Core + 26 UI
tests pass; stopped-game deployment and both DLL hashes verified. No owner saves or
game launch. Native arrow/scroll/text/check appearance remains [?]. Updated the
stale Activity-log parent checklist to reflect delivered work and pending checks.

## Status 2026-10-05: round 114 complete

- Continue the authorized loop on claude/practical-wright-hicri0. No DD2 launch until explicitly allowed; no owner-save access or restore pending. Owner heartbeat authorizes resumed work despite the app's earlier paused goal status.
- Round 114 deployed: Release + 399 Core + 26 UI tests green, DLL hashes match. Caretaker quest/roster lists now follow independent native/optional areas. Next round 115: inspect documented plot-map secret rooms/doors against DD1 maps and Unity; split into small verified map/crawl steps if needed.
- Cold audit unchanged 175 complete/15 partial/1 blank; hidden visibility blocked. Private 24-image scenery and preview localhost:8766/combat-extensions.html/server session 20403 unchanged/outside Git; native UI/performance/rendering checks await launch.
- Last observed five-hour usage 47%, weekly 70%; check round boundaries and stop near 95% with committed handoff. Existing hourly minute-50 resume heartbeat remains active. Protected estates and abandoned project untouched.

## Round 115: retain native plot secret rooms and door links

Read owned DD_map4.dm and town_invasion_0.dm through existing Dd1Binary (no owner
saves). Static door_to.area_to and dynamic content establish corA.tile17 (12,3)
-> rooC (12,5), and corC.tile2 (8,1) -> rooA (5,1). The Unity port includes both
maps and parses secret_room_treasures into SecretTresures; no secret movement code
was found. Split the larger missing mechanic into metadata and access/loot steps.

Importer now retains code-9 rooms after normal rooms, preserving normal IDs and
prop-roll order, and code-13 door targets on their exact hall squares. IsSecret /
SecretRoomId survive saves with false/-1 defaults for older maps. Main corridor
endpoints stay unchanged. QuestRooms defines the ordinary graph/explore quota;
secret visits cannot fulfill it and are never required. Secret minimap icons stay
withheld until access exists; this step does not expose an unusable UI choice.
Four cases cover both actual native links/coordinates, main graph/boss, reload,
old-map defaults and exploration exclusion. Existing native map counts now include
the retained rooms. Initial test compile guessed movement names; corrected to
existing Travel/Step(bool). Final Release + 403 Core + 26 UI tests pass. Deployment
green-gated while stopped, both DLL hashes match. No DD2 launch/owner-save access.

## Status 2026-10-05: round 115 complete

- Continue the authorized loop on claude/practical-wright-hicri0. No DD2 launch until explicitly allowed; no owner-save access or restore pending. Owner heartbeat authorizes resumed work despite the app's earlier paused goal status.
- Round 115 deployed: Release + 403 Core + 26 UI tests green, DLL hashes match. Plot secret metadata is saved, hidden and excluded from normal exploration. Next round 116: inspect critical scouting/secret treasure references, add branch discovery/entry/return with exact corridor resume and saved state; split loot/UI if needed.
- Cold audit unchanged 175 complete/15 partial/1 blank; hidden visibility blocked. Private 24-image scenery and preview localhost:8766/combat-extensions.html/server session 20403 unchanged/outside Git; native UI/performance/rendering checks await launch.
- Last observed five-hour usage 50%, weekly 70%; check round boundaries and stop near 95% with committed handoff. Existing hourly minute-50 resume heartbeat remains active. Protected estates and abandoned project untouched.

## Round 116: secret branches preserve native accessibility and exact return

Inspected static hd_always_accessible: both installed plot secret doors are true.
DD_map4 disables scouting, so critical-only access would permanently hide its
branch. Imported the flag and reveal it when the party reaches the door square,
or scouting reaches it. Ordinary doors use critical scouting within the existing
square budget. DD1 dynamic crit_scout and Unity RaidSceneManager.ScoutingEvent's
6/12-square distinction inspected; no port secret movement implementation exists.

Core EnterSecretRoom requires the current accessible, unblocked door. New saved
return fields preserve corridor/tile/heading independently of normal room retreat
state. Exit validates all three and emits only EnteredTile: it does not execute
movement or replay torch/stress/hunger/ambush/RNG. Secret rooms skip ordinary
scouting, remain optional for exploration, and still await player UI/contents.
Nine cases cover actual access, both directions and mid-branch reload, critical
budgets, walking discovery, blocked/ended refusal and invalid saved return.
Release + 412 Core + 26 UI tests pass. Green-gated stopped-game deployment; both
DLL hashes match. No game launch or owner saves. Native verification remains [?].

## Status 2026-10-05: round 116 complete

- Continue the authorized loop on claude/practical-wright-hicri0. No DD2 launch until explicitly allowed; no owner-save access or restore pending. Owner heartbeat authorizes resumed work despite the app's earlier paused goal status.
- Round 116 deployed: Release + 412 Core + 26 UI tests green, DLL hashes match. Secret branch rules/discovery/saved return exist; UI still withheld. Next round 117: inspect exact secret curio_prop hashes (town is heirloom_chest, DD_map4 hash 163014735 unresolved) and add usable controls/markers with safe contents; do not invent a generic stash. Then remaining Memorial/cold-trinket gaps.
- Cold audit unchanged 175 complete/15 partial/1 blank; hidden visibility blocked. Private 24-image scenery and preview localhost:8766/combat-extensions.html/server session 20403 unchanged/outside Git; native UI/performance/rendering checks await launch.
- Last observed five-hour usage 53%, weekly 71%; check round boundaries and stop near 95% with committed handoff. Existing hourly minute-50 resume heartbeat remains active. Protected estates and abandoned project untouched.

## Round 117: secret contents use native prop identity and sprite aliases

Owned curios/curio_props.csv resolves DD_map4 curio_prop hash 163014735 to
thanks_chest, sharing unlocked_strongbox art, and town hash 283272093 to
heirloom_chest. The curio type library gives Thanks Chest one THANKS loot draw;
loot.json gives that draw journal_page specific_page_index 0. It is not a generic
secret stash. Unity map copies/parser inspected; no equivalent prop CSV loader
or secret handling was found. CurioLibrary now owns sprite aliases, and the plot
importer reads prop hashes from its authoritative CSV names. Secret effect IDs
and saved taken state retain identity; main room/corridor prop rolls stay intact.
DrawProp resolves the sprite alias without replacing its gameplay curio ID.

Four new cases cover exact hashes/contents/sprites, serialized taken state, native
THANKS outcome, unknown alias fallback and missing-name withholding. Existing
branch tests now expect the legitimate Curio event on entry. Release + 416 Core
+ 26 UI tests pass; green-gated stopped-game deployment, both DLL hashes match.
No game launch or owner saves. UI entry remains withheld: the existing loot roller
omits journal_page, so enabling this chest now would discard its only reward.
Next round implements page collection before exposing branch controls/markers.

## Status 2026-10-05: round 117 complete

- Continue the authorized loop on claude/practical-wright-hicri0. No DD2 launch until explicitly allowed; no owner-save access or restore pending. Owner heartbeat authorizes resumed work despite the app's earlier paused goal status.
- Round 117 deployed: Release + 416 Core + 26 UI tests green, DLL hashes match. Secret contents/aliases now reference actual DD1 props; journal payout and player controls still pending. Next round 118: DD1/Unity journal loot collection and persisted owner-estate page identities using synthetic tests only; then branch UI and Memorial pages.
- Cold audit unchanged 175 complete/15 partial/1 blank; hidden visibility blocked. Private 24-image scenery and preview localhost:8766/combat-extensions.html/server session 20403 unchanged/outside Git; native UI/performance/rendering checks await launch.
- Last observed five-hour usage 57%, weekly 71%; check round boundaries and stop near 95% with committed handoff. Existing hourly minute-50 resume heartbeat remains active. Protected estates and abandoned project untouched.

## Round 118: journal rewards survive the pack and homecoming

Installed loot.json THANKS has fixed journal_page index 0; JOURNALONLY ranges 1..21
inclusive. Port DarkestDatabase and RaidSolver preserve those IDs in carried items;
no saved page collection implementation found. LootTables now handles both forms,
and ItemCatalog keeps distinct journal_page+<id> keys with normal one-slot capacity.
Homecoming stores carried indices in CollectedJournalPages only when an outcome
records a survivor, on victory or retreat. Wiped/unknown-survivor/uncarried pages
do not become history; duplicate copies add no achievement or monetary reward.
Older saves start empty. Collection uses no RNG and changes no other wipe-loot
policy. Pack art uses native inv_journal_page.png with localized title/description
and a carry/discard hint. Saved Memorial reading remains the next UI step.

Eight cases cover native fixed page/RNG, inclusive ranged/deterministic rolls,
actual Thanks Chest with full pack/overflow/reload/single use, victory/retreat/wipe,
unknown survivors, duplicate collection, old estates and discarded pages. Release
+ 424 Core + 26 UI tests pass. Green-gated stopped-game deployment, both DLL hashes
match. No DD2 launch or owner saves. Native inventory/loot/return checks remain [?].
Secret entry controls are still withheld until the following UI round.

## Status 2026-10-05: round 118 complete

- Continue the authorized loop on claude/practical-wright-hicri0. No DD2 launch until explicitly allowed; no owner-save access or restore pending. Owner heartbeat authorizes resumed work despite the app's earlier paused goal status.
- Round 118 deployed: Release + 424 Core + 26 UI tests green, DLL hashes match. Exact native secret contents and their journal reward now persist. Next round 119: safe secret minimap/entry/return controls and existing fades, including no unreachable-route softlock; then collected Memorial journal reading/narration.
- Cold audit unchanged 175 complete/15 partial/1 blank; hidden visibility blocked. Private 24-image scenery and preview localhost:8766/combat-extensions.html/server session 20403 unchanged/outside Git; native UI/performance/rendering checks await launch.
- Last observed five-hour usage 62%, weekly 72%; check round boundaries and stop near 95% with committed handoff. Existing hourly minute-50 resume heartbeat remains active. Protected estates and abandoned project untouched.

## Round 119: usable secret-room controls and map markers

Inspected native marker_secret.png (gold star) and port RaidMapRoomSlot /
RaidMapHallSectorSlot's known-icon visibility. Crawl map now shows discovered
door/branch stars; Enter/Return controls sit to the right of the curio sidebar,
only outside camp/blocked/modal loot paths. Driver uses Core entry/exit with
saved state, existing room-transition audio/fade and stopped walking. Keyboard
directions return from the branch; ordinary map destinations first return then
route. Secret map clicks use guarded native entrance lookup and approach a local
square; distant clicks give approach guidance rather than enqueueing an isolated
room. Core Return remains exact and effect-free. Modal and pending travel guard
transitions; stale step-interrupt flags are cleared after explicit stopped changes.

Found general curio map reveal exposed all IsSecret rooms after round 115.
Corrected it to normal rooms plus native always-accessible branches; ordinary
secrets retain critical-only discovery. Three new cases cover guarded entrance
lookup and both reveal policies; existing branch/reload/loot tests still pass.
Release + 427 Core + 26 UI tests green; stopped-game deployment and both DLL hashes
verified. No game launch/owner saves. Native marker/buttons/keyboard/fades/loot
need allowed playtesting. Fixed map secrets exist only on newly imported maps;
older saved expeditions retain their saved layout rather than being rewritten.

## Status 2026-10-05: round 119 complete

- Continue the authorized loop on claude/practical-wright-hicri0. No DD2 launch until explicitly allowed; no owner-save access or restore pending. Owner heartbeat authorizes resumed work despite the app's earlier paused goal status.
- Round 119 deployed: Release + 427 Core + 26 UI tests green, DLL hashes match. Plot secret metadata, discovery, exact contents/journal payout and usable controls now exist; native check remains. Next round 120: collected Memorial journal reading from saved page IDs using DD1 localized text/category/backdrop, without revealing uncollected pages; then boss narration.
- Cold audit unchanged 175 complete/15 partial/1 blank; hidden visibility blocked. Private 24-image scenery and preview localhost:8766/combat-extensions.html/server session 20403 unchanged/outside Git; native UI/performance/rendering checks await launch.
- Last observed five-hour usage 66%, weekly 73%; check round boundaries and stop near 95% with committed handoff. Existing hourly minute-50 resume heartbeat remains active. Protected estates and abandoned project untouched.

## Round 120: Memorial reads only collected journal pages

Owned statue_media_info.json supplies the backerjournal category/title/entry art;
statue.layout.darkest supplies the 600x580 list and 10-pixel entry spacing. Native
journal.string_table.xml supplies exact page titles/text, including special page
0. Unity StatueWindow/StatueAudioEntry handles plot audio, with no page reader.
Memorial.Journals is a read-only projection of saved collected IDs, ordered and
withheld when either native title or text is absent. It never creates collection
history or draws a seed. UI appends measured journal cards to the existing videos,
using native art, bounded scroll and an empty-state message. Height/text caches
refresh on collection/font changes and reset scroll/cache on estate changes.

Two Core tests verify ordered native text, unknown/uncollected withholding,
byte-identical state/RNG through viewing and reload, and estate isolation. Two UI
tests verify full long/wrapped text geometry, native spacing and empty collections.
Release + 429 Core + 28 UI tests pass. Green-gated stopped-game deployment, both
DLL hashes match. No game launch or owner saves; native reader/scroll/art check [?].
Boss narration remains a separate missing category, not inferred from journal pages.

## Status 2026-10-05: round 120 complete

- Continue the authorized loop on claude/practical-wright-hicri0. No DD2 launch until explicitly allowed; no owner-save access or restore pending. Owner heartbeat authorizes resumed work despite the app's earlier paused goal status.
- Round 120 deployed: Release + 429 Core + 28 UI tests green, DLL hashes match. Plot secrets and saved journal reading now work in code. Next round 121: native DD1 Memorial boss/Darkest narration, actual audio event/portrait/plot unlock references and coherent optional-region filtering; native DD2 lairs must not receive unrelated DD1 boss narration.

## Round 121: native Memorial boss and Darkest narration

Inspected DD1 statue_media_info.json category regexes/priority, installed portrait
files, narration.json loading-screen quest tags and voiceover.bank FSB names.
Unity StatueAudioEntry and serialized EstateManagement scene confirm the exact
plot/event IDs and locked goal versus completed audio-line caption policy.

Memorial now projects 24 DD1 boss tiers and the first three Darkest quests from
those installed records, with native portraits and captions. Optional DD1 areas
show future entries only while enabled; completed entries survive disabling.
Native DD2 lair completions stay independent. DD4 has no matching loading entry,
so no fourth speech is invented. Measured cached cards preserve category order,
long captions, journal reading, estate reset and bounded visible-row drawing.

DD1 narration indexes its voiceover bank lazily and streams a single FSB sample
without copying game audio. Clicking the active entry stops it; another replaces
it. Playback releases after completion, panel/estate/phase/audio-setting change
and before a video replay. Disabled or unavailable samples cannot be played.

Three Core cases check all 27 native samples/portraits/both captions, region/tier
unlocks, disabled history, DD2 isolation, save/RNG neutrality and estate isolation.
Two UI cases check long captions, native minimum geometry and empty rows.
Release + 432 Core + 30 UI tests pass before stopped-game deploy; both installed
DLL hashes match. No game launched and no owner estate accessed. Native audio,
art and scrolling remain [?].

## Status 2026-10-05: round 121 complete

- Loop continues under owner authorization; native app goal still reports paused and cannot be resumed by the status tool. No duplicate goal created.
- Round 121 deployed: Release + 432 Core + 30 UI tests green, DLL hashes match. Memorial videos, saved journals and independent native DD1 narration now exist in code. Next round 122: inspect remaining visible cold-menu trinket conditions, starting with target disease on Sickening Silence; hidden/blocked policy remains intact.
- Ordinary usage available; session 75%, weekly 74%; five-hour reset 2026-10-05 13:50:52 UTC (15:50:52 Paris). Continue at boundaries until approximately 95% with clean committed handoff. Do not launch DD2 without owner permission; protected estates/abandoned project/private native artwork constraints remain.

## Round 122: target disease no longer hides Sickening Silence retaliation

Read installed DD1 trinket buff lists and Unity Buff.ToolTip Status wrapping.
Native DD2 target_is_diseased is QUIRK_TAG_AMOUNT disease/TARGET >=1, with no
hidden/inverse/source/exclusion restriction. ConditionCalculation counts the
target's tagged quirks; SkillCalculation retains the original condition input
when applying target-to-performer retaliation. ConditionDescription's generic
quirk template omits actor, so cold text retains the localized target label.

Sickening Silence now shows its actual Blight 6 (3 Turns) Apply to Attacker When
Hit alongside the -200% Disease RES self penalty. No combat behavior changed.
Only the proven target presence shape is added; self wording and blocked hidden
policy remain. Other counts/actors/source/exclusion/unknown fields stay withheld.
Two actual-data/localization/malformed-body cases and the existing unsafe disease
fixtures pass (TARGET is now supported, so that fixture checks unsupported BOTH).

Release + 434 Core + 30 UI tests pass before stopped-game deploy; both installed
DLL hashes match. Full cold audit: 176 complete, 14 partial, 1 blank of 191.
No game launched or owner estate accessed; native tooltip comparison remains [?].

## Status 2026-10-05: round 122 complete

- Loop continues under owner authorization; native goal remains paused with no resume API or duplicate goal.
- Round 122 deployed and green. Next round 123: inspect remaining visible trinket choice/conditional fields from actual native data; skip hidden-policy blockers and do not invent unknown effects. Native scene/recruit/combat/reader checks still await owner launch permission.
- Ordinary usage available; session 79%, weekly 75%; five-hour reset 2026-10-05 13:50:52 UTC (15:50:52 Paris). Continue toward approximately 95% with clean committed handoff. Protected estates, abandoned project and private native artwork constraints remain.

## Round 123: Spiked Leather Cap retains its native disease risk

Inspected DD1 quirk_library disease flags and Unity Trinket.ToolTip's complete
buff enumeration. Native add_1_disease_5pct adds one disease-tag quirk with zero
range at 5%; EffectDescription supplies singular/add templates, and EffectInstance
draws an eligible disease from the recipient's quirk bag. Only that exact visible
shape is supported. Unknown rarity, category, selector, count, range and hidden
effects stay withheld, with the existing visibility policy unchanged.

Spiked Leather Cap now retains Gain On CRIT: Add Disease (5%) alongside CRIT when
target Bleed, retaliation Bleed and Bone Saw's additional Bleed. No combat behavior
changed. Two actual-data/localization/malformed-template cases and nine synthetic
unsafe-shape cases pass. Release + 445 Core + 30 UI tests pass before stopped-game
deploy; both installed DLL hashes match. Audit: 177 complete/13 partial/1 blank
of 191. No game launched or owner estate accessed; native tooltip remains [?].

## Status 2026-10-05: round 123 complete

- Loop continues under owner authorization; native goal remains paused and no duplicate goal exists.
- Round 123 deployed and green. Next round 124: inspect Icon of the Light's visible torch -5 at 66% on received CRIT; native EffectDescription templates/units and DD1/Unity torch display are references. Do not alter hidden buff/condition policy to force complete coverage.
- Ordinary usage available; session 81%, weekly 75%; five-hour reset 2026-10-05 13:50:52 UTC (15:50:52 Paris). Continue toward approximately 95% with clean committed handoff. No DD2 launch, protected estates/abandoned project/private artwork constraints remain.

## Round 124: native additive Flame changes retain their raw units

Read DD1 base.effects Darkness/Light torch amounts and Unity Effect.Tooltip's
Global torch branch. Native EffectDescription enumerates RunValues using signed
raw change versus set-to templates; EffectInstance applies the corresponding
ChangeValue or SetValue. Icon of the Light's visible effect is torch -5 at 66%
when critically hit. It now appears beside existing Regen/Consecration benefits.

Only finite nonzero additive torch is supported, using native localization,
event, sign, raw precision and chance. Set-to, unknown metadata, hidden effects
and malformed bodies remain withheld. Three signed/raw-unit fixtures and seven
unsafe value/selector fixtures accompany the actual item/localization checks.
The initial item test wrongly expected it to remain partial. Audit confirmed
torch was its final omitted effect; that expectation was corrected, then all
checks passed before deployment. No red build/test was deployed.

Found a separate gap: Number accepts NaN chance, whose comparisons skip the
suffix and could imply a guaranteed effect. Recorded for next round, not changed
here. Release + 456 Core + 30 UI tests pass; stopped-game installed DLL hashes
match. Audit: 178 complete/12 partial/1 blank of 191. No game launched or owner
estate accessed. Native tooltip comparison remains [?], hidden policy unchanged.

## Status 2026-10-05: round 124 complete

- Loop continues under owner authorization; native goal remains paused without a resume API or duplicate goal.
- Round 124 deployed and green. Next round 125: finite effect chance validation, preserving omitted/default 100%, real 5/66% risks and body retention. Then remaining visible token mutations/conditional effects; skip hidden-policy blockers.
- Ordinary usage available; session 84%, weekly 75%; five-hour reset 2026-10-05 13:50:52 UTC (15:50:52 Paris). Continue toward approximately 95% with clean committed handoff. No DD2 launch, protected estates/abandoned project/private artwork constraints remain.

## Round 125: malformed effect chances cannot read as guaranteed

Read DD1 base effects/Unity Effect percentage parsing and native DD2
EffectDefinition's default chance one plus EffectDescription's fractional chance
suffix. The cold parser accepted NaN through Number; all comparisons were false,
so its suffix vanished. It now validates finite (0,1] chance before generated or
authored descriptions. Malformed/nonfinite/zero/negative/out-of-range values stay
withheld. Omitted/default one remains guaranteed, native 5/66% risks keep units,
and a valid authored body's chance is not duplicated. No combat code changed.

Sixteen generated/authored negative fixtures, four default/fractional cases and
one valid authored case pass. Release + 477 Core + 30 UI tests pass before
stopped-game deploy; both installed DLL hashes match. Full audit unchanged:
178 complete/12 partial/1 blank of 191. No game launched or owner estate accessed;
native tooltip comparison remains [?], existing hidden policy unchanged.

## Status 2026-10-05: round 125 complete

- Loop continues under owner authorization; native goal remains paused without a resume API or duplicate goal.
- Round 125 deployed and green. Next round 126: inspect Early Experiment's visible invert_2_positive_tokens effect and native inversion templates/selector semantics; skip hidden-policy blockers and preserve unknown effect withholding.
- Ordinary usage available; session 86%, weekly 76%; five-hour reset 2026-10-05 13:50:52 UTC (15:50:52 Paris). Continue toward approximately 95% with clean committed handoff. No DD2 launch; protected estates/abandoned project/private artwork constraints remain.

## Round 126: Early Experiment resolves its shared native inversion effect

DD1 complete trinket buff lists/Unity Trinket.ToolTip remain the presentation
reference. Native ActorDataEffectDescription uses SourceDefinition effects from
the complete library. Early Experiment's invert_2_positive_tokens Effect lives
only in boss_blessing_data_export.Group.csv, not the main effect table. Its
native authored text already says Invert 2 Positive Tokens. EffectInstance
replaces up to two eligible tokens with their defined inverse; no generic
inversion description or behavior was invented.

Cold loading now imports only missing Effect definitions from the 6,329-byte
installed boss-blessing table. Primary definitions win. No boss buffs, actors
or runtime libraries are initialized, and ordinary authored/condition/visibility
and unknown-field safeguards remain. Early Experiment now retains Apply On CRIT:
Invert 2 Positive Tokens beside Blight Dealt and its movement-triggered DOT risk.
Three cases cover actual/localized/malformed body and fallback precedence with
unknown-field withholding. Release + 480 Core + 30 UI tests pass before stopped
game deploy; both installed DLL hashes match. Audit 179 complete/11 partial/
1 blank of 191. No game launched or owner estate accessed; native check [?].

## Status 2026-10-05: round 126 complete

- Loop continues under owner authorization; native goal remains paused without a resume API or duplicate goal.
- Round 126 deployed and green. Next round 127: inspect the remaining visible His Rings/Rat Skull/Shambler's Eye/Apron/Snap Judgement fields and referenced tables; choose one documented supported gap, leaving hidden-policy blockers intact. Prepare committed quota handoff at approximately 95%.
- Ordinary usage available; session 90%, weekly 76%; five-hour reset 2026-10-05 13:50:53 UTC (15:50:53 Paris). No DD2 launch; protected estates/abandoned project/private artwork constraints remain.

## Round 127: Rat Skull keeps the living-enemy Creature healing penalty

Read installed DD1 monster-type buffs and Unity Buff.ToolTip EnemyType qualifier.
Native enemy_party_contains_animal is TAG/MONSTERS/animal >=1 without inverse,
source/exclusion or hidden restrictions. ConditionCalculation queries living
enemy team members. Generic TAG text omits actor/presence, so the cold reader
keeps native Enemy/Creature labels and an explicit presence wrapper. A localized
effect_tooltip_condition_enemy_tag_presence can replace the English fallback;
actor, tag and penalty must survive formatting.

Rat Skull retains -66% Healing Received from Skills while an enemy Creature is
present, beside its three existing benefits. Other actor/count/source/exclusion,
hidden and unknown-field requirements remain withheld. Appalling Apron's DOT
cleansing uses hidden conditions and stays withheld under blocked round 75;
no visibility policy changed. Two actual/localization/malformed-wrapper cases
and ten unsafe requirement fixtures pass. Release + 492 Core + 30 UI tests pass
before stopped-game deploy; both installed DLL hashes match. Audit 180 complete/
10 partial/1 blank of 191. No game launched or owner estate accessed; native [?].

## Status 2026-10-05: round 127 complete

- Loop continues toward quota handoff under owner authorization; native goal remains paused without a resume API or duplicate goal.
- Round 127 deployed and green. Next round 128 candidate: Snap Judgement's visible end_combo_snap_judgement (m_IsCombo=True, remove Combo at Speed >=12) needs native marker/description inspection before any code. Other remaining lines mostly involve hidden conditions or unsupported triggers; preserve blocked round 75 policy.
- Ordinary usage available; session 93%, weekly 77%; five-hour reset 2026-10-05 13:50:52 UTC (15:50:52 Paris). Prepare clean handoff at approximately 95%. No DD2 launch; protected estates/abandoned project/private artwork constraints remain.

## Round 128: Combo presentation metadata no longer hides Snap Judgement removal

DD1 complete buff lists and Unity Buff.ToolTip conditional wrapping remain the
presentation reference. Native end_combo_snap_judgement removes one Combo at
Speed >=12, with m_IsCombo=True. SkillCalculation uses that flag only to mark an
already applied result IsCombo, consumed by pop text/haptics; EffectDescription
does not alter the removal body or add an application requirement.

Only well-formed boolean Combo metadata is accepted. Snap Judgement now retains
Turn Start: Remove Combo when Speed is 12 or more beside its other effects.
The Speed threshold, malformed metadata, unknown-field and hidden guards remain.
One actual-data case plus four true/false/invalid boolean fixtures pass. Release
+ 497 Core + 30 UI tests pass before stopped-game deploy; both installed DLL
hashes match. Audit 181 complete/9 partial/1 blank of 191. No game launched or
owner estate accessed. Native tooltip check remains [?].

## Status 2026-10-05: quota pause after round 128

- Stop requested by owner at approximately 95% session usage. Latest API: session 95%, weekly 77%, ordinary usage still available; five-hour reset 2026-10-05 13:50:52 UTC (15:50:52 Paris), resetsAt 1791208252. Stay quiet on scheduled runs before this reset or while quota remains >=95%/ordinary unavailable. After reset, owner authorizes resuming this same loop.
- Native get_goal reports paused, objective unchanged, tokensUsed 722444/timeUsedSeconds 7907 from its earlier pause. Tool cannot resume that status; no duplicate goal or false completion was created. Actual authorized work continued after reset and now stops for quota. The objective is not complete.
- Completed and deployed rounds 103-128 in this authorized continuation, each documented with DD1/Unity references, required tests/Release checks, stopped-game deploy/hash verification, commit and push. Latest green state: 497 Core + 30 UI tests (527 total), Release, both installed DLL hashes match, cold trinket audit 181 complete/9 partial/1 blank of 191. No active test/build/subagent/game process remains.
- Delivered persistent weekly activity history/native cards/Caretaker goals, migration/RNG/wagon/resolve corrections, imported secret-room discovery/contents/journal payout/entry/return controls, collected Memorial journal reader and exact native DD1 boss/Darkest narration, plus visible cold-trinket effects/risks/validation. DD2 lair/optional DD1 progress stays independent. Detailed per-round evidence is above.
- Next round 129: re-read this Status, CLAUDE.md, PARITY.md, tools/handoff_prompt.md and tools/parity_loop.md on claude/practical-wright-hicri0. Inspect remaining visible His Rings and other partial trinket fields from actual installed tables/decomp; choose one documented gap before coding. Shared source-table omissions and metadata are candidates. If a condition is hidden, do not loosen blocked round 75 policy to claim coverage. Appalling Apron's cleansing and several other remaining effects use hidden checks and remain withheld.
- No DD2 launch until owner explicitly permits it. Stagecoach/recruit portrait/input, first-Hamlet load, native arena light/startup timing, regional scenery memory/seams/fades, secret room HUD/payout and Memorial playback/scroll/art remain native checks rather than [x]. Tests here used synthetic state only; protected estates 1/3 and abandoned Documents/DD1inDD2 were never accessed.
- Private native-derived artwork remains outside Git; the 24 approved arena extensions, six per primary region, and existing owner preview are unchanged. Do not kill the localhost 8766 gallery server or alter the owner's selected preview unnecessarily.
- Existing resume-darkest2in1-loop-after-quota-reset heartbeat is ACTIVE, hourly at minute 50, targeting this chat. No duplicate automation was created. Resume locally after reset, one gap per round, green checks before deployment, commit/push, check quota at boundaries and hand off again near 95%.

## Round 129: His Rings keeps its native Dead of Night loot rewards

After the quota reset, inspected installed DD1 trinket buff lists and Unity
Trinket.ToolTip, plus actual DD2 effects and EffectDescription/EffectInstance.
The two guaranteed loot effects have a visible Dead of Night skill condition.
Native labels supply +2 Relics/+1 Bauble; no table quantity is inferred.
The cold reader retains both rewards alongside Combo, CRIT and stress risk.
Fractional loot chances, missing/malformed labels, hidden conditions and unknown
metadata remain withheld. Two actual/localization cases and eleven synthetic
guards pass. An initial assertion used the wrong casing for an existing native
label; corrected that expectation before the green verification and deployment.

Release + 510 Core + 30 UI tests pass. DD2 stopped during deployment; both DLL
hashes match. Audit 182 complete/8 partial/1 blank of 191. Native comparison [?].
No game launched, owner estate accessed or private artwork changed. Removed
outdated duplicate Memorial TODO and contradictory old quota/audit tail bullets.

## Status 2026-10-05: round 129 complete

- Loop resumed under the owner's heartbeat authorization after reset. Native goal still reports paused and has no resume API; no duplicate goal or false completion.
- Round 129 deployed and green. Next round 130: inspect remaining partial native effects/conditions, including Shambler's Eye and Blood-Smeared Calculations. Preserve round 75's hidden-condition policy; choose one documented supported gap before coding or move to another actionable parity item.
- Ordinary usage available; session 5%, weekly 78%; reset 2026-10-05 19:51:32 UTC (21:51:32 Paris), resetsAt 1791229892. Check round boundaries and prepare a clean committed handoff near 95%.
- No DD2 launch until explicitly permitted. Protected estates/abandoned project remain untouched. Private native artwork and the existing localhost 8766 owner preview remain outside Git and unchanged. Native UI, rendering and timing checks remain pending.

## Round 130: menu introduction replay

The remaining Shambler's Eye effect uses hidden target_not_combo_primed;
Blood-Smeared Calculations' extra conversions explicitly set m_IsVisible=False.
These remain withheld under round 75. Moved to the documented menu replay gap.
Inspected DD1 miscellaneous.string_table.xml menu_base_element_watch_intro,
shared/menu/menu.layout.darkest and Unity MainMenuWindow/CampaignSelectionManager.
Watch Intro Cinematic appears above The Hamlet and reuses the existing opening
queue, subtitles, narration and skip handling. It remains disabled until content
loads and does not cache a missing localization table before that point.
The action does not enter an estate, persist, change phase or trigger a raid;
ending/skipping returns to the same menu. No new rules or mirrored UI tests.

Release + 510 Core + 30 UI tests pass; stopped-game deployed DLL hashes match.
Native placement/playback/skip/return remain [?]. No game or owner saves accessed.

## Status 2026-10-05: round 130 complete

- Round 130 deployed and green; loop continues under owner authorization. Native goal still paused without a resume API; no duplicate goal or false completion.
- Next round 131: inspect another documented actionable system gap, including provision fullness/reset behavior against actual DD1/Unity files, or a concrete UI/performance omission. All remaining partial trinkets inspected so far involve hidden policy; do not loosen it to claim completion.
- Ordinary usage available; session 9%, weekly 79%; reset 2026-10-05 19:51:32 UTC (21:51:32 Paris), resetsAt 1791229892. Check boundaries and hand off near 95%.
- No DD2 launch, protected estates or abandoned project access. Private artwork and owner preview unchanged; native UI/rendering/timing checks still pending.

## Round 131: early menu requests no longer poison DD1 font/text caches

Gui.Text requests Dd1Font while the content worker has not published Session.
Get caught that null session and permanently cached a null font; Dd1Text cached
an empty localization table under the same condition. This can keep fallback
fonts/labels throughout a session despite successful DD1 loading afterward.
Inspected all four installed .fnt/TGA resources, native miscellaneous label and
Unity LocalizationManager's load-before-lookup path. Both readers now guard
Session.Dd1 readiness before accessing caches/loading/logging. Genuine missing
resources after readiness retain their existing cached result.

Linked the real Dd1Font/Dd1Text runtime files into the UI suite with a minimal
session/texture shim. The old sources fail both recovery cases; fixed sources
pass all three, including actual metrics/wrapping, main-thread texture ownership,
localization recovery and post-readiness missing files. Shims do not prove GPU
font drawing. Release + 510 Core + 33 UI tests pass; stopped-game deployment
hashes match. No launch, owner-save access or native artwork in Git.

## Status 2026-10-05: round 131 complete

- Round 131 deployed and green; loop continues under owner authorization. Native goal remains paused without a resume API; no duplicate or false completion.
- Next round 132: inspect a concrete remaining startup asset-loading issue or another actionable parity gap. Art.Dd1/Png still synchronously reads/decodes first-use images, and CinematicCache.Voice can synchronously read 79,369,515-byte/46,185,826-byte opening OGVs on first playback. Investigate before choosing one bounded change; do not claim native timing improvement without a permitted trace.
- Ordinary usage available; session 13%, weekly 80%; reset 2026-10-05 19:51:32 UTC (21:51:32 Paris), resetsAt 1791229892. Check boundaries and hand off near 95%.
- No DD2 launch, protected estate/abandoned project access or private art/owner preview changes. Native input/visual/timing checks remain pending.

## Round 132: prewarm initial Hamlet PNGs without first-draw IO/decode bursts

Art.Dd1 still synchronously read/decoded UI PNGs on first draw after the Spine
worker change. Inspected DD1's exact sky/nameplate/currency/navigation files and
Unity EstateSceneManager initialization/fade. Twelve initial images total only
746,364 encoded bytes; warm only this bounded set, not every town state/building.
Driver requests them when DD1 content becomes available at the menu, workers
read files, and a main-thread pump uploads at most one image per Update.
Pending Art.Dd1/Png requests return null for their existing UI fallback; finished
requests share the texture/cache, and task byte buffers release after upload.
Existing unqueued/scenery behavior remains. Missing/faulted assets finish as a
cached fallback, and corrupt images free their failed texture.

Three linked PngPreloader cases cover actual native paths/dimensions, no texture
APIs during request, one-upload/main-thread bounds, duplicate/cache reuse and
missing/corrupt/locked-file failures without starving later images. Native GPU
decode cost/frame timing still unmeasured. Release + 510 Core + 36 UI tests pass;
stopped-game deployed DLL hashes match. No game/owner saves/private art changed.

## Status 2026-10-05: round 132 complete

- Round 132 deployed and green; loop continues under owner authorization. Native goal remains paused without resume API; no duplicate or false completion.
- Next round 133: inspect first-play cinematic audio extraction or another concrete actionable parity gap. OGV audio extraction still reads a large native video on the caller; prepare/streaming/thread ownership need inspection before changes. Native gameplay/input/rendering/timing checks await explicit launch permission.
- Ordinary usage available; session 17%, weekly 80%; reset 2026-10-05 19:51:32 UTC (21:51:32 Paris), resetsAt 1791229892. Check boundaries and hand off near 95%.
- Protected estates and abandoned project untouched. Native artwork and the 24 approved arena variants remain outside Git; existing owner preview/server unchanged.

## Round 133: stream native cinematic narration without full-video allocations

Installed OGV sizes are 79,369,515 and 46,185,826 bytes; first Voice previously
read that entire array, then allocated the extracted track. Inspected native
Ogg/subtitle files and Unity MoviePlayer video/audio path. Core now copies
complete pages of the first Vorbis serial through one 65,307-byte buffer, using
sequential short-read-safe IO. Headers, checksums, sequences and EOS bytes remain
unchanged; incomplete/non-Ogg tails and other serials are excluded as before.
The byte[] API delegates to the same reader without a second input allocation.
Voice writes a unique temporary OGG, rejects no-audio files, publishes after
closing it and cleans temporary failures. Existing ready audio is reused.

Pre-change deployed extractor hashes captured as evidence, without committing
native data: House of Ruin 1,233,823 bytes, FB77CAE6070988C999F9D34FEF2E7E467334C516F30573CE73BB41B42A3ADFF5;
Old Road 661,094 bytes, 290D3E4CDB19634D2C250ABDB7DB745D124F74378EFA4C01CBBAD78201F4B4E8.
Both streaming outputs match. Nine Core cases cover native equality, short reads,
non-seeking streams, serial selection, truncated tails and maximum page size.
Three linked cache cases cover ready reuse/publication and missing/invalid
cleanup. Release + 519 Core + 39 UI tests pass; stopped-game DLL hashes match.
Native first-play timing/audio still [?]; IO remains on caller until background
preparation is added. No launch/owner estate access/private artwork changes.

## Status 2026-10-05: round 133 complete

- Round 133 deployed and green; loop continues. Native goal remains paused without resume API; no duplicate or false completion.
- Next round 134: move voice preparation off the playback caller with pre-resolved plain cache paths, deduplicated work and observable failures; keep FMOD/Unity calls on the main thread. Account for prepare/first-play concurrency before changing the cache. This is a documented latency gap, not a measured native timing claim.
- Ordinary usage available; session 20%, weekly 81%; reset 2026-10-05 19:51:32 UTC (21:51:32 Paris), resetsAt 1791229892. Check boundaries and hand off near 95%.
- No DD2 launch, protected estates or abandoned project access. Private native artwork and owner preview/server remain unchanged/outside Git.

## Round 134: asynchronous narration readiness with main-thread cache ownership

Streaming removed large arrays but still blocked first playback on file IO.
Session's content worker also called Prepare -> Dir -> Unity's save-path API;
ffmpeg Exited called Pump -> Dir from another worker. Inspected native OGVs and
Unity GameIntro/MoviePlayer sequencing. Preparation now starts from Driver;
workers receive plain source/output paths, deduplicate by output and stream
audio independently of ffmpeg availability. Pending results return immediately;
completed failures/temporary-cleanup errors are observed and logged once.
Callbacks enqueue conversion publication/progression for Driver's main-thread
pump, and completed processes dispose before the next conversion.

The actual player waits for voice readiness before starting video/audio/subtitle
time. Skip runs first and can cancel pending playback without starting audio;
the background cache job can finish safely for later reuse. Existing no-video
narration and no-audio video behavior remains. Three linked cache cases now
cover async completion and main-thread SaveDir access; three linked player cases
cover subtitle clock zero after delay, immediate pending skip and video/audio
start/stop on the drawing thread. The first harness compile hit implicit System
Object ambiguity; an explicit Unity Object alias fixed it before green checks.
Release + 519 Core + 42 UI tests pass; stopped-game DLL hashes match. Native
FMOD/video/rendering/timing remains [?], no launch or owner-save access.

New separate gap: repeated Prepare can duplicate an active video conversion;
start failure leaves the remaining queue unpumped and Process undisposed. Logged
in PARITY rather than folded into this round.

## Status 2026-10-05: round 134 complete

- Round 134 deployed and green; loop continues under owner authorization. Native goal remains paused without resume API; no duplicate or false completion.
- Next round 135: address the documented conversion-queue active-name/failure progression gap after actual reference inspection; use synthetic files/process helpers, never owner cache files or DD2 launch.
- Ordinary usage available; session 26%, weekly 82%; reset 2026-10-05 19:51:32 UTC (21:51:32 Paris), resetsAt 1791229892. Check boundaries and hand off near 95%.
- Protected estates/abandoned project untouched. Private native artwork and owner preview/server remain unchanged/outside Git; native UI/rendering/timing checks await explicit launch permission.

## Round 135: conversion queue deduplication and failure progression

Inspected installed opening assets and Unity GameIntro's ordered completion.
Queued names excluded the current conversion, so repeated Prepare could start
it twice; start failure abandoned later movies and leaked the Process wrapper.
Queue jobs now own source/encoder/output paths and unique temporary WebM files,
with one reservation covering both queued/active work. Missing sources and start
failure release their key and progress later jobs. Main-thread completion keeps
ready output intact, publishes only its completed temporary, cleans failed
temporary output, disposes the Process and advances. Picture readiness tracks
the exact active output rather than a substring in process arguments.

Three linked cache tests use synthetic DD1 markers/files/cache paths. Missing
encoder start failure progresses both jobs and permits retry; harmless where.exe
rejects encoder arguments to exercise real asynchronous exit callbacks, active/
queued dedup and external-ready preservation; ready output/missing source skip
without blocking later work. Logging/callback state stays on the pump thread.
No owner cache, native video or DD2 process launched during these tests. Release
+ 519 Core + 45 UI tests pass; stopped-game DLL hashes match. Native ffmpeg
conversion/picture/playback remains [?].

## Status 2026-10-05: round 135 complete

- Round 135 deployed and green; loop continues under owner authorization. Native goal remains paused without resume API; no duplicate or false completion.
- Next round 136: inspect remaining crawl/party system gaps against actual DD1/Unity, including handling of dead heroes in trap/curio/hunger or battle continuation. Choose one proven difference and document it before coding; do not infer missing rules without reference evidence.
- Ordinary usage available; session 30%, weekly 82%; reset 2026-10-05 19:51:32 UTC (21:51:32 Paris), resetsAt 1791229892. Check boundaries and hand off near 95%.
- No DD2 launch, protected estates or abandoned project access. Private native artwork and owner preview/server remain unchanged/outside Git; native UI/rendering/timing checks await explicit launch permission.

## Round 136: require a living investigator for curios

InteractCurio accepted dead, null and unknown hero IDs before resolving ordinary
or quest curios. A key could be consumed, loot taken and a quest completed
without a living investigator. Inspected DD1 heirloom_chest's skeleton_key
interaction and crypts gather/activation goals, plus Unity ExecuteDeath's unit
removal/reselection and CurioEvent's selected-unit effects. The entry point now
rejects actors absent from IParty.Alive before any state/RNG change.

Twelve synthetic cases reproduced the old behavior and pass after the guard.
They cover room/hall, gather/activation, exact save preservation, unchanged next
loot/RNG, valid subsequent interaction and reload. Release + 531 Core + 45 UI
tests pass; stopped-game DLL hashes match. Native hero selection remains [?].
No game launch, owner-estate access or private-art changes.

## Status 2026-10-05: round 136 complete

- Round 136 deployed and green; loop continues under owner authorization. Native goal remains paused without resume API; no duplicate or false completion.
- Next round 137: inspect remaining crawl/party lifecycle gaps against actual DD1/Unity, including stale actions after expedition end. Choose one proven difference and document it before coding.
- Ordinary usage available; session 36%, weekly 83%; reset 2026-10-05 19:51:32 UTC (21:51:32 Paris), resetsAt 1791229892. Check boundaries and hand off near 95%.
- No DD2 launch, protected estates or abandoned project access. Native-derived art and owner preview/server remain unchanged/outside Git; native checks await explicit launch permission.

## Round 137: ended expeditions reject late actions

Only secret-room actions checked State.Ended. Ordinary crawl entry points could
still move, spend/loot, camp, roll enemies or resolve a battle after termination;
late Retreat could rewrite a successful end. DD1's abandonment help and Unity
RaidResultsEvent's raycast/room/hall disable establish the terminal boundary.
Core now enforces it before mutation; QuestComplete alone still allows exploring.

Eight scenario cases exercise room/hall/camp/battle after success/retreat and
synthetic reload, asserting exact save, actor, external loot and RNG preservation.
A ninth follows a completed quest through loot, movement and camp until leaving.
Release + 540 Core + 45 UI tests pass; stopped-game DLL hashes match. Native
transition/input remains [?]. No game launch or owner-estate/private-art access.

## Status 2026-10-05: round 137 complete

- Round 137 deployed and green; loop continues under owner authorization. Native goal remains paused without resume API; no duplicate or false completion.
- Next round 138: inspect remaining high-impact campaign/crawl parity against installed DD1 and Unity. Check camping loot capacity and other concrete resource effects before choosing one documented gap; do not invent rules.
- Ordinary usage available; session 38%, weekly 83%; reset 2026-10-05 19:51:32 UTC (21:51:32 Paris), resetsAt 1791229892. Check boundaries and hand off near 95%.
- No DD2 launch, protected estates or abandoned project access. Native-derived art and owner preview/server remain unchanged/outside Git; native checks await explicit launch permission.

## Round 138: camping rewards respect pack capacity

ApplyCampEffect added loot unconditionally, exceeding 16 slots without a scroll.
Inspected installed pilfer/supply (S), trinket_scrounge (T_ANTIQ_CAMP), native
loot tables and Unity ExecuteCampEffect -> LoadSingleLoot/LootEvent. Real drops
now use existing TryTake capacity checks. LastSpoils represents battle or camp
loot; its scroll draws before camp controls, with native Treasure! text for camp.
Camp remains active and excess can be taken after discarding pack items.

Four cases cover all three native skills, full-pack overflow, freeing/taking a
slot, resolved trinkets, stacking into a full pack, unchanged RNG, use limits and
synthetic reload. Release + 544 Core + 45 UI tests pass; stopped-game DLL hashes
match. Native scroll/camp controls remain [?]. No launch or owner-estate access.
Separate follow-ups recorded: partial transfer capacity and runtime-only overflow
persistence. Inspect native inventory/save behavior before implementing either.

## Status 2026-10-05: round 138 complete

- Round 138 deployed and green; loop continues under owner authorization. Native goal remains paused without resume API; no duplicate or false completion.
- Next round 139: inspect native partial loot distribution versus Core's all-or-nothing TryTake. If proven, fix one bounded transfer gap with exact taken/remainder/slot/RNG tests; overflow persistence is a separate round.
- Ordinary usage available; session 42%, weekly 84%; reset 2026-10-05 19:51:32 UTC (21:51:32 Paris), resetsAt 1791229892. Check boundaries and hand off near 95%.
- No DD2 launch, protected estates or abandoned project access. Native-derived artwork and owner preview/server remain unchanged/outside Git; native checks await explicit launch permission.

## Round 139: partial loot pickup with exact remainder

Installed currency stacks are gold 1750, portrait 3, bust/deed 6 and crest 12.
Unity InventoryItem.MergeItems transfers available capacity and updates the
source remainder; the port's TakeAll still checks whole source stacks. Our
scroll click now adapts that partial transfer. Inventory.TakePartial leaves its
source unchanged and reports an amount; Crawl keeps/copied-taken amounts with
no new RNG or trinket resolution. Automatic TryTake policy remains unchanged.

Seven cases cover native gold/portrait/torch partial stacks, no-room and later
full transfer, multiple empty slots, stable copied amounts, concrete trinkets,
quest-item pass prevention, invalid inputs and pack reload with unchanged RNG.
Release + 551 Core + 45 UI tests pass; stopped-game DLL hashes match. Native
scroll/click remains [?]. No launch, owner-estate or private-art changes.

## Status 2026-10-05: round 139 complete

- Round 139 deployed and green; loop continues under owner authorization. Native goal remains paused without resume API; no duplicate or false completion.
- Next round 140: inspect native save/inventory behavior and close the runtime-only overflow recovery gap in bounded stages, first battle/camping reports, then curios if their alias identities need separate handling.
- Ordinary usage available; session 46%, weekly 85%; reset 2026-10-05 19:51:32 UTC (21:51:32 Paris), resetsAt 1791229892. Check boundaries and hand off near 95%.
- No DD2 launch, protected estates or abandoned project access. Native-derived artwork and owner preview/server remain unchanged/outside Git; native checks await explicit launch permission.

## Round 140: serialize resolved battle/camping loot

DD1 starting_save inventory and Unity SaveCampaignData inventory/formation plus
BattlegroundSaveData.BattleLoot were inspected. No equivalent port serialization
for a currently open loot scroll was found. Our persisted cleared spot/pack
previously lost LastSpoils on reconstruction. PendingSpoils now stores resolved
taken/remainders in ExpeditionState; rebuilding Core reuses them without rolls.
Dismissal clears only the current passable report and persists; successful camp
skills now persist their generated report/use limits. Legacy saves remain null.

Six cases cover actual battle/camp loot, repeated synthetic reload, partial
pickup, dismissal/pass without awards, quest/stale/ended guards and legacy data.
Release + 557 Core + 45 UI tests pass; stopped-game DLL hashes match.

Discovered larger recovery gap: Driver.EnterHamlet forces every started saved
expedition into retreat and clears it; OnRoadReady always resets to the entrance.
DD1/Unity saved raid continuation contradicts the old "DD1 counts that as a
retreat" comment. Logged separately. This round is groundwork only, not native
resume completion. Curio alias/serialization is the next bounded report stage.
No game launch, owner-estate access or private-art changes.

## Status 2026-10-05: round 140 complete

- Round 140 deployed and green; loop continues under owner authorization. Native goal remains paused without resume API; no duplicate or false completion.
- Next round 141: persist curio outcomes/remainders with restored Loot/LeftBehind identities and explicit dismissal; verify quest effects and RNG do not replay. Then inspect actor snapshots and raid resume as separate stages before enabling native continuation.
- Ordinary usage available; session 49%, weekly 85%; reset 2026-10-05 19:51:32 UTC (21:51:32 Paris), resetsAt 1791229892. Check boundaries and hand off near 95%.
- No DD2 launch, protected estates or abandoned project access. Native-derived artwork and owner preview/server remain unchanged/outside Git; native checks await explicit launch permission.

## Round 141: serialize curio outcomes without replay

Inspected installed heirloom_chest, thanks_chest and crypts quest goals plus
Unity CurioEvent/investigated save counts. PendingCurio now stores the already
resolved result. JSON copies shared loot objects, so reconstruction reconnects
LeftBehind to matching Loot display objects one-to-one without rewards/effects
or RNG. Driver reads this stored result; invalid/repeated investigations preserve
it. Dismissal clears only its current passable result and persists.

Seven cases cover native ordinary/journal/gather/activation outcomes, repeated
synthetic reload, distinct/partial links, unchanged quest progress/items and
RNG, guarded dismissal and legacy null. Release + 564 Core + 45 UI tests pass;
stopped-game DLL hashes match. Native recovery/scroll remains [?]; runtime load
still forces retreat pending the separate actor/location/resume stages.
No game launch, owner-estate access or private-art changes.

## Status 2026-10-05: round 141 complete

- Round 141 deployed and green; loop continues under owner authorization. Native goal remains paused without resume API; no duplicate or false completion.
- Next round 142: implement Core re-entry at a saved room/corridor/camp and preserve pending encounter/surprise instead of calling Begin/resetting the entrance. Verify no movement/scout/trap/hunger/RNG replay; inspect installed maps and Unity raid load sequence first. Actor snapshots/runtime resume remain separate stages.
- Ordinary usage available; session 52%, weekly 86%; reset 2026-10-05 19:51:32 UTC (21:51:32 Paris), resetsAt 1791229892. Check boundaries and hand off near 95%.
- No DD2 launch, protected estates or abandoned project access. Native-derived artwork and owner preview/server remain unchanged/outside Git; native checks await explicit launch permission.

## Round 142: Core re-entry preserves position and encounter rolls

Inspected DD1 starting_save in_area/areatile, installed DD_map4 and Unity's
InRaid branch restoring location/direction/torch/camp/formation. Resume now
presents a validated saved spot without ordinary entry traversal. Begin on an
already started crawl delegates to it rather than resetting the entrance.
PendingEncounter stores battle/ambush/surprise as a copy and emits copies on
re-entry, clearing on win/successful fallback. Legacy unresolved battles get
neutral surprise without another draw; exact prior flags cannot be recovered.

Twenty cases cover native plot rooms/secrets, hall interactions/resolved hunger
and traps, active camp/night ambush, groups/surprise/copy ownership, fallback,
invalid/ended rejection and exact saved state/actor/RNG preservation. Release
+ 584 Core + 45 UI tests pass; stopped-game DLL hashes match. Native continuation
is still not enabled: actor snapshots and runtime load routing remain separate.
No game launch, owner-estate access or private-art changes.

## Status 2026-10-05: round 142 complete

- Round 142 deployed and green; loop continues under owner authorization. Native goal remains paused without resume API; no duplicate or false completion.
- Next round 143: inspect actual DD2 actor capture/restore and save living/dead HP/stress/quirk state at existing persistence boundaries. Preserve pre-fight checkpoints before enabling interrupted native combat re-entry; exact mid-turn DD2 combat restoration needs separate evidence.
- Ordinary usage available; session 55%, weekly 86%; reset 2026-10-05 19:51:32 UTC (21:51:32 Paris), resetsAt 1791229892. Check boundaries and hand off near 95%.
- No DD2 launch, protected estates or abandoned project access. Native-derived artwork and owner preview/server remain unchanged/outside Git; native checks await explicit launch permission.

## Round 143: capture DD2 condition into expedition saves

Inspected Unity saved formation/alive state and DD2 ActorInstance's serialized
m_Hp/m_Stress/m_WoundPercent/m_IsLiving and public condition/container getters.
Existing main-thread save boundaries now capture plain per-hero raw condition
and current quirks/trinkets. Core owns copies, ignores foreign/nonfinite samples
and keeps prior data when native actors are unavailable. Confirmed DEAD roster
or IsLiving=false is death; living zero/negative HP is retained as death's door.
Destroyed dead actors use the previous valid HP denominator when needed.
Interrupted-return outcomes now reuse snapshots, fixing the old living-only
projection even before full resume is enabled.

Eleven Core cases cover list ownership, raw HP/stress/wound reload, death's door,
invalid/foreign/missing preservation, legacy estate evidence and actual Homecoming
death/quirk/loadout results. One expectation omitted existing abandonment +2
stress; corrected after the first failure. Release + 595 Core + 45 UI tests pass;
stopped-game DLL hashes match. Native capture/restore remains [?]. No launch,
owner-estate access or private-art changes.

New separate gaps recorded: camp meal lacks immediate save; the dead Homecoming
branch skips recorded quirk changes (verify native fallen/From Beyond behavior).
No change to dead-trinket policy.

## Status 2026-10-05: round 143 complete

- Round 143 deployed and green; loop continues under owner authorization. Native goal remains paused without resume API; no duplicate or false completion.
- Next round 144: inspect/implement DD2 actor restoration without triggering damage/heal/meltdown events and preserve pre-fight party checkpoints. Then route interrupted loads only after all actor/location prerequisites are concrete; exact mid-turn combat restore remains separate.
- Ordinary usage available; session 60%, weekly 87%; reset 2026-10-05 19:51:32 UTC (21:51:32 Paris), resetsAt 1791229892. Check boundaries and hand off near 95%.
- No DD2 launch, protected estates or abandoned project access. Native-derived artwork and owner preview/server remain unchanged/outside Git; native checks await explicit launch permission.

## Round 144: restore recorded DD2 actor condition quietly

DD2 JsonSerializationUtils.ComplexTypeSerializer.ApplyTo assigns only supplied
serializable fields. ActorInstance.UpdateStatus(status, source, isLoad:true)
rebuilds death's-door buffs while skipping effect/event/enter-status callbacks.
Use that native path for m_Hp/m_Stress/m_WoundPercent, then native clamp, status
and prior-max refresh. Do not load the whole actor JSON: GUID and containers
belong to the newly created host actor. Skip equipment's full-health refill
when restoring condition and do not apply stress damage. Core projects a copied
full HeroRecord loadout, overlays recorded changes and excludes confirmed deaths;
missing/foreign/invalid snapshots conservatively use estate evidence.

Six Core cases cover progression, independent lists, empty trinket slots and
legacy/death fallbacks. Seven tests execute the real small restore bridge against
native API shims: raw negative/zero/positive/clamped HP, stress 10 without an
overstress callback, wounds, repeated application, identity/container preservation
and load call order. UI first failed on a missing test namespace import; fixed.
Release + 601 Core + 52 UI tests pass; stopped-game DLL hashes match. Native
restore remains [?]; runtime resume and fight checkpoint are separate stages.
No launch, owner-estate access or private-art changes.

## Status 2026-10-05: round 144 complete

- Round 144 deployed and green; loop continues under owner authorization. Native goal remains paused without resume API; no duplicate or false completion.
- Next round 145: save an explicit pre-fight condition checkpoint before enabling native interrupted-fight restart. Then route valid saved crawls back to their saved position without repeating week/provision/quest rolls. Exact mid-turn DD2 combat reconstruction remains separate.
- Ordinary usage available; session 67%, weekly 88%; reset 2026-10-05 19:51:32 UTC (21:51:32 Paris), resetsAt 1791229892. Check boundaries and hand off near 95%.
- No DD2 launch, protected estates or abandoned project access. Native-derived artwork and owner preview/server remain unchanged/outside Git; native checks await explicit launch permission.

## Round 145: checkpoint native fights before their transition

DD1 starting_save/persist.raid.json records location/party; Unity
Setup/SaveSystem/SaveCampaignData.UpdateRaid records current location, formation
and fighting BattleGroundSaveData. DD2 CombatBhv.SaveToJson/LoadFromJson requires
complete Battle/Arena state. The mod does not save that native state, so a restart
must restore both teams from the beginning rather than combine damaged/dead
mid-fight heroes with fresh enemies.

Store copied pre-fight raw condition, torch, resolved native configuration/arena,
surprise, kind/presentation and translated lineup before DD2 changes mode. The
prepared callback persists first; Session.Persist now reports failure so a failed
write can stop combat. Valid saved checkpoints provide exact restart plans and
resume presentation without crawl RNG. Successful win/fallback clears them;
failed fallback and start rejection preserve them. Native table results are
stored after their roll, not rerolled on re-entry. Runtime load remains separate.

Thirteen Core cases cover room/hall/camp restart, pre-existing versus mid-fight
deaths and damage, exact plans, copied lists, torch/surprise/RNG stability, invalid
and stale state, legacy saves and outcome cleanup. Release + 614 Core + 52 UI
tests pass; stopped-game DLL hashes match. Native restart remains [?]; exact
mid-turn combat restoration is not implemented. No launch, owner-estate access
or private-art changes.

## Status 2026-10-05: round 145 complete

- Round 145 deployed and green; loop continues under owner authorization. Native goal remains paused without resume API; no duplicate or false completion.
- Next round 146: route valid interrupted crawls to a fresh host at their saved position, restore checkpoints/actors before presentation, avoid repeated week/provisions and retain terminal results once. Set Crawling before handling entry events so an immediate fight stays Fighting. Native DD2 run-entry needs guarded failure handling; inspect before changing it.
- Ordinary usage available; session 70%, weekly 88%; reset 2026-10-05 19:51:32 UTC (21:51:32 Paris), resetsAt 1791229892. Check boundaries and hand off near 95%.
- No DD2 launch, protected estates or abandoned project access. Native-derived artwork and owner preview/server remain unchanged/outside Git; native checks await explicit launch permission.

## Round 146: resume the saved dungeon instead of abandoning it

DD1 starting raid in_area/areatile and Unity RaidSceneManager.InRaid restore
current room/hall, direction, inventory, formation, quest, camp and torch.
DD2 GameModeMgr runs its completed-entry callbacks after native scene receivers.
Core now inspects saves without mutation: town, never-left refund, valid resume,
terminal results or invalid preservation. Valid loads restore pre-fight evidence
first, then create native actors with recorded condition and Resume at the saved
spot; no repeated town week, provisions cost or quest rolls. Legacy missing
condition conservatively retains estate evidence, not invented historical wounds.

Terminal results keep recorded success/retreat/deaths and clear the expedition
once; ordinary FinishExpedition also captures/outcomes via the recorded path.
Invalid state or native host failure shows a saved-expedition retry/menu screen;
another embark cannot overwrite it. Entry sets Crawling before handling events,
fixing the immediate-battle phase overwrite. Dd2Run reports busy/setup/transition
failure, retries a ready host road without a second run and invalidates stale
callbacks on end/replacement. A no-callback timeout is a separate recorded gap.

Twelve Core cases cover repeated room/hall/camp/loot reconstruction with exact
campaign/RNG preservation, mid-fight versus prior deaths, invalid saves and
terminal reward/retreat outcomes once. Five linked tests run the actual Dd2Run
against native API shims for busy/missing modes, completed-entry ordering,
ready-road retries, setup/exhausted-transition failures and callback cancellation.
Release + 626 Core + 57 UI tests pass; stopped-game DLL hashes match. Runtime
resume is enabled in code but remains [?] until a permitted native synthetic-estate
check. Exact mid-turn DD2 combat is not restored. No launch, owner-estate access
or private-art changes.

## Status 2026-10-05: round 146 complete

- Round 146 deployed and green; loop continues under owner authorization. Native goal remains paused without resume API; no duplicate or false completion.
- Next: save camp meals immediately (separate documented gap), then inspect fallen-hero quirk preservation and host-entry timeout. Resume checks must use only the test estate after explicit launch permission; test corridor/camp/loot/unresolved battle/terminal result and exact single week/payment.
- Ordinary usage available; session 74%, weekly 89%; reset 2026-10-05 19:51:32 UTC (21:51:32 Paris), resetsAt 1791229892. Check boundaries and hand off near 95%.
- No DD2 launch, protected estates or abandoned project access. Native-derived artwork and owner preview/server remain unchanged/outside Git; native checks await explicit launch permission.

## Round 147: persist successful camping meals immediately

DD1 shared/rules.json meals_table defines none/half/full/feast rations, health and
stress. Unity ScrollMealEvent allows one selected meal and SaveCampaignData stores
camp phase and formation. Driver's successful EatMeal now immediately persists
through the existing actor snapshot boundary; rejected choices do not write.
Four native meal-size cases cover exact food/condition/Ate/respite/light/RNG
across synthetic reconstruction and no repeated food/effects. Release + 630 Core
+ 57 UI tests pass; stopped-game hashes match. Native immediate persistence remains
[?]. No launch, owner-estate access or private-art changes.

New separate gap: SaveFile's delete-then-move publication leaves a crash window
without the main file, despite its atomic-save intent. Inspect replacement and
backup semantics using synthetic temporary paths only.

## Status 2026-10-05: round 147 complete

- Round 147 deployed and green; loop continues under owner authorization. Native goal remains paused without resume API; no duplicate or false completion.
- Next round 148: remove save delete-before-publish gap with tested same-directory atomic replacement and previous-file backup; use only owned synthetic temp files. Then inspect fallen-hero quirks and host-entry timeout.
- Ordinary usage available; session 76%, weekly 89%; reset 2026-10-05 19:51:32 UTC (21:51:32 Paris), resetsAt 1791229892. Check boundaries and hand off near 95%.
- No DD2 launch, protected estates or abandoned project access. Native-derived artwork and owner preview/server remain unchanged/outside Git; native checks await explicit launch permission.

## Round 148: publish saves without deleting the prior destination

The old writer copied .bak, deleted the main save and only then moved its temp.
DD1 complete starting saves and Unity SaveLoadManager/SaveCampaignData are payload
references, not a reason to keep this mod-specific failure window. Consulted
[Microsoft File.Replace](https://learn.microsoft.com/en-us/dotnet/api/system.io.file.replace?view=netframework-4.8)
for same-volume replacement and replacement of an existing backup with the prior
destination. Use a unique same-directory temp, File.Replace for existing saves
and Move for first creation. Clean failed temps; never delete the destination
as a fallback when replacement fails.

Three tests use real Windows filesystem operations on owned temporary synthetic
paths: exact complete current/previous JSON through repeated saves, an exclusively
locked destination preserving both files and cleaning temp before a successful
retry, and a blocked first destination retaining its directory/sentinel. Cleanup
asserts the resolved temp parent and owned prefix before recursive deletion.
Release + 633 Core + 57 UI tests pass; stopped-game DLL hashes match. Native
Mono persistence remains [?]. No launch, owner-save/cache access or art changes.

## Status 2026-10-05: round 148 complete

- Round 148 deployed and green; loop continues under owner authorization. Native goal remains paused without resume API; no duplicate or false completion.
- Next round 149: inspect fallen-hero quirk records and From Beyond, preserve only proven recorded state without changing dead-trinket policy. Then bound host-entry waits while keeping slow native asset transitions safe; native resume tests still await explicit launch permission.
- Ordinary usage available; session 78%, weekly 90%; reset 2026-10-05 19:51:32 UTC (21:51:32 Paris), resetsAt 1791229892. Check boundaries and hand off near 95%.
- No DD2 launch, protected estates or abandoned project access. Native-derived artwork and owner preview/server remain unchanged/outside Git; native checks await explicit launch permission.

## Round 149: keep recorded quirk changes on fallen heroes

Installed base.town_events.events.json dead_recruit requires dead_heroes=3 and
offers number_data=3 after minimum_week=15. It does not define resurrection quirk
rules. Unity DeathRecord only stores memorial metadata, not a full resurrectable
hero. The mod already reuses its full graveyard HeroRecord for From Beyond, but
Homecoming discarded a dead outcome's recorded quirks. Copy known quirks/diseases
onto that record; unknown null stays unchanged and known empty clears it. This
fixes the mod's own recorded-state loss without claiming unproven native semantics.
Existing dead-trinket/progression behavior is unchanged.

Three cases exercise real Homecoming, synthetic save, native-data From Beyond
and recruit, plus copy ownership and null/empty handling. Release + 636 Core +
57 UI tests pass; stopped-game DLL hashes match. Native graveyard/return display
remains [?]. No game launch, owner-estate access or art changes.

## Status 2026-10-05: round 149 complete

- Round 149 deployed and green; loop continues under owner authorization. Native goal remains paused without resume API; no duplicate or false completion.
- Next round 150: bound the no-callback host wait with a main-thread timer, one reported failure and cancelled stale callbacks; preserve an in-progress native transition/save and allow retry when ready. Use a generous threshold because native asset loading can be slow. Then inspect remaining save/recovery and crawl UI gaps.
- Ordinary usage available; session 80%, weekly 90%; reset 2026-10-05 19:51:32 UTC (21:51:32 Paris), resetsAt 1791229892. Check boundaries and hand off near 95%.
- No DD2 launch, protected estates or abandoned project access. Native-derived artwork and owner preview/server remain unchanged/outside Git; native checks await explicit launch permission.

## Round 150: bound inactive host-entry waits

DD1 loading-screen data/Unity SaveSelector.LoadScene are presentation references;
DD2 GameModeMgr dispatches ready callbacks only after completed native entry.
Driver now pumps a main-thread three-minute inactivity timer. Each completed
intermediate scene resets it; road readiness/end clears it. A wait with no progress
reports once and cancels late gameplay callbacks while retaining the save and
native host/transition, so DD2 may finish loading and a ready-road retry can reuse
it. Three minutes is a generous mod UX choice, not a claimed DD1 timing value.

Three real-linked fake-clock cases cover slow-but-successful readiness with no
later failure, timeout during continuing native loading/one notification/late
callback cancellation/ready-host reuse, intermediate progress and end cancellation.
Release + 636 Core + 60 UI tests pass; stopped-game DLL hashes match. Native
slow-load timing/retry remains [?]. No launch, owner-estate access or art changes.

New gap recorded: missing/corrupt main saves do not recover from an existing
backup. Missing main can be misidentified as a new estate after the old writer's
delete window. Fix read-only backup selection separately, then picker error UI.

## Status 2026-10-05: round 150 complete

- Round 150 deployed and green; loop continues under owner authorization. Native goal remains paused without resume API; no duplicate or false completion.
- Next round 151: validated read-only backup recovery for missing/corrupt main saves, without rewriting or swallowing access/lock errors; test only temporary synthetic files. Then show unrecoverable load errors in the picker and inspect loot-scroll truncation.
- Ordinary usage available; session 82%, weekly 90%; reset 2026-10-05 19:51:32 UTC (21:51:32 Paris), resetsAt 1791229892. Check boundaries and hand off near 95%.
- No DD2 launch, protected estates or abandoned project access. Native-derived artwork and owner preview/server remain unchanged/outside Git; native checks await explicit launch permission.

## Round 151: recover valid backups without rewriting saves

DD1 complete campaign saves and Unity ReadSave's header/SaveCampaignData are
format references. SaveFile.Load prefers a valid main file, otherwise reads a
validated backup for absent/malformed/null/missing-estate payloads. Existing
unrecoverable files throw rather than looking like a new estate. Reads propagate
access/lock errors; File.Exists is not used to mask those errors as absence.
Recovery metadata is not serialized and Session logs the backup selection.
Neither file is rewritten by loading; JSON is parsed once for validation/loading.

Ten real-filesystem cases use owned synthetic temporary files: exact campaign,
raid, raw condition and pending loot; missing/invalid main, valid-main preference,
unrecoverable rejection, truly absent files, Windows lock propagation and no
writes/temp files. Release + 646 Core + 60 UI tests pass. Deployment was blocked
by the stopped-game guard: DD2 is now running. No game process was launched or
stopped by this agent; use Deploy=false until it stops. Round 150 is the last
deployed build. Native validation remains [?]; no owner-estate or art access.

New gap: picker summary failures look like new estates, and Session.SavePath is
assigned before load, risking a stale prior Save with a different path after an
exception. Fix feedback and clear stale context in the next round.

## Status 2026-10-05: round 151 complete, deployment pending

- Round 151 built/tested; deployment deferred while DD2 runs. Loop continues with Deploy=false. Native goal remains paused without resume API; no duplicate or false completion.
- Next round 152: show unavailable/recovered estate summaries and retry/refresh; guard click-time failure and clear previous Save/SavePath before reading. Do not inspect owner saves. Deploy accumulated changes only after the game is stopped; round 150 is last deployed.
- Ordinary usage available; session 85%, weekly 91%; reset 2026-10-05 19:51:32 UTC (21:51:32 Paris), resetsAt 1791229892. Check boundaries and hand off near 95%.
- No DD2 launch, protected estates or abandoned project access. Native-derived artwork and owner preview/server remain unchanged/outside Git; native checks await explicit launch permission.

## Round 152: preserve failed estate selection and show its state

Core CampaignSaveSlot clears prior Save/Path before reading, assigning the pair
only after success. This prevents Session keeping the previous estate with a
new destination after a load exception. Picker summaries distinguish absent/new,
valid, recovered and unavailable saves; unreadable rows cannot open as new, refresh
retries and recovered summaries show previous-save feedback. Driver catches
click-time failure and opens preserved-file feedback. Unity SaveSlot and installed
DD1 complete campaign saves are references for selection/payload behavior.

Two Core and one linked picker-summary test use owned temporary synthetic files
for cross-slot failures, new/recovered selections, retry/refresh and no writes or
stale destination. Release + 648 Core + 61 UI tests pass. DD2 stopped again before
deployment; accumulated rounds 151-152 DLLs deployed and hashes match. Native
visual/load checks remain [?]. No launch, owner-estate access or art changes.

Owner steering while the round finished: drag-to-hire still fails and minimap
hover lacks content labels. Both documented and now take priority. The diagnosing
bugs skill was read; build a real red-capable caller/input loop before guessing.

## Status 2026-10-05: round 152 complete

- Round 152 green, committed/deployed; loop continues under owner authorization. Native goal remains paused without resume API; no duplicate or false completion.
- Next round 153: owner-reported stagecoach drag-to-hire, then round 154 minimap Battle/Curio/Quest Item hover. Existing shared Drag tests are too shallow to confirm native caller behavior; add the correct reproduction seam. No game launch or owner-estate inspection.
- Ordinary usage available; session 87%, weekly 91%; reset 2026-10-05 19:51:32 UTC (21:51:32 Paris), resetsAt 1791229892. Check boundaries and hand off near 95%.
- No DD2 launch, protected estates or abandoned project access. Native-derived artwork and owner preview/server remain unchanged/outside Git; native checks await explicit launch permission.

## Round 153: stagecoach drag release without an intermediate drag pass

Installed DD1 stage_coach.layout.darkest hero_recruit_store and Unity
RecruitSlot.OnBeginDrag/HeroRosterPanel.OnDrop define moving a recruit to the
roster once while separating details clicks. The actual shared Drag.Source ->
Begin -> Drop -> Core Hamlet.Recruit reproduction failed twice: a distant left
MouseUp without an intermediate MouseDrag never promoted the pending payload.
Ranked explanations were missing promotion, coordinate mismatch and roster
button interception. Changing promotion alone made that reproduction pass.

Begin now accepts threshold-crossing left release before ordinary buttons,
using the existing screen-space threshold. Four added cases cover actual one-time
recruitment, reserved release, outside cancellation/retry, below-threshold click
and another-button rejection. All 648 Core + 65 UI tests and Release pass;
DD2 was stopped and both deployed DLL hashes match. No temporary instrumentation.
The headless event shim does not reproduce native GUI.Button, viewport matrices
or full Hamlet rendering. This is a confirmed shared-input gap, not proof of the
owner's exact native event sequence; keep native verification [?]. No launch,
owner-estate access or artwork changes.

Minimap investigation for next round: CrawlUi.DrawMap draws hall markers and room
icons but never calls Gui.Tip. Installed miscellaneous.string_table.xml lines
1600-1612 provide Battle/Curio/guarded-room/Treasure/Trap/Obstacle/Secret Door/
Boss/Quest Location labels. Unity RaidMapRoomSlot only offers movement feedback;
RaidMapHallSectorSlot preserves hidden/scouted/visited/completed knowledge. Add
actual content labels without exposing unknown or already-resolved content.
Room/HallTile.IsQuestGoal can identify quest locations; don't invent quest-item
details from absent payload data. Gui.Tip queues text during Repaint and DrawTip
renders after EndGroup, so hit testing must remain in the clipped map coordinates.

## Status 2026-10-05: round 153 complete, quota handoff

- Round 153 green, deployed with matching DLL hashes; commit/push this handoff. Native goal remains paused without resume API; no duplicate or false completion.
- Quota pause at session 94%, weekly 92%, ordinary usage still available. Stop approximately 95% with a clean committed handoff. Resume only after reported session reset 2026-10-05 19:51:32 UTC (21:51:32 Paris), resetsAt 1791229892, through the existing hourly heartbeat. Do not create another automation. Heartbeats before this reset must remain quiet.
- Next round 154: owner-reported missing minimap Battle/Curio/Quest Location hover labels. Record DD1 label/fog differences in PARITY before code, use a meaningful real label-selection seam, test/build/deploy only while stopped and commit/push. The stagecoach's exact native symptom remains unverified; never describe the whole native bug as proven fixed from headless input alone.
- DD2 stopped at deployment; no game launched or killed. No protected estates, abandoned project or native-derived artwork accessed. Owner preview/server remains unchanged. No running agent, test or build; no save restores pending.
- Later candidates remain loot-scroll truncation and permitted synthetic-estate/native validation for saved-raid recovery, lighting/startup and stagecoach. Owner launch authorization is still absent.

## Round 154: restore minimap content hover labels

Owner directly requested finishing the pending task while ordinary usage was
still available at 95%; completed this single bounded round and then stopped.
Installed miscellaneous.string_table.xml str_map_*_tooltip supplies Battle,
Curio, Quest Location, guarded curios/treasure, Treasure, Trap, Obstacle, Boss
and Secret Door. Unity RaidMapHallSectorSlot gates content by Knowledge and hides
hunger; RaidMapRoomSlot provides movement feedback. DD2 KingdomMapCellBiomeInfoBhv
UpdateKillContract uses its own native tooltip/localization path; the mod retains
its DD1 map and Gui.Tip rather than replacing native DD2 map components.

Moved the existing map drawing/panning into CrawlMapUi without changing its
initial behavior, enabling a linked-source pointer replay of the real draw loop.
Battle, curio and quest-location hover cases failed with no queued tip. Ranked
missing-request, group-coordinate and fog hypotheses; adding requests alone
made the original cases pass. Labels follow the visible marker/icon, preserving
unknown/resolved/consumed states. Remaining guarded curios change from battle
labels to their curio/treasure label; quest objects use DD1 Quest Location wording.
Known secret doors retain their label after resolving another hall object.
Viewport, disabled-page and pan guards prevent tips outside the active map.

Seventeen drawing/input cases exercise installed text, fog, completed objects,
known secrets, guarded objectives, clipped/disabled pointers, Repaint, plot and
generated halls, pan/recenter and unchanged travel callbacks. The shim models
group-local pointer coordinates and tooltip requests, not Unity rasterization,
native GUI.Button or viewport matrices. All 648 Core + 82 UI tests and Release
pass; DD2 was stopped and both installed DLL hashes match. No temporary probes,
new dependencies, save access, game launch or native-derived artwork changes.
Native tooltip visibility/position remains [?], including scaled windows.

## Status 2026-10-05: round 154 complete, quota handoff

- Round 154 built, all 730 tests green and deployed with matching DLL hashes; commit/push this handoff. Both owner-reported input/display gaps now have tested fixes, with native confirmation still pending. Native goal remains paused without resume API; no duplicate or false completion.
- Pause now at session 99%, weekly 93%, ordinary usage available at the final boundary. Owner's finish-task instruction allowed this bounded round past the usual cutoff; do not start another before session reset 2026-10-05 19:51:32 UTC (21:51:32 Paris), resetsAt 1791229892. Existing hourly heartbeat resumes the loop after reset; earlier unchanged-quota heartbeats stay quiet. Do not create another automation.
- Next round 155 candidate: inspect and document battle/curio loot-scroll truncation against DD1 and the Unity port, then implement one proven gap with tests/build and stopped-game deployment. Native stagecoach drag and map hover checks remain [?]; do not claim native symptoms fully confirmed by headless input tests.
- DD2 stopped at deployment; no launch or process termination. No protected estates, abandoned project or private art accessed; owner preview/server unchanged. No running tests/builds/agents or save restores pending.
- Native checks once explicitly permitted: drag recruits to occupied/empty roster areas, cancel/retry/open details/check full capacity; hover scouted/visited battle, curio, quest location and guarded room/hall markers; pan and scale the map, ensure hidden/completed content stays concealed and tooltips render above the panel.

## Round 155: make every retained loot card reachable

Battle/camp scrolls capped Taken+LeftBehind at 10 cards; curio results capped Loot
at 5. Taken entries or partial pickups could hide a waiting quest object while
Continue remained blocked. Installed overlay.loot.darkest max_items 9 and dynamic
backdrop/overflow assets define presentation; Unity ScrollEventLoot distributes
all generated loot and Close refuses remaining quest items. Shared LootGridUi
first preserved the capped drawing as a linked test seam; both original battle
and curio scenarios failed to reach the hidden quest item. Ranked display limit,
waiting-index and capacity causes; paging removed the observed display failure.

DD1 shared/menu left/right arrows sit outside existing card bounds. Page owner
changes reset to the first page; shrinking reports clamp the current page. Clicks
retain actual waiting-list indexes, consumed arrow events cannot also pick a
card, and existing Core capacity/pickup/dismissal rules remain. Pagination is this
mod's UX choice; ordinary small-card positions and 10/5 cards per page are kept.
Seven actual linked UI/Core cases cover hidden quest pickup, partial pickup/list
growth/remainder retry, curio indexes and saved aliases, clamp/reset, disabled
input, exact report preservation and unchanged RNG. After correcting two test
compile issues during development, all 648 Core + 89 UI tests and Release pass;
DD2 was stopped and both installed DLL hashes match. Native appearance remains [?].

New separate gap: gather goals bypass capacity with Pack.Add and create no
LeftBehind. Ordinary curios already TryTake; fix native gather overflow next.

Owner asked whether https://github.com/morluto/rea would help. Reviewed its README,
docs/managed-code-analysis.md and docs/windows-ghidra-p0.md. Useful candidates are
managed IL/build comparison for DD2 and targeted native DD1 questions unavailable
in data or the Unity port. Windows Ghidra is experimental, read-only and limited
to native x64 executables; managed analysis is a separate provider. Read-only PE
header inspection of installed _windows/win64/Darkest.exe found machine 0x8664,
PE32+ magic 0x20b and zero CLI RVA, matching that target format but not proving
provider compatibility. No REA installation, configuration or game launch occurred.
Sources: https://github.com/morluto/rea/blob/main/docs/managed-code-analysis.md and
https://github.com/morluto/rea/blob/main/docs/windows-ghidra-p0.md.

## Status 2026-10-06: round 155 complete

- Quota reset cleared the previous pause; round 155 is green/deployed and the authorized loop continues. Native goal remains paused without resume API; no duplicate or false completion.
- Next round 156: gather quest full-pack capacity/retained quest loot, using installed native goals and synthetic states. Then inspect any proven bypass of pending quest-loot dismissal rather than guessing.
- Ordinary usage available; session 9%, weekly 95%, session resetsAt 1791324444. Check usage at round boundaries, stop around 95% session or if ordinary usage becomes unavailable, and leave a clean committed handoff. Existing hourly heartbeat remains; no duplicate automation.
- All 737 tests and Release pass; stopped-game DLL hashes match. Native loot paging/hover/stagecoach/save recovery still need permitted game checks; no launch authorization yet.
- No protected estates, abandoned project or native-derived artwork accessed. No running tests/builds/agents or save restores pending; preview/server unchanged. REA was evaluated only, not installed.

## Round 156: gather quest objects obey pack capacity

DD1 quest.types.json gather_holy_relic/reliquary yields three holy_relic objects;
base.quest_item.inventory.items.darkest assigns one per stack to native gather
items. Unity ScrollEventLoot.LoadCurioLoot routes quest objects to limited loot
inventory and TakeAll respects pack space; Close refuses a waiting quest object.
The mod used unconditional Pack.Add and its special goal return skipped overflow.
Five actual native-zone/room/hall scenarios failed with 17 occupied slots in a
16-slot pack. Ranked insertion bypass, bad stack limit and bad slot count;
capacity-aware TryTake alone removed that overflow.

Gather reports retain a waiting drop shared with Loot/LeftBehind when full or
the catalog is unavailable. The caller receives the same waiting entries in its
overflow list. One-time investigation, objective progress and curio consumption
stay recorded; report dismissal already requires taking quest loot. Ten cases
cover all four native regions, room/hall, full/free/missing-catalog, reload aliases,
pickup rejection then freeing/taking, no repeated goal or item, exact capacity/RNG.
The first broad Core run exposed a walkthrough helper that silently depended on
overfilling; it now frees a nonquest item and collects required loot, asserting
capacity before continuing. The second Core run passes all 658 cases; 89 UI and
Release also pass. DD2 stopped and deployed DLL hashes match. Native scroll
interaction remains [?]; no game launch or owner-save access.

New separate gap: Driver.Leave unconditionally ends the expedition and never
checks retained required loot. Follow-up inspection corrects the initial
assumption about Retreat: it currently has no Quest.CanRetreat guard either;
only the visible UI hides forbidden abandonment. Guard explicit returns in Core
next; forced wipe must stay separate.

## Status 2026-10-06: round 156 complete

- Round 156 green/deployed; loop continues under the reset heartbeat authorization. Native goal remains paused without resume API; no duplicate or false completion.
- Next round 157: guarded explicit return respecting required loot and refused retreat, with actual Core transition tests and Driver integration. Do not alter forced wipe results. Then inspect whether another curio can overwrite pending required loot.
- Ordinary usage available; session 13%, weekly 95%; session reset 2026-10-06 22:07:24 UTC (2026-10-07 00:07:24 Paris), resetsAt 1791324444. Check boundaries; stop around 95% session or ordinary unavailability with clean commit/push and existing heartbeat handoff.
- All 747 tests and Release pass; stopped-game DLL hashes match. Native UI/persistence checks remain [?]; launch authorization is still absent.
- No protected estates, abandoned project or art access. No running tests/builds/agents or save restores pending; owner preview/server unchanged. REA evaluated, not installed.

## Round 157: explicit returns respect required loot and quest retreat flags

Driver.Leave set Ended without guarding pending quest loot; Core.Retreat also
lacked a CanRetreat check, correcting the earlier assumption in round 156.
Installed DD4 can_retreat=false and Unity ScrollEventLoot.Close's required-loot
gate are the references. Three actual Core cases reproduced ending with required
curio/battle loot and abandoning the native final DD quest. Ranked transition
guards, imported quest flags and loot identity; the latter two were correct.

Core CanLeaveExpedition/TryLeave now owns explicit return eligibility; Retreat
shares the required-loot gate and independently refuses forbidden abandonment.
Completed returns do not set Retreated, while permitted abandonment still does.
Driver uses TryLeave; the quest return button/art dims with a collection hint
when blocked. Forced wipe stays on its separate results path. Nine cases cover
both reports, completed/incomplete quests, synthetic save reload, pickup then
return, repeated calls, native forbidden retreat, optional loot and legacy quest
defaults, including byte-exact rejected states and unchanged RNG. All 667 Core
+ 89 UI tests and Release pass. DD2 was stopped; both installed DLL hashes match.
No temporary probes, game launch or owner-save access. Native button dimming and
full Driver presentation still need a permitted game check.

## Status 2026-10-06: round 157 complete

- Round 157 green/deployed; commit/push and continue the authorized loop. Native goal remains paused without resume API; no duplicate or false completion.
- Next round 158: inspect whether a second curio can overwrite retained required loot. Reproduce the actual transition before choosing a fix, using installed DD1 and the Unity port.
- Ordinary usage available; session 17%, weekly 96%; session reset 2026-10-06 22:07:24 UTC (2026-10-07 00:07:24 Paris), resetsAt 1791324444. Check boundaries; stop around 95% session or ordinary unavailability with clean commit/push and existing heartbeat handoff.
- All 756 tests and Release pass; stopped-game DLL hashes match. Native input/UI/persistence checks remain [?]; launch authorization is absent.
- No protected estates, abandoned project or art access. No running tests/builds/agents or save restores pending; owner preview/server unchanged. REA evaluated, not installed.

## Round 158: another investigation cannot replace required loot

Installed gather_holy_relic and native one-item stacks supply a two-curio fixture.
Unity RaidSceneManager awaits LootEvent; ScrollEventLoot.Close refuses quest items.
Actual gather/travel/second room or hall investigation reproduced replacement of
the first uncollected holy relic. Ranked missing interaction guard, item identity
and report-link restoration; both failures preceded reload with native item keys.
One interaction-entry guard for required loot in either report made both pass.

Six cases cover room/hall paths, synthetic reload, byte-exact refused state,
freeing/collecting/dismissing then investigating the next objective, and native
keyed chest interaction with either pending report. The rejected call consumes no
key, RNG or objective; optional-loot behavior is deliberately unchanged here.
All 673 Core + 89 UI tests and Release pass. DD2 stopped and both deployed DLL
hashes match. Native input/presentation remains [?]; no launch or save access.
Movement behind the modal still needs a separate round rather than widening this
required-item-loss fix.

The owner now explicitly asks to use REA and produce a list of difficult DD1-to-DD2
systems so they can choose priorities. Finish this round, then perform a bounded
read-only REA trial and create that list before selecting further implementation
work. REA 4.0.1 installed locally under C:/Users/Piral/rea-workbench with lifecycle
scripts disabled; no global agent/MCP configuration changed. Published package
omits the docs paths, so use the official repository docs for setup details.

## Status 2026-10-06: round 158 complete, REA prioritization next

- Round 158 green/deployed; commit/push this fix. Next authorized task is the owner's REA feasibility trial and difficult-system listing, then let the owner guide priorities. Do not automatically choose another implementation gap ahead of that list.
- Ordinary usage available; session 22%, weekly 97%; session resetsAt 1791324444. Check usage; stop around 95% session or ordinary unavailability with clean handoff and the existing heartbeat. Native goal remains paused without resume API; no duplicate or false completion.
- All 762 tests and Release pass; stopped-game DLL hashes match. Native UI/input/persistence checks remain [?], and launch authorization is absent.
- No protected estates, abandoned project or art access. No running tests/builds/agents or save restores pending; preview/server unchanged. Local REA CLI is installed for the requested trial, not yet a verified DD1 analysis.

## REA reconnaissance: owner priority list, 2026-10-06

Owner requests an evidence-backed list before choosing further implementation
priorities. Followed REA's supplied investigation workflow; its explicit parallel
scope instruction authorized one read-only campaign-reference worker. No code was
changed after round 158. tools/rea_investigation_backlog.md has ten stable R1-R10
choices, implementation estimates, source locations, current behavior and unknowns.
R3 dismissal is a narrow native pilot; R1/R2/R4 share campaign return questions.
R5 Never Again and R6 resurrection share persistent hero state. R7-R10 cover DD2
stress adaptation, remaining trinket descriptions and runtime performance/input.

Direct REA 4.0.1 managed CLI inspection succeeded on the installed IronCrown.dll.
Unity Mono/AnyCPU/CIL observed; 4,721 types/33,158 methods with complete metadata
coverage and no issues. Selected stress/run/effect-description CIL bodies present.
Installed/reference SHA-256 matches dd97e30f2f37e2a287633ab322b8fd18bcfe9e240903713a5c2ae00c894692e6;
MVID c8627dd8-3822-4311-9403-8920d40a2892. Source hash unchanged. Static evidence
does not observe runtime hook timing, application behavior or performance.
Private full evidence, selected summary and helper live under
C:/Users/Piral/rea-workbench/evidence, outside Git; no game code is copied here.

DD1 native inspect with provider ghidra returned provider_unavailable/not_configured.
Doctor confirms Ghidra absent; no Java command resolves. The live official raw
Windows guide says npm 4.0.1 lacks its Windows native bundle, which also is absent
from the installed package. Main-branch documentation and release support differ.
Do not claim original DD1 rules recovered or that MCP registration supplies this
bundle. Native analysis needs a compatible verified bundle/Ghidra12.1.4/fullx64JDK21
or a separately validated supported host. Sources are linked in the backlog.

The broad quest/curio quirk PARITY entry was corrected: ordinary DD1 return quirk
rolls are missing in Homecoming, while curio/town gains and caps already exist.
Other native blockers remain blocked pending evidence; no native conclusions
were inferred from rule names or Unity port differences alone.

## Status 2026-10-06: REA list ready, awaiting owner's priorities

- Round 158 remains the last implemented round, committed/pushed/deployed at be92fce, with 673 Core + 89 UI tests and Release passing. Research-only changes need no repeat build. Commit/push this listing and handoff.
- tools/rea_investigation_backlog.md lists R1-R10. Await the owner's ranking before selecting a further implementation round; next number 159. Existing heartbeat must remain quiet until a selection arrives, then retain quota-aware resumption for the selected work.
- REA managed analysis works through direct CLI. DD1 native analysis is unavailable with the current package/provider prerequisites; the list records that limitation. No global agent/MCP configuration changes, Ghidra/Java installs or open native analysis session.
- Usage at research boundary: ordinary available, session 31%, weekly 98%, session resetsAt 1791324444. Check limits when work resumes; ordinary unavailability can stop work before session95. Native goal remains paused without resume API; no duplicate or false completion.
- No game launch, owner-save/protected estate access, abandoned project access or native art changes. Native UI/input/performance checks remain [?]. No running tests/builds/agents or save restores pending; owner preview/server unchanged.

## REA usage documentation, 2026-10-06

Owner asked to add and commit how to use the REA skill before selecting priorities.
tools/rea_usage.md provides the project recipe: loading the upstream skill from the
private installed package, pinned CLI installation, exact target routes, DD2
identity/member inspection and evidence reuse, managed build comparison, and
conditional DD1 native queries after provider prerequisites are verified.
CLAUDE.md, README.md and the investigation backlog point to this guide.
The upstream skill remains package-owned; no REA package, game/CIL output or
agent registration is added to the repo. Writing-for-agents instructions applied
to the agent pointer and guide structure.

Verified installed REA version4.0.1 and search/decompile/compare command help.
Six PowerShell examples parse, local guide links resolve and diff checks pass.
No production code changes; the previous 762 passing tests/build/deployment remain
the last runtime verification. No game launch or new binary analysis required.

## Status 2026-10-06: REA usage documented, priorities pending

- Commit/push the usage guide and its discovery links. Round 158 at be92fce remains the last implemented round; next implementation number159 awaits the owner's R1-R10 ranking.
- Direct managed REA CLI works. DD1 native prerequisites and published/main release distinction remain documented in the guide/backlog; no global MCP/agent configuration changes.
- Usage at the start of this documentation task: ordinary available, session38%, weekly99%, session resetsAt1791324444. Check usage before the next task; keep quota-aware heartbeat quiet while priorities are pending.
- Native goal remains paused without resume API. No duplicate goal, tests/builds/agents or save restores pending. No game/protected-estate/abandoned-project/art access; existing preview/server unchanged.

## DD1 code map, 2026-10-06

The owner asked for a full reverse-engineered DD1 map first, for easy future mapping. Built with Ghidra 12.1.4
(official zip, checksum verified, at D:\re-tools) and Microsoft OpenJDK 21 (winget), driven headless without REA.
The repo has only scripts and docs, in tools/dd1re (README.md explains querying and rebuilding). The map is private
under D:\dd1-decomp: Ghidra project, raw facts, one decompiled C file per function and map files. Never commit or
copy it. C: has under 7 GB free, so Ghidra and the map live on D: (an SSD with 100 GB free).

Darkest.exe win64 (sha256 4d78fbfa..., PE timestamp 2026-09-21) ships no PDB, but has 3,402 RTTI type names,
126 DD1 source paths in assert strings and 180 of the 188 rules.json keys as literals. Auto-analysis took 9 minutes
and found 56,343 functions. Ghidra's RecoverClassesFromRTTIScript stops with "More than one Base Class Array"
(MSVC folds identical lambda RTTI), so Dd1NameVirtuals names virtual functions instead: 2,316 under their classes.
Dd1NameFromStrings applied 169 names from DD1's own log strings. The first full decompile produced 55,965 C files.

Key technique: a loader compares each rules key by name and stores it in a global (rules.json loader 1404ea3d0).
build_map.py turns that into key -> global -> users. That pattern extracted 170 rules keys. Gotchas: analyzeHeadless.bat
breaks on "(x86)" paths, so the exe is copied first. PowerShell 5.1 '*>' writes UTF-16 logs, so run_all uses
Start-Process. The second run (8 GB heap, 12 decompilers, with the owner's game running) used up RAM and Claude Code
stopped it. Defaults are now 4 GB and 4 threads, and the decompile resumes with -From decomp -Resume.

First answers, written into PARITY and the backlog:
- R1: Roster::System::OnRaidFinish ages all quirks by 1 per finished quest, then auto-locks negatives
  (age >= 2, roll < 0.25, cap 3). Mod has no per-quirk age.
- R3: dismissal stress is dead data (key never referenced). Mod already matches; PARITY [x].
- New gap: DD1's surprise is one weighted pick with a 25% "none" floor, hero surprise buffs and a one-hero exemption.
  The mod rolls sequentially.
- meals_table / rations_per / healing are never referenced by the exe; round 147's meal numbers are unconfirmed.

## Status 2026-10-06: DD1 code map built, refresh pending the owner's OK

- Last implemented round is still 158 (be92fce); next implementation number 159. No game launch; DD2 code unchanged,
  so no build or test was needed. The map scripts compile against Ghidra (javac), parse (PowerShell) and byte-compile (Python).
- Map state: the Ghidra project and raw facts include the virtual and string names. D:\dd1-decomp\map is from the first
  run. decomp/ is partial (35,134 of 55,971 files) because the refresh was stopped for low memory. Finish it, when the
  owner allows and isn't gaming, with:
  powershell -ExecutionPolicy Bypass -File tools\dd1re\run_all.ps1 -From decomp -Resume
- D:\dd1-decomp\ghidra_after_autoanalysis is a backup of the project from before any naming (311 MB); delete it when it's no longer needed.
- Next candidates once the map is complete: R1 implementation (per-quirk age); the surprise roll shape; DD1's real meal
  numbers; R2/R4 from the same OnRaidFinish function. Owner priorities still decide.

## DD1 reverse-engineering tool review, 2026-10-08

Owner asked to inspect the new tools/dd1re pipeline. Reviewed the PowerShell runner, four Ghidra scripts, map builder,
query CLI and documentation. The installed win64 Darkest.exe still matches the private manifest's SHA-256. A real
key query reaches the rules loader, and Darkest::RaidFinish resolves to an existing decompiled file. No game launch,
binary import, bulk decompile or private map rewrite was needed.

Confirmed a rebuild blocker with a synthetic loader/global/reader fixture: build_map.py overwrote its string-reader
dictionary with display text while writing rule_globals.md, then failed at users.items() with AttributeError. Renamed
that display variable. tools/dd1re/test_build_map.py reproduced the failure before the fix, then passed, checking the
source-file index and the actual key/global/function query commands against a temporary synthetic map.

The current private outputs are mixed: map/ indexes 55,971 C files, but decomp/ contains 35,134 and 22,540 indexed
paths are missing after renames and interruption. globals_index.json and rule_globals.md are absent, so the real
global query fails. No attempt to complete the refresh was made; the preceding handoff requires the owner's OK and
no concurrent gaming. README now records these limitations. The exporter keeps failed-decompile C stubs on resume;
file counts do not prove successful coverage. An unmatched key in exported string references likewise does not
prove a game rule is unused; the guide and generated-map wording now require following the loader and indirect use.

Verification: one synthetic CLI integration test passes, including three query subchecks; PowerShell runner parses.
The initial query-test fixture lacked the exported signature field; corrected that fixture without changing dd1q.py.
Only tooling and documentation changed, so no gameplay build, deployment or repeated .NET suite was needed.

## Status 2026-10-08: DD1 tool reviewed, builder fixed; priorities and refresh pending

- Last gameplay round remains 158 at be92fce; next number 159 awaits the owner's R1-R10 selection. No gameplay gap selected.
- Commit/push the map-builder fix, synthetic regression and review notes. Native map refresh remains pending the owner's OK under the previous handoff; no Ghidra/decompile/build/test processes or save restores left running.
- The private map and game binaries remain untouched. Query limitations are documented in tools/dd1re/README.md; use REA for managed DD2 and this direct Ghidra pipeline for native DD1.
- Usage at task start: ordinary available, five-hour 1%, weekly 0%, session reset1791500512. Goal remains paused without a resume API; no duplicate goal. Heartbeat stays quiet while owner priorities are pending.
- No game launch, protected-estate access, abandoned-project access or artwork changes. Last gameplay verification remains 673 Core +89 UI tests and Release passing; native UI checks remain unverified.

## Full quest lifecycle reverse-engineering map, 2026-10-08

The owner now requests the complete base-game DD1 quest experience mapped and then implemented inside DD2.
This supersedes waiting for R1-R10 choices and authorizes finishing the interrupted private map. The existing
quota-aware heartbeat was updated to resume this work, preserving the no-launch and save-protection rules.
REA's broader investigation instruction authorized three read-only phase workers: preparation, exploration,
and return. No worker edited the repo or accessed saves.

The private Ghidra refresh completed with a 1 GB heap and one worker while no game ran. It retained 35,134 files,
decompiled 20,834 pending functions and reported 3 failures in that batch. The rebuilt manifest indexes 56,343
functions and 55,971 exported C files, 2,597 named functions, and 141 keys traced into globals. Four C files have
failed-decompile markers; this is not a claim of complete usable-body coverage. Global lookup now reaches the
quirk rule initializer, loader and raid-finish consumer. All native evidence stays under D:/dd1-decomp.

tools/quest_parity_map.md maps Q01-Q23 across board/party/provisions/cancel, start/restore, room and corridor
events, combat, loot, hunger, camping, goal completion, abandonment/wipe, results, town refresh and persistence.
Each row connects native addresses to current Core/bridge methods, tests, mismatches and evidence limits.
Important recovered differences:
- Native provisioning prepopulates party supplies, cancellation refunds and clears both inventories, and the
  recovered confirmation helper checks food rather than adding the mod's torch warning.
- Hallway stress makes one chance and picks one hero; forward stress applies only on empty tiles. Hunger uses
  an explicit eat/starve choice and saved room buffer. Camp light restores before meals and skills.
- Explore goals truncate the required room count; gather goals count currently held quest items. Kill and
  tutorial goals also have predicates that differ from the current simplified progress checks.
- Return separates finished from successful quests; zone XP can be suppressed by plot metadata; resolve XP
  bonuses round up and stored XP caps at the final threshold. Town event/board generation follows return facts.
- Native ordinary return quirks/disease formulas and candidate filters are recovered, but failed-return town
  eligibility, effective disease resistance and weighted pools still need work before implementation.

The map distinguishes static facts from observed play and includes success, continue, abandon, retreat,
full-pack gather, camp interruption, failed-host rollback, wipe and restart acceptance cases. Full 1:1 parity
is the target, not a completed claim. Existing native DD2 combat and DD2 primary regions remain the design.

## Round 159: native DD1 encounter surprise selection

Native 1405fe3d0 uses one weighted pick in order none, party, monsters. None has a minimum weight of 0.25,
not a probability applied before two sequential rolls. Party and monster weights include knowledge, light
and living heroes' active surprise buffs, capped at 0.65. A lone hero loses the party base before modifiers;
darkness can still surprise that hero. The forced branch returns party-only before modifiers.
Native content dispatch 140766c20 distinguishes ordinary ac_battle from forced ambush contents. Roaming
return fights are ordinary battles, even though the mod labels their event Ambush.

Crawl now uses that weighted draw, includes active pending DD1 surprise buffs and applies lone-base ordering.
Driver preserves roaming flags instead of forcing hero surprise. Camp ambush construction remains separate.
Tests reproduced five surprise mismatches and the forced roaming mismatch before the fix. Seven new cases
cover actual encounters at 0.65/0.65 (5/31 none, 13/31 each side), buffs/caps, lone hero, roaming fights and
saved pending encounters without rerolling. All 680 Core + 89 UI tests and Release pass. DD2 was stopped;
both deployed assembly hashes match. No game launch, protected save, abandoned project or artwork access.
Native first-turn/announcement presentation and plot/formation override branches remain [?].

## Status 2026-10-08: round 159 and full quest map ready

- Commit/push round 159 and the Q01-Q23 quest map. Next round is 160: explore threshold truncation, then held-item gather completion. Continue the owner's authorized quest mapping/implementation loop; no R1-R10 wait remains.
- All 769 tests and Release pass; stopped-game deployment hashes match. Native UI/gameplay still awaits explicit launch permission. No outstanding builds, workers, native-map processes or save restores; preview unchanged.
- Private map rebuild completed; four known failed bodies remain documented. Native/CIL evidence and derived game assets stay outside Git. Some preparation/failed-return gates remain unresolved rather than guessed.
- Usage at this boundary: ordinary available, session 66%, weekly 10%, reset1791500512. Check each round; stop around95% with a clean committed handoff. Native goal remains paused without a resume API; no duplicate or false completion.

## Round 160: native explore objective threshold

Native ExploreRoom goal start 14057a740 counts ordinary rooms excluding secret-door rooms, uses an explicit
nonzero amount when present, otherwise truncates room count times percentage. Installed quest.types.json's
explore_all_rooms uses amount 0 and percentage 0.9. Unity QuestVisitedData compares a ratio instead, effectively
rounding up, as the mod previously did. The native executable is authoritative for this mismatch.

Crawl.CheckQuest now follows that count predicate, including the legacy 90% fallback. Eight new cases cover
3/9/10/11/20-room boundaries, secret rooms, amount precedence, and actual travel/arrival followed by saved
continuation and a single return. Five cases failed against the old code. The first full suite exposed an older
two-room secret fixture that expected both rooms; updated it to three ordinary rooms so it still verifies
secret exclusion without asserting the wrong rounding. All 688 Core + 89 UI tests and Release pass. DD2 was
stopped and both deployed hashes match. Native completion presentation remains [?]; no game/save access.

## Status 2026-10-08: round 160 complete, gather predicate next

- Round 159/map committed and pushed at f5cd20e. Commit/push round 160 now. Next number 161: Q16 gather completion must count held quest items and reevaluate after pickup, retaining the existing investigation counter and overflow guards.
- All 777 tests and Release pass; stopped-game hashes match. No builds, tests, native-map processes, workers or restores pending. Native gameplay/UI remains unverified; no launch authorization.
- Ordinary usage available, session 70%, weekly 11%, reset1791500512. Continue until approximately95%, leaving a clean committed handoff. Goal stays paused without a resume API; updated heartbeat resumes this quest-parity priority.

## Round 161: gather attempt reverted after two verification failures

Native Gather predicate 14057a930 and Unity QuestGatherData both require currently held matching items.
Nine full-pack/held-item cases reproduced the mod's premature completion; the proposed fix also refreshed
the report and HUD after pickup. First full verification failed on mapless non-gather exit fixtures because
pickup reevaluation was too broad, and on older CurioActor gather fixtures. Narrowing to gather removed the
exit errors, but the second run still failed three CurioActor cases: they set two prior investigations without
the two corresponding carried items. SavedCurio had the same incomplete setup and was corrected in the attempt.

Following the loop's two-failure rule, all implementation and test edits for this attempt were reverted to
round 160. No deployment occurred; last green deployed binaries are unchanged. The retry patch is private at
C:/Users/Piral/rea-workbench/evidence/quest-parity-round161.patch. The Q16 blocker is known fixture setup,
not missing native evidence. Correct BOTH fixture families before reattempting in a later round.

## Status 2026-10-08: round 161 reverted, round 162 quest HUD next

- Round 160 committed/pushed at15c06f8; round161 left no gameplay change. Commit/push this rollback handoff, then implement the Q15 HUD count target in round162. Reopen Q16 after both fixture setups are corrected; use the private patch as a reference, not as an unchecked replay.
- Last green baseline remains688 Core+89 UI tests, Release and stopped-game hashes. No game launch/save access or builds/workers/restores pending. No failed tests or uncommitted implementation will be committed.
- Owner reiterated the95% session cutoff and resumption after reset; existing hourly heartbeat already provides it. No duplicate automation. Check usage at the next boundary.

## Round 162: explore HUD shares the native count target

CrawlUi still displayed an upward-rounded 90% quota after round 160 fixed completion. Extracted the existing
native predicate target into Crawl.ExploreRoomTarget and used it in both Core completion and the HUD. This
also retains explicit amount and excludes secrets in the displayed quota. Existing ExploreGoalTests now
assert the shared target, and their DD2 fixture uses the actual dd2_city ID. All 688 Core + 89 UI tests and
Release pass; stopped-game deployed hashes match. Native layout/display remains [?]. No launch or save access.

## Status 2026-10-08: round 162 complete, gather retry prerequisites known

- Round 161 rollback recorded at b5ca544; commit/push round 162. Next round 163 reopens Q16 after correcting both SavedCurio and CurioActor fixtures to include held items from their previous investigations. Narrow pickup reevaluation to gather goals. No guess about native behavior is needed.
- All 777 tests and Release pass, stopped-game hashes match; no tests/builds/native-map jobs/workers/restores remain. Game-facing checks stay [?]. No new assets or private game evidence in Git.
- Ordinary usage available, session 75%, weekly 12%, reset1791500512. Continue with the existing heartbeat and stop around95% session with clean commits/pushes; owner explicitly reiterated this cutoff.

## Round 163: native gather completion waits for carried items

Reopened Q16 after round 162, correcting BOTH known fixture prerequisites before implementation: prior gather
investigations in SavedCurio and CurioActor now include the corresponding held quest items. Nine regressions
again failed against the old predicate while the corrected fixture families passed. Native 14057a930 and
Unity QuestGatherData both count matching items in the current inventory, agreeing with installed quest.types.

Crawl.QuestCurioProgress now provides held gather count to completion, result text and HUD; activation retains
its consumption counter. Interactions still resolve once even when their item overflows. Only gather pickup
reevaluates completion, updates its report and announces the first transition; ordinary pickup is unchanged.
Full-pack last-objective cases across all four native item families and room/hall placement reload while
incomplete, collect after freeing a slot, dismiss and return. Four DD2 region cases distinguish unrelated items,
investigation count and held count. An added headless quest-flow case reloads the final waiting item, collects,
returns to the estate, applies carried gold plus success rewards and saves the estate. This is Core integration,
not a claim of observed native DD2 play or complete return-phase parity.

All 693 Core + 89 UI tests and Release pass. DD2 stopped; both deployed hashes match. Q16 is no longer blocked.
Native UI announcement/banner/modal checks remain [?]. No game launch, protected save, abandoned project or art access.

## Status 2026-10-08: round 163 complete, camp torch timing next

- Round 162 committed/pushed at 64274cf; commit/push round 163. Next round 164: Q14 camp light restores before meals/skills, rather than on BreakCamp erasing a skill's reduce_torch effect. Read native phase ordering and installed skill before changing it.
- All 782 tests and Release pass; stopped-game DLL hashes match. No builds/tests/workers/native-map processes/restores pending. Native UI/gameplay checks remain [?]; DD2 launch still unauthorized.
- Ordinary usage available, session 77%, weekly 12%, reset1791500512. Stop around 95% with clean commits and resume after reset via the updated existing heartbeat. The broad quest implementation remains incomplete; do not mark its goal achieved.

## Round 164: camp intro restores light before meals and skills

Native camp controller 1406fbb90 calls torch modifier 1406091a0 with camp_restore_torch in intro phase2,
then advances to provisions phase3 and saves. The modifier adds its amount with light bounds, rather than
assigning an absolute target. Unity RaidSceneManager camp intro also increases torch before provisions.
Installed default.camping_skills.json gives dark_ritual a guaranteed reduce_torch100 effect.

Moved the additive restore to accepted MakeCamp and removed the late BreakCamp restore, which erased a
camp skill's reduction. Existing saved camp light is preserved on Resume rather than reapplying intro.
Six cases cover entry from0/30/100, an additive25 fixture, light events, single firewood use, rejected repeat,
saved pre-meal state, and actual dark ritual across saved rest with forced/no ambush. Five failed before the fix.
All 699 Core + 89 UI tests and Release pass; DD2 stopped and both deployed hashes match. Native camp light/
transition remains [?]. Food modifiers/refusal and exact native ambush-return restoration remain separate.

## Status 2026-10-08: round 164 complete, hallway stress next

- Round 163 committed/pushed at46e1fd7; commit/push round164. Next round165: Q08 hallway stress should roll once, pick one hero and apply forward stress only on an empty tile, before torch loss. Inspect native caller/selector and installed darkness/rules before implementation.
- All788 tests and Release pass; stopped-game hashes match. No pending tests/builds/native-map processes/workers/restores. Game-facing checks remain [?]; no game/protected-save/abandoned-project/art access.
- Ordinary available, session81%, weekly13%, reset1791500512. Continue to around95%, then commit a quota handoff for automatic reset resumption. Goal remains paused without a resume API; quest mapping/implementation continues under owner authorization.

## Round 165: native hallway stress selects one hero before torch loss

Native tile handler 140768820 checks an advancing empty tile or backing step, invokes 1405f98b0 before torch
loss, then resolves effects/content. The helper makes one raw chance roll and selector 1405f9c70 with all filter
flags false uniformly picks one party hero. The forward chance global DAT_142acb918 has initializer/loader/caller
users; caller and helper add no darkness chance. Installed rules give forward0.30/2 stress and backing0.55/5.
The Unity area path loses light before its EnteredSector call, differing from the native ordering.

The mod previously rolled independently for each hero on every tile after light loss, also adding torch
chance metadata. Crawl now gates advancing content squares, rolls once, picks one living hero and uses
pre-step light damage scaling. All eleven direct cases failed before the change; they cover five content
types, three backing destinations, uniform selection, band-crossing event order and raw chance. The old
per-hero average test now isolates an empty destination and checks the installed-rule party mean of0.06 DD2
points per square. Individual modifiers and plot overrides remain separate work, not a full stress-parity claim.

All 710 Core + 89 UI tests and Release pass. DD2 stopped and both deployed hashes match. No launch/save/art
access; native stress presentation remains [?]. Saved encounter flags remain covered by the full suite.

## Status 2026-10-08: round 165 complete, return XP next

- Round164 committed/pushed at5cd5d65; commit/push round165. Next round166: Q20 survivor resolve-XP bonus rounds up separately and stored XP caps at the final threshold. Inspect the current effective-bonus path/native body and installed roster thresholds before implementing.
- All799 tests and Release pass; stopped-game hashes match. No tests/builds/workers/native-map jobs/restores pending. Native gameplay remains unverified; no DD2 launch permission.
- Ordinary available, session85%, weekly13%, reset1791500512. Continue only while below roughly95%; leave clean commits and quota handoff for the existing reset automation. Full quest implementation remains in progress.

## Round 166: corridor roads rotate before window scaling

Owner screenshot showed rectangular road strips at different heights. Gui.Begin already scales/translates
the virtual canvas; GUIUtility.RotateAroundPivot then applies its turn after that matrix. The tile-dependent
pivot consequently moves each strip differently in scaled/letterboxed windows. The public Unity GUIUtility
source confirms that ordering; the Unity reference RaidHallway.LoadHallway reverses sector presentation for
left/bottom traversal. This is a bridge rendering correction, not a change to DD1 corridor rules or native combat.

Moved the actual road draw into linked CorridorGroundUi. Its exact quarter-turn now precedes the screen
transform, retaining the existing vertical-road UV mirroring and restoring the canvas in finally. Eight cases
failed with the old draw after extraction; eleven cover five window sizes including the screenshot dimensions,
both directions, negative/fractional camera positions, matching edges, floor bounds and draw-failure restoration.
The first test compile needed its missing Xunit import; no gameplay change was involved in that fixture repair.
All 710 Core + 100 UI tests and Release pass; DD2 stopped and both deployed DLL hashes match.
Native GPU/clipping/visual checks remain [?]; no DD2 launch, protected-save or abandoned-project access.

Owner priority now explicitly puts Hamlet, selection, room/hallway travel, curios, battles and transitions first.
Return XP is deferred. Later ideas are recorded in PARITY: DD1 resolve levels/tiered damage and utility skill
upgrades, trinkets from both games, and a DD1-style trinket panel with a central main button. These are backlog
items, not implemented claims. Updated the quest map, continuation instructions and stale REA ranking wait.

## Status 2026-10-08: round 166 complete, quota handoff

- Round165 is committed/pushed at bc3956b; commit/push round166's corridor renderer and owner-priority docs.
  All810 tests and Release pass. DD2 stopped; both deployed DLL hashes match. No test/build/worker/native-map
  jobs or save restores pending. Native floor alignment, reverse walking, room fades and combat return still
  need a permitted visual check; the owner's screenshot does not authorize launching DD2.
- Quota pause near the95% cutoff: last reading94% session/15% weekly, ordinary available. Do not start another
  round before the reported reset at2026-10-08T23:01:52Z (1791500512; Paris2026-10-09 01:01:52). The existing
  heartbeat has been updated and will resume selected main-flow work after reset; no duplicate automation.
- Next round167 candidate: Q12 Core movement/room/secret/camp guards while a curio, loot, camp or encounter
  report is pending. Read native event/modal ordering and Unity's awaited raid events before implementing.
  Prioritize Hamlet, selection, hallway/curio flow, battles and transitions; return-XP tuning is deferred.
  Later progression/trinket ideas are in PARITY.md. Full quest parity remains incomplete; the native goal is
  still paused without a resume API, and this authorized manual work must not be marked achieved.

## Round 167: navigation waits for the active event

Quota reset confirmed; resumed from clean/pushed44f0878 with no active workers, DD2 or Ghidra job. Native
InteractWithProp140768320 requires a reset overlay and changes the interaction phase; RaidDisplay vf10 at
14074e0c0 waits in phase4 until the overlay resets, then saves in phase5 before returning to2. Unity
RaidSceneManager.CurioEvent disables movement and awaits LootEvent before restoring movement and clearing
CurrentEvent. These are phase-contract references; no native gameplay was observed.

Core CanNavigate now rejects traversal/new camp during PendingCurio, PendingSpoils, Camp or PendingEncounter.
Room travel, hall steps and secret entry/return share it. Driver rejects step animation/map routes and cancels
a queued room transition before processing it when an event or UI modal owns input. Unopened obstacles/traps
still allow backing away. Collection/dismissal, battle outcomes and camp completion remain usable.

Nineteen regression cases failed before implementation. All24 new cases pass: optional/required reports in
room/hall/secret locations, saved reconstruction with exact state/HP/stress preservation, same-location retry,
actual camp→ambush→saved encounter→battle loot→dismissal→travel and trap/obstacle backing. Updated existing
walk helpers and gather continuation to close their reports rather than walking behind them. Empty native loot
is already auto-dismissed by CrawlUi.DrawSpoils, so the phase guard has an existing completion path.
All734 Core +100 UI tests and Release pass; DD2 stopped and both deployed hashes match. Test TRX is private
at C:\Users\Piral\rea-workbench\evidence\round167.trx. No game/save/abandoned-project access.

## Status 2026-10-09: round 167 complete, environmental interaction guards next

- Round166 is44f0878; commit/push round167. All834 tests and Release green, stopped-game hashes match.
  Native held movement/map clicks during curio/loot/camp and a queued room fade need permitted visual checks.
- Next round168: Q12 prevent optional result replacement and environmental actions behind a pending event;
  retain collection/dismissal. Camp skill/rest and explicit exit guards are separate follow-ups. Main-flow
  features remain first; progression/trinket work stays in the later backlog.
- Session32%, weekly21%, ordinary available, reset1791521699. Continue to roughly95%, then leave a clean quota
  handoff for the existing automation. No tests/builds/workers/native jobs/restores pending. DD2 launch remains
  unauthorized; full quest parity is not complete.

## Round 168: environmental actions respect the active event

Native InteractWithProp140768320 requires a reset overlay; trap/obstacle handler140768820 sets the same
overlay and phase3. Unity CurioEvent/TrapEvent disables movement/environment controls while processing the
event. The previous required-loot guard allowed optional curio results to be replaced by another interaction,
and direct skip/clear/disarm calls could still consume items or resolve props behind camp/loot/encounter state.

InteractCurio, SkipCurio, ClearObstacle and DisarmTrap now use the existing exploration-phase guard. Collection
and dismissal remain available, including optional loot. Thirty-two cases failed before the change; all 34
new tests pass with saved reconstruction, exact state/RNG/actor preservation, retained report identity, empty
results, room/hall variants, native heirloom-chest key consumption after collection/dismissal, and restored
trap/obstacle actions. Updated the older optional-replacement expectation and quest-content survey to dismiss
results explicitly. No new engine state or save migration.

All 768 Core + 100 UI tests and Release pass; DD2 stopped and both deployed DLL hashes match. Before/after
TRX evidence is private under C:\Users\Piral\rea-workbench\evidence\round168. Native click/drag interaction
delivery remains [?]. No game launch, protected saves, abandoned project or binary/art additions.

## Status 2026-10-09: round 168 complete, camp loot phase next

- Round167 is committed/pushed at 2f0ee82; commit/push round168. All 868 tests and Release pass; stopped-game
  DLL hashes match. Native event controls still need a permitted check. No jobs/workers/restores pending.
- Next round169 candidate: camp loot must finish before another skill or rest can replace/hide its report.
  Inspect native camping event ownership and Unity camping loot wait. Explicit expedition-exit gates remain
  separate; then return to Hamlet/quest-selection preparation gaps in the main lifecycle map.
- Session 50%, weekly 23%, ordinary available, reset1791521699. Continue toward roughly95%, leave a clean
  quota handoff and resume via the existing automation. DD2 launch remains unauthorized; full parity is open.

## Round 169: camp loot completes before the next action

Native camp controller1406fbb90 waits for readiness140769e20 between stages; Skills1406fdd70 changes its
controls while raid work is pending. The exact pending pointer type was not identified as loot. Unity's
ExecuteCampEffect(Loot) explicitly awaits LootEvent before the skill loop accepts another choice. Existing
Core allowed another skill to replace PendingSpoils or rest to hide it behind an ambush.

Shared pending-event detection now backs CanNavigate and CanContinueCamp. Meals, skills and rest reject
pending results/encounters without cost, effects or RNG. Driver avoids camp transition sounds on rejection;
the rest button follows the same guard. Saved curio results now precede camp controls, allowing older
overlapping state to be dismissed. Collection and optional-loot passing remain available.

Nine cases reproduced the bug; eleven new cases cover actual pilfer/supply/trinket_scrounge, full/free packs,
saved reconstruction, report identity/state preservation, collecting then closing before another skill,
optional-loot pass and older curio/camp overlap through meal/rest. All 779 Core + 100 UI tests and Release
pass; DD2 stopped, both deployed hashes match. Private TRX under rea-workbench/evidence/round169. Native
camp scroll/button/audio verification remains [?]. No launch/protected-save/abandoned-project access.

## Status 2026-10-09: round 169 complete, explicit exit gates next

- Round168 committed/pushed at a0831a8; commit/push round169. All 879 tests and Release pass; stopped-game
  hashes match. Native event/camp interactions remain unverified. No jobs, workers or restores pending.
- Next round170: Q18 explicit return/abandon must wait for pending events and camp/encounter completion;
  inspect native completion and retreat helpers. Preserve completed-quest exploration, optional-loot passing,
  no-retreat policy and forced outcomes. Then prioritize provision/quest-selection gaps.
- Session 75%, weekly 27%, ordinary available, reset1791521699. Reserve time for a clean cutoff around95%.
  Existing automation resumes after reset. DD2 launch remains unauthorized; full parity remains open.

## Round 170: explicit returns wait for event completion

Native completion helper140741fb0 checks combat, camp, interaction phase and readiness140769e20. Unity
CurioEvent and CampingEvent disable retreat during their events. CanLeaveExpedition now uses CanNavigate,
covering optional results, camp and pending encounters as well as the existing quest retreat policy.
Collection followed by dismissal is required before explicit return; optional loot may still be passed.
Driver's forced wipe sets Ended independently and does not use this explicit-return guard.

All eight new cases failed before the change and now pass for success/abandon, saved reconstruction,
state preservation, report dismissal, camp/battle completion and one terminal result. Updated required-loot
and optional-pass fixtures to close their reports. All 787 Core + 100 UI tests and Release pass; DD2 stopped,
both deployed hashes match. Private TRX: rea-workbench/evidence/round170. Native return/abandon controls
remain [?]. No game launch, protected-save or abandoned-project access.

## Status 2026-10-09: round 170 complete, quota pause

- Rounds167–169 pushed at 2f0ee82, a0831a8 and f5f1c06; commit/push round170. All 887 tests and Release pass,
  stopped-game DLL hashes match. No tests/builds/workers/native jobs/restores pending. Native event/camp/
  return controls and the prior corridor fix still require permitted visual checks.
- End-of-round usage check reached97% session/31% weekly, ordinary available. Stop implementation now;
  only finish this clean handoff. Quota pause until reported reset1791521699 (2026-10-09T04:54:59Z).
  The existing active heartbeat resumes after reset. Reserve more headroom before the next final round.
- Next round171 candidate: Q04 provisioning cancellation should clear the purchase cart when returning to
  quest selection. Follow native1406d3e30 and Unity return-to-preparation; account for the mod charging only
  on embark, rather than inventing a refund. Then address Q03 starting supplies in the preparation inventory.
  Main-flow priorities remain in force; later progression/trinkets are deferred. No DD2 launch permission.
  Full quest parity remains incomplete; do not mark the goal achieved.

## Round 171: generated regional corridors

Owner 2026-10-09 requested generated corridor diversity like the room set and explicitly authorized launching
DD1, DD2 and the mod for tests. CLAUDE, handoff, parity loop and the existing heartbeat now retain that
permission. Protected estates remain off limits; only estate 2 was used.

DD1 crypts.corridor_wall.00–05 and Unity RaidHallway.LoadHallway/RaidHallwayView retain regional ordered
segments. The mod only drew generated scenes in rooms. Added a separate private corridor pool under the
existing NativeRoomSceneryFolder, with dd2_<region>-corridor-<NN>.png names, the existing PNG bounds and
12-image limit per pool. Seed/corridor ID choose scene/mirror without gameplay RNG. A full panorama and its
floor scroll together; reflected repeated edges join continuously in the existing physical camera coordinates.
The room fade and arrival cue remain, and generated corridors do not depend on native Addressables success.
Worker file IO and at-most-one main-thread upload/update are shared; completed pools adopt once and clear
on region/expedition exit. Failed or missing art retains the previous scene path.

Eight built-in imagegen panoramas use the private native exterior/resistance reference renders: charred street
and library lane, farm lane and blighted orchard, ruined forest and palisade track, boardwalk and sandy shore.
Four first candidates received localized bottom-ground edits; no raster manipulation outside imagegen.
All final PNGs decoded opaque RGB, six 2048x768 and two 2043x770. Full prompts, corrections, paths, reference
hashes and output hashes: C:\Users\Piral\DarkestDungeon3\local-art\corridors\manifest.json. Eight installed
copies in game\PrivateScenery hash-match. No source-derived imagery or binary evidence entered Git.

All 792 Core +106 UI tests pass. Five new Core cases cover discovery/pool separation and stable regional
selection. Six linked panorama draw cases cover floor/viewport/edge coverage, direction/mirroring, scaled and
letterboxed canvases and restoration on failure. The actual loader test now checks two valid corridors and
one invalid corridor alongside room art, completed adoption, single uploads, fade, fallback and teardown.
Release build passed; both normal deployed DLL hashes matched with DD2 stopped.

Native visual checks used a temporary F2 shortcut in a private test build to enter estate 2 directly (avoiding
picker reads of protected estates) and cycle preview region while crawling. No such shortcut is committed or
left installed. Both images in all four regions were inspected in the actual game; real map navigation left
room2 for corridor2, returned to room2, then entered corridor1 in the opposite heading and displayed its
other variant. Screenshots at 1600x900 and 1440x900 show continuous ground, party/curio placement and the
curio interaction panel. A native Curio minimap tooltip was also visible. The resume retry correctly handled
our early entry while the main menu was still transitioning. No BepInEx exceptions/errors. This is visual
scenery verification, not a full quest/combat or prolonged memory test.

Private evidence: C:\Users\Piral\rea-workbench\evidence\round171. The test game was stopped by exact PID;
DebugKeys.cs was restored byte-for-byte, estate_2.json and its .bak restored and hash-matched after process
exit, then the normal Release was rebuilt/deployed. No protected-estate or abandoned-project access.

## Status 2026-10-09: round 171 complete, quota handoff

- Round170 is committed/pushed at001f83c. Commit/push round171 corridor art loader/drawing/tests/docs.
  All898 tests and Release pass; normal stopped-game deployment hashes match. Eight private images are
  installed locally and documented; their generation prompts and native evidence stay outside Git.
- Session89%, weekly45% at verification boundary. Reserve the remaining allowance for clean handoff;
  do not start another round before reset1791540022 (2026-10-09T10:00:22Z). Existing automation resumes
  after reset. No tests/builds/native jobs/test processes or save restores pending.
- Launching DD1/DD2/the mod for tests is explicitly authorized as of2026-10-09, superseding older no-launch
  notes. Use estate2 only; never read protected estates. The normal picker reads all slots: future automation
  must likewise avoid that path, using an isolated test entry if needed.
- Next round172 candidate: Q04 provisioning cancellation clears the purchase cart when returning to quest
  selection. Follow native1406d3e30 and Unity return-to-preparation; mod charges only on embark, so no
  invented refund. Then Q03 starting supplies in preparation inventory. Main-flow priorities remain first;
  broader native combat/reload/long-session checks and later progression/trinkets remain open.

## Round 172: future-feature roadmap and complete installed DD2 item inventory

Owner added five tracks: DD1 old-memory curio/boss/trinket with transformed hero visuals; the Mountain
as an authored final region with void-themed hall/room fights and a confession boss per quest; camping
items based on DD2 inn items with a cap of 1; possible DD1-style stun; and DD2 shop supplies/new curios.
Recorded each in tools/overhaul_roadmap.md with dependencies, proposed first slice and acceptance cases.
The camping cap's scope remains open. Native DD2 already has stun/daze; access and balance need an audit.

Built tools/map_dd2_items.py and an offline browser template. The private catalogue reads the current
game StreamingAssets/Excel: 553 CSVs, 615 Item definitions, 602 IDs, 13 IDs with variants. Unique types:
85 combat, 70 rest, 109 currency, 107 stagecoach, 211 trinket, 20 memory. Mode/DLC/editor records remain
distinct; enumeration is not proof of enabled inventory. Source paths/lines/hashes and raw rows preserve
duplicates, repeated fields and empty positional columns. Explicit active item bindings all resolve.
The private snapshot has 7,624 evidence records and retains skill payloads, effects, costs, loot entries
and parent tables. Candidate cross-references are labelled; no runtime interpreter or load order claimed.

Read native ItemDefinition constructor/FetchDefinitions, ActorDataSkill payload selection,
ActorInstance use histories and inn resets, RestWidgetBhv target/pair/party application and
TriggerStoreBhv/StoreInventory loot draws. Findings in tools/dd2_item_mapping.md. Per-actor item/tag
limits differ from stack/possession limits; inn histories cannot be copied into camping without an
explicit reset/persistence rule. Store roots also come from serialized components, so costs or CSV
ancestry do not certify live shop availability. Static links do not prove native behaviour.

Read installed DD1 supply inventory/curio tables and Unity Curio/CurioInteraction; mod Supply/ItemCatalog
and CurioResolver still own DD1 identities and consumption. Proposed adapter must distinguish game
source and preserve legacy IDs. New curio use must be an explicit authored rule, not an accidental
invocation of a native combat skill. Existing final boss Mountain mapping was noted for the later audit.

Validation: 10 synthetic Python cases cover variants, empty columns, repeated effects, skill precedence,
unresolved IDs, positional loot conditions/zero weights/cycles, malformed input, private-output protection
and HTML escaping. First run exposed an empty test fixture for the existing-evidence check; corrected the
fixture and all10 pass. All792 Core +106 UI tests and Release pass with existing warnings; DD2 stopped,
built/deployed DLL hashes match. No runtime source or gameplay changes, no game launched or saves read.
Corrected stale handoff wording that still suggested slot3; both owner slots remain protected.

Browser check: the in-app automation entry failed with missing kernel assets. Used headless installed Edge
to check all615 entries, search, rest filtering, variants, bandage effect/loot drill-down, whiskey's adverse
effects, zero-result search and a640px layout; zero JavaScript errors. Inspected screenshot at1440px.
The browser tab was queued with open_in_codex. This verifies the research viewer, not native game UI.

Private output/evidence: C:\Users\Piral\rea-workbench\evidence\round172\dd2-items-v2\ (HTML/JSON/CSV).
Round172 directory also holds original browser-check script/screenshots; v1 is the earlier private snapshot.
Only original tooling, synthetic fixtures and authored documentation enter Git.

## Status 2026-10-09: round 172 complete, future ideas retained

- Round171 is committed/pushed at2d631c1. Round172 maps the requested installed item universe and records
  all five future tracks; commit/push its tooling and documentation. No campaign feature from these tracks
  is enabled yet. Per-item native behaviour, live shop filters/roots, art coverage and balance remain open.
- All908 tests and Release pass. DD2 is stopped and the deployed normal DLLs match Release. No test game,
  save restore, build, Ghidra or REA jobs pending. Private catalogue preview serves only127.0.0.1:8772
  from dd2-items-v2, exec session51423, for owner browsing; it performs no background game work.
- Usage64% session/57% weekly at the finalization boundary; no quota pause required. Check fresh limits
  before another round and reserve finalization headroom before95%. The earlier round171 pause has reset.
- Next main-flow round173 candidate remains Q04: clear purchase cart on return to quest selection,
  referencing native1406d3e30 and Unity return-to-preparation. Then Q03 starting supplies. Reported defects
  remain first. Refine overhaul tracks individually from tools/overhaul_roadmap.md when selected; do not
  silently settle the camping cap, memory defeat/repeat rules, confession sequence or stun balance.
- Launches are authorized; use estate2 only with backup/restore/hash checks. The normal picker reads
  protected slots, so avoid it in automation. No protected estate or abandoned project access this round.

## Design follow-up 2026-10-09: Faded Memory and Haunting Memories

Owner refined O3: generated DD1-looking curio named Faded Memory, guaranteed once on every boss quest.
Hand interaction enters a DD1 boss fight in its corresponding DD1 zone. Change only hero sprites;
retain current skills/stats and native DD2 combat. Boss kill drops two items: one random Legendary
DD1 item and one random Ancestor DD1 item, such as Ancestor's Pistol. This supersedes the earlier
single-trinket sketch and its open placement/entry-method questions.

Owner also added Haunting Memories: a special event makes a random quest in an area a DD1 dungeon,
using our current expedition mechanics and DD1 trinket loot. Later these special features replace
the manual DD1/DD2 region switch. Keep that switch until the features and preservation/migration
of existing progress are ready. The event is a full quest; the curio is a separate boss encounter.

Specification: tools/faded_memory_design.md. Proposed original hand-action copy: "Confront the past."
Recorded art/visual boundaries, checkpoint/return and two-reward persistence requirements, event
selection persistence and acceptance cases. Ordinary event quests do not inherit boss-curio rewards;
automatic hero-sprite conversion for full event quests was not specified. Defeat/retreat/replay rules,
event scheduling, boss/visual mappings and exact reward pools still need refinement.

Installed DD1 base.rarities.trinkets.json has very_rare, ancestral and ancestral_shambler, not a
legendary rarity ID. base.entries.trinkets.json lists ancestors_pistol as ancestral. Retain the owner's
Legendary label; mapping it to very_rare is a proposal, not a silently chosen rule. Inclusion of the
Shambler's separate ancestral pool and duplicate/unique ownership handling remain open.

Updated roadmap, PARITY, CLAUDE and handoff pointers so later sessions use the refined rules. This
follow-up changes Markdown only: no art generated, runtime code changed, game launched or saves read.
Checked documentation links and diff; previous round172's 908-test/Release result remains the last
code validation, not a new test run. Main-flow implementation round173 remains available for Q04.

## Status 2026-10-09: memory features specified, implementation pending

- Round172 is committed/pushed atbfeb315. Commit/push this Markdown-only design refinement separately.
  Read tools/faded_memory_design.md for Faded Memory/O3 and Haunting Memories/O6. Their agreed rules
  supersede earlier idea sketches; unresolved choices are listed there. Neither feature is enabled.
- Last code validation is908 passing tests and Release from round172. No new build/deploy/native test
  was needed for the documentation change. No process launched, save read or restore pending this turn.
- Session usage84% at finalization; check fresh usage before further work and retain headroom before95%.
  No new quota pause. Existing hourly automation reads the latest Status and roadmap.
- Next main-flow implementation candidate remains round173/Q04 provisioning-cart cancellation, then
  Q03 starting supplies. Owner is detailing future tracks; retain these specifications and refine each
  before implementation. Keep the region swap control until special-feature access and migration work.

## Status 2026-10-09: quota headroom handoff

- Heartbeat10:53Z found session usage91%, weekly61%, ordinary usage available. Reserve the remaining
  allowance for finalization rather than start a round requiring native research, tests and deployment.
  Quota pause until the reported reset1791559553 (2026-10-09T15:25:53Z); resume on a later scheduled run
  after reset and a fresh usage check. No additional owner action is needed.
- Round172 is committed/pushed atbfeb315; the Faded Memory/Haunting Memories design refinement is
  committed/pushed at39425fe. Checkout was clean on claude/practical-wright-hicri0 before this handoff.
  No implementation round started, no game/save access or new test/build/analysis job this heartbeat.
- Last validation remains908 passing tests and Release from round172. Native runtime verification of
  future features is still pending. Keep the private catalogue output outside Git.
- Next implementation candidate remains round173/Q04, then Q03. Read tools/faded_memory_design.md
  for the owner's refined future-feature rules; preserve unresolved reward pools and encounter policy.

## Round 173: clear provisions when returning to quest selection

Q04 reference: private DD1 FUN_1406d3e30 refunds tracked spending and clears the provision inventories.
Unity EstateSceneManager.cs return from ProvisionScreen (around line741) calls SellOutEverything before
restoring preparation. Our purchase cart is only charged at embark, so cancellation must clear it without
crediting estate gold. Previously Back only changed two flags: quantities and their slot layout survived
quest/party changes, as did an old failure message.

Moved the actual Back transition and its existing state into EmbarkUi.Navigation.cs, linked into the UI
test project. Back now clears quantities, layout and error and returns immediately from provision drawing;
quest and party stay selected. No Core economics or native combat changes. Before the fix, two regression
cases failed on retained twelve food/two keys and food leaking into the next real Embark.Create. All six
new navigation cases now pass, including non-Back/disabled input. Full validation:792 Core +112 UI =904
tests pass. The previous ten catalogue-tool tests are not included in this round's test count.

Native check used estate2 only with byte-for-byte backups of its main file and .bak. A temporary F2 entry
loaded estate2 without opening the protected-slot picker; its subsequent reflection-based party setup
failed twice (NullReferenceException, temporary DebugKeys.cs:45). Switched to ordinary Hamlet and quest
controls instead of repeating that setup. Selected The Shroud/Apprentice Medium Explore and four heroes;
gold in Hamlet was58,175. A carried Jester icon remained at the Provision button and two clicks did not
enter the shop. Later keyboard driving reported ChatGPT as foreground. The Back/reopen native acceptance
case is therefore unverified, not a pass. Diagnose focus/automation versus a drag-release defect separately.
The log's two errors belong to the removed temporary setup; no normal navigation exception was logged.

Stopped exact game PID32576. Restored estate2, its .bak and DebugKeys.cs from the private manifest; all
three SHA256 checks match. Forced normal Release Rebuild after restoring source, because copied source
timestamps can otherwise allow an incremental build to retain test code. Build succeeded with the three
existing warnings and no errors; deployed both DLLs only while DD2 was stopped and verified both hashes.
Normal DebugKeys has no Git diff. No protected estate or abandoned project access.

Private evidence: C:\Users\Piral\rea-workbench\evidence\round173, containing the backup manifest,
screenshots01-07 and copied BepInEx/Player logs. No private assets or native analysis entered Git.

## Status 2026-10-09: round 173 cancellation implemented; native check pending

- Q04 cancellation fix and six regressions are ready to commit/push on claude/practical-wright-hicri0.
  All904 Core/UI tests and normal Release Rebuild pass; deployed DLL hashes match. Native verification
  remains [?]: buy/rearrange supplies, Back, preserve quest/party, reopen empty cart with full stock/zero
  cost/unchanged gold, then repeat after changing quest. Investigate the recorded quest-board input issue
  first if it reproduces with focus secured; reported input/rendering failures retain priority.
- DD2 stopped; estate2 main/.bak restored with matching hashes; temporary DebugKeys removed. No test,
  restore, build or analysis jobs pending. Earlier private catalogue preview remains independent.
- Usage83% session/76% weekly at final rebuild, ordinary usage available. Reserve finalization headroom;
  check fresh usage before another round. Reported session reset1791579072 =2026-10-09T20:51:12Z.
- Next preparation feature is Q02 free starting supplies/combined pack capacity. Earlier Status entries
  incorrectly called this Q03; Q03 actually concerns the plot warning flag and no-torch confirmation.
  Keep the owner's future Faded Memory/Haunting Memories specification pending its unresolved choices.

## Status 2026-10-09: round 173 pushed; reserve quota for the next reset

- Round173 committed/pushed at b3da393. Checkout clean after push. All904 Core/UI tests and normal
  Release Rebuild pass; both deployed hashes match. Native Back/reopen verification remains [?], with
  the quest-board drag/focus observation recorded in PARITY. No game, temporary harness or restore pending.
- Session87%, weekly77%, ordinary usage available at finalization. The remaining allowance is too small
  for another complete research/test/native-check round. Quota headroom pause until reported reset
  1791579072 (2026-10-09T20:51:12Z), then check usage and resume. No owner action required.
- Next: resolve or isolate the native quest-board input obstruction and finish Q04's visual acceptance
  check; then Q02 free starting supplies. Keep later overhaul features deferred to their recorded design.

## Status 2026-10-09: owner starts Faded Memory and Haunting Memories

- Owner's latest "Start" selects O3 Faded Memory, followed by O6 Haunting Memories. Resume from
  tools/faded_memory_design.md after reset. Start with boss/zone/hero-sprite coverage and saved encounter
  state, then one complete hand interaction -> DD1 boss fight -> two rewards -> return implementation.
  Preserve DD2 player skills/stats. Guarantee one reachable curio on every boss quest once enabled.
  Keep the region switch until special access and migration are ready. Exact reward pools and remaining
  encounter policy are still open; the request repeats the agreed rules without settling those details.
- Fresh usage92% session/77% weekly; preserve the owner's approximately95% stop rule. Quota pause
  remains until1791579072, 2026-10-09T20:51:12Z. This turn records the selected priority only; no new
  gameplay implementation, game launch or save access. Last code validation remains904 tests/Release.
- Round173 fix is pushed at b3da393, handoff at80c4d3b. Its native Back/reopen and drag/focus checks
  remain pending, as does Q02. They are not silently marked complete by this priority change.

## Round174: Unity consumes rawType too, breaking every carried release

Owner reported Stagecoach recruitment, Blacksmith/Guild input and curio right-click failures. Prioritized
these before Faded Memory. The owner also confirmed its Legendary draw uses DD1 very_rare trinkets;
updated tools/faded_memory_design.md. Fresh allowance reset to2%, superseding the earlier quota pause.

A temporary native OnGUI probe through the real Drag.Source/Begin/Drop/Overlay reproduced the shared
drag failure without any save access: before Begin, type/rawType were MouseUp; after Event.Use both
were Used. Drop returned false and Overlay left Active=true. The headless shim preserved rawType and
therefore hid the defect. Changing the shim to match measured Unity behaviour made four existing drag
tests fail. Capture left release at Begin before consumption, deliver once to a target, and clear pending
click/drag state at Overlay. Two extra cases cover consumed ordinary clicks and right-release/repaint.
All12 drag cases, 114 UI and792 Core tests pass. Native rerun now reports delivered=True/Active=False.

DD1 references: installed stage_coach/blacksmith/guild layout files; Unity RecruitSlot.OnEndDrag and
HeroSlot/InventorySlot drop paths. The defect is in our IMGUI adapter, not DD1 recruitment/economics.
Live estate2 session recorded roster6->7 and recruits3->2, and displayed a hero in Guild. Purchases are
not yet verified. The test window began receiving user input, so further scripted input stopped.
Curio right-click is a separate missing route in DrawInventory and remains the next fix.

Private evidence: rea-workbench/evidence/round174/native-red.log, native-green.log and screenshots.
Native test PID27644 remains running at this point; it uses a temporary automatic estate2 entry and
state logger. Source probes were restored from backups; normal Release Rebuild with Deploy=false passes.
Do not restart the currently deployed test DLL. After this test session exits, restore estate2 main/.bak
from round174/backup-manifest.json, verify hashes, deploy normal Release and compare DLL hashes.
No protected estate access or normal picker. Preserve this pending restore through further rounds.

## Round175: right-click inventory items on the open curio

DrawInventory previously supported left-click hero use and drag-to-curio only. Added a right-click
route before the ordinary button. An open panel matching the current room/hall location receives the
item; closed/stale panels route to the selected hero. Existing UseOnCurio validates the supply and
Driver/Core retain consumption, effects and persistence. Active drags, disabled input, pending reports,
camp and blocked navigation cannot use items. No combat or curio outcome rules changed.

Reference: Unity InventoryItem.OnPointerClick handles right-button item activation, while the curio
interaction slot dispatches through the interaction inventory. DD1 heirloom_chest's skeleton_key
interaction supplies the consumption/loot contract. Eleven linked UI/Core regressions cover room/hall
consumption exactly once, saved reports, stale/closed panels and blocked input; four positive cases
failed before the route existed. All 125 UI +792 Core =917 tests and normal Release Rebuild pass.

Native estate 2 verification also completed round174's building checks: dragging MacRae into the
Blacksmith/Guild succeeds. Weapon rank 0->1 costs 600 gold (63,175->62,575); mastering Hand of Light
costs 1,000 (62,575->61,575). Screenshots03/05 and native-first.log record those purchases.
The first curio fixture accidentally retained its quest-goal flag; right-click reached the interaction
but the quest correctly requested holy water. It did not prove chest-key consumption. Corrected only
the private fixture. Screenshot13-key-used and native-final.log now show Used Skeleton Key, key 1->0,
water remaining 1, CurioTaken=true and 4 deeds/2 busts/4 crests presented. The brief white prop in
screenshot11 resolved once its texture loaded (screenshot12); no lasting rendering defect established.

Interrupted native test PID27404 had already exited when this heartbeat resumed. Preserved its logs
and restored estate 2 main/.bak and DebugKeys from round174/backup-manifest.json before the final test.
Stopped verified test PID28240 after that check, restored all three again and matched manifest SHA256s.
Both estate backups in the actual manifest have hash64B7A80D...; use that manifest, not an earlier summary.
Forced normal Release Rebuild after source restoration, deployed while DD2 stopped and checked both DLL
hashes. No DebugKeys/UiRoot diff or private harness remains. No protected estate/picker/abandoned project.
Private evidence: C:\Users\Piral\rea-workbench\evidence\round175. The earlier log contains an
IMainInstaller shutdown exception after the interaction; the final chest check logged no exception.

## Status 2026-10-10: input fixes verified; continue Faded Memory

- Round174 is pushed at e3e0784. Round175 curio right-click and its 11 regressions are ready to commit
  and push. All 917 tests and normal Release Rebuild pass. Native recruitment, building purchases and
  chest-key right-click are verified. Both deployed DLL hashes must match the build at finalization.
- DD2 stopped; estate 2 main/.bak and DebugKeys restored with manifest hashes. No temporary harness,
  save restore, test, build or analysis job remains. The interrupted test cleanup is complete.
- Usage reset confirmed at 1% session/16% weekly, ordinary usage available. The previous pause is over;
  check usage at round boundaries and preserve finalization headroom before 95%.
- Next selected feature: O3 Faded Memory, then O6 Haunting Memories. Start with native boss/zone/hero
  sprite coverage and a saved encounter contract; do not expose an incomplete curio. Legendary now
  means the owner-approved very_rare DD1 pool. Remaining Ancestor/unique-item and defeat/retreat rules
  stay open in tools/faded_memory_design.md. Keep the region switch. Q04 native Back/reopen remains
  pending separately; its shared drag obstruction is repaired, but cancellation has not been observed.

## Round176: Faded Memory coverage and saved return position

Started O3's first bounded slice. Audited existing boss translation, hero art and DD1 reward sources;
tools/faded_memory_coverage.md records the findings and next adapters. Private audit scanned installed
DD1 monster/mash data and DD2 hero-tagged classes, retaining input hashes outside Git. It found 80
boss-tagged records across 27 families (including companions/props), 15 DD2 hero IDs and 13 namesake
DD1 art sets. Duelist/Runaway lack namesake art. Base reward files have 24 very_rare, 9 ancestral and
5 ancestral_shambler entries; these counts do not settle DLC/uniqueness/eligibility policy.

Current Driver excludes bosses from DD1 translation, while zones.json substitutes DD2 boss encounters.
The existing skill mapper drops summon/capture/control contracts and may omit excess skills. Private
native EffectTarget::GetTypeFromId1404e6c00 and installed NecroSummon 1 corroborate summon candidate,
weight, count and no-loot fields. A native mechanic has not been implemented merely by finding them.
The apprentice Necromancer is the first adapter candidate; all enabled boss destinations still need
coverage before live placement. Dd2Catalog rarity translation also returns DD2, not DD1, trinkets.

Added optional ExpeditionState.FadedMemory identity/presentation data and MemoryReturnPosition.
Capture/restore validate the source quest, dungeon, seed and coordinates. Restore changes location
only, preserving combat HP/stress/deaths, current inventory, loadout, quest state and RNG. It includes
secret-detour and previous-entrance coordinates without assuming that a remembered entrance is the
current room's corridor (ordinary travel retains it). No runtime caller exposes/enters a memory yet.

Twenty-one cases use real Crawl travel and save reconstruction for room, forward/reverse hall and
secret room, then verify retained consequences/loadout and usable secret exit. Foreign/invalid
checkpoints reject without mutation; legacy saves keep the same route/JSON and no new memory.
Fixed a test's incorrect HeroRecord.Skills name to EquippedSkills before running the cases. All
813 Core +125 UI =938 tests and Release pass. Native memory visuals/transitions remain unverified;
the feature is deliberately not exposed before boss, hero, reward and return routing are complete.
DD2 remained stopped. No new save access/game launch; both deployed hashes match. Private audit
and native catalogue are at rea-workbench/evidence/round176 and do not enter Git.

## Status 2026-10-10: round176 memory checkpoint ready

- Round175 is committed/pushed at a946c06. Round176 adds saved return-position groundwork and
  coverage notes; all 938 tests/Release pass. Commit/push this slice before continuing O3.
- DD2 stopped, normal source/deployment, estate 2 already restored with matching hashes in round175.
  No harness, save restore, test/build or native analysis job remains. No playable memory curio yet.
- Next: explicit boss adapter, beginning with Necromancer summon/skill/AI coverage, and DD1 hero
  rendering. DD1 reward identity/effects and guaranteed reachable placement remain separate slices.
  Use tools/faded_memory_coverage.md. Preserve very_rare confirmation and remaining policy questions.
  Keep the region switch and record native Q04 Back/reopen as pending.
- Check fresh usage at this boundary; stop with a clean handoff before approximately95%.

## Round177: native Necromancer summon attempt contract

Round176 was committed/pushed at fafcc9e. Followed the installed NecroSummon 1/2/3 effects through
private DD1 EffectApplyInternal@1404bc4f0. Candidate weights roll cumulatively before capacity is
checked; even an oversized/full-board failure spends the attempt. Unity's retry-on-oversize behavior
differs. DD2's native summon queue can retain an unplaced entry, requiring bounded processing in the
future bridge. Team.OnQueueSummon can replace actors; DD1 corpse eligibility and timing remain open.

Added Dd1SingleSummon for the proven single unconditional performer effect. It reads weighted pools,
the no-loot flag and once/on-hit/on-miss contract, plans front-rank placement, spends RNG before the
capacity decision and never rerolls a smaller candidate. Unsupported fields/rules and missing actor
coverage fail closed. It does not enqueue actors, determine corpse capacity or enable a boss.

Thirty regressions use all installed Necromancer tiers/skills plus synthetic boundary/invalid cases.
All 843 Core +125 UI =968 tests and Release pass (three existing plugin warnings). No save access,
game launch or Ghidra job. Native summoning remains unverified, as does the complete memory flow.

## Status 2026-10-10: round177 summon groundwork verified

- Round175 input repairs and round176 memory checkpoint are pushed. Round177 bounded summon rules
  pass all968 tests/Release; commit/push before the next O3 slice. No live memory curio/boss yet.
- DD2 stopped; normal deployment only. Estate2/DebugKeys cleanup/hash restoration was completed
  in round175. No pending save restore/harness or native analysis job. Keep protected estates untouched.
- Continue with DD1 hero sprite presentation and explicit boss runtime dependencies, using
  tools/faded_memory_coverage.md. Very Rare reward confirmation stands; other reward/retreat policies
  remain open. Preserve region switch. Q04 native Back/reopen remains a separate acceptance check.
- Check usage at this boundary and retain finalization headroom before95%.

## Round178: DD1 hero sprite presentation without player conversion

Round177 committed/pushed at55c9cba. Added Dd1HeroArt: campaign hero anim skeletons/atlases plus
outfit-A PNG pages, base or campaign DLC. Never falls back to arena or unrelated classes. Shared
DD2 skill names select DD1 poses, with mastery/path suffixes accepted; new unmatched skills stay
in combat pose pending explicit visual mapping. Twenty-four tests load nonempty poses/pages for
all13 namesake heroes and reject missing Duelist/Runaway/invalid IDs. Unity HeroClass.SkillArtInfo
and installed hero .art.darkest combat_skill entries corroborate pose identity and outfit separation.

Dd1MonsterView.PrepareHeroes reuses drawing via exact player GUIDs. It never calls enemy MapSkills,
naming or AI restriction for heroes. Right-facing hero sprites keep native player effects visible.
Clear restores remembered forceRenderingOff states, including late model parts; hero ratios are
excluded from the enemy scale median. No production controller or curio activates this path yet.

All867 Core +125 UI =992 tests and normal Release Rebuild pass. Native temporary estate2 probe
PID29700 shows Vestal/Flagellant/Jester/Leper converted in screenshot01-dd1-heroes, and restored DD2
models in screenshot04-restored. Immediate class/name/max-HP/equipped-skill snapshots compare equal
both at preparation and Clear. Screenshot02's attempted attack did not prove live attack pose timing;
keep attack/defend/death and the other heroes pending. No exceptions found in captured logs.

Corrected WinDrive key syntax from F2 (rejected) to0x71; screenshot03 was before restoration and is
not acceptance evidence. Source review excluded heroes from the enemy scale median and clears the
draw flag on restoration; the final normal build includes these cleanup details. Stopped exactPID29700
and waited for exit, restored estate2 main/.bak and DebugKeys from round178/backup-manifest.json,
verified all hashes, then forced normal Release Rebuild/deployment while DD2 stopped. Private
screenshots/logs/probe remain under rea-workbench/evidence/round178; no native assets/evidence inGit.

## Status 2026-10-10: round178 hero presentation verified in isolation

- Round177 is pushed. Round178 adds the isolated DD1 hero drawing path; all992 tests/Release pass.
  Commit/push before continuing. Full Faded Memory encounter remains disabled/incomplete.
- DD2 stopped. Estate2 main/.bak and DebugKeys restored/hash-matched. No pending save restoration,
  probe source, game/build/test or native analysis job. Check deployed DLL hashes at finalization.
- Next O3: explicit boss runtime adapter and DD1 reward identity/equipment, then one complete
  hand/fight/reward/return path. Use tools/faded_memory_coverage.md. Shared-skill pose coverage is
  implemented; new-skill visual equivalents and Duelist/Runaway art still need work.
- Preserve owner Very Rare confirmation, remaining reward/retreat questions, region switch and
  Q04 native Back/reopen acceptance. Check usage at the round boundary; stop with headroom by95%.

## Round179: independent DD1 memory enemy kits in native DD2 combat

Round178 pushed at354473c. Ordinary translated enemies borrow DD2 class numbers and pair a limited
skill list. Memory bosses need complete independent kits. Dd1EnemyKit reads exact installed tiers,
HP/speed/turns/size/resists/protection/skills and life-link metadata; missing tiers never fall back.
The scoped factory registers apprentice Necromancer/common/militia skeletons with independent DD2
class/stat/skill elements and private ResourceActor/ResourceSkill clones. ActorInstance initializes
identity and skills from resource name/list, so aliases alone are insufficient. Clear skill ID overrides
on clones; patch only LibraryActors.CreateActor(string), never the generic resource getter. Art naming
uses the prepared skill map without retranslating generated IDs. No normal gameplay caller yet.

Installed skeleton life_link.base_class=necromancer and militia prot0.15 were discovered and recorded.
Protection maps to health_damage_received_percent=-0.15 (native SkillCalculation direct damage, separate
from DOTs); DD2 accuracy remains native. Linked deaths, corpses, skeleton brains and summons are next.

Eleven new cases and all878 Core +125 UI =1003 tests pass. Native temporary estate2 fight PID18912
creates boss105HP/speed8/all3 attacks and skeleton8/10HP, all Random controllers and complete skills.
Screenshot02 shows Necromancer Apprentice, Six Feet Under and DD1 enemy sprites. Original Bishop
resource/class snapshot remains identical before/after creation. No exceptions found in captured logs.
Fixed a temporary probe namespace compile error before launch. Stopped exactPID18912, waited for exit,
captured private logs/screenshots, restored estate2 main/.bak/DebugKeys/Driver and verified four hashes.
Forced normal Release Rebuild/deployment with DD2 stopped; no harness or save change remains.

## Status 2026-10-10: round179 independent boss factory verified

- Round178 is pushed. Round179 native actor factory and all1003 tests/Release pass; commit/push next.
  Complete Faded Memory remains disabled until summons, reward equipment and routing are ready.
- DD2 stopped; estate2 main/.bak and probe source restored/hash-matched. No pending save restore,
  harness, game/test/build or native analysis job. Verify deployed DLL hashes at finalization.
- Next O3: once-per-skill summon bridge, dynamic DD1 sprites, Necromancer-linked skeleton death and
  corpse handling using installed/native/Unity evidence. Keep unresolved encounter policies open.
- Preserve Very Rare reward confirmation, remaining reward questions, region switch and Q04 native
  Back/reopen acceptance. Check fresh usage at this boundary and reserve finalization headroom by95%.

## Round180: bounded once-per-attack Necromancer summons

Round179 pushed ata82e261. Connected prepared enemy summons to native EventSkillFinalizeResults,
which fires once after the full result, so multiple targets/hits/misses do not multiply attempts.
Validate every candidate before consuming DD2 SUMMON RNG; spend a roll on a full formation as native
DD1 does. Queue exactly one front actor with removeAfterProcess, never defer it to a later opening.
Reject loot-bearing summon contracts; generated classes inherit no stand-in loot. Post-native team
creation adds an exact-GUID sprite and restarts bounded binding without clearing party presentation.

All878 Core +125 UI =1003 tests and Release pass. Native temporary estate2 probe PID18404 let the
boss perform real attacks while heroes passed through the native EventBattlePass. Formation grew
1->2->3->4, front placement verified in actor snapshots; next attack logged attempt spent/0 free
ranks and no fifth actor. Screenshot03 shows all skeletons plus retained DD1 hero sprites during
all-target stress. No exceptions found. Fixed missing Core.Expedition using before launching.

Corpse_A.can_be_summon_rank and Unity AvailableSummonSpace establish corpse capacity as a following
gap; this slice checks genuinely free ranks. Native zero add-to-turn-order means next-round timing,
consistent with Unity's absent initiative list but not yet direct-native DD1 acceptance. Death-chain
metadata exists in DD2 and may supply linked skeleton death after confirming DD1's base-class rule.

Stopped exactPID18404/waited exit, captured private round180 logs/screenshots, restored estate2
main/.bak/DebugKeys/Driver and verified all hashes, then forced normal Release Rebuild/deploy.
No live curio/controller is enabled and no temporary source/save remains.

## Status 2026-10-10: round180 native summons verified in isolation

- Round179 pushed. Round180 bounded summons/dynamic sprites and all1003 tests/Release pass;
  commit/push next. Complete memory still disabled pending corpse/death/reward/routing coverage.
- DD2 stopped, estate2 main/.bak and source probes restored/hash-matched. No pending save restore,
  harness, game/test/build or analysis job. Confirm normal DLL deployment hashes before committing.
- Next O3: native DD1 life-link consumer and corpse replacement/initiative audit; connect exact
  supported behavior without broadening into unsupported bosses. Reward identity/equipment follows.
- Keep owner Very Rare confirmation, open reward/retreat policies, region switch and Q04 acceptance.
  Check usage at this boundary; retain enough headroom for clean finalization before95%.

## Round181: native death links for the Necromancer's skeletons

Round180 pushed at7b306c0. DD1 loader stores life_link.base_class at MonsterClass+0xeb8; private
callback1405f69a0 matches notification identity and sets dependent flag0x1504. Unity checks linked
base-class presence in formation. DD2 ActorInstance's same-team m_DeathChainIds/CHAIN path supplies
the scoped one-anchor encounter. Register the prepared same-tier Necromancer before minion class
validation; generated skeletons link to it with no chain loot/corpse. No ordinary classes change.
Summon bridge now rejects !IsLiving as well as nonpositive HP. Multiple anchors remain unsupported.

Two cases plus all880 Core +125 UI =1005 tests/Release pass. Probe1 PID5664 logs boss SKILL death
and three skeleton CHAIN deaths (two initial, one actual summon), with skeleton views removed.
Its direct Kill did not lower boss HP; boss remained visible/acted, so this is not victory evidence.
Probe2 PID28292 instead applies lethal skill damage, but placeholder sourceId probe181 makes native
AchievementsMgr.HandleEventActorDeath dereference a missing ActorDataSkill before remaining listeners.
Stop after2 probes; full lethal kill/return acceptance remains [?]. Correct the harness to use a real
equipped skill ID when testing the next death-flow slice. Do not claim completed memory victory.

Stopped both exactPIDs/waited exit, saved separate private attempt logs/screenshots, restored estate2
main/.bak/DebugKeys/Driver after each and verified all hashes. Forced normal Release Rebuild/deploy.
No game, probe, save restore or native analysis remains. Native catalogue/evidence stays outsideGit.

## Status 2026-10-10: round181 death links built; lethal acceptance pending

- Round180 pushed. Round181 links pass all1005 tests/Release; chain death observed in isolation,
  complete lethal kill/return [?] after two probe setup failures. Commit/push before next slice.
- DD2 stopped; estate2 main/.bak/DebugKeys/Driver restored/hash-matched. No pending save restore,
  harness, game/build/test or analysis job. Confirm normal deployed DLL hashes at finalization.
- Next O3 gap: prepared skeleton corpses still borrow stand-in corpse definitions; inspect existing
  CorpseRule/Dd1Corpse before replacing data, then allow native front summons to consume eligible
  corpse ranks. Native death-flow test must use an actual equipped skill ID (achievement handler).
- Memory remains disabled. Preserve Very Rare confirmation, open reward/retreat policies, region
  switch and Q04 acceptance. Usage69% at this boundary; keep finalization headroom before95%.

## Round182: DD1 skeleton corpses and usable summon ranks

Round181 pushed atc0d2558. Skeleton death_class now registers a private corpse_A adapter:7HP,
zero skills/turns,3-round native lifetime, replaceable summon rank, cleared status containers and
no loot. No shared corpse changes. Native DD1 loader stores can_be_summon_rank at0xf91; the summon
capacity branch excludes eligible actor sizes, corroborating Unity AvailableSummonSpace. Core
AvailableRanks now uses that flag; DD2's bounded front queue removes the eligible corpse before
creating one fresh skeleton. Existing CorpseRule crit/DOT veto remains in force.

All882 Core +125 UI =1007 tests and Release pass. Native estate2 probes29684 and28876 both produce
dd3_memory_corpse_A_corpse with the slain skeleton's GUID,7/7HP, replaceable=True and dead-pose log.
Next real boss attack replaces that GUID with one new front skeleton; four living enemies total.
Three-round expiry is loaded/tested but not observed without replacement, so it remains [?].

Probe1's valid-source ApplyHealthDamage(SKILL) lowers boss HP below zero and triggers linked deaths,
but standard SKILL presentation needs an actual skill result. Direct damage leaves the boss onscreen;
do not claim fight completion. Probe2 uses native selection/target events only after validity checks,
but never finds a valid equipped damaging skill against the rank4 boss. Full lethal/victory/return
acceptance stays [?] after2 probes. Next attempt should log each skill's validity and use a reachable
boss rank. No exceptions in either native log. Initial hidden boot30128 stopped before a scene test.

Stopped both exact testPIDs/waited exit; restored estate2 main/.bak/DebugKeys/Driver after each and
verified all hashes. Forced normal Release Rebuild/deploy. Evidence private under round182.

## Status 2026-10-10: round182 corpses verified; memory still disabled

- Round181 pushed. Round182 corpse creation/replacement passes native estate2 checks, all1007 tests
  and normal Release Rebuild/deploy. Commit/push this round before another slice.
- DD2 stopped; estate2 main/.bak and probe source restored/hash-matched. No pending restore, harness,
  native analysis or game/build/test process. Confirm deployed DLL hashes during finalization.
- Full boss lethal presentation/return and unreplaced3-round corpse expiry remain [?]. Use real hero
  skill results and a reachable boss rank; direct SKILL damage is not sufficient acceptance.
- Next O3: complete the native death-flow acceptance, audit specific brains/initiative, then DD1
  reward identity/effect/equipment and live saved encounter routing. Keep the global guarantee dormant
  until every required encounter can finish. Very Rare confirmed; other reward/retreat choices open.
- Region switch and Q04 Back/reopen acceptance remain. Usage88% at this boundary; reserve headroom
  for finalization before95%. Resume from this Status, never duplicate native jobs or touch estates1/3.

## Round183: exact installed DD1 trinket identities for memory rewards

Round182 pushed atdfd4b04. Added independent runtime Dd1Trinkets reader for the base entries file,
retaining all seven native fields and read-only lists. Exact rarity queries separate very_rare,
ancestral and ancestral_shambler, without assuming DLC/duplicate/weight policy. Duplicate identity,
missing fields and unknown entry fields fail closed. No DD2 trinket relabeling or gameplay grant.

Ten cases verify installed490 identities and24/9/5 pools, no legendary rarity alias, Ancestor's
Pistol's exact buffs/limit1, Legendary Bracer's very_rare/limit0, Sacred Scroll's Vestal restriction,
and invalid/duplicate rejection. All892 Core +125 UI =1017 tests and Release pass; deployed with
DD2 stopped. No native launch needed for this dormant data reader; no saves/source probes touched.

Existing Dd1Buffs drops conditional rule fields, so it cannot by itself equip these items accurately.
The next reward adapter must retain conditions and explicitly handle combat-stat differences such
as DD1 accuracy versus DD2's engine. No effects or native equipment/grants are claimed here.

## Status 2026-10-10: round183 reward data ready; complete encounter pending

- Round182 pushed atdfd4b04; round183 pushed atfb2fca0. Installed DD1 identity reader passes1017
  tests/Release and is deployed with DD2 stopped. No game, native analysis, test/build or probe running.
- Estate2 main/.bak and both native probe source files restored/hash-matched after round182 and
  rechecked at finalization. Both deployed DLL hashes match. No pending restore; protected slots unread.
- Next O3: real hero skill lethal/victory/return acceptance with reachable boss rank and per-skill
  target-validity logging, plus unreplaced corpse expiry. Specific brains/initiative remain pending.
- Rewards: Dd1Trinkets supplies exact base identities only; implement conditional effect/equipment/art
  adapter, then saved once-only two draws/grant and complete hand interaction/return. Never expose an
  incomplete curio. Owner confirmed Very Rare; Ancestor subpool/DLC/unique and retreat rules stay open.
- Keep region switch and Q04 Back/reopen acceptance. Final usage92%; reserve the remaining headroom
  for the95% stopping rule rather than starting another native slice. Quota headroom pause until
  reported reset2026-10-10 03:56:01 UTC (1791604561); recheck ordinary availability/session at resume.
- Resume automation remains ACTIVE/hourly and now records confirmed Very Rare and current acceptance
  limitations; it follows this Status instead of repeating completed audits. No duplicate native jobs.

## Round184: first playable Faded Memory and retained DD1 rewards

Owner resumed full implementation. Added saved separate curio placement, hand-only entry, exact hero
sprite mappings, retained reward draws, native combat routing/checkpoint recovery and location-only
return. First coverage is crypts/Sprawl apprentice Necromancer; other regions/tiers remain incomplete.
Ctrl+F6 uses the same controller in estate2. Generated transparent mirror art stays in the private
save cache. Real DD1 trinket identities/art and supported conditional effects are used, with explicit
DD2 adaptations and inactive unsupported rules documented in tools/faded_memory_coverage.md.

Defaults: very_rare24/ancestral9 base pools, prefer unowned limited items then duplicate fallback;
no Shambler/DLC. Retreat spends the curio without rewards, wipe uses expedition loss. Duelist uses
Grave Robber sprites and Runaway Houndmaster sprites only; skills/stats/class stay unchanged.

All906 Core +125 UI =1031 tests and normal Release Rebuild pass. Native estate2 hand entry, temporary
sprites and real summons observed. Owner used F10: return save has one blasphemous_vial and one
ancestors_moustache_cream, stage complete, ordinary QuestComplete false. Actual hero-skill lethal
presentation, reload and conditional equipment effects remain unaccepted. No exceptions seen.
Exact test PID stopped; estate2 main/.bak and DebugKeys probe restored/hash checked; normal binaries
rebuilt/deployed. Evidence remains private at rea-workbench/evidence/round184.

## Status 2026-10-10: round184 playable slice; owner rendering defects next

- First playable memory slice implemented; do not claim global Faded Memory completion.
- Owner reports heroes/boss too large, summons good, and wrong Necromancer room. Round185 takes
  priority: isolate boss scenery from return location and correct hero/boss scale only.
- Native installed DD1 room selection and Unity reference are being checked. No named Necromancer
  wall found so far; do not mislabel a chosen generic Ruins wall as dedicated original boss art.
- DD2 stopped, estate2/source restored, normal Release deployed. Verify hashes at commit boundary.
- Next acceptance uses reachable party ranks and native skill targeting; F10 is debug-only evidence.
  Broaden regional/tier adapters afterward; keep region switch. Usage29%, ordinary available.

## Round185: separate memory boss scenery and correct relative sprite size

Round184 pushed atabdb5b9. Owner reported oversized heroes/boss and wrong room, but good summons.
Memory used return-position InRoom/Entrance and could inherit a corridor. It now always composes a
boss room in its DD1 zone. Native DD1 FUN_1407544f0 chooses plot-specific final wall then generic;
Unity LoadRoomEnvironment instead assigns a normal variation to bosses. Installed Ruins has no
dedicated Necromancer final PNG. Use crypts library as an explicit fallback, not a claimed original
unique boss asset. ZoneArt retains plot/boss/tier/family/generic final selection for other coverage.

Only memory hero/Necromancer scales change: heroes0.68, boss initially0.65. Owner requested a larger
boss after that300px check, so final boss0.85 gives417px in the second native view, summon unchanged
242px, heroes roughly250px. Sprite proportions do not alter rank footprint or combat stats.

Two native attempts: first owner F10 contaminated victory acceptance. Second private binary disables
F10, uses reachable party ranks, logs skill/target validity and delays target selection one second.
Boss HP1 fixture: Leper Chop misses, Vestal Judgement kills, linked militia dies, native victory
returns both DD1 items without completing the ordinary quest. This is actual native skill/death/
victory/return evidence, but not full-health balance. No direct damage/death shortcut used.
No exceptions found. Four art tests plus all910 Core +125 UI =1035 tests and Release pass.
Exact PIDs24652/33448 stopped/waited. Both attempts' estate2 main/.bak and DebugKeys restored and
hash checked, normal Release rebuilt/deployed; protected slots unread. Evidence outside Git.

## Status 2026-10-10: round185 rendering and normal skill return verified

- Round184 pushed atabdb5b9; round185 ready to commit/push. Memory boss/library scene, corrected
  hero sizes, enlarged boss silhouette and unchanged summons verified natively.
- Actual Judgement kill, skeleton linked death, native victory and retained two-reward return pass.
  Full-health boss balance, reload/retreat, corpse expiry and conditional equipment remain pending.
- Current live coverage still crypts/Sprawl apprentice Necromancer only; global guarantee remains
  incomplete. Broaden encounters/tiers after checking reward equipment and saved recovery.
- DD2 stopped, no probe active. Estate2/source restored/hash matched, normal Release deployed.
  Do not reopen protected picker or duplicate analysis. Usage38% at native boundary.

## Round186: Ancestor's Portrait reward grants its Resolve XP bonus

The actual ancestor pool includes ancestors_portrait with TRINKET_RESOLVEXPBONUS_B4 amount0.5,
rule always. Its tooltip advertised XP but the return path counted town buffs only. Homecoming now
accepts an equipment-buff resolver and adds supported unconditional XP effects after restoring final
native equipment slots. Carried/stashed items give no bonus; trinkets remain equipped after return.
Seven cases cover installed bonus after reload, carrying, final removal, retreat/death, town stacking
and unsupported conditional rules. All917 Core +125 UI =1042 tests and Release pass; native return
presentation remains [?]. Conditional native trinket equipment/resource checks are the next gap.

## Owner branch migration, 2026-10-10

Owner explicitly requested merging the working branch and continuing directly on main. This supersedes
older branch instructions. CLAUDE/handoff/loop now target main; resume automation is being updated too.
Fetched origin: main has one unique95ad25d hotfix, working branch200 unique commits. Preserve both
histories through a normal merge after committing round186, then verify tests/Release and push main.
Keep the old branch as history; no deletion requested. DD2 stopped and original estate2 restored.

## Status 2026-10-10: continuing directly on main after merge

- Round186 committed on the former branch at9c94de5, then merged into main with main's95ad25d hotfix
  history retained. Its renderer-rescan conflict resolves to the newer visibility-restoring/filtering
  implementation, preserving both intents. All four hotfix files match the verified working version.
- Merged source passes917 Core +125 UI =1042 tests and Release Rebuild; DD2 stopped, normal binaries
  deployed. Estate2 is restored, no temporary probe or game active. Push the merge and work on main.
- CLAUDE.md, handoff/loop and the existing hourly resume heartbeat now target main. Keep the former
  branch for history; future rounds commit/push directly to main as the owner requested.
- O3 next: native conditional trinket equipment/resource checks and saved encounter reload/retreat,
  then broaden regional/tier coverage. Actual Necromancer skill victory/return and owner rendering
  fixes are accepted; full-health balance and global guarantee remain incomplete. Portrait XP now
  applies in Core; native results presentation remains [?]. Keep region switch and protected saves.

## Round187: native DD1 reward equipment acceptance

Cold estate2 restoration retains Ancestor's Pistol and Martyr's Seal. Native inventory removal and
re-equipment give baseline HP44/speed2 versus equipped HP51/speed5. Native BuffContainer evaluation
with actual skill definitions gives melee crit0/ranged crit0.05, and fixture Death's Door gives melee
crit0.14/damage1.6. Restored HP removes the conditional effect. This verifies stat/condition evaluation,
not an additional native skill or return. Installed DD1 effects and runtime code did not change.

Native ResourceDatabaseItem lacks both custom icon entries; InventoryUiUtils.IsItemIconLoaded is
false. Mod HeroSheet already draws the correct local DD1 art. Native icon adapter remains the next
slice, scoped to concrete inventory UI methods and a cloned native prefab retaining appearance
components. Never patch shared generic resource getters. Native class-restricted equip policy is a
separate newly recorded gap; Core catalogue restrictions alone do not establish native enforcement.

Probe Release build passed. Existing baseline tests917 Core +125 UI passed before this acceptance
round. Exact test PID4776 stopped; estate2 main/.bak and DebugKeys restored with matching SHA256.
Later ordinary hall-fight transition logs native Globals.InvokeUninstallCallbacks<IMainInstaller>
KeyNotFoundException after equipment samples. Cause/reproducibility unverified, recorded separately;
do not label this probe exception-free. Evidence and probe scripts remain private in round187.

## Status 2026-10-10: round187 stats accepted; another chat owns active corridor test

- Main merge269640b is pushed. Round187 records native conditional equipment acceptance only; no
  icon runtime patch or second native attempt yet. Next O3 gap is native item icons, then saved
  memory restart/retreat and broader complete boss/region/tier adapters. Global O3 remains incomplete.
- Another owner chat, "Add corridor-ready walking models", is actively editing HeroStage, Driver,
  project files and corridor motion/tests in this same checkout. Its DD2 PID35780 is running estate2.
  Leave those changes, game and save alone. Avoid another build/deploy/native probe until it finishes
  and restores its own estate2 backup. This chat's earlier estate2/DebugKeys restoration completed.
- Normal DebugKeys source SHA256 remains3342e5b2e37c5d37c46fb35e887f332cc58238800228605741bd25de209e7ba2.
  The other chat rebuilt/deployed and launched after this round's temporary probe was removed.
- Resume automation should first check usage and other active work. Usage68%, ordinary allowed at
  this boundary; quota reset1791624236. Record only this round's documentation in its commit; preserve
  all unrelated corridor changes. Do not stop the other chat's game or claim its acceptance here.

## 2026-10-10: camera-facing corridor heroes and skeletal walk

Owner reported away-facing models and no walk, then chose to retain DD2 appearances.
HeroStage previously only bounced whole slots while native combat/road idle played.
Added corridor-only motion on the existing separate stage actors. Native anatomical
axes and mesh bind matrices determine yaw, hip spacing and flat ankle orientations;
no shared mesh/controller or combat actor is modified. Driver supplies actual signed
hallway speed. Two-bone leg IK, continuous foot-contact/swing trajectories, phased
ranks, unscaled timing and start/stop/reverse blending replace slot bouncing.

Owner saw the first live slice and reported a leg-running feel under rigid busts.
Reduced stride 0.64->0.46 leg lengths, lift 0.15->0.08, and lengthened cycle
1.05->1.28 seconds. Added supporting-leg weight shift, pelvis compression/rotation,
chest counter-motion and head stabilization. Weapon arms follow the torso together
to retain two-handed grips. Per-frame bone restoration prevents accumulated offsets.
Final supporting-leg sway sign reviewed after the revised capture; other motion is
as captured. This is an adaptation of native meshes, not newly generated hero art.

Installed UnityPy inspection found compatible leg/torso suffixes in all 12 base
heroes plus Crusader, Duelist and human Abomination. Native estate2 captures show
Vestal, Flagellant, Leper and Jester, forward/back walking, stopping and saved hall
reload. No stage animation exceptions. First capture's reverse-input attempt was
blocked when ChatGPT took foreground; keys released. Owner explicitly authorized
the short follow-up, and revised capture completed. Other heroes/garments/art modes
still require visual checks. In-place gait does not claim exact ground-speed match
to the existing fast scenery scroll. See tools/corridor_heroes.md.

Six production-source gait tests and all917 Core +131 UI tests passed; final Release
build deployed with no DD2 process, plugin hashes match. Initial test PID35780 had
already ended when stopping it; revised exact PID14016 stopped/waited. Original
estate_2.json and .bak copied back and independently SHA256-verified; protected
slots not read/changed by this workflow. Native footage, logs and backups remain
outside Git at C:\Users\Piral\.universal-modder\inspection\corridor-walk-20261010;
corridor-walk.mp4 is the revised owner-review preview. No pending save restore.

## Round188: DD1 reward icons through native inventory methods

Dd1TrinketIcons patches only the four concrete InventoryUiUtils icon/load methods. Known custom
items use installed inv_trinket+ID.png, consistent with Unity WagonSlot. A separate cached clone of
DD2's default prefab retains VariableAppearanceBhv and pool support; native resources/shared prefabs
remain untouched. Missing local art and unrelated items fall through. Template loading is asynchronous.

First native estate2 probe PID35496 verifies definition/instance overload equality, loaded=True,
appearance0/1, pooled recycle/re-spawn and unchanged default sprite IDs for Pistol/Seal. Canvas was
covered by mod IMGUI, so no native-panel visual acceptance. Second PID2944 fails its private harness
at FindObjectOfType<UiRoot>() before drawing; likely DontSave lookup exclusion, not established as a
production icon failure. Two-attempt limit reached. Keep full native inventory visual/interaction [?].

All917 Core +131 UI tests pass; parallel test builds initially collided on the shared Core output,
then sequential rerun passed. Final normal Release Rebuild/deploy passes; both deployed DLL hashes
match. Both exact PIDs stopped/waited; estate2 main/.bak and DebugKeys restored/SHA256 checked.
Evidence and raw native data remain private in rea-workbench/evidence/round188 and round188b.

## Status 2026-10-10: owner explicitly paused; round188 ready for later resume

- Owner said "pause for now" after the round188 checks. Stop the iteration loop and pause the resume
  automation. Do not resume on quota reset or a stale heartbeat without a later owner instruction.
- Main includes corridor work fb55eca, preserved here. Round188 adds native DD1 icon loading/pooling;
  full native panel screenshot/interaction remains [?]. Next harness should use the retained UiRoot
  reference or include DontSave objects, rather than FindObjectOfType's default lookup.
- No game or probe remains active, estate2/source restored/hash checked, normal Release deployed with
  matching DLL hashes. Commit/push only this completed slice and handoff, then remain paused.
- Later O3 candidates: native class-restricted equip validation, saved memory restart/retreat, then
  complete boss/region/tier expansion. O6 follows O3. Global guarantee/coverage remains incomplete.

## Round189: approved expedition-selector artwork

Owner approved the lighter, clearer expedition-selector v02 on 2026-10-10, retained the paired region
switches, and explicitly requested reusable prompts. Exact original/readability prompts and the art
direction are recorded in tools/art-prompts and tools/expedition_selector_design.md. This is scoped
selector work; the separate parity-loop pause and Mountain gameplay proposals remain in force.

Route: retain IMGUI and the DD1 quest/provisioning rules, draw real controls over a private ImageGen
painting, and register local feathered overlays for DD1 destinations and Sluice. Runtime uses PNG,
1920x1080 canvas, bilinear sampling and soft alpha; previews are 512x256. No game assets enter Git.
Original DD1 background/layout remain the fallback when the optional private map is unavailable.
Native verification pending. Use estate2 only, with backup/restore and no normal estate picker.

## Owner decisions, 2026-10-10

Owner answered four open [user] questions; recorded in PARITY.md and CLAUDE.md. Direction only;
nothing is scheduled or built by this entry.
- Party-wipe trinkets: DD1's direction, recovered through the crow (bird) plot quest. A custom DD2
  version of that quest comes later, when the owner says the time is right.
- Afflictions: DD1 afflictions/virtues replace DD2 meltdown, except the Flagellant keeps DD2's Toxic
  overstress (DD2 hero_flg `overstresses,toxic`). Audit and design the stress-scale mapping first.
- DLC: DD1 and DD2 DLC hero classes are in scope, deferred until 3D models and related work are
  possible. Other DD1 DLC content and the game modes remain open [user] questions.
- Mountain (Darkest Dungeon) quests: heroes who finish one may return. Implement later with O4.

## Round190: fights start in place (owner main focus: feel and performance)

Owner: a battle played DD2's battle start with a loading; start it DD1's way, in the current corridor or
room. Native trace (Player.log + DD2 decomp GameModeMgr.ChangeModeAndNotify/ExitMode/EnterInNewMode):
our SetMode(COMBAT, FADE_IN_AND_OUT) faded DRIVING to black, destroyed pools, ran MemoryCleanup
(Resources.UnloadUnusedAssets ~84 ms, then GC.Collect), loaded combat_arena_*, entered COMBAT, faded in,
then CombatPresentationBhv.RunIntroTimeline played m_DefaultIntroTimeline (camera, battle-start stamp).
The "combat" scene itself stays loaded from the first DRIVING entry. WaitForCombatStart also preloads
combat_results each fight. DD2 regions fought in native arenas unlike the painted corridor; our IMGUI
cover then crossfaded from the corridor into a fading-in, different arena. Throbber: ScreenFaderBhv only
opens it when fading to black with showThrobber, so the override already kept it hidden.

Change (Look.FightStartsInPlace, default on; off restores the old path):
- Dd2Combat.Start uses SceneTransition.SKIP; the cover hides DD2's switch. Exit/enter timings logged.
- NoDd2BattleIntroInPlace ends the intro timeline exactly like DD2's battle_skip_intro pref (reflection:
  the build has no DirectorModule reference) and still sets the battle modifier icon.
- UiRoot hands Dd1Backdrop a screen-sized snapshot of CrawlUi.DrawScene (scene only, no heroes/HUD) on
  the fight's first repaint. Compose draws its virtual 0..720 strip (FightBackdropLayout.SceneStrip,
  letterbox-aware, 5 UI tests) with the floor at the heroes' feet. DD2 regions now use it too; Faded
  Memory boss rooms keep their own composition. No snapshot within 2 s keeps DD2's arena.
- The cover crossfades with the snapshot itself (the old black fill + scene at partial alpha darkened
  the midpoint). After the fight, StraightBackToTheDungeon fades the scene back in over 0.3 s, then
  Dd2Combat.Tick switches to DRIVING with SKIP under it.

IMGUI into a RenderTexture: activate the screen-sized RT, GL.PushMatrix + LoadPixelMatrix(0,W,H,0),
re-apply GUI.matrix, draw, restore. The result is upright on this D3D11 build (verified in game).

Native verification, estate2 only (backup + SHA256, temporary F2 EnterHamlet(2) entry so the picker
never read protected slots, then source/save restored and hash-matched, forced Release rebuild deployed,
DLL hashes match). Resumed Foetor expedition: hall fight plague_eater_mash_204 entered 733 ms, visible
967 ms; room fight plague_eater_mash_206 entered 667 ms, visible 883 ms (before: 1134-1433 ms entered,
plus fade-in and intro). Both show the same corridor/room behind the fight, no black, no intro; DD2's
combat UI and turns work. Return: back in the dungeon 601 ms after the fight ended, no black. No
exceptions in LogOutput.log or Player.log. Exact PIDs 10016 and 38548 stopped. Recordings and frames
are private in C:\Users\Piral\.universal-modder\inspection\round190. All 917 Core + 136 UI pass.

Follow-ups recorded in PARITY: backdrop slightly darker/warmer than the crawl (DD2 grading on the quad);
heroes jump to DD2 combat positions in the crossfade; ~0.9 s still hold (MemoryCleanup, arena load,
combat_results preload); DD1-region fights now use the snapshot too, unverified [?].

## Status 2026-10-10: round190 in-place fights verified; feel/performance is the main focus

- Owner's new main focus: overall feel and performance. Round190 (in-place fight start and return) is
  verified in game for DD2 regions and committed. The parity loop stays paused; the owner directs work.
- Owner decisions recorded 2026-10-10 (CLAUDE.md, PARITY): crow-quest trinket recovery with a later DD2
  version, DD1 afflictions except the Flagellant's Toxic, DLC classes later, Mountain veterans may return.
- Round189 selector art is committed but not yet seen in game ([?]).
- Next candidates for feel/performance: cut the remaining ~0.9 s fight-start hold (skip per-fight
  MemoryCleanup for crawl fights with a safe periodic cleanup, skip the combat_results preload when
  SkipDd2ResultsView is on), match backdrop brightness, smooth the heroes' move into combat positions,
  check DD1-region in-place fights. Ask the owner which issue they feel next.
- No game running; estate2 restored and hash-checked; normal Release deployed with matching hashes.

## Round191: in-place fights feel the same (owner: fluidity and coherent terrain/brightness)

Owner asked for the three round190 follow-ups, stressing fluidity into battle and terrain/brightness
coherence. Recorded frames compared crawl vs fight on background pixels: lifted darks, darker mids, green
pulled down (DD2 grading). Also found the backdrop sat ~50 px higher than the crawl (aligned to DD2 feet).

Look: DD2's custom LUT ScriptableRendererFeature has a static LUT.LutPassEnabled (like DDFog); it is off
for in-place fights and restored after. URP Tonemapping joins the held effects for them. Post exposure is
read from VolumeManager.instance.stack ColorAdjustments and undone on the backdrop material (2^-EV; 0 EV
in the Foetor, camera HDR). The snapshot is drawn at its crawl position (SceneShift 0) with 1.002
overscan. Arena-scene renderers on the Characters layer that are not actors are hidden in place: the
left black post was farm_fence/signpost/grass tufts. Result: sky, houses and wall unchanged in game.

Feel: FightTransition (UI-tested) times the start: heroes turn to the enemy for 0.35 s from the start,
glide for 0.3 s once DD2 is ready (backdrop ready and CombatBhv.IsBattleRunning), then 0.15 s crossfade;
the return is 0.15 s cover then a 0.3 s glide back, then DRIVING with SKIP. HeroStage renders during the
cover; CorridorHeroMotion.Turn slerps the slot facing from (1,0,-0.48) to (1,0,-0.18). Targets come
from bones both rigs share (ROOTSHJnt, Head_TopSHJnt, l/r_Leg_AnkleSHJnt): FightTransition.Land puts the
stage slot so pelvis, lower ankle and height match DD2's. The first try used SkinnedMeshRenderer bounds,
which don't follow the pose: heroes shrank in the glide (owner saw it). A capped clock (max 1/30 s per
frame) replaced wall time after frames showed a ~200 ms stall at load collapsing the glide into 2 frames.

Load: GameModeMgr.MemoryCleanup (UnloadUnusedAssets + GC.Collect) is skipped on DRIVING->COMBAT for fights
in place; the return still runs it under the cover. Setup poll 0.1 -> 0.03 s. Entered 527-633 ms (was
627-733), ready 760-843 ms (was 883-967). Tried skipping the combat_results preload: the battle then
never ended (owner: "battle isn't finishing, locked"). CombatPresentationBhv ends a battle only via
RegisteredCombatResultsPresentation, which that scene registers. Reverted; recorded in PARITY.

Native estate2 checks (backup + SHA256, temporary F2 entry, no picker): three hall-fight runs in the
Foetor. Battle ends and returns 752-769 ms after the last blow, no black; heroes land within a few px
at DD2's size. LogOutput.log shows only DD2's known IMainInstaller teardown exception when the game was
closed. Estate2 and DebugKeys.cs restored and hash-matched, clean Release rebuild deployed with matching
hashes. Recordings private in C:\Users\Piral\.universal-modder\inspection\round191. 917 Core + 147 UI.

## Status 2026-10-10: round191 in-place fights match the corridor

- Fights in place keep terrain and colours, heroes turn and glide onto DD2's places, returns mirror it.
  Committed and verified in the Foetor (DD2 region). DD1-region in-place fights remain [?].
- Remaining visible change: the heroes' pose and lighting in the last 0.15 s crossfade (PARITY).
  Remaining time: ~0.5 s DD2 arena load and actor spawn stall, now hidden behind the heroes' turn.
- No game running; estate2 restored and hash-checked; normal Release deployed with matching hashes.
- Owner's main focus stays feel and performance; the parity loop stays paused. Ask what they feel next.

## Round192: provisioner Shift+click, corpse rule at Kill, scouting to the room

Owner requests while playing (2026-10-10):
- Provisioner: Shift+left-click buys up to a full stack, Shift+right-click returns the last stack (Core
  Inventory.ToFullStack/LastStack, bounded by shelf stock and pack room; 3 Core cases).
- Corpses after crit/bleed/blight kills stayed. CorpseRule only recorded the hit in ApplyHealthDamage, but a
  lethal skill hit never goes there: SkillCalculation.ApplyActorResults kills IsDamageKill targets directly
  with ActorInstance.Kill(DeathType.SKILL...). CombatPresentationBhv also re-asks GetIsDeathClassValid
  (DeathType.SKILL) when it plays the death, after any per-call memory is gone. Now RememberLethalCrits
  reads the skill's ActorResults (IsDamageKill && IsCrit by target), DecideAtDeath prefixes the main Kill
  overload (DOT from its SourceType) and calls DD2's SetDeathClassIgnored, which both checks honour.
- Scouting: owner rule, a reached branch is shown to its end with its room; secret doors stay
  budget-bound. Scouting tests updated to the rule; the SecretBranch case still requires reach.
- Quests: owner says they are not coherent with DD1 (explore 90% of rooms, all battle rooms). Core goals,
  selector text and quest curio placement already follow quest.types.json; asked the owner for examples.

Owner's own DD2 session (PID 26780) was running: an earlier build in this round deployed over the
plugin while it ran (BepInEx had not locked it; the running game kept its loaded build). That broke the
"deploy only while DD2 is stopped" rule; later builds used -p:Deploy=false. Corpse and scouting builds
are not deployed yet: deploy when the game is closed. 920 Core + 147 UI tests pass.

## Round193: combat colours for walking heroes and fights in place

Owner requested the battle appearance/palette for the walk, reported that the blue tint remained and
also appeared in battle, then confirmed "blue tint gone" after the native combat preset was applied.
They found that first warm pass faded. The final contrast pass is awaiting their visual check.

Private material inspection found the cause: Red Hook/Lit/Hero stores blue `_ShadowColour`
(0.463,0.714,0.831) in the prefab and its material block. Its `_RealLight` defaults off; scene ambient
and light-layer experiments did not correct the appearance. All those experiments and the narrower
walking angle were removed. HeroStage already starts the native combat idle via ActorBhv.Show; its
existing camera-facing skeletal walk remains the animation layer.

HeroCombatPalette loads the installed MaterialPropertySettings_Combat_Arena_Farm preset, matching
the owner's Foetor battle reference. The catalog's Assets/... path is an InternalId, not a valid load
key: resolve it once through ResourceLocators, cache the location, then load that location. Native
AddTarget/RemoveTarget with distinct sources keeps the overrides local to presentation actors;
shared materials/assets are not edited. HeroStage applies it after actors load; Dd1Backdrop applies
it to its cached hero actors only. Block identities detect replaced art; caches/handles are cleared
on party changes, presentation destruction and fight end. No repeated catalog or scene scans.

The native Farm preset has brown shadow (0.474,0.334,0.255), brightness1.5, grey tint0.89 at opacity1
and intensity1.6. Both views omit DD2's final region grading. The final pass retains the warm preset
but overrides brightness1.25, tint intensity1.0 and saturation1.10 to reduce the washed appearance.
These last three values have not been reviewed in game. Release +920 Core +147 UI tests pass.

Native checks used a detached copy of the earlier estate2 walk fixture, with persistence blocked in
a temporary helper. Protected estates were not accessed. The game log recorded warm materials on
all four stage actors and all four fight heroes; owner confirmed removal of the blue cast. The new
selector's full layout and the Sprawl/Ruins artwork switch with the correct live quest were also
inspected. Remaining selector interactions stay [?]. Private captures/material inspection are under
C:\Users\Piral\.universal-modder\inspection\selector-overhaul-20261010. The helper DLL was moved out
of BepInEx. Both estate2 files are hash-checked against the fresh run-2 backups; no restore needed.
The exact selector prompts remain in tools/art-prompts and tools/expedition_selector_design.md.

## Status 2026-10-10: round193 ready for owner colour review

- Warm hero materials verified in corridor and battle; owner confirmed no blue tint. Final brightness/
  tint/saturation adjustment built, native visual review pending. Pose change in the battle crossfade
  remains separate work in PARITY.
- Approved selector renders in game; Sprawl/Ruins art and quest selection checked, full interaction
  review pending. Prompt memory is saved in the repo.
- Temporary test helper removed. Estate2 files unchanged/hash-matched; protected estates untouched.
- Owner first asked to wait before launching, then explicitly authorized a launch when the work is
  done. Finish checks/deploy while stopped and open the normal game once, without the test helper.
  Further test launches should follow the owner's current instructions. The parity loop stays paused.

## Round194: correct the final walking image against the battle appearance

Owner still reported blue heroes and asked to use the battle model as the walk's
base while preserving the finished gait and angle. HeroStage already creates
the native battle actor with no starting-state override and calls ActorBhv.Show;
CorridorHeroMotion layers the accepted skeletal walk on that combat idle. No new
mesh, rig, animation controller or angle change is needed.

The previous successful preset-application log was insufficient evidence for
colour. A native corridor PNG reproduces blue neutral cloth while its material
block already has the warm Farm shadow colour. A private read-only image check
of the test Leper's hood gives RGB75.29/78.04/84.48 and fails the neutral criterion.
The first broad material check also counted enemy Hero-shader renderers; restrict
that check to the actual party before using it. Native ambient and fake-light
colour experiments did not visibly change the cast; none enter product code.
An unmistakable red material tint did change the rendered image, establishing
the working seam for a corridor colour balance.

HeroCombatPalette now accepts a corridor flag. Only the stage instances add
`_ColorTintColor=(0.96,0.90,0.74)` at the existing presentation override priority.
Fight actors keep their current native combat tint, avoiding a second warm pass.
The existing handle/override cleanup also removes the new tint. No shared asset
edits, new scene scans, per-frame allocations or global rendering patches.

Native test covers the four fixture heroes, idle and the existing walk, plus a
scripted in-place battle. Final selected tint was tested through the same native
material override path; neutral-hood pixels become RGB81.65/75.16/67.97 and pass.
Captured walking and battle views were inspected. Full subjective colour matching
and the other classes remain for owner review. Release, 920 Core and 147 UI tests
pass. No meaningful Core test seam exists for rendered colour; the native PNG
check, not a duplicated constants test, is the regression oracle for this fixture.

Private evidence, test helper and check scripts are under
C:\Users\Piral\.universal-modder\inspection\hero-palette-20261010. The owner
authorized restarting the open game for this test. It used a detached estate2
fixture with persistence blocked. Human input entered fights during the first
captures; the final isolated run freezes navigation and drives the gait in place.
Protected estates were not read. Fresh estate2 files are backed up in that folder.

## Status 2026-10-10: round194 walking colour balance

- Native battle models remain the walking base, with the accepted gait and angle.
  Corridor-only material colour balance verified in native captures; owner review
  of the exact appearance and other hero classes remains pending.
- Release deployed while stopped; installed DLL hash matches the build. Private
  helper removed. Both estate2 files match this round's fresh backup, so no restore
  was needed. Normal game reopened for owner review, PID13220, without the helper.
- Selector prompt memory remains in tools/art-prompts and tools/expedition_selector_design.md.
  Selector interaction checks and battle crossfade pose matching remain as recorded
  in PARITY. The parity loop stays paused.

## Round195: Leper grip, combat colour handoff and connected walking posture (2026-10-10)

Owner accepted the corrected walking colours, then reported Leper's sword sliding
out of his hands and a yellow tint on combat entry. Follow-up review requested
less bent knees, then better correlation between the upper body and the stride.

Native rig inspection reproduces the sword bug: Sword_AuxSHJnt is under the pelvis,
while both wrists follow the chest. The old procedural layer changes their relative
pose by up to 8.78 bone-local units and 8.68 degrees. Capture the native sword pose
relative to the right wrist each frame and carry it through the torso motion; save
and restore its original local transform before the next Animator evaluation.
Both wrists pass 180-frame native comparisons after the change: maximum drift
0.059 units, angular drift zero (the remaining positional noise is at the far-away
stage's float precision). No rig reparenting or shared animation changes.

The walk previously compressed a battle crouch further. It now raises that pose
toward 93% of leg length with smaller compression. Native Leper planted-knee flexion
is 31.4–57.6 degrees. Torso rise/sway is centred on foot support, with the high point
over each planted foot and the low point in double support. Hip yaw follows foot
separation; reduced chest counter-rotation keeps the shoulders moving with the body.
Two production-source posture tests fail with the old crouch/bob and pass after the
change. The accepted cadence, stride, lift and camera angle remain.

Native transition capture reproduces yellow neutral cloth while the corridor tint
remains active under the newly loaded arena environment. The existing tint override
is now mutated to the neutral combat colour when the actual ambient-probe/shadow
lighting differs from the road reference. HeroStage updates it before material
blocks' LateUpdate. Using art-scene load/unload alone was too early and introduced
a brief return mismatch; final entry/return captures follow the lighting itself.
The private read-only hood check fails before (RGB45.07/42.93/31.90, R/B1.413) and
passes after (RGB60.42/59.84/54.57, R/B1.107). The native renderer and frame captures
remain the colour/grip regression seam; there is no faithful offline GPU/rig seam
in the UI shims. Fight actors keep their current combat palette.

Release, 920 Core and 149 UI tests pass (1069 total). The four-hero walk, combat entry
and a debug-forced return were captured and inspected. Final subjective gait feel
and other classes remain for owner review. Evidence/helper/checks are private under
C:\Users\Piral\.universal-modder\inspection\hero-grip-transition-20261010.
The detached fixture blocks persistence. Protected estates were not read; both
estate2 files match their fresh backups (3C260376... and BB8068C0...), so no restore
was needed. The temporary helper was removed and the deployed DLL matches the build.
The parity loop remains paused.

## Round196: slightly stronger upper-body walking motion (2026-10-10)

Owner requested a little more upper-body movement and explicitly no game launch.
Reduce spine/chest counter-rotation so net torso yaw increases from 1.5 to 2.25
degrees and sway from 1 to 1.5 degrees, on the existing support/stride curves.
Head counter-yaw follows the new total to keep the gaze steady. Hip/leg motion,
knee posture, timing, wrist-relative sword grip and colour handoff are unchanged.
Release build and the eight gait tests pass; deployed while DD2 is stopped.
No native run this round, per the owner's instruction. Visual review remains pending.

## Round197: manual corridor entry from room clicks and map zoom (2026-10-10)

Owner requested removal of automatic corridor walking after selecting the next room,
plus map zoom. CrawlUi now dispatches room clicks to Driver.SelectMapRoom. It cancels
queued walking and selects only the first exit toward the destination through the
existing fade. In a corridor it leaves the party in place for held keyboard/mouse
movement. A discovered secret room can still be entered at its door; distant secret
clicks give manual approach guidance. Explicit hall-square travel and F3 developer
room routing retain their existing behavior. Map navigation methods are grouped in
Driver.MapNavigation.cs so tests run the production selection and route state.

The map accepts wheel input inside its enabled panel, bounded to 50–200%, anchored
under the pointer. Room/hall icons, markers, indicator, tooltips and click geometry
share the scale. Panning stays in screen pixels. Moving the party recenters without
changing zoom; a new map resets to 100%. Wheel input during a press suppresses the
release click so zoom cannot accidentally select a room.

Nineteen new cases cover manual room/secret selection, cancellation, event blocking,
zoom anchoring/bounds, scaled hover/clicks, pan/recenter/reset, disabled/outside input
and press-plus-wheel cancellation. All920 Core +168 UI tests and Release pass.
The tests replace Unity's fade/audio wrapper, not the production navigation methods;
native IMGUI capture, rendering and movement timing still need a game check.
Deployed while DD2 is stopped; plugin/Core DLL hashes match the build. No game launch
or campaign-save access this round, per the owner's instruction.

## Status 2026-10-10: round197 ready, game left closed

Round197 built and deployed. Native review pending: select the next room, wait idle
after the fade, then hold D/A or the corridor edges to walk; wheel-zoom and drag the
map, check tooltips/clicks at both limits and enter/return through a secret door.
Round196 upper-body animation still awaits owner visual review. No running game,
temporary probe or save restore pending. The parity loop remains paused.

## Round198: register the owner's combined DLC source and compare versions (2026-10-10)

Owner supplied C:\Users\Piral\darkestwithdlc\game with DD1 and DD2 DLC files,
then asked whether using it would break the existing reverse-engineering bases.
Read-only inventory finds DD1 data plus folders 1117860_arena_mp, 445700_musketeer,
4964110_fires_edge, 580100_crimson_court, 702540_shieldbreaker and
735730_color_of_madness. Fire's Edge contains Duelist and Runaway hero folders.
DD2 StreamingAssets/Excel includes dlc_dul_cru, dlc_catacombs, dlc_origin_skins and
dlc_supporter data. File presence does not establish runtime DLC ownership/loading.

The DD2 executable, UnityPlayer.dll and all164 managed DLLs match the current modded
install by SHA256, with no added/missing DLLs. IronCrown also matches C:\Users\Piral\dd2-decomp\refs:
DD97E30F2F37E2A287633AB322B8FD18BCFE9E240903713A5C2AE00C894692E6.
The Unity runtime remains 2022.3.62. The new DD1 shared/rules.json,
scripts/map_generator.darkest, campaign/quest/quest.types.json,
campaign/quest/quest.plot_quests.json and trinkets/base.entries.trinkets.json
match the Steam source exactly. This is sampled data compatibility, not a claim
that every DD1 DLC rule matches the existing native code map. The svn_revision.txt
bytes differ and neither file is a readable version string.

Existing C:\Users\Piral\dd2-decomp and D:\dd1-decomp stay intact. The DD1 native
manifest still identifies its original executable as 4d78fbfa...; DLC-specific rules
can extend the evidence without replacing that baseline. The new folder has no
BepInEx loader yet. No active build/runtime paths or mod settings were changed,
and no game was launched or campaign save accessed. No code/test changes this round.

## Status 2026-10-10: DLC folder inventoried, active paths unchanged

Round197 remains deployed in C:\Users\Piral\DarkestDungeon3\game, with round196/197
native review pending. The owner has not yet chosen whether the new DLC folder
becomes the active install; their response asked about preserving reverse-engineering
work. DD2's checked binaries match and DD1's sampled core data match, so the existing
references can remain the baseline. If the new folder is selected, configure local
build/deployment, the loader, DD1 source and private art paths together. Preserve the
old install and both research workspaces. No game, probe or restore pending; loop paused.

## Round199: activate the DLC install while preserving existing references (2026-10-10)

After the owner accepted the compatibility assessment, made
C:\Users\Piral\darkestwithdlc\game the active install. Copied Doorstop/BepInEx core,
config and the current DarkestDungeon3 plugin, plus PrivateScenery and
PrivateExpeditionArt. Empty probe folders, old caches and logs were excluded.
The original install and research workspaces were retained without edits.

Directory.Build.local.props sets GameDir to the new folder and remains ignored.
MSBuild confirms compile references still use C:\Users\Piral\dd2-decomp\refs,
while deployment targets the new BepInEx/plugins/DarkestDungeon3. The copied mod
config sets DarkestDungeon1Folder to the combined root and both art paths to their
new local copies. All other settings match the old config. Five playtest helpers
now use the new executable/log/working-directory paths; handoff, machine and
current DD2 inspection-path notes are updated. The native DD1 code baseline and
Steam-based default tests remain unchanged. No new DLC gameplay was implemented.

Verification: all90 copied loader/plugin/art files match SHA256. Release builds
and deploys successfully, and plugin/Core DLL hashes match the build. All five
changed shell helpers pass bash -n without being executed. A private offline audit
uses the production Dd1Install, Dd1Campaign, CrawlContent and Dd1HeroArt readers
against the explicit new root, with no fallback: campaign/crawl content loads,
and Vestal, Flagellant, Leper, Jester, Musketeer, Shieldbreaker, Duelist and Runaway
art definitions resolve. Audit source is private under
C:\Users\Piral\.universal-modder\inspection\dlc-install-20261010\InstallAudit.
No gameplay code changed; no game launch, protected-save access or save writes.

## Status 2026-10-10: DLC install active, game left closed

Build/deploy and future launch helpers now target C:\Users\Piral\darkestwithdlc\game.
DD1 runtime data comes from the same combined folder. Current mod/settings/art
are installed and checked offline. The old game and both reverse-engineering
workspaces remain intact. Native loader startup, DLC availability and round196/197
visual/input checks await a permitted launch. No running game, probe or restore
pending. DLC implementation deferrals and the paused parity loop are unchanged.

## Round200: DD2 destination cards as a quest select preview (2026-10-10)

Owner request (via the other agent's session, which ran out of tokens before writing code): try DD2's
destination choice instead of the DD1 quest map, with an option to swap between the two like the region
switches. Design as agreed there: Mountain above four visible regional cards, paired-area arrows, compact
quest rows, the roster and party on the same screen, DD2 art drawn in our IMGUI (no inn scene).

DD2 facts (decomp): the inn's route screen is SubScreenBiomeChoiceBhv spawning BiomeChoiceBhv cards
(280 px wide, the END/Mountain card 800). A card shows the biome painting and frame, its name, the biome
goal with its reward and the modifier ("biome_mutator_<id>") with a tooltip; hover scales 1.1 and lifts a
grey veil, sounds AudioPathsBhv.InnBiomeHoverLeft / InnBiomeSelect. InnPresentationBhv loads the
paintings from BiomeData.GetChoiceSprite()/GetChoiceFrameSprite() (AssetReferenceSprite), so they load
without the inn. ResourceDatabaseBiomeData is registered by PostMainMenuSystemsInstaller; RegionCardArt
uses it when it is up, else loads BiomeData under the owned DLC labels (as the database does). Biomes:
City/Farm/Forest/Coast/Cave = dd2_city/farm/forest/coast/cave; MountainBrain..MountainBody (END).

Built: EmbarkUi.Destinations (cards, Mountain banner, quest tooltip, party line-up, menu switch),
DestinationLayout (geometry, paging, cover crop; 12 UI tests), RegionCardArt (own Addressables handles,
released on Back/embark/switch), DrawQuestMedal shared with the DD1 map, PrepareAreas and
DrawEmbarkControls shared by both quest selects. Switch: BepInEx Look/Dd2DestinationMenu (default off),
toggled by a row under the areas in the Hamlet's Regions panel and a top-left link on both quest screens.
The DD1 map path is unchanged. The frame sprites are not drawn yet (their shape is unseen).

920 Core + 180 UI tests pass; built and deployed with DD2 closed (plugin hash 02068171...). Not yet seen
in game: whether the database or the label path supplies the paintings in the Hamlet, the Mountain art,
crop anchors, and the hero line-up's look.

## Round201: DD2's real route screen instead of redrawn cards (2026-10-10)

Owner, after seeing round 200 in game: "this is not what i imagined: can you take the exact dd2 inn region
selection UI". Owner choices: party selection is a separate step after the pick; I may close and relaunch
the test game.

Round 200 in game (estate 2, before the pivot): DD2 biome data came from the DLC-label path in the Hamlet
("[destinations] DD2 biome data loaded: 15 regions"; ResourceDatabaseBiomeData was not up); the redrawn
cards, Mountain banner, hover lift, party line-up, quest details and the Regions switch row all worked.

DD2 facts (catalog + decomp): the inn UI prefabs are standalone addressables whose primary key is the asset
path: Assets/Prefabs/UI/Inn/inn_sub_screen_select_route.prefab (SubScreenBiomeChoiceBhv, holds
m_biomeChoicePrefab), Assets/Prefabs/UI/Inn/biome_choice_slot.prefab (BiomeChoiceBhv),
inn_sub_screen_collection.prefab (the inn's tab collection), innkeeper portraits
Assets/Art/UI/Portraits/npc_innkeeper0N.png. ScreenStackBhv.PushScreen(prefab, Layer.Inn) instantiates
under the Inn layer and calls OnScreenPushed after Start; OnFinishedClosing pops and destroys it. A
standalone SubScreenElementBhv closes itself (ToggleVisibility/CloseSubscreen set Closing when it has no
collection); right-click closes it. SubScreenBiomeChoiceBhv.OnScreenPushed and BiomeChoiceBhv.Init read
InnBhv / InnPresentationBhv / ResourceDatabaseBiomeData, absent in the Hamlet.

Built: Dd2/RouteScreen pushes the prefab; a prefix on SubScreenBiomeChoiceBhv.OnScreenPushed (ours only)
sets push params and Opening, spawns the cards and fills each like BiomeChoiceBhv.Init (DataContext
biome_sprite/biome_frame/biome_label, mutator_name = quest lines, biome_goal, tooltips, LayoutElement 800
for the Mountain); card selection is our own callback; area names go through a Localization prefix for
"dd3_area_*" keys. It logs the screen and first card hierarchy once ("[route] screen:" / "[route] card:").
UiRoot no longer blocks DD2's input while the screen is active. The redrawn cards and their layout code
are gone (PictureCrop keeps the painting crop, 3 tests). If the screen fails, RouteScreen.Broken falls back
to the DD1 map for the session (source only so far).

Slips: the native build was deployed while the owner's own DD2 (PID 37972, started 16:51) was running;
the running game keeps its loaded build. That deployed build still has the temporary F2 test key; the
source is restored (DebugKeys.cs 3342e5b2...) and the clean build with the fallback must be deployed once
DD2 is closed. Estate 2 was restored after the round-200 check (7d849523... / 03e27189...). Owner is
restarting DD2 to look at the native screen.

## Round202: DD2 destination menu removed; the DD1 quest select stays (2026-10-10)

Owner: "ok you can remove that we will keep the dd1 design". Removed everything rounds 200-201 added:
RouteScreen, RegionCardArt, EmbarkUi.Destinations, PictureCrop/DestinationLayout and their tests, the
Look/Dd2DestinationMenu setting and its Regions-panel row, the switch link, the UiRoot input exception and the
EmbarkUi/HamletUi refactors. src/, tests/ and tools/expedition_selector_design.md are identical to fa46205
(before round 200). The DD2 facts in rounds 200-201 remain for reference.

Never seen in game: DD2's real route screen (round 201). The owner had been launching the old copy,
C:\Users\Piral\DarkestDungeon3\game (its plugin is from 15:38, before rounds 200-201), so neither the switch
nor the screen appeared there. The active install is C:\Users\Piral\darkestwithdlc\game; the owner was told
to launch its exe or repoint the shortcut. The old copy was left untouched.

The DLC install's config still holds the orphaned Dd2DestinationMenu = true entry (BepInEx keeps unknown
entries; harmless). Deploy the removal build once DD2 (PID 40252, launched for the owner) is closed.

## Round203: the DD2 DLC Crusader and a permanent Bounty Hunter at the stagecoach (2026-10-10)

Owner: "can you introduce back from dd1 the shieldbreaker and from dd2 dlc the crusader", then "i will iterate
later on the spells, also enable permanant hiring from stagecoach of the bounty hunter from dd2". Owner choices
for the Shieldbreaker: DD1 2D art over a hidden DD2 stand-in, DD1 skills translated.

Facts: the Hamlet runs before DD2's ActorDataClass library exists, so Dd2Catalog always used its fallback list
of the 11 base classes. DD2's Crusader (dlc_dul_cru, hero_cru_data_export) is ActorDataClass `crusader`, the
same id as DD1's; the Bounty Hunter (expedition/hero_bh_data_export) has m_StartingRosterStatusType hire and
m_SpawnLootIds trinket_bounty_hunter_hire, a loot table rolled into his trinket inventory by every new
ActorInstance (ActorInstance ~2579). DD1's Shieldbreaker lives in <dd1>/dlc/702540_shieldbreaker/heroes/
shieldbreaker (info/art darkest, anim, A-D skins, ability icons).

Built: Core RecruitClasses (base 11 + bounty_hunter + crusader when owned; 3 tests incl. Reynauld as a
Crusader with the DLC and the Man-at-Arms without). Dd2Catalog uses it (logs "[stagecoach] classes: ...");
ActorResources.Has(classId) reads the owned ResourceActor locations. Dd2Heroes.Apply clears spawned trinkets
before adding the record's. Ctrl+F4 (test estate only) puts a Bounty Hunter and a Crusader on the stagecoach.

Verified on estate 2 (DLC install, PID 2476): class list includes bounty_hunter and crusader; Cardonell
(Bounty Hunter) and Neot (Crusader) hired with DD2 portraits, embarked (Ruins), DD2 models in the corridor,
the Crusader's DD2 skills and DD1 crusader weapon/armour in the panel, a DD1 spider fight in native combat
ending with spoils. The provisioner's Shift+click bought full stacks (12 food, 8 torches). The owner was also
at the game (four F10 presses that were not ours), so driving stopped there; the spawn-trinket clearing was not
observed. Estate 2 restored from this round's backup (c2758d23... / 496b2dfb...), temporary F2 removed, clean
build deployed with matching hashes. 923 Core + 168 UI tests pass.

Next: the Shieldbreaker (PARITY).

## Round204: Shieldbreaker 3D model and animation prototype (2026-10-10)

Owner: Shieldbreaker has been added; make her a 3D model and animations, possibly by remodeling an existing
hero to her DD1 design. Then: "don't launch the game, you can start implementing". No DD2 launch or save
access in this round. The existing uncommitted Shieldbreaker class/skill integration is included as a
dependency of this presentation work, with its DD1 art retained as fallback.

Existing class integration: RecruitClasses includes Shieldbreaker when her installed DD1 kit exists;
Dd1HeroKit reads her info/art, equipment, ranks and seven skills. Dd1HeroClasses clones Hellion's resource
and creates separate native class/stats/skill data, portraits and icons. DLC effect/buff folders are read
after base data. Dd2Heroes registers the class before party creation; Driver prepares DD1 hero sprites in
ordinary fights as well as memories. Ctrl+F4's test-estate recruit helper includes her. Native skills,
recruitment and persistence were not observed here; six actual-data Core cases pass.

Model: locally authored body, costume, turban, veil, visible eye opening, spear and round shield, fitted
to Hellion's existing native rig. Donor boot detail remains. Actual DD1 in-game anatomy is spear in the
right hand, missing left hand and shield strapped to the left forearm; promotional art can be mirrored.
The pack has 20,922 split vertices, 6,974 triangles, 130 native bones and 86 named mesh parts. Native
bone order and all bind matrices remain exact. Two 4096x2048 painted atlases use DD2's existing shader.
The current geometry is an early art pass, with proportions, cloth and material detail still to refine.

Runtime: ShieldbreakerModel attaches only to a true Shieldbreaker ActorBhv after its presentation loads.
It replaces that instance's body mesh/material and hides the donor glaive, preserving DD2's material
property/palette system and the existing camera angle and corridor gait. Spear vertices have weight 1 on
the right wrist, shield vertices weight 1 on the left elbow. Chest-relative two-bone arm solves provide
an idle loop, one shared offensive thrust and a shield raise for Serpent's Sway or an incoming target
presentation. Native animation still supplies the lower body and death. Prior local rotations restore
before the next Animator evaluation. Generated meshes/materials/textures are owned and released, with
textures shared between her stage/combat instances. JSON parsing runs on a worker; validated geometry
and PNG decode/upload stay on the Unity thread.

DD2 fact: _global/MaterialPropertyBhv.RefreshRenderers clears m_materialInstancesCache but retains
m_materialSharedCache. ClearRenderer later restores that cached shared baseline. Update the changed
renderer entry as well as refreshing renderer caches, or actor disable/reenable can restore Hellion's
texture on the new mesh. Release restores the original mesh, materials and local bounds. No global
lighting or shared hero asset edits. Native material lifecycle still needs a game check.

Look/Shieldbreaker3D defaults true. Paths/HeroModelFolder defaults to <active game>/PrivateHeroModels.
Missing, oversized or incompatible model/texture files retain DD1 art; disabling the option and restarting
restores the same fallback. Faded Memory encounters retain their DD1 sprites. HeroStage/CrawlUi handle
individual missing models, use body height excluding weapons for the model's stage fit, and request an
actual DD1 combat figure for flat fallback instead of stretching the roster portrait.

Reproducible pipeline and acceptance steps: tools/shieldbreaker/README.md. build_model.py extracts the
owned Hellion rig into a private folder, model_blender.py authors the fitted mesh and exports its pack,
ModelAudit reads that pack through the production Core reader and samples the actual CorridorWalkCycle,
and preview_walk.py renders those samples in Blender. Stills read back the written mesh. Blender's bone
edit basis differs from the original bind rotations; preserve each rig's own rest basis for preview wrist
rotation. Blender needs --python-exit-code 1 to propagate a Python failure.

Verified offline: Release build, 944 Core +168 UI tests, model audit (exact bind matrices and bone order,
rigid weapon weights, bounded PNGs, attack/defence ending at idle), three exported pose stills and a
24-frame walk preview. New Core coverage rejects invalid model/clip data and checks loop continuity.
Private assets/scene/previews: C:\Users\Piral\.universal-modder\inspection\shieldbreaker-3d-20261010\model.
The Blender preview approximates the runtime pose solver; it does not verify DD2's rendering or timeline.

Deployed while DD2 was stopped to C:\Users\Piral\darkestwithdlc\game. Only shieldbreaker.json,
shieldbreaker_base.png and shieldbreaker_ink.png copied into PrivateHeroModels; all three SHA256 match.
Deployed plugin/Core hashes match the Release outputs (E4C28BEC... / DDB1F37C...). Prior four plugin
binaries and config backed up at C:\Users\Piral\.universal-modder\backups\shieldbreaker-3d-20261010-190602.
Generated meshes, textures, donor dumps, Blender files and previews remain outside Git.

## Status 2026-10-10: round204 installed, game left closed

- Shieldbreaker's first 3D mesh and idle/walk/thrust/defence prototype is built, checked offline and deployed.
- Native actor binding, final palette/scale, combat timing, return from combat, camp framing and DD1 fallback
  remain [?]. Use tools/shieldbreaker/README.md for estate-2 acceptance when launching is authorized.
- Art still needs refinement; offensive skills currently share a thrust. No claim of finished DD2 hero quality.
- No game running, no saves read or modified, and no pending save restore. Owner explicitly forbids a launch
  for this implementation. Preserve that constraint until the owner changes it.
- The approved DD1 expedition selector remains. Do not resume the removed round200-201 menu.

## Round205: Shieldbreaker costume rebuilt against her DD1 design (2026-10-10)

Owner showed the first POC and asked to make it look like Shieldbreaker. The first mesh's brown tunic,
narrow trousers, plain wooden shield and cap did not match her base outfit. No game launch or save access.

Reference: the owned Shieldbreaker DLC combat atlas, guild header and portrait. ModelAudit --reference
assembles her actual combat pose with the production Core Spine reader/raster. The revised mesh has
cropped gold chest wraps, exposed brown midriff, full gathered gold trousers, green sash and spear ties,
a cream headcloth around a dark helmet, yellow face veil, left shoulder/knee/shin armour, hip tassets,
and an olive shield with silver crescents and her green serpent emblem. The spear stays in her right
hand; the shield stays strapped to her left forearm, with no left hand. Shaped anatomy, gripping fingers,
folded cloth and a convex shield replace the initial blockout. Donor boot geometry remains.

Pipeline: reference_parts.py unwraps the owner's native paint into private tiles; design_mesh.py owns
costume geometry and projections separately from model_blender.py's native export/rig. Rotated atlas
regions need clockwise 90-degree unrotation, checked against the assembled Spine pose. Painted panels
follow source alpha at their edges. Trousers retain gold paint and small ink creases while broad empty
fields from the bent source pose are filled before projection. Export preserves smooth corner normals;
the roundtrip preview reads them back. Native shader, runtime code, bind poses and clips are unchanged.

Verified offline: 30,480 split vertices, 10,160 triangles, 130 native bones and 117 named parts. The
production reader audit passes exact native bone order/bind matrices, rigid wrist/forearm weapon weights,
4096x2048 texture bounds and attack/defence recovery to idle. Three exported pose stills and a 24-frame
walk were rendered and inspected. Release build and 944 Core tests pass. UI code is unchanged; its
168 tests passed in round204. No native lighting, actor binding or combat timeline check was made.

Private assets, editable Blender scene and review images:
C:\Users\Piral\.universal-modder\inspection\shieldbreaker-refine-20261010\model.
preview_outputs.py produces comparison.png, pose/walk sheets and walk.gif from actual mesh renders.
The offline preview approximates the pose solver and uses Blender lighting; it is not DD2 footage.
The mesh remains an early art pass, with individual skill animation and further art polish pending.
Rebuild recipe and native acceptance steps: tools/shieldbreaker/README.md.

Installed while DD2 was stopped. Previous three private model files, four plugin binaries and config
backed up at C:\Users\Piral\.universal-modder\backups\shieldbreaker-design-20261010-194743.
All three installed assets match SHA256 (mesh 57148171..., base 4CD29E7E..., ink 1E8668F0...). Release
deploy's four binaries match their outputs (plugin E75D1D5B..., Core 948CDF0A...). Only scripts and notes
enter Git; derived meshes, textures, extracted reference art and Blender/previews remain private.

## Status 2026-10-10: round205 installed, game left closed

- The revised Shieldbreaker design is installed and checked through exported pose/walk previews.
- Native palette, scale, model lifecycle, combat timing and fallback remain [?]. Use the README's
  estate-2 acceptance steps only when the owner authorizes launching again.
- No saves read or modified, no pending restore, and no game launched. Preserve the no-launch constraint.
- The approved DD1 expedition selector remains; do not resume the removed round200-201 menu.

### Owner-authorized launch after round205

Owner: "you can launch the game". Launched C:\Users\Piral\darkestwithdlc\game\Darkest Dungeon II.exe
at 20:08 local time, PID 52396. This supersedes the no-launch instruction above. No game input or estate
selection was sent; Shieldbreaker's native appearance and timing still need review.

### Shieldbreaker added for the owner's test

Owner asked to add her to a roster. Estate 2's two save files were backed up first at
C:\Users\Piral\.universal-modder\backups\shieldbreaker-roster-20261010-201207. Ctrl+F4's existing
slot-2-only helper offered Shieldbreaker Hachet, plus the missing Bounty Hunter and Crusader. The native
roster and saved estate then confirmed Hachet (1de29d310d3b, shieldbreaker, resolve 0), with all six prior
heroes retained. The test roster now has nine heroes, including the Crusader and Bounty Hunter. These
additions are intentionally kept for the owner's test; do not restore this backup automatically.

The owner continued interacting with the game and embarked; agent input stopped. Runtime logged
`[shieldbreaker-3d] 7: 30480 vertices, native rig, idle/attack/defend ready` and her corridor gait ready.
This verifies native binding of the revised pack, not final visual quality or combat skill timing.
No restart, deployment or protected-save file access was performed for the roster request.

## Round206: Shieldbreaker remodeled from an actual DD2 hero body (2026-10-10)

Owner showed the model glowing and losing its painted fronts in the corridor, and asked to
use another hero as the base. Compared private native Hellion, Grave Robber and Runaway
mesh renders. Retained Hellion because her body and existing 130-bone presentation are
suitable for this costume. This pass uses the actual native torso, arms, face, right hand,
cloth topology and boots; the preceding pass mostly used a native skeleton with newly
constructed anatomy. There is no native Shieldbreaker model in the active DD2 asset set.

native_body.py selects native surfaces by topology, bone influence, bounds and UV region.
Hair, feathers, bone jewellery, long skirt and the left hand are removed. Native leg cloth
is widened and recoloured gold; the pelvis is clipped from the native skirt with interpolated
UVs, normals, tangents, colours and normalized skin weights. A gathered underwrap closes
the open-front skirt under the belt. Chest wraps follow the donor torso, the headcloth fits
the native head, and the native right hand replaces the authored fingers. Her original
DD1 veil, helmet, serpent shield, left-side armour, sash and spear remain fitted over it.

Two rendering defects were found in the old pack. The exporter reversed authored triangles,
although their cross products already agreed with native outward normals. The old pack
fails the new ModelAudit winding check at 10,120 /10,160 reversed triangles; the corrected
pack has six imperfect native triangles /10,168. Do not reverse native import, authored
export or roundtrip preview faces. The runtime also reduced the renderer's material array
to one material, dropping its separate native outline pass. It now preserves the array and
replaces only the painted body material. Native smooth-outline tangents and vertex colours
are extracted, validated, exported and uploaded. Older packs may omit these optional channels.

The private atlas preserves native ink and recolours native fabric/warpaint/midriff, while
DD1 painted tiles supply costume details. Authored sRGB paint values are converted to Blender's
linear image buffer before PNG encoding; painted ink tiles now carry dark creases instead
of all-white shading. These changes are local to Shieldbreaker's isolated actor. No global
light, shared hero asset, gameplay or save migration changes. Offensive skills still share
one thrust; further art polish and individual skill animations remain future work.

Verified: Release build; 949 Core and 168 UI tests; production-reader model audit with exact
native bone order and bind matrices, optional-channel validation, rigid wrist/forearm weapons,
4096x2048 texture bounds and clip recovery; three exported pose stills and a 24-frame walk.
Final pack: 30,504 split vertices, 10,168 triangles, 97 parts, 130 bones, body height 192.51256.
The costume comparison and final walk sheet were inspected. Blender is an offline preview
and does not reproduce the native DD2 shader or combat timeline.

Private assets/scenes/derived donor dumps/previews remain outside Git:
C:\Users\Piral\.universal-modder\inspection\shieldbreaker-donor-20261010\model.
Only scripts, Core/runtime changes, tests and documentation enter the repository.
Backup before installation, including the three previous private assets, four plugin binaries,
config and only estate 2's two save files, with SHA256 verification:
C:\Users\Piral\.universal-modder\backups\shieldbreaker-native-20261010-203725.
Installed while DD2 was stopped. All three asset hashes match the private outputs:
mesh 878DBB18..., base 21339E6F..., ink 4B873D0D.... All four deployed DLL/PDB hashes
match Release outputs (plugin 138342AE..., Core 539D89B2...).

Reopened the active install with standing owner permission for interactive review. The initial
hidden launch had no visible window and was replaced by an interactive launch before entering
any estate. No game input or estate selection was sent in this round. The normal estate picker
reads all three slots; avoid it during agent testing and use an isolated estate-2 entry instead.
No protected saves were read/modified. Hachet's existing roster addition is intentionally retained.
Final launch PID 50196 has a visible, responding Darkest Dungeon II window at the main menu;
startup logs confirm the updated plugin loaded. Read-only window capture saved privately as
shieldbreaker-donor-20261010/game-open.png. Both estate-2 files still match their backup hashes.

## Status 2026-10-10: round206 installed, game reopened for owner review

- Shieldbreaker uses an actual native DD2 body remodeled into her costume; export, outline and
  palette defects are corrected and offline rig/pose/walk checks pass. Private pack installed.
- Native visual appearance, grip through animation, final scale/material lifecycle, individual
  skill timing and fallback remain [?]. Use tools/shieldbreaker/README.md for acceptance steps.
- DD2 is running for the owner. No estate selected or game input sent by the agent this round.
  No save changes or pending restore; the pre-install estate-2 backup is precautionary.
- Estate 2 retains Shieldbreaker Hachet for testing. Do not restore the earlier pre-recruit backup.
- The approved DD1 expedition selector remains; do not resume the removed round200-201 menu.

## Round207: the Shieldbreaker's combat mechanics in DD2 terms (2026-10-10)

Owner, while another agent remeshes her: "you can get current context and git state to implement her combat
mecanics"; she was not launched. Another agent committed round 203's uncommitted Shieldbreaker integration with
its model work (3a28467); its in-progress model files were left untouched.

DD2 facts: skills ignore tokens through token_ignores (TokenIgnore til_ignore_block_buff = block+block_plus,
til_ignore_guard, til_ignore_stealth) and cap uses per battle with m_Limit (hel_adrenaline_rush has 2, like
Serpent Sway). Effects exist for add_1_daze (Token daze: consume delay_turn, inverse of speed), add_1/2/3_block,
remove_all_guard, remove_all_stealth, add_1_vulnerable.

Built (Core, general rules): SkillShape carries IgnoreProtection/IgnoreGuard/IgnoreStealth/PerBattleLimit;
Dd1HeroKit reads them from a hero's combat_skill; SkillText adds the token ignores (kept with the base skill's)
and DD1's limit (the base skill's stays when DD1 has none). Map: negative speed buffs -> add_1_daze, crits
received -> add_1_vulnerable, unstealth -> remove_all_stealth, clearguarded -> remove_all_guard,
health_damage_blocks N -> add_N_block (max 3). DD1 monsters with these effects benefit too. Dd1HeroClasses
logs her DD1 effects with no DD2 counterpart (Block Guard, SB Dmg Marked). Tests: base effect mapping and real
DD2 ids, token ignores and limits in skill text, her flags and every skill's effects (954 Core pass).

## Round208: an exported Dark Souls III boss in a DD2 fight (2026-10-10)

Owner: "can you put C:\Users\Piral\ds3-extract\soul_of_cinder\ as a fightable boss in the game, for now just try to
put his 3D model etc so it fits the game and pressing a FKEY to launch his battle, goal is to see if we can use these
kind of exports in the game", then "you can even place him like that (his exported model) if the model can render".

The export (the owner's own DS3 install, personal use, README in the folder): glTF 2.0 with external .bin, 15 meshes
(five overlapping weapon forms, #01# shown in game), 54,468 triangles, a 123-bone DS3 biped skin (Pelvis, Spine..Spine2,
L/R_Clavicle..Hand, Thigh..Toe0, twists, fingers, mant cape bones), T-pose, Y-up facing +Z, ~3.1 m; PBR textures as PNG
(base, mr, normal, em), alphaMode MASK; no animations (Havok), cloth or particles.

DD2 donor: lost_battalion_knight (Excel lost_battalion_knight_data_export: size 2, tags large/knight, 36 HP; mesh
msh_lost_battalion_knight with a 184-bone rig and a sword). DD2's character shader takes _Base and _Ink textures
(ShieldbreakerModel). A later step could rebind the DS3 mesh to the knight's rig so DD2's animations move it.

Built: Core GltfModel (accessors of float/ushort/ubyte/uint, nodes TRS, skin, materials, external images kept inside
the model folder) + 3 tests on the real export. Plugin ExportedBoss (MonoBehaviour, debug keys only): F1 ->
Driver.DebugExportedBossFight (hall fight, plan.Enemies = [lost_battalion_knight], no checkpoint resume, no DD1 sprite
view) -> on combat, once the knight's renderers have bounds: build nodes/bones and one SkinnedMeshRenderer per mesh
(bindposes from the glTF), materials cloned from the knight's body material, place/scale/face, parent to the knight,
forceRenderingOff on the knight's renderers; cleared when combat ends. Paths/ExportedBossModel defaults to
<game>/PrivateBossModels/soul_of_cinder/soul_of_cinder.gltf; gltf, bin and the base/em PNGs (13 MB) were copied there.
957 Core + 168 UI pass; not deployed or launched yet (the owner and another agent are using the game).

In game (estate 2, owner: "You test it"; DD2 closed between builds, estate 2 backed up c. 2f230b7f/dd18f310 and
restored after): F1 started the hall fight; the export bound in a few frames ("[exported boss] soul_of_cinder.gltf
stands in for lost_battalion_knight: 11 meshes, 54468 triangles, 123 bones, 3.12 m x1.151 (knight 3.00), knight
material Red Hook/Lit/Hero"; the hero shader's textures are _Base and _Ink). Iterations:
- White vertex colours made it pale: DD2 hero meshes carry (1,0,0) almost everywhere (Hellion donor dump: R mean 0.8,
  G=B=0), now used.
- DD2's _Base is an ordinary dark albedo (tex_hellion_col) and _Ink black lines on white (tex_hellion_ink); the DS3
  base maps average 66-87/255, so the x2.2 gain washed it white: now x0.85 plus the dim ember maps.
- Ink from the raw albedo speckled (DS3 grain); now the darkest 12% of a box-blurred brightness (radius width/256).
- Facing the party showed a profile: now three-quarters toward the camera; the T-pose sword pointed at the heroes:
  now down in front (rotating the hand onto the blade tip measured from the #01# mesh).
- The enemy takes the export's name ("Soul Of Cinder"). F10 needed three presses (the knight's cadaver phase), then
  the fight returned in place with spoils and the model was cleared. No exceptions.
The shared working tree also built the other agent's uncommitted Shieldbreaker files into these test builds.

## Round209: the exported boss moves with DD2's animations (2026-10-10, /loop iteration 1)

Owner (/loop): "improve the model of soul of cinder until he renders well within the game as a boss sizewise,
animationwise etc"; another agent is extracting Gael and Vilhelm under ds3-extract (context only).

Knight rig (UnityPy dump to the private inspection folder exported-boss-20261010/knight_rig.json): 184 bones,
root TrajectorySHJnt, ROOTSHJnt (pelvis), Spine_01/02/Top, Neck_01_01, Head_Top, l/r_Arm_Clavicle/Shoulder/
Elbow/Wrist, Finger_01-04_01-03, Thumb_01_01-03, l/r_Leg_Hip/Knee/Ankle/Ball/Toe, cloth/coat/grass aux bones,
Sword_AuxSHJnt; one material on msh_lost_battalion_knight_b.

Retargeting: first try used the knight's bindposes as its rest: the export came out upside down (the bind
space is not the renderer's as assumed). Working method: at bind, size by chain length (L_Foot..Head), turn to
the knight's hips line, set pelvis on pelvis, bend each mapped export bone (parents first) so it points where
the knight's bone does now; record both poses; then every LateUpdate (DefaultExecutionOrder 32000) each export
bone turns as its knight bone has turned since (rotation delta in the actor's frame), pelvis follows ROOTSHJnt.
Seen: idle stance, a hit reaction in DD2's close-up shot, and the knight's sword attack lunging at Hachet.

Look: Size 1.05 looked hero-sized; 1.35 now. MASK materials (mantle 67% transparent, BD02 28%) rendered solid and
pale: 297 cut-out triangles dropped, 1297 two-sided triangles doubled, transparent texels painted dark (16,12,10).
Ink from normal-map gradients (top 9%, one pixel wider) instead of blurred dark areas. Colour gain 1.0.
961 Core tests pass. Estate 2 restored (2f230b7f/dd18f310). Next: armour speckle, waist cloth flap, maybe a
little larger, then the death animation and multiple-fight cleanup.

## Round210: exported boss polish (2026-10-10, /loop iteration 2)

Ink: the export's normal maps carry fine surface grain, so gradient ink speckled the armour; a 3x3 smoothing
first and the top 6% of turns (was 9%) give plate lines; the waist cloth now reads as a dark flap. Size 1.5x the
knight's chain length: towers over the party with the head clear of the HUD, and stays framed in DD2's close-up
attack shots (seen in Cardonell's Collect Bounty). Death: the first F10 triggers the knight's Armor Break phase
(raised-sword stance, followed), the second kills it; the fight returns in place at once, so no death animation
shows (true of every enemy with the in-place return). A second F1 fight after the spoils builds the model again
from cached textures; nothing is left behind. Estate 2 restored (2f230b7f/dd18f310); deployed build matches
(65452369). The loop ends here: size, animation and look are in place within the stand-in approach.

## Round211: the exported boss plays its own DS3 moves (2026-10-11)

Owner: wants the boss animated with "his (or some) animations" instead of the T-pose; chose the plan "decode his own
DS3 animations, preview in Blender, play them in DD2 with the knight still running the fight".

Export side (ds3-extract, private): c5280.anibnd holds 117 hkaSplineCompressedAnimation clips (Havok 2014; 112
non-additive) on a 105-bone Havok skeleton. Its hierarchy differs from the FLVER's (Root between Master and
Pelvis/Spine, cape chain parented); every skinned bone matches the FLVER bind to 1e-3, so the animated export takes
Havok's parents/rest pose. Clips play in place (root motion is a separate reference frame). make_moveset.py picks
clips per knight move and splits each at the sword tip's fastest stretch (strike = peak -0.12/+0.45 s), writing
`<model>.moveset.json` beside a lean glTF with only the 14 clips used. Also fixed in the exports: triangle winding
was inverted (the Z mirror already flips it), so earlier copies rendered inside out.

Knight states (UnityPy, read-only: faction_lost_battalion_assets_all.bundle, lost_battalion_knight_animation_controller):
idle_A/idle_B (9.3 s), attackslash/downswing/attackdown/riposte _antic/_action/_recover (antic up to 5.3 s, held),
downswing_idle and buff_idle (held between antic and action), buff_antic/action/impact/recover,
impact_action/impact_recover (hit), deathsdoor_antic/deathsdoor_idle (Armor Break).

Built: Core GltfModel reads animations (LINEAR/STEP channels, mirrored like nodes) and samples them (clamped,
shortest-way normalised rotations); Core ExportedMoveset maps a donor clip name to (move, part) and resolves
(export clip, time, weapon form): wind-up plays from 0 and holds, the strike starts at the wind-up's end when DD2's
action begins, recovery runs to the clip's end, the stance loops, unknown moves fall back to it; deathsdoor plays the
kneel and switches to phase 2 (a005 moves, #05# greatsword). ExportedBoss: with animations and a moveset, every
weapon form of the phases is built and only the phase's shown, feet go on the knight's TrajectorySHJnt (carries its
lunges), and each LateUpdate samples the clip with a 0.15 s cross-fade between states; each state change is logged
("[exported boss] <knight clip> -> <export clip> (<weapon>)"). Without them the Round209 rig following still runs.
Tests: 6 new Core cases (state names, timing, stance, Death's Door phase change, sampling, the private export's
clips/moveset); 973 Core pass; Release builds with -p:Deploy=false. Not deployed or seen in game yet: DD2 was open
at the campaign menu with the owner at the PC. The game copy (<game>/PrivateBossModels/soul_of_cinder) still holds the
pre-fix static export; replace it with ds3-extract/soul_of_cinder/dd2/* (gltf, bin, moveset, textures) while DD2 is
stopped.

## Round212: native campaign entry, estate management and torch loading (2026-10-11)

Owner request: replace the old IMGUI main-menu buttons with a coherent DD2 entry into an actual DD1 campaign,
then align trinkets/quest HUDs. Refinement: create more estates and delete them, choose an estate before loading,
replace the progress bar with a burning torch, and remove the rigid modal look. Applied the owner's pasted
dd2-native-kit skill using installed native art/fonts; no extracted assets copied into Git or the plugin.

CampaignMenu clones DD2's Kingdoms banner but removes localization and all persistent start/submit callbacks.
The added Hamlet row ignores the native horizontal layout, sits above Confessions, and joins its explicit
navigation. The front end uses TMP's loaded NDDunkelD-Bold/AlegreyaSans fonts, ink banners, ivory/brass/red text,
a scrolling estate list and separate details/actions. Main menu input/canvas state is restored on Back. The
Mountain's world-space title sprite (Darkest_Dungeon_Horizontal_TItle_Sprite) is hidden/restored while the estate
screen is up, so it does not cross estate details. Naming and delete confirmation have their own full screens.

EstateSlots discovers positive numeric estate IDs beyond3, including backup-only saves, without parsing files.
The picker then reads summaries only when the player opens it. New estate IDs follow the highest occupied ID;
creation names the new estate, refuses an occupied slot and must persist successfully before continuing.
Delete confirmation removes the recovery copy and then the primary; a selected runtime save is cleared to avoid
resurrection through later persistence. Corrupt slots remain visible, cannot be entered and can be explicitly
deleted. Tests only use checked temporary directories, never the installed estate folder.

Session's worker now also prepares DD2 data tables and the town layout, publishes with Volatile, and can retry
failure. Choosing an estate requests town PNGs, idle town renders, exact selected upgrades/sky and owned native
hero resources. PNG decoding remains one upload/update; Spine raster work stays on workers. Completion counts
come from real uploads/resource callbacks, including completed fallback art, rather than elapsed time. The
animated native orange torch material and ui_profile_torch sprite are loaded through Addressables and released
with the UI. No bar/percentage or fixed loading duration. Retry/cancel remain available. The existing
Driver.EnterHamlet recovery/resume/new-estate cinematic path handles the actual entry. Old Road's artificial
three-second delay is removed; its existing loading art now covers the actual DD2 host handoff.

Native checks at1600x900: real button callback opened the torch, loaded12 PNGs/18 town renders/13 owned heroes,
and cancel/reopen reached ready. Native animated flame visually verified. Synthetic estate banners inspected;
the subsequent world-title removal/row ink alignment is compiled/deployed but not yet visually inspected.
Private evidence: local-art/ui-overhaul/campaign-torch-v01.png and campaign-estates-v04.png. No protected slots
were opened by the agent. The owner clicked a synthetic preview and reported infinite torch loading: this was
the intentional read-only handoff guard after assets completed in2.85s. Relaunched normally without either
debug flag, and changed the preview's terminal text to "Asset check complete" so a held test cannot masquerade
as loading. Never leave -dd3-menu-check or -dd3-menu-preview enabled for ordinary play.

Release build/deploy passes with3 existing warnings. All979 Core+175 UI tests pass; six added Core cases cover
slot discovery beyond3, no parsing/writes during discovery, safe creation paths, deleting primary/backup and
backup-only saves, invalid IDs and clearing an active selection. Seven UI readiness cases verify each pending
resource group, failure/recovery and immediate completion without a fake timer. Initial parallel test builds
contended on Core's obj cache; rerunning Core sequentially passed. Tests did not access campaign saves.
Unverified: live name entry/delete controls, lists beyond one viewport, selected upgraded-town loading, and
saved-expedition resume. These are marked [?]; native asset completion is verified separately. After the normal
relaunch the owner's session passed the gate in2.88s, rendered the Hamlet's hover assets and the approved quest
map, then reached native combat. A read-only main-menu screenshot also exposed the copied Kingdoms tooltip and
blue Button.colors; removed the tooltip component and replaced the cloned button colour blocks with warm
ivory/brass/red states. This last cosmetic fix was built/deployed after DD2 closed; native hover acceptance remains.
The gate also counts images already in the synchronous cache (a direct debug entry may have primed them),
waits for the selected-estate queue to be installed, and times out a stalled data worker as well as art uploads.

## Status 2026-10-11: round212 deployed; normal DD2 launch restored

DD2 PID3596 was launched normally at1600x900, without QA flags. The owner's session reached the campaign and
combat, then DD2 closed. The final normal build, including tooltip/colour cleanup, was deployed while it was
stopped; the game is left closed. Do not open the ordinary picker in an automated test: it reads protected
estates1 and3. Create/delete acceptance can be checked by the owner, or through an isolated estate2 test after its usual
backup. No actual saves were read/modified by agent tools. Pre-change plugin backup is
C:\Users\Piral\.universal-modder\backups\dd3-main-menu-before-20261011\20261011-062238.zip.
Existing dirty Shieldbreaker model/tools/tests were preserved outside this UI change. Following UI work is the
trinket HUD and quest-selection cohesion; preserve the owner's approved map/region-switch layout.

## Round213 (2026-10-11): expedition HUD, DD1 composition with native DD2 type and controls

Owner requested the quest/expedition overlays while another agent handles the Hamlet. Used the pasted
dd2-native-kit against the installed game; no extracted fonts, textures or sprites enter Git. After the owner
asked for more DD1 placement, restored its inset quest scroll, progression plates, quest medallions, right-edge
roster, party plate and provisioner/shopkeeper composition. The approved private map, regional overlays and
paired switches retain their existing positions. Native NDDunkelD-Bold/AlegreyaSans, node icons, banner buttons,
portrait frames, selection marks and dividers supply the DD2 finish with ivory/brass/red states. Regional names
have a thin near-black outline and a small drop shadow, as requested for lighter terrain. Button labels stay
fixed; readiness and supply warnings are separate text. Objectives fit their available space and retain full tips.

Addressables discovery: some installed fonts and node assets expose GUID keys only. The loader first tries
the path, then indexes resource locations by InternalId once per asset type. Native logs confirm both fonts and
all five node resources loaded. Missing private map art retains DD1 terrain; missing native art keeps controls
usable. Party right-click previously indexed front-to-back storage using the back-to-front screen cell; it now
opens the displayed hero, covered by seven linked-source cases for full, partial and empty parties.

Added opt-in -dd3-expedition-preview, gated by debug-key configuration and native main-menu/idle state. It
creates a transient campaign with no estate path, refuses to replace an existing save selection, suppresses
Persist only for that owned object and blocks expedition launch. Back to Hamlet clears it and closes the
preview. Used only this fixture for native checks; no campaign saves or protected estates were opened.

Native1600x900 checks: approved map/quest dossier and outlined names; Sprawl/Ruins switch with changed local
artwork and live quest selection; provisioner; Shift-click food12 plus one torch (cost975, gold12000 unchanged);
food moved to another pack slot; right-click torch return (cost900); Back/reopen cleared all staged supplies and
cost while retaining the quest/party; party click removal and roster drag restored4/4; right-click Baldwin's
displayed cell opened Baldwin's sheet. Native screenshots remain private under
local-art/ui-overhaul/expedition-native-hud (final-selector.png, provisions-v04.png, purchases-v04.png,
pack-return-v04.png, pack-settled-v04.png, cancel-reopen-v04.png, party-inspect-v04.png and party-drop-v04.png).
Raw label-region comparisons before/after the drag matched exactly; image-preview artifacts were not treated
as a runtime font defect. No native asset failure or expedition UI exception appeared in the preview log.

Release build/deploy passes; all979 Core and182 UI tests pass. Core was checked before the final cosmetic
revision; UI and Release were rerun afterward. Remaining native acceptance: busy quest-row pagination, every
paired/extra area, unavailable-hero restrictions, all supply drag/return variants and real campaign embark with
the refreshed controls. This preview deliberately cannot verify actual embark. Existing dirty Shieldbreaker
work remains outside this commit; the Hamlet agent's scope is unchanged.

## Status 2026-10-11: round213 deployed; ordinary launch restored

The unsaved preview was closed by exact owned PID60712. DD2 PID59072 was launched at1600x900 without QA
flags for ordinary owner play. Do not open the estate picker automatically: protected saves remain off limits.
The deployed HUD includes the DD1 placement refinement and outlined regional headings. Next presentation scope
is the trinket HUD; retain the approved map hubs, inline region arrows and regional quest rows. Plugin backup:
C:\Users\Piral\DarkestDungeon3\local-art\ui-overhaul\expedition-native-hud\plugin-before\DarkestDungeon3.dll.

## Research 2026-10-11: owner idea for a perspective 3D Hamlet

Read-only feasibility check while another agent owns Hamlet presentation. The private kit has real Hoarder,
Field Hospital and three inn-level model assemblies, including merchant/nurse and surrounding props. Inspected
their exported geometry metadata and three-quarter reference renders. Installed TriggerHospitalBhv/TriggerStoreBhv
expect TileNodeBhv, driving UI and native encounter inventory, so reuse visuals with Hamlet-owned interactions
instead of enabling the road scripts. HamletUi.Open already maps building IDs to DD1 town panels. A dedicated
perspective stage is feasible in principle; renderer/material/fog dependencies, camera angles, NPC animation,
missing buildings and repeated loading/cleanup still need an isolated runtime proof. This is an owner proposal,
not authorization to replace the other agent's work. Details, source paths and official Unity references:
tools/hamlet_3d_feasibility.md. No code, game process, private assets or campaign saves changed.

## Round214 (2026-10-11): implement approved private Hamlet hillside v05

Owner explicitly requested implementation of the completed Hamlet art. Retained the recognizable DD1 hillside,
the DD2 native model/texture renders and painted abbey/guild/forge/asylum/coach/shop/camp cutouts. Installed only
the clean scene; the mock UI and labelled art are review images. All derived art remains outside Git/releases.
`tools/prepare_hamlet_art.py` packages the approved plate and ten cropped visible masks to the active game's
`PrivateHamletArt`. `Paths.HamletArtFolder` selects the private pack; empty/missing/invalid packs retain DD1.
Workflow/manifest details and limits are in `tools/hamlet_art.md`.

Session reads the manifest and CPU masks on its data worker. The existing torch PNG queue also warms all21
private textures (plate plus10 tint/outline pairs), one upload per frame. Native logs:33 PNGs/18 DD1 fallback
renders/13 heroes ready in4.72s on the visual preview and4.88s on the interaction check. Validation requires all
ten service IDs, matching mask dimensions and canvas, files within the private pack and valid native texture
sizes; a partial pack cannot leave mismatched scenery/hotspots. Picking uses top-down visible alpha and reverse
depth, including transparent holes. Quick-nav, roster/footer, modals, trinket overlay and heirloom exchange own
their covered pixels. Locked buildings dim and keep the original unlock dispatch; services/rules are unchanged.
Estate title and hover labels reuse the installed DD2 fonts/banner sprites already used by the expedition HUD.
Embark moves into the bottom bar so it no longer covers the Nomad Wagon/memorial. Main menu/quest HUD work is
preserved; the existing Shieldbreaker source/tools/test changes are outside this round's commit.

Added explicit `-dd3-hamlet-preview` fixture: transient campaign, no estate path selected, persistence suppressed
only for the owned object, real game embark blocked by the existing expedition preview. An additional
`-dd3-hamlet-check` dispatches IMGUI events inside that overlay, without OS input/focusing the owner's desktop.
Native1600x900 logs verified visible-mask clicks and Escape closes for camping_trainer, abbey, graveyard,
sanitarium, guild, tavern, statue, nomad_wagon, stage_coach and blacksmith, then the actual footer mouse-down/up
opened the approved quest selector. Private evidence: `local-art/ui-overhaul/hamlet-native-v05/town.png`,
`service-check.png`, `native-check.log`; pre-change DLL/config backup is its `plugin-before/` directory.
The isolated interaction run logged no Hamlet exceptions. Earlier visual-preview session ended after manual
menu use and a native IMainInstaller shutdown exception; no conclusion about its cause is established. Agent
tools did not select/read/write campaign saves; only synthetic in-memory campaigns were used for these checks.

All995 Core+182 UI tests pass. Sixteen added Core cases cover alpha holes/top-down orientation/edge coordinates,
foreground ordering, missing packs, partial/duplicate/unknown service mappings, invalid dimensions/masks and
paths outside the pack. Release build/deploy passed with3 existing warnings; source/deployed DLLs match.
Native locked appearance, service transactions, saved-campaign embark and other window sizes remain [?].
The current exterior is static v05 for unlocked buildings: upgrade-level exteriors and special plot/event sky
paintings need an art pass. No town-event, upgrade or save rules were changed.

## Status 2026-10-11: round214 deployed; normal DD2 launch restored

Owned check PID62904 was closed after verification; DD2 PID58852 was launched normally at1600x900, without
either Hamlet QA flag, and reached MAIN_MENU with the normal Hamlet button. No campaign picker was opened
by agent tools. Keep protected estates1/3 off limits. The private pack is installed in the active
`C:\Users\Piral\darkestwithdlc\game\PrivateHamletArt`, not the older retained game directory.

## Round215 (2026-10-11): reversible live 3D Hamlet and DD1 architectural details

Owner authorized a perspective Hamlet using installed DD2 scenery, behind an easy rollback flag, then asked
for DD1 building textures to establish the correct service identities. `Look.Hamlet3D` defaults false; the
town's 3D town/Painted town buttons switch immediately and persist only the config choice. The approved
round214 painted pack remains the default, followed by the original DD1 town if that pack is unavailable.

`HamletStage` loads native Addressables by source path (including GUID-only resource keys), copying only
MeshFilter/MeshRenderer data into an owned distant stage. Skinned visuals can be baked once; native road
nodes, inventories, shops, triggers and animators are never instantiated. A dedicated perspective camera
renders into a 1920x1080 texture. Wheel zoom, middle-drag pan, reset and smooth service focus use the existing
Hamlet panel dispatch. Navigation/roster/footer and modal pixels retain input ownership. Hidden town screens
release the stage after three frames; switching to painted art releases it immediately. Load failures and a
45-second timeout leave the fallback usable, with retry through the mode switch.

Ten services use the native inn, cathedral entrance, Watchtower, Field Hospital/nurse, Hoarder, coach,
camp shelter, graves and Altar of Hope. A shortened inn component becomes the forge. Seven installed DD1
idle-atlas crops provide the anvil sign, forge opening, guild banners and abbey windows on fixed world
surfaces. Atlas rotation/original padding is respected; textures remain in the owned install. Native paving,
camp props, lampposts, Valley scenery and mountain/sky models connect the composition. Only specifically
needed ground textures were inspected privately; the other agent's road/arena export pipeline was untouched.

Native rendering findings: the node cathedral prefab is empty; the direct cathedral entrance FBX works.
Combined road meshes contain several materials, with env_black first; select the actual road material by
name. Native road materials carry `_FOG`, `_HASLIGHTING_ON`, `_NEARDARK_ON`, distance-darkening and dithering
keywords. Those produced black/gray patches at the distant stage origin. Owned material copies disable the
road-specific effects, retaining native textures, cutouts, depth and palette. Shared native materials are
never changed. Temporary camera globals are restored after the owned camera renders. Wheel renderers are
named `Mesh and Mesh Collider`; retain those visuals while excluding the actual wagon collider mesh.
The farm hill texture is a horizon strip, unsuitable for repeating terrain; installed foundation paving
provides the shared surface. Private exports and all derived game art stay outside Git/releases.

The explicit `-dd3-hamlet3d-preview -dd3-hamlet3d-check` fixture overrides the feature setting only in memory.
It creates no estate path, guards persistence only for its owned save object and blocks real embark. Native
1600x900 event checks opened and Escape-closed all ten world services, verified pan/zoom/reset, switched to
painted art and checked every owned resource category was cleared, reloaded and clicked again, entered the
approved quest selector, verified stage teardown, then returned to a ready 3D town. The final run logged PASS
with 72 renderers and no plugin warnings/errors. No OS input/focusing or protected estate access was used.
Private evidence: `local-art/ui-overhaul/hamlet-3d-v01/town-final.png`, `native-qa-final.log`, selected terrain
references and `plugin-before/` DLL/config backup. Earlier build experiments exercised the safe fallback;
the final scene replaces the invalid assets and fixes the reset control and memorial's occluded placement.

All 995 Core + 182 UI tests pass; Release build/deploy passes with 3 existing warnings. Native checks used the
deployed DLL with SHA256 `481317CF71EE864C9BF648F1066571AA1A607D1B6BB645ADD4916B4193F88C86`.
No campaign rules changed. This is the first static 3D composition: further building-specific
texture/geometry fitting, NPC idle animation, upgrade/event exteriors, locked appearance, transactions,
saved-campaign embark and other resolutions remain separate acceptance/art scope. See tools/hamlet_3d.md.
Unrelated Shieldbreaker source/tools/tests were preserved outside this round's commit.

Status: normal DD2 relaunched without preview/check flags (PID37708), reached MAIN_MENU and attached the
native Hamlet entry. `Look.Hamlet3D=false` remains the owner's default. The independent corridor changes
and Shieldbreaker work remain unstaged. No estate was opened during this round. During the final commit,
the corridor agent rebuilt/redeployed and started its own `-dd3-corridor-preview -dd3-corridor-check` process
(PID60456); leave that independently owned session running. The normal launch above predates that test.


## Round216 (2026-10-11): install native DD2 corridor scenery in all four primary regions

Owner requested beginning integration of the newly extracted road scenery, with an honest audit of missing
assets. Installed 24 road/decor/skirt/skybox-derived panoramas, six each for Sprawl, Foetor, Tangle and Shroud,
replacing the eight generated round171 corridor images. No new generated art. The room pack is untouched:
all 24 room PNGs byte-match the pre-change backup. Valley's four images stay uninstalled because it has no
campaign region plan; Caves/Tundra/Mountain are not invented as aliases onto existing campaign quests.
The 95 exported battle arenas were not installed in this corridor round.

New tools/prepare_native_corridors.py validates the full native source set and packages canonical names,
source/output hashes and fitting metadata outside Git. Live CrawlUi/HeroStage feet are y=680 in 1920x720,
source y=1020 in 2880x1080. Raw Tangle's band was nearly black under the feet (mean luma 0.2; 98.8% near-black);
Foetor also fell below its road (mean 8.7). Uniform top-anchored crops at 1.15x/1.08x respectively bring real
textured road under that line; no synthetic floor is painted. Prepared foot-band means: city 34.1, farm 42.1,
forest 12.2, coast 34.9-36.2. These statistics identify the black-border issue; they are not visual acceptance.
Sprawl/Shroud keep 1.00x framing. All 24 PNGs decode RGB 2880x1080 and installed SHA256 values match the manifest.
The kit's independently maintained extraction/render scripts were not changed.

Existing filename discovery, bounded worker IO, one texture upload/update, deterministic selection, reflected
scroll joins and regional teardown already support six variants; no gameplay/save changes were needed.
Changed the loader log from "generated panorama" to "private panorama" to describe either art source correctly.
The installed pack is local-art/corridors-native-v01 copied to the active darkestwithdlc/game/PrivateScenery.
Whole original 35-file scenery backup: .universal-modder/backups/dd3-scenery-before-native-corridors-20261011/
20261011-133022.zip. Pre-change 17-file DLL/config/data backup: dd3-plugin-before-native-corridors-20261011/
20261011-133328.zip under the same backup root. Restore the whole scenery folder, not an overlay which would
leave variants 03-06 behind. All native-derived art/private manifests remain outside Git/releases.

Added explicit -dd3-corridor-preview and optional -dd3-corridor-check QA fixture. It owns only an Off-phase
native main menu with Session.Save/SavePath null; no estate selection/creation, persistence, gameplay spawning
or OS input. Harmony redirects the ordinary Off-phase loader cleanup to its selected active region. The actual
CorridorPanoramaUi renderer scrolls forward/reverse over gold y=680 marks. Initial figure references were Story
portrait crops, unsuitable for feet; v02 uses full-body DD1 combat silhouettes via the existing SpineArt worker.
These are framing references, not native 3D actor acceptance. Close restores ordinary menu input/background mode.
Both native 1600x900 checks decoded/adopted six corridors per region and rendered all 24 choices, logging PASS.
Private evidence: local-art/corridors-native-v01/native-check/ (initial 24 captures/log) and native-check-v02/
(23 captures, complete 24-choice log, and post-exit log), plus native-regions-in-dd2.jpg. The capture loop started
after the second run's first city choice; it is not a missing decoder/render result.

Visual audit: Sprawl's distant maroon ground edge and mirrored houses are obvious; Foetor has reflected flesh
arches/flat light; Tangle retains dark base slabs and tree masses; Shroud posts/candles are baked into the lane
and cannot occlude actors. The six variants reuse one floor per region. Native shader lighting/fog/emission/VFX
are absent in the flat offline renders. First fix composition/ground coverage, quiet repeat ends, material
interpretation and foreground/depth passes with the existing assets. No generated replacement textures are
needed for the four primary regions yet. Road index 555 assets/58 empty; Tundra 44/46 empty needs dependency-aware
re-export before use. Mountain's three organ arenas and separately supplied Sprawl/Tangle arena floors remain
extraction gaps for later work. Detailed per-biome counts/priorities: tools/native_corridor_audit.md.

After v02 logged PASS, native shutdown logged the previously observed KeyNotFoundException for IMainInstaller
in Globals.InvokeUninstallCallbacks/GameInstaller.OnUninstallFinished. No scenery exception was logged; cause
and reproducibility remain unverified. Do not call this whole session exception-free or infer a scenery cause.
Real estate 2 DD2 hero foot contact, curio/trap/obstacle interaction, torch levels, room entry, in-place battle
snapshot/return and repeated-expedition memory remain [?]. No campaign save was read or changed by these checks.

Release build/deploy passed with three existing warnings; all 995 Core + 182 UI tests pass. Final deployed/source
plugin SHA256: 72546C54AFE0F4457ECFDDA6B50DBC878C2E9D117BC9814A03785649D43A7E25.
The other agent's round215 3D Hamlet and unrelated Shieldbreaker/hamlet_models work were preserved.

## Status 2026-10-11: round216 native corridors deployed; normal launch restored

The corridor QA processes have ended. Normal DD2 PID 58148 runs at 1600x900 with no QA flags, reached MAIN_MENU
and attached the native Hamlet button. No ordinary estate picker was opened. Private native corridors are
active for the four existing DD2 campaign regions; rooms/arena art and the default painted Hamlet are retained.
Next visual work: distant-ground edges, reflected repetition, Tangle readability, foreground props and native
lighting/emission, followed by real estate 2 quest acceptance. Keep protected estates 1/3 off limits. The separate
Shieldbreaker and new hamlet_models work remains outside this round's commit.

## Round217 (2026-10-11): ten individual Hamlet building model studies

Owner requested modeling each DD1 Hamlet building as a DD2-style 3D exterior, reusing native prefabs where
useful. This is an offline asset-authoring round. The installed town and Look.Hamlet3D implementation from
round215 are unchanged. Used the pasted dd2-native-kit and asset-pipeline skills, the owner's installed DD1
town FX/atlas artwork through the private dd1-reference-kit, and the existing dd2-native-kit mesh/texture
exports. Compared all ten fully repaired exteriors; retained level01/02/03/locked contact sheets for reference.
DD1 gives fronts/silhouettes/details; unseen sides and backs are inferred. No generated bitmap buildings.

New tools/hamlet_models builds separate Abbey, Tavern, Sanitarium, Guild, Blacksmith, Survivalist, Stage Coach,
Nomad Wagon, Graveyard and Ancestor statue models. Reused native inn/cottage/coach/tower/graves/nurse/camp props;
authored the missing nave, turret, hall, forge bay, tent, caravan and sculpture. Real roof volumes, stairs,
buttresses, window frames, wheels/spokes, grave thickness and service details establish depth across four
camera views. The Ancestor follows the DD1 bare head, long coat/cape and hands on staff. Native painted
surfaces plus selected DD1 banners, forge sign, eye crest, glass grain and stone weathering supply materials.
Corrected stretched roof UVs, the hidden Sanitarium turret, Nomad awning/door overlap and thin native graves.

Blender 5.2's glTF exporter silently loses constant color factors through legacy MixRGB nodes. A round-trip
render exposed white Sanitarium windows and missing cloth tint despite valid embedded images. Replaced
the authored/native material multiply nodes with current RGBA Mix nodes, rebuilt all ten, and added a
packed-Blender-to-GLB factor comparison. Imported images are lazily decoded: validate packed data and actual
pixel lengths instead of rejecting has_data=False before decoding. The shared native-kit tools are untouched.

Validation: all seven Python modules parse; Blender built ten textured GLBs and ten packed editable .blend
documents with forty 1024x1024 angle renders. Re-imported and rendered every GLB independently: all ten pass,
73,081 triangles total, exact topology and zero bounds change, finite three-dimensional geometry, embedded
texture data, and 37 painted material-factor checks. Private verified.json records SHA256 and results.
Gallery generation validates all fifty PNGs and rejects any GLB changed after verification. Visually reviewed
the model sheet, DD1 comparison and individual corrective views. No game build/test suite was rerun for
these offline-only tools; this establishes exported asset validity, not native game acceptance.

Private outputs: local-art/ui-overhaul/hamlet-buildings-v01/gallery.html,
hamlet-building-models-v01.png, dd1-to-dd2-comparison-v01.png and each building's GLB/.blend/five PNGs.
Only original authoring scripts and documentation belong in Git. All models, packed documents, extracted
textures, reference art and manifests remain outside the repository/releases. See tools/hamlet_models/README.md.

## Status 2026-10-11: round217 offline Hamlet models verified; runtime unchanged

The first modeling pass covers all ten services and has an offline gallery with DD1 comparisons. It needs
further art fitting: coarse Ancestor sculpture, repeated stone/wood grain, generic small doors/sign faces,
Guild side-wing detail and native material/lighting fit. Lower upgrade/locked geometry, event variants,
NPC animation, collision and LODs are not built. Current Core GltfModel.Load reads JSON glTF with external
buffers/images, not packed GLB, and does not apply painted color factors. Convert/extend that seam before
binding these models into HamletStage; retain the existing feature flag and immediate painted rollback.
No game process was launched/stopped, no deploy occurred and no save/estate was read or changed in this round.
The independently edited Expedition Map3D and Shieldbreaker files remain outside this commit.

## Round218 (2026-10-11): native painted detail pass for the Hamlet model library

Owner requested improving the textures/details toward DD2 accuracy, with an honest assessment of capability.
Built a separate v02 offline library while retaining v01 for comparison. Reused more native painted geometry
rather than upscaling the first pass's small repeating samples: hero_shrine for the Abbey entrance,
valley_academic_house for the Sanitarium, city_bigwall_main plus individual inn_lvl_01 roof/wing meshes for
the Guild, and the_hoarder_main cart joinery for the Nomad Wagon. Fitted parts keep their UVs and native
atlas textures. The inn prefab contains two separated buildings: selecting a single wing avoids two small
houses perched on the Guild roof. Existing source kits and the independent extraction pipeline are untouched.

Prepared original-resolution native cloth (591x453), timber (490x160), neutral masonry (125x270) and subtle
sculpture weathering panels with crop/tone provenance. Tent/wagon fabric now has real painted folds and
stitching instead of thatch grain. Added corner courses, carved cornices, sewn seams, pegs, tankard/coach
sign reliefs, forge tools, ironwork and grave accents. Ancestor facial planes, fingers and coat seams improve
readability; gray weathering replaces the brick-like robe. The custom sculpt remains coarse relative to DD2
character art. Corrected the Sanitarium cone's radial UV islands so shingle courses wrap around the cap.
Retained a physically closed pointed cap after native spire cutouts produced disconnected decorative faces.

Native-kit preview materials use emission. Converting those to explicit Principled emission preserves their
painted brightness in GLB, including values above one via KHR_materials_emissive_strength. Native alpha
cutoffs are retained. Verification now compares base and emission texture factors against the packed Blender
document and checks native MASK thresholds. Offline lighting is still an approximation of DD2's shaders,
outlines/fog and scene lighting; valid export does not establish an indistinguishable in-game result.

render.py selects v01/v02 and supports cheaper view/sample previews, with separate output directories per
pass. gallery.py produces versioned model/DD1 comparison sheets, a before-after sheet and First pass buttons
using copies of v01's verified exported renders. All generated models, textures, packed Blender files,
reference images and private manifests stay outside Git/releases. Rebuild workflow: tools/hamlet_models/README.md.

Validation: eight Python modules parse. Ten GLBs and packed editable Blender scenes, forty 1024px authoring
views and ten independently re-imported GLB renders complete. All ten exports pass exact topology, zero
bounds change, finite geometry, embedded texture decoding, 99 painted base/emission factor checks and native
alpha thresholds. Total 101,646 triangles. Authoring versus re-imported hero renders have RGB mean absolute
errors 0.022-0.182 on the 0-255 scale; this checks export appearance, not DD2 artistic acceptance. Gallery
generation validates hashes/current images; all 93 image/file/button paths resolve and all PNGs decode.
Visually reviewed the final model/before-after sheets and corrective detail views. Game tests/build were
not rerun for these offline-only tools. No game process/deploy or save access occurred in this round.

## Status 2026-10-11: round218 Hamlet native detail library verified; runtime unchanged

Private v02 output: local-art/ui-overhaul/hamlet-buildings-v02/gallery.html,
hamlet-building-models-v02.png, dd1-to-dd2-comparison-v02.png, before-after-v02.png and model subdirectories.
The first pass remains intact. Native portal/façade/roof/joinery and painted fabric improve the main art
weaknesses; the Ancestor still needs a finer sculpt, and repeated masonry/roof samples, small signs/trim,
lower upgrades and event variants remain. Native outline shaders, lighting/fog, animation, collision/LODs
and actual game integration are separate acceptance work. Current GltfModel loader requires external JSON
glTF/buffers/images and lacks painted factors; adapt it before installing the new GLBs into HamletStage.
Keep Look.Hamlet3D and immediate painted rollback. Independent Expedition Map3D/Shieldbreaker files were
preserved outside this round. No game or save state was changed by this modeling work.

## Round219 (2026-10-11): install the ten v02 buildings on a terraced 3D Hamlet

Owner request: apply the building library in game and replace the flat ground with a better perspective.
The ten v02 GLBs are now a private runtime package at active `PrivateScenery/HamletModels`, 54 hashed files
plus manifest, 59.5 MB. tools/hamlet_models/package.py verifies the ten source hashes, unpacks external
glTF/buffers/images, deduplicates PNGs and puts base alpha into emission paint maps where the RGB export
lost it. Original geometry, node transforms, UVs, factors and alpha cutoffs remain. No game assets enter Git.

GltfModel reads base/emission factors and KHR_materials_emissive_strength. HamletStage.Models parses and
hashes files on a worker, uploads one texture per frame and constructs one exterior per frame. It restores
the authored front after coordinate conversion, normalises ground pivots and batches by material using
owned meshes and copies of DD2's painted environment shader. Original primitive bounds remain separate
for picking, so batching does not turn a whole building's empty space into one click box. All uploaded
textures/meshes/materials release with the existing stage rollback, including after failed loads.

Core HamletLayout places the coach/market below the workshops, then the Guild and Abbey on higher levels.
Terrain rises 15 units with level foundations, a winding road, branch approaches, low retaining walls
and steps. A higher three-quarter camera and native Valley trees/rocks frame the town. The first native
capture showed dark earth and a front wagon hidden by the footer; the second pass brightens earth/paving,
moves coach/market farther into the scene and eases the camera framing. Old service placeholders are removed;
native surrounding scenery and its material templates remain. Service rules and UI dispatch are unchanged.

Validation: Release builds, 1028 Core and 182 UI tests pass. Four focused terrain/material cases pass again
after final layout/loader cleanup. Package Python parses and all 54 installed file hashes match. Native
unsaved QA PID67776 fully passed ten service clicks/Escape, pan/zoom/reset, painted rollback/resource release,
reload/re-entry click, footer Embark, stage cleanup and return. PID71468 with the brighter terrain/framing
passed all ten clicks, controls, rollback/reload and Embark cleanup. The owner asked to stop launching games
for 30 minutes before that run's final return-to-town check. Its logs and current capture were saved, then
only the owned QA process was closed. No normal relaunch. No estate was selected or saved; protected slots
were never read. Pre-change plugin/config snapshots: universal-modder hamlet-v02-plugin/hamlet-v02-config.

Private package/evidence: local-art/ui-overhaul/hamlet-ingame-v02/HamletModels and native/*.png/*.log.
first-check.png is the complete native run's town capture before the brightness/framing adjustment.
second-framing.png caught the painted fallback while models loaded; second-check.png caught the quest UI.
Neither second capture should be presented as a final 3D overview. Final overview/return acceptance remains.
Player.log repeats URP RTHandle reallocations between the native and stage camera targets; investigate
render-target churn before claiming performance acceptance. No speculative camera changes were made for it.

## Status 2026-10-11: round219 v02 buildings installed; owner game-launch pause

DD2 is stopped. Do not launch before 2026-10-11 16:56:44 UTC / 18:56:44 Paris unless the owner changes the
instruction. This overrides normal-game relaunch at the end of QA for now. Continue offline work. The
owner's existing local Hamlet3D=true is retained; source feature default stays false and Painted town
releases the full stage immediately. Ten models and the hillside are installed, with a full first native
PASS and partial second framing run as described above. Coarse Ancestor/trim, upgrades/events/NPC animation,
saved-campaign transaction acceptance, final bright-terrain overview and native render-target churn remain.
Independent Expedition Map3D, Shieldbreaker and corridor work was preserved outside the Hamlet commit.

## Round220 (2026-10-11): compact DD1 cliff landscape for the 3D Hamlet

Owner request: modify terrain/landscape to match DD1's verticality and map size. Read the installed
DD1 reference kit's native layout and assembled level03 Hamlet. Repositioned the ten sites around its
asymmetric composition: high left Survivalist cliff, Tavern/coach below, Sanitarium behind Tavern,
graveyard below the upper Abbey, Guild above the right forge, and statue/wagon in the low square.
Service foundations now span 24.5 world units vertically. The bounded ground shrinks from 300x300 to
124x105 (about 86% less area), and service centre depth from64 to53. The Abbey and Sanitarium are larger
landmarks. A lower, more frontal camera leaves room for the roster and footer.

HamletLayout separates explicit service elevations, rotated rectangular model footprints and a connected
road network. Height keeps foundations/doors grounded, carves approaches through uneven civic shelves,
and forms the steep left cliff and foreground cut. HamletTerrain bakes faceted soil/rock, directional
rock UVs and closed skirts. HamletGroundwork bakes ragged lanes, a small irregular square, level stair
treads and retaining courses into two surfaces. All geometry is computed on the existing model worker;
Unity uploads three owned meshes/four material draws rather than creating individual stair objects.

Installed DD2 forest_ground_distance soil, cultist_barricade_rocks paint and foundation cobbles distinguish
earth, exposed cliffs and paths. Exact material paths are present in the active Addressables catalogue.
Repeatable GPU texture copies preserve shared native resources. All new handles/render textures release
on rollback. Native trees/bushes and camp props follow the revised ground; two unrelated background inns
are removed. Enlarged native sky covers the frontal camera. The v02 building package itself is unchanged.
Look.Hamlet3D and immediate Painted town rollback remain; local Hamlet3D=true is retained, default false.

LayoutExport exports actual Core terrain, paving/masonry and camera to a private JSON. prepare_landscape.py
extracts three installed material textures with provenance. town_preview.py verifies all ten v02 GLB hashes,
places them and native scenery, renders1920x1080 and saves a packed Blender scene. Reviewed successive
renders against DD1: removed the broad continuous stone plateau, separated soil from paving, enlarged the
two civic landmarks, raised the left cliff above the Abbey shelf and fixed the sky's visible upper edges.
The final projected bounds for all ten buildings fit the town viewport; all referenced scenery exists.
This preview establishes geometry/composition, not DD2 shader/lighting or picking acceptance.

Validation: Release and all1030 Core +182 UI tests pass. Five final focused cases pass after the last
height/camera adjustments: DD1 relative placement/compact size, level rotated footprint corners, all
doors grounded and connected, and finite/nondegenerate terrain, stair and retaining meshes. Python syntax
passes. Final terrain has80,868 vertices and3720 exposed-rock triangles including skirts. Backed up the
installed plugin as universal-modder hamlet-landscape-v03-plugin, checked DD2 stopped, deployed, and
verified DLL/PDB SHA256 equality. No game was launched or estate read/saved, including after the owner's
30-minute pause elapsed. Other agents' dirty Expedition Map3D and Shieldbreaker work remains outside this commit.

## Status 2026-10-11: round220 compact landscape deployed; native acceptance pending

DD2 remains stopped. Private preview, actual geometry, surface provenance and packed scene are under
local-art/ui-overhaul/hamlet-landscape-v03/. Read tools/hamlet_3d.md and tools/hamlet_models/README.md
for the source seams and offline workflow. Repeat the unsaved Hamlet fixture for the new positions,
lighting/picking, rollback/resource release, Embark and return; the old native round219 PASS applies only
to its older landscape. Native frame time/URP render-target churn, Ancestor/individual trim, upgrades/events
and saved-campaign transaction acceptance remain. No new launch or normal session was restored this round.

## Round221 (2026-10-11): clearer terrain filler and a closer Mountain

Owner supplied a crop of the round220 offline render: cliff filler too dark, Mountain too far/small,
camera potentially usable with polish. Renewed the no-game-launch pause for30 minutes at17:17:37 UTC;
do not launch any game before17:47:37 UTC /19:47:37 Paris. Stored the deadline privately and in the handoff.
All work in this round remained offline, without reading or writing any estate.

HamletPalette now shares surface grading and native atlas windows between Unity and Blender. Soil uses
the painted upper30% of farm_ground_hill_transition, instead of the nearly uniform forest distance
texture. Cliffs sample the rock-only top24% of cultist_barricade_rocks, excluding its skulls and enlarging
the shapes. Owned GPU copies use mirrored wrapping to soften tile boundaries. World texture periods
increase from6 to10 for soil and12 for rock; softer facet shading avoids crushed black faces in previews.
Paving and retaining stone receive their own brighter, less saturated settings. Four native military
rubble clusters dress cliff bases; Valley trees sit1.4 units lower to reduce floating edges.

Native shader evidence: loaded installed shader/material bundles read-only through UnityPy. Both the
Valley road template and mainmenu_mountain_mat use Red Hook/Lit/Environment. Its actual color-control
keyword is _COLOR_CONTROLS; _UseColorControl toggles it, _Brightness defaults0, _Saturation defaults1.
The Mountain's installed material already enables that keyword. Grade only owned copies; disable
_HASVERTEXPAINTING_ON because the template's unused channel defaults can hide the paint. Shared materials
are untouched. Shader export is a property/variant description, not the HLSL implementation; Blender
color controls and mesh shade therefore remain an approximation requiring native visual acceptance.

Mountain width100->174, centreZ123->96, baseY-24->-36, height multiplier.85, depth multiplier.34. Its
roughly51-unit depth keeps the near edge behind the terrain'sZ69 bound. Graded native Environment
paint replaces its Sprite material. The perspective camera changes from40 to42 degrees and rises
slightly. Buildings, level foundations, routes, compact map extent and v02 model package are unchanged.

Offline pipeline exports Core palette/geometry, verifies all ten GLB hashes, uses three additional
private native rock-model exports and saves the final1920x1080 PNG/packed scene under
local-art/ui-overhaul/hamlet-landscape-v04/. Reviewed soil/rock alternatives and the final full composition.
All10 projected bounds fit x.071.. .722 and y.153.. .800, clear of roster/footer; no missing scenery.
Verified all five new native material/prop paths in the active Addressables catalogue. Assets, shader
exports, geometry and renders remain outside Git. The skill's native export pipeline reads bundles only.

Validation: Release passes with three existing unrelated warnings, five Hamlet layout/mesh tests pass,
and both Python tools compile. Backed up the previous plugin with universal-modder as
hamlet-landscape-v04-plugin/20261011-193912.zip. Checked DD2 stopped, deployed and verified SHA256 equality
for both Core/plugin DLLs and PDBs. No game launched. Other agents' Expedition Map3D/Shieldbreaker edits
remain unstaged by this round. The last complete test suite was round220's1030 Core+182 UI pass.

## Status 2026-10-11: round221 filler installed; launches paused until19:47:37 Paris

DD2 remains stopped. Keep Hamlet3D's defaultfalse/localtrue and Painted town rollback. New native grading,
camera, scenery placement and picking remain [?] until the unsaved Hamlet fixture can run. Do not launch
any game before17:47:37 UTC /19:47:37 Paris; no launch is scheduled. Private v04 evidence, sources and
packed scene are ready for review. Remaining art/RTHandle/saved-campaign acceptance is unchanged.

## Round223 (2026-10-11): native Hamlet paint, coherent routes, atmosphere and stable HDR buffers

Owner asked to keep iterating towards cleaner visuals and an immersive, coherent in-game Hamlet. The timed
launch pause expired at17:47:37 UTC /19:47:37 Paris before testing. All native runs used the explicit unsaved
Hamlet fixture with no estate path; no saved campaign was selected, read or modified.

Native baseline established all ten service/Escape routes, controls, rollback/reload, Embark cleanup and
return on the compact layout. Actual installed Environment paint remained too black despite round221's
_COLOR_CONTROLS values. Grade now makes bounded owned texture copies during construction:512px terrain/rubble,
1024px Mountain, cached by source/paint/crop. sRGB decoding, tint, luminance saturation, linear brightness
and gamma encoding lift shadows while preserving paint. Texture CPU pixels are discarded after upload.
Five Core cases cover neutral paint, lifted blacks/highlights and saturation/brightness. Shared game assets
remain untouched; owned copies, materials and handles release on rollback.

Rock crop grows24%->46% of the skull-free upper atlas. UVs now compensate for rectangular crop proportions;
the previous square-world mapping stretched both earth and rocks. Three cliff material tint bands reproduce
directional facet shading because native vertex colours are unused. Paving gets warmer/brighter paint.
Survivalist stairs wind across the cliff; the graveyard shares the civic ascent, branching down to the
Sanitarium. One added layout case caps stair grade at0.9 rise/run. Low broken forecourt walls bake into the
existing masonry surface. Four native Valley leaf banks and two Coast cliff ledges soften the edges.
Reviewed/exported native cliff geometry privately; use it as low ledges rather than stretching it over
24 metres. Four owned particle systems add restrained smoke at the actual Tavern, Sanitarium and forge
chimney caps, sharing one tiny texture/material. No native behaviours or gameplay systems are instantiated.

Performance red loop: baseline1920x1080 LDR target against DD2's1600x900 native camera caused roughly480
RTHandle reallocations/sec. Size-only probe halved that by stopping depth churn. Camera.main was null:
the active Main Menu Camera is untagged. Finding that view and copying allowHDR alone still left roughly240
colour reallocations/sec, because the target remained R8G8B8A8_SRGB. Read-only installed URP inspection
confirmed its target descriptor chooses the render texture's format. Selecting the native HDR format through
URP's own format selector produces B10G11R11_UFloatPack32 and zero repeated allocations in the final five-second
probes. Native camera/format lookup is cached and render targets follow live window resizing. Temporary
camera diagnostics were removed. The reproducible measure_native.ps1 rejects non-preview PIDs and requires
the3D town to have rendered before counting; an early pre-render zero probe was discarded.

Final native build:128 mesh renderers,4 small chimney systems,10 service models/101646 source triangles.
The extended unsaved fixture passes ten real world clicks/Escape, pan/zoom/reset, painted rollback/all
resource release, reload/re-entry click, Embark/stage cleanup, return, then1600x900->1440x900->1600x900.
All ten service bounds remain clickable at both sizes. Captures reviewed; no logged exceptions/warnings.
Final accepted probe records0 allocations over5.01 seconds. This is buffer stability, not an FPS or
long-session memory claim. NPC idles, exterior variants, sculpture/trim and saved transactions remain.

Validation: Release passes with the same three unrelated warnings; all1036 Core+182 UI pass. After the
final UV change, all31 Hamlet cases pass. Both Python tools compile; final offline1920x1080 preview and
packed scene render successfully. Private evidence:local-art/ui-overhaul/hamlet-immersion-v05/; accepted-native.png,
accepted-native.log, accepted-player.log, accepted-allocations.json, composition.json, layout.json and packed
hamlet-immersion-v05.blend. Pre-final plugin backup:hamlet-immersion-v05-plugin/20261011-222012.zip.
Both DLLs/PDBs match deployed SHA256 hashes. Owned QA PID64716 closed; DD2 stopped. Hamlet3D stays defaultfalse,
localtrue with immediate Painted town rollback. Independent Expedition Map3D/Shieldbreaker edits and round222
notes remain outside this commit. No game assets or decompiled code are committed.

## Status 2026-10-11: round223 Hamlet installed and native presentation checks pass

Continue from tools/hamlet_3d.md and the private v05 evidence. The owner's timed launch pause has expired.
Native service/camera/resource/resize checks and repeated-buffer stability are established. Preserve protected
estates, the feature flag/painted fallback and other agents' dirty files. Saved-campaign acceptance and remaining
art work are listed separately; the QA process is closed.

## Round225 (2026-10-12): Hamlet paint, wooded surroundings, stronger stone and clean stair junctions

Owner asked to remove the glowy building texture, add backgrounds around the isolated plateau and give terrain
more texture. Follow-up screenshots requested stronger road/wall contrast, then identified stretched entrance
paint and competing surfaces around the upper stairs. Kept the service sites, camera, gameplay dispatch,
feature flag and immediate Painted town fallback. All native testing used the unsaved Hamlet fixture.

Building importer had added the glTF author's preview emission into every surface tint, including native
paint-only materials with emission strengths1.2-1.55. BuildingTint now normalizes that channel as albedo,
uses base colour for ordinary architecture and retains brighter glass/forge paint. Owned1024px copies
slightly desaturate the building paint. The verified private model package is unchanged.

HamletLandscape adds a ring of lower valley terrain whose first vertices match the playable boundary,
with seven native Valley tree groups and three Camp treeline silhouettes. Shared Core placements feed
runtime and LayoutExport. First native iteration made treelines too high/pale; rejected it, lowered the
strips and kept their original silhouette paint/alpha through owned Sprite materials. No native scripts.
HamletGroundPaint supplies broad damp/dry stains and gravel weights; a one-time1024px owned world texture
combines native earth/stone. Source textures and GPU readbacks remain bounded, cached and construction-only.

Native common-bundle road_rough_patch replaces the faded large paving atlas. Tileable bricks replaces the
repeated rock atlas on terrace faces; foundation_panel_06 paints retaining courses and stairs. Crops were
selected from private contact sheets, avoiding doors/posters and rough-road transparent fringes. Shadow
lift falls to.002-.003 for the stone paints, retaining dark ink joints and bright stone. Native paths/cliffs
keep1024px owned paint; masonry/soil use512px. No game assets enter Git.

The entrance corruption was geometric: a gentle1-unit rise over13 units triggered four long stair blocks.
Their UVs used riser height on the horizontal tread face, creating stretched slivers. Stair construction
now requires grade>0.18 and rise>=0.45. Gentle roads stay paved ramps. Actual stairs use a separate coursed
stone mesh with level landings, and no cobbled strip through the blocks. Box UVs use the actual face width,
height/depth. All paving branches and the square share one world projection. Cliff UVs use a continuous
diagonal world projection rather than choosing a new axis at each triangle normal. Side-face scale varies
somewhat, but courses no longer jump at facet boundaries.

Six new Core cases cover boundary continuity, finite/upward unoccupied surroundings, architecture versus
warm-light tint, road/stair/wall UV contracts, retained stone contrast and ground variation. The old black-lift
test now tests an explicitly requested lift independently of the darker current palette. Full1044 Core+
182 UI pass. Release passes with the same three unrelated warnings. Private1600x900 offline PNG/packed scene
and composition have no missing scenery. Native final140 MeshRenderers/four chimney systems passes ten world
service clicks/Escape, controls/reset, painted rollback/all resources released, reload/re-entry click,
Embark/cleanup/return and1600x900->1440x900->1600x900. Screenshots reviewed after the final ramp/stair/UV fixes.
No logged exceptions/warnings. Final URP probe:0 reallocations over5.024 seconds. Buffer stability only,
not an FPS or long-session memory claim. Both DLL/PDB pairs match deployed hashes; owned QA PID76868 closed.

Private evidence:local-art/ui-overhaul/hamlet-landscape-v06/accepted-native.png, accepted-native.log,
accepted-player.log, accepted-allocations.json, deployed-hashes.json, core-tests.log, layout.json,
composition.json and hamlet-landscape-v06.blend. Previous round223 backup remains in
hamlet-immersion-v05-plugin/20261011-222012.zip; first-pass backup is hamlet-landscape-v06-plugin/first-pass.zip.
No estate selected, read or saved. Other agents' Shieldbreaker/Expedition Map3D edits and round222/224 notes
are preserved outside this commit. Hamlet3D remains defaultfalse/localtrue with immediate painted rollback.

## Status 2026-10-12: round225 Hamlet installed; entrance and upper stair junctions repaired

Continue from tools/hamlet_3d.md and private v06 evidence. Stronger stone contrast is retained; the entrance
stays a ramp and actual stairs have one material with correct face mapping. Native service/control/resource/
resize and repeated-buffer checks pass. DD2 is stopped, no save restore pending. Preserve protected estates,
the painted fallback and independent dirty work. Saved-campaign transactions, exterior/NPC variants and
finer art remain separate.
Owner: "she is holding the spear in reverse, hand does a 360 and tip of the spear is behind her towards the ground".
DD1's reverse grip (blade low behind) reads as backwards in 3D, and clip keys blend spear directions linearly: from
blade-behind to blade-forward the blend passes through almost no direction, so the wrist flipped. Every spear key now
points forward or up (z >= 0; checked at export): idle upright and slightly forward at the hip, defend upright, expose
low but blade forward. Installed after the owner closed DD2: json 2B8245D4 (textures unchanged).

## Round226 (2026-10-12): native grass on unused Hamlet ground

Owner requested grass where it fits and better source texture quality if available. Reviewed75 native
ground/grass materials from common, Valley, Foetor and Tangle bundles, plus three Valley grass meshes.
The usual grass grids and tileable turf are512px. Foetor's farm_gate_node_assets_grass atlas is4096px;
three grass/reed rectangles have substantially more source pixels per tuft. Private crop review excludes
the neighbouring bell, gate, timber and masonry. No generated texture or game art enters Git.

HamletVegetation in Core defines ground coverage, route/rotated-footprint clearance and grounded placements.
The field fades outside route edges and service aprons, leaving the paths, stairs and entrances clear.
170 tufts sit only on gentle ground with at least1.5 units of clearance. Each has two low angled planes;
one owned mesh/material binds the original4096px native atlas directly. Native alpha/ink remains; restrained
olive tint fits the town's dead foliage. Disable native wind because these planes lack its painted weights.
The grass does not enter service picking or gameplay dispatch.

The world ground bake grows from1024px to2048px. It combines native512px short dry grass with existing
earth/gravel, broad stains and a256x256 Core clearance field, evaluated once during construction.
The surrounding valley uses the same turf. Existing stone/path paints and all service/camera positions
remain. New material handles, owned grass paint and geometry release with the stage. Painted town remains
immediate rollback. No estate selected/read/saved.

prepare_landscape exports six source paints; LayoutExport exports the same Core coverage and tuft geometry.
town_preview resolves kit/model roots before Blender imports, avoiding relative texture-path failures after
glTF changes the process directory. Private1600x900 preview/packed scene contains every scenery export.
Three Core cases check clear routes/entrances, grounded placements and finite cropped geometry.
Release passes with the three pre-existing warnings; all1068 Core+182 UI pass, including40 Hamlet cases.

Owner approved closing ordinary DD2 PID77236 for installation and a brief unsaved check. Closed only that
verified process, deployed while DD2 was stopped, and matched both DLL/PDB pairs against build hashes.
Owned QA PID86776 renders170 grass tufts in one mesh,141 total renderers/four chimney systems. Native alpha,
crop boundaries, muted paint and cleared paths were reviewed in the full1600x900 screenshot. All ten service
clicks/Escape, pan/zoom/reset, painted rollback/all resources released, reload/re-entry, Embark/cleanup/return
and1600x900->1440x900->1600x900 pass. No warning/error/exception during the check. Final native HDR target
retains0 repeated URP allocations over5.029 seconds; this is buffer stability, not an FPS or memory benchmark.
Owned QA PID86776 is closed. A later ordinary DD2 PID85280 is left untouched. The fixture never selected/read/saved an estate.
The independent Expedition Map3D build subsequently replaced the assemblies; current installed/build hashes
match and every accepted Hamlet source file is unchanged from51822ee. Keep deployed-hashes.json as the native
acceptance build, and post-map-build-hashes.json as the later deployment record.
Independent Shieldbreaker and Expedition Map3D changes/notes remain outside this commit.
Private evidence:local-art/ui-overhaul/hamlet-grass-v07/{grass-sources.json,grass-crops.jpg,ground-source.json,
layout.json,composition.json,hamlet-grass-v07.png,hamlet-grass-v07.blend,core-tests.log,ui-tests.log,release-build.log}.

## Status 2026-10-12: round226 grass installed and native presentation checks pass

Round226 grass code is committed/pushed as51822ee; native installation and acceptance now pass. Current
evidence is local-art/ui-overhaul/hamlet-grass-v07, including accepted-native.png/log, accepted-player.log,
accepted-allocations.json and deployed-hashes.json. A subsequent ordinary DD2 PID85280 is running; leave it
untouched. post-map-build-hashes.json records the independent map deployment. No save access/restore is pending.
Read tools/hamlet_3d.md and preserve independent agents' dirty files. Saved transactions, finer art/variants
and full frame-time/long-session memory acceptance remain separate.

## Round227 (2026-10-12): continuous expedition camera and native regional atmosphere

Owner requested animated Shroud mist, Sprawl smoke/fire/embers and Foetor gas, plus a smooth approach to a
quest's area without the two-step/reload feeling, keeping quest controls in place. Existing uncommitted Map3D
selector/config/layout work was adopted and backed up privately before editing. The independent Hamlet and
Shieldbreaker changes are preserved outside this round.

The old enlarged overview followed by a separate closer-image fade is removed, including the closer-image
loader API. One persistent map3d.png is transformed by Core ExpeditionMapCamera's critically damped springs
(log scale and bounded pan). Retargeting/reversing preserves the current frame and velocity, unscaled time
drives movement and stalled frames are bounded. All region labels, paired arrows and quest rows stay visible,
clickable and at their overview positions; scenery hit areas follow the camera. Escape/right-click/Back to the
map returns without clearing the quest. The painted fallback and config defaultfalse remain.

tools/extract_expedition_vfx.py reads the owned DD2 Addressables via universal-modder's UnityPy environment,
with bundle hashes and bounded source evidence. Five private RGBA PNGs provide six effect IDs: City64-frame
8x8 fire30FPS/smoke20FPS and four spark shapes; Coast native fractal fog/half-oval mask; Farm/Forest four native
soft-wash cloud variants. Native Farm steam is brown/pink, not green. The older dd2-native-kit drafts omitted
particles; this is a separate native atlas layer. No generated art or extracted game assets enter Git.
REA reused matching IronCrown identity evidence only; particle settings came from shipped serialized
prefab/material data, not observed shader execution or live native playback.

Runtime ExpeditionMapEffects reads off-thread, uploads at most one unique image per Unity frame and shares
the cloud sheet. The prepared pack decodes to13.25MiB without mipmaps. Leaving the map releases its owned
textures after three unused frames; stale workers own no Unity objects and cannot repopulate a later pack.
ExpeditionMapAtmosphere draws52 deterministic, clipped quads beneath quest controls without consuming
gameplay RNG or handling input. Effect density/trajectories/fades and some tints adapt native masks to this map.

Verification: all1071 Core and187 UI tests pass; Release Deploy=false passes with the three existing warnings.
New tests cover mid-flight retarget/reversal, stable frames, frame-rate consistency/coverage, atlas playback and
malformed metadata, plus the actual loader's shared textures, one upload/frame, main-thread API use, invalid
dimensions, stale workers, cleanup and reload. The UI shim does not validate GPU decoding. The extractor
passed compilation, full re-extraction, PNG/frame-cell inspection and a source/converted contact-sheet review.

Native fixture -dd3-expedition-check is built, not yet run: it injects IMGUI input only into the existing unsaved
preview to check actual quest rectangles, retarget/reversal, paired switches, painted rollback/reload and ordinary
provisioning cleanup/return. It guards Persist, blocks gameplay handoff and selects no estate path.
DD2 PID77236 was already in a battle, so it was left running and no plugin/config/art was installed. Asked the
owner for a safe restart window. Native appearance, timing, click/lifecycle checks and hash-verified deployment
remain pending. No estate files were read or written and no restore is pending.

Limits: this is a camera over a rendered valley, without live perspective/parallax, depth occlusion or native
HDR/bloom/distortion. The overview already has baked haze/glow/motes; use restrained overlays. A clean overview
re-render can separate those layers using the existing kit; no new generated textures are needed. Higher source
resolution can improve close-view sharpness. Read tools/expedition_map3d.md before changing packs/ownership.
Private evidence:local-art/expedition-motion-v01/{baseline,before-qa.png,core-tests.log,ui-tests.log,build.log};
local-art/expedition-vfx-v01/{vfx-manifest.json,native-vfx-evidence.json,serialized-evidence,runtime-effects-preview.jpg}.

## Status 2026-10-12: round227 expedition map ready; safe restart/native acceptance pending

Code is committed/pushed ase33ac7c; the private native VFX pack is ready. The Hamlet agent subsequently
deployed a shared build including this map code (source/installed plugin SHA256 match20E4066B...), passed its
unsaved Hamlet checks in PID86776 and restored normal DD2. Normal PID85280 has already entered battle and
was left untouched. The VFX folder is still absent; the expedition-specific native check has not run.
Before installation: PrivateExpeditionArt backup is universal-modder expedition-motion-v01-art/20261012-073814.zip;
shared plugin backup is expedition-motion-v01-plugin/20261012-073815.zip; config copy is private
local-art/expedition-motion-v01/config-before-qa.cfg. No saves were accessed. Once DD2 is safely closed, install
the manifest/five PNGs, verify deployed hashes and run -dd3-expedition-check plus native visual acceptance.
Close only the owned QA PID and restore a normal launch. Preserve the independent Hamlet/Shieldbreaker work.

## Round228 (2026-10-12): Hamlet hover, estate double-click and expedition roster swaps

Owner requested three small input fixes. CampaignMenu cloned native buttons now own a transparent full-rect
raycast surface and explicit ivory/brass TMP feedback for pointer hover and controller selection. Native banner
art and highlight components remain. Estate rows accept only a left double-click; it selects that row and calls
the same guarded EnterSelected/BeginLoading path as the Return button. Single click still only selects, right
double-click does nothing, and unreadable saves cannot enter. Loading assets and gameplay handoff are unchanged.

The formation already swapped party portraits, but roster payloads for heroes already in the party were rejected.
DropHero now resolves membership by hero ID regardless of pickup source. Core ExpeditionFormation owns the bounded
four-rank swap/replace/append operation; other ranks retain their positions and existing heroes cannot duplicate.
Existing availability, missing-week and quest-refusal guards remain. Carried portraits use identity and the tooltip
now describes swapping. Shared Drag behavior is unchanged.

Verification: all1080 Core and187 UI tests pass, plus Release with three existing unrelated warnings. Nine new Core
cases cover swaps in both directions, full-party replacement, packed empty ranks, same-rank drops and bad input.
Native -dd3-menu-input-check passed actual EventSystem handlers for hover/reset/controller focus, single selection,
right double-click, invalid synthetic summary and selected-row double-click through the torch gate. The first fixture
attempt tested the deliberately disabled native entry while the picker was open; the fixture now temporarily uses
its normal enabled state and restores it. Native -dd3-roster-check passed real IMGUI mouse down/drag/up, selected
roster swaps, replacements, fast releases, outside/Escape cancellation, click removal, empty rank and return to roster.
Its first attempt put the final release below the roster area; it now uses the actual Area.center. These were fixture
corrections, with no additional gameplay changes. Final native screenshot inspected; logs contain both PASS markers.
ExpeditionPreview now injects its synthetic event before Drag.Begin and runs Overlay before restoring the real event.

Both fixtures use synthetic estates/transient roster data, no estate path, and the existing no-Persist guard. Native
estate-file selection/handoff was deliberately not repeated. Ordinary PID85280 had already exited before deployment.
Owned QA PIDs90412/89568/88908/83932 are closed; live process inspection finds DD2 stopped. Config SHA256 is unchanged;
deployed plugin/Core DLL hashes match the final build. No saves were accessed and no restore is pending. No round227
VFX pack was installed. Independent Shieldbreaker files and notes remain outside this commit. Private evidence:
local-art/ui-overhaul/menu-roster-input-v01/{menu-accepted.log,roster-accepted.log,roster-accepted.png,deployed-hashes.json}.

## Status 2026-10-12: round228 input fixes installed and native checks pass

The three requested input fixes are installed. All1080 Core+187 UI and native synthetic menu/roster checks pass.
DD2 is stopped, owner config unchanged, no estate read/written and no save restore pending. Keep normal launches free
of -dd3-menu-input-check/-dd3-roster-check. Round227 VFX installation/full map acceptance remains separate; preserve
the independent Shieldbreaker work and pending art/model checks. Next work should follow the owner's next request.

## Round229 (2026-10-12): complete curio design mapping, native source audit and private atlas

Owner requested a DD2-based visual design for every curio, grounded in extracted models/textures. This pass
authors 104 individual proposals: 60 base-library entries, 10 additional quest props, 27 installed DLC definitions,
six legacy art-only identities and the Faded Memory exception. Independent inventory checks exact ID coverage;
quest region mapping comes from quest goal lists, not guesses. Sprite aliases and supply/quest identities stay
separate. Current CurioLibrary loads the base CSV only; DLC/legacy coverage is not a runtime-support claim.

tools/curios/README.md records regional direction, closed/active/open presentation, model backlog, production order
and a proposed reversible presentation seam. author_designs.py/designs.json hold author-owned proposals and native
source selectors. catalog.py audits actual meshes/materials/texture dimensions and writes private HTML/Markdown/
JSON. Each card names shape, remaining new modeling, supply cues and used state. No gameplay behavior, renderer,
feature flag or asset pack was implemented. Faded Memory retains the owner's explicit DD1 appearance.

The native audit renders 39 reference groups (42 selected source objects). Direct common/inn model exports supplement
the kit. Visual inspection rejected a two-triangle pickup-chest icon, the old combined inn table/knife mesh, a
Guardian building mislabeled altar, a Hero Shrine chapel used as statue and black cage backing. Use a real camp
coffer, direct table/knife models, compact Chirurgeon slab and textured chain; statue/cage/occult sculpture remains
new work. Banners, moss, tendrils and nurse are labeled material/shape/pose references rather than finished bodies.
There are 12 preliminary native prop assemblies; none is claimed as a finished bespoke curio model.

Verification: Python compile, all104 inventory/design IDs,39 nonempty textured source groups,51 nonempty transparent
renders and252 local image links pass. Native-source and assembly contact sheets were visually inspected after
replacing misleading sources. Node VM exercised the actual inline filter script with DOM stubs:104 total,60 base,
10 quest,27 DLC,6 legacy,1 memory,12 studies, case-insensitive exact-ID search, combined no-match, region and reset
pass. Browser automation timed out; actual browser layout was not observed. The saved HTML was queued in Codex.
No .NET/runtime code changed, so runtime test/deploy/launch is outside this design round. No saves or config accessed.
Offline previews use native base-color/tint emission; native shader/VFX/animation/picking acceptance remains pending.

Private output:local-art/ui-overhaul/curio-design-v01/{index.html,curio-mapping.md,inventory.json,audit.json,
source-sheet.jpg,assembly-studies.jpg,artifact-check.json,filter-check.json,renders,concepts,parts,extra}.
Only authored tools/design data/docs are versioned; all game assets/fonts/images remain private outside Git.
Independent Shieldbreaker files and notes are preserved outside this commit.

## Status 2026-10-12: round229 curio proposals ready for review; runtime implementation remains separate

The full104-entry curio design mapping is complete. Review the private searchable atlas and tools/curios/README.md
before implementing. Start with ordinary container states, books/bones and camps; distinctive bodies need modeling.
The 12 assemblies show source combinations only. No game launch or save access occurred in this pass. Existing
round228 input acceptance stands; separate round227 VFX install/native acceptance and Shieldbreaker work remain.

## Round230 (2026-10-12): clickable 3D Hamlet camera/background research and offline comparison

Owner requested best practices and another camera/background iteration without applying code. Authored
tools/hamlet_hub_research.md separates firsthand source findings, comparable hubs, project inferences and
unverified interaction claims. Sources include Red Hook's Bourassa interview, original GDC lighting slides,
Blizzard Hyperion material, 2K/XCOM material, Xbox navigation/contrast/motion guidance and Unity/Epic camera/
atmosphere documentation. No universal camera angle or lens is claimed. The relevant Red Hook GDC talk was
identified only by its listing; no detailed claims rely on an unviewed talk.

Six private1600x900 offline renders use the accepted packed grass-v07 scene. A is current18.75-degree pitch,
42-degree verticalFOV. B25/34 and C32/40 expose more scenery joins/crop the peak; D15/30 compresses terraces.
E22/36 with3-degree yaw and136-unit distance is a modest overview candidate. F retains E's camera and service
positions, quiets repeated stone, offsets the full-scale native mountain right/down, grades distance and replaces
the camp treeline strips with two continuous ridge placeholders. F is a composition blockout, not final DD2 art:
plain ridges, dark facades and outer-ground transitions need work. Preserve DD1 placement, a stable home shot,
readable entrances and backed hover names; later service focus must remain optional/reduced-motion compatible.

The private HTML compares the baseline with five alternatives, optional grayscale and measured HUD occupied-area
guides. These are guide rectangles, not native UI. Six image decode/dimension checks,60 clear and safe door-apron
proxy ray/projection checks and unchanged source-blend SHA256 pass. Baseline/E/F color/grayscale sheet inspected.
The actual review script passes choices/image/summary changes and HUD/grayscale toggles with Node DOM stubs.
Browser layout was not observed; Codex queued the opening request. Full building bounds, real picking, service
panels, resized views and camera motion are not accepted from these renders. No .NET tests are needed for notes.

Private outputs:local-art/ui-overhaul/hamlet-camera-study-v01/{index.html,review-sheet.jpg,a-current.png,
b-overlook.png,c-high.png,d-low.png,e-refined.png,f-proposal.png,proposal.blend,camera-study.json,checks.json}.
Native assets, renders and study scripts remain outside Git. No runtime/config/save change, build, deploy or game
launch occurred. Concurrent corridor/room and Shieldbreaker work and notes are preserved outside this commit.

## Status 2026-10-12: round230 Hamlet research ready; no camera/background implementation

Review tools/hamlet_hub_research.md and the private comparison before a new native art pass. Begin near22-degree
pitch/36-degree verticalFOV, compose with actual HUD, connect the valley, then adjust facade/road/wall hierarchy.
The offline F ridge blockout is not production scenery. Native service/picking/motion/resize checks and game-facing
implementation remain separate. All previous installed Hamlet acceptance and the painted rollback are unchanged.

## Round231 (2026-10-12): remove mirrored corridor twins and install native encounter/boss room scenery

Owner reported doubled/mirrored corridor architecture and requested actual DD2 encounter/boss backgrounds for rooms.
Confirmed cause: CorridorPanoramaUi tiled complete perspective scenes with alternating reflection. The raw Sprawl
source has no bilateral join; the prior native capture does. A failing regression observed two adjacent actual UI
draw calls for one complete scene. The fix draws one bounded UV crop with uniform8% overscan. Physical corridor
progress drives a small pan, independent of corridor global tile offsets and heading reflection. Source floor y680
stays fixed; UVs stay inside the PNG. Props retain physical travel; the full plate now has slower distant parallax.
Corridor choices retain source orientation. Native tile-layer fallback, map generation and gameplay RNG are unchanged.

Private room discovery supports arena-<purpose>-NN names, prioritizes those over legacy numbered extensions, and
retains12-image/16MiB/4096x2048/header/aspect allocation bounds. Purpose selection uses stable Room.Content/IsSecret:
boss, entrance, battle (including guarded curios/treasures), quiet and treasure. Clearing/revisiting/curio use does
not swap scenery. Missing boss uses an encounter; missing purpose uses legacy/distributed art. Broken files are
excluded before completed-set adoption. Native focal compositions never mirror. A separate actual room draw seam
retains fallback floor blending; full private arenas can render even when the native tile Addressables are unavailable.

tools/render_native_rooms.py renders32 scenes, eight per primary region, from the owner's DD2 kit. Native lair
interior/exterior become boss/entrance; resist/faction/military/cultist supply four encounter variations; gaunt/pillager
supply quiet/treasure. Shroud cultist export lacks a landmark, so Chirurgeon replaces it. The original combat camera
put the floor above crawl feet; vertical lens shift0.31 reframes it. Native cobblestone, farm/forest ground and Shroud
deck materials extend across an authored foreground plane below arena geometry. Native base-color/tint/depth haze
remain approximate lighting, without animated VFX. This selects scenery only, not DD2 boss/encounter gameplay.

All32 PNG decodes/dimensions/byte bounds/source-output hashes and8-per-region counts pass. All24 installed corridor
hashes remain unchanged. The24 old generic arena PNGs moved to local-art/scenery-single-scene-v02/previous-room-pack;
HamletModels and provenance are preserved. Complete PrivateScenery backup:
C:/Users/Piral/.universal-modder/backups/dd3-scenery-single-scene-v02/20261012-132054.zip (106 files/170MiB).
Prior plugin/data backup:dd3-before-single-scene-plugin/20261012-132430.zip. All private imagery remains outside Git.

Verification: original draw regression red->green; actual UI coverage/aspect/floor/tint/matrix/failure cases; continuous
forward/reverse/discrete progress; typed purpose/cleared stability/invalid discovery/fallback; actual loader's skipped
boss, completed adoption, one upload/update and cleanup. Release +1095 Core +188 UI pass. A final four-case position
check includes reverse discrete stepping. Installed plugin/Core DLL hashes match19e5119e.../0b3f0899... recorded privately.
The shared deployed build includes independent in-progress Shieldbreaker sources; those files/notes are not part
of this commit. Concurrent round230 Hamlet research is preserved. No generated textures are used or needed here.

One native1600x900 unsaved -dd3-scenery-check launch, PID87872, displays all24 corridors and32 room scenes through
the actual loader/draw methods. All56 frames captured, native PASS logged, and both complete contact sheets visually
inspected: mirrored joins absent, arena floor reaches full-body DD1 reference feet at y680. No native error/exception
was logged during the completed check. QA process closed by exact owned PID; DD2 stopped; mod config SHA256 unchanged.
No estate selected/read/written by the fixture or tools, no campaign save modification/restore, no OS input sent.

Private evidence:local-art/scenery-single-scene-v02/{rooms,native-check,native-room-sheet.jpg,
native-corridor-sheet.jpg,asset-verification.json,installation.json,deployed-hashes.json,*tests.log}.
Workflow/limits:tools/corridor_art.md and tools/native_corridor_audit.md. Remaining art work: hard distant ground-card
edges, flat lighting, baked foreground occlusion and static fog/water. Recover shader/emission/VFX and depth-separated
passes; more diffuse textures/generated art are not currently necessary. The small bounded pan is not a unique
full-length corridor; longer lateral native renders or live layers would support more travel. Native3D actor contact,
saved expedition room transitions/torch/curios and in-place battle capture/return remain separate live-quest acceptance.

## Status 2026-10-12: round231 corridor reflection fix and native rooms installed and preview verified

DD2 is stopped with normal launch behavior. Use the new backgrounds in the active install; no preview flag needed.
Do not infer saved-quest/3D actor or combat handoff acceptance from the unsaved DD1-reference preview. No rollback is
pending. Preserve independent Shieldbreaker work and Hamlet research; round227 map VFX installation/acceptance remains
separate. Next visual priority is ground-card joins/native lighting/depth rather than generating replacement textures.

## Round232 (2026-10-12): approved Hamlet overlook and native landscape iteration

Owner authorized implementing round230's camera/background direction. HamletLayout now uses22-degree down
pitch,3-degree yaw,36-degree verticalFOV and136-unit distance. Ten DD1 service sites remain fixed. The full-size
native Mountain moves right/down, separating it from the Abbey. Quieter cliff/coursed stone retains ink joints;
building albedo and genuine window/forge light retain the earlier non-glowing treatment. Hover lift is reduced
and the native brass name banner has dark backing. Selected-service zoom is smaller and fully eased; hovering
does not move the camera. Reset restores the home view immediately.

The approach extends to the town boundary and follows a graded painted ribbon into the lower valley. Dense
outer terrain rings and broad uneven wooded shoulders replace the isolated edge. Fourteen native Valley groups
replace seven groups/three Camp cards; their root banks sample nine footprint heights and sit below them.
The owned2048px valley paint shares native soil/gravel/turf and stain patterns with town, blends across its edge
and adds distance haze. Sources/readbacks and a256x256 grass field are construction-only. Native assets, private
exports/textures and packed Blender output remain outside Git. No native node gameplay components are copied.

Tab/Shift-Tab and directional arrows focus services in screen order; Enter/Space uses existing service dispatch,
Escape backs out and pointer events restore mouse hover. Header/roster/footer and active overlays block world
input. Camera motion On/Off controls automatic service focus and chimney smoke, leaving explicit pan/zoom/reset.
Look.HamletCameraMotion defaults true; the fixture changes it only in memory. Look.Hamlet3D stays defaultfalse
with immediate Painted town rollback. No estate/save contract or service rule changes.

Private compile snapshot freezes tested sources, preserving tracked concurrent hero code already in the shared
deployed build; that hero code is outside this commit. Release has only the three existing warnings. All1097 Core
and188 UI tests pass; all42 Hamlet tests pass after the final ridge-height adjustment. A road regression caught
the approach becoming stairs; its grade was corrected rather than weakening the existing UV/stair contract.
Initial offline rendering exposed floating leaf banks and an abrupt grass edge; footprint grounding and matched
near-town paint corrected those. Final packed preview and native overview/service panel were inspected.

After plugin/config backups and confirmed DD2 shutdown, installed only the four Release DLL/PDB files. Owned
unsaved1600x900 QA PID95980 passes all ten world-service clicks/Escape, pan/zoom/reset, painted rollback/full
resource release, reload/re-entry, Embark/cleanup/return and1440x900 resize/restore. Extended fixture also passes
keyboard focus/Enter/back, still camera during an open service, smoke stopped/cleared when off and resumed on.
No OS input sent. Final145 mesh renderers,168 batched grass clumps and four chimney systems retain the native HDR
target. Allocation probe:0 repeated URP reallocations over5.055 seconds. No native error/exception in completed
acceptance logs. Source and all four installed file hashes match. Config semantic delta is solely the newly bound
default motion setting; existing feature preferences are retained. Owned QA closed; DD2 remains stopped.

Private evidence:local-art/ui-overhaul/hamlet-overlook-v08/{native-overview.png,native-first.png,native-BepInEx.log,
native-Player.log,native-allocations.json,acceptance.json,build-sources.json,*tests.log,release-build.log,
hamlet-overlook-v08.png,hamlet-overlook-v08.blend,layout.json,composition.json}. Pre-change universal-modder backups:
hamlet-before-overlook-v08/20261012-134633.zip and hamlet-before-overlook-config-v08/20261012-135139.zip.
No estate selected/read/saved; no save restore. Concurrent corridor/atmosphere and Shieldbreaker sources/notes
remain outside this commit. Read tools/hamlet_3d.md for controls, lifecycle and limitations.

## Status 2026-10-12: round232 Hamlet overlook installed and unsaved native acceptance complete

Use the existing3D feature preference; no debug flags needed for play. Painted town and camera-motion controls
are available immediately. Native checks cover presentation/routing/lifecycle at the tested sizes, not controller
comfort, arbitrary text/window sizes, saved transactions, final lighting or long-session/frame-time behavior.
NPC/upgrade exteriors and finer individual trim/model work remain art scope. Retain independent corridor and
hero work; check live process and installed hashes before another deployment. DD2 is stopped.

## Round233 (2026-10-12): continuous native corridor ground and animated expedition atmosphere

Owner authorized the remaining scenery gaps after round231. Rebuilt24 native corridor PNGs (six per primary
region), preserving32 typed room PNGs and HamletModels. The broad horizontal stripe came from opaque ground
composited above distant geometry. Native textured ground now sits below those rows; ground/skybox horizons and
the lowest three world units of geometry skirts fade continuously. Native placement/camera/road fitting remain;
Sprawl lamps and Shroud mooring posts move behind the walking lane. Shroud distant water colour is approximate
because its exported ocean material has no diffuse texture. No generated art or additional extraction needed.
tools/render_native_corridors.py imports the private kit helpers without modifying them. Separate Blender passes
and Pillow composition retain source/output hashes, camera/placements and material/tool provenance. Final ground
horizon blend spans160px. Fresh output is needed after geometry/material edits; completed road passes are cached.

Core DungeonAtmosphere produces bounded visual-seed/time motion without gameplay RNG: Sprawl smoke/embers,
Foetor steam, Tangle haze and Shroud mist. Same panorama crop and positive atlas UV clipping keep effects stable
on reversal and inside the scene. Torch brightness reduces visibility. Existing shared ExpeditionMapEffects
provides five native PNGs/six effect descriptors (~13.25MiB decoded), one upload per frame and release after use.
The round227 private VFX pack is now installed for crawl use; map input/camera/lifecycle acceptance remains separate.
Coverage-only mist/clouds use white RGB and native red alpha (plus native fog gradient) rather than double-darkening
the mask through RGB and alpha. Fire/smoke simulation RGB and sparks stay intact; native HDR shaders are approximate.

tools/prepare_scenery_emitters.py projects native active fire-mesh bounds through the installed room camera.
Optional bounded metadata supplies16 anchors in six Sprawl room scenes, tied to exact flat filenames. Lair scenes
have no accepted visible fire meshes; no arbitrary flames are added. Invalid/missing packs retain static scenery.
RegionSceneryArt adopts emitter metadata with its worker result and clears it on teardown. During in-place combat,
the base snapshot excludes particles, crop/anchors freeze, and LateUpdate redraws base plus current effects into
the existing RT before native actor cameras render. Time continues through combat/return without particle trails.
Faded Memory preserves its DD1 appearance. No campaign rule/save schema or ordinary combat action changes.

Verification: Release passes with the three existing warnings. All1112 Core/188 UI tests pass, including motion,
crop/UV clipping, bounded metadata, native anchor binding and actual loader adoption/cleanup. Python compile and
diff checks pass. Native1600x900 -dd3-scenery-check passes all24 corridors/32 rooms; all56 final-art captures and
five contact sheets inspected. The final unsaved -dd3-crawl-scenery-check passes actual DD2 actors, torch use,
curio resolution, trap/disarm, obstacle/shovel, reversal, room battles and identical-texture return in all four
regions with the final assets. Victories use debug damage/kills/native turn-pass, not ordinary equipped skills.

The repeat native check exposed a QA shortcut failure: fanatic_mash_211 left a Pit Fighter alive at zero HP,
while other enemies were corpses and turns advanced. A pinned native repro distinguished this from a stalled
return hook. The fixture now finishes survivors through the existing native debug-kill helper; its first battle
pins that configuration so every run covers it. Native red->green is the regression oracle; no unit shim exercises
DD2 Death's Door/presentation. Temporary state/HP instrumentation and repro-only flag removed. Final check logs
contain no error/exception. Earlier failed/interrupted runs stay private and are not counted as final acceptance.

Pre-change backups:dd3-before-atmosphere-v03-scenery/20261012-140313.zip (115files/192.4MiB),
dd3-before-atmosphere-v03-plugin/20261012-140319.zip (17files/16.9MiB), and
dd3-before-crawl-check-native-profile/20261012-140320.zip (39files/1.8MiB), under the universal-modder backup root.
All deployment occurred with DD2 stopped. Owned QA closed by exact PID; normal launch behavior retained. Four
installed DLL/PDB hashes match Release; config unchanged. Native DD2 SaveFiles was restored after earlier checks;
final read-only comparison matches all39 original file hashes. Owned final QA PID98200 is closed. A normal DD2
launch PID95604 appeared afterward and is left running untouched; no final restore/deploy was performed over it.
No DD3 estate selected/read/persisted, no protected estate access, no OS input sent.
The shared Release includes pre-existing concurrent Shieldbreaker sources; those files/notes are outside this commit.

Private evidence:local-art/scenery-atmosphere-v03/{corridors-final,scenery-emitters.json,installation-final.json,
native-all,native-final-verified,native-complete,native-final-*.jpg,final-verification.json,*tests-verified.log,
release-build-final.log,field-note.md}. Local universal-modder field note check passes; no public KB submission.
Read tools/corridor_art.md and tools/native_corridor_audit.md. Art, exports and decompiled data stay outside Git.

## Status 2026-10-12: round233 scenery/atmosphere installed and native travel/battle-return verified

Use the active install with a normal game launch; no QA flags needed. Continuous ground and moving native effects
are present in all four default regions. Native deferred exposure/bloom, animated ocean, independent foreground
depth and longer unique lateral travel remain fidelity work. Existing diffuse textures suffice; these gaps need
native shader/effect/depth reconstruction. Ordinary pointer/controller gameplay, saved reload, normal skill kills
and long-session/frame-time acceptance remain separate. Normal DD2 PID95604 is running untouched; preserve
concurrent hero/Hamlet work and check live processes before deployment.

## Round234 (2026-10-12): native DD2 Hero Paths and the complete owned hero roster

Owner chose DD2 Paths alongside DD1 Resolve, equipment and Guild progression, then explicitly requested
Abomination and any other missing DD2 classes. The installed expedition roster has 15 native classes.
Abomination and Duelist join the normal weekly coach when their owned ResourceActor is available, alongside
Crusader and permanent Bounty Hunter hires. Existing offers and heroes remain intact. Campaign asset loading
prepares all owned DLC portraits. Cold Dd2Tables now includes expedition/DLC hero stats and skills, excluding
Kingdom overrides. Completeness is checked against the installed selectable hero catalog, not only a count.

HeroPathCatalog reads native defaults, class/tag eligibility, unlock IDs, ordered choices, comparison flags,
423 ordinary/mastered replacement entries and mode/always-equipped metadata. All 42 specialisations across
14 classes are available at Resolve 2. Bounty Hunter retains Professional; DD1 Shieldbreaker has no invented
Path. The Guild previews native Path text and current/proposed skill effects, with one explicit Follow purchase
for 1,000 gold before Guild discount. Only available roster heroes in an open Guild, outside an expedition,
can retrain. Learned/mastered/equipped base IDs and all Resolve/equipment progress stay unchanged. Default
choices serialize as null; older saves retain defaults. Invalid saved choices fall back without rewriting saves.

Native actor creation selects the class default, unlocks starting/learned skills, applies mastery, then sets
the chosen ActorDataPath and canonical loadout. Path-first mastery misses replaced base skills. DD2 itself
supplies combat effects, buffs and replacements. Guild/hero sheet icons and tooltips resolve the active variant.
The exploration HUD now uses native GetCombatBaseSkillId rather than suffix trimming, which previously left
Path icons blank. Native current-mode filtering keeps each Abomination form's banner independent. Guild kits
allow four chosen skills per form plus automatic Transform/Revert. Bounty Hunter's nonexistent mastery upgrades
are not sold. Cold-menu effect text uses real native data/localization; rare unsupported conditions are marked
as abbreviated. Native libraries supply the full formatter after host initialization.

Audited reachable infinite-duration Path/hero buffs through skills, passives, effects and token edges. Of 41
unique applied candidates, native combat-end cleanup handles Jester, Hellion, Abomination and Flagellant groups;
the two Monarch Oath IDs persist. HeroPathExpedition captures/restores only those two, idempotently with native
definitions and the 66-stack cap. Expedition/fight checkpoints deep-copy the state, interrupted fights roll
back to pre-fight counts, and homecoming discards accumulation. HeroRecord stores no Oath progress.
Matching REA managed artifact/member evidence was reused for IronCrown SHA256
dd97e30f2f37e2a287633ab322b8fd18bcfe9e240903713a5c2ae00c894692e6,
MVID c8627dd8-3822-4311-9403-8920d40a2892. Native details stay private; no game/decompiled data enters Git.

Verification: Release passes with the three existing warnings; all 1,133 Core and 188 UI tests pass. New cases
cover real-data roster/Path completeness, native replacements, owned DLC stats, refusal/payment/persistence,
mode capacities, Oath JSON/copy/checkpoint rollback and normal weekly permanent recruitment of the four extra
classes. Final unsaved 1600x900 -dd3-paths-check passes real Guild confirmation, all 57 default/specialisation
choices in ordinary and mastered form (114 native actors), canonical skill/loadout/form HUD mapping and bounded
idempotent Oath restoration. The earlier real-click check's Abomination/Duelist/Crusader/Bounty Hunter party and
Guild captures were inspected. Final -dd3-paths-actors-check repeats actor/mode HUD assertions with rule-based
confirmation, avoiding OS input while the owner is active, and prefers Unchained/Instructrice/Templar/Professional.
No errors occur through the final PASS. The process exits before its final HUD capture; native IMainInstaller
uninstall and OnApplicationQuit/audio exceptions follow PASS. Shutdown and final HUD visual acceptance remain
open; do not claim a clean teardown or blame Paths without a reproduction. Full ordinary all-skill combat/
balance, controller comfort, arbitrary text/window sizes and long-session performance remain unverified.

Pre-native profile backup: universal-modder/backups/dd3-before-paths-check-native-profile/20261012-201404.zip
(39 files). Final-plugin backup:dd3-before-paths-final-plugin/20261012-203558.zip (17 files). Deployment only
with DD2 stopped. QA owns a transient save, refuses existing estates and guards its own persistence. OS input
was restricted to the fixture's Follow button after more than a minute of idle. No DD3 estate read/selected/
persisted, no protected estate access. Final installed DLL/PDB/config/profile checks and closed PID are recorded
in local-art/hero-paths-v01/installation-final.json. Final owned QA PID95796 is no longer running; it was not
killed by this agent. Owner requested no game launches for 30 minutes at 18:47 UTC; do not launch before
2026-10-12 19:18 UTC (21:18 Europe/Paris). Native profile hashes match the original backup; no restore
was needed. Shared Release includes independent Shieldbreaker WIP; those sources/tests/notes remain outside
this commit. Private evidence:local-art/hero-paths-v01; maintenance and limits:tools/hero_paths.md.

## Status 2026-10-12: round234 Hero Paths and all owned DD2 classes installed and native-verified

Normal launch needs no QA flag. New DLC classes can appear at the next normal coach refresh; existing offers
are retained. Guild -> hero -> Choose Path, Resolve 2, 1,000 gold before discount. Paths persist with heroes,
including through expeditions. Abomination retains both forms and native transformations. All owned DD2 class
defaults and specialisations passed native construction/mapping checks. Cold conditional tooltip completeness,
ordinary full combat playthroughs, final HUD visual/shutdown and broader input/performance acceptance remain the limits above. Preserve
independent Shieldbreaker work and recheck live processes before another deployment.

## Round235 (2026-10-13): Abomination form HUD and native shutdown comparison

Continued the hero/Path work after the owner's October 12 launch restriction expired. Added an optional mode
to Art.Portrait/HeroIcon and the cache key, and passed the live actor mode from the exploration HUD. Native
ResourceActor.GetPortraitIconByType(Color, mode) falls back to the human colour portrait when the mode has no
colour override. A distinct native CombatBar portrait is now preferred in that case, preserving native art.
No new textures, class rules, Path balance or estate schema changes.

Opt-in -dd3-paths-visual-check extends the existing unsaved actor fixture. It checks all 114 ordinary/mastered
Path actors, then selects the actual Abomination and changes human -> beast -> human through DD2's native
EventActorChangeMode. A direct SetActorMode only changes data; the event also dirties ActorBhv's model selection.
Checks cover five equipped skills in each form, visible native model roots, portrait lookup, ordinary Leave/
homecoming, stage model release and Hamlet return. Each stage holds 15 seconds for GPU capture, then requests
Application.Quit. This is mode presentation acceptance, not a Transform/Revert combat skill/timeline test.

Native first QA PID20504 passes all 114 actor cases and the full form/homecoming fixture. Human, beast, restored
human and Hamlet images were later captured; visual review caught the colour-portrait fallback despite the
initial native equality assertion passing. The final assertion now requires the beast portrait to differ from
the human portrait. The correction is built; that final combat-portrait screen check is pending while the
separate Hamlet task owns the shared install (QA PID23348 at last observation). It was left untouched. A second
capture run closed before its last PASS and is not counted as complete acceptance. Do not claim final portrait
visual verification from the earlier images. Private before-portrait images retain the diagnostic evidence.

Investigated the earlier shutdown errors using matching REA evidence ev_790fdee0e6fe7a5824d9eb3d20e6c8f1a0733ff9d59b34b8961d6e0c0787440d
for IronCrown dd97e30f2f37e2a287633ab322b8fd18bcfe9e240903713a5c2ae00c894692e6 / MVID
c8627dd8-3822-4311-9403-8920d40a2892. Globals.InvokeUninstallCallbacks (0x0600003c) directly indexes its
dictionary; only Reinstaller.Reinstall registers IMainInstaller's callback. Ordinary quit resets/uninstalls
the main installer without registration, then GameInstaller.OnUninstallFinished (0x0600008b) invokes it.
GameSpeedMgr.OnApplicationQuit (0x0600154b) dereferences AudioMgr after native teardown can clear the singleton.
DrivingPositionalSound.Release (0x06006107) has the same unsafe dependency. Static contracts explain the stacks;
identical installer errors also occur in unrelated October 11 fixtures, before Paths.

Owned cold-start baseline PID12532 was launched with process-local DOORSTOP_DISABLE=1, supported by the active
doorstop_config.ini ignore_disable_switch=false. BepInEx/plugin startup is absent from its Player log and the
BepInEx log remains unchanged. Window-close via that exact PID's CloseMainWindow reproduces IMainInstaller's
missing-key and GameSpeedMgr's quit null exception without DD3 loaded. Baseline remained on the cold warning
screen; this does not claim a native road/positional-audio comparator. No quit exceptions were suppressed or
patched, and the temporary environment override does not alter normal launches.

Release/frozen compile passes with the three existing warnings. Frozen 377-file snapshot includes current
concurrent Hamlet/Shieldbreaker sources without staging them. All 1,134 Core and 188 UI cases pass after copying
the native address catalog to the private snapshot's expected relative fixture path. The first Core attempt
failed only for that missing fixture; no assertion was weakened. No full game/data copy is committed.
The latest shared Hamlet build includes the four exact hero source files and its installed DLL matches its
snapshot, but final hero visual acceptance is still pending. Later installs remain under the other task's control.

Backups:dd3-before-paths-visual-native-profile/20261013-141202.zip (39 files) and
dd3-before-paths-visual-plugin/20261013-141203.zip (17 files). Native tests use their own unsaved estate object;
no DD3 estate selected/read/persisted, no protected estate access. Initial deployments occurred with DD2 stopped;
after observing the other Hamlet QA, no further shared deployment or process control was performed. Native
profile/config are not restored over the other session. The chat-messaging tool's explicit authorization was
requested for coordinating the final test slot; no cross-chat message was sent without an answer.
Evidence:local-art/hero-paths-v02; tools/hero_paths.md. Preserve all independent Hamlet/Shieldbreaker changes.

## Status 2026-10-13: round235 form models/kits/homecoming verified; final portrait capture pending

All 15 DD2 classes and 42 specialisations remain implemented from round234. Native form models, five-skill HUD
switching and return cleanup pass. The native combat-portrait fallback is built and included in the shared build,
but its final in-game image/stronger assertion must be checked in the next free game window. Existing native
shutdown errors reproduce with the loader disabled and are separate from Paths. Do not interrupt the other
Hamlet QA or deploy over a running game. Cold rare conditional descriptions, ordinary combat/transformation
playthroughs, controller/text sizing and long-session performance remain broader acceptance work.

## Round236 (2026-10-13): Hamlet material fidelity, native colour and grounded terrain

Owner: continue iterating the 3D Hamlet toward DD2 design, texture quality, colour coherence and readability.
Used the pasted dd2-native-kit, Blender and asset-pipeline/game-automation workflows. All native art stays
private. The ten v02 GLBs, compact DD1 service placement and approved camera are retained.

Building grading no longer resamples every atlas to a 1024px square. Paint copies retain source/crop aspect
and native detail up to 2048px on the longest edge, without upscaling small images. Texture uploads use 4x
anisotropy. Native Environment cutouts now use the verified _____IsAlphaCutout property and keyword. Grading
preserves material alpha: an intermediate experiment made invisible native backing meshes white; fixed and
asserted in the final native fixture. Locked-service tint is less dark so its silhouette remains identifiable.

Live native comparison identified the main-menu renderer's LUT as the remaining blue/grey colour cast.
Camera post-processing off and URP Unlit alone did not remove it. Only the owned Hamlet camera now selects
the installed Universal_Featureless_ForwardRenderer by name; no native camera, renderer feature or pipeline
array is edited. Owned terrain/paving/rock/masonry bind the explicitly loaded native URP Unlit shader.
Buildings, sky, Mountain and foliage retain their Environment materials. The shader handle is owned and
released on rollback/exit; missing renderer/shader keeps Painted town available. Ambient globals are neutral
and restored after the owned camera renders. Paint readbacks explicitly set/restore GL.sRGBWrite; official
Unity contracts and exact runtime details are linked in tools/hamlet_3d.md.

Reviewed 25 unique installed stone/rock surfaces. Rejected a repetitive cultist crop, oversized Coast strokes
and four bare Camp trees. Final exposed cliffs use Foetor's farm_eroded_hill, a 960x760 rock-only window with
matching texel-aspect UVs and a 12m period. Dressed masonry remains on stairs/retaining courses. Paving is
quieter, and the world paint gains bounded contact darkening at foundations/banks and gravel wear around
roads/aprons. A 256x256 construction field feeds the existing 2048px bake; no per-frame painting/readback.
Distant native woodland is cooler/quieter; valley haze reaches its existing cap sooner. Six native surface
exports and shared Core contact/wear fields keep the private Blender preview reproducible.

Verification: frozen Release passes (three existing warnings); all 1,135 Core + 188 UI tests pass. New Core
cases cover retained atlas/crop resolution and bounded contact/wear, alongside existing terrain/UV/clearance
contracts. Final unsaved native fixture passes ten world services/Escape, pan/zoom/reset, painted rollback
with complete resource release, reload/re-entry, Embark/cleanup/return, 1600x900 ->1440x900 ->1600x900 resize,
keyboard focus/Enter/back and still-camera/smoke toggles. Shader/transparent-backing assertions pass. Native grass, rock and earth bind, the offline export resolves
all six surface sources, and the final native overview has no white backing artifacts.
145 mesh renderers, 168 grass clumps and four plumes retain the native HDR target. Zero repeated URP buffer
reallocations over 5.053 seconds; this does not establish full frame-time/long-session memory performance.

All 15 owned source files match the frozen build and all four installed DLL/PDB hashes match. Config is
byte-identical to the pre-pass snapshot. Pre-deploy universal-modder backups:
hamlet-before-fidelity-v09/20261013-142649.zip and hamlet-before-fidelity-config-v09/20261013-142650.zip.
Native captures/logs, source/install/config hashes, tests, reviewed textures and final packed scene:
local-art/ui-overhaul/hamlet-fidelity-v09/{acceptance.json,native-final.png,native-BepInEx.log,native-Player.log,
native-allocations.json,build-sources.json,hamlet-fidelity-v09.png,hamlet-fidelity-v09.blend,layout.json}.
Owned final QA PID25176 is closed. No ordinary game relaunched, estate selected/read/saved or OS input sent.
Concurrent Shieldbreaker sources are preserved in the shared build and excluded from this commit; its journal
and parity changes remain unstaged. Round235's hero work remains in HEAD and in the frozen candidate.

## Status 2026-10-13: round236 Hamlet material/terrain polish installed and native-verified

Keep Look.Hamlet3D defaultfalse and immediate Painted town rollback. Texture/rendering defects are corrected,
but full DD2 production art parity remains open: custom cliff silhouettes, facade/roof trim, NPCs and upgrade/
event exteriors. Controller comfort, arbitrary window/text sizes, saved transactions/real embark and complete
frame-time/long-session memory acceptance remain separate. Read tools/hamlet_3d.md and preserve other work.

## Round237 (2026-10-13): enclosing Mountain and hostile Hamlet boundaries

Owner found the Mountain underwhelming because of its size and visible space around/behind it, then asked for
dark/aggressive outer vegetation so the Hamlet reads as the sheltered, buildable area. Compared six private
native-mesh placements. Final Mountain width174 ->500, pivot(28,-41,96) ->(-12,-78,112), heightScale.85 ->.52,
depthScale.34 ->.15. Actual height86 ->151, about2.9x wider/1.75x taller. Lower mass is buried beyond the ridge;
its front bound remains aroundZ80, behind the Z69 town boundary. Peak/outer-slope cropping is intentional.
Native mesh/material, camera and all ten service sites remain. Runtime and LayoutExport placements agree.

Fifteen native Tangle decor assemblies replace seven nearby Valley groups and extend around the town's sides,
rear and low foreground. forest_tile_01/12 supply gnarled trees, vines, roots and broken palisades; two
forest_tile_04 banks supply low debris. Seven distant Valley groups remain. Existing nine-sample burial grounds
their banks. Wildwood desaturates cyan toward dark grey/green; hidden forest ground backing keeps the owned
terrain continuous. Wilderness paint smoothly darkens outer earth by up to35%, preserving the clearing and
approach. The exported field/grade and preview match runtime. No scripts, triggers, colliders or native game
behaviours are instantiated, and no extracted assets are committed. New Core checks caught and corrected a
foreground bank encroaching on route clearance; they also check all sides, service/approach gaps and paint joins.

First Mountain-only native run passed pointer/camera/rollback/resize but failed Enter after synthetic Tab while
the owner was using the preview. Product mouse activity intentionally clears keyboard focus. The synthetic QA
overlay now consumes live pointer/key events only while its checks run, then releases them after PASS/failure.
The full native rerun checks the actual Tab/Enter/Escape path with this input isolation; ordinary play input
is unchanged. Failed log is preserved privately. No OS input or desktop focus sent.

Final frozen Release and1,137 Core+188 UI pass. Final native unsaved fixture passes ten services/Escape, pan/
zoom/reset, painted rollback/full resource release, reload/re-entry, Embark/cleanup/return,1600x900 ->1440x900
->1600x900, keyboard focus/Enter/back and still-camera/smoke toggles. Shader/hidden-alpha assertions pass and
the final native overview is inspected. Zero repeated URP allocations over5.036sec establishes buffer
stability, not full frame-time or long-session memory. Source/install hashes match; config is byte-identical.
Owned final QA PID25580 closed, DD2 stopped, no ordinary launch and no estate selected/read/saved.

Private evidence/studies/final packed scene:local-art/ui-overhaul/hamlet-mountain-v10. Pre-deploy backups:
hamlet-before-mountain-v10/20261013-191604.zip and hamlet-before-mountain-config-v10/20261013-191605.zip.
Preserve independent Shieldbreaker code/notes; its existing tracked sources remain in the shared frozen build
and outside this commit. Read tools/hamlet_3d.md and tools/hamlet_models/README.md for rendering and limits.

## Status 2026-10-13: round237 Mountain and hostile outer boundary installed and native-verified

Look.Hamlet3D stays defaultfalse, with immediate Painted town rollback and existing local preferences retained.
The larger Mountain and dark native woodland frame the safe clearing. Bespoke cliff/facade art, NPC/upgrade/
event exteriors, controller/other text/window sizes, saved transactions/real embark and full performance remain.

## Round238 (2026-10-13): standard3D Hamlet/embark and eastern cliff; native acceptance paused

Owner retired the2D/3D presentation flags for Hamlet/embark, asked for more exposed rock on the right, then
requested no game launches for1h20. Pause starts18:06:19 UTC and ends19:26:19 UTC /21:26:19 Europe/Paris.
Owned QA PID29076 closed immediately; do not count its interrupted run as complete native acceptance.

Both3D presentations are now standard. Removed Plugin view entries, runtime mode setters, header switches
and native-fixture overrides. Startup consumes/removes old Look.Hamlet3D/Look.ExpeditionMap3D keys even when
false. Actual interrupted boot verifies only those two keys are removed; all other config values retained.
Camera motion/reset remain in the freed header space; failed scenery exposes Retry scenery. Painted art is
automatic loading/recovery only. Service, quest, roster, pairing, progression and provision rules are unchanged.

Returning-estate torch loading now requests the real native Hamlet and waits for Ready before handoff. Scene
failure offers retry/cancel; Retry clears its failure/resources. New estates still start on the Old Road and
saved expeditions use their own route. The unused-frame lifecycle handles cancellation/exit. This new native
torch path still needs its read-only fixture after the pause. No estate is required by that check.

Rejected added Coast shelves after offline inspection; they looked like thin fins. Core now cuts a steep,
irregular eastern face beyond the workshop foundations so slope-classified native rock is visible on the right.
Existing service pads, roads, camera, Mountain and hostile woodland remain. Added a Core rock-exposure case
alongside the existing foundation/route/finite-geometry coverage. The full private preview/packed scene is
local-art/ui-overhaul/hamlet-embark-default-v11/hamlet-cliff-v11.{png,blend}; shaders remain approximate offline.

Hamlet native QA now releases all stage resources directly, checks automatic default3D reload and uses the
new reset-control position. It no longer forces a mode or clicks removed buttons. Expedition native QA starts
with the ordinary default map, checks explicit effect release/automatic reload and provisioning cleanup/return.
Live input is isolated only during synthetic checks. These modified final lifecycle/map checks remain pending.

Frozen Release and all1,138 Core+188 UI pass. Final cliff subset:19 layout/landscape cases pass. All13 owned
sources and four installed DLL/PDB hashes match. The interrupted native Hamlet run opened ten world services
with no error and no view override; no full PASS, final native cliff capture or buffer probe. Expedition/torch
fixtures were not launched. Later source changes are failure/config descriptions and a fixture-only camera-motion baseline.

Private acceptance.json records offline verified/native pending, source/install hashes and the two-key config
migration. Interrupted logs and a guarded future launch helper are alongside it. Before-default backups:
hamlet-before-default-v11/20261013-195821.zip and hamlet-before-default-config-v11/20261013-195822.zip.
Final artifacts deployed while DD2 stopped, with no launch during the pause. No estate selected/read/saved,
no OS input sent. Preserve independent Shieldbreaker work and its journal/parity changes outside this commit.

## Status 2026-10-13: round238 built/installed; finish native acceptance after the owner launch pause

No game launch before19:26:19 UTC /21:26:19 Paris. Afterward, use the private launch helper's hamlet, expedition
and torch modes, with no mode override. Complete default Hamlet services/controls/full release/reload/Embark/
return/resize/keyboard/still-camera checks, native right-cliff visual capture, default map camera/effects/provision
checks, and read-only torch prewarm/cancel/reopen. Record final buffer stability/source/install/config evidence,
close only owned QA PIDs and leave ordinary play untouched. Broader saved/controller/performance/art work remains.

## Round239 (2026-10-13): retain Hamlet through quest selection; torch-only cold entry

Owner reported the old 2D town and "Preparing town scenery" on return from expedition selection. The stage
released every owned resource after three undrawn frames; HamletUi then drew its painted/DD1 fallback until
reconstruction completed. This also defeated the returning-estate gate once the selector had been visited.

HamletSceneLease separates visible, retained and departed use. EmbarkUi retains an existing stage throughout
quest selection, hero sheets and provisioning. A complete hidden town parks its camera and particles, keeping
one set of models, handles and its rendered texture. Returning resumes that same scene. Build completion is
now independent of camera.enabled, so a parked camera cannot cause terrain/model construction to run twice.
Actual departure still releases after three unused frames; explicit retry/shutdown release all resources.
A retained-window resize revalidates the native target before presentation and waits for a fresh render.

Homecoming prewarms the town behind expedition results. Cold/retry entry blocks town HUD/service input on an
opaque full-window torch page until the actual stage is Ready. It reuses the installed CampaignTorch flame/art,
with the same expand scaling as the menu. Failure gives Retry scenery/Main menu. Removed the painted/hybrid
and DD1 Spine town draw paths, preventing a 2D fallback flash even when loading is slow or fails. Quest,
service and campaign rules are unchanged. Existing legacy preloaded art/config remain compatible but are not
drawn by HamletUi. No game assets committed.

Offline regression: extracted the existing three-frame lifecycle without changing its initial decisions;
`dotnet test tests/DarkestDungeon3.Core.Tests -c Release --filter FullyQualifiedName~HamletSceneLeaseTests`
failed all three cases before the fix (retained requests released/rendered instead of parking). It now passes
retention across 600 selector frames, immediate resume, true departure/grace, same-frame use order and reset.
Frozen Release passes with three existing warnings, all 1,141 Core + 188 UI tests pass. The native unsaved fixture
now asserts cache/texture identity, parked camera/particles, resumed rendering and cold torch gating, in place
of the former expected selector cleanup. Updated fixture and Homecoming/torch visuals have NOT run in game.
No game launched, estate accessed or OS input sent; owner launch pause respected throughout this round.
Private build, source/install hashes, backup and acceptance: local-art/ui-overhaul/hamlet-return-cache-v12.

## Status 2026-10-13: round239 loading/cache fix built and installed; native acceptance pending

The town is cached while selecting/provisioning a quest, prewarmed during expedition results and gated by its
first native frame on cold entry. There is no painted town fallback. Full native services/park/resume/resize/
retry/cold torch and real expedition-results handoff acceptance remain pending. Round238 map/estate torch
checks also remain pending. Do not auto-launch after the owner's timed pause. Preserve independent
Shieldbreaker WIP and protected estates; continue from tools/handoff_prompt.md.

## Round240 (2026-10-14): DD1 stage contact and continuous DD2 scenery

Owner reported that backgrounds and ground alignment broke immersion, then requested a refinement loop before
Vilhelm integration. A private Blender differential reproduced the room camera mismatch: a four-unit reference
was 504px high at the actor origin and 229px at depth10, against HeroStage's normalised400px body. Replaced the
independent room/corridor cameras with scenery_camera.py: six-degree pitch,24-unit distance, reference feet at
y680, four-unit body400px after runtime overscan. Environment meshes use2x native scale. The asset floor uses a
shared world-UV convention and level native regional material. Removed uneven driving-road tiles from the actor
lane, independent farm/forest image zooms and forest_ground_dark foundations. Wall bases fade over.6 world units;
distant bases retain3. Source kit files were never modified and no generated art was needed.

Rebuilt24 private corridors as4096x768 and32 typed native rooms as2880x1080. Bounded corridor travel grows from
154 to2,227 virtual pixels without repeating/reflection. Wide PNG opt-in applies only to discovered corridor
files; decoded corridor allocation stays at4096x768 pixels, while existing room-pack bounds remain compatible.
Reprojected native fire emitters through the new room camera with the2x environment transform. The existing
dynamic fog/fire/smoke and frozen battle crop/lifecycle remain intact. Each corridor is still one baked plate;
its per-square scenery speed depends on corridor length. Physical foreground occlusion, water shaders and
native deferred lighting remain limits, rather than diffuse-texture shortages.

Verification:2 Python camera contracts and50 actual Blender projections pass; frozen Release builds with only
the three existing warnings;1,145 Core and190 UI tests pass. The ordinary unsaved four-region fixture passed
native actor travel, torch, curio/trap/obstacle/reversal, room battle and same-scene return (debug victories).
The final -dd3-crawl-motion-check passed forward/reverse/stop in all four regions over1,022/1,021/1,021/1,020
sampled frames, with maximum forward pan speed371.2-371.4px/s and no tile-boundary/reversal jumps. Native hunger
checks remain active; the fixture resumes an interrupted ordinary route and carries32 food. Its initial
reverse assertion was corrected after an ordinary automatic hunger meal stopped the route. A38-second native
recording and inspected contact sheet cover sustained movement. All56 plates were inspected offline; the
ordinary native test does not exhaustively visit every room variant.

Private output/evidence: local-art/scenery-contact-v04 (camera-repro.json,camera-verification.log,final pools,
manifests,emitters,native-visible,motion-native.log,continuous-walk.mkv,walk-contact.jpg,build source hashes).
Original top-level PrivateScenery/plugin/config/native-profile backup: backup-before-20261014-040721.zip;
additional intermediate backup and plugin-only pre-deploy snapshots are alongside. Final assets/plugin were
deployed only with DD2 stopped; hashes were checked. The first two native attempts were interrupted; hidden
window capture/lighting was unusable, so final QA used a visible native window without OS input. Owned final
QA PIDs22344 and24284 were stopped after their checks. Native SaveFiles39-file hashes remain unchanged; no DD3
estate was selected, read or persisted. Other Hamlet QA PIDs were left alone; no cross-chat message was sent.

Follow-on owner goal remains active: make extracted Sir Vilhelm playable as test content, preserving model and
native DS3 animations, including black-flame sword buff. His original assembled export has21 meshes,65,946
triangles,176 bones and Onyx Blade bound to R_Weapon. Local DS3 archives now privately supply c0000 common,
a0x/a1x/a2x,lo/md/hi andDLC1 animation binders;20 candidate clips were exported onto his original rig without
retargeting and rendered in Blender under local-art/vilhelm-v01. No Vilhelm game integration is implemented
yet. a026_003040 is a candidate sword-buff pose, not yet proven to be the black-flame action; do not label it
confirmed. Private TAE reference investigation and pose sheets are alongside. DS3 source files remain under
C:/Users/Piral/ds3-extract, never in Git. Keep independent Hamlet/Shieldbreaker work out of this commit.

## Round241 (2026-10-14): cliff forms, depth, service yards and native loading composition

Owner approved the Hamlet visual review. Preserve the ten v02 exteriors, their UVs, all service positions,
walking routes and camera. Civic/Abbey/Survivalist/eastern cliff transitions now have broken ledges instead
of smooth ramps. Nine faceted outcrops add 315 triangles, with bases buried under the lowest surrounding
ground sample. Foundation/route clearance filters exclude intrusive rocks. Continuous rock projection
grows from12 to16 world units, with the original rectangular atlas texel proportions retained.

HamletSetDressing adds17 native props in purposeful service groups: coach waiting shelter/trunk/lamp,
Tavern storage, Sanitarium courtyard supplies, Guild tools/chest, forge workyard, Nomad stock and an Abbey
approach lamp. Only installed visual components are copied, with no native shop/node scripts. Native model
geometry/UVs remain unchanged. Roof/wall normals select three restrained albedo tint bands; warm emissive
windows keep their original paint. Hover and locked feedback preserve those bands. No shared material edited.

Two static soft mist layers behind all services and a low foothill break separate the town from the Mountain.
The Mountain keeps its enclosing width500 placement with a small charcoal-blue lift. Paving is quieter/warmer,
gravel patches less busy, and foundations have stronger localized contact shadows. Baked entrance light adds
small amber pools without global bloom or brightening facades. Grass is shorter, duller and confined to verges:
50 clumps instead of168. Existing four chimney plumes and camera-motion control remain. Owned native scene
has173 renderers; no extra render targets or lighting passes are introduced.

Offline review caught missing vertex shade on the first outcrop preview and rocks sitting above steep edges;
the final mesh includes facet shade and samples surrounding ground for burial. Core checks also caught props
on the Survivalist path and rocks in foundations; moved/excluded those rather than weakening clearance.
Release passes with three existing warnings; frozen 1,145 Core+190 UI tests pass, including53 Hamlet cases.
The private preview re-reads actual Core geometry and verified GLBs, with no missing scenery.

Native unsaved checks pass all ten services/Escape, pan/zoom/reset, explicit full resource release/reload,
re-entry, Embark park/cache identity/resume,1600->1440->1600 resize, keyboard focus/Enter/back and still-camera/
smoke controls. The fixture also checks all services use facet materials and both distant mist bands exist.
Native final overview inspected; repeated URP reallocations remain0 over5.040 seconds on the final run.
This verifies buffer stability, not a frame-time/long-session benchmark. Existing v02 exteriors are unchanged;
bespoke facade/sculpt/upgrade/NPC work and saved expedition-results return remain separate acceptance.

Cold capture exposed round239's Canvas/IMGUI layering bug: an IMGUI black fill hid the native flame despite
the torch component being active. Move the opaque background into the torch Canvas; keep IMGUI labels and
retry controls above it. The same native capture timing now shows the torch. check_loading_capture.py is the
native screenshot regression:0 bright torch pixels before,5,508 after,100% opaque corner margin. Native fixed
screen inspected. measure_native.ps1 now accepts both supported Hamlet preview aliases, retaining its PID/
rendered-scene guards. No fake progress bar, elapsed delay or painted town fallback is added.

Private renders, captures, source/install hashes, pre-change binary backups and logs are under
local-art/ui-overhaul/hamlet-set-dressing-v13. Concurrent Shieldbreaker/scenery work stays outside this commit.
No estate selected/read/saved and no OS input sent. Owned QA processes closed; ordinary play is not reopened.

## Status 2026-10-14: round241 visual pass and cold torch composition accepted in unsaved native preview

The approved visual iteration is installed. The retained town/cache and cold torch now have native acceptance.
Real saved Homecoming/transactions/embark, full default-map/provisioner/estate-picker torch fixtures, controller
comfort, custom facade/NPC/upgrade art and full performance/memory work remain open. Preserve concurrent
Shieldbreaker/scenery files and their notes; continue from tools/handoff_prompt.md.


## Round242: approved Hamlet Mountain depth and building finishes, 2026-10-14

Owner approved thev15 native capture and authorized committing. Preserve the original darker native cliff
paint, grading,16-unit world period and760/960 atlas aspect. The Mountain now presents its detailed painted
face toward town with restrained charcoal-blue grading, low rocky foothills and two elevated mist layers.
Worn world-space paving margins blend into the ground across branches and the forecourt. Four service
exteriors gain fitted trim: Abbey coping/ridge/buttress caps, Sanitarium drip/hip courses, Guild doorway
trim/rivets and Tavern sign/lantern details. Original building envelopes and painted UVs remain intact;
six GLBs are unchanged. Selected slate/masonry grades improve separation without adding lighting passes.

The private54-file package is59.7 MB at local-art/ui-overhaul/hamlet-surface-v15/HamletModels. finish.py
rebuilds the fitted details from the verifiedv02 models; package.py retains its original format. All game
assets and derived model/paint outputs stay outside Git. No generated cliff texture is selected or packaged.

Frozen committed baseline plus this pass: Release passes with three existing warnings and54 Hamlet cases
pass. v14 full native services/Escape, controls, release/reload, cache park/resume, resize, keyboard and still
checks passed with1,143 Core+190 UI tests before the final native-rock revision. v15 unsaved PID24016 loads
all ten services/181 renderers; approved final overview inspected and0 repeated buffer allocations over
5.040sec. Buffer stability is not a frame-time or long-session benchmark. Review captures, packed offline
scene, source hashes and exact rollback backups: local-art/ui-overhaul/hamlet-surface-v15/review.json.

Review previews were closed and the exact pre-test plugin/model package/config restored. The approved
private package is ready for the next stopped-game deployment with current shared sources. This commit
does not redeploy while another agent's native test runs. No estate read/save or OS input sent. Saved
Homecoming/transactions, full performance and controller/text/window comfort remain separate acceptance.

## Round243 (2026-10-14): optional Sir Vilhelm, native motions and corrected sword mount

Owner requested the original DS3 model as a playable local test character, including black-flame ignition,
then reported a reversed sword axis. Separate sir_vilhelm class/stats and five independent skill aliases
borrow owned Crusader combat rules/resources without altering Crusader. Stagecoach invitation creates a
normal recruit, prevents duplicate living visitors and uses ordinary hiring. Guild mastery, equipment and
camping map to Crusader costs; the option controls invitations while existing visitors still register.
The public option defaults off; this installation now has Experimental/VilhelmHero=true. No Souls zone added.

Private conversion retains21 meshes/65,946 triangles/176 bones, original texture coordinates and12 c0000
Havok clips: stance/walk, three attacks, a245_038700 blackflame, three Great Heal phases, front impact and
death/corpse. The Onyx Blade mount rotates native negative-Y to positive-Z about R_Weapon, minus90deg localX,
without rotating hands/bones or changing animation tracks. Blender comparison and native cast/attack/idle
acceptance confirm the corrected mount. Native actor Timeline/controller clocks map contiguous antic/action/
recovery segments; recovery names containing action no longer replay the action. The blade inspection pose
is the native ignition action and returns to idle. Selected skill IDs drive mapping, not generic animation
names. Only active native material controllers apply to added meshes; inactive-controller NRE was fixed.
Borrowed performer particle clips are suppressed only for this visitor's own skills.

Native DS3 TAE/SpEffect/FXR evidence proves a245_038700, ignitionFFX440460 at0.566667-1.4s, event331 applying
130022400, chain130022401/45sec and persistentFFX440461. Original s11629 RGBA8x8 atlas is attached along the
animated weapon. Native Radiance buff presence/turn expiry currently drives the aura; this and the five
Crusader ability rule sets are an integration baseline, not final DS3 balance. Particle count/attachment was
observed, but visible flame quality remains [?]. The final material uses a privately loaded native DD2 flame
material, independent alpha and preserved native queue/blend states. Offline shader evidence establishes
UniversalForward/transparent, not EnvironmentTransparent's opaque weighted pass; that hypothesis was rejected.

Native acceptance before the owner's launch pause: all five real selected skills and eligible targets,
calculation/application, actual healing/buff expiry, original clips/three heal phases/idle returns, flame
emission through the next attack, transient coach recruitment and base/mastered canonical loadouts pass.
Survival healing is explicit QA setup. Earlier corrected-mount four-region travel/curio/trap/obstacle/reversal/
room fight/same-scene return also passed with debug victories. These checks do not prove final balance or
visible flame quality. No repeated ExportedHeroModel exception in the final action run; the known native
IMainInstaller quit callback exception remains separate. Latest action recording: native-actions-193423.mkv.

Offline follow-up replaces the extraction kit's specular-to-metallic PBR colors with original diffuse PNGs
for24/27 material definitions, retaining exactly identical geometry/animation bin hashes. Eight Blender poses
and a new portrait were inspected. A temporary second UV flip was rejected: GltfModel already flips V. It is
removed. Final diffuse/shader appearance, continuous Vilhelm walking, death/corpse presentation, actual coach
button comfort and saved recruitment/return still need native review. Cloth physics, full DS3 FXR simulation,
voice/audio and balance are not implemented. No generated art was used.

Final isolated Release+1,149 Core/190 UI pass. Combined shared snapshot, preserving Shieldbreaker WIP, builds
and passes1,152 Core/190 UI. Installed42 private pack files and both DLL/PDB pairs with hashes checked while
DD2 stopped; private model/config/plugin backups are under local-art/vilhelm-v01. Native SaveFiles39 hashes
remain unchanged; no DD3 estate was selected/read/persisted. No extracted assets/binaries/decomp committed.
Private provenance, Blender/native captures, native skill APIs, shader tree and build manifests are under
local-art/vilhelm-v01 and local-art/vilhelm-native-audit. See tools/vilhelm/README.md.

Owner requested no game launches for two hours at17:38:14 UTC. No game was running when checked; owned QA
PID32676 had already exited. No launch before2026-10-14 19:38:14 UTC /21:38:14 Europe/Paris. The owned launch
helper enforces this boundary; offline work only until then. Native final visual acceptance remains pending.


## Round244 (2026-10-14): native corridor depth, ground contact and distance-driven walking

Owner asked for more immersive DD2-faithful corridors, then reported characters sliding against the scrolling
ground. Work stayed offline during the explicit two-hour launch pause, ending at19:38:14 UTC /21:38:14 Paris.

Art and composition:
- Compared round240 native captures and current24 panoramas with installed DD1 Weald/Cove layers and the
  allowed Unity port's hall composition. Retained native DD2 meshes, diffuse paint, UVs and stage projection.
- Composed24 complete4096x768 fallback plates plus72 RGBA companions. Back/middle/front travel at18/48/100%.
  Only one corridor's depths load:36 MiB steady decoded companions, worker IO, one shared upload per update,
  complete-set adoption and release on hall/region changes. Missing/corrupt depths keep the complete plate.
- Local geometry contact renders into native floor paint at12 samples. Near floor paint is20% quieter. A56px
  horizon fade replaces the160px blue-mist gap beneath near scenery. Excluded empty Tangle hill foundations
  and lowered raised foliage bases; rerendered all six forest layouts. No generated textures or kit mutation.
- Partial renderer passes retain other scene records. Private manifests hash every source pass and output.
  Reviewed all24 end/centre/end crops and four-region before/after/motion comparisons. Whole scenery never
  repeats or reflects. Translucent covers use one complete image, avoiding separately faded wall overlaps.

Sliding diagnosis and correction:
- The deterministic crop/planted-foot loop reproduced five failures in7ms. Old gait used normalized held
  input and a fixed1.28s cadence while panorama distance changed with hall length. Independent0.14s speed
  smoothing delayed stops/reversal, and the0.1s animation time cap lost travel on frame hitches.
- CorridorSceneMotion now shares button-step easing, held travel and rendered camera displacement with the
  scene, particles and gait. Each native rig converts virtual pixels through fitted leg length/facing.
  Phase consumes actual distance; upper-body pose may blend while planted contacts stay on ground targets.
  Tile carry, art adoption, room changes and return baselines cannot create a teleport stride.
- ViewProgress presents forward travel toward screen right from either entrance without reflected UVs.
  Backward input reverses immediately. Optional imported-hero playback uses signed distance and retains its
  pose on reversal. Sprite bob also follows travel distance. The native fixture now checks planted-foot
  target error within6 virtual pixels; actual native acceptance remains pending.

Validation and installation:
- Frozen HEAD74fcb39 plus only this round: Release,1,167 Core and203 UI pass. Frozen current shared sources,
  preserving independent Shieldbreaker work: Release,1,170 Core and203 UI pass. Six depth/composition tests
  and two camera cases pass. Red/green locomotion evidence and all frozen source hashes are retained privately.
- With DD2 stopped, installed101 hash-verified files:24 canonical corridors,72 companions,manifest and four
  plugin/debug assemblies. Other93 private scenery files, including rooms/HamletModels/emitters, preserved
  byte-for-byte. Shared source hashes checked before deployment. No config/save/estate/profile accessed.
- Rollback: local-art/scenery-depth-v05/install-backup-20261014-190827.zip. Installation/source/provenance
  records, review-final/comparison.jpg and motion-comparison.mp4 are under local-art/scenery-depth-v05.

Limits and next native check:
- No game launched. Keep the owner's pause through21:38:14 Paris; afterward use unsaved fixtures, never an
  ordinary/protected estate. Check native walking pace/contact, stop/reverse, both entrances, torch/props,
  same-scene battle return, upload timing and the hero camera's brightened-frame timing.
- Backdrops remain painted2.5D with approximate water/lighting and no foreground actor occlusion. Per-square
  background travel still varies with corridor length. True floor/prop world projection needs further work.
- Private imported Vilhelm walk audit found only~2.4mm forward foot excursion over its2.4s source clip, too
  little for credible stride calibration. Its true locomotion source still needs mapping; no new private
  hero model/rig assets were installed. Only its distance clock/reversal was corrected in this round.

## Status 2026-10-14: round244 installed and verified offline; native visual acceptance deferred

Native DD2 corridor depths/ground contact and the owner's walking synchronization fix are installed with
rollback and tests. Native acceptance is[?] until the explicit launch pause ends and the unsaved checks run.
Independent Shieldbreaker source/test/art work and its journal entries remain uncommitted and untouched.


## Round245 (2026-10-15): enclosed native corridor verges and grounded woodland

Owner requested less flat repeated ground and more enclosing detail. Rebuilt18 corridors, six each for
Tangle/Foetor/Shroud, with original DD2 meshes, painted textures and native UVs. Rear scenery moves three
native units closer. An independently seeded low verge row adds irregular ferns/roots, roadside scrub,
well/fence fragments, rope posts and shore debris while leaving the level actor lane clear. Tangle verge
meshes retain only foliage faces; Shroud dead-fish parts retain only their native material faces. Large
black barnacle mounds and low-detail cliff cards were rejected after camera-aligned comparisons.

Restored native dark earth banks behind distant Tangle skirts. Dropping their banks had left raised tree
bases hanging in rectangular silhouettes. Near driving foundations remain excluded. Broad restrained
value variation and new geometry contact break up native floor bands without changing hue or UV scale.
Stage projection, hero scale, gameplay travel, room art, emitters and Sprawl's24 image files are unchanged.
No generated bitmap art, source-kit mutation or new runtime texture allocation was added.

Six composition and two camera tests pass. All18 verge alpha footprints clear the y680 hero contact;
their lowest significant source pixel is y692, above the y725 source footline. End/centre/end crops for
all24 variants and three regional before/after comparisons inspected. The unsaved native preview21976
rendered all24 corridor choices through the actual loader/depth draw and logged PASS; regional captures
with DD1 reference figures were inspected. This establishes scenery rendering, not 3D hero locomotion.

Art-only stopped-game installation changed49 hash-checked files and preserved143 other scenery/plugin
files, including rooms/HamletModels/emitters/Sprawl. Rollback zip, source-pass/output hashes, native logs,
captures and comparisons: local-art/scenery-verges-v06. Plugin assemblies remain the verified round244
shared build. No estate was selected/read/persisted and no keyboard/mouse input was sent.

Hidden startup windows prevented native hero samples in preliminary motion checks. A normal visible
launch10508 produced4122 visible samples and four real rigs, then the motion assertion fired during the
discrete doorway transition. Steady-route acceptance needs a separate transition phase in the fixture.
No native gait PASS is claimed. All owned preview/motion games are closed; DD2 is stopped.

## Status 2026-10-15: round245 corridor art installed and native preview verified

The requested enclosed corridor pass is ready with complete rollback. Round244 native walking pace/contact,
stops/reversal, in-place battle/return and imported Vilhelm stride still need acceptance. Keep independent
Shieldbreaker changes and notes outside this commit. The October14 launch pause has expired; use owned
unsaved fixtures and never automatically open an ordinary/protected estate.


## Round246 (2026-10-15): native walking contact reach and final corridor acceptance

The visible native run reproduced a real planted-foot miss after separating the fixture's doorway
transition from its steady-route measurement. The first timing assertion had included Travel's discrete
.3s ease in a constant-speed bound. Motion QA now waits .4s, then samples only forward/stop/reverse phases.
No gameplay speed, camera projection or door behavior changed for this fixture correction.

Ranked contact hypotheses were retained combat stance beyond leg reach, rig/solver scale mismatch and an
oversized first camera delta. A targeted native probe confirmed the first: Plague Doctor right contact,
body blend .657, requested hip-to-foot2.408 units versus available2.231, error8.398px. The frame traveled
.031 leg lengths. Raising the pelvis while retaining the combat foot spread forced IK to clamp its foot.

Both world-space foot targets are now calculated first. Their current leg lengths/horizontal reach
constrain one common pelvis height before either leg solves, with2% knee reserve. Normal settled gait
needs no extra drop. This preserves contact positions through the first stance instead of dragging the
foot when the body rises. The measured reach case reproduced a failing regression before the correction;
all204 UI cases now pass. Shared Release passes with three existing warnings; unchanged Core passes1,170.
Temporary DEBUG-dd3-contact instrumentation was removed before final build/deployment.

Final owned unsaved native23528 passes continuous forward/reverse/stop in Sprawl/Foetor/Tangle/Shroud.
Per-region observed frames1,017/1,020/1,022/1,018, total4,077; maximum pan speed371.3 virtual px/s. Planted
native foot targets stay within6 virtual pixels and no camera jump occurs. Native real-actor regional
captures inspected. The earlier24-choice background/depth preview PASS remains valid; all190 scenery
files were hash-preserved across plugin deployment. Source/build/install hashes, plugin rollback zips,
red/green reach evidence, native logs/captures and video remain in local-art/scenery-verges-v06.

All owned games are closed and DD2 is stopped. No estate was accessed or persisted; no keyboard/mouse
input was sent. Independent Shieldbreaker source/tests/art/notes remain outside this commit. The final
shared plugin preserves them. Remaining acceptance: native both-entrance visits, discrete button gait,
other heroes/hall lengths and cadence comfort, GPU frame-timing precision, torch/props/battle-return on
the new depths and imported Vilhelm's true stride. Painted depth/water/occlusion limits are unchanged.

## Status 2026-10-15: corridor art and four-region native continuous walking verified

Rounds245/246 are installed with rollback:18 improved corridors, all24 native background/depth previews,
and four-region real-actor forward/reverse/stop/contact checks pass. No game remains running. Preserve
the independent Shieldbreaker work; continue from tools/handoff_prompt.md for the remaining scenarios.


## Round247 (2026-10-15): break up repeated Tangle ground and close the forest

The owner's forest-04 screenshot exposes the driving road's parallel brown strips repeated into
depth. Low-frequency value variation alone retained that structure. All six Tangle corridors now
blend original forest_road_roots_01 paint into irregular patches at a different world period:
18.6 staged units versus the road's eight. Coverage increases behind the walking lane. Native tint,
ink, local geometry contact and restrained value variation are retained; no new bitmap art is used.

Nearer individual trunks use selected native tree materials from forest_tile_06,
forest_tile_brambles_02, forest_tile_01 and forest_tile_02, with varied scale, yaw and depth and no
consecutive reuse. Whole entrance/root clusters were rejected after full-crop review because their
silhouettes repeated too visibly. Placement grounds the actual retained vertices after rotation,
instead of the discarded driving foundation. Smaller forest_tile_11 foliage fills the root gaps.
The native mesh UVs, existing large verges, distant earth banks and skyline remain intact.

Private evidence/pack/installer/QA helpers: local-art/scenery-woodland-v07. Twelve images change
(six canonical plates and six front companions);84 other images are byte-identical. No runtime
objects, layers or memory budget are added. Camera, y680 actor lane, gait, rooms and other regions
are unchanged. Eight Python composition/camera cases pass. All six variants at both ends and centre
were inspected; the full-width y676-720 near-geometry alpha band is empty. PNG bounds/alpha/source
hashes and retained-material mesh bounds pass. Source assets and derived artwork remain outside Git.

The private pack and stopped-game rollback installer are ready but NOT installed.
Another chat is actively testing Vilhelm in DD2; the owner was asked before interrupting it.
No game was launched or stopped by this round. Recheck processes before deployment. Native acceptance
is pending: install while DD2 is stopped, run the unsaved corridor preview, inspect the six Tangle
captures and close only that owned PID. No estate was accessed or persisted.

This art-only round does not repeat or expand round246's native locomotion acceptance. Existing
both-entrance, discrete-button, other-hero/hall-length, GPU-timing, torch/props/battle-return and
Vilhelm-stride acceptance remains separate. Native albedo and approximate haze still differ from
DD2's full deferred lighting. Independent Shieldbreaker/Vilhelm source, tests, art and notes stay
outside this commit.

## Status 2026-10-15: Tangle ground/enclosure ready; deployment/native preview pending

Round247's six-variant native tree/root/undergrowth pass is ready in local-art/scenery-woodland-v07.
Keep the current install until DD2 stops or the owner authorizes closing the other chat's test.
Then run install.py and the unsaved preview; update PARITY after inspection.
Preserve independent Shieldbreaker/Vilhelm work and check live process state before any deployment.

## Round248 (2026-10-15): native Vilhelm walking and visible blade Blackflame

The native DS3 Move behavior selects a002_020000. The previously imported 004000
pose has only 2.8 mm forward foot range; the actual 41-frame walk has 0.882 m and
a 1.333333 s cycle. Extracted hkaDefaultAnimatedReferenceFrame samples establish
2.0000007 m of root travel. The private pack now contains 13 clips and motion.json.
Signed rendered ground travel, divided by fitted source scale, advances/inverts
that original cumulative curve. Stops/reversal preserve phase; no animation or
hand tracks are rotated. Geometry/nodes and the previous 12 animation accessor
payloads remain byte-identical. Installer carries the motion calibration, and the
runtime rejects a new walk clip without valid finite/increasing calibration.

DD2's painted hero shader lacks DS3's separate specular contribution. An owned
runtime diffuse copy applies a bounded tone curve, retaining original PNGs, UVs,
detail and alpha. All 24 available native diffuse material maps are hash-verified.
Blackflame uses the original s11629 RGBA atlas with separate near-black/pale layers
following the corrected weapon mount. The native DD2 torch particle material is
loaded from its actual Addressable location and retains independent RGBA alpha.
Its shader already has a UniversalForward pass/queue 3000; changing an imagined
OIT queue was not the cause. Full DS3 FXR simulation is still approximated.

The borrowed cast's gold outline persisted because DD2 applies material controllers
at beginContextRendering, after LateUpdate, and uses BOTH _RIM and _SOFT_RIM.
Actor-local native material overrides now disable both only during Vilhelm's own
Blackflame cast, and are removed on idle/release. Direct property-block/keyword
writes and testing only the hard-rim keyword were insufficient. An opt-in cast
fixture records five exact rendered ignition frames and checks both keywords after
rendering. Final frames show dark flame/pale fringe on the blade and no gold rim.

Shared Release passes with three existing warnings; 1,171 Core and 204 UI cases
pass, including nonuniform native-distance forward/stop/reverse/wrap calibration.
Native five-skill selection/targeting/application, original motion phases/idle
returns, flame buff expiry, base/mastered loadouts and transient coach invitation/
ordinary recruitment pass. Four-region travel/torch/curio/trap/obstacle/reversal/
room fight/return passes using debug victories, with no balance claim. The final
rim-only correction additionally passes native cast/application/idle/render checks.
Sustained imported walking passes forward/reverse/stop in Sprawl/Foetor/Tangle/
Shroud over 834/843/849/844 observed frames (3,370 total), no camera jumps, maximum
pan speed 371.3 virtual px/s. The fixture requires the original calibration and an
updated imported pose each frame. Native movement recordings/captures inspected.
The six-pixel planted-foot bound measures DD2 rigs, not Vilhelm's imported bones.

Private evidence, frozen shared build/source hashes, model verification, captures,
native logs/video and rollback backups: local-art/vilhelm-v01. Final acceptance
manifest: final-acceptance-20261015.json. Installed four plugin artifacts/43 model
files match the tested build; all 403 frozen shared source files still match.
39 native profile files are unchanged. Local invitations remain enabled; public
default remains off. All owned QA PIDs are closed, last 23092; DD2 is stopped.
No estate was selected/persisted and no OS input was sent. The October14 launch
pause had expired before these tests. No generated artwork or game assets enter Git.

Remaining: full cloth/FXR/audio and final combat rules; native death/corpse visuals,
saved recruitment/homecoming transactions and broader motion comfort. The original
extra armor/cloth bones still follow reference/parent poses. Round247's pending
Tangle pack was not installed by this round. Independent Shieldbreaker work/notes
are preserved and remain outside this commit, though present in the shared build.

## Status 2026-10-15: Vilhelm locomotion/Blackflame installed and native verified

Round248 is installed with rollback and locally recruitable at the Stagecoach.
Five-skill and four-region native checks pass; all owned games are closed.
Continue from tools/handoff_prompt.md, preserving other agents' work and checking
live processes before deployment. Round247's Tangle art installation remains pending.


## Round249 (2026-10-15): native ground/enclosure treatment across all four regions

The owner approved round247's Tangle and requested the same treatment for all regions.
All six Sprawl, Foetor and Shroud variants now break driving-road repeats with irregular
patches of native regional paint. Their patch distribution/UV phase also varies by corridor.
The eight-unit underlying road UVs remain intact. Detail periods are14.0 units for Sprawl,
18.2 for Foetor and17.0 for Shroud. Approved Tangle paint/geometry/images are unchanged.

Sprawl uses road_rough_patch_darker from its urgent-repair arena, closer masonry,
selected broken arches and sparse native camp crates/barrels. Foetor uses farm_footpath,
nearer native crop banks/wilted sunflowers, occasional dead trees, low walls and scrub.
Shroud uses coast_road_01_mix_alt, wrecked boats/mooring posts/ropes and small native
fish remains, retaining open sea views. A flesh-covered Foetor floor and orange camp
props on the cold coast were rejected in offline comparison. Selected mesh faces keep
native UVs; true retained bounds after rotation anchor them behind the actor lane.
Ground contact is rerendered with the added scenery. No source assets are modified.

Private complete pack, native source/placement/output hashes, regional comparisons,
all24 variants at start/centre/end, installer and exact rollback: local-art/scenery-regions-v08.
Relative to approved v07,36 canonical/front images change and60 image files are identical.
All24 approved Tangle image files and every back/middle companion remain byte-identical.
Camera, y680 lane, rooms, depth travel, gait and runtime texture budgets are retained.
No runtime scenery objects/layers are added. No generated artwork or game assets enter Git.

Eight Python composition/camera cases and all24 full-width y676-720 clear-footline checks
pass. PNG dimensions/alpha/16MiB bounds, source hashes and selected-mesh lane bounds pass.
All24 end/centre crops were inspected. Stopped-game installation changes49 files,
including the previously pending Tangle pass, and hash-preserves145 scenery/plugin files.
The four current DLL/PDB artifacts, rooms and Hamlet models remain intact. This round
does not rebuild or deploy a DLL; round248's shared plugin acceptance is retained.

Owned unsaved native preview14208 passes all24 corridor choices and all24 depth sets.
Every native capture was inspected with the fixture's DD1 reference bodies. No new
scenery errors/exceptions appear in its log. The exact owned PID was closed, DD2 is
stopped, and final installed/preserved hashes match. No estate was accessed/persisted
and no OS input was sent. Round247's installation/native preview gap is now resolved.

Native locomotion/torch/props/battle-return acceptance remains scoped to the earlier
fixtures; this background preview does not repeat those gameplay checks. Original
albedo, approximate haze and coarse native bank/water foundations remain art limits.
Preserve independent Shieldbreaker source/tests/art/notes outside this commit.

## Status 2026-10-15: all24 corridor variants installed and native preview verified

Round249's four-region pack and approved Tangle are installed with exact rollback.
All24 actual loader/depth choices,24 clear footlines and eight Python cases pass;
native regional captures inspected. Owned preview14208 closed; DD2 stopped. No estate
or OS input accessed. Active pack/evidence: local-art/scenery-regions-v08. Older pack
installers can restore older regions; use v08 for the current complete pack.
Preserve independent Shieldbreaker WIP and the installed round248 shared plugin.

## Round250 (2026-10-15): end-to-end walkthrough and curio generation audit

Owner requested gameplay/graphics weaknesses, then specifically curio generation and smoothness.
This round is an audit, with no production code/art changes. Private instrumentation compiled in
a frozen shared copy, an object-owned persistence guard and a private checkpoint path permit real
campaign APIs/UI without selecting an estate. Native menu input uses synthetic estate summaries.
Evidence, temporary source/build, original plugin backup, logs,45 native captures, native state and
acceptance.json: local-art/walkthrough-audit-20261015. The temporary audit plugin was removed.

Native walkthrough passed recruitment by drag, Guild/Blacksmith inspection, map selection/camera,
four-hero formation, Shift purchases, cancellation/reopen with empty cart/same quest/party/gold,
ordinary embark, native hall and room victories through skills, trap/torch, a cold checkpoint resume
with pending loot, ordinary curio interaction/reward, camp/meal/skill, retreat confirmation/results,
return to Hamlet/town event and trinket drag/equip. No forced victory or debug health refill.
Private resume preserves position/light/steps/wins/pack/pending loot/party condition, with no duplicate
reward. Null object fields differ between SaveFile and observer serialization and were normalized;
null array slots remain meaningful. First expedition deliberately retreated, second stopped at
the inventory counterexample. Successful quest completion, bosses/tier/Path balance, normal estate
picker file handoff and long sessions are NOT established by this audit.

Confirmed defects, in repair order:

1. P1 activation-quest inventory: affordable native16-stack cart (7,450 gold) enters Shroud with20
stacks. Firewood is at slot16 and three protective wards at17-19; CrawlUi draws only0-15. Discarding
four visible partial stacks leaves four visible holes but all wards remain hidden. Inventory.Arrange
keeps existing overflow indices; it does not move them into those holes. InteractQuestCurio requires
the explicit quest-item key, so the ordinary item-use path cannot complete the objective. Core
forest probe separately produces16->21 stacks with three hidden antivenom quest items. Fix starting
item reservation/capacity and layout recovery together. Relevant: Campaign/Embark.cs114-120,
Expedition/Inventory.cs164-193, Ui/CrawlUi.cs714-744. Native captures43-45 and full-pack checkpoint.

2. P2 camp picking/presentation: Driver keeps native models at corridor RankX while CrawlUi moves
bars/click targets to CampX. Real click on the standing Plague Doctor selects the Vestal, confirmed
by HUD/skill header in capture31. Bodies remain standing beside the fire. Backpacks in campfire.png
are supplies, not duplicated seated heroes. Fix shared native camp formation, hit regions and poses.

3. P2 quirk pool: suit_of_armor grants quirk_kingdoms_heal_wounds_faster_at_camp_pos in an EXPEDITION
host; native CSV requires generation_all_conditions=is_kingdoms. Reynauld also starts with
quirk_kingdoms_weak_debuff_vs_siege_neg. Dd2Catalog.MapDd1Quirk/StartingQuirks choose by polarity/tags
without native mode-generation conditions. UI displays internal-derived Kingdoms names. Filter
supported campaign effects/mode conditions and localize; do not assume every imported effect works.

4. P2 food cap: five immediate real-pointer food uses heal Reynauld8->18/40 and consume24->19 food.
Native shared/rules.json max_provisions_before_full=4 is not implemented in Crawl.UseSupply.
The cap/reset policy needs native reference work before a fix. Native captures22/25 and assertions.

5. P2 curio hover: armour becomes an opaque pale silhouette under the pointer, then its painted
detail returns when the pointer leaves. Captures26/27. DrawProp replaces the closed picture with
the active picture; inspect state compositing/alpha rather than generating replacement art first.

6. Gameplay compatibility gaps confirmed in code: purchased bandage/antivenom/laudanum are not
bridged to native combat items. Crawl.UseSupply treats only food/holy water and says the others
wait for curios. Curio/trap poison/bleed effects apply full duration as immediate damage through
CurioResolver.ApplyEffect; there is no travel DoT treatment window. These reduce preparation and
counterplay. Ordinary native skill healing works; do not confuse that with combat supply support.

Curios are rolled from paired DD1 weighted room/hall/treasure prop tables when the seeded map is
built. Quest curios are placed afterward in eligible rooms, then hall squares. Outcomes roll on
interaction and pending reports persist. Stress audit generated14,400 configuration/seed cases
across four default regions, three lengths/six types/200 seeds: zero disconnected maps, missing
goal placements, null curios, missing definitions or missing art folders. File availability does
not establish correct native states.3,844 cases contain adjacent identical ordinary hall curios,
4,497 pairs, maximum run3, including three stacks of books in city short/explore seed2. These are
unweighted stress configurations including map fallbacks, NOT a campaign frequency estimate.
Independent weighted picks have no anti-repeat/spacing rule. Theme/art remains paired DD1 props.

Other visual weaknesses: static DD1 service/hero/inventory/results panels differ sharply from the
3D Hamlet/map, small low-contrast text, flat camp/prop composition and baked scene lighting versus
live actors/effects. Cold initialization shows17.75/18.23s Hamlet and19.33s expedition-resume frame
stalls in the instrumented audit; these need an uninstrumented profile, not a production benchmark
claim. First native fight setup/reveal takes38/1,167ms with the same backdrop, without a load screen.

93 targeted curio/map/quest/inventory tests pass, though they miss the counterexamples above.
All owned QA games closed, last27584. Four original DLL/PDB artifacts/config restored byte-exact;
190 scenery files and39 native profile files hash-preserved. Protected estates1/3 and estate2
were not accessed; only the private checkpoint was written. OS input went only to the owned game
after21987s desktop idle was verified. Independent Shieldbreaker code/art/journal WIP is preserved.

## Status 2026-10-15: audit complete; hidden activation items are the first repair

Installed round248 plugin/round249 scenery restored and DD2 stopped. No audited bug is fixed by
these notes. Start with a failing full-cart Embark.Create/Arrange/UI-visible-inventory regression,
then camp formation/picking and quirk generation filters. Preserve independent Shieldbreaker WIP.

## Round251 (2026-10-16): walkthrough repairs and repeat checks

Fixes the round250 inventory, camp picking, quirk generation, food and curio-hover findings.
Embark now reserves the actual free supplies and activation items before charging for a cart.
The provisioner previews that combined pack, limits purchases to16 slots and leaves the paid
cart unchanged on a failed departure. Existing overflow fills visible holes; larger legacy
overflow is paged without discarding goods. Native legacy-pack recovery exposes firewood and
all3 wards after4 visible holes are emptied.

Camp models and mouse regions share HeroFormation. Native inn poses face the fire; entering
and leaving camp retains each native actor instance. All4 actual body clicks select the right
hero. Food heals5% with the native4-provision cap. Five real clicks on a wounded40HP Crusader
consume4 food and heal20->28; the fifth is refused. Battle completion/flee resets the cap.
DD1's16 bounded provisions_consumed offset references confirm normal battle cleanup and a
wave reset; they do not justify a camp/timer reset. No guessed reset was added.

Future quirk rolls filter native class/mode conditions and fully resolved inactive payloads.
47 pure coach/inn/route/affinity payloads are excluded. Mixed/unknown payloads remain eligible,
and Resolution retains its real DD1 tavern restriction. Native localized titles replace
internal-derived labels. Existing saved quirks are retained. Curio hover uses painted closed/
open art and a restrained tint, rather than the opaque active-mask silhouette. Native armour
hover still needs a dedicated capture after the source fix.

Curio/trap bleed and blight become persisted travel stacks with native resistance and the
DD1 .5-turn-per-hall-square progress. Bandage/antivenom and camp cures remove matching stacks
before further travel damage. Native combat imports each stack once and exports remaining
duration before teardown; burn/horror stay native combat-only. Actual native bandage and
antivenom targeting, cure/heal, invalid healthy target, exact shared-pack charge and one-item-
per-hero-turn refusal pass. Travel treatment removes2 remaining bleeds with1 bandage. Private
status/wound seeding is labelled as fixture setup, not naturally generated campaign coverage.

An unfinished fight restarts both teams and now rewinds pack quantities/layout and travel
stacks with HP/light. Legacy checkpoints lacking those snapshots keep their current records.
A retained native item slot cannot apply a free cure after another hero spends the last shared
item; query/target guards complement the ledger. Restart JSON/copy regressions and stale-slot
Core regression pass. The new native stale-slot query guard still needs final-build acceptance.

Exploration DoT wipes now finish as defeat, suppress queued fights and recover no pack goods,
including after an objective was already complete. Mortal-party travel and completed-goal
wipe regressions pass. Hall curio spacing preserves counts and quest locations, moves ordinary
adjacent stops into isolated holes where possible, and re-rolls repeated neighbours from the
weighted native table. The repeated-city-seed regression and14,400 stress cases pass with0
ordinary adjacent identical pairs, missing objectives, disconnected maps, missing definitions
or missing art folders. This is not a campaign-weighted probability estimate; paired DD1 prop
themes and total curio density remain unchanged.

Service, hero, stash and results panels use scoped native DD2 fonts/painted chrome, brighter
text and the same measured style for wrapping/tooltips. Native transaction, equipment drag
and results-layout acceptance remain pending on the final build. Hamlet terrain and material
grading move plain RGBA loops to cancellable workers, with bounded main-thread readback/upload
and reveal after completion. Old scoped native baseline: BuildGround17,814ms and independent
frame gap17,899ms. Byte-identical portable terrain/grade references and actual Paint.cs scheduler
checks pass. This does not establish native post-fix performance; cold/return profiling remains.

Frozen shared Release,1,216 Core and207 UI tests pass. Private evidence, source snapshot and
native captures01/04-13: local-art/walkthrough-fixes-20261015; combat/native DD1 references:
local-art/combat-supplies-20261015. A native hall fight completed through ordinary skills, with the
medical/camp cases above. End-to-end successful quest completion, bosses, final balance and
normal estate-file handoff are not established by this pass.

Owned native23124 was checkpointed and closed for the art/gait chat's QA window. Original4
production DLL/PDB artifacts/config restored byte-exact;190 scenery files and39 native profile
files hash-preserved. No estate selected/read/written; only private checkpoint data. The owner
explicitly authorized active-desktop QA input, sent only to the owned game. Independent
Shieldbreaker and corridor art/gait changes are excluded from this commit and preserved.

## Status 2026-10-16: repair sources verified; final native acceptance and room floors underway

Round251 repairs are committed. The production build was restored before the other chat took
the QA/deployment window. Keep DD2 closed until that chat releases it. Next: final-build
native stale-item guard, successful ward/results/return/panel flow, cold async Hamlet profile,
and the owner's newly reported repeated room ground. Room-only renderer/preview work is
separate from the other chat's corridor gait/Shroud-water work. Do not restore an older scenery
baseline after another chat installs its approved pack. Preserve protected estates1/3 and all
independent WIP; use the private activation checkpoint for the final flow.

## Round252 (2026-10-16): readable corridor steps and Shroud water detail

The owner's rapid-step report reproduced in production-source tests. The old
0.46-leg contact arc advanced only 0.371 projected leg lengths per step. Short
halls also swept the entire panorama in too few squares: a two-square route could
pan at 1,856 virtual pixels/s, while the 0.3s button ease peaked at 2,227px/s.
The contact arc is now 0.90 with the existing 62% stance, about 0.726 projected
leg lengths per step. Signed rendered travel still drives phase; the solution
does not cap cadence independently of the moving ground.

Short halls use a centred span, (tileCount-1)/max(9,tileCount-1), at the same art
scale. Button steps share the ordinary 1.2s walking duration. A second button
press cannot reset an unfinished ease; held movement waits for that accepted
step, while continuous square crossings keep their signed carry. Native repeated
button guards pass in every region.

Initial native runs exposed a second bug during long frames and stop/restart:
the sampled poses could skip a complete foot lift/landing but retain the old
planted contact beyond the leg's reach. A measured 1.161883-leg frame reproduced
the failure. Contacts now track unwrapped signed phase and start a new landing
after a missed swing. Restart phase follows the currently observed leading foot;
initial swing offsets, reverse travel and lift blending preserve the observed
pose. Existing two-contact pelvis fitting and native weapon-grip handling remain.
Regression cases failed on the previous implementation and pass after the fix.

Exact installed round248 sources plus gait changes pass Release, 1,176 Core and
219 UI tests. Shared Release, 1,216 Core and 219 UI also pass with deployment
disabled. Read-only native instrumentation checks four DD2 actors in Sprawl,
Foetor, Tangle and Shroud over 3,804 observed motion frames. Forward/reverse/stop/
restart and repeated button cases pass: maximum 2.770 steps/s, 206.3px/s and
0.195px planted-target error, no duplicate actor update. Regional captures and
sampled frames of the 55s native video were inspected. The final production build
has identical runtime sources without the temporary probe, and is installed.

The Shroud's native ocean material has cloud/foam maps rather than a base-colour
texture. A private export resolves both 512x512 maps through the shared bundle,
retains original colours/properties and records provenance hashes. The optional
renderer samples native masks at different world periods and per-corridor phase,
adding restrained water detail. This is baked surface paint, not Unity's animated
ocean shader. Omitting --ocean retains the earlier flat-colour recipe.

The complete v09 pack changes only six Shroud canonical/front pairs: 12 PNGs;
84 PNGs remain byte-identical to v08. Back/middle depths, other regions, front
alpha and source pixels from y650 downward, including the walking surface, are
unchanged. Eight Python cases and all 24 clear-footline measurements pass. With
the final production plugin, all 24 unsaved native choices/depth sets render and
all 24 captures were inspected with DD1 reference bodies. This loader/art fixture
does not substitute for the separate native gait acceptance above.

Deployment changed four plugin artifacts first, preserving 191 scenery/config
files; the art install then changed 13 files and preserved 181 scenery/plugin
files. All 195 final scenery/config/plugin hashes match. Plugin deployment uses
the verified previously installed source snapshot, preserving its Shieldbreaker/
Vilhelm state and excluding the other chat's unfinished room/UI/combat deployment.
Round251 source repairs still need its final combined native acceptance.
Private source hashes, red/green cases, final logs/captures/video, completion
records and exact rollbacks: local-art/gait-cadence-v10 and scenery-coast-v09.

Owned native QA games are closed, last preview23628; DD2 is stopped. No estate
selected/read/written and no OS input sent. The owner authorized coordination
with “Review repo for menu rework (2)”. Its old manage.py restore must not run
after this installation: it restores old DLLs before its old scenery assertion.
Future experiments must capture a fresh baseline. Independent Shieldbreaker and
room renderer/preview/journal WIP are preserved outside this commit.

## Status 2026-10-16: gait fix and current corridor pack installed

Round252 is installed and verified; active complete scenery pack is v09. The
game window is released through the authorized handoff. Next round is253. Other
chat continues room floors and final acceptance/deployment of round251 sources.
Remaining gait scope: owner comfort review, other heroes/hall lengths, ordinary
saved campaign and interaction/battle handoffs with this cadence. Vilhelm retains
his separately verified imported root animation; this run uses native DD2 actors.
Preserve protected estates1/3, installed artifacts and all independent WIP.

## Round253 (2026-10-16): native room-floor correction and final walkthrough repair acceptance

The repeated room floor was baked into the PNGs. RoomPanoramaUi already draws one full-frame
plate, and native room choices retain their orientation. The old renderer extended every arena
with a complete driving-road/board atlas repeated every8 staged units. Tracks, skulls, planks
and the distant plane edge became a regular grid. tools/room_floor.py now uses native regional
rubble, footpath, root and worn-board paint, with three independent periods, broad irregular
joins and an arena-specific phase. Native arena meshes/UVs, camera, footline and room roles stay
unchanged. Geometry contact and distance haze join the extension to the arena. No generated art
or new extraction was needed. Renderer completion records now invalidate stale/interrupted passes
by source/tool hashes. tools/room_art.md records the recipe and limits.

All32 rebuilt room plates pass native provenance, RGB/8:3/16MiB loader checks. Largest PNG3.89MiB.
All50 actual Blender camera projections pass. The new opt-in -dd3-room-check fixture runs only
rooms. All32 actual DD2 frame captures, eight per default region, were inspected with DD1 reference
bodies at y680. Live DD2 bodies and the activation prop were also inspected in a Shroud room.
This does not establish every native actor/room combination or original DD2 encounter parity.
Room-only install replaces32 PNGs and their manifest;162 other scenery/plugin/config files were
hash-preserved at installation. The active v09 corridor pack and verified round252 gait remain.
Private candidate, source manifests, comparisons,32 native captures, logs, installer and exact
room rollback: local-art/scenery-rooms-v10. Extracted artwork stays outside Git.

Round251 final native acceptance now covers the shared-supply query guard, ordinary bandage use,
targeted quest completion/results/return, campaign panels/equipment drag and asynchronous Hamlet
paint. Actual native one-charge item equipment rejects skill/target queries once the last shared
item is spent through a seeded other-hero ledger receipt. This is a native query-seam fixture,
not an end-to-end second-hero click. A separate real bandage click/target cures bleed, heals the
Vestal20->25 and charges2->1 bandages; a second item on that actor's turn is refused.

A private late-quest fixture seeded two of three ward activations, then used the normal curio/
inventory UI for the last one. The wrong torch does nothing and remains8. The lantern consumes
1->0, objective2->3, and the normal completion/results flow grants4500gold/5crests, reports the
fallen Plague Doctor, returns to the rendered Hamlet showing Week15 (stored index14). Both results pages,
Guild window, hero sheet, stash and actual unequip/drag-to-equip cycle pass. A wounded/status-
seeded native fight retreated with three survivors after23 ordinary skill actions. A later fresh
fixture wins its hall fight through27 ordinary actions with one casualty. These automated choices
and seeded conditions do not validate full-quest difficulty, boss balance or a naturally played
successful quest. Protected estates were not used; all checkpoint data is private.

Native return profiling confirms the terrain bake runs on a worker:17,126ms on thread50, with
all64 grades on workers. GPU readbacks/uploads stay on the main thread; three terrain uploads
span three frames. BuildGround's worst main-thread call is19.93ms; the longest observed Homecoming
frame gap is215.34ms, versus the old measured17,899ms painting stall. The returned town's colour/
layout is intact. This is one instrumented return/prewarm run, not a general cold-start or long-
session FPS benchmark. Total cold town preparation still takes roughly20seconds while frames
and results remain responsive. Private timing evidence: final-profile-state.json, acceptance.json and loading/.

The acceptance loop also reproduced a cold pending-fight resume bug: DD2 invokes its road-entry
callback before its mode transition flag clears. Immediate party setup requested COMBAT too soon,
leaving a manual Fight prompt. Dd2Run now defers that callback until the road is idle, with existing
timeout/retry/cancellation behavior retained. Two regressions fail before the fix; all10 run tests
pass afterward. A fresh native resume automatically reaches the hero's combat turn without a
mode-change warning or manual click. Q now uses the mod's retreat action through IMGUI/InputSystem;
an actual Q press reaches the normal retreat roll and failure feedback. Painted closed armour is
captured natively. The specific alpha-hit hover highlight still needs a deliberately confirmed
hover capture; the captured pointer position was not sufficient to prove that state.

Final Release+1,216 Core+221 UI pass. Normal deployment is frozen from the exact verified round252
production snapshot plus owned round251/253 repairs, preserving its installed Shieldbreaker/
Vilhelm and excluding later independent Shieldbreaker WIP. All417 normal source hashes and four
installed DLL/PDB hashes match; private observer/profiler/capture types are removed. Current v09
corridor files/config and39 native profile files are hash-preserved. All owned games are closed,
last26200; DD2 is stopped. Final evidence and new post-peer backup: local-art/walkthrough-fixes-
20261015. Never run its historical manage.py restore against the newer corridor/gait installation.

## Status 2026-10-16: room floors and combined walkthrough repairs installed

Round253 is installed. Room pack is local-art/scenery-rooms-v10; complete corridor pack remains
local-art/scenery-coast-v09. Normal plugin includes verified gait and the round251 repairs plus
cold pending-fight handoff/Q input. DD2 is stopped. No estate was selected/read/written; only
private fixtures. Next acceptance gaps: deliberately confirmed armour alpha-hover state, a
naturally played full successful quest and bosses/balance, ordinary estate-file handoff, broader
gait/class/hall-length coverage and long-session performance. Ground bake is responsive but total
cold preparation remains about20seconds. Native lighting/animated arena water/VFX are approximate.
Preserve independent Shieldbreaker source/art/journal WIP and both current private scenery packs.

## Round254 (2026-10-16): faster Hamlet paint and preparation before the recap

The owner's return-loading report follows round253. The prior native terrain worker still took
17,126ms every time the town was rebuilt. Its work was off the main thread, but moving it to a
worker alone did not remove the wait. Existing prewarm also began only through results OnGUI.

HamletPaintBaker now precomputes the separable world-to-tile coordinates per axis, removing
repeated divisions/mirrored lookups from millions of pixels. Field and pixel rows run on at
most four managed workers, bounded by half the available CPUs. Unity/GPU calls stay on the
main thread, and the existing three texture uploads still span separate frames. Six original
64px reference buffers and the complete2048px/48MiB output compare byte-for-byte with the old
baker. No texture detail, terrain resolution, alpha or world projection is reduced.

One process-local HamletPaintCache retains the most recent completed48MiB paint. SHA256 covers
all four readbacks, absent grass and both paint dimensions. Changed texture bytes invalidate
it; cancelled requests never replace a completed entry. Overlapping worker requests share one
bake. Result buffer properties cannot be reassigned by the plugin; uploads no longer null the
shared buffers. Only one entry is retained, with temporary extra memory on replacement. No
on-disk cache or extracted art is added. A fresh game process still performs its first bake.

Ground preparation starts alongside model construction. It waits for its four input grades,
then computes while remaining building grades/uploads finish. Final town reveal still waits
for complete models, grades and terrain uploads. Driver.Update now requests hidden prewarm
once the objective completes in exploration/combat, and throughout both recap pages/native
return transition. Recap drawing no longer owns the preload. Scene release/cancellation and
parked-camera behavior remain; a full town is not retained for the entire expedition.

Feedback: the new cache seam initially fails3/4 focused cases with the former unconditional
bake. Five cache tests then pass, covering reuse by content, mutation, grass/dimensions,
eviction, cancellation and concurrent requests. Frozen Release+1,221 Core+221 UI pass.
Standalone .NET8 full-resolution comparison: old2,508ms, new first bake541ms, hit2.7ms. These
are managed benchmark timings, not Unity return times. The native DD2 fixture observes the
first terrain bake4,372ms/cache miss4,411ms versus the prior17,126ms bake. HamletReady is true
while the completed private quest is still open in Crawling. No full recap-to-town duration
or native warm-cache latency is established by this run. Its cold driving/asset-startup frame
gaps must not be presented as a Hamlet return benchmark.

Owned native QA9208 was no longer running before capture/recap acceptance; the cause was not
established. The owner confirmed another agent is using DD2 for QA and requested keeping it
closed. No further launch or OS input followed. Native warm second return, both recap pages,
instant-return/cancellation stress and fresh native visual acceptance remain open. The old
IMainInstaller uninstall warning also appears in the native log and predates this update.
All checkpoints and observer outputs are private; protected estates were not accessed.

The private observer/profile hooks were removed, then the normal Release was rebuilt without
incremental reuse. Its419 source hashes and four installed DLL/PDB hashes match. Deployment
required DD2 stopped and the exact backed-up round253 plugin still installed.190 scenery files
and config are unchanged, retaining current room/corridor packs, cadence and installed hero
models. Independent fractional-stress and Shieldbreaker source/test/journal work is excluded.
Private baseline, build, red/green logs, full-resolution comparison, native cold-ready state/log
and deployment record: local-art/hamlet-return-v11. DD2 is stopped. Capture a fresh baseline
before the other agent's next QA/deployment; historical room/walkthrough restores would undo
this loading update.

## Status 2026-10-16: loading update installed, DD2 kept closed

Round254 normal build is installed. Objective completion starts background Hamlet preparation;
recap continues it, and unchanged terrain paint is reused within this process. Native first
bake improves from17.1s to4.4s; complete return/warm-cache timing and final visual/recap checks
are pending the other agent's QA window. Respect the owner's request to keep DD2 closed.
Next round255. Preserve all independent stress/Shieldbreaker work, current scenery and heroes.

## Round255 â€” DD1 resolve and heart attacks, persistent native Toxic (2026-10-16)

Owner: use DD1's stress system, with100-stress resolve breaks and200-stress heart attacks,
keeping Flagellant's native Toxic self-effects through the quest. The owner explicitly
answered "Keep DD2 closed; finish offline." No DD2 launch, OS input or estate access
followed. Runtime behavior below is built, not natively accepted.

Core now reads the installed DD1 traits/rules: seven generated afflictions, five virtues,
base75% affliction chance, effective-difficulty adjustments,5%/99% clamps and history
weights. The first crossing hit caps at100; virtue resets stress to the installed45.
Affliction persists until0; virtue at200 clears itself/stress without a heart attack.
Afflicted/Toxic heroes at200 lose remaining HP or die immediately if already at Death's
Door. The native survivor consumer applies30*(1+modifier) relief; the bridge uses the
base zero modifier, leaving170. Mapping that native modifier field remains open.
Native DD1 references/inputs are recorded privately; no decompiled code/assets are committed.

The old save scale remains ten DD1 stress per native unit, so existing7 still means70.
Persistent stress is now float, preserving small travel/camp/curio amounts. No numeric
save migration occurs. Resolve state/history and Toxic counts are independently copied
through actor state, JSON, fight recovery and homecoming. Interrupted fights keep current
stress/condition and permanent heart-attack deaths instead of rerolling/reviving them;
legacy null-state checkpoints retain their earlier rewind behavior. Hamlet caps stress
at100, clears virtue and cures affliction/Toxic only at0.

Scoped DD2 hooks affect expedition-bound heroes only: stress cap20 native units, no
normal meltdown/resolute queue, trait modifiers and weighted turn acts/comments/refusals.
DD1 stat effects reuse the existing DD2 adaptations for accuracy/dodge. Pass/random
commands, self/friendly attacks, buffs/heals/marks/moves, heal/retreat/supply and camping
refusals are connected. Named block_effect matching prevents virtues from refusing all
stress healing. Empty random skill-target lists pass instead of locking input. Curio
compulsion/theft and generic combat buff/item/player-move refusals remain unconnected;
see tools/stress_system.md for limits and acceptance cases.

Flagellant's first100 break applies native Toxic definitions except its stress-reset
effect. Blight/HP/cooldowns retain DD2 path conditions. Virulent-blight/manager tokens
and DBR/Scourge counts survive battle cleanup/reload; restoration never replays the
onset heal or cooldown reset. Toxic does not retrigger at100 while still afflicted,
and reaches the same200-stress heart-attack branch. HUD/roster/results/tooltips show
0â€“200, pips have a second layer above100, and native floating numbers rescale only at
the presentation queue.

Offline acceptance: frozen Release+1,242 Core+221 UI pass, including installed trait
loading,100/200 branches, legacy/fractional JSON, Toxic copies, resolve checkpoint
recovery, permanent heart death, accumulated one-point threshold crossings and camping/
supply refusals. A formerly ineffective
Hamlet side-effect test now uses Dismas and checks successful bar admission; Reynauld's
God Fearing quirk had prevented the activity and idle-heal log lines hid that mistake.
Metadata checks validate18 native method targets and6 reflected fields, without loading
the game. This confirms signatures, not Harmony execution or gameplay.

Stopped-game installation overlays only stress changes onto round254's exact419-file
normal baseline. The resulting425-file snapshot preserves its loading/cache, installed
hero work, gait and current corridor/room packs. Unrelated shared CrawlUi corridor hunks
and Shieldbreaker edits stay out. Fresh four-artifact rollback, source/overlay manifests,
logs, native-input hashes and deployment: local-art/stress-system-v01. Four DLL/PDB hashes
match; config,13 other plugin files and190 private scenery/model files are unchanged.
Normal plugin SHA256 05260b7d62f72375929083ef00d48308badfbc6759a2637974c31dea23eef4aa;
Coreb55e2ee7a541c401eb09e21cf6f394de6fa9daff4aa7d6722ce322885e14bc99.

## Status 2026-10-16: stress rework installed offline; DD2 must remain closed

Round255 is built, offline tested and installed. DD1 resolve/heart attacks and quest-
persistent Toxic remain `[?]` for native gameplay. No protected estate was accessed.
Keep DD2 closed until the owner explicitly lifts the hold. Next round256. Use the new
installed baseline, never historical rollback scripts that would undo round254/255.
Preserve independent corridor and Shieldbreaker work. Native acceptance scenarios and
remaining trait adaptations are listed in tools/stress_system.md.

## Round256 (2026-10-16): coherent corridor projection and traversal camera

Owner reports scenery direction/jumps, foot-step bugs and separating layers; all three are
noticeable, with camera jumps the worst. This round stays offline under the active request
to keep DD2 closed. No native visual/rig acceptance is claimed.

The previous complete4096x768 plate panned about247px per square, while curios/obstacles/
traps retained720px. This made props slide roughly2.9times as fast as the floor. Props and
arrival cues now use the actual crop's projected square spacing. Legacy scenery keeps its
720px coordinates. All visible known hallway props are considered, avoiding disappearance
at the old one-neighbour limit when square spacing is smaller. Atmosphere and hero feet
already use the same complete-plate crop/distance clock; they remain coupled to that view.

Old ViewProgress clamped at the first/last square centres, freezing the floor for a half-
square while movement continued. The bounded view now reserves the half-square approach at
both doors, including single-square halls. Tile carries remain continuous in both headings,
with unchanged painting orientation, scale and y680 contact. Short halls keep bounded speed.

The baked perspective depths had another camera of their own. Late companion readiness
moved far/middle landmarks even while the party stood still; battle covers switched to the
full plate and then back, causing another rearrangement. Gameplay/preview now draw the
complete native plate with one crop throughout. Depth passes remain private authoring data;
no companion decode/upload is requested in gameplay. Fully loaded complete corridor/room
scenes also suppress the tiled road beneath them, preventing a second floor showing through
battle-cover alpha. Native image assets, resolution, colours and geometry are unchanged.

Actual Driver traversal was extracted into Driver.Traversal.cs so tests link its real
methods with Core Crawl. Obstacle rejection, a trap action with no move and refused room
travel reproduced camera steps with no state movement. A clock step is now accepted only
when the location changes. Held movement restores its previous fractional position on a
refused step instead of applying another tile's carry; blocked input cannot recenter the
floor. Hall entry uses its room fade without a second unrequested sweep. Room entry clears
the slide/progress and starts its fade on that same frame, avoiding a clear frame before
fade-in. The continuous-step flag is restored even if event handling throws.

Feedback: initial projection cases fail4/7, stationary-depth registration fails1/1 and
real traversal fails3/4. Final focused projection/motion checks pass23 and focused render/
traversal/locomotion checks pass33 before additional room-transition coverage. Final combined
Release+1,250 Core+228 UI pass. Registration checks cover stationary views, companion arrival,
partial sets and battle opacity; projection checks cover both headings, endpoints, short/
long/single-square halls, tile carry and unchanged legacy spacing. Traversal covers accepted,
repeated and refused inputs, trap handling and both door transitions. No game launch/input
or save access. DD2 remains closed at the owner's request.

The final430-source normal build layers only these walking changes onto the other chat's
verified425-source round255 stress build, retaining round254 loading and installed hero
models. All upstream hashes match. Four installed DLL/PDB hashes match the frozen Release;
190 scenery/model files and config are preserved. No private observer hooks. Fresh exact
round255 rollback, source manifests, red/green logs and deployment record are in
local-art/corridor-motion-v12. Normal DLL is bfc9b693b5054fa5f8e94a4c2ad32d15d5681a82ab79d29e8467b0f69e06767e.
Independent Shieldbreaker source/art/journal WIP is preserved. Historical gait/stress/loading
restores would undo this combined build; future QA needs a fresh installed baseline.

## Status 2026-10-16: corridor projection fixes installed, native QA held

Round256 is installed on top of rounds254/255. All three reported walking problems have
reproduced code defects and offline repairs. Native visual comfort, real input/rig response,
asset-adoption/fade behavior and full corridor-room-battle walkthrough remain unverified.
Keep DD2 closed until the owner lifts the hold. Next round257. Preserve the combined430-source
baseline and independent Shieldbreaker WIP.


## Round257 (2026-10-17): native DD2 typography, starting with trinkets

Owner requests DD2 fonts, style and sizing, with trinkets first, and explicitly says
not to launch DD2 until they say OK. This round performs no game launch, input or estate
access. Extracted reference assets stay private.

Installed fonts are NDDunkelD-Bold and AlegreyaSans-Regular. Shared Gui text previously
fell back to DD1 bitmap faces outside campaign skin scopes, with different line-height
multipliers. Equipment names also lacked their heading flag and used body20/effects17.
Shared text/measurement now use the same installed DD2 faces and explicit virtual-pixel
sizes throughout. Styles use normal weight over the native bold heading face. Short
fixed rows fit by width; prose wraps. Activity/memorial caches, path-effect measurement
and hero-name width now track the actual native font. Fonts load separately from art
and wait for DD2's catalog, avoiding failed GUID-only lookups during initial loading.

Native TooltipCanvasElement prefab confirms regular24 / large30, no automatic font
shrinking, zero added character/line spacing and40px background padding. ItemDescription
uses Dunkel at120% for the title and80% hints. Trinket tooltips now use that hierarchy,
DD2's larger-tooltip option, installed name/effect/type/hint/rarity colours and the
painted ui_tooltip sprite. Nine-slicing retains authored borders. DD1 memory rarity data
remains supported. Localized item names strip presentation tags, including Dark Impulse's
colour markup. Inventory title/status rows get explicit native sizes and alignment.

The shared tooltip pass replaces the separate expedition tip renderer. It grows from460
to640px before scrolling long descriptions, without shrinking the text. Wheel handling
runs before underlying scroll views at the hover point. New content resets scroll, and
duplicate local/root passes within one frame preserve it. Eight new geometry checks
cover corners, long-content scrolling and atlas/border registration.

Offline evidence: actual font bundles and TooltipCanvasElement extracted with UnityPy;
prefab sizes and source font references agree with decompilation. Native IronCrown and
compile reference hashes both dd97e30f2f37e2a287633ab322b8fd18bcfe9e240903713a5c2ae00c894692e6.
All191 installed trinkets produce382 regular/large Pillow layouts with no horizontal
overflow. The contact sheet uses actual native fonts/frame and data, but is an offline
approximation, not a game screenshot. Release,1,250 Core and236 UI checks pass. An initial
Core run had one missing private catalog fixture; copying the installed read-only catalog
into the isolated fixture restored the complete suite without source changes.

The436-source normal build layers only17 owned typography code/test files onto round256's
verified430-source installed baseline. Non-owned source hashes match. Four DLL/PDBs match
the compiled Release;190 private scenery/model files and both configs are unchanged.
Normal plugin SHA256 7dc3e07dc9ac051ad45b7d6719da155da8b2c023dd9aa2c8c8b405b0d14aa2c2.
Fresh round256 backup, source/artifact manifests, asset metadata, data/packing checks,
preview and test logs are in local-art/typography-v01. Independent Shieldbreaker WIP is
preserved. Older backups would undo this build; use a fresh baseline for future QA.

## Status 2026-10-17: DD2 typography installed; native visual acceptance held

Round257 is installed and offline checked. DD2 remains closed until the owner explicitly
says OK. Native rasterization, real hover/wheel and larger-tooltip option, smaller window
scaling, localized fallback and full campaign/expedition layouts remain unverified. The
renderer uses native source fonts through IMGUI; TMP's SDF materials and inline token
sprites are not reproduced. See tools/ui_typography.md. Next round258. Preserve the
combined436-source baseline and independent Shieldbreaker work.

## Round258: clickable corridor doors and stronger scene travel (2026-10-17)

Owner: stop automatic corridor-to-room entry and click a door as in DD1. The corrected
movement report concerns how little the background moves, not the hero's walking pose.
The explicit no-launch hold remains. No DD2 launch, OS input or estate access occurred.

Feedback first:8 new red cases reproduce both symptoms against real Core functions.
Four direction/end combinations enter rooms through Crawl.Step; four native plate/length
cases move only185.6â€“247.5 virtual pixels per square. The Unity port's RaidHallSector/
RaidDoor activation and installed DD1 doorway textures were inspected privately.

Crawl.Step now stops at either boundary. CanEnterDoor/EnterDoor validate an explicit
interaction before room visits, scouting, quest progress or battles happen. Pending
loot/camp/encounters block entry; fights pin both doors, while traps/obstacles permit
returning through the entrance behind them. CameFrom retains the retreat square.
Single-square halls expose both destinations. Reload stays in the hallway without
replaying tile effects. Existing headless quest/scouting/loot tests explicitly enter doors.

Visible passages have hover/entry labels and click targets. Legacy DD1 scenes keep their
walls; outdoor DD2 scenes use installed DD1 doorway paint with a cached feathered arch
mask, without its square wall tile. No derived assets are shipped or written to game
files. Both entrances share the ground projection. Captures retain world geometry but
omit interaction labels. Clicking starts0.3s fade-out, then entry/existing fade-in.
Duplicate clicks cannot restart it. Pending requests belong to their Crawl instance.
Held input/map routes stop at the half-square approach without an automatic fade or
additional footsteps at the limit.

Bounded panorama travel targets360px per square with a minimum six-square span.
Longer halls can zoom up to1.6x original overscan, retaining aspect and authored y680
contact. Actual4096x768 plates travel360â€“371.2px per square in the tested3/6/9/12-square
cases. Very long/narrow plates may cap below target. There is one finite painting,
without repeated architecture or an independently moving floor. Props, doors, particles
and the existing distance-driven feet share this crop. Pose/stride code,1.2s logical-
square duration and half-speed backtracking stay unchanged.

Frozen Release+1,266 Core+237 UI pass. Coverage includes blocked doors, both headings,
single-square halls, reload, one-time entry, retreat metadata, duplicate-click/fade timing,
actual paint draw versus physical camera/props/doors, crop bounds/aspect/footline.
Four offline compositions use real regional paintings, the old compiled crop and new
Core crop/mask. These are previews, not DD2 screenshots or gameplay acceptance.
The future native scenery fixture now clicks doors and uses the new physical bound.

Installed440-source normal build overlays26 owned files onto round257's436-source
typography baseline. Typography, stress, loading, installed heroes and earlier corridor
repairs remain. Four artifacts match;190 scenery/model files,13 other plugin files and
the checked plugin config are unchanged. Fresh round257 rollback, exact source/overlay,
red/green/full logs, compositions and deployment hashes: local-art/corridor-doors-v13.
Independent Shieldbreaker source/art/journal work is excluded.

## Status 2026-10-17: corridor doors/travel installed offline; keep DD2 closed

Round258 is installed. Click a passage to enter/return; movement stops at corridor ends.
Background travel is larger while pose/stride code stays unchanged. Native pointer
placement, comfort/rig contact, controls, fades and room/battle/return acceptance remain [?].
Keep DD2 closed until the owner explicitly says OK. No launch/input/estate access.
Next round259. Preserve typography/stress/loading/scenery/heroes and Shieldbreaker WIP;
capture a fresh baseline and never run historical restores over this build.


## Round259 (2026-10-17): continuous walking clock and stall recovery

Owner says steps/background remain wrong and clarifies "While walking continuously".
DD2 remains closed until an explicit OK. No game launch, OS input or estate access.

Earlier checks covered projection and planted-target distance, but not the real held-walk
clock's physical speed. The unchanged continuous integration was extracted from Walk into
Driver.Walking.cs so tests execute it with real Core Crawl, camera crop and gait functions.
Nine red cases reproduce 309px/s native / 600px/s legacy travel, 4.15 steps/s on the 102.6px
projected leg derived from the earlier native capture, 7.75 on a scaled55px leg, and247–480px
ground jumps after an800ms delayed frame. Seven cases passed after fixing the clock, with
both hitch cases still red at144px. Applying the movement catch-up cap then passed all.

Held keys, mouse and routes now advance by physical rendered pixels: target180px/s,
converted through the current projected square spacing. Native fitted rigs can lower
the party limit to1.8 projected leg lengths/s. Imported models keep their own distance
calibration; disabled3D models use the default limit. Reverse motion retains half speed.
Only100ms of movement is consumed per frame, so a render/asset stall resumes from the
visible position instead of catching up several unseen strides. Logical tile/curio/event
semantics, crop/door geometry, artwork and native pose/stride arc remain unchanged.
Walking a larger painted square now takes longer, about2.06s for the canonical six-square
hall. Discrete developer-step easing remains1.2s and is separate from held gameplay input.

Fourteen new actual-loop cases cover physical speed across6/12/20-square halls/fallback,
native/small rig cadence, stalls, reverse motion,30/144Hz, exact tile carry and passage
stopping. Combined crop/registration/locomotion checks pass42; full Release+1,266 Core+
251 UI pass. Offline replays produce180px/s,2.42 steps/s and18px stall advance on the
recorded short rig, and99px/s/2.48steps/s/9.9px for the55px scale. No new offline crop
mismatch was found. The future native fixture now enforces physical held-walk speed,
instead of permitting a fast1.2s-per-tile pan. No temporary observers in the normal build.

The442-source normal build overlays12 owned code/test files onto round258's verified440
source baseline. Non-owned source hashes match; all four installed artifacts match the
compiled Release.190 scenery/model files,13 other plugin files and both configs preserved.
Normal plugin SHA256 cf5e8b8a9a0972ce51ffd6f537eea9e438413120fc49a8cf0b89251470d3921e.
Private evidence, extracted unchanged loop, red/speed-only/green/full logs, replay metrics,
exact source manifest and fresh round258 rollback: local-art/walk-pace-v14.
Independent Shieldbreaker WIP remains excluded and intact.

## Status 2026-10-17: continuous walking pacing installed; native visual check held

Round259 repairs reproducible continuous-movement speed/catch-up defects. The user-visible
problem is not declared visually resolved: actual rig projection/comfort, loading adoption,
input and sustained four-region walking still need native acceptance. Do not launch DD2
until the owner explicitly says OK. No launch/input/save access this round. See
tools/corridor_heroes.md. Next round260. Preserve the442-source baseline, room/corridor art,
doors, typography, stress, loading, installed heroes and independent Shieldbreaker work.
Capture a fresh installed baseline; historical restores would undo this build.


## Round260 (2026-10-17): longer native corridor strides

Owner says distance per step is still underwhelming. Round259 reduced ground speed/cadence
but retained the 0.90-leg contact arc, only0.726 projected leg lengths per footfall. Three
crop/gait cases fail the longer-step acceptance, and five actual held-loop cases fail at
0.72–0.73 leg lengths between alternating landings. Native visuals are not observed here.

Increase the native gait contact arc to1.24 at unchanged62% stance. Rate consumes the same
arc, preserving planted-foot/floor velocity. Each step now covers one projected leg length,
37.8% more distance. Physical ground speed stays180px/s, with existing small-rig limit and
half-speed reverse. Recorded102.6px rig replay gives102.6px/step and1.75steps/s, versus
74.5px/2.42;55px scale gives55px/1.80steps/s. Imported Vilhelm root motion is separate.
Existing restart, reverse, contact continuity, body support and reachable soft-knee checks
pass. Long-frame test inputs now specify skipped phases so stride changes retain the same
missed liftoff/landing scenario. All51 focused checks, Release+1,266 Core+256 UI pass.

An initial private old-source copy kept its old timestamp, so incremental MSBuild reused
the new test assembly. Touching the copied input forced recompilation: all five held-loop
cases then failed on the old stride and passed after restoring the new stride. The final
native Release was rebuilt with --no-incremental. Red/green logs and metrics are private in
local-art/walk-stride-v15, alongside exact442-source manifest and fresh round259 rollback.
Only3 code/test files overlay the prior normal baseline; all other source hashes match.
All four installed artifacts match compiled output.190 scenery/model files,13 other
plugin files and both configs preserved. Independent Shieldbreaker WIP remains intact.
Normal plugin SHA256 ea5eacd8127a1eb182d59cab8b6a709380ee511b983b1fae90c6f5c5a21bdcf1.

## Status 2026-10-17: longer stride installed; native comfort pending

The reproducible short stride is corrected offline. User-visible smoothness is not declared
resolved. Keep DD2 closed until the owner explicitly says OK. No launch, OS input or estate
access this round. Native rig projection/comfort, sustained four-region walking, controls
and battle handoffs remain pending. See tools/corridor_heroes.md. Next round261. Preserve
the442-source normal snapshot, artwork, doors, typography, stress/loading and installed
heroes. Capture a fresh installed baseline; historical restores would undo this build.


## Round261 (2026-10-17): native walking pose and fitted pace

Owner reopened DD2 and explicitly said "you can launch and record", lifting the launch hold.
Read-only live captures caught idle/door stops, so repeatable acceptance used unsaved native
fixtures, with no estate selected or persisted. Estate2 was backed up/hashed, never modified;
protected1/3 were not accessed. Temporary observer, native profiles and all assets/config
were captured from a fresh round260 baseline before testing, then restored after owned QA.

Actual bone/video measurements expose what the offline single-leg replay missed: the common
pelvis was lowered by both long rear contacts and airborne swing targets, producing some
supporting knee bends above90 degrees. The new shared-support test first fails at63.8 degrees.
Reducing stance62%->55% alone still fails at55.2; limiting walking pelvis constraints to ground
contacts then passes. Stride=2*Stance keeps1.0 projected leg length per footfall, and Rate
shares the same arc. Stopping retains the earlier two-leg reach constraint. Reverse, restart,
contact continuity, lift/landing, reachable knees and floor-distance checks remain covered.

The currently fitted native legs measure154–202px, much larger than the102.6px older replay.
The180px/s ceiling therefore forced0.89–1.17 steps/s. Four actual held-loop cases reproduce
that slow cadence. Raising the physical ceiling to320px/s retains the existing party-minimum
1.8 projected leg lengths/s limit: native parties now move277–279px/s, with half-speed reverse
and unchanged100ms movement catch-up cap. Smaller rigs still slow the party; step distance
and logical tiles/events/doors remain intact. Imported Vilhelm root motion remains separate.

Red/green evidence, Release+1,266 Core+262 UI and57 focused checks pass. Native all-four-region
forward/reverse/stop/repeated-button routes pass first with Highwayman/Grave Robber/Plague
Doctor/Man-at-Arms, then Leper/Shieldbreaker/Flagellant/Jester as seen in the owner's party.
Bone traces and35-second videos accompany both revised parties; sampled100ms poses inspected.
Final owner's-party continuous contact slip stays below0.58 virtual pixels; planted IK target
error max0.1953. Some class/startup poses still bend more than the idealized math fixture;
this is observed improvement, not an every-class animation or ordinary-campaign certification.

An observer attempt initially read a stale prior launch log before BepInEx truncated it;
later final runs require a fresh log write and show the new277–279px/s limit. Final validation
uses those fresh logs. Private QA class substitution is confined to its fixture. The normal
442-source build overlays6 owned files on round260 and excludes all temporary observation.
All4 installed artifacts match compiled output.190 scenery/model files,13 other plugin files,
both configs, estate2 and39 native profile files preserved. Shieldbreaker WIP remains intact.
Normal DLL SHA256 aaa763e1ae486156797d8ae44f48d06c4bc0a5b3d89886f669b533faf48b0f95.
Private source/backup/logs/bone traces/video: local-art/walk-observe-v16. walk-preview.mp4
shows the revised Leper/Shieldbreaker/Flagellant/Jester party. All owned QA games closed.

## Status 2026-10-17: native-tested walking fix installed

Owner permits launch/record again. Current gait/pace has native four-region checks on eight
classes in two unsaved parties, with real footage and contact measurements. Full Release+
1,266 Core+262 UI pass; normal build installed and observer removed. Ordinary estate handoffs,
every class/scale, combat-idle body translations and imported Vilhelm acceptance remain broader
work. See tools/corridor_heroes.md. Next round262. Preserve442-source normal baseline, existing
art/doors/typography/stress/loading/heroes and independent Shieldbreaker WIP. Use fresh hashes
before any QA/deployment; historical restores would undo this build.


## Round262 (2026-10-17): softer foot recovery and hip handover

Owner finds the steps a little rigid/stiff. Actual sampler tests reproduce ankle angular-
velocity jumps170.7/285.7 degrees/cycle, a17-degree toe-down swing at40% recovery and abrupt
vertical acceleration. Quintic contact roll, toe recovery and earlier0.12-leg lift replace
the old0.08 low arc. Five initial red cases; roll alone passes2 with3 lift/recovery failures.
Horizontal ground-contact travel, step distance,55% stance and fitted pace remain unchanged.

Existing native bone traces expose the larger support-bit defect:516/606 contact changes
jump over5px, max32.03. Four red coupled-pelvis cases reproduce0.0594-leg discontinuities.
The body now releases old support over the first25% of swing and prepares the next landing
over its last40%, leaving middle swing unconstrained. Ground support remains full. Relaxed
hip height0.925 keeps earlier soft-knee/height bounds. Moving airborne targets initially
spread the impulse but produced larger drops, so prediction uses ground anchors instead.
Native nominal-anchor testing then exposed retained wide combat poses: the support release
must capture the actual liftoff anchor. CorridorSupportContact handles both directions and
resets on stop/visibility/restart. Two negative controls reproduce the nominal-anchor snap.
Bounded easing also avoids negative roundoff weights;12 new regression cases overall.

Final unsaved Leper/Shieldbreaker/Flagellant/Jester routes pass forward/reverse/stop/repeated
buttons in all four regions.35-second footage and100ms poses inspected.15,490 actor-motion
samples give16/576 contact jumps over5px, max7.23, versus516/606/max32.03 before. Largest
all-frame height jump32.03->15.43;95th percentile unchanged3.52px. Continuous planted-foot
slip0.578px and IK target error0.1953px remain sub-pixel. Fitted pace remains about278px/s.
This is a measured reduction in rigid contacts; some startup/class/native idle motion and
imported Vilhelm's separate animation still need broader aesthetic acceptance.

Release+1,266 Core+274 UI checks pass,69 focused.442-source normal build overlays3 owned
files on round261, preserving non-owned source hashes. Temporary observer and private class
fixture excluded. Four installed artifacts match compiled output.190 scenery/model files,
13 other plugin files,both configs,estate2 and39 native profile files preserved; protected1/3
not accessed. User's estate2 backed up and current window closed gracefully before unsaved
QA. Independent Shieldbreaker WIP intact. All owned QA closed. Normal DLL SHA256 3740fef2df138dcc7c12fe6ffb7c348cfb47a7b8c88b02af77ebbe43226ca9d6.
Private evidence, failed native iterations, source manifests/fresh rollback and revised
walk-preview.mp4: local-art/walk-softness-v17. See tools/corridor_heroes.md.

## Status 2026-10-17: softer native walk installed

Foot roll/recovery and actual-anchor hip handover now have native four-region evidence on
the owner's displayed four classes.69 focused and full Release+1,266 Core+274 UI pass.
Normal442-source build installed without temporary observers; QA closed, saves/profiles
preserved. Step distance and fitted pacing retained. Owner's launch/record authorization
remains current. Broader class/scale/saved-campaign/startup and native idle polish remains.
Next round263. Preserve current scenery/doors/fonts/stress/loading/heroes and Shieldbreaker
WIP. Capture fresh installed hashes; older restore scripts would undo this build.


## Round263 (2026-10-17): stop throwing the free thigh backward

Owner reports the quad/hamstring and whole upper leg extending too far behind
the body. A fresh private native hip/knee observer reproduces 35.67 degrees
during sustained walking, with almost straight knees just after liftoff. The
all-frame maximum including retained combat contacts is 42.78 degrees. Existing
two-bone IK tries to reach the delayed early swing arc by extending the thigh.

CorridorKneeRecovery preserves the last supporting thigh angle at toe-off and
relaxes toward 20 degrees with a 60ms response. Only free knee goals change;
the shin still aims at its original target, lifting/retracting the free ankle.
Ground contacts, footstep distance, pace, phase, pelvis support and reverse
travel retain their contracts. Stop/hidden/restart paths reset the new state.
Three coupled sampler/pelvis/geometry cases first fail on rear extension;
fourth regression covers wide toe-off, unchanged plants, frontward movement
and reset. Release +1,266 Core +278 UI pass, including73 focused locomotion.

Unsaved Leper/Shieldbreaker/Flagellant/Jester forward/reverse/stop routes pass
all four native regions. Settled samples start after2.5 leg lengths from each
actual stop/direction change. Airborne rear maximum35.67->22.58 degrees; all
settled legs max23.94. Across all frames42.78->30.75; retained initial combat
contacts deliberately blend out instead of snapping.13,794 baseline and11,486
revised actor samples. Contact slip max0.527px; IK target error0.1953px. Revised
35s1600x900 footage/pose sheet inspected. Baseline video captured a tiny strip;
its native bone measurements, not that video, establish the baseline.

Normal442-source build overlays3 owned source/test files on round262. Private
observer/class fixture excluded. Four installed artifacts match built output,
plugin SHA256 a356b1d6e09e90cae1827af3dca0c78b1038ac8a83715bf85219c1ba42e68f16.190 scenery/model files,13 other plugin files,2 configs,
estate2 and39 native profile files preserved. Protected estates1/3 not accessed.
Independent Shieldbreaker/README/presentation/journal work retained. Both owned
QA windows closed; launch/record remains authorized. Other classes/scales,
ordinary saved handoffs and Vilhelm's separate motion need broader acceptance.
Private evidence/red-green scripts/fresh backup/video: local-art/walk-rear-reach-v18.

## Status 2026-10-17: rear thigh recovery installed

Round263 reduces the owner's excessive rearward thigh swing by folding only the
free knee; planted contacts/step distance/fitted pace remain unchanged. Native
owner-party four-region routes and Release+1,544 tests pass. Installed normal
snapshot excludes observers, preserves assets/config/saves/profiles and unrelated
work. DD2 stopped. Native steady airborne rear angle35.67->22.58; startup combat
stance still blends out. See tools/corridor_heroes.md. Next round264. Use fresh
installed hashes before deployment; older rollback scripts would undo this build.


## Round264 (2026-10-18): authored walk and a capped opening pan

Owner finds steps sequential, with Vilhelm's original walk the desired reference.
The old native layer pins an ankle flat until liftoff, then schedules recovery
separately from the body. HumanoidWalk samples the installed owned clip into a
shared distance-space pose table. Native rig proportions receive coordinated
pelvis/spine/leg motion and rolling heel/toe support. Source knee poles avoid
the old free-thigh clamp plateau. Native weapon poses and Leper's sword grip
are retained; Vilhelm's own renderer/clock are unchanged. Optional sampling
is prepared off-thread. Missing/bad private packs keep the procedural fallback.
No extra runtime assemblies or game-derived assets are distributed.

First native pass catches premature heel support while the arriving foot is
still reaching forward. It stretches a locked contact and drops the body.
Support now requires backward sole movement, and retargeted travel is calibrated
to the supporting sole. Contact prediction and rendering share the same blended
ankle rotation, removing startup sole drift. Two contact tests first red; the
owned clip check plus a negative control catch the premature heel plant.

Owner then accepts the steady result but reports the opening burst. Its separate
1.2s button pan actually peaks at 463.9px/s versus 279px/s held movement. Five
red actual Driver/crop replays show 450-900px/s across size/fallback/reverse cases.
Driver.StepTiming captures each pan's duration from rendered square pixels and
fitted party speed, accounting for smoothstep's 1.5x peak/mean factor. Reverse
retains half speed. Slide and all input guards share the captured duration; a
late model/scale update cannot retime an active pan. Sixth regression verifies
that guard and repeat rejection. The accepted steady gait and held pace stay
unchanged, and the full square distance is retained.

Release +1,270 Core +284 UI checks pass. A shared Core build-output collision
during parallel test builds is recorded privately; sequential rerun passes.
Transient Leper/Highwayman/Vilhelm/Jester comparison and owner-party Leper/
Shieldbreaker/Flagellant/Jester routes pass all four regions. Final fixture
also measures button pans, then forward/reverse/stop/repeated-input routes.
Button peaks 278.3-279.0px/s, held about279.1px/s. Final16,343 actor samples all
use authored motion; sole slip max0.630px, P95<0.238px, IK error0.1953px. Several
native35s1600x900 captures/pose sheets inspected; final preview includes the
opening step. Owner accepts steady feel. Other classes/scales and ordinary saved
handoffs remain broader acceptance work; do not claim universal perfection.

Installed447-source normal snapshot overlays14 owned source/test files on263.
Private observer/class substitutions excluded. Four artifacts match build,
plugin SHA256 4e5a0dab9c46a4dec2b2495e5e5ea0a9dbd38e4878c3d489983795de10fbf75e.190 scenery files,66 private model files,13 other plugin
files,2 configs,estate2 and39 native profiles preserved. Protected estates1/3
not accessed. Independent Shieldbreaker/journal work retained. All owned QA
closed. Evidence, failed iterations, source manifests, fresh rollback and
walk-preview.mp4: local-art/walk-authored-v19. See tools/corridor_heroes.md.

## Status 2026-10-18: authored walking and opening pace installed

Round264 uses the owner's existing private Vilhelm clip to coordinate native
body/leg/foot roll. Steady feel accepted by owner. The initial fixed-duration
button burst is capped to the same fitted walking speed, 464->279px/s. Release
and1,554 tests pass, with final owner-party four-region native evidence. Normal
447-source/14-file snapshot installed, observer removed, assets/config/saves/
profiles and unrelated work preserved. DD2 stopped. Launch/record authorization
remains current. Next round265. Use fresh installed hashes before deployment;
older restores would undo this build. Other native classes/scales and ordinary
saved transitions remain broader acceptance work. Keep the accepted steady gait.

## Round265 (2026-10-18): Sprawl footage in the trailer

Owner asks to refresh the existing Remotion trailer with the accepted walking,
the burning city and Crusader in place of Shieldbreaker. Follow-up: "you can use
corridor walking, deathblows on attacks and the battle start, don't show the
whole battle".

Recorded a transient, unsaved Sprawl route with Leper, Crusader, Flagellant and
Jester on a copy of the installed round264 build (447-source snapshot). A private
fixture (TrailerCombat.cs) picks each hero's first valid skill and a living enemy
through DD2's own skill/target queries and events, so the kills in the footage
are native deathblows, not debug kills.

The expedition chapter now cuts 9 s of walking, a 4.4 s battle opening
("Enemies ahead!") and 8.53 s of attacks: Crusader's Smite on the Pit Fighter
(3.1 s), Leper's Chop deathblow on the Whipper (3.1 s) and Leper's Chop
deathblow on the Pit Fighter (2.33 s). The rest of the battle is left out. The
Hamlet, map and comparison chapters are unchanged. Shorter captions: "Explore
the Sprawl on foot", "Battle begins where you stand", "Steel and fire in the
Sprawl". prepare-media.mjs normalizes the game audio to -20 LUFS (true peak
-3 dB); it plays at 70% while bed-b ducks from 0.84 to 0.42 under the attacks
and comes back for the recap. Total 79.03 s, 2371 frames, 1080p30.

The first full render failed to fetch a combat frame. The combat section alone
rendered, and the final render passed with concurrency 1, a 512 MB
OffthreadVideo cache and TEMP on D:\dd3-video-render-tmp (C: had 3.8 GB free).
Checks: TypeScript; contact sheets of the expedition and the whole video; both
deathblows stepped at 200-250 ms; full decode, 2371 frames, H.264 with AAC
48 kHz stereo, peak -1.8 dBFS. Section loudness: walk -13.7, opening -12.4,
attacks -16.7, recap -20.6 LUFS.

Output: presentation/out/darkest2in1.mp4 (SHA256 7dd315d7...), with
darkest2in1-v02.mp4 alongside; the previous render is kept as
local-art/presentation-v02/darkest2in1-v01.mp4. Raw takes, fixture, QA and
cleanup scripts, contact sheets and video-verification.json are in
local-art/presentation-v02. No media committed.

DD2 closed and the round264 normal plugin restored. 190 scenery files, 66
private model files, 13 other plugin files, 2 configs, estate 2 and 39 native
profile files match the pre-recording backups. Protected estates 1 and 3 not
accessed. Independent Shieldbreaker/journal work left uncommitted. No gameplay
change.

## Status 2026-10-18: trailer refreshed with Sprawl footage

Round265 refreshes the presentation (79 s, 1080p30) with the accepted round264
walk in the Sprawl, the battle opening and three native attacks, two of them
deathblows; the full battle is not shown. The installed build is still the
round264 normal snapshot; take fresh installed hashes before any deployment.
DD2 stopped. Launch/record authorization remains current. Next round266. Other
native classes/scales and ordinary saved transitions remain broader acceptance
work. Keep the accepted steady gait.

## Round266 (2026-10-18): cinematic Hamlet composition and detailed models installed

Owner shifts this conversation to the functional but visually weak 3D Hamlet;
another agent owns gameplay fixes. Reference: actual DD1 epilogue town view at
25.5 seconds, plus House of Ruin/Old Road contact sheets. Official DD1 media
pages confirm the cinematic context; local installed movies supply the frames.
The useful composition is overlapping roofs, light sky, dark foreground and
a small estate on the rear ridge. No extracted game files committed.

HamletLayout now targets (12,12,20), offset (2,36,-112), vertical FOV37.5.
Larger coach/tavern/abbey/guild/forge/wagon fill the view; Survivalist moves
inward. That move initially made its last stair too steep; corrected the actual
landing. Ground-paint fixture hashes change with footprints/doors; the lamp
test's outside point now lies outside all neighbouring pools. Original dark
native cliff paint/UV scale retained, rock/paving darkened, stronger cool key
and shadow facets. Three native Valley cottages fill roof gaps. Four native
Sprawl assemblies form a small estate beyond the Abbey/Guild. It is decorative,
never a service or picking bound. An oversized/clipped first estate and floating
centre tower were rejected; final native capture checks joined towers and framing.

The huge native Mountain/starry Camp sky is replaced by the cloudy upper part
of installed DD1 town_bg.png on a distant plane. Every building remains 3D.
Art cache prepares it asynchronously before stage readiness; stage owns only
sky mesh/material and releases them normally. Neutral sky fallback. Compact
estate title/camera controls, hints on hover and actual UI input bounds free
more scene area; a dark roster header preserves text/icon contrast.

Found approved round242/v15 finish package never deployed. Installed its four
finished Abbey/Guild/Sanitarium/Tavern pairs plus matching manifest: exactly
nine changed PrivateScenery files, six models and all textures unchanged.
54 hashed assets in private HamletModels package (59.7MB). Original paint kept;
no new generated art. LayoutExport/town_preview reproduce the same geometry,
camera, native sky, scenery and grading; Blender lighting is approximate.

Frozen clean HEAD7846597 +12 owned files excludes concurrent gameplay and
Shieldbreaker WIP. Release +1,267 Core +284 UI pass. Native unsaved1600x900
fixture passes ten service world clicks/Escape, pan/zoom/reset, full resource
release/reload/re-entry, Embark same-texture park/cache/resume,1440x900 resize/
restore, keyboard Enter/back and still-camera/smoke. Zero live input events.
Final skyline-only adjustments checked in fresh1920x1080 native overviews;
189 renderers, four plumes, native B10G11R11_UFloatPack32. Zero repeated
buffer reallocations in final5.042sec; not a full frame-time benchmark.

Observed limitation: after the forced full-release/resize stress path, roster
portrait slots can go black. Fresh launch displays all correctly. Cause not
established; separate asset-lifecycle follow-up. Bespoke cliff/facade painting,
NPC/upgrade exteriors, controller/arbitrary text/window sizes, saved transactions
and long-session performance remain open. This improves composition and model
finish; it does not establish full DD2 production-art parity.

Normal final plugin installed with fresh baseline/hash guards. DLL SHA256
2d3a3b7079521a125a2b07bf0d04185622ef66388c32e300d8ed52a040e859f3.
Only four plugin artifacts and the approved nine scenery files change.66 hero
model files,13 other plugin files,2 configs,estate2 and39 native profile files
preserved; protected1/3 never accessed. Native profiles restored after every
QA launch. Game closed. Private evidence, final layout/Blender scene, native
capture/logs, source hashes, failed studies, baseline and deployment.json:
local-art/ui-overhaul/hamlet-cinematic-v16. Do not run its QA restore helper over
later builds; capture fresh installed hashes first. See tools/hamlet_3d.md and
tools/hamlet_models/README.md. Next round267.

## Status 2026-10-18: cinematic Hamlet installed

Round266 installs lower camera/larger services, darker native cliffs, native
sky/Valley cottages/distant Sprawl estate and the previously approved four
detailed service models. Release+1,551 tests, unsaved native interaction/cache/
resize checks and final1920 overview pass. Normal frozen build installed;
game closed; save/config/profile hashes preserved. Concurrent gameplay WIP
excluded and retained. Remaining art/lifecycle acceptance above. Next round267.


## Round267 — fuller Hamlet and service architecture (2026-10-19)

Round267 combines a fuller town with rebuilt Abbey, Guild and Sanitarium architecture.
Fifteen grounded native DD2 lots, a windmill and three streets use shared terrain pads and
vegetation clearance. Native decorative building paint retains up to2048px; cached terrain,
lanes and valley paint use4096px (192MiB retained managed paint, plus GPU textures).

The three services now have cut door/window recesses, physical arch stones, sagged thick roofs
and side windows. The Abbey has its DD1 rose and a native Sprawl tower. The Guild has a plaster/
timber upper hall, oxblood dormers and sword standards. The Sanitarium combines a uniformly
scaled native Academic house with a stone turret, green casement and apothecary porch.
Seven other models match the previous approved package. Abbey's terrain pad is widened.

Private v19 materials combine native wood, plaster, cut stone and courtyard paving with the
v18 generated ashlar/slate/red-roof paintings. The v17 generated masonry was rejected and is
not installed. Optional v17 sky/cliff paintings remain, with native fallback. Provenance and
prompts stay outside Git. All generated art remains under visual review: the new masonry is
still too regular beside native DD2 facades, and the other seven services need further review.

Frozen HEADa2914aa plus owned Hamlet files excludes concurrent gameplay/Shieldbreaker changes.
Release,1267 Core and284 UI tests pass. Unsaved native fixture passes ten services, Escape,
pan/zoom/reset, full release/reload/re-entry, Embark park/cache/resume, resize/restore, keyboard
and still-camera/smoke.208 renderers; zero repeated buffer allocations over5.032sec. This does
not establish full frame-time/long-session performance or saved campaign transactions.
Evidence and guarded deployment scripts: local-art/ui-overhaul/hamlet-material-v19.
Protected estates1/3 were never accessed. See that folder's CONTINUE.md for current deployment.
The following round266 notes are historical where they conflict with this pass.

Native1920x1080 overview and final deployment are recorded separately in the private
checkpoint. Generated stone remains too regular for final DD2 art parity; subsequent work
must improve painted shapes and weathering rather than merely adding texture resolution.


## Round268 — distinct service materials and DD2 detail replacements (2026-10-19)

Round268 responds to the owner's screenshot of bland shared brickwork and mismatched DD1
details. Abbey/Guild/Sanitarium now use separate paintings: worn limestone, warm masonry with
broken plaster, and cool damp limewash respectively. Nine native-reference paintings include
those walls, slate/clay roofs, continuous wood and three service emblems. Original DD2 atlases
remain style references; generated paint is not described as native extraction. Low-resolution
wood/plaster strips are replaced on the rebuilt services, stone trim uses a selected worn block,
and paving is finer. UVs retain metric courses and follow each timber's long axis.

Guild banner, forge sign and wagon eye have new DD2-style paintings. Window grain comes from
DD2's Academic house, with physical rose tracery replacing the Abbey's DD1 sprite. Pixel-hash
inspection of all ten exported GLBs finds none of the six legacy DD1 detail inputs. Five GLBs
are unchanged; Abbey adds720 triangles (9393 total), all models remain under45k. Two iterations
were rendered after GLB re-import. The source preparer reproduces ten selected textures exactly.

Asset-only change: the installed round267 plugin is unchanged, and gameplay WIP is excluded.
Native unsaved fixture passes ten world services, Escape, camera, full release/reload/re-entry,
Embark park/cache/resume, resize, keyboard and still-camera/smoke. No estate selected or saved.
Private package: ten models,61 hashed files,117.5MB. The existing packager duplicates embedded
image payloads in mesh buffers; package size/long-session memory remain optimization work.

Evidence, originals, prompts, provenance, both mesh iterations, audit, native captures and fresh
baseline/deployment records: local-art/ui-overhaul/hamlet-material-v20. Further quality work:
forge roof/hood/anvil, wagon joinery, physical Guild crest, bespoke facade wear and more native
lighting integration. Do not claim final DD2 production-art parity from texture detail alone.


## Round269 — workshop materials and fitted details (2026-10-19)

Round269 refines the forge, Nomad Wagon and Guild crest. The forge has a thick slate
canopy, correctly oriented small firebrick courses, recessed hearth, faceted iron anvil
and working-face detail. The wagon has worn plum canvas without the repeated atlas,
fitted door boards, open stair treads and enclosed side/rear walls. The Guild crest has
tapered blades, guards, pommels and a steel-edged shield. Three new generated paintings
use native DD2 references; an earlier cracked iron painting was rejected. Native cottage,
Hoarder and camp component geometry/UVs are preserved. Seven source GLBs are byte-identical.

Three exported-model review passes caught rotated chimney UVs, oversized stair grain and
the original wagon's open sides. Final GLBs pass re-imported topology/bounds/image checks;
the complete ten-model audit finds none of the six old DD1 detail bitmaps. Asset-only:
normal267 plugin unchanged, no new C# test claim. See private native.log and deployment.json
for exact final native acceptance and installation, rather than inferring it from renders.

Private evidence and reproducible inputs: local-art/ui-overhaul/hamlet-workshops-v21.
The remaining native cottage thatch is softer than the new canopy, forge roof joins need
further architectural work, and wagon end-board grain remains large. Broader lighting,
NPC/upgrade exteriors, saved transactions and long-session memory are unfinished. The
packager still duplicates embedded image payloads; this round does not fix that cost.


## Status 2026-10-19: workshop pass installed

Final native fixture PASS: ten services, camera, full release/reload, Embark cache/resume, resize, keyboard and still camera/smoke. Zero live OS input; no estate selected/saved. Fresh1920 overview inspected;214renderers,0 repeated buffer allocations/5.039sec. Final asset package installed, normal267 plugin unchanged, native profiles/config/estate2 restored and hashes verified. DD2 CLOSED. Protected estates untouched. Next round270.


## Round270 — fitted smithy and detailed thatch (2026-10-19)

Round270 replaces the forge's intersecting native cottage and slate canopy with one
continuous thick thatched roof, fitted dormer, real chimney openings and enclosed timber/
lime workshop. A higher eave reveals the anvil and recessed furnace; the sign hangs clear
of the roof. Three generated native-reference paintings supply detailed reed, stained
lime and static furnace fire. This is generated paint, not newly extracted native art.

Three offline iterations corrected the low eave, hidden sign, glass-textured hearth and
side-window placement. The final exported smithy has 5,630 triangles and ten embedded
images, re-imported for topology/bounds/paint checks and four review views. Native camp
barrel/crate geometry and UVs are retained; only the cottage is explicitly rebuilt.
Nine other service GLBs are byte-identical to v21. Ten-model decoded-pixel audit excludes
the six old DD1 detail bitmaps. Prepared inputs exactly match rendered texture files.

Asset-only round: normal267 plugin unchanged; no new C# test claim. Native unsaved fixture
passes ten services, camera, full release/reload/re-entry, Embark cache/resume, resize,
keyboard and still-camera/smoke. No estate selected or saved; no live OS input. Inspect
native-overview.png, native.log, native-buffer-probe.json and deployment.json in
local-art/ui-overhaul/hamlet-smithy-v22 for final visual, allocation and install evidence.

Remaining work: wagon end grain/wheels, service-specific facade wear, remaining old
service architecture and broader lighting. Thatch repetition and straight roof side
edges can still improve. The furnace painting is static. Package is 164.2 MB/64 hashed
files and still duplicates embedded images in buffers; no full performance claim.
NPC/upgrade exteriors, saved transactions and forced-reload portrait defects remain.


## Round271 — ground detail and nurse removal (2026-10-20)

Round271 addresses the owner's ground screenshots and removes the Sanitarium nurse.
Streets use DD2's unmodified cobblestones_base tile at4.8m, with repeat wrapping instead
of mirrored rocks. Generated1254px earth supplies small gravel, clay wear and dead leaves;
native grass remains, with finer4.2m mapping, fuller sheltered margins and187 plants grouped into irregular clumps.
The lane image already blends final ground pixels, so the second runtime paving tint
is removed. Terrain stays4096px with512px construction readbacks and cached worker bakes.
Two optional private PNGs prepare asynchronously and retain the previous native fallback.

Enlarged native samples proved the atlas UVs and alpha clipping are correct. The early
small, heavily tinted tufts exposed the painting's black basal mass and read as stumps.
Final plants are grouped, taller and brighter, with 35% planted below the soil. All 187
plants preserve route/apron/slope clearances. The runtime uses explicit alpha clipping.
No new grass art was generated. Diagnostic giant cards/readback are excluded from the build.

The static Sanitarium nurse and the blue robe-shaped foreground Ancestor figure are
removed. The latter matches the figure in the owner's road screenshot. Its stone plinth,
steps, native candles and memorial service remain. Both legacy authoring paths now omit
the figure. Eight other GLBs are byte-identical to round270. The first model refinement
caught 30 figure/detail meshes rather than the 14 old base meshes; the final recipe removes
all 30 and preserves the native candles and retained model geometry/UVs. The exposed
memorial plinth replaces its stretched tiny crop with the reviewed limestone painting
and native small paving at a consistent world scale.

Release build, 1268 Core checks and 284 UI checks pass. The final vegetation revision
passes all 72 focused Hamlet checks. A frozen-checkout catalog-path
failure was resolved by supplying the exact installed catalog, then rerunning that case.
Paint snapshots/count/wear expectations reflect the intended new art; route/apron/slope
invariants remain. A new lane-margin case checks exact agreement with the ground image.
Native unsaved ten-service/camera/release/reload/cache/resize/keyboard checks pass; no
estate selected or saved. Final evidence and deployment hashes are recorded privately in
local-art/ui-overhaul/hamlet-ground-v23. Foreign gameplay/curio/Shieldbreaker work excluded.

Street cobbles and ground grass are native 512px paint; tuft atlas is 4096px.
Generated earth and the reused v20 limestone painting are not native extraction. Ground painting is
still flatter than native dressed scenes, service plinths retain larger paving, and fuller
lighting/foliage treatment remains. No full frame-time/long-session claim. The old offline
town_preview approximation does not yet reproduce this ground; use the native captures.


## Round272 — native service terraces and continuous stair paving (2026-10-20)

Round272 replaces stretched service-court paint with native DD2 cobbles and intact
foundation_panel_06 stone courses. Abbey, Guild, Sanitarium, Tavern, Blacksmith and
Graveyard retain their geometry; the other four GLBs remain byte-identical to round271.
Authored surfaces account for the runtime fit of each service, keeping paving at a
6.2-unit period and retaining courses at 3.8 by 1.8. Individual graveyard blocks use
individual painted stones rather than a miniature brick wall on each block.
The burial surface reuses the reviewed round271 generated earth; no new art generated.

Connecting stairs use native street cobbles at 4.8 units, with continuous world UVs
instead of restarting one wall-panel strip per tread. Native retaining paint excludes
the panel's cap and loose black rubble. The added stair material shares the cached
graded cobble texture and follows the existing scene-release ownership.

Release build and all 1268 Core checks pass. Six exported models were re-imported,
rendered from four sides and verified; the final graveyard earth revision was separately
re-imported and rendered. Triangle counts remain unchanged. The native unsaved fixture
passes ten service clicks, camera, release/reload/re-entry, cache, resize and keyboard.
Fresh 1920x1080 overview and a five-second buffer-allocation probe were inspected.
No estate selected or saved. Frozen HEAD0796f84 plus five owned runtime/test files
excludes concurrent gameplay/curio/Shieldbreaker work. Evidence and deployment hashes:
local-art/ui-overhaul/hamlet-terraces-v24.

Remaining art work: broad cliff repetition, lighting, roof repetition and service-specific
facade wear. Ground still lacks the dressing of native DD2 scenes. The old offline town
preview predates the current ground; native captures remain the acceptance reference.
The package still embeds duplicate image bytes in buffers. No full frame-time,
long-session, controller or saved-transaction claim; forced-reload portrait issue remains.


## Round273 — fitted Abbey and Sanitarium masonry (2026-10-20)

Round273 fits new masonry to the Abbey and Sanitarium while preserving their native
components and exact outer bounds. The Abbey has separate arch courses, stepped ashlar
buttresses, outward-sloping weathering caps, eave corbels, drip courses and rear lancets.
The Sanitarium has fitted portal stones, recessed front/rear cellar windows, corner
quoins and revised cylindrical plaster mapping. Eight other GLBs are byte-identical.

This pass reuses the reviewed v20 generated stone/plaster paintings and native DD2
glass. No new bitmap art is generated. Individual stone faces sample detailed portions
of the approved full painting instead of a tiny stretched band. Native tower, house and
candle geometry/UVs remain exact. The final Abbey has16988 triangles and Sanitarium14975;
both exported bounds match the source exactly. Two complete offline iterations were
re-imported, checked for decoded images/topology/bounds and rendered from four sides.
The second adds rear openings and corrects side-cap orientation.

Asset-only installation: normal round272 plugin unchanged, no new C# test run.
Native unsaved ten-service, camera, full release/reload/re-entry, cache, resize and
keyboard checks pass with zero live input. Fresh1920x1080 overview inspected; see the
five-second allocation probe and deployment hashes in local-art/ui-overhaul/hamlet-masonry-v25.
No estate selected/saved; config, estate2, native profiles and other assets preserved.
Foreign gameplay/curio/Shieldbreaker work excluded.

Native production parity remains unfinished. Roof repetition/edges, broad cliff
repetition, scene lighting and ground dressing still need work. The offline town
preview predates the new ground; use native captures. Package still duplicates image
bytes in buffers. Forced-reload portraits, saved transactions, controller support and
full frame-time/long-session validation remain outside this art pass.


## Round274 — physical slate and reed roof courses (2026-10-21)

Round274 replaces the Abbey's smooth painted roof surfaces with individual,
overlapping slate courses and folded ridge caps. The Smithy has thick, irregular
reed bundles around real chimney and dormer openings, with a painted underlay.
The service footprints and all accepted masonry stay exact; eight other GLBs
are byte-identical. Native mesh positions, transforms and UVs remain unchanged.

This reuses the reviewed v20 slate and v22 reed paintings without generating
new bitmap art. Native Farm/Valley cottage silhouettes and the retained Sprawl
tower are the references. The final Abbey has 29012 triangles;
the Smithy has 17590. Pieces share combined meshes/materials.

Four offline iterations were exported, re-imported and rendered from four sides.
Early versions were rejected: the old thick roof intersected the new courses,
and boolean cuts on intersecting reed bundles silently removed neighbouring
pieces. The final pass fits the exported outer surface and clips each bundle
before joining. A sampled top-surface audit catches the rejected Smithy's 34%
coverage and measures 99.5% on the accepted roof; it is not a complete watertightness
proof. Full native appearance remains the acceptance check.

Asset-only installation, normal round272 plugin unchanged; no new C# test run.
Native unsaved ten-service, camera, full release/reload/re-entry, cache, resize,
keyboard and still-camera checks pass. Fresh1920x1080 overview inspected, plus
the five-second buffer-allocation probe. No estate selected/saved, zero live input.
The interrupted hidden-window launch did not initialize the OnGUI preview; the visible
game-window rerun renders and completes the fixture with the identical binary/assets.
Config, estate2, native profiles and other assets preserved. Foreign gameplay,
curio and Shieldbreaker work excluded. Evidence: local-art/ui-overhaul/hamlet-roofs-v26.

Native production parity remains unfinished. Smithy underlay/end-grain detail, Guild/Sanitarium roof trim, broad
cliff repetition, lighting and ground dressing remain. Package image duplication,
old offline ground preview, forced-reload portrait and saved/full-performance
acceptance limits remain unchanged.


## Round275 — Smithy roof underlay and edge paint (2026-10-21)

Round275 fits the Smithy underlay closely beneath its reed courses and repairs
collapsed texture coordinates on the bundle sides and curved end caps. The original
upper/lower reed painting and course geometry remain exact. Nine other GLBs are
byte-identical; all service bounds and triangle counts are unchanged. Smithy17590 triangles.
No new images or C# changes; no new C# tests. Existing reviewed v22 paint is retained.

Three offline variants were exported/re-imported and rendered from four sides.
The independent edge audit confirms identical course geometry/upper-lower painting;
nondegenerate faces with collapsed UVs fall from6433 to24 on the main roof and369 to0
on the dormer. Five tiny pre-existing degenerate side triangles remain unchanged.
This is a refinement, not a claim that every mesh issue is resolved.

Native unsaved ten-service/camera/release/reload/re-entry/Embark/cache/resize/keyboard/
still-camera checks pass, with zero live input and no estate selected/saved. Fresh
1920x1080 overview reviewed and five-second allocation probe passes. Asset-only:
normal272 binaries unchanged. Saves/config/other assets and foreign gameplay WIP
preserved; protected estates1/3 untouched. Evidence: local-art/ui-overhaul/hamlet-roof-finish-v27.
Guild/Sanitarium roofs, broader lighting/cliff/ground dressing, full saved/controller/
performance acceptance and the existing forced-reload portrait gap remain open.


## Round276 - Guild and Sanitarium roof joins (2026-10-21)

Round276 gives the Guild fitted clay courses, folded ridge saddles, dormer
flashing and exposed eave rafters. The Sanitarium gets fitted turret and porch
slate courses, retaining its original hip bindings. The native Academic's house
and all other native components keep their geometry and UVs. Existing reviewed
clay/slate/oak/iron paintings reused; no new bitmap generation. Eight service
GLBs are byte-identical and all ten outer bounds retained. Guild 25115 triangles;
Sanitarium 21551 triangles. Asset-only, normal272 binaries unchanged.

Three exported/re-imported four-view iterations. First clay crops included baked
shadow bands, making false holes despite full main-roof coverage. Interior face
crops and dark physical edges fix this. A separate small-roof underlay correction
eliminates sampled intersections. Final sampled coverage is 100% for the Guild
main/armory and Sanitarium porch, 99.84% for the turret. This is a sampled coverage
check, not a watertightness proof. Restoring the narrow hip bindings preserves the
original bounds after the second candidate contracted the turret eave by 2 cm.

Final native unsaved ten-service/camera/release/reload/re-entry/Embark/cache/resize/
keyboard/still-camera checks pass. Fresh 1920x1080 overview inspected; five-second
buffer probe passes. No live OS input or estate selection/saving. Config, estate2,
native profiles, other assets and foreign WIP preserved; protected estates untouched.
No C# edit/build/test this round. Evidence: local-art/ui-overhaul/hamlet-roof-joinery-v28.

The town still needs broader lighting, cliff repetition and ground dressing work.
Facade-specific wear, NPC/upgrade exteriors, saved transactions/controller/full
performance, forced-reload portraits and package image duplication remain open.


## Round277 preparation - native lantern study (2026-10-21)

Round277 preparation: native lantern comparison and a private Guild proof are saved in
local-art/ui-overhaul/hamlet-lanterns-v29. Installed state remains verified round276
(commit9197120), normal272 plugin, game closed; hashes rechecked. No new deployment.
The common lamppost/cropped head is rejected. Native assistance square/round lamps
are suitable candidates with original paint/UVs; hero-selection cage is unlit and
needs further treatment. Guild square-lamp proof exported/re-imported with nine exact
other GLBs, four renders produced, hero view reviewed. It is not yet tested in game.
No generated art needed. Next round remains277; fresh baseline required before deploy.


## Round277 - native building lanterns (2026-10-21)

Round277 replaces six primitive glowing cubes with native DD2 assistance lamps:
square amber lamps at the Guild/Tavern and round pale lamps at the Sanitarium/Caravan.
Original geometry proportions, painted UVs, chain, ring and cage are retained.
No new generated art. Fitted brackets attach the lamps to walls. Native geometry
queries revealed the Tavern's previous lamps sat two to three units ahead of its
facades; both now sit beside actual walls, outside the passage. The Sanitarium
bracket reaches its porch jamb and the Caravan supports reach its timber facade.

Two four-service exported/re-imported iterations follow the earlier three-lamp
reference study. All six new lamp meshes match native reference proportions/UVs;
all original native components retained, six other GLBs byte-identical and all
service bounds unchanged. The first candidate's detached mounts were corrected.
Source hashes, mesh audit, render reviews and package are private in
local-art/ui-overhaul/hamlet-lanterns-v29. No C# changes/build/tests; normal272 binaries.

Final native unsaved ten-service/camera/release/reload/re-entry/Embark/cache/resize/
keyboard/still-camera checks pass. Fresh 1920x1080 overview inspected, five-second
buffer-allocation probe passes. No live OS input or estate selection/saving. Config,
estate2, native profiles, other assets and foreign WIP preserved; protected estates
untouched. Wider lighting and facade wear remain. These lamps use painted light;
this pass does not add dynamic lights or prove full frame-time performance.


## Round278 - Tavern fixture supports (2026-10-21)

Round278 fixes the Tavern's floating sign and two old lantern backplates.
Move the complete sign toward the tower, brace its bracket and give both suspension
chains a crossbar. Seat the backplates behind the newly installed native lamps.
The initial private study lacked the crossbar; final source models-final includes it.
Existing paint, native parts and UVs are retained. No new bitmap or C# changes.

One exported/re-imported Tavern, four rendered views, native component preservation,
nine byte-identical other GLBs and exact service bounds. Tavern17981 triangles.
Native unsaved ten-service/camera/release/reload/cache/resize/keyboard checks PASS.
Fresh1920x1080 overview inspected; five-second buffer probe recorded in private
hamlet-signs-v30. Normal272 binary unchanged. No estate selection/save or live OS
input. Configs, estate2, native profiles and foreign WIP preserved; protected estates
untouched. Wider facade wear/lighting, cliff repetition and ground dressing remain.


## Round279 - shared service palette (2026-10-22)

Round279 gives interactive buildings shared material finishes for limestone/lime,
weathered timber, slate, clay and cloth. Keep service accents and warm glass/fire;
preserve painted ink/shadows. Older and newer slate names now share the same finish.
Mixed native inn/coach/prop atlases receive a gentle common grade. Source textures,
meshes, UVs, all ten asset models and scene geometry are unchanged. No generated art.

Release build passes,1274 Core tests pass. Initial isolated test run missed its
relative game catalog; copied the installed catalog privately and all tests passed.
Native unsaved ten-service/camera/release/reload/cache/resize/keyboard checks PASS;
fresh1920x1080 palette overview inspected, buffer probe recorded. Build from clean
HEAD plus three owned files; concurrent foreign changes excluded. Configs, estate2,
native profiles/other assets preserved, protected estates untouched. No live input
or estate selection/save. Colour harmony improved; full lighting, cliff repetition,
ground dressing and additional facade wear remain. Do not claim production parity.


## Round280 - Guild fitted stonework (2026-10-22)

Round280 refines the Guild portal, bonded corner stones and armory window surround.
Replace long jamb strips with fitted limestone courses and drip mouldings. Corner
stones turn onto both faces with alternating lengths. Reuse reviewed Abbey stone
samples so new parts receive the shared279 limestone finish. No new bitmap, C# or
native part changes. Native meshes and UVs retained; all service bounds exact,
nine other GLBs byte-identical. Guild28127 triangles,65-file package176.1MB.

One four-view exported/re-imported model reviewed. Side-window geometry was probed
because the wing roof partly occludes the casements; glass/recesses are intact, so
no window movement was made. Native unsaved ten-service/camera/release/reload/cache/
resize/keyboard checks PASS; fresh1920x1080 overview inspected and buffer probe
recorded. Runtime279 unchanged. No live input/estate selection/save. Configs,
estate2/native profiles/other assets and foreign WIP preserved, protected estates
untouched. Facade-specific wear, lighting contrast, cliff repetition and ground
dressing still need work. This is an architectural refinement, not production parity.


## Round281 - architectural contact shading (2026-10-22)

Round281 adds static contact visibility to service geometry using a private Blender
BVH bake.16 short hemisphere rays per triangle, radius1 authoring unit. Connected
coplanar triangles share area-weighted visibility to avoid diagonal wall patches.
Native lamps/candles/flames and explicit emissive objects retain their prior shade.
Opaque non-light surfaces occlude; alpha-cutout cards do not cast solid box shadows.
The existing three material facets consume the optional scalar accessor, so the
renderer count remains255. Palette279, source geometry, UVs, materials and PNGs stay
exact; audit verifies original binary prefixes and all original accessors/nodes.

Private glTF extras hamlet_contact_shade references a SCALAR visibility accessor per
primitive triangle. Loader validates count/range/finite values, and CutOut/back-face
helpers preserve triangle correspondence. Older/non-Hamlet models omit it and keep
their existing shading. Source data is plain arrays; no per-frame rays or new lights.
Future art changes must repackage raw GLBs and rebake, not bake an already baked pack.

Two-building study and first whole-town preview inspected before planar averaging.
Final native full ten-service/camera/release/reload/cache/resize/keyboard checks PASS,
all ten contact accessor counts present in native logs. Fresh1920x1080 final overview
inspected, five-second allocation probe recorded. Release build and1282 Core tests
pass. Synthetic covered/open planes and coplanar/folded faces pass. Native lamps
and shared colours retained. The effect is restrained contact depth; dynamic shadows
between buildings, broader lighting direction, cliff/ground dressing remain.

Frozen HEAD plus four owned runtime/test files excludes foreign WIP. Configs/estate2/
native profiles/other assets preserved; protected estates untouched. No live OS input,
no estate selected/saved. Evidence local-art/ui-overhaul/hamlet-contact-v34. Raw source
geometry remains v33/models, accepted runtime package is HamletModels-reviewed.


## Round282 - native Tavern sign (2026-10-22)

Round282 replaces the Tavern's primitive tankard sign with the native DD2 inn
crest from inn_lvl_02_prefab/the_inn_sign_01. Retain its painted wood/gold, crossbar,
links, original proportions and UVs. A fitted arm and existing diagonal brace join
it to the tower wall. No new bitmap or C# change. Palette279/contact281 retained.
Tavern18167 triangles; all service bounds unchanged. Nine other raw GLBs and their
baked glTF/binaries are byte-identical. Four exported/re-imported views reviewed;
native source triangle/UV comparison passes within2e-6. Full contact bake audited.

Native unsaved ten-service/camera/release/reload/cache/resize/keyboard checks PASS;
fresh1920x1080 overview inspected and allocation probe recorded. Runtime281 binaries
unchanged, so no build/test rerun for this asset-only round. No estate selected/saved.
Estate2/native profiles/configs/other assets/foreign WIP preserved; protected estates
untouched. One window focus after idle>7hours resumed the native startup splash;
no click/key input was sent. Game closed after QA.

Private evidence: local-art/ui-overhaul/hamlet-cliff-relief-v35. Despite its study
folder name, accepted art is the Tavern sign; the native cliff candidates were
rejected as insufficient detail for the central terraces. They are not installed.
The private seamless cliff texture still uses the native atlas crop and mirror
wrap; test full-tile repeat/aspect before replacing any more art. Broader terrain,
facade wear and directional lighting remain; production parity is not established.

## Status 2026-10-22, round282

Accepted sign package installed, runtime281 retained, game closed. See v35/CONTINUE.md. Next283 private cliff full-tile sampling test. No pending restores.


## Round283 - whole cliff painting (2026-10-22)

Round283 corrects private cliff painting: hamlet-cliff-v17.png is a whole square
seamless tile, but previously received the farm_eroded_hill atlas crop, Mirror wrap
and cropped-atlas aspect. It now receives full-image Repeat and square world UVs
across both terrain and outcrops. Preserve the existing private colour grade and
16-unit period. Native fallback keeps its original rock crop, Mirror and760/960
aspect. PrepareTownArt precedes the model worker so it can select the UV contract.

No new texture, model, layout, material colour, building or renderer group. Palette279,
contact281 and native sign282 retained;254 renderers. All private assets byte-exact.
One regression verifies unchanged hillside/outcrop geometry, unchanged earth UVs and
world density for full tiles/native fallback. All1283 Core tests and Release pass.
LayoutExport accepts --full-cliff-tile so the offline cliff UVs match the native scene;
its private full layout export succeeds. Native unsaved ten-service/camera/release/
reload/cache/resize/keyboard/still-camera/smoke PASS; full-tile mode confirmed in log.
Fresh1920x1080 overview inspected and allocation probe recorded. No estate selected/
saved. Configs/estate2/native profiles/other assets/foreign WIP preserved, protected
estates untouched. Game closed after QA. Frozen HEAD plus seven owned runtime/test
files excludes concurrent agents' work.

Evidence local-art/ui-overhaul/hamlet-cliff-sampling-v36. Native raw/baked models
remain v35/models and v35/HamletModels-reviewed. Runtime283. Whole painting is now
used coherently; broad cliff silhouettes, facade wear, ground dressing, directional
lighting and long-session acceptance remain. Native fallback is covered structurally
and by the density regression; no separate native launch with private paint absent.

## Status 2026-10-22, round283

Runtime283 and native Tavern sign package282 installed. Game closed, no pending restores. Latest v36/CONTINUE.md; next284 cliff geometry/ground dressing/facade wear. No extra assets generated.


## Round284 preparation - Abbey entrance iron, native pending (2026-10-22)

Round284 preparation: Abbey split tapered hinges, hammered studs, pin/knuckle
fittings and ring plates on the existing oak door. Reuse reviewed iron_v21; no new
bitmap or C# change. Three private fitting studies; candidate models-reviewed,
package HamletModels-reviewed. Abbey29588 triangles. All724 retained Abbey meshes
keep textured triangles within4.77e-7; support and oak-contact checks pass. Nine
other raw/baked models exact; all service bounds unchanged. Four model views and
close door-detail.png reviewed; package/contact bake audited.

Native acceptance pending: one unsaved preview attempt stalled at the publisher
intro before any Hamlet model was loaded. Focus and one Escape key did not resolve
startup. Candidate package was restored to the exact accepted round283 installation,
game/observer closed. Runtime283 and accepted raw/baked v35 sign models remain
installed. Configs/estate2/native profiles/other assets restored and verified;
protected estates untouched. No build/test rerun for this asset-only preparation.
Broader scene/texture quality remains; this is not native acceptance or production parity.

## Status 2026-10-22, round284 preparation

Native QA pending; accepted283 restored, game/observer closed. Next native acceptance of v37 model. No pending restore.


## Round284 accepted - Abbey entrance iron (2026-10-22)

Round284 is now accepted in the native Hamlet. Split tapered hinge straps,
hammered studs, pin/knuckle fittings and ring plates refine the Abbey's existing
oak entrance. Reuse reviewed workshop iron; no new bitmap or runtime change.
Raw source remains v37/models-reviewed; accepted baked package v38/HamletModels-reviewed,
byte-identical to the reviewed v37 candidate. Abbey29588 triangles. All724 retained
Abbey meshes preserve textured triangles within4.77e-7. Ring supports and plates
meet physically. All service bounds unchanged; nine other raw/baked models exact.
Four final model views and door-detail.png reviewed in v37.

Fresh v38 baseline captured after the usage reset, preserving intervening work.
Full native unsaved ten-service/camera/release/reload/Embark-cache/resize/keyboard/
still-camera/smoke checks PASS. Fresh1920x1080 overview reviewed; five-second buffer
probe recorded.256 renderers, two more for the new iron paints. Runtime283 DLL/PDB
files exact, no C# build/test rerun for this asset pass. Shared palette279, contact281,
Tavern sign282 and cliff sampling283 retained. No estate selected/saved. Configs,
estate2/native profiles/other assets/foreign WIP preserved; protected estates untouched.
Game closed after QA. The earlier publisher-intro stall did not recur on this retry;
its cause remains unestablished, and no startup code was changed.

Commit ed0368d prepared the source; this acceptance supersedes its native-pending
status. Private v38 contains final native evidence/deployment/checkpoint; v37 holds
authoring studies. Wider facade/plank painting, ground dressing, cliff shape and
scene lighting still need work. Native production parity is not established.

## Status 2026-10-22, round284 accepted

Installed Abbey iron; runtime283 retained, game closed, no pending restore. Newest v38/CONTINUE.md. Next285 broader painting/ground/lighting.


## Round285 - fitted Abbey oak painting (2026-10-22)

Round285 gives the Abbey entrance six fitted oak planks. Clip each board to the
existing pointed door, preserve the backing and all fitted iron, replace only five
old ink joint rods. Four continuous900x80px grain strips from reviewed wood_v20
match the roughly11:1 board aspect. Map by actual board length; narrow kerfs leave
97.24% face coverage. This avoids stretching the painting's broad black splits
across the whole door. Same wood colour/emission and shared timber family finish.
No new bitmap generated or extracted. The engine samples the original full image;
grain-strip-review.png is a diagnostic sheet, not installed paint.

Abbey29624 triangles.749 retained Abbey meshes/textured surfaces remain within
4.77e-7, including oak backing, native parts, iron, walls and roof. All service
bounds exact; nine other raw GLBs and baked glTF/binaries byte-identical. Four
exported/re-imported views and close detail reviewed; plank coverage/support and
raw/baked contact audits pass. Accepted raw models/ and package HamletModels-reviewed.

Native unsaved ten-service/camera/release/reload/cache/resize/keyboard/still-camera/
smoke checks PASS; fresh1920x1080 overview inspected and allocation probe recorded.
Runtime283 DLL/PDB files exact; no runtime change or build/test rerun for this asset
pass. Shared palette279, contact281, sign282, cliff283 and iron284 retained. No
estate selected/saved; configs/estate2/native profiles/other assets/foreign WIP
preserved and protected estates untouched. Game closed after QA.

Private evidence local-art/ui-overhaul/hamlet-oak-planks-v39. Source grain rects,
source hash and dimensions are in plank-candidates.json; physical outlines/UVs/
coverage in models/plank-fit.json. Wider facade painting, ground dressing, cliff
silhouette and scene lighting remain. This is a local material/fit improvement,
not whole-town native DD2 production parity.

## Status 2026-10-22, round285

Installed fitted oak/iron Abbey, runtime283 retained, game closed. Latest v39/CONTINUE.md. No pending restores. Next286 broader materials/terrain/light.


## Round286 - shared native-reference wall paintings (2026-10-22)

Round286 replaces the primary Abbey, Guild and Sanitarium wall paintings with
three private native-reference paintings. Broad painted stone facets, deliberate
ink seams and quieter wear fit the extracted DD2 inn/Academic's house language.
Keep distinct stone, broken lime and clinical lime finishes in the shared279
palette; retain all native atlases, fitted trim, roof and oak/iron work. New full
paintings are1254px, without artificial upscaling. Built-in image generation was
used; prompts, native references and rejected studies are saved privately in v40.

Fit one painting around the Sanitarium tower. Fit the undercroft/porch wall height
to expose worn paint edges; the exposed undercroft top reuses the native courtyard
paving. All geometry and service bounds retained. Seven other raw GLBs and baked
glTF/binaries byte-identical. Geometry/UV audit retains all original surfaces;
only three named Sanitarium UV layouts intentionally change. Non-wall material
factors/decoded pixels and native parts remain exact. Twelve reimported model
views, tile study, original paint/geometry and contact bake audits reviewed.

Native unsaved ten-service lifecycle/camera/release/reload/Embark-cache/resize/
keyboard/still-camera/smoke PASS. Fresh1920x1080 overview inspected. Allocation
probe 0 reallocations over5.042sec. Runtime283 unchanged;
no C# change or build/test rerun for this asset-only pass. Palette279/contact281/
sign282/cliff283/iron284/oak285 retained. Game closed after QA. No estate selected
or saved. Estate2/native profiles/config/other assets and foreign WIP preserved;
protected estates untouched. Accepted raw models-reviewed/; baked HamletModels-reviewed/.

Private evidence local-art/ui-overhaul/hamlet-facade-paints-v40. More work remains
on the older roof/trim paintings, ground dressing, cliff form and scene lighting.
This improves the service wall language; whole-town native production parity is open.

## Status round286

Accepted v40 paintings/models installed, runtime283 retained, game closed. No pending restore. Next287 roof/trim painting or wider lighting/terrain.


## Round287 - larger painted roof faces (2026-10-22)

Round287 fits larger native-reference painted faces onto the existing Abbey,
Guild and Sanitarium roof courses. Two new private1254px atlases hold twelve
slate/clay faces each. Reviewed sampling windows are about292x389px, replacing
the earlier47–187px-wide,45–116px-high samples. Broad grey/plum slate and muted
maroon clay brushwork replace busy mottling; the shared279 colour families remain.
Built-in image generation used native Academic/Inn03 atlases as references, with
prompts/provenance saved privately. No artificial upscaling or native atlas edits.

Retain every physical tile, overlap, ridge saddle, dormer, flashing and underlap.
Remap only the recorded physical roof face meshes. Previously collapsed Abbey
cut-side UVs receive finite local paint and one shared dark slate edge material.
All geometry, bounds and triangle counts exact; all other UVs/material factors/
decoded pixels and native art retained. Seven other raw/baked services exact.
Wall286 and door284/285 retained. Twelve reimported views and geometry/paint/atlas/
contact audits reviewed. Accepted raw models-reviewed and baked HamletModels-reviewed.

Native unsaved ten-service lifecycle/camera/release/reload/Embark-cache/resize/
keyboard/still-camera/smoke PASS. Fresh1920x1080 overview inspected. Allocation
probe 0 reallocations over5.031sec. Runtime283 DLL/PDBs
unchanged; asset-only, no C# build/test rerun. No estate selected/saved. Native
profiles/estate2/config/other assets and foreign WIP preserved; protected estates
untouched. Game closed after QA. Private evidence hamlet-roof-paints-v41.

The roof painting now shares the walls' broader painted forms. Older fitted trim
painting, ground dressing, cliff silhouette and scene lighting still need work.
Whole-town native DD2 production parity remains open.

## Status round287

Accepted v41 roof paints/models installed, runtime283 retained, game closed. No pending restore. Next288 fitted trim paint or wider scene work.


## Round288 - shared fitted stone painting (2026-10-22)

Round288 unifies fitted stone courses across Abbey, Guild and Sanitarium with
the unchanged reviewed wall286 painting. Eight whole stone-face windows retain
painted edge highlights and dark seams. The first interior-only crop study was
too flat and is not installed. No new bitmap generation or material-factor change.
Stable per-mesh selection varies faces while preserving the shared stone palette.

All geometry, service bounds and triangle counts retained. Only recorded fitted
limestone UVs/paint change; other UVs/material factors/decoded pixels remain exact,
including native art, wall286, roof287 and oak285/iron284. Seven other raw GLBs
and baked glTF/bin byte-identical. Twelve reimported views and geometry/paint/
noncollapsed trim UV/contact audits reviewed. Accepted raw models-reviewed and
baked HamletModels-reviewed. Shared279 palette and runtime283 remain unchanged.

Native unsaved ten-service lifecycle/camera/release/reload/Embark-cache/resize/
keyboard/still-camera/smoke PASS. Fresh1920x1080 overview inspected. Probe reports
0 reallocations over5.03sec. Asset-only; no C# build/test
rerun. No estate selected/saved. Estate2/native profiles/config/other assets and
foreign WIP preserved; protected estates untouched. Game closed after QA.

Evidence local-art/ui-overhaul/hamlet-trim-paints-v42. Main service stone/roof
finishes now share broader native-reference paint forms. Legacy moulding/verge
painting, ground dressing, cliff silhouette and scene lighting remain. Whole-town
native DD2 production parity is still open.

## Status round288

Accepted v42 fitted trim/models installed, runtime283 retained, game closed. No pending restore. Next289 legacy moulding/verge/ground/cliff/lighting work.


## Round289 - coherent window and verge stone (2026-10-22)

Replaces the legacy moulding paint on182 Abbey/Guild/Sanitarium pieces with
unchanged reviewed wall286 stone faces. Stable per-mesh variation retains painted
rim/ink detail. Local face axes orient each member and give end caps finite UVs.
No new bitmap generation or material-factor change. All geometry/service bounds/
triangles and all non-moulding UVs/material pixels retained, including native art,
wall286, roof287, fitted stone288 and entry oak285/iron284. Seven other raw GLBs
and baked glTF/bin exact. Twelve reimported views, geometry/decoded-paint/finite-UV/
contact audits PASS. Accepted raw models-reviewed and baked HamletModels-reviewed.

Native unsaved ten-service/camera/release/reload/Embark-cache/resize/keyboard/
still-camera/smoke fixture PASS; fresh1920x1080 overview reviewed. Five-second
buffer probe PASS. Runtime283 DLL/PDBs unchanged, asset-only, no C# build/test rerun.
Game closed after QA. Estate2/native profiles/configs/other assets and foreign WIP
preserved; protected estates untouched. Private evidence hamlet-moulding-paints-v43.
Shared service stone now reads consistently. Ground dressing, cliff form, lighting
and small signs remain; whole-town native DD2 production parity is still open.

## Status round289

Accepted v43 installed, runtime283 retained, game CLOSED. No pending restore.
Next290 wider ground/cliff/lighting or small service details, maintaining shared colours.


## Round290 preparation - native medical sign (2026-10-22)

Offline Sanitarium candidate replaces two flat mint cross primitives with DD2's
115-triangle rope-bound hospital cross. Original2048 painting/UVs and uniform
proportions retained; rigid yaw faces the approach and back seats against plaster.
Initial oblique study retained privately, not selected. Four reimported views,
444 retained meshes/UVs, material factors/decoded pixels, native triangle-edge
lengths/UVs/paint, exact service bounds and nine other GLBs PASS.
Private evidence hamlet-medical-sign-v44/models-reviewed, recipe medical_sign.py.

NOT INSTALLED. Native full-town quality/lifecycle/performance QA pending. Accepted289
v43 package/runtime283 exact, game remains closed, no launch/save access this round.
No C# build/test rerun or generated art. Usage reserve requires checkpointing before
native QA. Continue290 with fresh usage/baseline, package/contact bake and native
fixture/fresh overview/probe. Preserve all concurrent work and protected estates.

## Status round290 preparation

Offline candidate reviewed, native QA pending. Installed acceptance remains289.


## Round290 acceptance - native medical sign (2026-10-22)

Accepted the prepared v44 Sanitarium detail after native QA. Native rope-bound
hospital cross replaces two flat mint primitives. Original115 triangles,2048 paint,
UVs and uniform proportions retained; rigid facing/grounded wall attachment.
444 original Sanitarium meshes/UVs and all other material pixels/factors retained.
Bounds exact; nine other raw GLBs and baked glTF/bin byte-identical. Contact bake
audited across all ten services. Raw source v44/models-reviewed, installed baked
package v45/HamletModels-reviewed. No new bitmap generation or C# changes.

Complete unsaved ten-service/camera/full-release/reload/Embark-cache/resize/keyboard/
still-camera/smoke fixture PASS. Fresh1920x1080 overview viewed;258 renderers.
Buffer probe zero reallocations. Runtime283 binaries exact, no build/test rerun.
Game closed after QA. Estate2/native profiles/config/other assets/foreign WIP
preserved; protected estates untouched. Evidence hamlet-medical-review-v45.
This supersedes290 preparation's pending native status. Broader terrain/cliff form,
lighting and whole-town native DD2 production parity remain open.

## Status round290 accepted

v45 medical detail package installed, runtime283, game CLOSED. No pending restore.
Next291 terrain/cliff form/lighting, preserving DD1 composition and shared colours.


## Round291 - bounded cliff relief (2026-10-22)

Adds angular physical relief to steep natural faces, outside fixed building pads,
routes and outer boundaries. Shared grid samples keep adjoining facets closed and
avoid four repeated layout lookups per cell. Retains reviewed cliff paint, shared
service colours, all building/native assets and exact paving/masonry/stairs. No new
bitmap, vertex-count increase or material batch.80868 terrain vertices;15162 render
vertices move, max0.594436m. Diagnostic matched-camera smooth/relief renders and
export audit reviewed. Native full-town overview confirms the restrained shape change.

Frozen HEAD9fef53d plus only three owned geometry/test files excludes foreign WIP.
Release PASS.1285 Core checks PASS in aggregate:1284 initial suite passes, one
catalog-fixture path failure rerun PASS after supplying the private read-only
native catalog.89 Hamlet checks pass. No product/test source workaround for fixture.
Complete unsaved ten-service/camera/release/reload/Embark-cache/resize/keyboard/
still-camera/smoke PASS; fresh1920x1080 overview inspected;258 renderers and zero
buffer reallocations. Runtime291 installed, v45 model package byte-identical.
Game closed; estate2/native profiles/config/other assets and foreign WIP preserved,
protected estates untouched. No estate selected/saved. Evidence hamlet-cliff-form-v46.

## Status round291

Accepted runtime291 and unchanged v45 art installed, game CLOSED, no pending restore.
Next292 service-detail quality: Survivalist canvas uses a tiny thatch strip. Native
forest_tent_01/02 are available with2048 art. Ground/lighting and wider parity remain.


## Round292 - native Survivalist shelter (2026-10-22)

Replaces21 coarse authored shelter/pole/rope/seam/peg/bedroll meshes with DD2's
complete double-peak Tangle tent. Original3420 triangles,2048 paint, UVs and uniform
proportions retained; rigid yaw keeps plain canvas toward the approach. Eight
original equipment/fire meshes and their UVs retained exactly. Camp now6000
triangles/nine meshes. Nine other raw GLBs and baked glTF/bin byte-identical.
Original horizontal footprint/normalization retained; taller5.3m silhouette is
intentional. No bitmap generation. Shared service palette/contact shading retained.

Four-view/source/proportion/paint/geometry/contact audits PASS. Initial width6.4
study passed native checks but was rejected as too small in town; final width8.7
passed a fresh complete unsaved ten-service/camera/release/reload/Embark-cache/
resize/keyboard/still-camera/smoke fixture. Fresh1920x1080 overview inspected.
254 renderers/three chimney plumes; zero reallocations over5.024sec. Normal
runtime291 binaries exact; asset-only, no C# build/test rerun. Game CLOSED.
Estate2/native profiles/config/other assets/foreign WIP preserved; protected
estates untouched, no estate selected/saved. Evidence hamlet-camp-v47.

## Status round292

Accepted scaled campsite package installed, raw models-scaled/ and baked
HamletModels-scaled-reviewed/, runtime291, game CLOSED, no pending restore.
Fresh usage/baseline next293. Maintain service material harmony; wider lighting,
terrain dressing and native production-art parity remain open.


## Round293 - paving illumination continuity (2026-10-22)

HamletPaintBaker previously blended unshaded lane stone over lit earth, erasing
foundation contact and warm doorway pools at full paving coverage. The stone now
receives the same existing world light before the original margin blend. No new
paint/geometry/material batches/palette; earth and valley hashes unchanged, exact
ground pixels outside roads retained. Does not add directional cast shadows.

Two meaningful regressions fail on the old bake: white paved foundations stay255
and black paving loses warm entrance light. Final checks cover actual shaded
approaches, clear southern road centres and native lamp pools. Only the two lane
golden hashes change. Frozen HEAD8baca3c plus owned Core/test files excludes all
foreign WIP. Release and all1287 Core checks PASS, including private native catalog
fixture supplied before the suite. Native complete unsaved ten-service/camera/
release/reload/Embark-cache/resize/keyboard/still-camera/smoke PASS. Fresh1920x1080
overview inspected;254 renderers/three chimney plumes and zero reallocations.
All art/configs/estate2/native profiles/other plugins/foreign WIP exact; protected
estates untouched, no estate selected/saved. Game CLOSED. Evidence hamlet-paving-light-v48.

## Status round293

Runtime293 and unchanged camp292 model package installed, game CLOSED. No pending
restore. Fresh usage/baseline next294. Wider directional lighting, scene dressing
and whole-town native DD2 production-art parity remain open.


## Round294 - native ground in the art preview (2026-10-22)

Adds private capture/conversion and town_preview --native-ground. Three actual
4096px worker-result maps, exact decoded RGBA/row origin, sRGB/clamp and no repeated
preview grade/tint. Core export records Hamlet source hashes; loader rejects stale
sources/altered paint. Reviewed model paints no longer receive the legacy override
in native-ground mode. Core exported geometry/layout exact against accepted291.
Packed Blender render inspected. Native overview/capture from frozen HEAD9ea0a20
plus one PRIVATE file-only capture injection compiled successfully. The fixture
was closed and normal293 restored byte-exact before conversion/offline work.

No production runtime source/art change. All normal binaries/assets/configs/
estate2/native profiles/foreign WIP exact; protected estates untouched, no estate
selected/saved. Game CLOSED, no pending restore, capture fixture NOT installed.
Native lifecycle evidence remains293; no redundant full suite/fixture rerun for
this tool-only change. Ground-map/source/hash/no-double-grade guards pass.
Evidence hamlet-native-paint-v49. Offline authored buildings still look much darker
than native DD2; retain that limit rather than treating the render as final art.

## Status round294

Native-ground art-review tool saved; normal293/camp292 art installed, game CLOSED.
Fresh usage/baseline next295: align service material preview with actual native
palette/tints/contact. Wider lighting and native production-art parity remain open.


## Round295 - Core material review (2026-10-22)

Offline tools use actual Core GltfModel, HamletPalette/BuildingTint/LightBand and
SetDressing facet tints. Exported world geometry/UVs/winding, native contact bands,
41 shared grade variants and pure emission review materials replace the older
diffuse authoring-light approximation. Native models/ground keep source/version/
image/reader guards. --resources reads previous extracted cache into a new output.
All ten geometry audits pass, max world-position difference5.58e-6m, UV float32 and
winding exact; original triangle counts retained.41 decoded graded PNG/alpha
checks pass. Packed scene reopens with121 retained images and exact ten-service
triangle counts/unlit band materials. Render inspected against native294 evidence.

One724x2172 Guild standard resizes to683x2048 via recorded PIL bilinear; Core
validates target extent, GPU filtering remains approximate. Native shader effects
and final production-art parity are not established by the offline render.
Tool Release build PASS. No runtime rule change or redundant Core/native lifecycle
rerun. No game launched/deployed in295. Normal293/camp292 art, all configs/assets/
estate2/native profiles/foreign WIP exact, protected estates untouched. Game CLOSED.
Evidence hamlet-material-review-v50; raw/candidate art remains unchanged.

## Status round295

Review tools accepted; normal293 and camp292 package installed, game CLOSED,
no pending restore. Stop with usage reserve and resume fresh usage/baseline next296.
Use the matched preview to judge remaining service-detail/lighting work; native
DD2 production-art parity and directional shadows remain open.


## Round296 - native Guild entrance weaponry (2026-10-22)

DD1 Guild frontage reference has a weapon stand beside its door. Replaces the
tiny camp axe with three complete native Tangle sword meshes and one battered
native military shield on eight beams using the Guild's existing oak paint.
Native source props retain original2048x1024/2048x2048 decoded paint, alpha,
UVs and uniform proportions. Empty dummy export and black silhouette-only arena
swords rejected; original FBX exports loaded with Forest/Common dependencies.
Native battlefield rack with skulls/cyan scraps rejected for civic colour harmony.
No new painting/generation or palette change. Guild28621 triangles;616 original
meshes and other UVs/material factors/decoded paint retained. Geometry max delta
2.39e-7m, native UV float error at most2.98e-8. Exact service bounds/normalization
and nine other raw GLBs/baked glTF+bin. All contact scalars/paint/geometry guards pass.

Four model views and matched Core-material whole-town render inspected. Full
unsaved ten-service/camera/release/reload/re-entry/Embark-cache/resize/keyboard/
still-camera/smoke fixture PASS. Fresh1920x1080 overview inspected;256 renderers,
three plumes, zero render-buffer reallocations over5.023sec. Runtime293 exact;
asset-only, no C# build/test rerun. Game CLOSED. All configs/estate2/native profiles/
other assets/plugins/foreign WIP retained, protected estates untouched. No estate
selected/saved. Evidence hamlet-guild-frontage-v51, native inputs remain private.

## Status round296

Accepted Guild frontage installed, raw models-reviewed/, baked HamletModels-reviewed/,
runtime293, game CLOSED, no pending restore. Fresh usage/baseline next297. Main
service colours remain coherent; directional cast shadows, broader scene dressing
and final native DD2 production-art parity remain open.


## Round297 - private directional-shadow study (2026-10-22)

Two controlled EEVEE off/on comparisons preserve Core graded paint/facets with
an occlusion multiplier. The current steep key has little visible ground effect;
a lower sunset direction aligns with the bright sky and improves grounding.
This is a Blender study, not native shader parity. No existing paint was repainted.

shadow_field.py derives a1024 ground field from actual Core-reviewed opaque
service/terrain meshes,192332 caster triangles and named service terraces.
Unity light(.60,.45,.55),3-degree disc/four samples, floor.6. Main bounds
X[-66,58],Z[-36,69]. PNG top=MaxZ, native bottom=MinZ, pixel centres; decoded
gray bytes and NPZ rows exact. Source Core/reader/package stamps reject stale
inputs before output creation. Procedural box/plane cases verify ray direction,
occlusion, clear side, no self-contact and vertical sun. All source models and
paints retained. Reprojected field affects two existing offline ground materials;
packed Cycles render inspected. Alpha scenery/trees/background cottages excluded.

Tool/study only. No native shadow loader, worker/cache change or game deployment;
runtime293/Guild296 art remain installed, game CLOSED. Native lifecycle acceptance
remains296; no redundant full Core/native suite for offline-only code. Foreign
gameplay WIP/journals retained; protected estates never accessed. Evidence v52.

## Status round297

Shadow study and guarded baker saved; game still uses the verified296 art. Next298
native shadow integration requires source/model staleness guards, mask-aware paint
cache keys, correct data/row sampling and native appearance/performance checks.
Do not install the private Blender study as production or alter service colours
to compensate. Wider architecture/material/native production-art parity remains.
