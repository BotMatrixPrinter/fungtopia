# Sneakers (Shoes, starter): chunky white-and-red sneakers over the rig's shoes, with fat soles and laces.
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
SIZE = (0.082, 0.132, 0.076)
for name in ("LegL", "LegR"):
    leg = Part(name)
    c = shoe(leg, white, sole, size=SIZE)
    # A red toe cap and a red band round the heel.
    leg.sphere(red, c + Vector((0, 0.064, -0.006)), (0.074, 0.074, 0.062), segments=20, rings=10,
               cut=((0, 0, 0.012), (0, 0, -1)))
    leg.torus(red, c + Vector((0, 0, -0.016)), 0.084, 0.011, arc=0.5, segments=24, scale=(1.55, 1),
              rotation=Matrix.Rotation(math.radians(-90), 4, "Z"))
    # Laces across the instep, in front of the leg.
    for dy in (0.05, 0.075):
        pts = []
        for k in range(7):
            dx = -0.036 + 0.072 * k / 6
            h = 1 - (dx / SIZE[0]) ** 2 - (dy / SIZE[1]) ** 2
            pts.append(c + Vector((dx, dy, SIZE[2] * math.sqrt(max(0.0, h)) + 0.003)))
        leg.tube(lace, pts, 0.006, ring=8)
    leg.build()
export(__file__)
