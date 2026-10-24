# Star Sunglasses (Face, shop): pink star-shaped frames with dark lenses.
# Run from the repository root (writes star_sunglasses.glb next to this script):
#   blender -b --factory-startup --python modules/Wardrobe/Models/Clothes/star_sunglasses.py
# Shapes and rig sizes: ../kit.py.
import math
import os
import sys

sys.dont_write_bytecode = True  # no __pycache__ next to kit.py
sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), ".."))
from kit import *  # noqa: E402,F401,F403

reset()
frame = material("Frame", (1.0, 0.45, 0.7))
lens = material("Lens", (0.2, 0.15, 0.3))
head = Part("Head")
face = Matrix.Rotation(math.radians(90), 4, "X")
y, z = HEAD_C.y + HEAD_R * 1.04, HEAD_C.z - 0.03
for side in (-1, 1):
    c = Vector((side * 0.16, y, z))
    head.sphere(lens, c, (0.075, 0.015, 0.075), segments=20, rings=8)
    for i in range(5):
        t = i / 5 * 2 * math.pi
        d = Vector((math.sin(t), 0, math.cos(t)))
        head.sphere(frame, c + d * 0.08, (0.03, 0.02, 0.06),
                    rotation=Matrix.Rotation(-t, 4, "Y"), segments=12, rings=6)
    head.torus(frame, c, 0.075, 0.015, rotation=face, segments=30)
    head.capsule(frame, (side * 0.25, y - 0.02, z + 0.02), (side * 0.39, 0.0, z + 0.04), 0.013, segments=8)
head.capsule(frame, (-0.08, y + 0.005, z + 0.02), (0.08, y + 0.005, z + 0.02), 0.014, segments=8)
head.build()
export(__file__)
