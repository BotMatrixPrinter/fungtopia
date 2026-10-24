# Rain Boots (Shoes, starter): shiny yellow rain boots up to the knee with a white sole and a red rim.
# Run from the repository root (writes rain_boots.glb next to this script):
#   blender -b --factory-startup --python modules/Wardrobe/Models/Clothes/rain_boots.py
# Shapes and rig sizes: ../kit.py.
import math
import os
import sys

sys.dont_write_bytecode = True  # no __pycache__ next to kit.py
sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), ".."))
from kit import *  # noqa: E402,F401,F403

reset()
yellow = material("Cloth", (1.0, 0.82, 0.25))
sole = material("Sole", (1.0, 0.97, 0.9))
rim = material("Rim", (0.92, 0.3, 0.32))
for name in ("LegL", "LegR"):
    leg = Part(name)
    x = leg.pivot.x
    shoe(leg, yellow, sole, size=(0.1, 0.14, 0.08))
    leg.cone(yellow, (x, -0.005, 0.05), 0.088, 0.09, 0.2, segments=24)
    leg.torus(rim, (x, -0.005, 0.25), 0.092, 0.02, segments=24)
    leg.build()
export(__file__)
