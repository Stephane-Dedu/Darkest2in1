"""Read-only PNG-to-RGBA stream for the CPU timing harness; no files are written."""
import struct
import sys
from PIL import Image
image = Image.open(sys.argv[1]).convert("RGBA")
sys.stdout.buffer.write(struct.pack("<II", *image.size))
sys.stdout.buffer.write(image.tobytes())
