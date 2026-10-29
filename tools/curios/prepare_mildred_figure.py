"""Preserve the generated Mildred figure and prepare its source-proved departure.

The complete figure is generated raster art, not native geometry. Its closed
PNG and alpha remain byte-exact. Used is fully transparent because the proper
original investigated pose contains no visible figure. No used redraw is made.
"""
import argparse
import hashlib
import json
import os
from pathlib import Path
import shutil

from PIL import Image

from departed_state import SEMANTICS, departure_proof


def prepare(body, terminal_reference, output):
    body, terminal_reference, output = (Path(p).resolve() for p in (body, terminal_reference, output))
    repo = Path(__file__).resolve().parents[2]
    if any(p.is_relative_to(repo) for p in (body, terminal_reference, output)) or output.exists():
        raise ValueError("Use the retained private originals and a new private output")
    digest = hashlib.sha256(body.read_bytes()).hexdigest()
    if digest != "831aa31afea6d43b619ebbb95cec93c00240c67166b13093424b44c36bef21c8":
        raise ValueError("Generated original changed")
    with Image.open(body) as figure:
        if figure.mode != "RGBA" or figure.size != (1024, 1536):
            raise ValueError("Retain the complete original RGBA figure")
        size, alpha = figure.size, figure.getchannel("A").getextrema()
    if alpha[0] != 0 or alpha[1] <= 8:
        raise ValueError("The closed figure must remain visible with original transparency")
    metadata = {
        "id": "com_millerwife_curio_design", "used_state_semantics": SEMANTICS,
        "departure_proof": {
            "curio_id": "com_millerwife_curio", "animation": "investigated",
            "reference_file": str(terminal_reference),
            "reference_sha256": "f95f8d1af2b2b0f0eda2420ac53edb1eaee6c7c42b128ebc8837a1497c711015",
        },
    }
    metadata["departure_proof"] = departure_proof(metadata, output.parent)
    output.mkdir(parents=True)
    closed = output / "closed.png"
    if body.drive.lower() == output.drive.lower():
        os.link(body, closed)
    else:
        shutil.copyfile(body, closed)
    Image.new("RGBA", size).save(output / "used.png")
    metadata.update(original=str(body), original_sha256=digest, size=list(size),
                    entire_figure_generated_raster_not_native_geometry=True,
                    closed_PNG_bytes_and_alpha_exact_original=True,
                    used_fully_departed_no_generated_redraw_or_visible_remnant=True,
                    no_crop_repaint_alpha_threshold_or_normalization=True,
                    accepted=False, installed=False, native_QA=False)
    (output / "provenance.json").write_text(json.dumps(metadata, indent=2))


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--body", type=Path, required=True)
    parser.add_argument("--terminal-reference", type=Path, required=True)
    parser.add_argument("--out", type=Path, required=True)
    args = parser.parse_args()
    prepare(args.body, args.terminal_reference, args.out)
