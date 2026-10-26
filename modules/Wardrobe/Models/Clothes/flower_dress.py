# Flower Dress (Top/dress, shop): a butter-yellow dress over the torso, flaring below the hips over the legs' tops, with little white and pink flowers, a white hem and a green sash.
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
rings = dress(body, yellow, OUT)
hem_ring(body, white, rings, 0.625, 0.02)
SASH_Z = 1.1
ring(body, sash, SASH_Z, 0.02, out=OUT)
for side in (-1, 1):
    body.sphere(sash, body_point(180 + side * 18, SASH_Z, OUT + 0.035)[0], (0.045, 0.02, 0.032),
                rotation=body_frame(180 + side * 18, SASH_Z) @ Matrix.Rotation(math.radians(side * 20), 4, "Z"),
                segments=16, rings=8)
for i, (a, z) in enumerate(((10, 1.18), (-50, 1.22), (70, 1.2), (-110, 1.19), (130, 1.24), (-160, 1.2),
                            (190, 1.3), (35, 1.31), (-25, 1.34), (90, 1.3), (-80, 1.33), (20, 1.02), (-130, 1.03))):
    p, n = body_point(a, z, OUT + 0.008)
    flower(body, pink if i % 2 else white, middle, p, n, 0.022)
# Flowers on the skirt (none beside the hands, which hang over its sides).
for i, (a, z) in enumerate(((0, 0.76), (35, 0.86), (-40, 0.84), (150, 0.78), (-150, 0.8), (180, 0.9),
                            (20, 0.68), (-20, 0.7), (165, 0.68), (-165, 0.67), (60, 0.92), (-120, 0.92))):
    p, n = hem_point(rings, a, z, 0.008)
    flower(body, white if i % 2 else pink, middle, p, n, 0.024)
body.build()
export(__file__)
