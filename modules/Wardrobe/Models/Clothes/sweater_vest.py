# Sweater Vest (Top, shop): a white tee with a collar under a sleeveless knitted lilac vest with a V neck and cream argyle diamonds.
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
shirt(body, tee, puff=0.012)
collar(body, tee, minor=0.03)
body.sphere(vest, CHEST_C, (CHEST_R[0] + 0.032, CHEST_R[1] + 0.032, CHEST_R[2] + 0.01),
            cut=((0, 0, 0.33), (0, 0, -1)), keep=((0, 0, NECK_Z - 0.06), (0, 0, 1)))
body.torus(vest, (0, 0, 0.335), on_chest(90, 0.335).x + 0.035, 0.026, scale=(1, CHEST_R[1] / CHEST_R[0]))
for side in (-1, 1):
    body.capsule(vest, on_chest(side * 30, NECK_Z - 0.07, 0.04), on_chest(side * 4, NECK_Z - 0.2, 0.045), 0.016, segments=8)
for x, z in ((-0.1, 0.45), (0.1, 0.45), (0.0, 0.4), (0.0, 0.52)):
    p = on_chest(math.degrees(math.atan2(x, 0.22)), z, 0.04)
    body.box(diamond, p, (0.03, 0.01, 0.03), rotation=Matrix.Rotation(math.radians(45), 4, "Y"))
body.build()
export(__file__)
