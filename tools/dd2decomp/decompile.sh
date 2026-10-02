#!/usr/bin/env bash
# Decompile a game assembly type by type (survives the decompiler's stack overflows).
# usage: tools/dd2decomp/decompile.sh <assembly.dll> <out dir>
set -u
HERE="$(cd "$(dirname "$0")" && pwd)"
ASM="$1"; OUT="$2"; REFS="$(dirname "$ASM")"
dotnet build "$HERE/dd2decomp.csproj" -c Release -o "$HERE/bin/out" > /dev/null || { echo "build failed"; exit 1; }
rm -rf "$OUT"; mkdir -p "$OUT"
start=0
while true; do
  "$HERE/bin/out/dd2decomp.exe" "$ASM" "$REFS" "$OUT" $start > "$OUT/_last_run.log" 2>&1
  [ -f "$OUT/_progress.txt" ] || { echo "decompiler failed to start:"; head -5 "$OUT/_last_run.log"; exit 1; }
  p=$(cat "$OUT/_progress.txt")
  [ "$p" = "done" ] && break
  idx=$(echo "$p" | cut -f1)
  echo "$p	STACKOVERFLOW" >> "$OUT/_failures.txt"
  start=$((idx+1))
done
echo "done: $(find "$OUT" -name '*.cs' | wc -l) files, $(cat "$OUT/_failures.txt" 2>/dev/null | wc -l) failures"
