import json
from pathlib import Path
import tempfile
import unittest

from PIL import Image, ImageDraw

from assemble_open_grave import assemble


class OpenGraveAssemblyGuards(unittest.TestCase):
    def inputs(self, root):
        marker = Image.new('RGBA', (16, 16))
        ImageDraw.Draw(marker).rectangle((4, 2, 6, 13), fill=(42, 36, 30, 255))
        marker.save(root / 'marker.png')
        closed = Image.new('RGBA', (32, 32))
        closed.putpixel((0, 0), (15, 15, 15, 1))
        ImageDraw.Draw(closed).rectangle((8, 18, 27, 30), fill=(61, 47, 34, 255))
        ImageDraw.Draw(closed).rectangle((22, 20, 25, 23), fill=(200, 190, 160, 255))
        closed.save(root / 'closed.png')
        used = closed.copy()
        ImageDraw.Draw(used).rectangle((22, 20, 25, 23), fill=(61, 47, 34, 255))
        used.save(root / 'edit.png')
        mask = Image.new('L', (32, 32))
        ImageDraw.Draw(mask).rectangle((22, 20, 25, 23), fill=255)
        mask.save(root / 'mask.png')
        return {'native_marker': 'marker.png', 'native_offset': [0, 0], 'canvas': [40, 40],
                'component_closed': 'closed.png', 'component_edit': 'edit.png',
                'state_mask': 'mask.png', 'component_crop': [8, 18, 28, 31],
                'component_width': 20, 'component_offset': [14, 24]}

    def test_refuses_component_that_extends_below_canvas(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            spec = self.inputs(root)
            # The real rejected study included distant alpha1 speckles in its
            # crop, making the component too tall and hiding it below canvas.
            spec['component_crop'] = [0, 0, 32, 32]
            path = root / 'spec.json'
            path.write_text(json.dumps(spec))
            with self.assertRaisesRegex(ValueError, 'placement clips'):
                assemble(path, root / 'result')
            self.assertFalse((root / 'result').exists())

    def test_refuses_patch_that_alters_native_marker_pixels(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            spec = self.inputs(root)
            spec['native_offset'] = [24, 24]
            path = root / 'spec.json'
            path.write_text(json.dumps(spec))
            with self.assertRaisesRegex(ValueError, 'changes native marker paint'):
                assemble(path, root / 'result')
            self.assertFalse((root / 'result' / 'closed.png').exists())


if __name__ == '__main__':
    unittest.main()
