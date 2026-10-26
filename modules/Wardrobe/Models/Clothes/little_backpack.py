# Little Backpack (Back, shop): a small round orange backpack on the torso's back with a flap, a front pocket and straps over the shoulders.
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
z = 1.16
rot = body_frame(180, z)
depth = 0.065
c, n = body_point(180, z, TOP_OUT + 0.016 + depth)
body.box(orange, c, (0.13, 0.14, depth), rotation=rot)
up = (rot @ Vector((0, 1, 0, 0))).to_3d()
body.sphere(flap, c + up * 0.09 + n * 0.005, (0.13, 0.065, depth * 1.05), rotation=rot, cut=(c + up * 0.065, -up))
body.box(flap, c - up * 0.05 + n * depth, (0.09, 0.055, 0.02), rotation=rot)
body.sphere(buckle, c + up * 0.045 + n * (depth + 0.008), (0.02, 0.016, 0.011), rotation=rot, segments=12, rings=6)
for side in (-1, 1):
    on_body(body, strap, [(side * 150, 1.27), (side * 95, 1.39), (side * 38, 1.33), (side * 45, 1.22),
                          (side * 95, 1.17), (side * 148, 1.08)], 0.014, out=TOP_OUT + 0.016)
body.build()
export(__file__)
