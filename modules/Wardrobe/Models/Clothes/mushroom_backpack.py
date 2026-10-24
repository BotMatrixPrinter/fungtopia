# Mushroom Backpack (Back, shop): a backpack shaped like a mushroom, a cream stem bag under a big red spotted cap.
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
stem = material("Stem", (1.0, 0.92, 0.8))
red = material("Cap", (0.92, 0.25, 0.27))
spot = material("Spots", (1.0, 0.97, 0.92))
strap = material("Strap", (0.6, 0.35, 0.2))
body = Part("Body")
c = Vector((0, -0.3, 0.44))
body.sphere(stem, c, (0.14, 0.09, 0.15), segments=24, rings=12)
cap = c + Vector((0, -0.01, 0.12))
body.sphere(red, cap, (0.22, 0.16, 0.16), cut=(cap, (0, 0, -1)), segments=32, rings=16)
for a, e in ((0, 40), (60, 30), (-60, 30), (120, 35), (-120, 35), (180, 45), (0, 75)):
    t, el = math.radians(a + 180), math.radians(e)
    n = Vector((math.sin(t) * math.cos(el) * 0.22, math.cos(t) * math.cos(el) * 0.16, math.sin(el) * 0.16))
    normal = Vector((n.x / 0.22 ** 2, n.y / 0.16 ** 2, n.z / 0.16 ** 2)).normalized()
    body.sphere(spot, cap + n, (0.035, 0.035, 0.012), rotation=normal.to_track_quat("Z", "Y").to_matrix().to_4x4(),
                segments=12, rings=6)
for side in (-1, 1):
    body.torus(strap, (side * 0.12, -0.02, 0.5), 0.23, 0.022, rotation=Matrix.Rotation(math.radians(90), 4, "Y"),
               arc=0.55, scale=(1.1, 1.05))
body.build()
export(__file__)
