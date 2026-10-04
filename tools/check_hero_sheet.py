"""Read-only occlusion check against the owner's DD1 artwork and the character-sheet draw order.

This catches the specific opaque-frame-over-hero regression; it does not validate Unity rendering.
Run: python tools/check_hero_sheet.py [DD1 install]
"""
from pathlib import Path
import sys
from PIL import Image

install = Path(sys.argv[1]) if len(sys.argv) > 1 else Path(
    r"C:/Program Files (x86)/Steam/steamapps/common/DarkestDungeon"
)
source = (Path(__file__).resolve().parents[1] / "src/DarkestDungeon3/Ui/HeroSheet.cs").read_text(encoding="utf-8")
frames = Image.open(install / "shared/character/characterpanel_frames.png").convert("RGBA")
# Hero: sheet+(18,250), 220x450. Frames: sheet+(10,10).
alpha = frames.getchannel("A").crop((8, 240, 228, 690))
coverage = sum(alpha.histogram()[128:]) / (alpha.width * alpha.height)
hero_draw = source.index("Art.DrawSprite(At(18, 250, 220, 450)")
frame_draw = source.index("GUI.DrawTexture(At(10, 10, 1395, 776), frames)")
if frame_draw > hero_draw and coverage > 0.95:
    print(f"FAIL: frames cover {coverage:.0%} of the hero area after the hero is drawn")
    sys.exit(1)
print(f"PASS: hero is above the frames ({coverage:.0%} coverage underneath)")
