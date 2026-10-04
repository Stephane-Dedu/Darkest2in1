You are continuing Darkest2in1: Darkest Dungeon 1's full game loop played inside Darkest Dungeon II, with DD2's heroes and DD2's combat. It is a BepInEx 5 / HarmonyX mod in C:\Users\Piral\DarkestDungeon3\mod (GitHub Stephane-Dedu/Darkest2in1, branch claude/practical-wright-hicri0). Earlier Claude sessions did the work so far, round by round; continue in exactly the same way, from the next round number in MODLOG.md.

## Start
1. In C:\Users\Piral\DarkestDungeon3\mod: `git checkout claude/practical-wright-hicri0` and `git pull --rebase`.
2. Read CLAUDE.md (owner's rules, machine paths, build/test/drive, skills), then the last "## Status" section at the end of MODLOG.md (where things stand, what to check first, anything pending), then PARITY.md (the checklist against real DD1).
3. Game launches: obey the latest owner instruction in CLAUDE.md / the MODLOG status. As of 2026-10-04 the owner said not to launch DD2 until told. If unsure, ask the owner before launching.

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
- Saves: never touch estate 1 (the owner's campaign); estate 2 is the test estate; test a new estate in slot 3 by moving estate_3.json and its .bak aside, then delete the test files, move the originals back and `cmp` them.
- Never commit game files, extracted assets or decompiled code. Never read C:\Users\Piral\Documents\DD1inDD2.
- Time-box in-game checks: if two tries can't produce the evidence, mark `[?]` with what to look at and move on.
- Talk to the owner in short plain language: what changed, what was verified and how, what needs them.

## Handoff, so the next session (or the previous one) can resume
- At the end of every round and before stopping, refresh the last "## Status <date>" section at the end of MODLOG.md: last round done, what's built but not verified (with how to check), pending save restores or running processes, next candidate items, open `[user]` questions.
- Keep PARITY.md current. Commit and push everything; never leave uncommitted work, never rewrite history or force-push. Only one agent works on the branch at a time.
