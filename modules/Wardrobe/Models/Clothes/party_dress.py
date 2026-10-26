# Triangle Party Dress (Top/dress, shop): a lilac-pink dress over the whole body like Abby's, with a ruffled hem, gold stars and a bow at the collar.
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
for body in bodies():
    OUT = TOP_OUT + 0.002
    cloth(body, pink, out=OUT)
    z = widest()[1] - 0.012
    for k in range(18):
        a = k / 18 * 360
        p, n = body_point(a, z, OUT + 0.022)
        body.sphere(ruffle, p, (0.04, 0.024, 0.02), rotation=body_frame(a, z), segments=14, rings=6)
    for a, f in ((0, 0.31), (60, 0.23), (-60, 0.25), (120, 0.29), (-120, 0.21), (180, 0.27), (25, 0.6), (-30, 0.65),
                 (150, 0.56), (-150, 0.6)):
        z = at(f)
        rot = body_frame(a, z)
        p, n = body_point(a, z, OUT + 0.003)
        for i in range(5):
            tt = i / 5 * 2 * math.pi
            off = (rot @ Vector((math.sin(tt) * 0.015, math.cos(tt) * 0.015, 0, 0))).to_3d()
            body.sphere(gold, p + off, (0.009, 0.009, 0.0045), rotation=rot @ Matrix.Rotation(-tt, 4, "Z"), segments=8, rings=4)
        body.sphere(gold, p, (0.0105, 0.0105, 0.005), rotation=rot, segments=8, rings=4)
    # The bow at the collar, on the front just under the top.
    z = at(0.81)
    rot = body_frame(0, z)
    c, n = body_point(0, z, OUT + 0.022)
    for side in (-1, 1):
        body.sphere(bow, c + (rot @ Vector((side * 0.04, 0.004, -0.004, 0))).to_3d(), (0.04, 0.026, 0.018),
                    rotation=rot @ Matrix.Rotation(math.radians(side * 15), 4, "Z"))
    body.sphere(bow, c + n * 0.006, (0.016, 0.018, 0.015), rotation=rot)
    body.build()
export(__file__)
