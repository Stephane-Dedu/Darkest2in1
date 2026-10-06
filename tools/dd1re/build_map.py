"""Builds the DD1 code map from the Ghidra exports (tools/dd1re/Dd1ExportFacts.java) and the DD1 install.

Reads  <root>/raw/{functions,strings,vtables}.jsonl, <root>/raw/names_from_strings.tsv, <root>/decomp/
Writes <root>/map/: README.md, keys.json, keys_by_file.md, rule_globals.md, globals_index.json,
       source_files.md, classes.md, named_from_strings.md, functions_index.tsv, manifest.json

Usage: python build_map.py [--root D:/dd1-decomp] [--dd1 "C:/Program Files (x86)/Steam/steamapps/common/DarkestDungeon"]
The map holds names, addresses and DD1's own data keys, but it sits next to decompiled code: keep it private.
"""
import argparse
import collections
import datetime
import hashlib
import json
import os
import re
from pathlib import Path

NOISE = re.compile(r"^(exprtk|std|PlayFab|Concurrency|`|_|Json::Value|curl|Steam|ImGui|spine|fmt)", re.I)
SOURCE = re.compile(r"source_code\\(.+\.(?:cpp|h|hpp|inl))$")
DARKEST_TYPE = re.compile(r"^\s*([A-Za-z_][A-Za-z0-9_]*)\s*:", re.M)
DARKEST_FIELD = re.compile(r"(?:^|\s)\.([A-Za-z_][A-Za-z0-9_]*)")
JSON_KEY = re.compile(r'"([A-Za-z_][A-Za-z0-9_]*)"\s*:')
GLOBAL = re.compile(r"\b_?DAT_([0-9a-f]{6,})\b")
KEY_LITERAL = re.compile(r'"([A-Za-z_][A-Za-z0-9_]*)"')
ASSIGN = re.compile(r"^\s*_?DAT_([0-9a-f]{6,})(?:\s*\[[^\]]*\])?\s*=")
# UI/presentation .darkest families: entries are named instances, not a fixed schema
PRESENTATION = re.compile(r"\.(layout|screen\.raid|raid|anim|lighting|map|raid\.status_bars|popup_text\.layout|"
                          r"panel\.\w+)\.darkest")


def load_jsonl(path):
    with open(path, encoding="utf-8") as f:
        return [json.loads(line) for line in f if line.strip()]


def family(rel):
    """shared/rules.json stays itself; heroes/crusader/crusader.info.darkest -> heroes/**/*.info.darkest.
    DLC files are grouped with their base-game family and tagged."""
    parts = rel.split("/")
    tag = ""
    if parts[0] == "dlc" and len(parts) > 2:
        tag, parts = " (dlc)", parts[2:]
    name = parts[-1]
    if name.count(".") < 2:
        return "/".join(parts) + tag
    suffix = name[name.find("."):]
    return f"{parts[0]}/**/*{suffix}{tag}" if len(parts) > 1 else f"*{suffix}{tag}"


def collect_keys(dd1):
    """key -> {"forms": exe strings to look for, "families": Counter}"""
    keys = {}

    def add(key, forms, fam):
        k = keys.setdefault(key, {"forms": set(), "families": collections.Counter()})
        k["forms"].update(forms)
        k["families"][fam] += 1

    for path in dd1.rglob("*"):
        rel = path.relative_to(dd1).as_posix()
        if rel.startswith(("mods/", "localization/", "_windows")) or not path.is_file():
            continue
        if path.suffix == ".json":
            text = path.read_text(encoding="utf-8", errors="replace")
            for key in set(JSON_KEY.findall(text)):
                add(key, {key}, family(rel))
        elif path.suffix == ".darkest":
            text = path.read_text(encoding="utf-8", errors="replace")
            for key in set(DARKEST_TYPE.findall(text)):
                add(key + ":", {key, key + ":"}, family(rel))
            for key in set(DARKEST_FIELD.findall(text)):
                add("." + key, {"." + key}, family(rel))
    return keys


