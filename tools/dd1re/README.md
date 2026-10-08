# DD1 code map (Darkest.exe reverse engineering)

A private, searchable map of DD1's native win64 `Darkest.exe`, built with Ghidra, for questions that DD1's data
and the Unity port don't answer: formulas, ordering, which data keys the game actually reads.
The REA CLI can't analyse native DD1 on this host (see [rea_usage.md](../rea_usage.md)), so this runs Ghidra directly.

**Private output.** Everything lands under `D:\dd1-decomp` (`$DD1RE_DIR`), outside the repo. It is decompiled game
code: never commit it, quote it in commits, or copy it into the mod. Write rules into MODLOG/PARITY in your own words.

## Look something up

```powershell
$py = "$env:LOCALAPPDATA\Programs\Python\Python311\python.exe"
& $py tools\dd1re\dd1q.py key quirk_chance_to_lock_negative   # who reads a rules.json key
& $py tools\dd1re\dd1q.py key .is_crit_valid                  # a .darkest field ('.field' or 'type:')
& $py tools\dd1re\dd1q.py str "Estate Error"                  # functions using a string
& $py tools\dd1re\dd1q.py fn Darkest::RaidFinish              # callers, callees, strings, decomp path
& $py tools\dd1re\dd1q.py callers 140123450 3                 # call tree upward
& $py tools\dd1re\dd1q.py show Darkest::RaidFinish            # decompiled C
```

Or grep `D:\dd1-decomp\decomp` (one `.c` file per function) and read `D:\dd1-decomp\map\`:

| File | What |
|---|---|
| `map/README.md`, `map/manifest.json` | exe SHA-256, counts, when it was built |
| `map/keys_by_file.md`, `map/keys.json` | every DD1 data key, per data file family, with the functions that reference it; keys the exe never references |
| `map/rule_globals.md`, `map/globals_index.json` | keys a loader stores in a global (`rules.json`: `quirk_chance_to_lock_negative` -> `DAT_142acbae0`), and every function using each global: where a rule is applied |
| `map/source_files.md` | DD1 source files named by assert strings (`game/rules.raid/actor.cpp`, ...) and their functions |
| `map/classes.md` | classes and namespaces (RTTI recovery + names from strings), vftable slots |
| `map/named_from_strings.md` | qualified names found in strings, and which were applied |
| `map/functions_index.tsv` | entry, name, size, caller/callee counts, decomp path, source files |
| `raw/*.jsonl` | the Ghidra facts behind all of the above (functions, strings, vtables) |
| `ghidra/DD1.gpr` | the Ghidra project: open it in Ghidra to rename and annotate while investigating |

## How to read the evidence
- A key with no reader has no match in the exported string references. That alone does not prove DD1 ignores it.
  The key may be constructed at runtime or consumed indirectly. Trace the loader and gameplay code before changing
  mod behaviour.
- Names come from RTTI (classes, vftables, constructors) and from DD1's own log strings. Names from strings carry the plate
  comment `dd1re: named from a log/assert string`: an inlined callee can lend its name to the caller.
- Most functions stay `FUN_<entry>`. Navigate from a data key to the global it is loaded into and the functions using
  that global (`dd1q.py key`), or from a string to its reader, then follow callers and callees. The decompiler also
  prints lambda vtable names such as `Roster::System::OnRaidFinish(...)::<lambda_1>`, which name the enclosing function.
- Decompiled floats, struct offsets and inlined code mislead easily. Check every rule against DD1's data, then write a
  Core test before coding it.

## Current private output (checked 2026-10-08)

The executable still matches the manifest's SHA-256. The interrupted refresh has 35,134 C files; the older map
indexes 55,971, with 22,540 indexed paths currently missing. `globals_index.json` and `rule_globals.md` have not
been generated in the private map yet. Key-to-string lookup works, but global queries fail until the map is rebuilt.
Treat this as mixed output from two runs, rather than a complete current analysis.

The owner's handoff requires approval for the interrupted decompile refresh, with the game stopped. After that
refresh, regenerate the map. `-Resume` skips any existing C file, including files containing a `decompile failed`
marker, so file counts alone do not establish successful decompilation.

The builder and query smoke test uses synthetic input in a temporary directory, without Ghidra or game files:

```powershell
& "$env:LOCALAPPDATA\Programs\Python\Python311\python.exe" tools\dd1re\test_build_map.py
```

## Rebuild
Needs Microsoft OpenJDK 21 (`winget install Microsoft.OpenJDK.21`), Ghidra 12.1.4 unpacked at
`D:\re-tools\ghidra_12.1.4_PUBLIC` (official release zip, SHA-256
`ddac49f903da9d5bac833e5cc79395098b9c33cfd3279be5f31bd00387d2d4db`) and Python 3.11. Rebuild when the exe's
SHA-256 differs from `map/manifest.json`, which means Steam updated DD1:

```powershell
powershell -ExecutionPolicy Bypass -File tools\dd1re\run_all.ps1                         # all steps
powershell -ExecutionPolicy Bypass -File tools\dd1re\run_all.ps1 -From map               # restart: import|classes|facts|decomp|map
powershell -ExecutionPolicy Bypass -File tools\dd1re\run_all.ps1 -From decomp -Resume    # finish an interrupted decompile
```

Measured on 2026-10-06: auto-analysis took 9 minutes, names and facts 3 minutes, and decompiling 56,000 functions on
12 threads about 10 minutes. With an 8 GB heap, 12 decompilers and a game running, the second run used up the 16 GB of RAM and
was stopped. The defaults are now a 4 GB heap and 4 decompiler threads (`-MaxMem`, `-Threads`), so expect the
decompile to take longer. Run it when the owner isn't playing.

- `analyzeHeadless.bat` breaks on paths containing parentheses, so the script copies the exe to `D:\dd1-decomp\bin`
  first.
- Ghidra's own `RecoverClassesFromRTTIScript` stops on this exe with "More than one Base Class Array", because MSVC
  folds identical lambda RTTI. `Dd1NameVirtuals.java` replaces it.
- Logs are in `D:\dd1-decomp\logs`.

Scripts:
- `Dd1NameVirtuals.java` names virtual functions under their class.
- `Dd1NameFromStrings.java` names functions from DD1's log strings.
- `Dd1ExportFacts.java` exports the raw facts.
- `Dd1ExportDecomp.java` writes the C files.
- `build_map.py` builds the map.
- `dd1q.py` runs queries.
