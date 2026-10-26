# Spotted Mushroom Dress (Top/dress, starter): a red dress over the whole triangle with white spots like a mushroom cap, a ruffled hem and a cream collar.
# Run from the repository root (writes spotted_dress.glb next to this script):
#   blender -b --factory-startup --python modules/Wardrobe/Models/Clothes/spotted_dress.py
# Shapes and rig sizes: ../kit.py.
import math
import os
import sys

sys.dont_write_bytecode = True  # no __pycache__ next to kit.py
sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), ".."))
from kit import *  # noqa: E402,F401,F403

reset()
red = material("Cloth", CAP_RED)
spot_mat = material("Spots", SPOT_WHITE)
cream = material("Collar", (1.0, 0.92, 0.8))
body = Part("Body")
cloth(body, red, out=TOP_OUT + 0.002)
ring(body, red, BODY_BOTTOM + 0.035, 0.022, out=TOP_OUT + 0.002)
ring(body, cream, 0.55, 0.022, out=TOP_OUT + 0.002)
for a, z in ((20, 0.22), (-40, 0.2), (75, 0.25), (-95, 0.22), (140, 0.24), (-150, 0.25), (180, 0.2),
             (5, 0.36), (-70, 0.35), (110, 0.37), (-130, 0.39), (40, 0.46), (-20, 0.48), (160, 0.45)):
    spot(body, spot_mat, a, z, 0.04, out=TOP_OUT + 0.002)
body.build()
export(__file__)
