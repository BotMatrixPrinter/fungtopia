# Spider Silk Scarf (Face, reward for the first Mushroom Tarantula): a soft shimmering lilac-white scarf around the neck with web threads and two tails in front.
# Run from the repository root (writes spider_silk_scarf.glb next to this script):
#   blender -b --factory-startup --python modules/Wardrobe/Models/Clothes/spider_silk_scarf.py
# Shapes and rig sizes: ../kit.py.
import math
import os
import sys

sys.dont_write_bytecode = True  # no __pycache__ next to kit.py
sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), ".."))
from kit import *  # noqa: E402,F401,F403

reset()
silk = material("Silk", (0.93, 0.9, 1.0))
web = material("Web", (0.7, 0.62, 0.9))
body = Part("Body")
z = NECK_Z - 0.01
body.torus(silk, (0, 0, z), 0.15, 0.05, segments=40, scale=(1, 0.9))
for k in range(10):
    t = k / 10 * 2 * math.pi
    body.torus(web, (math.sin(t) * 0.15, math.cos(t) * 0.135, z), 0.05, 0.006,
               rotation=Matrix.Rotation(-t, 4, "Z") @ Matrix.Rotation(math.radians(90), 4, "Y"), segments=12)
for side, length in ((-1, 0.2), (1, 0.15)):
    a = Vector((side * 0.05, 0.16, z - 0.02))
    b = on_chest(side * 18, z - length, 0.04)
    body.capsule(silk, a, b, 0.04, segments=12)
    body.torus(web, b + Vector((0, 0.01, 0)), 0.04, 0.007, rotation=Matrix.Rotation(math.radians(70), 4, "X"), segments=16)
body.sphere(silk, (0, 0.17, z - 0.02), (0.06, 0.045, 0.05), segments=16, rings=8)
body.build()
export(__file__)
