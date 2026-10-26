# Rain Boots (Shoes, starter): shiny round yellow rain boots over the rounded feet and up the leg tubes (under the trouser legs), with a white sole and a red rim.
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
for leg in legs():
    c, size = shoe(leg, yellow, sole, grow=(0.016, 0.016, 0.014))
    # The boot's shaft up round the leg tube, thin enough to go under the trouser legs: it starts
    # inside the shoe as an oval round the foot (between the foot and the upper) and rounds into
    # a tube round the leg, with a red rim round its top.
    top = 0.22
    x = leg.pivot.x
    r = LEG_R + 0.006
    leg.loft(yellow, [((x, FOOT_C[0]), (0.07, 0.09), 0.09), ((x, 0.012), (0.07, 0.08), 0.12),
                      ((x, 0), (r, r), 0.15), ((x, 0), (r, r), 0.18), ((x, 0), (r, r), top)])
    leg.torus(rim, (x, 0, top), LEG_R + 0.0035, 0.0035, segments=32)
    leg.build()
export(__file__)
