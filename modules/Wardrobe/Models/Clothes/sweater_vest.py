# Sweater Vest (Top, shop): a white tee over the body under a knitted lilac vest with a V neck and cream argyle diamonds.
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
for body in bodies():
    cloth(body, tee, TOP_HEM)
    ring(body, tee, at(0.9), 0.014)
    # The vest (just under the bow's strap) up to the chest.
    VEST_OUT = TOP_OVER
    vest_top = at(0.71)
    cloth(body, vest, TOP_HEM + 0.008, vest_top, out=VEST_OUT)
    ring(body, vest, TOP_HEM + 0.014, 0.013, out=VEST_OUT)
    # The ribbed edge round its top, dipping into a V in front.
    on_body(body, vest, [(0, at(0.52)), (40, at(0.66)), (90, vest_top - 0.006), (180, vest_top - 0.006),
                         (270, vest_top - 0.006), (320, at(0.66))], 0.012, out=TOP_OUT, closed=True, steps=8)
    for x, f in ((-0.055, 0.44), (0.055, 0.44), (-0.09, 0.57), (0.09, 0.57), (0.0, 0.36)):
        z = at(f)
        a = math.degrees(math.atan2(x, surface_r(z)))
        p, _ = body_point(a, z, VEST_OUT + 0.003)
        body.box(diamond, p, (0.02, 0.02, 0.006), rotation=body_frame(a, z) @ Matrix.Rotation(math.radians(45), 4, "Z"))
    body.build()
export(__file__)
