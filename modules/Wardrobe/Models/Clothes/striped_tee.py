# Striped Tee (Top, starter): a soft cream tee over the triangle with chunky coral stripes and a coral collar.
# Run from the repository root (writes striped_tee.glb next to this script):
#   blender -b --factory-startup --python modules/Wardrobe/Models/Clothes/striped_tee.py
# Shapes and rig sizes: ../kit.py.
import math
import os
import sys

sys.dont_write_bytecode = True  # no __pycache__ next to kit.py
sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), ".."))
from kit import *  # noqa: E402,F401,F403

reset()
cream = material("Cloth", (1.0, 0.95, 0.86))
stripe = material("Stripe", (0.95, 0.42, 0.42))
body = Part("Body")
cloth(body, cream, TOP_HEM)
ring(body, cream, TOP_HEM + 0.01, 0.014)
# Flat chunky stripes, and the collar round the top.
for z in (0.31, 0.39, 0.47):
    cloth(body, stripe, z - 0.022, z + 0.022, out=TOP_OUT + 0.004)
cloth(body, stripe, 0.535, 0.575, out=TOP_OUT + 0.004)
body.build()
export(__file__)
