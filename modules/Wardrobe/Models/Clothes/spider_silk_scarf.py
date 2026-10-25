# Spider Silk Scarf (Face, reward for the first Mushroom Tarantula): a soft shimmering lilac-white scarf wrapped round the triangle's top, with web threads and two tails hanging down the front.
# Run from the repository root (writes spider_silk_scarf.glb next to this script):
#   blender -b --factory-startup --python modules/Wardrobe/Models/Clothes/spider_silk_scarf.py
# Shapes and rig sizes: ../kit.py.
import math
import os
import sys

sys.dont_write_bytecode = True  # no __pycache__ next to kit.py
sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), ".."))
from kit import *  # noqa: E402,F401,F403

reset()
silk = material("Silk", (0.93, 0.9, 1.0))
web = material("Web", (0.7, 0.62, 0.9))
body = Part("Body")
z = 0.53
ring(body, silk, z, 0.045, out=0.0)
# Web threads round it, over its outside.
for dz in (-0.026, 0.026):
    ring(body, web, z + dz, 0.006, out=0.07)
for side, low in ((-1, 0.36), (1, 0.4)):
    on_body(body, silk, [(side * 14, z - 0.02), (side * 17, (z + low) / 2), (side * 20, low)], 0.034, out=0.002)
    # A web thread wound round the tail's end.
    c, n = body_point(side * 20, low + 0.03, 0.002 + 0.034 + 0.003)
    across = (body_frame(side * 20, low + 0.03) @ Vector((1, 0, 0, 0))).to_3d()
    body.tube(web, [c + (n * math.cos(math.radians(f)) + across * math.sin(math.radians(f))) * 0.04 for f in range(-80, 81, 20)],
              0.006)
body.sphere(silk, body_point(0, z - 0.01, 0.07)[0], (0.055, 0.04, 0.045), rotation=body_frame(0, z - 0.01), segments=16, rings=8)
body.build()
export(__file__)
