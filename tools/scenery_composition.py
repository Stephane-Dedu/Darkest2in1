"""Composition controls for the private native corridor renderer.

All distances are export units before the shared environment scale. These
controls keep the actor lane open without changing its camera or footline.
"""

PROFILE = "open-native-v21"
WALL_SCALE = {"city": .94, "farm": .76, "forest": .82, "coast": .86}
WALL_RETREAT = {"city": 2.0, "farm": 5.0, "forest": 5.0, "coast": 3.0}
INFILL_GAP = {"city": (18, 28), "farm": (14, 23), "coast": (18, 28)}
INFILL_DEPTH = {"city": (5, 8), "farm": (7, 10), "coast": (6, 9)}
LITTER_GAP = {"city": (12, 20), "farm": (11, 19), "coast": (14, 22)}
VERGE_SCALE = {"farm": (.36, .50), "forest": (.27, .40), "coast": (.30, .45)}
VERGE_GAP = (8, 15)
TREE_SCALE = .70
TREE_GAP = (10, 17)
TREE_DEPTH = (7, 11)
UNDERGROWTH_SCALE = (.09, .16)
UNDERGROWTH_GAP = (10, 18)
