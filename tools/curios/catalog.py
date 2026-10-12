"""Audit installed curio coverage and build a private, searchable design catalog. Never reads saves."""
import argparse
import csv
import html
import json
import os
import re
from collections import Counter
from pathlib import Path

import numpy as np
from PIL import Image

HERE = Path(__file__).resolve().parent
PAIR = {"Ruins": "Sprawl / Ruins", "Warrens": "Foetor / Warrens", "Weald": "Tangle / Weald",
        "Cove": "Shroud / Cove", "Darkest Dungeon": "Mountain", "Courtyard": "Crimson Court", "COM": "Farmstead"}

def read_library(path):
    rows = list(csv.reader(path.open(encoding="utf-8-sig")))
    headers = [i for i, r in enumerate(rows) if len(r) > 2 and r[2].strip() == "ID STRING"]
    entries = {}
    for n, start in enumerate(headers):
        end = headers[n + 1] - 1 if n + 1 < len(headers) else len(rows)
        key = rows[start + 1][2].strip()
        entry = {"id": key, "name": rows[start - 1][2].strip(), "region": "", "supplies": []}
        in_items = False
        for i in range(start + 1, end):
            row = rows[i] + [""] * 19
            if row[2] == "REGION FOUND" and i + 1 < end:
                entry["region"] = rows[i + 1][2].strip()
            if row[2] == "Item Interactions":
                in_items = True
            elif in_items and row[4].strip() and row[4] != "ITEM":
                entry["supplies"].append(row[4].strip())
        entries[key] = entry
    return entries

def inventory(game):
    base = read_library(game / "curios/curio_type_library.csv")
    entries = {key: dict(value, scope="base-runtime", origin="base") for key, value in base.items()}
    prop_paths = [game / "curios/curio_props.csv"]
    for path in sorted((game / "dlc").rglob("*curio_type_library.csv")):
        for key, value in read_library(path).items():
            entries[key] = dict(value, scope="DLC-design", origin=path.parent.parent.name)
    prop_paths += sorted((game / "dlc").rglob("*curio_props.csv"))
    for path in prop_paths:
        for row in csv.DictReader(path.open(encoding="utf-8-sig")):
            key = row.get("Curio Prop Name", "").strip()
            if key in entries:
                entries[key]["sprite"] = row["Sprite Reference"]
    goals = json.loads((game / "campaign/quest/quest.types.json").read_text())
    for goal in goals["goals"]:
        key = goal.get("data", {}).get("curio_name")
        if not key:
            continue
        e = entries.setdefault(key, {"id": key, "name": key.replace("_", " ").title(),
                                    "region": "Quest", "supplies": [], "scope": "quest-runtime", "origin": "base"})
        e.setdefault("goals", []).append(goal["id"])
        e["supplies"] = sorted(set(e["supplies"] + [x["id"] for x in goal.get("starting_items", [])]))
    for folder in (game / "props/shared/curios").iterdir():
        if folder.is_dir() and folder.name not in entries:
            entries[folder.name] = {"id": folder.name, "name": folder.name.replace("_", " ").title(),
                                    "scope": "legacy-art", "region": "Legacy", "supplies": [], "origin": "base"}
    entries["faded_memory"] = {"id": "faded_memory", "name": "Faded Memory", "scope": "owner-exception",
                                "region": "Boss quests", "supplies": [], "origin": "mod"}
    goal_regions = {}
    for kind in goals["types"]:
        for group in kind["goal_lists"]:
            if group["dungeon"] == "all":
                continue
            for alternatives in group["goals"]:
                for goal_id in alternatives:
                    goal_regions[goal_id] = {"crypts": "Ruins", "cove": "Cove", "warrens": "Warrens",
                                             "weald": "Weald"}[group["dungeon"]]
    for entry in entries.values():
        regions = {goal_regions[g] for g in entry.get("goals", []) if g in goal_regions}
        if regions:
            assert len(regions) == 1, f"Multiple quest regions for {entry['id']}: {regions}"
            entry["region"] = regions.pop()
    for key in ["beacon", "teleporter"]:
        entries[key]["region"] = "Darkest Dungeon"
    return entries

