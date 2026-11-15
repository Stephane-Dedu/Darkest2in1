# Playtest runbook

Build one dependable campaign loop first: town preparation, a saved Sprawl expedition with Crusader, ordinary combat, camping, rewards and reload. Then test failure branches, balance and the other regions. New content comes after this loop is accepted.

The user can say **"run the tests"**, **"what's my next step?"**, **"step 03 passed"**, or **"step 05 failed: ..."**. The agent handles the commands and keeps the record. The user does not need to operate the test runner.

## How to use this with an agent

1. Read this file and run `python tools/qa/run.py status`. Results and manual history live in `.qa/state.json`. Keep this local folder across sessions; it is ignored by Git because it contains machine paths and private evidence.
2. For "run/try the tests", run the full offline suite. For a specific defect, run its focused suite first, fix it, repeat that suite, then run the full suite before proposing another installed candidate. Use the configured Python with Pillow and NumPy, currently `C:\Users\Piral\DarkestDungeon3\universal-modder\.venv\Scripts\python.exe`.
3. For "what is my next step", read the ledger and give just the next checkpoint's actions and pass conditions. Prioritise a recorded failure over an untested later step. Prepare any offline prerequisites while live testing is unavailable.
4. When the user reports a result, record their observed outcome and evidence. Ask one targeted clarification if a verdict is ambiguous. Passing some checks within a step is partial progress, recorded as `pending` with the remaining checks. Automated success never supplies a manual verdict.
5. Fix one reproducible issue at a time. Preserve a failing test where the behaviour is testable. After a fix, repeat the affected step, the previous transition into it, and the next transition out. Finish with the complete loop.

**Current launch instruction, November 2, 2026:** keep DD2 closed until the user explicitly permits a launch. This runbook does not grant that permission. The offline runner never launches, drives, closes or deploys the game, and never opens real saves. For later native QA use estate 2 only. Estates 1 and 3 remain protected. User playtesting and agent-controlled QA are separate actions.

## 00. Establish the candidate and automated baseline

Agent actions:

1. Review the working tree and current handoff. Other agents have unpublished runtime changes. The runner freezes committed `HEAD` into its own directory and tests that source. It never silently includes working changes. A deliberately staged, reviewed Git tree can be supplied with `--ref`; report exactly which source was tested.
2. Run `python tools/qa/run.py run --suite full --dd1 "C:\Users\Piral\darkestwithdlc\game"`. Later runs reuse this data path. Core and UI tests use explicit DD1 data. Missing data, a build failure, zero tests, skipped tests, a timeout or a failed assertion cannot count as a pass.
3. Inspect `.qa/runs/<run-id>/result.json`, TRX files and logs. The retained source snapshot identifies the tested code. The full suite means Core, the UI tests using shims, runner regressions and the two scenery camera/composition modules. It is not all possible asset-authoring tests or a Unity engine test.
4. Capture the installed candidate with `python tools/qa/run.py baseline --game-dir "C:\Users\Piral\darkestwithdlc\game"`. This reads plugin/data/config and private scenery/curio hashes, without reading saves. It does not deploy tested code. Record any external asset changes separately.
5. Before native playtesting, reconcile tested source and installed binaries. If they differ, label the source tests and installed playtest separately. Deploy only a reviewed candidate, while DD2 is stopped, with a verified rollback. Never build/deploy a shared dirty source tree wholesale.

Completion: nonzero automated checks all pass, installed candidate is identified, and the user knows what build they will play. Offline checks cannot certify loading times, native animation, visual quality or balance. A changed installed fingerprint resets manual acceptance for the new candidate while retaining history. A changed executable source invalidates old automated results; documentation-only changes do not.

For a quicker regression pass use `--suite smoke`. For a specific step use its suite below. The default is `full`.

## Playtest setup

Use estate 2 and keep the same party, quest difficulty, Paths, equipment and display settings for comparisons. Start with Crusader, Highwayman, Plague Doctor and Vestal if available. Record substitutions; leave Shieldbreaker and experimental Dark Souls characters out of this baseline. Prefer a medium Apprentice Sprawl expedition so camping is available. If the board offers only short quests, complete steps 01-05 and 07, then test step 06 on the next medium quest. Keep step 06 pending until then.

