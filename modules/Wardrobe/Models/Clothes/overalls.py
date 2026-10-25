# Overalls (Bottom, starter; pairs with the Overalls Top): denim overalls, the band round the lower triangle down to its bottom edge, with a rolled-up cuff and a stitched pocket.
# Run from the repository root (writes overalls.glb next to this script):
#   blender -b --factory-startup --python modules/Wardrobe/Models/Clothes/overalls.py
# Shapes and rig sizes: ../kit.py.
import math
import os
import sys

sys.dont_write_bytecode = True  # no __pycache__ next to kit.py
sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), ".."))
from kit import *  # noqa: E402,F401,F403

reset()
denim = material("Denim", (0.32, 0.5, 0.82))
cuff = material("Cuff", (0.55, 0.7, 0.95))
stitch = material("Stitch", (1.0, 0.8, 0.3))
body = Part("Body")
cloth(body, denim, BODY_BOTTOM, BAND_TOP, out=BOTTOM_OUT)
ring(body, denim, BAND_TOP - 0.004, 0.014, out=BOTTOM_OUT)
ring(body, cuff, BODY_BOTTOM + 0.035, 0.026, out=BOTTOM_OUT)
rot = body_frame(35, 0.21)
p, n = body_point(35, 0.21, BOTTOM_OUT + 0.008)
body.box(denim, p, (0.045, 0.035, 0.01), rotation=rot)
body.torus(stitch, p + n * 0.01, 0.03, 0.005, rotation=rot, scale=(1.2, 0.95))
body.build()
export(__file__)
