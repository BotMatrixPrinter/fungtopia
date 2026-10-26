# Mary-Janes (Shoes, starter): round shiny cherry-red shoes over the rig's shoes, with a strap, a button and white socks with a frilly cuff round the ankle.
# Run from the repository root (writes mary_janes.glb next to this script):
#   blender -b --factory-startup --python modules/Wardrobe/Models/Clothes/mary_janes.py
# Shapes and rig sizes: ../kit.py.
import math
import os
import sys

sys.dont_write_bytecode = True  # no __pycache__ next to kit.py
sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), ".."))
from kit import *  # noqa: E402,F401,F403

reset()
red = material("Cloth", (0.8, 0.15, 0.22))
sole = material("Sole", (0.4, 0.2, 0.18))
sock = material("Sock", (1.0, 0.98, 0.95))
button = material("Button", (1.0, 0.85, 0.4))
SIZE = (0.084, 0.134, 0.075)
for name in ("LegL", "LegR"):
    leg = Part(name)
    x = leg.pivot.x
    c = shoe(leg, red, sole, size=SIZE)
    # The sock round the ankle, with its frill.
    leg_tube(leg, sock, 0.06, 0.17, out=0.004)
    leg_cuff(leg, sock, 0.17, 0.012, out=0.004)
    # The strap over the instep, a button at its outer end.
    dy = 0.045
    k = math.sqrt(1 - (dy / SIZE[1]) ** 2)
    strap_x, strap_z = SIZE[0] * k + 0.006, SIZE[2] * k + 0.006
    leg.torus(red, c + Vector((0, dy, 0)), strap_z, 0.009, arc=0.5, segments=24, scale=(1, strap_x / strap_z),
              rotation=Matrix.Rotation(math.radians(90), 4, "X") @ Matrix.Rotation(math.radians(90), 4, "Z"))
    side = 1 if x > 0 else -1
    leg.sphere(button, c + Vector((side * (strap_x + 0.004), dy, 0.012)), (0.008, 0.014, 0.014), segments=10, rings=5)
    leg.build()
export(__file__)