def scan_decomp(decomp):
    """One pass over the C files: entry -> relative path, global -> entries using it, and loader assignments
    (a data key compared by name, then stored into a global: `memcmp(p,"key",n) ... DAT_x = ...`)."""
    paths, globals_used, assigned = {}, collections.defaultdict(set), collections.defaultdict(set)
    if not decomp.is_dir():
        return paths, globals_used, assigned
    for p in decomp.rglob("*.c"):
        entry = p.stem.rsplit("@", 1)[-1]
        paths[entry] = p.relative_to(decomp.parent).as_posix()
        text = p.read_text(encoding="utf-8", errors="replace")
        for g in set(GLOBAL.findall(text)):
            globals_used[g].add(entry)
        if "memcmp(" not in text:
            continue
        lines = text.splitlines()
        for i, line in enumerate(lines):
            if "memcmp(" not in line:
                continue
            key = KEY_LITERAL.search(line) or (i + 1 < len(lines) and KEY_LITERAL.search(lines[i + 1]))
            if not key:
                continue
            start = i + 2 if KEY_LITERAL.search(line) is None else i + 1
            for follow in lines[start:i + 14]:
                if "memcmp(" in follow or KEY_LITERAL.search(follow):
                    break  # the next key's block: this key wasn't stored in a global
                m = ASSIGN.search(follow)
                if m:
                    assigned[key.group(1)].add((m.group(1), entry))
                    break
    return paths, globals_used, assigned


