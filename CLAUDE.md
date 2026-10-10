# Darkest2in1: notes for Claude sessions

Darkest Dungeon 1's game loop (Hamlet, quests, provisioning, room/corridor dungeons, curios, camping, loot, town events,
plot quests, the Darkest Dungeon) played inside Darkest Dungeon II with DD2's heroes and DD2's combat. BepInEx 5 +
HarmonyX plugin for DD2 v2.04 (Unity 2022.3, Mono). Repo: https://github.com/Stephane-Dedu/Darkest2in1.

## Current direction (owner, 2026-10-04)
DD2's Sprawl, Foetor, Tangle and Shroud are the default campaign regions. Keep DD1 destinations available through
region toggles. Retain DD1 expedition systems and DD2 combat; choose exploration/fight backgrounds for coherent,
responsive UI and transitions. Preserve existing campaign progress and explicit region choices. This supersedes
DD1-first region defaults; save-protection rules still apply.
Owner refinement, 2026-10-09: Faded Memory and the Haunting Memories event will provide special DD1
encounters/destinations. See `tools/faded_memory_design.md` before implementing either. Retire the region
swap button later, once those features and progress migration are ready; retain the current switch for now.
Owner priority, 2026-10-08: map the complete base-game DD1 quest lifecycle through reverse engineering, then implement
its quest systems in DD2. This supersedes waiting for R1-R10 choices. Use `tools/quest_parity_map.md` for native
contracts, implementation seams and acceptance scenarios. Finish the interrupted private native map with conservative
resources while no game runs; use game launches for testing as authorized on 2026-10-09.
Owner focus, 2026-10-08: first match Hamlet, quest selection, room/hallway travel, curios, battles and proper
transitions. Reported rendering/input defects outrank numeric return-phase tuning. Later ideas recorded in
PARITY.md: DD1 resolve levels and tiered skill damage/utility upgrades, both games' trinkets, and a DD1-style
trinket panel with a central main button. Audit existing progression before changing it; preserve campaigns.
Owner's map design: pair Sprawl/Ruins, Foetor/Warrens, Tangle/Weald and Shroud/Cove at the same map positions.
Keep DD1's area names with their quests directly beneath each area. A single existing next-arrow beside the name on
its right switches enabled areas at that position, such as Foetor/Warrens. Every area's XP, quests and bosses stay
independent. Do not move contracts into a separate strip below the map.

## Read first, every session
1. `MODLOG.md`: the journal (decomp facts, DD1 data formats, gotchas, click coordinates, debug keys). Append to it as you learn.
2. `PARITY.md`: the checklist against real DD1 (`[x]` verified in game, `[ ]` missing/wrong, `[?]` built but not seen in
   game, `[user]` needs the owner's decision, `[blocked]`). Update it with every change.
3. Anything not written in these two files is lost at the next context compaction.

## Working branch and the parity loop
- Work directly on `main` (owner instruction, 2026-10-10). The former working branch is merged; commit and push each verified round to `main`.
- `tools/handoff_prompt.md` is the prompt to start any session or agent on this project; leave the same handoff when you stop.
- The improvement loop prompt is in `tools/parity_loop.md`. Start it with `/loop` followed by that text, and only when the
  owner asks. The owner resumed it on 2026-10-04; game launches for testing are authorized as of 2026-10-09.

## Owner's standing rules
- **Owner authorized launching DD1, DD2 and the mod for tests on 2026-10-09.** Use estate 2 only; never touch protected estates. Deploy only while DD2 is stopped. Mark unobserved game-facing changes `[?]`.
- Decide and act on the project without asking (this machine and this repo only). Commit and push to the working branch
  after each change.
- DD1 data and the Unity port define expedition systems; DD2 data defines the default regions and native combat.
- Never read or reuse `C:\Users\Piral\Documents\DD1inDD2` (an older, abandoned attempt).
- The Unity port (`C:\Users\Piral\csharpdd\Darkest-Dungeon-Unity`, GPL, personal use) may be used completely, except its
  bundled Spine runtime.
- Never commit game files, extracted assets or decompiled code.
- Kill processes only by exact PID. Back up saves before risky changes.
- Saves (`%USERPROFILE%\AppData\LocalLow\RedHook\Darkest Dungeon II\DarkestDungeon3\`): **estate 1 is the owner's
  campaign, never touch it**; estate 2 is the test estate; estate 3 belongs to the owner too. Never read or modify either protected slot; use estate 2 for tests.
- Rules go in Core (no Unity) with a test; bridge and UI code in the plugin; match the existing code.
- Commits: the repo's local git config holds the author. End messages with
  `Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>`.

## This machine
| What | Where |
|---|---|
| DD2 (the modded game) | `C:\Users\Piral\DarkestDungeon3\game` (the E:/D: copies are old or broken) |
| DD1 install (read at runtime) | `C:\Program Files (x86)\Steam\steamapps\common\DarkestDungeon` |
| DD2 decomp / data / compile refs | `C:\Users\Piral\dd2-decomp` (`IronCrown/`, `data/Excel/`, `refs/`) |
| DD1 code map (Ghidra, private) | `D:\dd1-decomp` (`decomp/`, `map/`, `ghidra/DD1.gpr`); query with `tools/dd1re/dd1q.py` |
| Unity port of DD1 | `C:\Users\Piral\csharpdd\Darkest-Dungeon-Unity` |
| Logs | `game\BepInEx\LogOutput.log`, `%USERPROFILE%\AppData\LocalLow\RedHook\Darkest Dungeon II\Player.log` |
| Cinematics cache (webm/ogg) | saves folder `\cache` (made with the owner's ffmpeg, WinGet) |

## Build, test, drive
- `dotnet build src/DarkestDungeon3 -c Release` builds and deploys the plugin and `data/`; `dotnet test tests/DarkestDungeon3.Core.Tests`.
- Game driving: `tools/um win ...` (screenshots, clicks, keys, idle time, kill by PID) and the scripts in `tools/`:
  `test_hamlet.sh`, `test_enter_dungeon.sh`, `fight_once.sh`, `walk_until_curio.sh`. Window 1600x900 = virtual 1920x1080 × 0.8333.
- Debug keys (see MODLOG): F3 walk to a battle, F4/Shift+F4 test estate setup / open the Darkest Dungeon, F5 camp,
  F10 win the fight, Shift+F10 front enemy to 1 HP, Ctrl+F10 kill the front enemy as a skill kill, F11/Shift+F11 fight
  here (full pack).
- Git Bash eats `\\n` in heredocs: write patch scripts with the Write tool.

## Skills (universal-modder plugin)
For a native DD1 rule (formula, ordering, whether the exe reads a data key), start with the DD1 code map:
[`tools/dd1re/README.md`](tools/dd1re/README.md). It is decompiled code: never commit or copy it.
For DD2 CIL or build-comparison investigations, read
[`tools/rea_usage.md`](tools/rea_usage.md), then the installed REA skill it names.
The guide covers the working CLI, evidence reuse and current native prerequisites;
[`tools/rea_investigation_backlog.md`](tools/rea_investigation_backlog.md) holds the owner's priority choices.

The Claude Code plugin `universal-modder` is installed on this machine. Its skills fit this project:
`universal-modder:game-automation` (driving and screenshotting DD2), `universal-modder:mod-any-game`,
`universal-modder:mashup-mods`, `universal-modder:reverse-engineering`, `universal-modder:game-recon`. Load the relevant
one with the Skill tool before that kind of work. `tools/um` wraps its CLI.
