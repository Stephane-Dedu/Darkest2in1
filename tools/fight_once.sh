#!/usr/bin/env bash
# Playtest helper (in the dungeon): head for the nearest battle room (F3), dealing with traps, obstacles and curios
# on the way (Enter dismisses result scrolls, the "by hand" button disarms/clears/investigates), then win the fight
# (F10) and wait until the party is back in the dungeon.
set -u
UM=/c/Users/Piral/DarkestDungeon3/mod/tools/um
L="/c/Users/Piral/darkestwithdlc/game/BepInEx/LogOutput.log"
d() { $UM win drive --proc "Darkest Dungeon II" "$@" 2>&1 | grep -v "^ready" | tail -1 >/dev/null; }
f0=$(grep -c "DRIVING -> COMBAT" "$L")
for i in $(seq 1 60); do
  [ "$(grep -c "DRIVING -> COMBAT" "$L")" -gt "$f0" ] && break
  if [ $((i % 4)) -eq 1 ]; then d "key 0x0D"; d "click 1046 396"; sleep 0.5; d "key 0x0D"; d "key 0x72"; fi
  sleep 1
done
[ "$(grep -c "DRIVING -> COMBAT" "$L")" -gt "$f0" ] || { echo "no fight reached"; exit 1; }
grep "\[combat\]" "$L" | tail -1 | cut -c40-
w0=$(grep -c "fight over" "$L")
sleep 16; d "key 0x79"
for i in $(seq 1 45); do [ "$(grep -c "fight over" "$L")" -gt "$w0" ] && break; sleep 1; done
sleep 2; grep -E "fight over|\[loot\]" "$L" | tail -2 | cut -c40-
