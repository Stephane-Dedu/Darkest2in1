#!/usr/bin/env bash
# Playtest helper: (re)launch modded DD2 windowed, open Estate 2 (the test estate), embark on the first quest
# with the first four heroes, 8 food and 4 torches, and wait in the dungeon entrance.
# Coordinates are client pixels of a 1600x900 window (virtual 1920x1080 * 0.8333).
set -u
UM=/c/Users/Piral/DarkestDungeon3/mod/tools/um
GAME='C:\Users\Piral\DarkestDungeon3\game\Darkest Dungeon II.exe'
L="/c/Users/Piral/DarkestDungeon3/game/BepInEx/LogOutput.log"
P="/c/Users/Piral/AppData/LocalLow/RedHook/Darkest Dungeon II/Player.log"
d() { $UM win drive --proc "Darkest Dungeon II" "$@" 2>&1 | grep -v "^ready" | tail -1 >/dev/null; }

old=$(powershell -NoProfile -Command "(Get-Process | Where-Object { \$_.ProcessName -eq 'Darkest Dungeon II' }).Id")
for pid in $old; do powershell -NoProfile -Command "Stop-Process -Id $pid -Force -Confirm:\$false"; done
sleep 3
(cd /c/Users/Piral/DarkestDungeon3/mod && dotnet build src/DarkestDungeon3 -c Release 2>&1 | grep -E "Deployed|error CS" | head -3)
powershell -NoProfile -Command "\$p = Start-Process -FilePath '$GAME' -ArgumentList '-screen-fullscreen 0 -screen-width 1600 -screen-height 900' -WorkingDirectory 'C:\Users\Piral\DarkestDungeon3\game' -PassThru; 'pid=' + \$p.Id"
sleep 5
for i in $(seq 1 90); do grep -q "Finished loading scene main_menu_kingdom" "$P" 2>/dev/null && break; sleep 2; done; sleep 4
d "click 190 754"; sleep 2; d "click 800 468"; sleep 3; d "click 1417 837"; sleep 1; d "click 275 139"; sleep 0.5
for i in 1 2 3 4; do d "click 800 386"; sleep 0.4; done
# (no provisions: the test estate runs out of gold over many runs)

sleep 0.5; d "click 1450 857"; sleep 1; d "click 1450 857"
for i in $(seq 1 60); do grep -q "Entered The Ruins" "$L" && break; sleep 2; done
sleep 8
grep -E "Darkest Dungeon 3\]" "$L" | tail -3 | cut -c1-160
