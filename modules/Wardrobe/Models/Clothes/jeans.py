# Jeans (Bottom, shop): blue jeans, the band round the lower triangle down to its bottom edge, with gold seams and a brown belt.
# Run from the repository root (writes jeans.glb next to this script):
#   blender -b --factory-startup --python modules/Wardrobe/Models/Clothes/jeans.py
# Shapes and rig sizes: ../kit.py.
import math
import os
import sys

sys.dont_write_bytecode = True  # no __pycache__ next to kit.py
sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), ".."))
from kit import *  # noqa: E402,F401,F403

reset()
denim = material("Denim", (0.25, 0.38, 0.68))
seam = material("Stitch", (0.95, 0.75, 0.35))
belt = material("Belt", (0.5, 0.3, 0.18))
buckle = material("Buckle", (1.0, 0.82, 0.33))
body = Part("Body")
cloth(body, denim, BODY_BOTTOM, BAND_TOP, out=BOTTOM_OUT)
ring(body, belt, BAND_TOP - 0.012, 0.018, out=BOTTOM_OUT)
body.box(buckle, body_point(0, BAND_TOP - 0.012, BOTTOM_OUT + 0.036)[0], (0.035, 0.025, 0.012), rotation=body_frame(0, BAND_TOP - 0.012))
for a in (-90, 90):
    on_body(body, seam, [(a, BAND_TOP - 0.04), (a, 0.2), (a, BODY_BOTTOM + 0.035)], 0.006, out=BOTTOM_OUT)
body.build()
export(__file__)