def prepare_parts(doc, kit, out):
    evidence = {}
    for key, part in doc["parts"].items():
        prefix, suffix = part["folder"].split("/", 1)
        folder = (kit if prefix == "kit" else out / "extra") / suffix
        scene = json.loads((folder / "scene.json").read_text())
        selected = []
        for name in part["objects"]:
            candidates = [o for o in scene["objects"] if o["name"] == name and not o.get("lod_skip") and o.get("enabled", True)]
            if not candidates:
                raise ValueError(f"Missing native object {key}: {name} in {folder}")
            selected.append(candidates[0])
        if not part["objects"]:
            selected = [o for o in scene["objects"] if not o.get("lod_skip") and o.get("enabled", True)
                        and not any(x in o["name"].lower() for x in ["vfx", "smoke", "fog", "collider", "glow"])]
        target = out / "parts" / key
        target.mkdir(parents=True, exist_ok=True)
        bounds = []
        tris = 0
        for obj in selected:
            mesh = folder / f"mesh-{obj['mesh']}.npz"
            dest = target / mesh.name
            if not dest.exists():
                os.link(mesh, dest)
            data = np.load(mesh)
            faces = data["tri"]
            if "first" in obj:
                sub = np.repeat(np.arange(len(data["sub"])), data["sub"])
                faces = faces[(sub >= obj["first"]) & (sub < obj["first"] + obj["count"])]
            if not len(faces):
                raise ValueError(f"Empty native part {key}")
            verts = data["v"][np.unique(faces)]
            world = np.c_[verts, np.ones(len(verts))] @ np.array(obj["world"]).T
            bounds.append(world[:, :3])
            tris += len(faces)
        material_ids = {m for o in selected for m in o["materials"]}
        materials = {}
        textures = []
        for mid in material_ids:
            material = dict(scene["materials"][mid])
            if material.get("texture"):
                path = (folder / material["texture"]).resolve()
                with Image.open(path) as image:
                    dimensions = list(image.size)
                material["texture"] = str(path)
                textures.append({"name": material["name"], "file": str(path), "dimensions": dimensions})
            materials[mid] = material
        assert textures, f"No usable textures for {key}"
        private_scene = dict(scene, objects=selected, materials=materials)
        (target / "scene.json").write_text(json.dumps(private_scene), encoding="utf-8")
        vertices = np.concatenate(bounds)
        size = vertices.max(0) - vertices.min(0)
        evidence[key] = {"source": scene["source"], "source_folder": str(folder),
                         "objects": [o["name"] for o in selected], "triangles": tris,
                         "size_xyz": size.round(4).tolist(), "textures": textures, "note": part["note"],
                         "role": part["role"]}
    return evidence

