# Spotted Mushroom Dress (Top/dress, starter): a red dress over the torso, flaring below the hips over the legs' tops, with white spots like a mushroom cap, a rolled hem and a cream collar.
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
OUT = TOP_OUT + 0.002
rings = dress(body, red, OUT)
hem_ring(body, red, rings, 0.625, 0.018)
ring(body, cream, 1.405, 0.02, out=OUT)
for a, z in ((5, 1.33), (-60, 1.3), (70, 1.28), (130, 1.32), (-140, 1.31), (180, 1.27),
             (30, 1.16), (-35, 1.14), (100, 1.17), (-100, 1.18), (160, 1.15), (-170, 1.12), (-5, 1.03), (140, 1.02)):
    spot(body, spot_mat, a, z, 0.032, out=OUT)
# Spots on the skirt (none beside the hands, which hang over its sides).
for a, z in ((0, 0.74), (36, 0.84), (-36, 0.82), (144, 0.76), (-144, 0.75), (180, 0.86), (24, 0.68), (-22, 0.7),
             (160, 0.68), (-158, 0.66), (60, 0.92), (-60, 0.92), (120, 0.92), (-120, 0.92)):
    p, n = hem_point(rings, a, z, 0.003)
    body.sphere(spot_mat, p, (0.034, 0.034, 0.01), rotation=surface_frame(n), segments=16, rings=8)
body.build()
export(__file__)
