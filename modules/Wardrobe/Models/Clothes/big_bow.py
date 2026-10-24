# Big Bow (Head, starter): a big puffy red ribbon bow on top of the head.
# Run from the repository root (writes big_bow.glb next to this script):
#   blender -b --factory-startup --python modules/Wardrobe/Models/Clothes/big_bow.py
# Shapes and rig sizes: ../kit.py.
import math
import os
import sys

sys.dont_write_bytecode = True  # no __pycache__ next to kit.py
sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), ".."))
from kit import *  # noqa: E402,F401,F403

reset()
red = material("Ribbon", (0.95, 0.24, 0.36))
head = Part("Head")
cx, cy, cz, size = 0.1, 0.05, HEAD_C.z + HEAD_R * 0.98 + 0.02, 0.17
for side in (-1, 1):
    head.sphere(red, (cx + side * size * 0.9, cy, cz + size * 0.1), (size * 0.85, size * 0.45, size * 0.6),
                rotation=Matrix.Rotation(math.radians(-18 * side), 4, "Y"))
    head.capsule(red, (cx + side * 0.03, cy + 0.02, cz - 0.02), (cx + side * 0.12, cy + 0.04, cz - 0.15), 0.03, segments=10)
head.sphere(red, (cx, cy + size * 0.12, cz), (size * 0.38, size * 0.4, size * 0.38))
head.build()
export(__file__)
