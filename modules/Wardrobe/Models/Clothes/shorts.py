# Shorts (Bottom, starter): chunky sky-blue shorts with a rolled hem.
# Run from the repository root (writes shorts.glb next to this script):
#   blender -b --factory-startup --python modules/Wardrobe/Models/Clothes/shorts.py
# Shapes and rig sizes: ../kit.py.
import math
import os
import sys

sys.dont_write_bytecode = True  # no __pycache__ next to kit.py
sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), ".."))
from kit import *  # noqa: E402,F401,F403

reset()
blue = material("Cloth", (0.45, 0.68, 0.95))
hem = material("Hem", (0.35, 0.55, 0.85))
body = Part("Body")
hips(body, blue)
body.torus(hem, (0, 0, 0.4), on_chest(90, 0.4).x + 0.016, 0.018, scale=(1, CHEST_R[1] / CHEST_R[0]))
body.build()
for name in ("LegL", "LegR"):
    leg = Part(name)
    leg_tube(leg, blue, HIP_Z + 0.04, 0.2, 0.088, flare=0.012)
    leg.torus(hem, (leg.pivot.x, 0, 0.2), 0.1, 0.018, segments=24)
    leg.build()
export(__file__)
