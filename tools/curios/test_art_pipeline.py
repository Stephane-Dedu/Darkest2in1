"""Small artifact-level checks for orientation, alpha and state registration."""
import contextlib
import hashlib
import io
import json
import os
from pathlib import Path
import tempfile
import unittest

from PIL import Image, ImageDraw

from extract_reference_states import state_image
from fit_state_pair import fit
from catalog_quality import catalog
from build_pack import build


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


class NativeDependencyTests(unittest.TestCase):
    def test_legacy_source_hashes_are_checked_outside_repository_cwd(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            Image.new('RGBA', (16, 16), (90, 70, 50, 255)).save(root / 'body.png')
            dependency = root / 'mesh.bin'
            dependency.write_bytes(b'private source fixture')
            repo = Path(__file__).resolve().parents[2]
            digest = lambda p: hashlib.sha256(p.read_bytes()).hexdigest()
            (root / 'native.json').write_text(json.dumps({'entries': [{
                'id': 'fixture', 'file': 'body.png', 'output_sha256': digest(root / 'body.png'),
                'files': [{'path': os.path.relpath(dependency, repo), 'sha256': digest(dependency)}]}]}))
            (root / 'inventory.json').write_text(json.dumps([
                {'id': 'fixture', 'name': 'Fixture', 'scope': 'base-runtime', 'region': 'ALL'}]))
            (root / 'selection.json').write_text(json.dumps({'pairs': [], 'native_studies': [str(root / 'native.json')]}))
            previous = Path.cwd()
            (root / 'other-cwd').mkdir()
            try:
                os.chdir(root / 'other-cwd')
                with contextlib.redirect_stdout(io.StringIO()):
                    result = catalog(root / 'inventory.json', root / 'selection.json', root / 'review')
            finally:
                os.chdir(previous)
            self.assertEqual(result['counts'], {'native_body_needs_used_state': 1})
            self.assertTrue((root / 'review/coverage.json').is_file())


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

    def test_wide_pair_cannot_exceed_the_runtime_decoder_pixel_budget(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            Image.new('RGBA', (1280, 800), (120, 90, 60, 255)).save(root / 'source.png')
            spec = {'id': 'wide_fixture', 'pixel_height': 1600, 'display_height': 400, 'states': {
                state: {'file': 'source.png', 'foot': [640, 799], 'body_landmark_y': 0}
                for state in ('closed', 'used')}}
            path = root / 'spec.json'
            path.write_text(json.dumps(spec))
            # Each side fits the decoder bounds, but their product exceeds4M.
            with self.assertRaisesRegex(ValueError, 'native preloader bounds'):
                fit(path, root / 'fitted')
            self.assertFalse((root / 'fitted').exists())

    def test_region_names_and_shared_art_do_not_invent_new_curio_identities(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            with contextlib.redirect_stdout(io.StringIO()):
                fit(self.write_pair(root), root / 'fitted')
            (root / 'inventory.json').write_text(json.dumps([
                {'id': 'fixture', 'name': 'Cabinet', 'scope': 'base-runtime', 'region': 'ALL'}]))
            (root / 'native.json').write_text(json.dumps({'entries': []}))
            selection = {'pairs': [str(root / 'fitted')], 'native_studies': [str(root / 'native.json')],
                         'region_art_overrides': {'fixture': {'dd2_forest': {'ArtId': 'fixture', 'DisplayName': 'Locked field cache'}}}}
            (root / 'selection.json').write_text(json.dumps(selection))
            with contextlib.redirect_stdout(io.StringIO()):
                build(root / 'inventory.json', root / 'selection.json', root / 'pack')
            document = json.loads((root / 'pack/manifest.json').read_text())
            self.assertEqual(document['Version'], 3)
            self.assertEqual(len(document['Artifacts']), 1)
            self.assertEqual([e['Id'] for e in document['Entries']], ['fixture'])
            self.assertEqual(document['Entries'][0]['Regions']['dd2_forest']['DisplayName'], 'Locked field cache')

    def regional_selection(self, root):
        spec_path = self.write_pair(root)
        with contextlib.redirect_stdout(io.StringIO()):
            fit(spec_path, root / 'default')
            spec = json.loads(spec_path.read_text())
            spec['id'] = 'fixture_sprawl'
            spec_path.write_text(json.dumps(spec))
            fit(spec_path, root / 'variant')
        (root / 'inventory.json').write_text(json.dumps([
            {'id': 'fixture', 'name': 'Bookshelf', 'scope': 'base-runtime', 'region': 'Ruins'}]))
        (root / 'native.json').write_text(json.dumps({'entries': []}))
        return {'pairs': [str(root / 'default')], 'native_studies': [str(root / 'native.json')],
                'regional_pairs': [{'curio_id': 'fixture', 'folder': str(root / 'variant')}],
                'region_art_overrides': {'fixture': {'dd2_city': {'ArtId': 'fixture_sprawl', 'DisplayName': 'Ruined archive'}}}}

    def test_distinct_region_body_keeps_the_classic_default_and_single_coverage_identity(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            selection = self.regional_selection(root)
            (root / 'selection.json').write_text(json.dumps(selection))
            with contextlib.redirect_stdout(io.StringIO()):
                build(root / 'inventory.json', root / 'selection.json', root / 'pack')
            document = json.loads((root / 'pack/manifest.json').read_text())
            self.assertEqual(document['Entries'], [{'Id': 'fixture', 'ArtId': 'fixture',
                'Regions': {'dd2_city': {'ArtId': 'fixture_sprawl', 'DisplayName': 'Ruined archive'}}}])
            self.assertEqual({a['Id'] for a in document['Artifacts']}, {'fixture', 'fixture_sprawl'})
            coverage = json.loads((root / 'pack/review/coverage.json').read_text())
            self.assertEqual(coverage['counts'], {'offline_pair_candidate': 1})
            self.assertEqual(len(coverage['regional_artifacts']), 1)
            self.assertEqual(coverage['regional_artifacts'][0]['curio_id'], 'fixture')

    def test_regional_art_cannot_escape_its_identity_or_bypass_pair_validation(self):
        for problem in ('protected_owner', 'orphan', 'unsafe_art_id', 'changed_pixels', 'wrong_default'):
            with self.subTest(problem=problem), tempfile.TemporaryDirectory() as directory:
                root = Path(directory)
                selection = self.regional_selection(root)
                if problem == 'protected_owner':
                    selection['regional_pairs'][0]['curio_id'] = 'faded_memory'
                elif problem == 'orphan':
                    selection['region_art_overrides'] = {}
                elif problem == 'unsafe_art_id':
                    path = root / 'variant/provenance.json'
                    metadata = json.loads(path.read_text()); metadata['id'] = '../escape'
                    path.write_text(json.dumps(metadata))
                elif problem == 'changed_pixels':
                    path = root / 'variant/fixture_sprawl-used.png'
                    with Image.open(path) as image:
                        image.putpixel((0, 0), (255, 0, 0, 255)); image.save(path)
                else:
                    inventory_path = root / 'inventory.json'
                    inventory = json.loads(inventory_path.read_text())
                    inventory[0]['sprite'] = 'fixture'
                    inventory.append(dict(inventory[0], id='other'))
                    inventory_path.write_text(json.dumps(inventory))
                    selection['aliases'] = {'other': 'fixture'}
                    selection['region_art_overrides']['other'] = selection['region_art_overrides'].pop('fixture')
                (root / 'selection.json').write_text(json.dumps(selection))
                with self.assertRaises(ValueError):
                    catalog(root / 'inventory.json', root / 'selection.json', root / 'review')
                self.assertFalse((root / 'review').exists())


if __name__ == '__main__':
    unittest.main()
