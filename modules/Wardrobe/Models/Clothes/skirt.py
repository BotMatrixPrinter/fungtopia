# Skirt (Bottom, girl, shop): a pleated pink skirt from a cream waistband flaring over the upper legs, wide enough for them to swing inside it.
# Run from the repository root (writes skirt.glb next to this script):
#   blender -b --factory-startup --python modules/Wardrobe/Models/Clothes/skirt.py
# Shapes and rig sizes: ../kit.py.
import math
import os
import sys

sys.dont_write_bytecode = True  # no __pycache__ next to kit.py
sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), ".."))
from kit import *  # noqa: E402,F401,F403

reset()
pink = material("Cloth", (1.0, 0.62, 0.7))
pleat = material("Pleat", (0.92, 0.5, 0.6))
band = material("Band", (1.0, 0.94, 0.85))
body = Part("Body")
HEM = 0.62
rings = flared(BOTTOM_OUT, BAND_TOP, HEM, 0.1, stretch=1.8, power=2.8)
oval_lathe(body, pink, rings)
for k in range(16):
    on_skirt(body, pleat, rings, (k + 0.5) / 16 * 360, HEM + 0.02, BAND_TOP - 0.03, 0.007, out=-0.002)
hem_ring(body, pink, rings, HEM + 0.004, 0.014)
ring(body, band, BAND_TOP - 0.004, 0.02, out=BOTTOM_OUT)
body.build()
export(__file__)