def write_gallery(designs, evidence, out):
    def esc(s): return html.escape(str(s), quote=True)
    cards = []
    for d in designs:
        sources = "".join(f'<figure><img loading="lazy" src="renders/{p}.png" alt="{esc(p)} native source"><figcaption>{esc(p.replace("_", " "))}</figcaption></figure>' for p in d["parts"])
        scope = d["scope"]
        pairs = PAIR.get(d["region"], d["region"])
        source_details = "".join(f'<li><b>{esc(p)} · {esc(evidence[p]["role"])}</b><br>{esc(evidence[p]["source"])}<br><small>{esc(", ".join(evidence[p]["objects"]))}</small><br>{esc(evidence[p]["note"])}<br>{evidence[p]["triangles"]:,} triangles · {esc(", ".join("×".join(map(str,t["dimensions"])) for t in evidence[p]["textures"]))} texture pixels</li>' for p in d["parts"])
        supplies = ", ".join(d["supplies"]) or "Hand interaction / no mapped supply"
        concepts = f'<a href="concepts/{d["id"]}.png"><img class="concept" src="concepts/{d["id"]}.png" alt="Assembly study for {esc(d["name"])}"></a><p class="caption">Native prop assembly study. Custom sculpture and moving parts remain to build.</p>' if (out / "concepts" / (d["id"] + ".png")).exists() else ""
        searchable = " ".join([d["id"], d["name"], d["title"], d["design"], supplies, *d["parts"]]).lower()
        cards.append(f'''<article data-scope="{esc(scope)}" data-region="{esc(pairs)}" data-study="{int(bool(concepts))}" data-search="{esc(searchable)}">
<div class="meta"><span>{esc(pairs)}</span><span class="tag">{esc(scope)}</span></div>
<h2>{esc(d['name'])}</h2><p class="proposal">{esc(d['title'])}</p>{concepts}
<div class="sources">{sources}</div><p class="caption">Native source parts. Proposed shape changes are described below.</p>
<p>{esc(d['design'])}</p><details><summary>Build notes and interaction states</summary>
<p><b>New modeling / finish work</b><br>{esc(d['new_work'])}</p>
<p><b>Idle and hover</b><br>{esc(d['idle'])}</p>
<p><b>Resolved state</b><br>{esc(d['resolved'])}</p>
<p><b>Current supply cues</b><br>{esc(supplies)}</p><p><b>Original ID</b> <code>{esc(d['id'])}</code><br>
<b>Original sprite alias</b> <code>{esc(d.get('sprite', d['id']))}</code></p>
<ul class="provenance">{source_details}</ul></details></article>''')
    regions = sorted({PAIR.get(d["region"], d["region"]) for d in designs})
    options = "".join(f'<option>{esc(r)}</option>' for r in regions)
    summary = Counter(d["scope"] for d in designs)
    content = '''<!doctype html><html lang="en"><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1">
<title>Darkest2in1 · Curio design atlas</title><style>
@font-face{font-family:DD;src:url('fonts/NDDunkelD-Bold.otf')}@font-face{font-family:Body;src:url('fonts/AlegreyaSans-Regular.ttf')}
:root{color-scheme:dark;--bone:#d8d0bb;--muted:#a69e8c;--gold:#c7a465;--line:#433c31;--ink:#111210}*{box-sizing:border-box}
body{margin:0;background:var(--ink);color:var(--bone);font:19px Body,Georgia,serif;line-height:1.4}header{padding:54px max(5vw,24px) 30px;background:radial-gradient(ellipse at 85% 0,#432b27,transparent 65%);border-bottom:1px solid var(--line)}
header .eyebrow{font:13px system-ui;letter-spacing:.22em;color:var(--gold);text-transform:uppercase}h1{font:clamp(36px,5vw,70px) DD,Georgia;margin:12px 0}header p{max-width:920px;color:var(--muted)}a{color:var(--gold)}
.stats{display:flex;gap:28px;flex-wrap:wrap;font:14px system-ui;color:var(--muted)}.stats strong{font:30px DD;color:var(--bone);display:block}
nav{position:sticky;top:0;z-index:2;padding:18px max(5vw,24px);background:#161713f5;border-bottom:1px solid var(--line);display:flex;gap:12px;flex-wrap:wrap;align-items:center}
input,select{font:16px system-ui;background:#22231d;color:var(--bone);border:1px solid #5c5140;padding:12px;border-radius:2px}input[type=search]{flex:1;min-width:200px}#count,nav label{font:14px system-ui;color:var(--muted)}nav label{white-space:nowrap}
main{padding:32px max(5vw,24px);display:grid;grid-template-columns:repeat(auto-fill,minmax(330px,1fr));gap:24px;align-items:start}
article{background:linear-gradient(135deg,#24251e,#181a15);border:1px solid var(--line);padding:22px;box-shadow:0 8px 26px #0004}article[hidden]{display:none}
.meta{font:11px system-ui;letter-spacing:.07em;text-transform:uppercase;color:var(--gold);display:flex;justify-content:space-between;gap:8px}.tag{color:var(--muted)}h2{font:27px DD,Georgia;margin:15px 0 3px}.proposal{color:var(--gold);margin:0 0 20px;font-size:19px}
.sources{display:flex;gap:6px;background:#33372e;border:1px solid #4c5141;min-height:165px;align-items:center}.sources figure{margin:0;flex:1;min-width:0;text-align:center;padding:4px}.sources img{display:block;object-fit:contain;width:100%;height:140px}.sources figcaption{font:11px system-ui;color:#cec6ae;word-break:break-word;padding:8px 0}.caption{font:11px system-ui;color:var(--muted);margin:8px 0 18px}.concept{width:100%;display:block;background:#2e322b;margin-bottom:12px}
details{border-top:1px solid var(--line);padding-top:15px;font-size:17px}summary{cursor:pointer;color:var(--gold)}b{font-weight:600;color:#e5dcc3}.provenance{padding-left:18px;font:12px system-ui;overflow-wrap:anywhere}.provenance li{margin:10px 0}code{font-size:13px}footer{padding:30px 5vw;color:var(--muted);border-top:1px solid var(--line)}
</style><header><div class="eyebrow">Darkest2in1 · art direction / v01</div><h1>The Curio Atlas</h1>
<p>Keep every curio recognizable. Rebuild its silhouette with DD2's painted wood, carved stone, cloth and organic detail. Each proposal names real source parts and the modeling still needed. No gameplay changes are installed.</p>
<div class="stats">STATS</div><p>Base and quest entries map the current campaign. DLC entries are design coverage, not a claim of runtime support. Legacy art stays unassigned. Faded Memory retains its owner-defined DD1 appearance.</p>
<p><a href="curio-mapping.md">Read the complete mapping</a> · <a href="assembly-studies.jpg">View the 12 assembly studies</a> · <a href="audit.json">Coverage and source audit</a></p></header>
<nav><input id="search" type="search" placeholder="Search a curio, material, source or supply…" aria-label="Search curios"><select id="region" aria-label="Region"><option value="">Every region</option>OPTIONS</select>
<select id="scope" aria-label="Scope"><option value="">Every scope</option><option>base-runtime</option><option>quest-runtime</option><option>DLC-design</option><option>legacy-art</option><option>owner-exception</option></select><label><input id="studies" type="checkbox"> Assembly studies</label><span id="count"></span></nav>
<main>CARDS</main><footer>Private local references from the owner's installed games. Native source thumbnails are not finished curio models. Assembly studies are explicitly labeled. No saves were read or written.</footer>
<script>const cards=[...document.querySelectorAll('article')];const q=document.querySelector('#search'),r=document.querySelector('#region'),s=document.querySelector('#scope'),a=document.querySelector('#studies');function filter(){let n=0;for(const c of cards){c.hidden=!!((r.value&&c.dataset.region!==r.value)||(s.value&&c.dataset.scope!==s.value)||(a.checked&&c.dataset.study!=='1')||!c.dataset.search.includes(q.value.toLowerCase().trim()));if(!c.hidden)n++}document.querySelector('#count').textContent=n+' / '+cards.length+' proposals'}for(const e of [q,r,s,a])e.addEventListener('input',filter);filter();</script></html>'''
    stats = "".join(f'<span><strong>{n}</strong>{esc(scope)}</span>' for scope, n in summary.items())
    content = content.replace("STATS", stats).replace("OPTIONS", options).replace("CARDS", "".join(cards))
    (out / "index.html").write_text(content, encoding="utf-8")

