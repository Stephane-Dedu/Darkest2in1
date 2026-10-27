import json
from pathlib import Path
import tempfile
import unittest

import numpy as np
from PIL import Image, ImageDraw

from compose_local_component import compose_component
from compose_state_patch import compose


class ExplicitLocalSurfaceTests(unittest.TestCase):
    def inputs(self, root):
        closed = Image.new('RGBA', (32, 32))
        ImageDraw.Draw(closed).rectangle((8, 8, 23, 23), fill=(45, 33, 27, 255))
        closed.save(root / 'closed.png')
        edit = Image.new('RGBA', (33, 32), (85, 76, 54, 255))
        edit.save(root / 'edit.png')
        domain = Image.new('L', (32, 32))
        ImageDraw.Draw(domain).rectangle((10, 10, 13, 13), fill=255)
        domain.save(root / 'domain.png')
        fixed = Image.new('L', (32, 32))
        ImageDraw.Draw(fixed).rectangle((16, 0, 31, 31), fill=255)
        fixed.save(root / 'fixed.png')
        spec = {'closed': 'closed.png', 'component': 'edit.png',
                'domain': 'domain.png', 'protected': 'fixed.png',
                'crop': [10, 10, 14, 14], 'offset': [10, 10]}
        path = root / 'spec.json'
        path.write_text(json.dumps(spec))
        return path

    def test_wrong_canvas_stays_rejected_while_explicit_local_crop_preserves_body(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            path = self.inputs(root)
            with self.assertRaises(ValueError):
                compose(root / 'closed.png', root / 'edit.png', root / 'domain.png', root / 'wrong')
            record = compose_component(path, root / 'local')
            before = np.asarray(Image.open(root / 'closed.png'))
            after = np.asarray(Image.open(root / 'local/pair/used.png'))
            mask = np.asarray(Image.open(root / 'domain.png')) > 0
            self.assertTrue(np.array_equal(before[~mask], after[~mask]))
            self.assertEqual(16, record['changed_pixels'])
            self.assertFalse(record['component_resampled'])
            self.assertEqual((33, 32), record['component_source_size'])

    def test_local_crop_cannot_overwrite_protected_bone_or_body_paint(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            path = self.inputs(root)
            fixed = Image.open(root / 'fixed.png')
            fixed.putpixel((11, 11), 255)
            fixed.save(root / 'fixed.png')
            with self.assertRaisesRegex(ValueError, 'protected source pixels'):
                compose_component(path, root / 'result')
            self.assertFalse((root / 'result').exists())


if __name__ == '__main__':
    unittest.main()
