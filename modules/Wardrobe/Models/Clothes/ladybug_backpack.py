# Ladybug Backpack (Back, reward for the first Mushroom Ladybug): a round red shell with black spots and a black top on the torso's back, with black straps over the shoulders; no face (only creatures have eyes).
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
z = 1.15
size = (0.14, 0.15, 0.1)  # across, up, out
# Upright on the back, its flat side just behind the deepest part of the back it spans.
back = max(torso_size(z + size[1] * k / 10)[1] for k in range(-10, 11))
n = Vector((0, -1, 0))
rot = surface_frame(n)
base = Vector((0, -(back + TOP_OUT + 0.012), z))
body.sphere(red, base, size, rotation=rot, cut=(base, -n), segments=32, rings=16)
up = (rot @ Vector((0, 1, 0, 0))).to_3d()
across = (rot @ Vector((1, 0, 0, 0))).to_3d()
body.sphere(black, base + up * 0.125 + n * 0.04, (0.085, 0.05, 0.05), rotation=rot, segments=20, rings=10)
# The black line down the middle of the shell.
body.tube(black, [base + up * (size[1] * math.sin(math.radians(t))) + n * (size[2] * math.cos(math.radians(t)) + 0.004)
                  for t in range(-80, 81, 10)], 0.006)
for x, y, r in ((-0.065, 0.04, 0.027), (0.065, 0.04, 0.027), (-0.062, -0.065, 0.024), (0.062, -0.065, 0.024),
                (-0.105, -0.015, 0.019), (0.105, -0.015, 0.019)):
    dx, dy = x / size[0], y / size[1]
    h = size[2] * math.sqrt(max(0.0, 1 - dx * dx - dy * dy))
    p = base + across * x + up * y + n * h
    normal = (across * (x / size[0] ** 2) + up * (y / size[1] ** 2) + n * (h / size[2] ** 2)).normalized()
    body.sphere(black, p, (r, r, 0.007), rotation=normal.to_track_quat("Z", "Y").to_matrix().to_4x4(), segments=12, rings=6)
for side in (-1, 1):
    on_body(body, strap, [(side * 150, 1.26), (side * 95, 1.39), (side * 38, 1.33), (side * 45, 1.22),
                          (side * 95, 1.17), (side * 145, 1.08)], 0.013, out=TOP_OUT + 0.016)
body.build()
export(__file__)
