# Pigtails: the cap and two round puffs tied at the sides with little ties.
# Run from the repository root (writes pigtails.glb next to this script):
#   blender -b --factory-startup --python modules/Wardrobe/Models/Hair/pigtails.py
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
for side in (-1, 1):
    head.sphere(tie, HEAD_C + Vector((side * 0.4, -0.08, -0.02)), (0.05, 0.05, 0.05), segments=12, rings=6)
    head.sphere(hair, HEAD_C + Vector((side * 0.5, -0.1, -0.14)), (0.13, 0.13, 0.18),
                rotation=Matrix.Rotation(math.radians(side * 20), 4, "Y"), segments=20, rings=10)
head.build()
export(__file__)
