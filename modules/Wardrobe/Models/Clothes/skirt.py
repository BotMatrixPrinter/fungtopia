# Skirt (Bottom, girl, shop): a pleated pink skirt flaring round the lower triangle down past its bottom edge, with a cream waistband.
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
top, bottom = BAND_TOP, BODY_BOTTOM - 0.005
r_top, r_bottom = surface_r(top) + BOTTOM_OUT, BODY_R0 + 0.045
body.lathe(pink, [(r_top + (r_bottom - r_top) * k / 6, top + (bottom - top) * k / 6) for k in range(7)][::-1])
for k in range(16):
    t = k / 16 * 2 * math.pi
    d = Vector((math.sin(t), math.cos(t), 0))
    body.capsule(pleat, d * (r_top + 0.004) + Vector((0, 0, top - 0.02)), d * (r_bottom + 0.004) + Vector((0, 0, bottom + 0.008)),
                 0.01, segments=8)
body.torus(pink, (0, 0, bottom), r_bottom, 0.014, segments=48)
ring(body, band, top - 0.004, 0.02, out=BOTTOM_OUT)
body.build()
export(__file__)
