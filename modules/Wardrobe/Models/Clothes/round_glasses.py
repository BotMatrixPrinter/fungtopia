# Round Glasses (Face, shop): big round brown frames around the eyes (no lenses to hide them), with arms to the ears.
# Run from the repository root (writes round_glasses.glb next to this script):
#   blender -b --factory-startup --python modules/Wardrobe/Models/Clothes/round_glasses.py
# Shapes and rig sizes: ../kit.py.
import math
import os
import sys

sys.dont_write_bytecode = True  # no __pycache__ next to kit.py
sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), ".."))
from kit import *  # noqa: E402,F401,F403

reset()
frame = material("Frame", (0.45, 0.25, 0.15))
head = Part("Head")
face = Matrix.Rotation(math.radians(90), 4, "X")
y, z = HEAD_C.y + HEAD_R * 1.03, HEAD_C.z - 0.04
for side in (-1, 1):
    head.torus(frame, (side * 0.155, y, z), 0.105, 0.016, rotation=face, segments=40)
    head.capsule(frame, (side * 0.255, y - 0.02, z + 0.02), (side * 0.39, 0.0, z + 0.04), 0.013, segments=8)
head.torus(frame, (0, y + 0.01, z + 0.03), 0.05, 0.013, rotation=face @ Matrix.Rotation(math.radians(90), 4, "Z"), arc=0.3)
head.build()
export(__file__)
