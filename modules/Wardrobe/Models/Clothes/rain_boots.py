# Rain Boots (Shoes, starter): shiny round yellow rain boots over the rig's shoes and up the shins, with a white sole and a red rim.
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
TOP = 0.34
for name in ("LegL", "LegR"):
    leg = Part(name)
    shoe(leg, yellow, sole, size=(0.088, 0.138, 0.082))
    # The boot's shaft up the leg, and the red rim round its top.
    leg_tube(leg, yellow, 0.06, TOP, out=0.018)
    leg_cuff(leg, rim, TOP, 0.012, out=0.018)
    leg.build()
export(__file__)
