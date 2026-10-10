You are continuing Darkest2in1: Darkest Dungeon 1's full game loop played inside Darkest Dungeon II, with DD2's heroes and DD2's combat. It is a BepInEx 5 / HarmonyX mod in C:\Users\Piral\DarkestDungeon3\mod (GitHub Stephane-Dedu/Darkest2in1, branch main). The owner instructed merging the former working branch and continuing directly on main on 2026-10-10. Continue from the latest Status and next round number in MODLOG.md.

Current owner direction (2026-10-04): DD2 regions are the primary campaign areas; DD1 regions stay optional through
region toggles. Keep DD1 systems and native DD2 combat. Choose art/transitions for coherence and responsiveness;
preserve campaign progress. CLAUDE.md is authoritative for this direction and the latest launch permission.
Active install since round199: C:\Users\Piral\darkestwithdlc\game for DD2 and runtime DD1 data.
The machine-local build override and playtest helpers target it. Preserve the previous install and both
reverse-engineering workspaces; their references remain valid. The owner authorized launching again after round205.
Latest task, round205: Shieldbreaker's model reworked against her actual DD1 design. Read
tools/shieldbreaker/README.md before changing its private model pipeline or runtime binding.
The model is built and checked offline; native appearance and combat timing remain unverified.
The implementation was completed without launching. The owner then requested a launch of the updated install.
Estate 2 now includes Shieldbreaker Hachet for the owner's test; her revised pack and corridor rig bound
successfully in native logs. Keep the roster addition, and do not automatically restore its pre-test backup.
Owner focus (2026-10-08): reverse engineer and match the main DD1 quest flow first: Hamlet, selection,
room/hallway travel and curios, battles and proper transitions. Fix reported defects before return-XP tuning.
Use tools/quest_parity_map.md and the latest MODLOG Status; later progression/trinket ideas are in PARITY.md.
For the owner's memory-curio, Mountain, camping-item, stun or DD2 supply/curio proposals, read
tools/overhaul_roadmap.md. Item research starts at tools/dd2_item_mapping.md and its private catalogue;
retain native facts separately from proposed interactions. These tracks are not enabled gameplay.
Faded Memory and Haunting Memories now have owner-defined rules in tools/faded_memory_design.md.
Read it for their placement, sprite-only transformation, two victory rewards and future switch retirement.

## Start
1. In C:\Users\Piral\DarkestDungeon3\mod: inspect `git status`, switch to `main` with a clean tree, and `git pull --ff-only`. Preserve any local work or divergent remote commits before proceeding.
2. Read CLAUDE.md (owner's rules, machine paths, build/test/drive, skills), then the last "## Status" section at the end of MODLOG.md (where things stand, what to check first, anything pending), then PARITY.md (the checklist against real DD1).
3. Owner authorized launching DD1, DD2 and the mod for tests on 2026-10-09. Use estate 2 only; protected estates remain off limits. Deploy only while DD2 is stopped.

## How a round works (one gap per round)
Follow tools/parity_loop.md exactly; when the owner asks for the loop, run `/loop` with that file's text.
- Pick the highest-impact `[ ]` or `[?]` line: broken > missing > wrong numbers > looks different. Skip `[user]` and `[blocked]`.
- Find how DD1 does it (cite the file and field: DD1 install data, the Unity port as reference) and how the mod does it (DD2 decomp for internals). Write the difference into PARITY.md before coding.
- Smallest change that matches DD1. Rules in Core with a test; bridge/UI in the plugin; match the existing code. Split big items into sub-items and do the first.
- `dotnet build src/DarkestDungeon3 -c Release` (add `-p:Deploy=false` while the game runs) and `dotnet test tests/DarkestDungeon3.Core.Tests`: all green.
- Verify in game only if launching is allowed (tools/test_*.sh, debug keys, screenshot, LogOutput.log + Player.log). Otherwise mark `[?]` with exact steps to check.
- Update PARITY.md (status + one line of evidence) and MODLOG.md ("Round N: ..." findings, decomp facts, gotchas). Commit with a message saying what now matches DD1, ending with `Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>`, and push.
- New gaps found on the way go into PARITY.md, not into this round.

## Habits that matter (learned the hard way)
- Write multi-line patches with the Write tool or Python scripts that assert the old text exists; Git Bash heredocs eat backslashes.
- Before driving the game, check idle time (`tools/um win drive --proc explorer idle`); keep sessions short; kill processes only by exact PID.
- Saves: estate 2 is the only test slot. Never read or modify estates 1 or 3. Back up and hash-check estate 2 before native tests, restore it after the test game exits, and verify hashes. Avoid the normal picker, which reads protected slots; use an isolated test entry when needed.
- Never commit game files, extracted assets or decompiled code. Never read C:\Users\Piral\Documents\DD1inDD2.
- Time-box in-game checks: if two tries can't produce the evidence, mark `[?]` with what to look at and move on.
- Talk to the owner in short plain language: what changed, what was verified and how, what needs them.

## Handoff, so the next session (or the previous one) can resume
- At the end of every round and before stopping, refresh the last "## Status <date>" section at the end of MODLOG.md: last round done, what's built but not verified (with how to check), pending save restores or running processes, next candidate items, open `[user]` questions.
- Keep PARITY.md current. Commit and push everything; never leave uncommitted work, never rewrite history or force-push. Only one agent works on the branch at a time.
