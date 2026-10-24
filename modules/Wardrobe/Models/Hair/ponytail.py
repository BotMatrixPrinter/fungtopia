# Ponytail: the cap and a bouncy tail tied high at the back.
# Run from the repository root (writes ponytail.glb next to this script):
#   blender -b --factory-startup --python modules/Wardrobe/Models/Hair/ponytail.py
# Shapes and rig sizes: ../kit.py.
import math
import os
import sys

sys.dont_write_bytecode = True  # no __pycache__ next to kit.py
sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), ".."))
from kit import *  # noqa: E402,F401,F403

reset()
hair = material("Hair", (0.3, 0.18, 0.1))
tie = material("Tie", (1.0, 0.45, 0.6))
head = Part("Head")
hair_cap(head, hair)
tie_c = HEAD_C + Vector((0, -0.42, 0.12))
head.sphere(tie, tie_c, (0.06, 0.04, 0.06), segments=12, rings=6)
for i, (y, z, r) in enumerate(((-0.5, 0.06, 0.1), (-0.54, -0.06, 0.11), (-0.53, -0.18, 0.1), (-0.48, -0.28, 0.07))):
    head.sphere(hair, HEAD_C + Vector((0, y, z)), (r, r * 0.9, r * 1.1), segments=16, rings=8)
head.build()
export(__file__)
