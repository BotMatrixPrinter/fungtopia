# Jeans (Bottom, shop): blue jeans, a band round the body's lower edge and legs round the leg tubes down to just above the feet, with gold seams and a brown belt.
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
for body in bodies():
    cloth(body, denim, BODY_BOTTOM, BAND_TOP, out=BOTTOM_OUT)
    ring(body, belt, BAND_TOP - 0.012, 0.014, out=BOTTOM_OUT)
    body.box(buckle, body_point(0, BAND_TOP - 0.012, BOTTOM_OUT + 0.028)[0], (0.026, 0.019, 0.009),
             rotation=body_frame(0, BAND_TOP - 0.012))
    for a in (-90, 90):
        on_body(body, seam, [(a, BAND_TOP - 0.035), (a, (BAND_TOP + BODY_BOTTOM) / 2), (a, widest()[1])], 0.005,
                out=BOTTOM_OUT)
    body.build()
for leg in legs():
    leg_tube(leg, denim, 0.135)
    leg_ring(leg, denim, 0.142, 0.009)
    # The gold seam down the outside of the leg.
    side = 1 if leg.pivot.x > 0 else -1
    r = LEG_R + LEG_OUT + 0.004
    leg.capsule(seam, (leg.pivot.x + side * r, 0, 0.16), (leg.pivot.x + side * r, 0, LEG_TOP - 0.01), 0.005, segments=8)
    leg.build()
export(__file__)
