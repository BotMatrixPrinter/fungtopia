# Overalls Top (Top, starter; pairs with the Overalls bottom): a sunny yellow tee over the triangle under a denim bib with two straps over the top and big round buttons.
# Run from the repository root (writes overalls_top.glb next to this script):
#   blender -b --factory-startup --python modules/Wardrobe/Models/Clothes/overalls_top.py
# Shapes and rig sizes: ../kit.py.
import math
import os
import sys

sys.dont_write_bytecode = True  # no __pycache__ next to kit.py
sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), ".."))
from kit import *  # noqa: E402,F401,F403

reset()
tee = material("Cloth", (1.0, 0.85, 0.4))
denim = material("Denim", (0.32, 0.5, 0.82))
button = material("Button", (1.0, 0.8, 0.3))
body = Part("Body")
cloth(body, tee, TOP_HEM)
ring(body, tee, 0.56, 0.018)
# The bib over the belly: the front of a band from under the bottom's waist up to the chest.
BIB_OUT = BOTTOM_OUT + 0.004
cloth(body, denim, TOP_HEM, 0.43, out=BIB_OUT, cuts=(((0.1, 0, 0), (1, 0, 0)), ((-0.1, 0, 0), (-1, 0, 0)), ((0, 0, 0), (0, -1, 0))))
on_body(body, denim, [(-27, 0.43), (0, 0.43), (27, 0.43)], 0.012, out=BIB_OUT - 0.004)
body.box(denim, body_point(0, 0.36, BIB_OUT + 0.012)[0], (0.05, 0.035, 0.008), rotation=body_frame(0, 0.36))
# The straps: from the bib's top corners up over the top and down the back.
for side in (-1, 1):
    on_body(body, denim, [(side * 26, 0.42), (side * 40, 0.52), (side * 90, 0.575), (side * 140, 0.52), (side * 160, 0.4),
                          (side * 165, 0.3)], 0.018, out=TOP_OUT)
    body.sphere(button, body_point(side * 24, 0.415, BIB_OUT + 0.012)[0], (0.026, 0.026, 0.012),
                rotation=body_frame(side * 24, 0.415), segments=14, rings=7)
body.build()
export(__file__)
