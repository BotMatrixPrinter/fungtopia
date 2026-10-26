# Striped Tee (Top, starter): a soft cream tee over the body with chunky coral stripes and a coral collar.
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
for body in bodies():
    cloth(body, cream, TOP_HEM)
    ring(body, cream, TOP_HEM + 0.008, 0.011)
    # Flat chunky stripes (under the bow's strap), and the collar round the top.
    for f in (0.38, 0.54, 0.7):
        cloth(body, stripe, at(f) - 0.028, at(f) + 0.028, out=TOP_OVER)
    cloth(body, stripe, at(0.85), at(0.92), out=TOP_OVER)
    body.build()
export(__file__)
