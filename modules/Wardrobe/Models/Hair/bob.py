# Bob: rounded hair down to the chin all around, with a full fringe.
# Run from the repository root (writes bob.glb next to this script):
#   blender -b --factory-startup --python modules/Wardrobe/Models/Hair/bob.py
# Shapes and rig sizes: ../kit.py.
import math
import os
import sys

sys.dont_write_bytecode = True  # no __pycache__ next to kit.py
sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), ".."))
from kit import *  # noqa: E402,F401,F403

reset()
hair = material("Hair", (0.3, 0.18, 0.1))
head = Part("Head")
hair_cap(head, hair)
head.sphere(hair, HEAD_C + Vector((0, -0.04, -0.04)), (HEAD_R * 1.13, HEAD_R * 1.1, HEAD_R * 1.0),
            cut=(HEAD_C + Vector((0, 0.16, 0)), (0, 1, 0)), keep=(HEAD_C + Vector((0, 0, -0.27)), (0, 0, -1)), segments=40, rings=20)
for side in (-1, 1):
    head.sphere(hair, HEAD_C + Vector((side * 0.37, 0.1, -0.2)), (0.1, 0.12, 0.13), segments=16, rings=8)
head.build()
export(__file__)
