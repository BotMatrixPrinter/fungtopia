# Mary-Janes (Shoes, starter): round shiny cherry-red shoes over the round feet, with a strap, a button and a white frilly sock cuff.
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
    c = shoe(leg, red, sole, size=(0.098, 0.13, 0.062))
    # The sock's frill round the shoe's opening, and the strap across the top.
    leg.torus(sock, c + Vector((0, -0.035, 0.046)), 0.055, 0.012, segments=24, scale=(1.1, 0.9))
    leg.torus(red, c + Vector((0, 0.03, 0.0)), 0.098, 0.011, rotation=Matrix.Rotation(math.radians(90), 4, "Y"), arc=0.5,
              scale=(0.62, 1.01))
    side = 1 if x > 0 else -1
    leg.sphere(button, c + Vector((side * 0.096, 0.03, 0.0)), (0.008, 0.014, 0.014), segments=10, rings=5)
    leg.build()
export(__file__)
