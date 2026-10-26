# Jeans (Bottom, shop): blue jeans, a waist band round the torso's bottom with a brown belt and a gold buckle, and a leg round each leg down to the ankle with gold seams and a rolled cuff.
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
waist_band(body, denim)
ring(body, belt, BAND_TOP - 0.016, 0.016, out=BOTTOM_OUT)
body.box(buckle, body_point(0, BAND_TOP - 0.016, BOTTOM_OUT + 0.034)[0], (0.032, 0.024, 0.01),
         rotation=body_frame(0, BAND_TOP - 0.016))
body.build()
for name in ("LegL", "LegR"):
    leg = Part(name)
    side = 1 if leg.pivot.x > 0 else -1
    leg_tube(leg, denim, TROUSER_HEM, 0.95)
    leg_cuff(leg, denim, TROUSER_HEM + 0.022, 0.018)
    leg_seam(leg, seam, side * 90, TROUSER_HEM + 0.05, 0.9, 0.005)
    leg.build()
export(__file__)
