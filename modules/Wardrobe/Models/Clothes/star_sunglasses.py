# Star Sunglasses (Face, shop): pink star-shaped frames with dark lenses over the eyes.
# Run from the repository root (writes star_sunglasses.glb next to this script):
#   blender -b --factory-startup --python modules/Wardrobe/Models/Clothes/star_sunglasses.py
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
frame = material("Frame", (1.0, 0.45, 0.7))
lens = material("Lens", (0.2, 0.15, 0.3))
head = Part("Head")
rings = []
R = 0.12
for side in (-1, 1):
    c, rot = lens_frame(side)
    head.sphere(lens, c, (R, R, 0.015), rotation=rot, segments=24, rings=8)
    for i in range(5):
        t = i / 5 * 2 * math.pi
        d = (rot @ Vector((math.sin(t), math.cos(t), 0, 0))).to_3d()
        head.sphere(frame, c + d * (R + 0.012), (0.045, 0.085, 0.022), rotation=rot @ Matrix.Rotation(-t, 4, "Z"),
                    segments=12, rings=6)
    head.torus(frame, c, R, 0.016, rotation=rot, segments=30)
    rings.append((c, rot))
    edge = c + (rot @ Vector((side * R, 0.02, 0, 0))).to_3d()
    head.capsule(frame, edge, HEAD_C + Vector((side * 0.395, -0.02, edge.z - HEAD_C.z + 0.01)), 0.013, segments=8)
inner = [c + (rot @ Vector((-side * R, 0.03, 0, 0))).to_3d() for side, (c, rot) in zip((-1, 1), rings)]
mid = (inner[0] + inner[1]) / 2 + Vector((0, 0.02, 0.0))
head.tube(frame, [inner[0], mid, inner[1]], 0.014)
head.build()
export(__file__)
