# Ladybug Backpack (Back, reward for the first Mushroom Ladybug): a round red shell with black spots and a black top on the triangle's back, with black straps over the top; no face (only creatures have eyes).
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
z = 0.35
rot = body_frame(180, z)
base, n = body_point(180, z, TOP_OUT + 0.008)
size = (0.17, 0.18, 0.12)  # across, up, out
body.sphere(red, base, size, rotation=rot, cut=(base, -n), segments=32, rings=16)
up = (rot @ Vector((0, 1, 0, 0))).to_3d()
across = (rot @ Vector((1, 0, 0, 0))).to_3d()
body.sphere(black, base + up * 0.15 + n * 0.05, (0.1, 0.06, 0.06), rotation=rot, segments=20, rings=10)
# The black line down the middle of the shell.
body.tube(black, [base + up * (size[1] * math.sin(math.radians(t))) + n * (size[2] * math.cos(math.radians(t)) + 0.004)
                  for t in range(-80, 81, 10)], 0.007)
for x, y, r in ((-0.08, 0.05, 0.032), (0.08, 0.05, 0.032), (-0.075, -0.08, 0.028), (0.075, -0.08, 0.028),
                (-0.125, -0.02, 0.022), (0.125, -0.02, 0.022)):
    dx, dy = x / size[0], y / size[1]
    h = size[2] * math.sqrt(max(0.0, 1 - dx * dx - dy * dy))
    p = base + across * x + up * y + n * h
    normal = (across * (x / size[0] ** 2) + up * (y / size[1] ** 2) + n * (h / size[2] ** 2)).normalized()
    body.sphere(black, p, (r, r, 0.008), rotation=normal.to_track_quat("Z", "Y").to_matrix().to_4x4(), segments=12, rings=6)
for side in (-1, 1):
    on_body(body, strap, [(180 + side * 35, 0.47), (side * 110, 0.56), (side * 40, 0.52), (side * 60, 0.36),
                          (side * 110, 0.3), (180 + side * 40, 0.27)], 0.014, out=TOP_OUT + 0.006)
body.build()
export(__file__)
