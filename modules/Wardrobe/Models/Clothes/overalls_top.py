# Overalls Top (Top, starter; pairs with the Overalls bottom): a sunny yellow tee over the torso under a denim bib with two straps over the shoulders and big round buttons.
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
ring(body, tee, 1.405, 0.016)
# The bib over the belly: the front of a band from under the bottom's waist up to the chest.
BIB_OUT = BOTTOM_OUT + 0.004
BIB_TOP = 1.22
cloth(body, denim, TOP_HEM, BIB_TOP, out=BIB_OUT,
      cuts=(((0.1, 0, 0), (1, 0, 0)), ((-0.1, 0, 0), (-1, 0, 0)), ((0, 0, 0), (0, -1, 0))))
on_body(body, denim, [(-36, BIB_TOP), (0, BIB_TOP), (36, BIB_TOP)], 0.011, out=BIB_OUT - 0.004)
body.box(denim, body_point(0, 1.14, BIB_OUT + 0.01)[0], (0.045, 0.032, 0.008), rotation=body_frame(0, 1.14))
# The straps: from the bib's top corners up over the shoulders and down the back.
for side in (-1, 1):
    on_body(body, denim, [(side * 32, BIB_TOP - 0.01), (side * 38, 1.32), (side * 70, 1.405), (side * 120, 1.39),
                          (side * 150, 1.3), (side * 160, 1.15), (side * 162, 1.0)], 0.016, out=TOP_OUT)
    body.sphere(button, body_point(side * 31, BIB_TOP - 0.012, BIB_OUT + 0.012)[0], (0.022, 0.022, 0.01),
                rotation=body_frame(side * 31, BIB_TOP - 0.012), segments=14, rings=7)
body.build()
export(__file__)
