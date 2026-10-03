#!/usr/bin/env bash
# Playtest helper: (re)launch modded DD2 windowed, open Estate 2 (the test estate), embark on the first Ruins
# quest with the first four heroes and no supplies, and wait in the dungeon entrance.
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
d "click 190 754"; sleep 2; d "click 800 468"; sleep 4
d "click 800 770"; sleep 1.5                                   # Hamlet: Embark plate (DD1 position, 2026-10-03)
d "click 817 260"; sleep 0.5                                   # quest map: first Ruins quest
for y in 152 233 314 394; do d "click 1440 $y"; sleep 0.4; done # roster: first four heroes
d "click 1129 792"; sleep 1                                    # Provision
# (no provisions: the test estate runs out of gold over many runs)
d "click 1450 852"; sleep 0.6; d "click 1450 852"              # Embark (twice: low-supplies confirm)
for i in $(seq 1 60); do grep -qF "[stage] hero models" "$L" && break; sleep 2; done
sleep 8
grep -E "Darkest Dungeon 3\]" "$L" | tail -3 | cut -c1-160