Record the quest name, seed if available in logs, length, difficulty, party ranks, Paths, trinkets, resolution and graphics settings. Capture starting gold, supplies, HP and stress. Use normal purchases, movement and attacks. Debug wins, spawn shortcuts and auto-resolved fights are useful diagnostic fixtures but do not pass this manual cycle.

Before any approved interruption or failure test, the agent backs up estate 2 only. Do not inspect or copy protected estates. Ordinary quit/reload comes before forced termination tests.

<a id="01-town-party-paths-and-trinkets"></a>
## 01. Town, party, Paths and trinkets

Allow about 10 minutes. Automated suite: `town`.

1. Open estate 2. Visit roster, guild, treatment buildings and Nomad Wagon. Close each and reopen it.
2. Build the baseline party. Change a Path and inspect its affected skills. Equip and remove a trinket, then put it back.
3. In the Wagon, select several items and scroll a long description. Change page if available. Note gold, buy one affordable item once, and verify the item and exact charge. Reopen the Wagon.
4. Check tooltip edges and text at your normal resolution and one smaller supported window size. Return to your normal size for the baseline expedition.

Pass when selections are unambiguous, only Purchase spends gold, inventory and gold agree, long descriptions remain readable, and no panel covers a required control. Text must clearly identify conditions, duration, affected skills and restrictions. Native DD2 font/style, spacing and visual density require your judgment. Automated layout and equipment tests cover only part of this.

<a id="02-quest-selection-and-provisions"></a>
## 02. Quest selection and provisions

Allow about 5 minutes. Automated suite: `embark`.

1. Select different contracts in the same area, then another area, then the Sprawl contract. Confirm the displayed reward, difficulty, length and objective agree with the selected contract.
2. Watch the camera approach. Quests should stay readable and clickable throughout; look for the old two-stage reload or camera snap.
3. Buy supplies, cancel back to the map and verify the refund. Reopen provisions, buy the intended pack and embark once.
4. Check party order, equipment, quest objective and supply counts in the dungeon. Record embark-to-control time.

Pass when one selection leads to the correct quest, cancel refunds exactly, no purchase or embark repeats, required quest goods and firewood are accessible, and the first room becomes controllable without a persistent loading screen.

<a id="03-walking-scenery-and-navigation"></a>
## 03. Walking, scenery and navigation

Allow about 5 minutes spread across the expedition. Automated suite: `travel`.

1. Walk continuously for 10 seconds in a clear corridor. Stop, start again, reverse direction and turn forward. Try a short and a long corridor when available.
2. Watch Crusader's sword grip and the feet of every party member. Compare the first seconds with the established walking pace.
3. Enter a room, return through the door and revisit a cleared corridor. Inspect the start, middle and end of the background as you travel.
4. Observe one room and one corridor before and after combat.

Pass when step distance feels consistent, startup does not race, feet meet the ground, the sword stays in the hand, and scenery moves continuously without mirrored doubles, jumps, seams or repeated floor strips. Doors and map selection must agree with party position. Ground detail should support the scene while leaving heroes, enemies and interactable objects easy to distinguish. Automated camera maths does not prove this looks good in motion.

<a id="04-curios-supplies-and-full-inventory"></a>
## 04. Curios, supplies and full inventory

Allow about 5-10 minutes as encounters appear. Automated suite: `curios`.

1. Investigate one curio normally and one with an appropriate supply when available. Verify the selected hero, supply consumption, outcome and used appearance.
2. Walk away and return. A resolved curio must remain resolved and must not pay again.
3. With a full pack, open loot. Discard or move an item, take a partial stack and deliberately leave an ordinary item behind. Verify counts and the ability to continue. Do not discard mandatory quest items.
4. Quit and resume once while a curio or loot result is pending, after the estate 2 backup. Finish the interaction and revisit it.

Pass when art fits the region, there is no blue placeholder or accidental clickable-looking background object, the interaction is clear, rewards and supplies change once, and reload preserves the unresolved choice without rerolling or duplicating loot. A curio or pack state that did not occur remains pending; use an additional recorded quest to cover it.

<a id="05-ordinary-battles-and-transitions"></a>
## 05. Ordinary battles and transitions

