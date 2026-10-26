# Mushroom Backpack (Back, shop): a backpack shaped like a mushroom on the body's back, a cream stem bag under a big red spotted cap, with straps over the top.
# Run from the repository root (writes mushroom_backpack.glb next to this script):
#   blender -b --factory-startup --python modules/Wardrobe/Models/Clothes/mushroom_backpack.py
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
stem = material("Stem", STEM_CREAM)
red = material("Cap", CAP_RED)
spot_mat = material("Spots", SPOT_WHITE)
strap = material("Strap", (0.6, 0.35, 0.2))
for body in bodies():
    z = at(0.41)
    rot = body_frame(180, z)
    c, n = body_point(180, z, TOP_OUT + 0.008 + 0.06)
    body.sphere(stem, c, (0.09, 0.095, 0.06), rotation=rot, segments=24, rings=12)
    cap = c + Vector((0, 0, 0.075)) + n * 0.01
    a, b, h = 0.15, 0.072, 0.095
    body.sphere(red, cap, (a, b, h), cut=(cap, (0, 0, -1)), segments=32, rings=16)
    body.sphere(stem, cap + Vector((0, 0, 0.0015)), (a - 0.007, b - 0.007, 0.015), keep=(cap + Vector((0, 0, 0.002)), (0, 0, 1)),
                segments=32, rings=8)
    for ang, e in ((0, 40), (60, 30), (-60, 30), (120, 35), (-120, 35), (0, 75), (90, 60), (-90, 60)):
        t, el = math.radians(ang + 180), math.radians(e)
        d = Vector((math.sin(t) * math.cos(el) * a, math.cos(t) * math.cos(el) * b, math.sin(el) * h))
        normal = Vector((d.x / a ** 2, d.y / b ** 2, d.z / h ** 2)).normalized()
        body.sphere(spot_mat, cap + d, (0.024, 0.024, 0.0075), rotation=normal.to_track_quat("Z", "Y").to_matrix().to_4x4(),
                    segments=12, rings=6)
    straps(body, strap, 0.012, at(0.64), at(0.28))
    body.build()
export(__file__)
