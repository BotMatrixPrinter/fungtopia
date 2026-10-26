# Spotted Mushroom Dress (Top/dress, girl, starter): a red dress over the whole cone with white spots like a mushroom cap, a ruffled hem and a cream collar.
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
for body in bodies(both=False):
    OUT = TOP_OUT + 0.002
    cloth(body, red, out=OUT)
    ring(body, red, widest()[1] - 0.012, 0.016, out=OUT)
    ring(body, cream, at(0.88), 0.014, out=OUT)
    for a, f in ((20, 0.19), (-40, 0.15), (75, 0.25), (-95, 0.19), (140, 0.23), (-150, 0.25), (180, 0.15),
                 (5, 0.48), (-70, 0.46), (110, 0.5), (-130, 0.54), (40, 0.69), (-20, 0.73), (160, 0.67)):
        spot(body, spot_mat, a, at(f), 0.034 - 0.018 * f, out=OUT)
    body.build()
export(__file__)
