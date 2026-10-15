"""Private composition and crop contracts, using synthetic fixtures rather than game art."""
import json
import sys
import tempfile
import unittest
from pathlib import Path

import numpy as np
from PIL import Image

sys.path.insert(0, str(Path(__file__).resolve().parents[1] / "tools"))
from compose_corridor_depth import SIZE, BANDS, compose, digest, prepare_layer
from review_corridor_depth import crop, frame


class CorridorDepthTest(unittest.TestCase):
    def test_native_ground_is_opaque_under_near_wall_bases_but_keeps_a_soft_horizon(self):
        with tempfile.TemporaryDirectory(prefix="dd3-depth-") as temporary:
            pixels=np.zeros((SIZE[1],SIZE[0],4),dtype=np.uint8)
            pixels[545:]=[70,50,30,255]
            path=Path(temporary)/"ground.png"
            Image.fromarray(pixels).save(path)
            result=np.asarray(prepare_layer(path,"ground","forest"))
            self.assertEqual(0,int(result[545,2048,3]))
            self.assertEqual(255,int(result[610,2048,3]))

    def test_selective_render_replaces_its_manifest_entries_without_losing_other_regions(self):
        from render_native_corridors import merge_entries
        city = {"short":"city-01","sha256":"old-city"}
        forest = {"short":"forest-01","sha256":"old-forest"}
        updated = {"short":"forest-01","sha256":"new-forest"}
        result = merge_entries([city,forest],[updated])
        self.assertEqual([city,updated],result)

    def test_discarded_tangle_foundations_cannot_occupy_a_visible_wall_span(self):
        from render_native_corridors import wall_asset
        self.assertFalse(wall_asset("forest_tile_hill_05",(20,10,50)))
        self.assertFalse(wall_asset("forest_tile_base_1x1",(10,10,10)))
        self.assertTrue(wall_asset("forest_tile_12",(22,20,30)))
        self.assertTrue(wall_asset("coast_tile_09",(20,11,30)))
        self.assertFalse(wall_asset("forest_node_hospital",(20,20,20)))

    def test_complete_plate_matches_depths_at_the_hall_centre_and_source_is_untouched(self):
        with tempfile.TemporaryDirectory(prefix="dd3-depth-") as temporary:
            root = Path(temporary)
            source, output = root / "source", root / "output"
            (source / "passes").mkdir(parents=True)
            name = "dd2_forest-corridor-01.png"
            Image.new("RGB", SIZE, (30,40,50)).save(source / name)
            Image.new("RGBA", SIZE, (40,50,60,80)).save(source / "passes/forest-01-far.png")
            foreground = np.zeros((SIZE[1],SIZE[0],4),dtype=np.uint8)
            foreground[:,1000:1400] = [20,60,100,255]
            foreground[660:] = [70,40,10,255]
            Image.fromarray(foreground).save(source / "passes/forest-01-wall.png")
            (source / "render-manifest.json").write_text(json.dumps({"files":[{
                "file":name,"short":"forest-01","region":"dd2_forest"}]}))
            before = {p.name:digest(p) for p in source.rglob("*") if p.is_file()}
            manifest = compose(source,output)
            after = {p.name:digest(p) for p in source.rglob("*") if p.is_file()}
            self.assertEqual(before,after)
            self.assertFalse(manifest["generated_art"])
            depths = [Image.open(output / f"{name[:-4]}.depth-{band}.png").convert("RGBA") for band in BANDS]
            for depth in depths:
                self.assertEqual(SIZE,depth.size)
            flat = crop(Image.open(output / name).convert("RGBA"),.5).convert("RGB")
            delta = np.abs(np.asarray(flat,dtype=np.int16)-np.asarray(frame(depths,.5),dtype=np.int16))
            self.assertLess(delta.mean(),.5)
            for progress in (0,.25,.5,.75,1):
                view = frame(depths,progress)
                self.assertEqual((1920,720),view.size)
            self.assertEqual(2,len(manifest["files"][0]["source_pass_sha256"]))

    def test_private_output_cannot_replace_sources_or_land_in_the_repository(self):
        source = Path(__file__).resolve().parents[1]
        with self.assertRaises(ValueError): compose(source,source)
        with tempfile.TemporaryDirectory(prefix="dd3-depth-") as temporary:
            with self.assertRaises(ValueError): compose(temporary,temporary)

    def test_invalid_pass_dimensions_are_rejected_before_composition(self):
        with tempfile.TemporaryDirectory(prefix="dd3-depth-") as temporary:
            root = Path(temporary)
            source = root / "source"
            (source / "passes").mkdir(parents=True)
            Image.new("RGB",SIZE).save(source / "dd2_city-corridor-01.png")
            Image.new("RGBA",(64,64)).save(source / "passes/city-01-wall.png")
            (source / "render-manifest.json").write_text(json.dumps({"files":[{
                "file":"dd2_city-corridor-01.png","short":"city-01","region":"dd2_city"}]}))
            with self.assertRaises(ValueError): compose(source,root / "output")


if __name__ == "__main__": unittest.main()
