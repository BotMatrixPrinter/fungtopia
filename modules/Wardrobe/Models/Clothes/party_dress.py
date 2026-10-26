# Triangle Party Dress (Top/dress, shop): a lilac-pink dress over the torso flaring wide below the hips like Abby's triangle dress, with a ruffled hem, gold stars and a bow at the collar.
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
rings = dress(body, pink, OUT)
for k in range(20):
    a = k / 20 * 360
    p, n = hem_point(rings, a, 0.632, 0.014)
    body.sphere(ruffle, p, (0.05, 0.028, 0.028), rotation=surface_frame(n), segments=14, rings=6)


def star(p, rot):
    for i in range(5):
        tt = i / 5 * 2 * math.pi
        off = (rot @ Vector((math.sin(tt) * 0.02, math.cos(tt) * 0.02, 0, 0))).to_3d()
        body.sphere(gold, p + off, (0.012, 0.012, 0.006), rotation=rot @ Matrix.Rotation(-tt, 4, "Z"), segments=8, rings=4)
    body.sphere(gold, p, (0.014, 0.014, 0.007), rotation=rot, segments=8, rings=4)


for a, z in ((25, 1.22), (-30, 1.25), (150, 1.2), (-150, 1.22), (90, 1.15), (-90, 1.12), (0, 1.05), (180, 1.06)):
    star(body_point(a, z, OUT + 0.004)[0], body_frame(a, z))
for a, z in ((0, 0.8), (40, 0.72), (-40, 0.74), (140, 0.76), (-140, 0.72), (180, 0.84), (60, 0.9), (-60, 0.9),
             (120, 0.9), (-120, 0.9)):
    p, n = hem_point(rings, a, z, 0.004)
    star(p, surface_frame(n))
# The bow at the collar, on the front just under the top.
rot = body_frame(0, 1.34)
c, n = body_point(0, 1.34, OUT + 0.026)
for side in (-1, 1):
    body.sphere(bow, c + (rot @ Vector((side * 0.048, 0.004, -0.004, 0))).to_3d(), (0.048, 0.03, 0.022),
                rotation=rot @ Matrix.Rotation(math.radians(side * 15), 4, "Z"))
body.sphere(bow, c + n * 0.006, (0.02, 0.022, 0.018), rotation=rot)
body.build()
export(__file__)
