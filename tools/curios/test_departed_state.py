"""Check legitimate departure without accepting missing ordinary artwork."""
import contextlib
import hashlib
import io
import json
from pathlib import Path
import tempfile
import unittest
from unittest import mock

from PIL import Image, ImageDraw

import departed_state
from fit_state_pair import fit
from catalog_quality import audit_pair


class DepartedStateTests(unittest.TestCase):
    def setUp(self):
        self.temporary = tempfile.TemporaryDirectory()
        self.addCleanup(self.temporary.cleanup)
        self.root = Path(self.temporary.name)
        self.visible = Image.new("RGBA", (64, 64))
        ImageDraw.Draw(self.visible).rectangle((16, 16, 48, 55), fill=(70, 90, 100, 255))
        self.empty = Image.new("RGBA", (64, 64))
        self.visible.save(self.root / "closed.png")
        self.empty.save(self.root / "used.png")
        self.empty.save(self.root / "terminal-reference.png")
        digest = hashlib.sha256((self.root / "terminal-reference.png").read_bytes()).hexdigest()
        # Replace only reference digests so the tests need no private game files.
        patch = mock.patch.dict(departed_state.APPROVED_REFERENCES,
                                {key: digest for key in departed_state.APPROVED_REFERENCES})
        patch.start()
        self.addCleanup(patch.stop)
        self.spec = {
            "id": "com_millerwife_curio_design", "pixel_height": 128, "display_width": 200,
            "used_state_semantics": departed_state.SEMANTICS,
            "departure_proof": {"curio_id": "com_millerwife_curio", "animation": "investigated",
                                "reference_file": "terminal-reference.png", "reference_sha256": digest},
            "states": {state: {"file": state + ".png", "foot": [32, 56], "body_landmark_y": 16}
                       for state in ("closed", "used")},
        }

    def fitted(self, name="pair"):
        spec = self.root / (name + ".json")
        spec.write_text(json.dumps(self.spec))
        output = self.root / name
        with contextlib.redirect_stdout(io.StringIO()):
            fit(spec, output)
        return output

    def test_three_reviewed_figures_keep_visible_closed_and_empty_used_with_one_anchor(self):
        for identity in departed_state.APPROVED_REFERENCES:
            with self.subTest(identity=identity):
                self.spec["id"] = identity + "_design"
                self.spec["departure_proof"]["curio_id"] = identity
                output = self.fitted(identity)
                pair = audit_pair(output)
                with Image.open(pair["states"]["closed"]["file"]) as closed:
                    self.assertEqual(closed.getchannel("A").getextrema()[1], 255)
                with Image.open(pair["states"]["used"]["file"]) as used:
                    self.assertEqual(used.getchannel("A").getextrema(), (0, 0))
                metadata = json.loads((output / "provenance.json").read_text())
                self.assertEqual(metadata["states"]["closed"]["registered_foot"],
                                 metadata["states"]["used"]["registered_foot"])
                self.assertEqual(metadata["states"]["closed"]["uniform_scale"],
                                 metadata["states"]["used"]["uniform_scale"])

    def test_ordinary_empty_used_is_still_rejected(self):
        self.spec.pop("used_state_semantics")
        self.spec.pop("departure_proof")
        with self.assertRaisesRegex(ValueError, "Empty source sprite"):
            self.fitted()

    def test_even_source_proof_never_allows_empty_closed(self):
        self.empty.save(self.root / "closed.png")
        with self.assertRaisesRegex(ValueError, "Empty source sprite"):
            self.fitted()

    def test_protected_owner_and_unknown_identity_cannot_use_the_policy(self):
        for identity in ("faded_memory", "unknown_curio"):
            self.spec["id"] = identity + "_design"
            self.spec["departure_proof"]["curio_id"] = identity
            with self.subTest(identity=identity), self.assertRaisesRegex(ValueError, "Only the three"):
                self.fitted(identity)

    def test_missing_proof_wrong_animation_and_changed_reference_are_rejected(self):
        proof = self.spec.pop("departure_proof")
        with self.assertRaises(ValueError):
            self.fitted("missing")
        self.spec["departure_proof"] = proof
        proof["animation"] = "investigate"
        with self.assertRaisesRegex(ValueError, "terminal reference"):
            self.fitted("not_terminal")
        proof["animation"] = "investigated"
        self.visible.save(self.root / "terminal-reference.png")
        with self.assertRaisesRegex(ValueError, "reference changed"):
            self.fitted("changed")

    def test_visible_remnant_and_virtual_ground_drift_are_rejected(self):
        self.visible.save(self.root / "used.png")
        with self.assertRaisesRegex(ValueError, "completely transparent"):
            self.fitted("remnant")
        self.empty.save(self.root / "used.png")
        self.spec["states"]["used"]["foot"][0] += 1
        with self.assertRaisesRegex(ValueError, "virtual ground"):
            self.fitted("drift")

    def test_catalog_rejects_blank_ordinary_state_even_with_updated_hash(self):
        self.spec.pop("used_state_semantics")
        self.spec.pop("departure_proof")
        self.visible.save(self.root / "used.png")
        output = self.fitted()
        metadata = json.loads((output / "provenance.json").read_text())
        used = output / (self.spec["id"] + "-used.png")
        Image.new("RGBA", tuple(metadata["size"])).save(used)
        metadata["states"]["used"]["output_sha256"] = hashlib.sha256(used.read_bytes()).hexdigest()
        (output / "provenance.json").write_text(json.dumps(metadata))
        with self.assertRaisesRegex(ValueError, "Empty source sprite"):
            audit_pair(output)
