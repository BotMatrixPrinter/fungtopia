# Little Backpack (Back, shop): a small round orange backpack on the body's back with a flap, a front pocket and straps over the top.
# Run from the repository root (writes little_backpack.glb next to this script):
#   blender -b --factory-startup --python modules/Wardrobe/Models/Clothes/little_backpack.py
# Shapes and rig sizes: ../kit.py.
import math
import os
import sys

sys.dont_write_bytecode = True  # no __pycache__ next to kit.py
sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), ".."))
from kit import *  # noqa: E402,F401,F403

def straps(body, strap, radius, back_top, back_low):
    """The two straps: from the bag's top over the shoulders, down the front and back under the
    arms (clear of the hands) to the bag's bottom."""
    for side in (-1, 1):
        on_body(body, strap, [(180 + side * 30, back_top), (side * 115, at(0.9)), (side * 40, at(0.82)),
                              (side * 55, at(0.5)), (side * 115, at(0.4)), (180 + side * 40, back_low)],
                radius, out=TOP_OUT + 0.006)


reset()
orange = material("Cloth", (1.0, 0.6, 0.3))
flap = material("Flap", (0.9, 0.45, 0.22))
strap = material("Strap", (0.6, 0.35, 0.2))
buckle = material("Buckle", (1.0, 0.85, 0.4))
for body in bodies():
    z = at(0.47)
    rot = body_frame(180, z)
    depth = 0.05
    c, n = body_point(180, z, TOP_OUT + 0.01 + depth)
    body.box(orange, c, (0.105, 0.11, depth), rotation=rot)
    up = (rot @ Vector((0, 1, 0, 0))).to_3d()
    body.sphere(flap, c + up * 0.072 + n * 0.004, (0.105, 0.05, depth * 1.05), rotation=rot, cut=(c + up * 0.05, -up))
    body.box(flap, c - up * 0.035 + n * depth, (0.07, 0.045, 0.016), rotation=rot)
    body.sphere(buckle, c + up * 0.036 + n * (depth + 0.006), (0.016, 0.013, 0.009), rotation=rot, segments=12, rings=6)
    straps(body, strap, 0.012, at(0.68), at(0.3))
    body.build()
export(__file__)