Allow about 10-20 minutes within the expedition. Automated suite: `combat`.

1. Fight the first ordinary encounter with normal skills. Record time from "Enemies ahead" to the first usable combat action. Repeat the timing for at least two later encounters.
2. Across the encounters use a direct hit, a damage-over-time skill, a buff or heal and a combat supply when available. Inspect targets, tokens, descriptions, corpses, lethal hits and rewards.
3. Observe one hall fight and one room fight. Check party positions and backdrop before, during and after combat. Walk again after collecting loot.

Pass when input is clear, legal skills target the displayed ranks, the recorded Path/trinkets have their described effects, ordinary kills end combat once, and the party returns to the same location and condition. There must be no surprise reload, visible placeholder, displaced ground or long unexplained freeze. Missing skill/encounter coverage stays pending. Core combat-result tests do not execute DD2's combat engine.

Use these initial performance goals as a tuning baseline, not claims about current performance: warm battle start at or below 2 seconds; warm return to controllable town at or below 5 seconds; no walking hitch above 100 ms. Record cold starts separately. Capture at least five warm samples before comparing median and worst time. A stopwatch can measure loading, but frame hitches need a recording or profiler. Do not invent measurements or label one fast sample a pass. Adjust targets explicitly if the test machine requires it.

<a id="06-camping-and-continued-exploration"></a>
## 06. Camping and continued exploration

Allow about 5 minutes. Automated suite: `camp`.

1. Camp in an eligible cleared room. Record food and firewood, choose a meal and use camp skills.
2. Quit and resume during camp after the backup. Check remaining respite, food, buffs and party condition. Attempting a previously paid action must not charge again.
3. Break camp. Resolve an ambush normally if one occurs, then continue exploring and fight another encounter if available.

Pass when camp consumes the correct goods once, reload preserves the phase, skill effects survive as described, and breaking camp restores the proper room, controls and lighting. Record an unobserved ambush as separate missing coverage rather than forcing one into the accepted baseline run.

<a id="07-return-progression-and-saved-reload"></a>
## 07. Return, progression and saved reload

Allow about 5-10 minutes. Automated suite: `return`.

1. Complete the objective through normal play. Confirm it offers return rather than taking control away while loot or a curio is pending.
2. Before returning, capture pack contents, reward, party condition and quest completion. Read each recap page. Record the final-confirmation-to-controllable-town time; note time spent on the recap separately.
3. Check gold, trinkets, surviving heroes, stress, resolve XP, region progress, week and available contracts against the recap. Record any unexplained difference.
4. Quit normally and reopen estate 2 twice. Recheck those values, then begin preparing another expedition.

Pass when the recap agrees with town, each payout and progression change occurs once, repeated reload does not award again, and the next quest is usable. This completes the first ordinary saved loop. A synthetic test or debug win cannot substitute for it.

<a id="08-retreat-defeat-and-recovery"></a>
## 08. Retreat, defeat and recovery

Use separate estate 2 test outings with backups. Automated suite: `recovery`.

1. Retreat from combat, then abandon an incomplete expedition. Verify survivors, retained loot, stress and absence of a success reward. Reload the result.
2. In a deliberately expendable test party, observe a hero death and a full defeat. Verify roster removal, memorial, lost goods and the return to a usable town. Record trinket recovery expectations separately from any unimplemented crow content.
3. Quit normally in a room, corridor and pending loot phase. Resume each. After those pass and the user authorizes it, test one interrupted combat; expect the documented pre-fight checkpoint, not arbitrary mid-turn restoration.

Pass when every branch returns to a coherent saved state without missing survivors, duplicate loot, a stuck encounter or a broken campaign. Each branch needs its own evidence. Do not claim the whole step passes after testing only retreat.

<a id="09-expedition-balance-and-economy"></a>
## 09. Expedition balance and economy

Automated suite: `balance`. This is a measured playtest series, not a unit-test verdict.

Run at least three ordinary Apprentice Sprawl expeditions with recorded seeds/contracts and comparable party strength. Then sample Veteran and Champion with appropriately developed heroes. Record per encounter: pack, rounds, elapsed time, HP/stress change, DoT damage, deaths and consumables. Per quest record battle count, exploration time, supply cost, loot, reward and recovery expense. Include at least one retreat.

