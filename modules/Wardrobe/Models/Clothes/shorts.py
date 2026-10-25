# Shorts (Bottom, starter): chunky sky-blue shorts, a short band round the lower triangle with a rolled hem.
# Run from the repository root (writes shorts.glb next to this script):
#   blender -b --factory-startup --python modules/Wardrobe/Models/Clothes/shorts.py
# Shapes and rig sizes: ../kit.py.
import math
import os
import sys

sys.dont_write_bytecode = True  # no __pycache__ next to kit.py
sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), ".."))
from kit import *  # noqa: E402,F401,F403

reset()
blue = material("Cloth", (0.45, 0.68, 0.95))
hem = material("Hem", (0.35, 0.55, 0.85))
body = Part("Body")
cloth(body, blue, 0.2, BAND_TOP, out=BOTTOM_OUT)
ring(body, blue, BAND_TOP - 0.004, 0.014, out=BOTTOM_OUT)
ring(body, hem, 0.205, 0.02, out=BOTTOM_OUT)
body.build()
export(__file__)
