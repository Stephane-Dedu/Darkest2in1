"""Check catalog artifacts and create private proof sheets of rendered native geometry."""
import argparse
import json
from html.parser import HTMLParser
from pathlib import Path

from PIL import Image, ImageDraw, ImageFont

parser = argparse.ArgumentParser(description=__doc__)
parser.add_argument("--out", type=Path, required=True)
args = parser.parse_args()
out = args.out.resolve()
audit = json.loads((out / "audit.json").read_text())
designs = json.loads((out / "inventory.json").read_text())
studies = json.loads((out / "concepts/studies.json").read_text())
assert audit["curios"] == len(designs) == 104
assert not audit["missing_designs"] and not audit["duplicate_ids"]
assert set(studies) <= {d["id"] for d in designs}
# Reject the flat pickup icon and the previously combined scene mesh as chest/table bodies.
assert min(audit["parts"]["chest"]["size_xyz"]) > .1
assert audit["parts"]["table"]["triangles"] < 3000
assert audit["parts"]["dagger"]["triangles"] < 3000

class CatalogCheck(HTMLParser):
    def __init__(self):
        super().__init__()
        self.cards = 0
        self.images = 0

    def handle_starttag(self, tag, attrs):
        attrs = dict(attrs)
        if tag == "article":
            self.cards += 1
        if tag == "img":
            image_path = (out / attrs["src"]).resolve()
            assert image_path.is_relative_to(out) and image_path.is_file(), image_path
            self.images += 1

check = CatalogCheck()
check.feed((out / "index.html").read_text(encoding="utf-8"))
assert check.cards == 104
image_evidence = {}
for path in sorted((out / "renders").glob("*.png")) + sorted((out / "concepts").glob("*.png")):
    with Image.open(path) as image:
        assert image.mode == "RGBA", path
        bounds = image.getchannel("A").getbbox()
        assert bounds and bounds[2] - bounds[0] > 50 and bounds[3] - bounds[1] > 30, path
        image_evidence[str(path.relative_to(out))] = {"size": image.size, "alpha_bounds": bounds}

font = ImageFont.truetype(str(out / "fonts/AlegreyaSans-Regular.ttf"), 23)
small = ImageFont.truetype(str(out / "fonts/AlegreyaSans-Regular.ttf"), 18)

def sheet(items, filename, columns, width, height):
    image = Image.new("RGB", (columns * width, ((len(items) + columns - 1) // columns) * height), "#20241f")
    draw = ImageDraw.Draw(image)
    for i, (path, title, subtitle) in enumerate(items):
        x, y = (i % columns) * width, (i // columns) * height
        draw.rectangle((x + 7, y + 7, x + width - 7, y + height - 7), fill="#30362c", outline="#5b5946")
        with Image.open(path) as source:
            source.thumbnail((width - 24, height - 72))
            image.paste(source, (x + (width - source.width) // 2, y + 12), source)
        draw.text((x + 16, y + height - 58), title, font=font, fill="#ded5ba")
        draw.text((x + 16, y + height - 29), subtitle, font=small, fill="#b9ac8f")
    image.save(out / filename, quality=94)

sheet([(out / "renders" / (key + ".png"), key.replace("_", " "), p["role"])
       for key, p in audit["parts"].items()], "source-sheet.jpg", 6, 250, 252)
names = {d["id"]: d["name"] for d in designs}
sheet([(out / "concepts" / (key + ".png"), names[key], "Native assembly study / v01") for key in studies],
      "assembly-studies.jpg", 3, 500, 438)
report = {"cards": check.cards, "image_links": check.images, "source_groups": len(audit["parts"]),
          "assembly_studies": len(studies), "images": image_evidence,
          "limits": "Offline file/geometry coverage checks. Visual review and native runtime acceptance are separate."}
(out / "artifact-check.json").write_text(json.dumps(report, indent=2), encoding="utf-8")
print(json.dumps({key: value for key, value in report.items() if key != "images"}, indent=2))
