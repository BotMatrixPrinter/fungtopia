# Flower Dress (Top/dress, shop): a butter-yellow dress over the whole triangle with little white and pink flowers, a white hem and a green sash.
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
OUT = TOP_OUT + 0.002
cloth(body, yellow, out=OUT)
ring(body, white, BODY_BOTTOM + 0.035, 0.022, out=OUT)
ring(body, sash, 0.44, 0.022, out=OUT)
for side in (-1, 1):
    body.sphere(sash, body_point(180 + side * 18, 0.44, OUT + 0.035)[0], (0.05, 0.02, 0.035),
                rotation=body_frame(180 + side * 18, 0.44) @ Matrix.Rotation(math.radians(side * 20), 4, "Z"), segments=16, rings=8)
for i, (a, z) in enumerate(((10, 0.22), (-50, 0.25), (70, 0.2), (-110, 0.22), (130, 0.26), (-160, 0.21),
                            (190, 0.33), (35, 0.34), (-25, 0.52), (90, 0.33), (-80, 0.36), (20, 0.5))):
    p, n = body_point(a, z, OUT + 0.01)
    flower(body, pink if i % 2 else white, middle, p, n, 0.022)
body.build()
export(__file__)
