# Skirt (Bottom, shop): a pleated pink skirt flaring over the legs with a cream waistband.
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
top, bottom = 0.42, HIP_Z - 0.1
skirt(body, pink, top, bottom, 0.255, 0.34, ruffle=False)
for k in range(14):
    t = k / 14 * 2 * math.pi
    a = Vector((math.sin(t) * 0.258, math.cos(t) * 0.258, top - 0.01))
    b = Vector((math.sin(t) * 0.343, math.cos(t) * 0.343, bottom + 0.005))
    body.capsule(pleat, a, b, 0.012, segments=8)
body.torus(band, (0, 0, top), 0.255, 0.026, segments=40)
body.build()
export(__file__)
