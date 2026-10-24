# Mushroom Pocket Hoodie (Top, shop): a cozy mint hoodie with a hood down the back, drawstrings and a front pocket with a little red spotted mushroom on it.
# Run from the repository root (writes mushroom_hoodie.glb next to this script):
#   blender -b --factory-startup --python modules/Wardrobe/Models/Clothes/mushroom_hoodie.py
# Shapes and rig sizes: ../kit.py.
import math
import os
import sys

sys.dont_write_bytecode = True  # no __pycache__ next to kit.py
sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), ".."))
from kit import *  # noqa: E402,F401,F403

reset()
mint = material("Cloth", (0.55, 0.85, 0.72))
trim = material("Trim", (0.42, 0.72, 0.6))
red = material("Cap", (0.92, 0.25, 0.25))
spot = material("Spots", (1.0, 0.97, 0.92))
stem = material("Stem", (1.0, 0.92, 0.8))
string = material("String", (1.0, 0.97, 0.92))
body = Part("Body")
shirt(body, mint, puff=0.03, sleeve_mat=mint)
for side in (-1, 1):
    body.torus(trim, (side * 0.21, 0, NECK_Z - 0.18), 0.06, 0.015, rotation=Matrix.Rotation(math.radians(90), 4, "Y"))
# The hood, lying down the back of the neck.
body.sphere(mint, (0, -0.17, NECK_Z - 0.02), (0.2, 0.11, 0.12))
body.torus(trim, (0, -0.06, NECK_Z - 0.01), 0.15, 0.028, scale=(1, 0.75))
# Drawstrings.
for side in (-1, 1):
    a = on_chest(side * 14, NECK_Z - 0.04, 0.035)
    b = on_chest(side * 16, NECK_Z - 0.16, 0.04)
    body.capsule(string, a, b, 0.009, segments=8)
    body.sphere(string, b, (0.017, 0.017, 0.02), segments=10, rings=6)
# The pocket and its mushroom.
pocket = on_chest(0, 0.43, 0.025)
body.box(trim, pocket, (0.12, 0.03, 0.06))
body.sphere(stem, pocket + Vector((0, 0.035, -0.01)), (0.022, 0.012, 0.03), segments=12, rings=6)
body.sphere(red, pocket + Vector((0, 0.04, 0.025)), (0.05, 0.016, 0.032), cut=(pocket + Vector((0, 0, 0.012)), (0, 0, -1)),
            segments=20, rings=10)
for dx, dz in ((-0.022, 0.035), (0.02, 0.04), (0.0, 0.05)):
    body.sphere(spot, pocket + Vector((dx, 0.057, dz)), (0.008, 0.004, 0.008), segments=8, rings=4)
body.build()
export(__file__)
