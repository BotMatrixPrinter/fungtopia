# Mushroom Pocket Hoodie (Top, shop): a cozy mint hoodie over the triangle with a hood lying on its back, drawstrings and a front pocket with a little red spotted mushroom on it.
# Run from the repository root (writes mushroom_hoodie.glb next to this script):
#   blender -b --factory-startup --python modules/Wardrobe/Models/Clothes/mushroom_hoodie.py
# Shapes and rig sizes: ../kit.py.
import math
import os
import sys

sys.dont_write_bytecode = True  # no __pycache__ next to kit.py
sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), ".."))
from kit import *  # noqa: E402,F401,F403

reset()
mint = material("Cloth", (0.55, 0.85, 0.72))
trim = material("Trim", (0.42, 0.72, 0.6))
red = material("Cap", (0.92, 0.25, 0.25))
spot_mat = material("Spots", (1.0, 0.97, 0.92))
stem = material("Stem", (1.0, 0.92, 0.8))
string = material("String", (1.0, 0.97, 0.92))
body = Part("Body")
cloth(body, mint, TOP_HEM, out=TOP_OUT + 0.004)
ring(body, trim, TOP_HEM + 0.012, 0.016, out=TOP_OUT + 0.004)
# The hood, lying on the back below the top, and its trim round the top.
hood, _ = body_point(180, 0.45, TOP_OUT + 0.026)
body.sphere(mint, hood, (0.12, 0.085, 0.03), rotation=body_frame(180, 0.45), segments=24, rings=12)
body.sphere(trim, body_point(180, 0.47, TOP_OUT + 0.046)[0], (0.07, 0.045, 0.012), rotation=body_frame(180, 0.47),
            segments=20, rings=10)
ring(body, trim, 0.55, 0.02, out=TOP_OUT + 0.004)
# Drawstrings down the front.
for side in (-1, 1):
    on_body(body, string, [(side * 16, 0.53), (side * 13, 0.46), (side * 12, 0.42)], 0.008, out=TOP_OUT + 0.004)
    body.sphere(string, body_point(side * 12, 0.415, TOP_OUT + 0.02)[0], (0.016, 0.016, 0.02), segments=10, rings=6)
# The pocket and its mushroom.
frame = body_frame(0, 0.34)
pocket, n = body_point(0, 0.34, TOP_OUT + 0.02)
body.box(trim, pocket, (0.11, 0.05, 0.016), rotation=frame)
up = (frame @ Vector((0, 1, 0, 0))).to_3d()
body.sphere(stem, pocket + n * 0.016 - up * 0.008, (0.02, 0.026, 0.01), rotation=frame, segments=12, rings=6)
cap = pocket + n * 0.018 + up * 0.016
body.sphere(red, cap, (0.045, 0.026, 0.012), rotation=frame, segments=20, rings=10)
for dx, dy in ((-0.02, 0.01), (0.018, 0.012), (0.0, 0.022)):
    body.sphere(spot_mat, cap + n * 0.01 + (frame @ Vector((dx, dy, 0, 0))).to_3d(), (0.008, 0.008, 0.004),
                rotation=frame, segments=8, rings=4)
body.build()
export(__file__)
