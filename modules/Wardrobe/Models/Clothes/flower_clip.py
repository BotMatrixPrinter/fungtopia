# Flower Clip (Head, shop): a big pink five-petal flower clipped above one ear.
# Run from the repository root (writes flower_clip.glb next to this script):
#   blender -b --factory-startup --python modules/Wardrobe/Models/Clothes/flower_clip.py
# Shapes and rig sizes: ../kit.py.
import math
import os
import sys

sys.dont_write_bytecode = True  # no __pycache__ next to kit.py
sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), ".."))
from kit import *  # noqa: E402,F401,F403

reset()
pink = material("Petal", (1.0, 0.55, 0.7))
middle = material("FlowerMiddle", (1.0, 0.82, 0.3))
leaf = material("Leaf", (0.45, 0.78, 0.4))
head = Part("Head")
c = HEAD_C + Vector((0.34, 0.12, 0.2))
n = (c - HEAD_C).normalized()
head.sphere(leaf, c - n * 0.02 + Vector((0, -0.06, -0.04)), (0.06, 0.03, 0.02),
            rotation=n.to_track_quat("Z", "Y").to_matrix().to_4x4(), segments=12, rings=6)
flower(head, pink, middle, c + n * 0.02, n, 0.06)
head.build()
export(__file__)
