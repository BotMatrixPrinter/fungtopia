# Mushroom Backpack (Back, shop): a backpack shaped like a mushroom on the torso's back, a cream stem bag under a big red spotted cap, with straps over the shoulders.
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
z = 1.12
rot = body_frame(180, z)
c, n = body_point(180, z, TOP_OUT + 0.016 + 0.07)
body.sphere(stem, c, (0.1, 0.11, 0.07), rotation=rot, segments=24, rings=12)
CAP = (0.17, 0.09, 0.11)
cap = c + Vector((0, -0.025, 0.085))
body.sphere(red, cap, CAP, cut=(cap, (0, 0, -1)), segments=32, rings=16)
body.sphere(stem, cap + Vector((0, 0, 0.002)), (CAP[0] - 0.01, CAP[1] - 0.01, 0.02), keep=(cap + Vector((0, 0, 0.003)), (0, 0, 1)),
            segments=32, rings=8)
for a, e in ((0, 40), (60, 30), (-60, 30), (120, 35), (-120, 35), (0, 75), (90, 60), (-90, 60)):
    t, el = math.radians(a + 180), math.radians(e)
    d = Vector((math.sin(t) * math.cos(el) * CAP[0], math.cos(t) * math.cos(el) * CAP[1], math.sin(el) * CAP[2]))
    normal = Vector((d.x / CAP[0] ** 2, d.y / CAP[1] ** 2, d.z / CAP[2] ** 2)).normalized()
    body.sphere(spot_mat, cap + d, (0.028, 0.028, 0.009), rotation=normal.to_track_quat("Z", "Y").to_matrix().to_4x4(),
                segments=12, rings=6)
for side in (-1, 1):
    on_body(body, strap, [(side * 152, 1.26), (side * 95, 1.39), (side * 38, 1.33), (side * 45, 1.22),
                          (side * 95, 1.17), (side * 148, 1.08)], 0.015, out=TOP_OUT + 0.016)
body.build()
export(__file__)
