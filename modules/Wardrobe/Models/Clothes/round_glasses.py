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

def lens_frame(side, out=1.1):
    """Where a lens sits over the eye on the design head: its center, a little in front of the eye,
    and a rotation facing along the eye's direction (local X to the right, Y up, Z into the face)."""
    _, n = eye(side)
    right = n.cross(Vector((0, 0, 1))).normalized()
    up = right.cross(n)
    return HEAD_C + n * HEAD_R * out, Matrix((right, up, -n)).transposed().to_4x4()


reset()
frame = material("Frame", (0.45, 0.25, 0.15))
head = Part("Head")
rings = []
for side in (-1, 1):
    c, rot = lens_frame(side)
    head.torus(frame, c, 0.13, 0.017, rotation=rot, segments=40)
    rings.append((c, rot))
    # The arm from the frame's outer edge back to above the ear.
    edge = c + (rot @ Vector((side * 0.13, 0.02, 0, 0))).to_3d()
    head.capsule(frame, edge, HEAD_C + Vector((side * 0.395, -0.02, edge.z - HEAD_C.z + 0.01)), 0.013, segments=8)
# The bridge between the frames' inner edges, curving up over the face.
inner = [c + (rot @ Vector((-side * 0.125, 0.03, 0, 0))).to_3d() for side, (c, rot) in zip((-1, 1), rings)]
mid = (inner[0] + inner[1]) / 2 + Vector((0, 0.025, 0.03))
head.tube(frame, [inner[0], (inner[0] + mid) / 2 + Vector((0, 0.005, 0.01)), mid, (inner[1] + mid) / 2 + Vector((0, 0.005, 0.01)), inner[1]], 0.013)
head.build()
export(__file__)
