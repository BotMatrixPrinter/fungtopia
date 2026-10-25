# Mushroom Backpack (Back, shop): a backpack shaped like a mushroom on the triangle's back, a cream stem bag under a big red spotted cap, with straps over the top.
# Run from the repository root (writes mushroom_backpack.glb next to this script):
#   blender -b --factory-startup --python modules/Wardrobe/Models/Clothes/mushroom_backpack.py
# Shapes and rig sizes: ../kit.py.
import math
import os
import sys

sys.dont_write_bytecode = True  # no __pycache__ next to kit.py
sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), ".."))
from kit import *  # noqa: E402,F401,F403

reset()
stem = material("Stem", STEM_CREAM)
red = material("Cap", CAP_RED)
spot_mat = material("Spots", SPOT_WHITE)
strap = material("Strap", (0.6, 0.35, 0.2))
body = Part("Body")
z = 0.32
rot = body_frame(180, z)
c, n = body_point(180, z, TOP_OUT + 0.01 + 0.08)
body.sphere(stem, c, (0.12, 0.13, 0.08), rotation=rot, segments=24, rings=12)
cap = c + Vector((0, 0, 0.1))
body.sphere(red, cap, (0.21, 0.13, 0.13), cut=(cap, (0, 0, -1)), segments=32, rings=16)
body.sphere(stem, cap + Vector((0, 0, 0.002)), (0.2, 0.12, 0.02), keep=(cap + Vector((0, 0, 0.003)), (0, 0, 1)), segments=32, rings=8)
for a, e in ((0, 40), (60, 30), (-60, 30), (120, 35), (-120, 35), (0, 75), (90, 60), (-90, 60)):
    t, el = math.radians(a + 180), math.radians(e)
    d = Vector((math.sin(t) * math.cos(el) * 0.21, math.cos(t) * math.cos(el) * 0.13, math.sin(el) * 0.13))
    normal = Vector((d.x / 0.21 ** 2, d.y / 0.13 ** 2, d.z / 0.13 ** 2)).normalized()
    body.sphere(spot_mat, cap + d, (0.032, 0.032, 0.01), rotation=normal.to_track_quat("Z", "Y").to_matrix().to_4x4(),
                segments=12, rings=6)
for side in (-1, 1):
    on_body(body, strap, [(180 + side * 30, 0.46), (side * 110, 0.56), (side * 40, 0.52), (side * 60, 0.36),
                          (side * 110, 0.3), (180 + side * 40, 0.26)], 0.016, out=TOP_OUT + 0.006)
body.build()
export(__file__)
