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
