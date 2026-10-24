# Sneakers (Shoes, starter): chunky white-and-red sneakers with fat soles and laces.
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
for name in ("LegL", "LegR"):
    leg = Part(name)
    x = leg.pivot.x
    shoe(leg, white, sole, size=(0.1, 0.14, 0.08))
    leg.sphere(red, (x, 0.12, 0.06), (0.07, 0.06, 0.05), segments=16, rings=8, cut=((x, 0, 0.04), (0, 0, -1)))
    leg.torus(red, (x, 0.0, 0.1), 0.075, 0.016, segments=24)
    for z, y in ((0.11, 0.09), (0.095, 0.115)):
        leg.capsule(lace, (x - 0.035, y, z), (x + 0.035, y, z), 0.008, segments=6)
    leg.build()
export(__file__)
