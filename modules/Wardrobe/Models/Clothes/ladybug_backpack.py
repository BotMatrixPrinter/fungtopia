# Ladybug Backpack (Back, reward for the first Mushroom Ladybug): a round red shell with black spots and a black top on the body's back, with black straps over the top; no face (only creatures have eyes).
# Run from the repository root (writes ladybug_backpack.glb next to this script):
#   blender -b --factory-startup --python modules/Wardrobe/Models/Clothes/ladybug_backpack.py
# Shapes and rig sizes: ../kit.py.
import math
import os
import sys

sys.dont_write_bytecode = True  # no __pycache__ next to kit.py
sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), ".."))
from kit import *  # noqa: E402,F401,F403

def straps(body, strap, radius, back_top, back_low):
    """The two straps: from the bag's top over the shoulders, down the front and back under the
    arms (clear of the hands) to the bag's bottom."""
    for side in (-1, 1):
        on_body(body, strap, [(180 + side * 30, back_top), (side * 115, at(0.9)), (side * 40, at(0.82)),
                              (side * 55, at(0.5)), (side * 115, at(0.4)), (180 + side * 40, back_low)],
                radius, out=TOP_OUT + 0.006)


reset()
red = material("Shell", (0.92, 0.18, 0.2))
black = material("Black", (0.12, 0.1, 0.12))
strap = material("Strap", (0.25, 0.2, 0.22))
for body in bodies():
    z = at(0.47)
    rot = body_frame(180, z)
    base, n = body_point(180, z, TOP_OUT + 0.006)
    size = (0.12, 0.13, 0.085)  # across, up, out
    body.sphere(red, base, size, rotation=rot, cut=(base, -n), segments=32, rings=16)
    up = (rot @ Vector((0, 1, 0, 0))).to_3d()
    across = (rot @ Vector((1, 0, 0, 0))).to_3d()
    body.sphere(black, base + up * 0.108 + n * 0.036, (0.072, 0.043, 0.043), rotation=rot, segments=20, rings=10)
    # The black line down the middle of the shell.
    body.tube(black, [base + up * (size[1] * math.sin(math.radians(t))) + n * (size[2] * math.cos(math.radians(t)) + 0.003)
                      for t in range(-80, 81, 10)], 0.005)
    for x, y, r in ((-0.057, 0.036, 0.023), (0.057, 0.036, 0.023), (-0.054, -0.058, 0.02), (0.054, -0.058, 0.02),
                    (-0.09, -0.014, 0.016), (0.09, -0.014, 0.016)):
        dx, dy = x / size[0], y / size[1]
        h = size[2] * math.sqrt(max(0.0, 1 - dx * dx - dy * dy))
        p = base + across * x + up * y + n * h
        normal = (across * (x / size[0] ** 2) + up * (y / size[1] ** 2) + n * (h / size[2] ** 2)).normalized()
        body.sphere(black, p, (r, r, 0.006), rotation=normal.to_track_quat("Z", "Y").to_matrix().to_4x4(), segments=12, rings=6)
    straps(body, strap, 0.01, at(0.66), at(0.3))
    body.build()
export(__file__)
