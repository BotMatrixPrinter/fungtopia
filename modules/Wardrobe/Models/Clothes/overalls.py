# Overalls (Bottom, starter; pairs with the Overalls Top): denim overalls, a band round the body's lower edge with a stitched pocket and legs round the leg tubes down to just above the feet, with rolled-up cuffs.
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
for body in bodies():
    cloth(body, denim, BODY_BOTTOM, BAND_TOP, out=BOTTOM_OUT)
    ring(body, denim, BAND_TOP - 0.004, 0.011, out=BOTTOM_OUT)
    z = at(0.15)
    rot = body_frame(35, z)
    p, n = body_point(35, z, BOTTOM_OUT + 0.006)
    body.box(denim, p, (0.034, 0.026, 0.008), rotation=rot)
    body.torus(stitch, p + n * 0.008, 0.022, 0.004, rotation=rot, scale=(1.2, 0.95))
    body.build()
for leg in legs():
    leg_tube(leg, denim, 0.15)
    leg_ring(leg, cuff, 0.158, 0.017)
    leg.build()
export(__file__)
