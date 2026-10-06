"""Query the private DD1 code map (built by tools/dd1re/run_all.ps1) from the command line.

  python dd1q.py fn <name part | entry>      functions: size, signature, callers, callees, strings, decomp file
  python dd1q.py key <data key>              Darkest.exe functions reading a DD1 data key (rules.json key,
                                             '.field' or 'type:' of a .darkest file), and for loaded keys the
                                             global it is stored in and the functions using that global
  python dd1q.py global <DAT_142acbae0>      functions using a global
  python dd1q.py str <text>                  functions referencing a string containing <text>
  python dd1q.py callers <fn> [depth]        who calls a function, as a tree (default depth 2)
  python dd1q.py callees <fn> [depth]        what a function calls, as a tree (default depth 1)
  python dd1q.py show <fn>                   print the decompiled C of a function

<fn> is an entry address (14012abc0) or an exact/partial qualified name. Map location: $DD1RE_DIR or D:/dd1-decomp.
"""
import json
import os
import sys
from pathlib import Path

ROOT = Path(os.environ.get("DD1RE_DIR", "D:/dd1-decomp"))


def load():
    fns = {}
    with open(ROOT / "raw" / "functions.jsonl", encoding="utf-8") as f:
        for line in f:
            fn = json.loads(line)
            fns[fn["entry"]] = fn
    paths = {}
    index = ROOT / "map" / "functions_index.tsv"
    if index.exists():
        for row in index.read_text(encoding="utf-8").splitlines()[1:]:
            cols = row.split("\t")
            paths[cols[0]] = (cols[5], cols[6])
    return fns, paths


def find(fns, query):
    q = query.lower().removeprefix("0x")
    if q in fns:
        return [fns[q]]
    exact = [f for f in fns.values() if f["name"].lower() == q or f["name"].lower().endswith("::" + q)]
    return exact or [f for f in fns.values() if q in f["name"].lower()]


def label(fns, entry):
    fn = fns.get(entry)
    return f"{fn['name']}@{entry}" if fn else entry


def one(fns, query):
    hits = find(fns, query)
    if len(hits) != 1:
        print(f"{len(hits)} matches for {query!r}" + (": " + ", ".join(label(fns, h["entry"]) for h in hits[:20]) if hits else ""))
        return None
    return hits[0]


def tree(fns, entry, field, depth, indent=1, seen=None):
    seen = seen if seen is not None else {entry}
    for e in fns[entry][field]:
        print("  " * indent + label(fns, e) + ("  (seen)" if e in seen else ""))
        if e not in seen and depth > 1 and e in fns:
            seen.add(e)
            tree(fns, e, field, depth - 1, indent + 1, seen)


def main(argv):
    if len(argv) < 2:
        print(__doc__)
        return
    cmd, arg = argv[0], argv[1]
    fns, paths = load()
    if cmd == "fn":
        hits = find(fns, arg)
        for fn in hits[:10]:
            decomp, sources = paths.get(fn["entry"], ("", ""))
            print(f"{fn['name']} @ {fn['entry']}  size={fn['size']}  {fn['signature']}")
            print(f"  decomp: {ROOT / decomp if decomp else '-'}")
            if sources:
                print(f"  source files: {sources}")
            print(f"  callers ({len(fn['callers'])}): " + ", ".join(label(fns, e) for e in fn["callers"][:15]))
            print(f"  callees ({len(fn['callees'])}): " + ", ".join(label(fns, e) for e in fn["callees"][:25]))
            print(f"  strings ({len(fn['strings'])}): " + " | ".join(s[:80] for s in fn["strings"][:30]))
        if len(hits) > 10:
            print(f"... {len(hits) - 10} more")
    elif cmd == "key":
        keys = json.loads((ROOT / "map" / "keys.json").read_text(encoding="utf-8"))
        for k in [arg, "." + arg, arg + ":"]:
            if k in keys:
                info = keys[k]
                print(f"{k}  in {', '.join(info['files'])}")
                if not info["readers"]:
                    print("  not referenced by Darkest.exe as a string")
                for e in info["readers"]:
                    print("  read in " + label(fns, e))
                for g in info.get("globals", []):
                    print(f"  stored in {g['global']} by {label(fns, g['loader'])}; used by:")
                    for e in g["used_by"]:
                        print("    " + label(fns, e))
    elif cmd == "global":
        index = json.loads((ROOT / "map" / "globals_index.json").read_text(encoding="utf-8"))
        g = arg.lower().lstrip("_").removeprefix("dat_")
        for e in index.get(g, []):
            print(label(fns, e))
    elif cmd == "str":
        needle = arg.lower()
        for fn in fns.values():
            hits = [s for s in fn["strings"] if needle in s.lower()]
            if hits:
                print(f"{label(fns, fn['entry'])}: " + " | ".join(h[:100] for h in hits[:5]))
    elif cmd in ("callers", "callees"):
        fn = one(fns, arg)
        if fn:
            depth = int(argv[2]) if len(argv) > 2 else (2 if cmd == "callers" else 1)
            print(label(fns, fn["entry"]))
            tree(fns, fn["entry"], cmd, depth)
    elif cmd == "show":
        fn = one(fns, arg)
        if fn:
            decomp = paths.get(fn["entry"], ("", ""))[0]
            print((ROOT / decomp).read_text(encoding="utf-8") if decomp else "no decompiled file")
    else:
        print(__doc__)


if __name__ == "__main__":
    sys.stdout.reconfigure(encoding="utf-8")
    main(sys.argv[1:])
