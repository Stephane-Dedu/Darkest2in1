"""Small artifact-level checks for orientation, alpha and state registration."""
import contextlib
import io
import json
from pathlib import Path
import tempfile
import unittest

from PIL import Image, ImageDraw

from extract_reference_states import state_image
from fit_state_pair import fit


class AtlasOrientationTests(unittest.TestCase):
    def test_rotated_region_restores_upright_paint_and_trim_offset(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            original = Image.new('RGBA', (2, 3))
            colors = [(255, 0, 0, 255), (0, 255, 0, 255), (0, 0, 255, 255),
                      (255, 255, 0, 255), (255, 0, 255, 255), (0, 255, 255, 255)]
            original.putdata(colors)
            packed = Image.new('RGBA', (8, 8))
            packed.paste(original.transpose(Image.Transpose.ROTATE_90), (1, 2))
            packed.save(root / 'page.png')
            restored = state_image({'page': root / 'page.png', 'xy': (1, 2), 'size': (2, 3),
                                    'orig': (5, 7), 'offset': (1, 2), 'rotate': True})
            self.assertEqual(restored.size, (5, 7))
            self.assertEqual(restored.crop((1, 2, 3, 5)).tobytes(), bytes(c for pixel in colors for c in pixel))
            self.assertEqual(restored.getpixel((0, 0)), (0, 0, 0, 0))


class PairRegistrationTests(unittest.TestCase):
    def write_pair(self, root, zero_span=False):
        for state, size in [('closed', (64, 64)), ('used', (128, 128))]:
            image = Image.new('RGBA', size)
            scale = size[0] / 64
            draw = ImageDraw.Draw(image)
            draw.rectangle(tuple(round(v * scale) for v in (10, 20, 50, 49)), fill=(120, 90, 60, 255))
            draw.rectangle(tuple(round(v * scale) for v in (9, 19, 51, 20)), fill=(120, 90, 60, 80))
            image.save(root / (state + '.png'))
        spec = {'id': 'fixture', 'pixel_span': 128, 'display_width': 200, 'states': {
            'closed': {'file': 'closed.png', 'foot': [30, 50], 'scale_landmarks': [[10, 30], [10 if zero_span else 50, 30]]},
            'used': {'file': 'used.png', 'foot': [60, 100], 'scale_landmarks': [[20, 60], [100, 60]]}}}
        path = root / 'spec.json'
        path.write_text(json.dumps(spec))
        return path

    def test_horizontal_body_span_preserves_alpha_and_draw_contract(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            spec = self.write_pair(root)
            output = root / 'fitted'
            with contextlib.redirect_stdout(io.StringIO()):
                fit(spec, output)
            doc = json.loads((output / 'provenance.json').read_text())
            self.assertEqual(doc['foot_anchor'], [doc['size'][0] / 2, doc['size'][1] - 16])
            for state in ('closed', 'used'):
                with Image.open(output / ('fixture-' + state + '.png')) as image:
                    self.assertEqual(list(image.size), doc['size'])
                    self.assertEqual(image.mode, 'RGBA')
                    self.assertTrue(any(image.getchannel('A').histogram()[1:255]))
                self.assertTrue(all(abs(a - b) <= .5 for a, b in zip(doc['foot_anchor'], doc['states'][state]['registered_foot'])))
            # The texture body used twice the source resolution; its native draw
            # scale must remain equal instead of doubling after the state swap.
            self.assertAlmostEqual(doc['states']['closed']['uniform_scale'], doc['states']['used']['uniform_scale'] * 2)

    def test_empty_calibration_span_is_rejected_before_outputs(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            with self.assertRaisesRegex(ValueError, 'Empty reviewed scale span'):
                fit(self.write_pair(root, zero_span=True), root / 'fitted')
            self.assertFalse((root / 'fitted').exists())

    def test_existing_output_is_never_overwritten(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            output = root / 'fitted'
            output.mkdir()
            sentinel = output / 'keep.txt'
            sentinel.write_text('existing art')
            with self.assertRaisesRegex(ValueError, 'new output folder'):
                fit(self.write_pair(root), output)
            self.assertEqual(sentinel.read_text(), 'existing art')


if __name__ == '__main__':
    unittest.main()
