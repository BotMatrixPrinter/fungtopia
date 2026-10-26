# Sneakers (Shoes, starter): chunky white-and-red sneakers over the rounded feet, with fat soles and laces.
# Run from the repository root (writes sneakers.glb next to this script):
#   blender -b --factory-startup --python modules/Wardrobe/Models/Clothes/sneakers.py
# Shapes and rig sizes: ../kit.py.
import math
import os
import sys

sys.dont_write_bytecode = True  # no __pycache__ next to kit.py
sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), ".."))
from kit import *  # noqa: E402,F401,F403

reset()
white = material("Cloth", (1.0, 0.98, 0.95))
red = material("Accent", (0.92, 0.3, 0.32))
sole = material("Sole", (0.95, 0.85, 0.7))
lace = material("Lace", (0.3, 0.25, 0.3))
for leg in legs():
    SOLE = 0.026
    c, size = shoe(leg, white, sole, sole=SOLE)
    # A red toe cap and a red band round the heel.
    leg.sphere(red, c, (size[0] + 0.004, size[1] + 0.004, size[2] + 0.004), segments=32, rings=16,
               cut=((0, 0, SOLE), (0, 0, -1)), keep=((0, c.y + size[1] * 0.55, 0), (0, -1, 0)))
    dz = -0.01
    k = math.sqrt(1 - (dz / size[2]) ** 2)
    leg.torus(red, c + Vector((0, 0, dz)), 1.0, 0.01, arc=0.5, segments=32, scale=(size[1] * k + 0.001, size[0] * k + 0.001),
              rotation=Matrix.Rotation(math.radians(-90), 4, "Z"))
    for dy in (0.045, 0.07):
        z = c.z + size[2] * math.sqrt(1 - (dy / size[1]) ** 2) - 0.004
        leg.capsule(lace, Vector((c.x - 0.026, c.y + dy, z)), Vector((c.x + 0.026, c.y + dy, z)), 0.0055, segments=6)
    leg.build()
export(__file__)
