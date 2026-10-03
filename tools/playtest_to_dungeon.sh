#!/usr/bin/env bash
# Playtest helper: from DD2's main menu (game already launched windowed 1600x900), open Estate 2 (the test
# estate), take the first Ruins quest with the first four heroes, buy food/torches/shovels/keys and embark.
UM=/c/Users/Piral/DarkestDungeon3/mod/tools/um
L=/c/Users/Piral/DarkestDungeon3/game/BepInEx/LogOutput.log
P="/c/Users/Piral/AppData/LocalLow/RedHook/Darkest Dungeon II/Player.log"
w() { powershell -NoProfile -Command "Start-Sleep -Milliseconds $1"; }
d() { $UM win drive --proc "Darkest Dungeon II" "focus" "$@" 2>&1 | tail -1 >/dev/null; }
for i in $(seq 60); do grep -q "Finished loading scene main_menu_kingdom" "$P" 2>/dev/null && break; w 3000; done; w 3000
d "click 190 754"; w 2500
d "click 800 440"; w 4000
d "click 1088 846"; w 2500
d "click 816 256"; w 1000
d "click 1420 150" "click 1420 230" "click 1420 310" "click 1420 390"; w 800
d "click 1130 790"; w 1500
cmds=(); for i in $(seq 12); do cmds+=("click 806 206"); done; for i in $(seq 8); do cmds+=("click 872 206"); done
cmds+=("click 938 206" "click 938 206" "click 1202 206" "click 1202 206"); d "${cmds[@]}"; w 800
n=$(wc -l < $L); d "click 1446 850"
for i in $(seq 40); do tail -n +$n $L | grep -q "Entered The" && break; w 2000; done
tail -n +$n $L | grep "Entered The" | tail -1 | cut -c1-120
