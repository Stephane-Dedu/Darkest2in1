"""Offline render contract; python -m unittest discover -s tests -p scenery_camera_test.py."""
import sys
import unittest
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parents[1] / 'tools'))


class SceneryCameraTest(unittest.TestCase):
    def test_camera_matches_hero_stage_after_runtime_crop(self):
        from scenery_camera import settings, project
        for corridor in (False, True):
            view = settings(corridor)
            for x in (-8, -4, 0, 4, 8):
                feet = project(view, (x, 0, 0))
                head = project(view, (x, 0, 4))
                zoom = 1.08 if corridor else 1
                self.assertAlmostEqual(680, feet[1], places=3)
                self.assertAlmostEqual(400, (feet[1]-head[1])*zoom, places=3)
            self.assertLess(view['vertical_fov'], 20)

    def test_room_and_corridor_share_ground_perspective(self):
        from scenery_camera import settings, project
        room, corridor = settings(False), settings(True)
        for depth in (-1, 0, 2, 5, 10):
            a = project(room, (0, depth, 0))[1]
            b = project(corridor, (0, depth, 0))[1]
            self.assertAlmostEqual(a, 680 + (b-680)*1.08, places=3)
        self.assertGreater(project(room, (0, -1, 0))[1], 680)
        self.assertLess(project(room, (0, 5, 0))[1], 680)


if __name__ == '__main__':
    unittest.main()