Compare the total expedition burden with the intended DD1 campaign rhythm and the local DD1 reference data. DD2 enemies must fit the larger number of battles; HP alone is insufficient. Examine damage, DoT, healing opportunities, stress, recovery cost, rank coverage and Path choices together. Agree on desired quest duration and acceptable attrition from these observations before changing numeric targets.

Pass when repeated expeditions present meaningful choices, ordinary packs do not exhaust the whole party before the intended route is playable, victories fund another reasonable outing, and no single Path/trinket is mandatory to function. Record the evidence and the user's fun/difficulty judgment. Boss balance is a separate series; boss-looking room art does not prove a boss encounter is implemented.

<a id="10-other-regions-and-repeated-session-acceptance"></a>
## 10. Other regions and repeated-session acceptance

Automated suite: `full`.

Repeat steps 02-07 in Foetor, Tangle and Shroud. Check region-specific curios, enemies, rooms and environmental effects. Include short, medium and long routes across the series, and the difficulty samples from step 09. Keep a region/length/difficulty table in the result notes so missing combinations are visible.

Then play three expeditions in one session, including a retreat, and reload afterwards. Record loading times, memory at comparable town points after each outing, new log errors and any increasingly slow input. Compare with the first outing; don't call one memory reading a leak.

Pass when all four regions retain the accepted loop, the repeated session remains usable, no critical defect remains open, and the user accepts movement, readability and pacing. Repeat the full offline suite on the final reviewed source. Record the final installed fingerprint separately. Optional zones, new heroes and bosses can then use the same cycle.

## Record results and iterate

The agent runs these commands on the user's behalf:

```powershell
python tools/qa/run.py status
python tools/qa/run.py next
python tools/qa/run.py run --suite travel
python tools/qa/run.py record 03 pass --note "User tested continuous walking, both directions and room re-entry; grip and scenery accepted." --evidence "path-to-recording"
python tools/qa/run.py record 05 fail --note "First hall fight: expected control after transition; actual 7-second freeze. Warm starts not yet measured." --evidence "path-to-log"
```

Manual outcomes are `pending`, `pass`, `fail` or `blocked`. Record only what was observed, including partial coverage. Attach measurements with `--metrics <json-file>` if useful. A record is tied to the installed fingerprint, not the current source commit. The runner verifies the fingerprint again before saving a verdict. If the game or another agent changes it, capture a fresh baseline. Keep source-only failures distinct from failures in the installed game.

For a bug note include step, installed build, quest/region/difficulty, party/Paths, exact action, expected result, actual result, frequency and log/recording time. Label severity: **P0** save loss or unusable campaign, **P1** blocked progression or broken core input, **P2** visual, readability, pacing or performance defect, **P3** optional polish. Fix P0/P1 first. An obvious visual defect in the baseline route should be fixed before expanding content.

After a fix, record the failed case's successful retest with its evidence; keep the earlier failure in history. Use `pending` to reopen a previously accepted step. A changed installed build deliberately requires renewed manual acceptance. For unchanged builds, the latest verdict for each step is authoritative. Do not transfer passes automatically between builds.

If the runner is interrupted, its run folder retains available logs and a nonpassing status. A hard process kill can leave `.qa/running.lock`; inspect its PID and confirm no run is active before removing that lock. Never clear the ledger to hide a failure.

## What is automated now

The manifest in `tools/qa/steps.json` maps each step to real test classes. Existing suites cover campaign rules, map generation, saved interaction states, quest exits, equipment, UI layout/input shims and recovery. New regional lifecycle tests exercise provision creation, saved walking/battle phase, camping, return and repeated disk reload using unique temporary synthetic saves. Combat results are simulated there.

The runner freezes source, records exact revision and dirty-tree context, runs each selected suite without deployment, checks result counts, retains logs, and refuses to count skipped or empty runs as success. Its own regressions verify stale-source rejection, failure precedence, changed-build invalidation and separation of manual acceptance from automatic checks.

Still manual: native Unity rendering/input, actual skill fights, weapon grips, foot contact, map animation, native font appearance, placeholder flashes, loading/frame-time measurements, full-session memory behaviour and fun. Add engine tests or telemetry as failures justify them; keep those limits explicit.
