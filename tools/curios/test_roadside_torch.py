import json
from pathlib import Path
import tempfile
import unittest

import numpy as np
from PIL import Image, ImageDraw

from assemble_roadside_torch import assemble


class RoadsideTorchTests(unittest.TestCase):
    def fixture(self, root):
        body = Image.new('RGBA', (64, 64), (17, 29, 31, 0))
        ImageDraw.Draw(body).rectangle((10, 4, 13, 58), fill=(53, 37, 19, 255))
        body.save(root / 'body.png')
        torch = Image.new('RGBA', (16, 16))
        ImageDraw.Draw(torch).rectangle((4, 3, 10, 12), fill=(173, 142, 87, 255))
        torch.save(root / 'torch.png')
        spec = {'native_body': 'body.png', 'torch_component': 'torch.png', 'component_crop': [2, 2, 14, 14],
                'component_uniform_scale': 1, 'component_offset': [25, 15]}
        return body, spec

    def test_taking_component_restores_exact_native_rgba_and_keeps_body_fixed(self):
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            body, spec = self.fixture(root)
            (root / 'spec.json').write_text(json.dumps(spec))
            record = assemble(root / 'spec.json', root / 'pair')
            used = np.asarray(Image.open(root / 'pair/used.png'))
            closed = np.asarray(Image.open(root / 'pair/closed.png'))
            self.assertTrue(np.array_equal(used, np.asarray(body)))
            self.assertTrue(np.array_equal(closed[:, :20], np.asarray(body)[:, :20]))
            self.assertFalse(np.array_equal(closed, used))
            self.assertTrue(record['used_RGBA_equals_native_reference'])
            self.assertFalse(record['native_body_registration_or_repaint'])

    def test_rejects_visible_crop_loss_and_excessive_placement_domain(self):
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            _, spec = self.fixture(root)
            spec['component_crop'] = [8, 2, 14, 14]
            (root / 'spec.json').write_text(json.dumps(spec))
            with self.assertRaisesRegex(ValueError, 'clip visible'):
                assemble(root / 'spec.json', root / 'clipped')
            self.assertFalse((root / 'clipped').exists())
            spec.update(component_crop=[0, 0, 16, 16], component_uniform_scale=2, component_offset=[20, 15])
            (root / 'spec.json').write_text(json.dumps(spec))
            with self.assertRaisesRegex(ValueError, 'one fifth'):
                assemble(root / 'spec.json', root / 'oversized')
            self.assertFalse((root / 'oversized').exists())


if __name__ == '__main__':
    unittest.main()
