# Triangle Party Dress (Top/dress, shop): a tall lilac-pink triangle dress like Abby's, with a ruffled hem, gold stars and a bow at the collar.
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
bottom, top = HIP_Z - 0.12, NECK_Z + 0.02
body.cone(pink, (0, 0, bottom), 0.4, 0.11, top - bottom, segments=48)
body.sphere(pink, (0, 0, top - 0.01), (0.12, 0.11, 0.05), segments=24, rings=8)
for k in range(16):
    t = k / 16 * 2 * math.pi
    body.sphere(ruffle, (math.sin(t) * 0.4, math.cos(t) * 0.4, bottom + 0.005), (0.075, 0.075, 0.04), segments=14, rings=6)
for a, z in ((0, 0.34), (60, 0.29), (-60, 0.3), (120, 0.33), (-120, 0.28), (180, 0.31), (30, 0.5), (-35, 0.52)):
    p = cone_surface(top, bottom, 0.11, 0.4, a, z, out=0.008)
    n = Vector((p.x, p.y, 0.45)).normalized()
    rot = n.to_track_quat("Z", "Y").to_matrix().to_4x4()
    for i in range(5):
        tt = i / 5 * 2 * math.pi
        off = (rot @ Vector((math.sin(tt) * 0.02, math.cos(tt) * 0.02, 0, 1))).to_3d()
        body.sphere(gold, p + off, (0.012, 0.012, 0.006), rotation=rot @ Matrix.Rotation(-tt, 4, "Z"), segments=8, rings=4)
    body.sphere(gold, p, (0.014, 0.014, 0.007), rotation=rot, segments=8, rings=4)
for side in (-1, 1):
    body.sphere(bow, (side * 0.07, 0.12, NECK_Z - 0.04), (0.06, 0.03, 0.04), rotation=Matrix.Rotation(math.radians(side * -15), 4, "Y"))
body.sphere(bow, (0, 0.135, NECK_Z - 0.04), (0.025, 0.02, 0.025))
body.build()
export(__file__)
