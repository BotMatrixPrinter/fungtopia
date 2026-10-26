# Sweater Vest (Top, shop): a white tee over the triangle under a knitted lilac vest with a V neck and cream argyle diamonds.
# Run from the repository root (writes sweater_vest.glb next to this script):
#   blender -b --factory-startup --python modules/Wardrobe/Models/Clothes/sweater_vest.py
# Shapes and rig sizes: ../kit.py.
import math
import os
import sys

sys.dont_write_bytecode = True  # no __pycache__ next to kit.py
sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), ".."))
from kit import *  # noqa: E402,F401,F403

reset()
tee = material("Cloth", (1.0, 0.98, 0.95))
vest = material("Knit", (0.66, 0.55, 0.85))
diamond = material("Argyle", (1.0, 0.93, 0.78))
body = Part("Body")
cloth(body, tee, TOP_HEM)
ring(body, tee, 0.56, 0.02)
VEST_OUT = TOP_OUT + 0.012
cloth(body, vest, TOP_HEM + 0.01, 0.47, out=VEST_OUT)
ring(body, vest, TOP_HEM + 0.016, 0.018, out=VEST_OUT)
# The ribbed edge round its top, dipping into a V in front.
on_body(body, vest, [(0, 0.38), (40, 0.45), (90, 0.465), (180, 0.47), (270, 0.465), (320, 0.45)], 0.016,
        out=TOP_OUT, closed=True, steps=8)
for x, z in ((-0.08, 0.34), (0.08, 0.34), (-0.13, 0.41), (0.13, 0.41), (0.0, 0.3)):
    a = math.degrees(math.atan2(x, surface_r(z)))
    p, _ = body_point(a, z, VEST_OUT + 0.004)
    body.box(diamond, p, (0.028, 0.028, 0.008), rotation=body_frame(a, z) @ Matrix.Rotation(math.radians(45), 4, "Z"))
body.build()
export(__file__)
