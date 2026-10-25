# Sneakers (Shoes, starter): chunky white-and-red sneakers over the round feet, with fat soles and laces.
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
    c = shoe(leg, white, sole)
    # A red toe cap and a red band round the heel.
    leg.sphere(red, c + Vector((0, 0.07, -0.012)), (0.075, 0.07, 0.05), segments=16, rings=8, cut=((0, 0, 0.032), (0, 0, -1)))
    leg.torus(red, c + Vector((0, 0, -0.012)), 0.102, 0.012, arc=0.5, segments=24, scale=(1.33, 1),
              rotation=Matrix.Rotation(math.radians(-90), 4, "Z"))
    for dy in (0.03, 0.06):
        z = c.z + 0.064 * math.sqrt(1 - (dy / 0.135) ** 2) - 0.012
        leg.capsule(lace, c + Vector((-0.035, dy, z - c.z)), c + Vector((0.035, dy, z - c.z)), 0.007, segments=6)
    leg.build()
export(__file__)
