# Ladybug Backpack (Back, reward for the first Mushroom Ladybug): a round red shell with black spots and a black top; no face (only creatures have eyes).
# Run from the repository root (writes ladybug_backpack.glb next to this script):
#   blender -b --factory-startup --python modules/Wardrobe/Models/Clothes/ladybug_backpack.py
# Shapes and rig sizes: ../kit.py.
import math
import os
import sys

sys.dont_write_bytecode = True  # no __pycache__ next to kit.py
sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), ".."))
from kit import *  # noqa: E402,F401,F403

reset()
red = material("Shell", (0.92, 0.18, 0.2))
black = material("Black", (0.12, 0.1, 0.12))
strap = material("Strap", (0.25, 0.2, 0.22))
body = Part("Body")
c = Vector((0, -0.25, 0.48))
body.sphere(red, c, (0.19, 0.13, 0.2), cut=(c + Vector((0, 0.04, 0)), (0, 1, 0)), segments=32, rings=16)
body.sphere(black, c + Vector((0, 0.0, 0.18)), (0.11, 0.08, 0.06), segments=20, rings=10)
body.capsule(black, c + Vector((0, -0.125, 0.15)), c + Vector((0, -0.125, -0.17)), 0.008, segments=6)
for x, z, r in ((-0.09, 0.05, 0.035), (0.09, 0.05, 0.035), (-0.08, -0.09, 0.03), (0.08, -0.09, 0.03), (-0.13, -0.02, 0.022), (0.13, -0.02, 0.022)):
    dx, dz = x / 0.19, z / 0.2
    y = -0.13 * math.sqrt(max(0.0, 1 - dx * dx - dz * dz))
    n = Vector((x / 0.19 ** 2, y / 0.13 ** 2, z / 0.2 ** 2)).normalized()
    body.sphere(black, c + Vector((x, y, z)), (r, r, 0.01), rotation=n.to_track_quat("Z", "Y").to_matrix().to_4x4(),
                segments=12, rings=6)
for side in (-1, 1):
    body.torus(strap, (side * 0.12, -0.02, 0.5), 0.23, 0.02, rotation=Matrix.Rotation(math.radians(90), 4, "Y"),
               arc=0.55, scale=(1.1, 1.05))
body.build()
export(__file__)
