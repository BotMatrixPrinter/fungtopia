# Sweater Vest (Top, shop): a white tee over the torso under a knitted lilac vest with a V neck and cream argyle diamonds.
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
ring(body, tee, 1.405, 0.016)
VEST_OUT = TOP_OUT + 0.012
# The vest ends above the bottom's waist band (it sits further out than the band).
VEST_HEM = BAND_TOP + 0.03
cloth(body, vest, VEST_HEM, 1.3, out=VEST_OUT)
ring(body, vest, VEST_HEM + 0.006, 0.016, out=VEST_OUT - 0.008)
# The ribbed edge round its top, dipping into a V in front.
on_body(body, vest, [(0, 1.17), (40, 1.27), (90, 1.3), (180, 1.3), (270, 1.3), (320, 1.27)], 0.013,
        out=VEST_OUT - 0.01, closed=True, steps=8)
for a, z in ((-28, 1.13), (28, 1.13), (-55, 1.21), (55, 1.21), (0, 1.1), (-80, 1.12), (80, 1.12)):
    p, _ = body_point(a, z, VEST_OUT + 0.004)
    body.box(diamond, p, (0.026, 0.026, 0.008), rotation=body_frame(a, z) @ Matrix.Rotation(math.radians(45), 4, "Z"))
body.build()
export(__file__)
