# Overalls Top (Top, starter; pairs with the Overalls bottom): a sunny yellow tee under a denim bib with two straps and big round buttons.
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
shirt(body, tee)
collar(body, tee)
# The bib over the belly, curved to the chest.
body.sphere(denim, CHEST_C + Vector((0, 0.005, 0)), (CHEST_R[0] + 0.035, CHEST_R[1] + 0.035, CHEST_R[2] + 0.01),
            cut=((0, 0, 0.33), (0, 0, -1)), keep=((0, 0, 0.6), (0, 0, 1)))
body.sphere(denim, CHEST_C + Vector((0, 0.01, 0)), (CHEST_R[0] + 0.04, CHEST_R[1] + 0.04, CHEST_R[2] + 0.01),
            cut=((0.13, 0, 0), (1, 0, 0)), keep=((0, 0.1, 0.6), (0, -1, 0)))
for side in (-1, 1):
    front = on_chest(side * 30, 0.6, 0.04)
    back = on_chest(180 - side * 30, 0.6, 0.04)
    top = Vector((side * 0.13, 0, NECK_Z + 0.015))
    body.capsule(denim, front, top, 0.03, segments=12)
    body.capsule(denim, top, back, 0.03, segments=12)
    body.sphere(button, front + Vector((0, 0.02, 0)), (0.03, 0.015, 0.03), segments=14, rings=7)
body.box(denim, on_chest(0, 0.47, 0.05), (0.07, 0.02, 0.045))
body.build()
export(__file__)
