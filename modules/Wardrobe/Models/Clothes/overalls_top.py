# Overalls Top (Top, starter; pairs with the Overalls bottom): a sunny yellow tee over the body under a denim bib with two straps over the top and big round buttons.
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
for body in bodies():
    cloth(body, tee, TOP_HEM)
    ring(body, tee, at(0.9), 0.013)
    # The bib over the belly: the front of a band from under the bottom's waist up to below the
    # bow's strap (over the bottoms' band, so the overalls read as one).
    BIB_OUT = BOTTOM_OUT + 0.004
    bib_top = at(0.47)
    half = 0.075
    cloth(body, denim, TOP_HEM, bib_top, out=BIB_OUT,
          cuts=(((half, 0, 0), (1, 0, 0)), ((-half, 0, 0), (-1, 0, 0)), ((0, 0, 0), (0, -1, 0))))
    top_angle = math.degrees(math.asin(half / (surface_r(bib_top) + BIB_OUT)))
    on_body(body, denim, [(-top_angle, bib_top), (0, bib_top), (top_angle, bib_top)], 0.009, out=BIB_OUT - 0.003)
    body.box(denim, body_point(0, at(0.36), BIB_OUT + 0.009)[0], (0.035, 0.026, 0.006), rotation=body_frame(0, at(0.36)))
    # The straps: from the bib's top corners up over the top and down the back.
    for side in (-1, 1):
        on_body(body, denim, [(side * top_angle * 0.9, bib_top - 0.01), (side * 38, at(0.7)), (side * 70, at(0.86)),
                              (side * 110, at(0.86)), (side * 145, at(0.7)), (side * 162, at(0.5)),
                              (side * 165, at(0.32))], 0.013, out=TOP_OUT)
        a = side * top_angle * 0.85
        body.sphere(button, body_point(a, bib_top - 0.012, BIB_OUT + 0.01)[0], (0.019, 0.019, 0.009),
                    rotation=body_frame(a, bib_top - 0.012), segments=14, rings=7)
    body.build()
export(__file__)
