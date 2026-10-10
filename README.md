# Darkest2in1 (DarkestDungeon3)

Darkest Dungeon 1's game loop played inside Darkest Dungeon II: DD1's Hamlet, quests, provisioning, room-and-corridor
dungeons, curios, camping, loot and town events, with DD2's heroes and DD2's combat. A BepInEx 5 plugin for DD2.

Personal project. **No game files are included**: everything from DD1 (data, art, sounds) is read at runtime from your
own DD1 install, and DD2 is patched in memory. Keep it that way: never commit game files, extracted assets or
decompiled code (see `.gitignore`).

## Requirements
- Darkest Dungeon II v2.04 (Unity 2022.3, Mono) with [BepInEx 5](https://github.com/BepInEx/BepInEx) installed in its folder.
- Darkest Dungeon 1 installed (Steam is found automatically; otherwise set `Paths.DarkestDungeon1Folder` in
  `BepInEx/config` after the first launch).
- .NET 8 SDK.

## Build and install
Tell the build where DD2 is, once per machine, in `Directory.Build.local.props` next to `DarkestDungeon3.sln`:

```xml
<Project>
  <PropertyGroup>
    <GameDir>D:\Games\Darkest Dungeon II</GameDir>
    <!-- optional: a copy of DD2's Managed + BepInEx/core DLLs to compile against -->
    <!-- <RefsDir>D:\dd2-refs</RefsDir> -->
  </PropertyGroup>
</Project>
```

Then `dotnet build src/DarkestDungeon3 -c Release` builds and copies the plugin and `data/` into
`<GameDir>/BepInEx/plugins/DarkestDungeon3` (pass `-p:Deploy=false` while the game is running: it locks the DLL).

Tests (they read your DD1 install): `dotnet test tests/DarkestDungeon3.Core.Tests`.

## Playing
In DD2's main menu, **The Hamlet** opens the DD1 campaign (estates are saved next to DD2's saves). Options are in
`BepInEx/config/` (look: DD1 monsters in fights, DD1 backdrop, DD2 hero models; sound: DD1 music and sounds).

## Layout
- `src/DarkestDungeon3.Core` - game rules, no Unity: DD1 data readers (`.darkest`, Spine skeletons and animations,
  FSB5 sound banks), campaign, Hamlet, quests, map generation, the crawl.
- `src/DarkestDungeon3` - the BepInEx plugin: DD2 bridge (combat, heroes, Harmony patches), DD1-styled screens.
- `data/` - our own mappings: DD1 zones to DD2 fights, DD1 monsters to DD2 stand-ins.
- `tests/` - Core tests against the DD1 install.
- `MODLOG.md` - the working journal: findings, decisions, what still needs checking in game.
- `tools/` - playtest scripts and a type-by-type decompiler for reading DD2's code locally.

For binary investigations, follow [the REA usage guide](tools/rea_usage.md).
It covers the installed skill, CLI commands, private evidence and current Windows
native-analysis prerequisites. [The investigation backlog](tools/rea_investigation_backlog.md)
lists the systems awaiting owner priorities.
