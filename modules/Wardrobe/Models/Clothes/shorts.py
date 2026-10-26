# Shorts (Bottom, starter): chunky sky-blue shorts, a waist band round the torso's bottom and a short wide leg round each leg's top, ending above the knee with a rolled hem.
# Run from the repository root (writes shorts.glb next to this script):
#   blender -b --factory-startup --python modules/Wardrobe/Models/Clothes/shorts.py
# Shapes and rig sizes: ../kit.py.
import math
import os
import sys

sys.dont_write_bytecode = True  # no __pycache__ next to kit.py
sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), ".."))
from kit import *  # noqa: E402,F401,F403

reset()
blue = material("Cloth", (0.45, 0.68, 0.95))
hem = material("Hem", (0.35, 0.55, 0.85))
body = Part("Body")
waist_band(body, blue)
ring(body, blue, BAND_TOP - 0.004, 0.014, out=BOTTOM_OUT)
body.build()
for name in ("LegL", "LegR"):
    leg = Part(name)
    leg_tube(leg, blue, SHORTS_HEM, 0.95, out=LEG_OUT + 0.01)
    leg_cuff(leg, hem, SHORTS_HEM + 0.016, 0.018, out=LEG_OUT + 0.01)
    leg.build()
export(__file__)
