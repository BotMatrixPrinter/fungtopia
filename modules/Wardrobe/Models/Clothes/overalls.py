# Overalls (Bottom, starter; pairs with the Overalls Top): denim overalls trousers with rolled-up cuffs and a stitched pocket.
# Run from the repository root (writes overalls.glb next to this script):
#   blender -b --factory-startup --python modules/Wardrobe/Models/Clothes/overalls.py
# Shapes and rig sizes: ../kit.py.
import math
import os
import sys

sys.dont_write_bytecode = True  # no __pycache__ next to kit.py
sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), ".."))
from kit import *  # noqa: E402,F401,F403

reset()
denim = material("Denim", (0.32, 0.5, 0.82))
cuff = material("Cuff", (0.55, 0.7, 0.95))
stitch = material("Stitch", (1.0, 0.8, 0.3))
body = Part("Body")
hips(body, denim, top_z=0.42, puff=0.035)
body.box(denim, on_chest(0, 0.36, 0.04), (0.06, 0.015, 0.04))
body.torus(stitch, on_chest(0, 0.36, 0.058), 0.04, 0.006, rotation=Matrix.Rotation(math.radians(90), 4, "X"), scale=(1.3, 1))
body.build()
for name in ("LegL", "LegR"):
    leg = Part(name)
    leg_tube(leg, denim, HIP_Z + 0.04, 0.14, 0.085, flare=0.01)
    leg.torus(cuff, (leg.pivot.x, 0, 0.15), 0.098, 0.026, segments=24)
    leg.build()
export(__file__)
