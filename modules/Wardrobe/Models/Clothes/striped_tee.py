# Striped Tee (Top, starter): a soft cream tee with chunky coral stripes and puffy sleeves.
# Run from the repository root (writes striped_tee.glb next to this script):
#   blender -b --factory-startup --python modules/Wardrobe/Models/striped_tee.py
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
shirt(body, cream, sleeve_mat=stripe)
for z in (0.38, 0.47, 0.56, 0.65):
    r = on_chest(90, z).x + 0.02
    body.torus(stripe, (0, 0, z), r, 0.018, scale=(1, CHEST_R[1] / CHEST_R[0]))
collar(body, stripe)
body.build()
export(__file__)
