# Overalls (Bottom, starter; pairs with the Overalls Top): denim overalls, a waist band round the torso's bottom with a stitched pocket, and a leg round each leg down to the ankle with a rolled-up cuff.
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
waist_band(body, denim)
ring(body, denim, BAND_TOP - 0.004, 0.014, out=BOTTOM_OUT)
rot = body_frame(40, 0.975)
p, n = body_point(40, 0.975, BOTTOM_OUT + 0.008)
body.box(denim, p, (0.04, 0.03, 0.01), rotation=rot)
body.torus(stitch, p + n * 0.01, 0.026, 0.005, rotation=rot, scale=(1.2, 0.95))
body.build()
for name in ("LegL", "LegR"):
    leg = Part(name)
    leg_tube(leg, denim, TROUSER_HEM, 0.95)
    leg_tube(leg, cuff, TROUSER_HEM - 0.005, TROUSER_HEM + 0.06, out=LEG_OUT + 0.012)
    leg_cuff(leg, cuff, TROUSER_HEM + 0.06, 0.012, out=LEG_OUT + 0.008)
    leg.build()
export(__file__)
