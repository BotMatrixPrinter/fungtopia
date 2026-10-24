# Top bun (like Abby's): the cap and a tall round bun on top of the head with a ribbon around it.
# Run from the repository root (writes top_bun.glb next to this script):
#   blender -b --factory-startup --python modules/Wardrobe/Models/Hair/top_bun.py
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
top = HEAD_C.z + HEAD_R * 1.04
head.cone(hair, (0, -0.03, top - 0.05), 0.12, 0.1, 0.08, segments=24)
head.sphere(hair, (0, -0.03, top + 0.15), (0.16, 0.16, 0.15), segments=24, rings=12)
head.sphere(hair, (0, -0.03, top + 0.29), (0.09, 0.09, 0.08), segments=16, rings=8)
head.torus(tie, (0, -0.03, top + 0.03), 0.105, 0.025, segments=24)
head.build()
export(__file__)
