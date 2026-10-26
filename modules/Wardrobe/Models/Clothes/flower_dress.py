# Flower Dress (Top/dress, girl, shop): a butter-yellow dress over the whole cone with little white and pink flowers, a white hem and a green sash.
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
for body in bodies(both=False):
    OUT = TOP_OUT + 0.002
    cloth(body, yellow, out=OUT)
    ring(body, white, widest()[1] - 0.012, 0.016, out=OUT)
    z = at(0.65)
    ring(body, sash, z, 0.016, out=OUT)
    for side in (-1, 1):
        body.sphere(sash, body_point(180 + side * 22, z, OUT + 0.026)[0], (0.036, 0.015, 0.026),
                    rotation=body_frame(180 + side * 22, z) @ Matrix.Rotation(math.radians(side * 20), 4, "Z"),
                    segments=16, rings=8)
    for i, (a, f) in enumerate(((10, 0.19), (-50, 0.25), (70, 0.15), (-110, 0.19), (130, 0.27), (-160, 0.17),
                                (190, 0.42), (35, 0.44), (-25, 0.8), (90, 0.42), (-80, 0.48), (20, 0.76))):
        p, n = body_point(a, at(f), OUT + 0.008)
        flower(body, pink if i % 2 else white, middle, p, n, 0.019 - 0.006 * f)
    body.build()
export(__file__)
