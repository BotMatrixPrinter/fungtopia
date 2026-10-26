# Rain Boots (Shoes, starter): shiny round yellow rain boots over the round feet, with a white sole and a red rim.
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
    size = (0.105, 0.14, 0.066)
    c = shoe(leg, yellow, sole, size=size)
    # The red rim round the boot's top, where it meets the body.
    dz = 0.035
    k = math.sqrt(1 - (dz / size[2]) ** 2)
    leg.torus(rim, c + Vector((0, 0, dz)), size[0] * k + 0.006, 0.014, segments=32, scale=(1, size[1] / size[0]))
    leg.build()
export(__file__)
