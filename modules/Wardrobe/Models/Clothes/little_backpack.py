# Little Backpack (Back, shop): a small round orange backpack with a flap, a front pocket and straps over the shoulders.
# Run from the repository root (writes little_backpack.glb next to this script):
#   blender -b --factory-startup --python modules/Wardrobe/Models/Clothes/little_backpack.py
# Shapes and rig sizes: ../kit.py.
import math
import os
import sys

sys.dont_write_bytecode = True  # no __pycache__ next to kit.py
sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), ".."))
from kit import *  # noqa: E402,F401,F403

reset()
orange = material("Cloth", (1.0, 0.6, 0.3))
flap = material("Flap", (0.9, 0.45, 0.22))
strap = material("Strap", (0.6, 0.35, 0.2))
buckle = material("Buckle", (1.0, 0.85, 0.4))
body = Part("Body")
c = Vector((0, -0.31, 0.5))
body.box(orange, c, (0.17, 0.08, 0.18))
body.sphere(flap, c + Vector((0, -0.005, 0.12)), (0.17, 0.085, 0.08), cut=(c + Vector((0, 0, 0.1)), (0, 0, -1)))
body.box(flap, c + Vector((0, -0.08, -0.06)), (0.11, 0.025, 0.07))
body.sphere(buckle, c + Vector((0, -0.09, 0.1)), (0.025, 0.015, 0.02), segments=12, rings=6)
for side in (-1, 1):
    body.torus(strap, (side * 0.12, -0.02, 0.5), 0.23, 0.022, rotation=Matrix.Rotation(math.radians(90), 4, "Y"),
               arc=0.55, scale=(1.1, 1.05))
body.build()
export(__file__)
