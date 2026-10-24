# Red Mushroom-Cap Hat (Head, shop): a big red mushroom cap with white spots and cream gills underneath.
# Run from the repository root (writes mushroom_hat.glb next to this script):
#   blender -b --factory-startup --python modules/Wardrobe/Models/Clothes/mushroom_hat.py
# Shapes and rig sizes: ../kit.py.
import math
import os
import sys

sys.dont_write_bytecode = True  # no __pycache__ next to kit.py
sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), ".."))
from kit import *  # noqa: E402,F401,F403

reset()
red = material("Cap", (0.92, 0.22, 0.25))
spot = material("Spots", (1.0, 0.97, 0.92))
gills = material("Gills", (1.0, 0.9, 0.78))
head = Part("Head")
base = HEAD_C.z + 0.24
head.sphere(red, (0, -0.01, base), (0.52, 0.5, 0.32), cut=((0, 0, base), (0, 0, -1)), segments=40, rings=20)
head.sphere(gills, (0, -0.01, base + 0.002), (0.5, 0.48, 0.05), keep=((0, 0, base + 0.003), (0, 0, 1)), segments=40, rings=8)
for a, e in ((0, 25), (70, 30), (140, 22), (210, 28), (290, 24), (35, 60), (180, 58), (260, 62), (110, 64), (0, 85)):
    t, el = math.radians(a), math.radians(e)
    n = Vector((math.sin(t) * math.cos(el) * 0.52, math.cos(t) * math.cos(el) * 0.5 - 0.01, math.sin(el) * 0.32))
    p = Vector((0, 0, base)) + n
    normal = Vector((n.x / 0.52 ** 2, n.y / 0.5 ** 2, n.z / 0.32 ** 2)).normalized()
    head.sphere(spot, p, (0.065, 0.065, 0.018), rotation=normal.to_track_quat("Z", "Y").to_matrix().to_4x4(),
                segments=16, rings=6)
head.build()
export(__file__)
