#!/usr/bin/env bash
# Playtest helper: (re)build, relaunch modded DD2 windowed (1600x900) and open Estate 2 (the test estate)'s Hamlet.
set -u
UM=/c/Users/Piral/DarkestDungeon3/mod/tools/um
GAME='C:\Users\Piral\darkestwithdlc\game\Darkest Dungeon II.exe'
L="/c/Users/Piral/darkestwithdlc/game/BepInEx/LogOutput.log"
P="/c/Users/Piral/AppData/LocalLow/RedHook/Darkest Dungeon II/Player.log"
d() { $UM win drive --proc "Darkest Dungeon II" "$@" 2>&1 | grep -v "^ready" | tail -1 >/dev/null; }

old=$(powershell -NoProfile -Command "(Get-Process | Where-Object { \$_.ProcessName -eq 'Darkest Dungeon II' }).Id")
for pid in $old; do powershell -NoProfile -Command "Stop-Process -Id $pid -Force -Confirm:\$false"; done
sleep 3
(cd /c/Users/Piral/DarkestDungeon3/mod && dotnet build src/DarkestDungeon3 -c Release 2>&1 | grep -E "Deployed|error CS" | head -3)
powershell -NoProfile -Command "\$p = Start-Process -FilePath '$GAME' -ArgumentList '-screen-fullscreen 0 -screen-width 1600 -screen-height 900' -WorkingDirectory 'C:\Users\Piral\darkestwithdlc\game' -PassThru; 'pid=' + \$p.Id"
sleep 5
for i in $(seq 1 90); do grep -q "Finished loading scene main_menu_kingdom" "$P" 2>/dev/null && break; sleep 2; done; sleep 4
d "click 190 754"; sleep 2; d "click 800 468"; sleep 6
grep -E "Darkest Dungeon 3\]" "$L" | tail -3 | cut -c1-160
