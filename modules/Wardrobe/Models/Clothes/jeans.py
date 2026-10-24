# Jeans (Bottom, shop): blue jeans down to the shoes, with gold seams and a brown belt.
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
body = Part("Body")
hips(body, denim, top_z=0.4)
body.torus(belt, (0, 0, 0.395), on_chest(90, 0.395).x + 0.028, 0.02, scale=(1, CHEST_R[1] / CHEST_R[0]))
body.box(buckle, on_chest(0, 0.395, 0.045), (0.035, 0.012, 0.025))
body.build()
for name in ("LegL", "LegR"):
    leg = Part(name)
    x = leg.pivot.x
    leg_tube(leg, denim, HIP_Z + 0.04, 0.1, 0.08, flare=0.012)
    side = 1 if x > 0 else -1
    leg.capsule(seam, (x + side * 0.083, 0, HIP_Z + 0.02), (x + side * 0.091, 0, 0.11), 0.006, segments=6)
    leg.build()
export(__file__)