def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--game", type=Path, required=True)
    parser.add_argument("--kit", type=Path, required=True)
    parser.add_argument("--out", type=Path, required=True)
    args = parser.parse_args()
    if args.out.resolve().is_relative_to(HERE.parents[1]):
        parser.error("Private outputs must be outside the Git repository.")
    args.out.mkdir(parents=True, exist_ok=True)
    doc = json.loads((HERE / "designs.json").read_text())
    entries = inventory(args.game)
    ids = [d["id"] for d in doc["designs"]]
    assert len(ids) == len(set(ids)), "Duplicate design IDs"
    assert set(ids) == set(entries), f"Coverage gap: missing={set(entries)-set(ids)} extra={set(ids)-set(entries)}"
    evidence = prepare_parts(doc, args.kit, args.out)
    designs = []
    for proposal in doc["designs"]:
        assert all(p in evidence for p in proposal["parts"])
        d = dict(entries[proposal["id"]], **proposal)
        d["idle"] = "Stable grounded silhouette; only loose cloth, water or flame moves. Hover adds a thin warm rim and the native interaction glyph, without revealing the outcome."
        if d["scope"] == "owner-exception":
            d["idle"] = "Retain the existing DD1 spectral appearance and hand action; any DD2 frame remains secondary."
        designs.append(d)
    audit = {"curios": len(designs), "scopes": dict(Counter(d["scope"] for d in designs)),
             "native_source_groups": len(evidence), "source_objects": sum(len(p["objects"]) for p in evidence.values()),
             "missing_designs": [], "duplicate_ids": [], "parts": evidence,
             "limits": ["Source references and proposals, not installed 3D curios.", "DLC and legacy entries do not imply runtime support.",
                        "Flat pickup chest icon, combined inn scene mesh, Guardian building and Hero Shrine chapel are excluded as curio bodies.",
                        "Cloth, moss, chains and carved stone references require curio-specific meshes and UVs.",
                        "Offline emission renders do not validate native shaders, animation or picking."]}
    (args.out / "audit.json").write_text(json.dumps(audit, indent=2), encoding="utf-8")
    (args.out / "inventory.json").write_text(json.dumps(designs, indent=2), encoding="utf-8")
    fonts = args.out / "fonts"
    fonts.mkdir(exist_ok=True)
    for name in ["NDDunkelD-Bold.otf", "AlegreyaSans-Regular.ttf"]:
        dest = fonts / name
        if not dest.exists():
            os.link(args.kit / "fonts" / name, dest)
    lines = ["# Curio design mapping", "", "104 individual proposals. Source parts are native DD2 references; every remaining modeling task is explicit.", ""]
    for scope in ["base-runtime", "quest-runtime", "DLC-design", "legacy-art", "owner-exception"]:
        lines += ["## " + scope, "", "| Curio / ID | Region | Proposed design | Sources | New work | Resolved state |", "|---|---|---|---|---|---|"]
        for d in designs:
            if d["scope"] != scope:
                continue
            lines.append(f'| {d["name"]}<br>`{d["id"]}` | {PAIR.get(d["region"],d["region"])} | **{d["title"]}**. {d["design"]} | {", ".join(d["parts"])} | {d["new_work"]} | {d["resolved"]} |')
        lines.append("")
    (args.out / "curio-mapping.md").write_text("\n".join(lines), encoding="utf-8")
    write_gallery(designs, evidence, args.out)
    print(json.dumps({k: audit[k] for k in ["curios", "scopes", "native_source_groups", "source_objects", "missing_designs"]}, indent=2))

if __name__ == "__main__":
    main()
