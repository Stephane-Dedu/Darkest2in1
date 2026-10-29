"""Assemble generated split-barrel and crystal components into stable states.

The entire hull and crystal growth are generated raster art. Native barrel
paint was a material reference only. Used removes the added crystal mass;
the split hull, hoops, staves, residue and contact points remain registered.
"""
import argparse
import hashlib
import json
from pathlib import Path

import numpy as np
from PIL import Image


def assemble(components, output):
    components, output = Path(components).resolve(), Path(output).resolve()
    repo = Path(__file__).resolve().parents[2]
    if components.is_relative_to(repo) or output.is_relative_to(repo) or output.exists():
        raise ValueError("Use the retained private original and a new private output")
    digest = hashlib.sha256(components.read_bytes()).hexdigest()
    if digest != "19ea9413a6e76a0200e8714507bcf1655671bdb80f0462dbc622c2f0d4383e0a":
        raise ValueError("Generated original changed")
    image = Image.open(components)
    if image.mode != "RGBA" or image.size != (1774, 887):
        raise ValueError("Retain the complete original RGBA bitmap")
    alpha = np.asarray(image)[:, :, 3]
    if int(alpha[:, 1200:1300].max()) > 8:
        raise ValueError("The complete component gap must remain transparent")
    body_box, growth_box = (0, 0, 1200, 887), (1300, 150, 1774, 870)
    coverage = np.zeros(alpha.shape, dtype=bool)
    for x0, y0, x1, y1 in (body_box, growth_box):
        coverage[y0:y1, x0:x1] = True
    if np.any((alpha > 8) & ~coverage):
        raise ValueError("A visible source pixel would be discarded")
    body = image.crop(body_box)
    growth = image.crop(growth_box).resize((521, 792), Image.Resampling.LANCZOS)
    rgba = np.asarray(growth).copy()
    original_alpha = rgba[:, :, 3].copy()
    rgba[:, :, :3] = np.rint(rgba[:, :, :3].astype(float) * .75).clip(0, 255).astype(np.uint8)
    if not np.array_equal(rgba[:, :, 3], original_alpha):
        raise ValueError("RGB grading changed the component alpha")
    used = Image.new("RGBA", (1200, 1120))
    used.alpha_composite(body, (0, 220))
    closed = used.copy()
    closed.alpha_composite(Image.fromarray(rgba), (440, 180))
    output.mkdir(parents=True)
    for name, frame in (("closed", closed), ("used", used)):
        frame.save(output / (name + ".png"))
    record = dict(original=str(components), original_sha256=digest,
                  entire_body_and_growth_generated_raster_not_native_geometry=True,
                  original_alpha_retained_no_threshold_or_normalization=True,
                  body_crop=body_box, growth_crop=growth_box, body_offset=[0, 220],
                  growth_nominal_uniform_scale=1.1, rounded_growth_size=[521, 792],
                  growth_RGB_only_multiplier=.75, growth_offset=[440, 180],
                  growth_domain=[440, 180, 961, 972], whole_body_fixed=True,
                  used_clears_only_added_crystals=True, no_native3D_depth_claim=True,
                  accepted=False, installed=False, native_QA=False)
    (output / "provenance.json").write_text(json.dumps(record, indent=2))


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--components", type=Path, required=True)
    parser.add_argument("--out", type=Path, required=True)
    args = parser.parse_args()
    assemble(args.components, args.out)
