# Mushroom Pocket Hoodie (Top, shop): a cozy mint hoodie over the body with a hood lying on its back, drawstrings and a front pocket with a little red spotted mushroom on it.
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
red = material("Cap", CAP_RED)
spot_mat = material("Spots", SPOT_WHITE)
stem = material("Stem", STEM_CREAM)
string = material("String", (1.0, 0.97, 0.92))
for body in bodies():
    cloth(body, mint, TOP_HEM, out=TOP_OUT + 0.003)
    ring(body, trim, TOP_HEM + 0.01, 0.013, out=TOP_OUT + 0.003)
    # The hood, lying on the back below the top, and its trim round the top.
    z = at(0.68)
    body.sphere(mint, body_point(180, z, TOP_OUT + 0.022)[0], (0.085, 0.07, 0.022), rotation=body_frame(180, z),
                segments=24, rings=12)
    body.sphere(trim, body_point(180, z + 0.015, TOP_OUT + 0.04)[0], (0.05, 0.036, 0.009),
                rotation=body_frame(180, z + 0.015), segments=20, rings=10)
    ring(body, trim, at(0.87), 0.015, out=TOP_OUT + 0.003)
    # Drawstrings down the front.
    for side in (-1, 1):
        on_body(body, string, [(side * 18, at(0.84)), (side * 15, at(0.74)), (side * 14, at(0.66))], 0.006,
                out=TOP_OUT + 0.003)
        body.sphere(string, body_point(side * 14, at(0.655), TOP_OUT + 0.016)[0], (0.012, 0.012, 0.015),
                    segments=10, rings=6)
    # The pocket and its mushroom.
    z = at(0.44)
    frame = body_frame(0, z)
    pocket, n = body_point(0, z, TOP_OUT + 0.015)
    body.box(trim, pocket, (0.075, 0.04, 0.012), rotation=frame)
    up = (frame @ Vector((0, 1, 0, 0))).to_3d()
    body.sphere(stem, pocket + n * 0.012 - up * 0.006, (0.015, 0.02, 0.008), rotation=frame, segments=12, rings=6)
    cap = pocket + n * 0.014 + up * 0.012
    body.sphere(red, cap, (0.034, 0.02, 0.009), rotation=frame, segments=20, rings=10)
    for dx, dy in ((-0.015, 0.008), (0.014, 0.009), (0.0, 0.017)):
        body.sphere(spot_mat, cap + n * 0.0075 + (frame @ Vector((dx, dy, 0, 0))).to_3d(), (0.006, 0.006, 0.003),
                    rotation=frame, segments=8, rings=4)
    body.build()
export(__file__)
