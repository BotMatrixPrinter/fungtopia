# Mary-Janes (Shoes, starter): round shiny cherry-red shoes with a strap and a button, over white frilly socks.
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
for name in ("LegL", "LegR"):
    leg = Part(name)
    x = leg.pivot.x
    leg.cone(sock, (x, 0, 0.08), 0.07, 0.07, 0.1, segments=20)
    leg.torus(sock, (x, 0, 0.18), 0.075, 0.016, segments=20)
    shoe(leg, red, sole, size=(0.095, 0.13, 0.068))
    leg.torus(red, (x, 0.02, 0.1), 0.075, 0.012, rotation=Matrix.Rotation(math.radians(90), 4, "Y"), arc=0.5)
    side = 1 if x > 0 else -1
    leg.sphere(button, (x + side * 0.08, 0.02, 0.085), (0.012, 0.016, 0.016), segments=10, rings=5)
    leg.build()
export(__file__)
