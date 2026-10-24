# Curly: a fluffy cloud of round curls over the top, sides and back of the head.
# Run from the repository root (writes curly.glb next to this script):
#   blender -b --factory-startup --python modules/Wardrobe/Models/Hair/curly.py
# Shapes and rig sizes: ../kit.py.
import math
import os
import sys

sys.dont_write_bytecode = True  # no __pycache__ next to kit.py
sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), ".."))
from kit import *  # noqa: E402,F401,F403

reset()
hair = material("Hair", (0.3, 0.18, 0.1))
head = Part("Head")
hair_cap(head, hair, fringe=False)
for ring, (el, count) in enumerate(((75, 4), (45, 9), (15, 12), (-15, 10))):
    for k in range(count):
        t = math.radians(k / count * 360 + ring * 17)
        e = math.radians(el)
        d = Vector((math.sin(t) * math.cos(e), math.cos(t) * math.cos(e), math.sin(e)))
        if d.y > 0.55 and el < 40:
            continue  # leave the face open
        head.sphere(hair, HEAD_C + Vector((0, -0.02, 0.03)) + d * HEAD_R * 1.08, (0.1, 0.1, 0.1), segments=14, rings=7)
for x in (-0.18, 0.0, 0.18):
    head.sphere(hair, HEAD_C + Vector((x, HEAD_R * 0.78, 0.25)), (0.09, 0.07, 0.08), segments=14, rings=7)
head.build()
export(__file__)
