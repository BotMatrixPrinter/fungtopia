# Short hair: the cap and a soft fringe with a little tuft on top.
# Run from the repository root (writes short.glb next to this script):
#   blender -b --factory-startup --python modules/Wardrobe/Models/Hair/short.py
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
head.sphere(hair, HEAD_C + Vector((0.05, 0.05, HEAD_R * 1.0)), (0.05, 0.05, 0.1),
            rotation=Matrix.Rotation(math.radians(25), 4, "Y"), segments=12, rings=6)
head.build()
export(__file__)
