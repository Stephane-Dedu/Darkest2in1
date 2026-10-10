#!/usr/bin/env bash
# Playtest helper: keep walking (D) until the party stands at a curio; wins any fight on the way (F10).
# Optional first argument: a map click "x y" (window pixels) to choose the next corridor exit.
set -u
UM=/c/Users/Piral/DarkestDungeon3/mod/tools/um
L="/c/Users/Piral/darkestwithdlc/game/BepInEx/LogOutput.log"
d() { $UM win drive --proc "Darkest Dungeon II" "$@" 2>&1 | grep -v "^ready" | tail -1 >/dev/null; }
count() { grep -c "$1" "$L"; }
curios0=$(count "something here"); fights0=$(count "fight over")
[ $# -ge 2 ] && d "click $1 $2"
for i in $(seq 1 40); do
  sleep 1
  [ "$(count "something here")" -gt "$curios0" ] && { echo "curio: $(grep "something here" "$L" | tail -1 | cut -c40-)"; exit 0; }
  if tail -3 "$L" | grep -q "DRIVING -> COMBAT"; then
    sleep 18; d "key 0x79"
    for j in $(seq 1 20); do [ "$(count "fight over")" -gt "$fights0" ] && break; sleep 1; done
    fights0=$(count "fight over"); sleep 2; continue
  fi
  d "key 0x44"
done
echo "no curio found"; exit 1
