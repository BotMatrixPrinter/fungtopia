# Beanie (Head, starter): a snug knitted teal beanie with a folded brim and a fluffy pompom.
# Run from the repository root (writes beanie.glb next to this script):
#   blender -b --factory-startup --python modules/Wardrobe/Models/Clothes/beanie.py
# Shapes and rig sizes: ../kit.py.
import math
import os
import sys

sys.dont_write_bytecode = True  # no __pycache__ next to kit.py
sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), ".."))
from kit import *  # noqa: E402,F401,F403

reset()
teal = material("Knit", (0.3, 0.7, 0.75))
brim = material("Brim", (0.22, 0.58, 0.65))
pom = material("Pompom", (1.0, 0.95, 0.88))
head = Part("Head")
c = HEAD_C + Vector((0, -0.02, 0.05))
head.sphere(teal, c, (HEAD_R * 1.12, HEAD_R * 1.1, HEAD_R * 1.08), cut=((0, 0, HEAD_C.z + 0.12), (0, 0, -1)), segments=40, rings=20)
head.torus(brim, (0, -0.02, HEAD_C.z + 0.14), 0.42, 0.05, segments=48, scale=(1, 0.98))
for k in range(12):
    t = k / 12 * 2 * math.pi
    head.capsule(brim, (math.sin(t) * 0.4, math.cos(t) * 0.39 - 0.02, HEAD_C.z + 0.22),
                 (math.sin(t) * 0.27, math.cos(t) * 0.26 - 0.02, HEAD_C.z + 0.42), 0.012, segments=6)
head.sphere(pom, (0, -0.02, HEAD_C.z + 0.53), (0.1, 0.1, 0.09), segments=20, rings=10)
head.build()
export(__file__)
