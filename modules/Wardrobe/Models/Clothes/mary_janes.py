# Mary-Janes (Shoes, starter): round shiny cherry-red shoes over the rounded feet, with a strap, a button and a white frilly sock cuff round the leg.
# Run from the repository root (writes mary_janes.glb next to this script):
#   blender -b --factory-startup --python modules/Wardrobe/Models/Clothes/mary_janes.py
# Shapes and rig sizes: ../kit.py.
import math
import os
import sys

sys.dont_write_bytecode = True  # no __pycache__ next to kit.py
sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), ".."))
from kit import *  # noqa: E402,F401,F403

reset()
red = material("Cloth", (0.8, 0.15, 0.22))
sole = material("Sole", (0.4, 0.2, 0.18))
sock = material("Sock", (1.0, 0.98, 0.95))
button = material("Button", (1.0, 0.85, 0.4))
for leg in legs():
    x = leg.pivot.x
    c, size = shoe(leg, red, sole)
    # The sock's frill round the leg over the shoe's opening, and the strap across the top.
    leg_ring(leg, sock, c.z + size[2] * 0.9, 0.011, out=0.004)
    dy = 0.03
    k = math.sqrt(1 - (dy / size[1]) ** 2)
    leg.torus(red, c + Vector((0, dy, 0)), 1.0, 0.008, arc=0.5, segments=32, scale=(size[2] * k + 0.004, size[0] * k + 0.004),
              rotation=Matrix.Rotation(math.radians(90), 4, "X") @ Matrix.Rotation(math.radians(90), 4, "Z"))
    side = 1 if x > 0 else -1
    leg.sphere(button, c + Vector((side * (size[0] * k + 0.006), dy, 0.0)), (0.006, 0.011, 0.011), segments=10, rings=5)
    leg.build()
export(__file__)
