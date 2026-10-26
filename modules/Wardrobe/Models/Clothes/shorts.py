# Shorts (Bottom, starter): chunky sky-blue shorts, a band round the body's lower edge and short legs round the tops of the leg tubes, with rolled hems.
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
for body in bodies():
    cloth(body, blue, BODY_BOTTOM, BAND_TOP, out=BOTTOM_OUT)
    ring(body, blue, BAND_TOP - 0.004, 0.011, out=BOTTOM_OUT)
    body.build()
for leg in legs():
    leg_tube(leg, blue, 0.26)
    leg_ring(leg, hem, 0.268, 0.013)
    leg.build()
export(__file__)
