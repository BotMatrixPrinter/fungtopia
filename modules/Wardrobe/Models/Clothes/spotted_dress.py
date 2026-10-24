# Spotted Mushroom Dress (Top/dress, starter): a red triangle dress flaring over the legs with white spots like a mushroom cap and a cream collar.
# Run from the repository root (writes spotted_dress.glb next to this script):
#   blender -b --factory-startup --python modules/Wardrobe/Models/Clothes/spotted_dress.py
# Shapes and rig sizes: ../kit.py.
import math
import os
import sys

sys.dont_write_bytecode = True  # no __pycache__ next to kit.py
sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), ".."))
from kit import *  # noqa: E402,F401,F403

reset()
red = material("Cloth", (0.92, 0.28, 0.32))
spot = material("Spots", (1.0, 0.96, 0.9))
cream = material("Collar", (1.0, 0.92, 0.8))
body = Part("Body")
bottom, top = HIP_Z - 0.09, NECK_Z + 0.01
body.cone(red, (0, 0, bottom), 0.35, 0.14, top - bottom, segments=40)
body.sphere(red, (0, 0, top - 0.01), (0.15, 0.14, 0.06), segments=24, rings=8)
body.torus(red, (0, 0, bottom), 0.35, 0.03, segments=48)
collar(body, cream, minor=0.03)
for a, z in ((20, 0.3), (-40, 0.27), (75, 0.34), (-95, 0.28), (140, 0.31), (-150, 0.33), (5, 0.47),
             (-70, 0.46), (110, 0.49), (180, 0.45), (40, 0.6), (-30, 0.62)):
    p = cone_surface(top, bottom, 0.14, 0.35, a, z)
    n = Vector((p.x, p.y, 0.4)).normalized()
    body.sphere(spot, p, (0.04, 0.04, 0.015), rotation=n.to_track_quat("Z", "Y").to_matrix().to_4x4(), segments=14, rings=6)
for side in (-1, 1):
    body.sphere(red, (side * 0.18, 0, NECK_Z - 0.12), (0.075, 0.075, 0.07))
body.build()
export(__file__)
