# Flower Dress (Top/dress, shop): a butter-yellow dress with little white and pink flowers, puffy sleeves and a green sash.
# Run from the repository root (writes flower_dress.glb next to this script):
#   blender -b --factory-startup --python modules/Wardrobe/Models/Clothes/flower_dress.py
# Shapes and rig sizes: ../kit.py.
import math
import os
import sys

sys.dont_write_bytecode = True  # no __pycache__ next to kit.py
sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), ".."))
from kit import *  # noqa: E402,F401,F403

reset()
yellow = material("Cloth", (1.0, 0.88, 0.5))
white = material("Petal", (1.0, 0.98, 0.95))
pink = material("PetalPink", (1.0, 0.6, 0.72))
middle = material("FlowerMiddle", (1.0, 0.7, 0.25))
sash = material("Sash", (0.45, 0.78, 0.45))
body = Part("Body")
bottom, top = HIP_Z - 0.08, NECK_Z + 0.01
body.cone(yellow, (0, 0, bottom), 0.34, 0.15, top - bottom, segments=40)
body.sphere(yellow, (0, 0, top - 0.01), (0.16, 0.15, 0.06), segments=24, rings=8)
body.torus(white, (0, 0, bottom), 0.34, 0.025, segments=48)
f = (0.5 - bottom) / (top - bottom)
body.torus(sash, (0, 0, 0.5), 0.34 + (0.15 - 0.34) * f + 0.012, 0.024, segments=40)
for side in (-1, 1):
    body.sphere(yellow, (side * 0.19, 0, NECK_Z - 0.12), (0.085, 0.085, 0.075))
for i, (a, z) in enumerate(((10, 0.3), (-50, 0.33), (70, 0.28), (-110, 0.3), (130, 0.34), (-160, 0.29),
                            (190, 0.4), (35, 0.42), (-25, 0.6), (90, 0.4), (-80, 0.42))):
    p = cone_surface(top, bottom, 0.15, 0.34, a, z, out=0.012)
    flower(body, pink if i % 2 else white, middle, p, (p.x, p.y, 0.35), 0.022)
body.build()
export(__file__)
