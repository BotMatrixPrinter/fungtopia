# Spider Silk Scarf (Face, reward for the first Mushroom Tarantula): a soft shimmering lilac-white scarf wrapped round the torso's top, with web threads and two tails hanging down the front.
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
z = 1.385
R = 0.028
ring(body, silk, z, R, out=TOP_OUT)
# Web threads round it, lying on its outside above and below its middle.
for tilt in (-55, 55):
    pts = []
    for k in range(48):
        c, n = body_point(k / 48 * 360, z, TOP_OUT + R + 0.003)
        up = (body_frame(k / 48 * 360, z) @ Vector((0, 1, 0, 0))).to_3d()
        pts.append(c + (n * math.cos(math.radians(tilt)) + up * math.sin(math.radians(tilt))) * (R + 0.003))
    body.tube(web, pts, 0.005, closed=True, ring=8)
for side, low in ((-1, 1.17), (1, 1.21)):
    on_body(body, silk, [(side * 14, z - 0.03), (side * 17, (z + low) / 2), (side * 20, low)], 0.028, out=TOP_OUT + 0.006)
    # A web thread wound round the tail's end.
    c, n = body_point(side * 20, low + 0.025, TOP_OUT + 0.006 + 0.028 + 0.003)
    across = (body_frame(side * 20, low + 0.025) @ Vector((1, 0, 0, 0))).to_3d()
    body.tube(web, [c + (n * math.cos(math.radians(f)) + across * math.sin(math.radians(f))) * 0.033 for f in range(-80, 81, 20)],
              0.005)
body.sphere(silk, body_point(0, z - 0.03, TOP_OUT + 0.05)[0], (0.042, 0.034, 0.034), rotation=body_frame(0, z - 0.03),
            segments=16, rings=8)
body.build()
export(__file__)
