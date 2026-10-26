# Triangle Party Dress (Top/dress, shop): a lilac-pink triangle dress over the whole body like Abby's, with a ruffled hem, gold stars and a bow at the collar.
# Run from the repository root (writes party_dress.glb next to this script):
#   blender -b --factory-startup --python modules/Wardrobe/Models/Clothes/party_dress.py
# Shapes and rig sizes: ../kit.py.
import math
import os
import sys

sys.dont_write_bytecode = True  # no __pycache__ next to kit.py
sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), ".."))
from kit import *  # noqa: E402,F401,F403

reset()
pink = material("Cloth", (0.95, 0.55, 0.82))
ruffle = material("Ruffle", (1.0, 0.85, 0.95))
gold = material("Star", (1.0, 0.82, 0.33))
bow = material("Bow", (0.7, 0.45, 0.92))
body = Part("Body")
OUT = TOP_OUT + 0.002
cloth(body, pink, out=OUT)
for k in range(18):
    a = k / 18 * 360
    p, n = body_point(a, BODY_BOTTOM + 0.035, OUT + 0.03)
    body.sphere(ruffle, p, (0.055, 0.03, 0.04), rotation=body_frame(a, BODY_BOTTOM + 0.035), segments=14, rings=6)
for a, z in ((0, 0.28), (60, 0.24), (-60, 0.25), (120, 0.27), (-120, 0.23), (180, 0.26), (25, 0.42), (-30, 0.44),
             (150, 0.4), (-150, 0.42)):
    rot = body_frame(a, z)
    p, n = body_point(a, z, OUT + 0.004)
    for i in range(5):
        tt = i / 5 * 2 * math.pi
        off = (rot @ Vector((math.sin(tt) * 0.02, math.cos(tt) * 0.02, 0, 0))).to_3d()
        body.sphere(gold, p + off, (0.012, 0.012, 0.006), rotation=rot @ Matrix.Rotation(-tt, 4, "Z"), segments=8, rings=4)
    body.sphere(gold, p, (0.014, 0.014, 0.007), rotation=rot, segments=8, rings=4)
# The bow at the collar, on the front just under the top.
rot = body_frame(0, 0.52)
c, n = body_point(0, 0.52, OUT + 0.03)
for side in (-1, 1):
    body.sphere(bow, c + (rot @ Vector((side * 0.055, 0.005, -0.005, 0))).to_3d(), (0.055, 0.035, 0.025),
                rotation=rot @ Matrix.Rotation(math.radians(side * 15), 4, "Z"))
body.sphere(bow, c + n * 0.008, (0.022, 0.024, 0.02), rotation=rot)
body.build()
export(__file__)
