You are iterating on Darkest2in1: Darkest Dungeon 1's full game loop played inside Darkest Dungeon II, with DD2's heroes and DD2's combat. Each round, close ONE gap between this mod and real DD1. Work directly on main, as the owner instructed on 2026-10-10; commit and push each verified round there.

Current owner direction (2026-10-04): DD2 regions are the primary campaign areas; DD1 regions stay optional through
region toggles. Keep DD1 systems and native DD2 combat. Choose art/transitions for coherence and responsiveness;
preserve campaign progress. CLAUDE.md is authoritative for this direction and the latest launch permission.
Owner focus (2026-10-08): match Hamlet, quest selection, room/hallway travel and curios, battles and proper
transitions first, using tools/quest_parity_map.md. Reported input/rendering defects outrank return-XP tuning.

## Ground rules (never break these)
- Read MODLOG.md and PARITY.md before doing anything. Anything not written in them is lost at the next compaction.
- DD1 is the reference, not memory. Check behavior against the DD1 install's data (campaign/, dungeons/, monsters/, shared/, upgrades/, localization) and the Unity port at C:\Users\Piral\csharpdd\Darkest-Dungeon-Unity. For DD2 internals, use the local decomp. Never read C:\Users\Piral\Documents\DD1inDD2.
- Combat stays DD2's by design. "Parity" means DD1's systems around combat (surprise, torch effects, corpses, retreat, loot, stress, afflictions, deaths), not DD1's combat engine.
- Rules go in Core (no Unity) with a test. Bridge and UI code goes in the plugin. Match the code that's already there.

## Round 0 (only if PARITY.md doesn't exist)
Walk DD1's data and build PARITY.md: one line per DD1 system or behavior, grouped (Hamlet & buildings, roster/quirks/diseases, trinkets, quest board, provisioning, map gen, crawl: light/hunger/scouting/traps/obstacles/curios, camping, fights: surprise/retreat/corpses/torch, afflictions/virtues/deaths, loot & homecoming, town events, plot quests & the Darkest Dungeon, audio, UI look). Mark each line [x] done-and-verified, [ ] missing/wrong (with what differs from DD1), [?] unverified, [user] needs a decision from me, or [blocked] with the reason. Pull in the open items from MODLOG's "Needs in-game check", "End-to-end audit" and "Next" sections. Commit it, then stop this round.

## Every later round
1. Pick the highest-impact [ ] or [?] item: broken > missing > wrong numbers > looks different. Skip [user] and [blocked].
2. Find exactly how DD1 does it (cite the file and field) and how the mod does it now. Write the difference down in PARITY.md before writing code.
3. Fix it with the smallest change that matches DD1. If it's too big for one round, split it into sub-items and do the first one.
4. Verify it:
   - dotnet build src/DarkestDungeon3 -c Release (-p:Deploy=false if the game is running), and dotnet test tests/DarkestDungeon3.Core.Tests. All green.
   - If the change shows up in game and launching is allowed right now, use tools/test_hamlet.sh / test_enter_dungeon.sh / walk_until_curio.sh / fight_once.sh plus the debug keys. Take a screenshot, compare it with DD1 and read BepInEx/LogOutput.log for exceptions. If it can't be checked in game, mark it [?] with what to look at, not [x].
5. Update PARITY.md (status + one line of evidence) and MODLOG.md (findings, decomp facts, gotchas). Commit with a message saying what now matches DD1, then push.
6. If you find a new gap while working, add it to PARITY.md. Don't chase it this round.

## Stopping
- If a fix fails verification twice, revert it, mark it [blocked] with what you learned and move on. Never commit red tests or a build that doesn't compile.
- End the loop when every line is [x], [user] or [blocked]. Then give me a summary: what was done, what's blocked and why, and the [user] questions in one list.