def short(fn):
    return f"`{fn['name']}`@{fn['entry']}"


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--root", default=os.environ.get("DD1RE_DIR", "D:/dd1-decomp"))
    ap.add_argument("--dd1", default="C:/Program Files (x86)/Steam/steamapps/common/DarkestDungeon")
    args = ap.parse_args()
    root, dd1 = Path(args.root), Path(args.dd1)
    raw, out = root / "raw", root / "map"
    out.mkdir(parents=True, exist_ok=True)

    functions = {f["entry"]: f for f in load_jsonl(raw / "functions.jsonl")}
    strings = load_jsonl(raw / "strings.jsonl")
    vtables = load_jsonl(raw / "vtables.jsonl")
    decomp, globals_used, assigned = scan_decomp(root / "decomp")
    (out / "globals_index.json").write_text(
        json.dumps({g: sorted(es) for g, es in sorted(globals_used.items())}), encoding="utf-8")
    # functions touching many loaded globals at once set defaults or serialize: list them apart
    rule_globals = {g for pairs in assigned.values() for g, _ in pairs}
    bulk = {e for e, n in collections.Counter(e for g in rule_globals for e in globals_used.get(g, ())).items()
            if n > 40}

    # string value -> function entries (code references, then one data-table hop)
    users = collections.defaultdict(set)
    for fn in functions.values():
        for s in fn["strings"]:
            users[s].add(fn["entry"])
    for s in strings:
        users[s["value"]].update(s["functions"])
        users[s["value"]].update(s["via_table"])

    # data keys -> readers
    keys = collect_keys(dd1)
    key_out = {}
    for key, info in sorted(keys.items()):
        readers = set()
        for form in info["forms"]:
            readers |= users.get(form, set())
        key_out[key] = {
            "files": dict(info["families"].most_common()),
            "forms": sorted(info["forms"]),
            "readers": sorted(readers),
            "globals": [{"global": "DAT_" + g, "loader": loader,
                         "used_by": sorted(globals_used.get(g, set()) - {loader} - bulk)}
                        for g, loader in sorted(assigned.get(key, ()))],
        }
    (out / "keys.json").write_text(json.dumps(key_out, indent=1), encoding="utf-8")

    lines = ["# DD1 data keys loaded into globals, and the code using them", "",
             "A loader compares the key by name and stores its value in a global; the functions listed use that",
             "global. This is where a rule is applied. Functions using more than 40 of these globals (defaults,",
             "serialization) are left out: " + ", ".join(short(functions[e]) for e in sorted(bulk) if e in functions), ""]
    for key, info in key_out.items():
        for g in info["globals"]:
            users = ", ".join(short(functions[e]) for e in g["used_by"] if e in functions) or "(no other function)"
            lines.append(f"- `{key}` ({', '.join(info['files'])}) -> `{g['global']}`, loaded in "
                         f"{short(functions[g['loader']]) if g['loader'] in functions else g['loader']}; used by {users}")
    (out / "rule_globals.md").write_text("\n".join(lines) + "\n", encoding="utf-8")

    by_family = collections.defaultdict(list)
    for key, info in key_out.items():
        for fam in info["files"]:
            by_family[fam].append(key)
    lines = ["# DD1 data keys and the Darkest.exe functions that reference them", "",
             "In gameplay data, a key with no reader is not referenced as a string anywhere in the exe: DD1 most",
             "likely ignores it (confirm in the decompilation before relying on that). Presentation files come last:",
             "their entry names are widget/instance names looked up indirectly, so 'never referenced' means little",
             "there. Reader lists show at most 4 functions; the rest are in keys.json.", ""]
    for fam in sorted(by_family, key=lambda f: (bool(PRESENTATION.search(f)), f)):
        ks = sorted(by_family[fam])
        unread = [k for k in ks if not key_out[k]["readers"]]
        kind = " (presentation)" if PRESENTATION.search(fam) else ""
        lines += [f"## {fam}{kind}", "", f"{len(ks)} keys, {len(unread)} never referenced.", ""]
        if unread:
            lines += ["Never referenced: " + ", ".join(f"`{k}`" for k in unread), ""]
        lines += ["| key | readers |", "|---|---|"]
        for k in ks:
            rs = key_out[k]["readers"]
            if rs:
                shown = ", ".join(short(functions[e]) for e in rs[:4] if e in functions)
                more = f" (+{len(rs) - 4})" if len(rs) > 4 else ""
                lines.append(f"| `{k}` | {shown}{more} |")
        lines.append("")
    (out / "keys_by_file.md").write_text("\n".join(lines), encoding="utf-8")

    # source files named by assert strings
    sources = collections.defaultdict(set)
    for s, entries in users.items():
        m = SOURCE.search(s)
        if m:
            sources[m.group(1).replace("\\", "/")] |= entries
    lines = ["# DD1 source files and the functions whose assert/log strings name them", "",
             "A function listed here contains (or inlined) code from that file.", ""]
    for src in sorted(sources):
        fs = sorted(sources[src])
        lines.append(f"## {src} ({len(fs)})")
        lines += [f"- {short(functions[e])}" for e in fs if e in functions]
        lines.append("")
    (out / "source_files.md").write_text("\n".join(lines), encoding="utf-8")

    # classes / namespaces from function names, with RTTI vtables
    classes = collections.defaultdict(list)
    for fn in functions.values():
        if "::" in fn["name"]:
            classes[fn["name"].rsplit("::", 1)[0]].append(fn)
    vt_by_class = collections.defaultdict(list)
    for v in vtables:
        vt_by_class[v["class"]].append(v)
    game = sorted(c for c in set(classes) | set(vt_by_class) if not NOISE.match(c))
    noise = sorted(c for c in set(classes) | set(vt_by_class) if NOISE.match(c))
    lines = ["# Classes and namespaces (from RTTI recovery and string-based names)", "",
             f"{len(game)} game/engine classes below; {len(noise)} library ones (exprtk, std, PlayFab, ...) "
             "are only counted at the end.", ""]
    for c in game:
        fs = sorted(classes.get(c, []), key=lambda f: f["name"])
        lines.append(f"## {c} ({len(fs)} functions)")
        for v in vt_by_class.get(c, []):
            lines.append(f"- vftable @{v['addr']}: {len(v['slots'])} slots")
            lines += [f"  - [{i}] {slot}" for i, slot in enumerate(v["slots"])]
            users = [short(functions[e]) for e in v.get("referenced_by", []) if e in functions]
            if users:
                lines.append("  - loaded by (constructors, destructors or inlined copies): " + ", ".join(users))
        lines += [f"- {short(f)}" for f in fs]
        lines.append("")
    lines += ["## Library classes (counts only)", ""]
    lines += [f"- {c}: {len(classes.get(c, []))} functions, {len(vt_by_class.get(c, []))} vftables" for c in noise]
    (out / "classes.md").write_text("\n".join(lines), encoding="utf-8")

    # names applied from strings
    named = raw / "names_from_strings.tsv"
    if named.exists():
        rows = [r.split("\t") for r in named.read_text(encoding="utf-8").splitlines()[1:] if r]
        counts = collections.Counter(r[0] for r in rows)
        lines = ["# Qualified names found in Darkest.exe strings", "",
                 "`renamed`: applied to the only function using it. Other decisions were left as labels to read by hand.",
                 "", ", ".join(f"{k}: {v}" for k, v in counts.most_common()), "",
                 "| decision | name | functions | example string |", "|---|---|---|---|"]
        for r in rows:
            ex = r[3].replace("|", "\\|")[:120] if len(r) > 3 else ""
            lines.append(f"| {r[0]} | `{r[1]}` | {r[2]} | {ex} |")
        (out / "named_from_strings.md").write_text("\n".join(lines), encoding="utf-8")

    # flat index for grep
    src_by_fn = collections.defaultdict(set)
    for src, entries in sources.items():
        for e in entries:
            src_by_fn[e].add(src)
    with open(out / "functions_index.tsv", "w", encoding="utf-8") as f:
        f.write("entry\tname\tsize\tcallers\tcallees\tdecomp\tsource_files\n")
        for e, fn in sorted(functions.items()):
            f.write(f"{e}\t{fn['name']}\t{fn['size']}\t{len(fn['callers'])}\t{len(fn['callees'])}\t"
                    f"{decomp.get(e, '')}\t{','.join(sorted(src_by_fn.get(e, ())))}\n")

    exe = root / "bin" / "Darkest.exe"
    named_count = sum(1 for fn in functions.values() if not re.match(r"^(FUN_|thunk_FUN_|LAB_)", fn["name"].split("::")[-1]))
    manifest = {
        "built": datetime.datetime.now(datetime.timezone.utc).isoformat(timespec="seconds"),
        "exe_sha256": hashlib.sha256(exe.read_bytes()).hexdigest() if exe.exists() else None,
        "functions": len(functions),
        "functions_named": named_count,
        "decompiled_files": len(decomp),
        "strings": len(strings),
        "vftables": len(vtables),
        "data_keys": len(key_out),
        "data_keys_unreferenced": sum(1 for k in key_out.values() if not k["readers"]),
        "data_keys_loaded_into_globals": sum(1 for k in key_out.values() if k["globals"]),
        "source_files": len(sources),
        "game_classes": len(game),
    }
    (out / "manifest.json").write_text(json.dumps(manifest, indent=1), encoding="utf-8")
    readme = ["# DD1 code map (private: decompiled game code, never commit)", "",
              "Generated by tools/dd1re/build_map.py in the Darkest2in1 repo; its tools/dd1re/README.md explains",
              "how to query and rebuild. Query with `python tools/dd1re/dd1q.py`.", "",
              "| | |", "|---|---|"]
    readme += [f"| {k} | {v} |" for k, v in manifest.items()]
    (out / "README.md").write_text("\n".join(readme) + "\n", encoding="utf-8")
    print(json.dumps(manifest, indent=1))


if __name__ == "__main__":
    main()
