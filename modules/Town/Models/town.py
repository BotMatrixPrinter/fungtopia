# Builds Mushroom Town's buildings and props and exports them next to this script as town.glb.
# Run from the repository root:
#   blender -b --factory-startup --python modules/Town/Models/town.py
#
# One object per Fungtopia.Town TownPiece: PlayerHouse (12 x 12 m, 10 m tall), MillieHouse
# (10 x 10 x 8), Clinic (12 x 10 x 8.5), FurnitureShop (7 x 4 x 3.5), Fountain (7 x 7 x 2.2),
# House0..House3 (9 x 9 x 7, four looks), ClothingShop (10 x 9 x 8), FoodStall (4.2 x 2.4 x 3), StreetLamp, Bench, Fence (one 3.5 m segment),
# Planter, Crate and Flowers (a small clump): each the size of its footprint in the Map Contract, standing on the ground at the
# origin (a little foundation below it, for uneven ground), its front (door, counter, seat)
# toward Blender +Y (Godot -Z, a building's front at rotation 0). Chunky and cute; no eyes or
# faces on anything (only creatures have them). Materials carry plain colors only; the game draws
# them toon-shaded with ink outlines, and makes Glow (lamp bulbs) and Window light up at night.
import math
import os

import bmesh
import bpy
from mathutils import Matrix

HERE = os.path.dirname(os.path.abspath(__file__))

COLORS = {
    "Wall": (1.0, 0.93, 0.78),
    "WallWhite": (0.97, 0.97, 0.95),
    "Stone": (0.72, 0.7, 0.66),
    "StoneDark": (0.56, 0.54, 0.52),
    "Wood": (0.72, 0.46, 0.28),
    "WoodDark": (0.5, 0.3, 0.18),
    "Cap": (0.9, 0.26, 0.22),
    "CapPink": (0.98, 0.62, 0.74),
    "CapPinkInner": (1.0, 0.82, 0.86),
    "CapWhite": (0.99, 0.98, 0.96),
    "CapOrange": (0.98, 0.56, 0.24),
    "CapLilac": (0.7, 0.56, 0.9),
    "CapBlue": (0.42, 0.66, 0.94),
    "CapYellow": (0.98, 0.8, 0.3),
    "Gills": (0.96, 0.86, 0.7),
    "Spots": (1.0, 0.97, 0.88),
    "SpotsPale": (0.92, 0.9, 0.94),
    "Cross": (0.92, 0.18, 0.2),
    "DoorRed": (0.82, 0.32, 0.26),
    "DoorBlue": (0.44, 0.6, 0.86),
    "DoorGreen": (0.44, 0.7, 0.44),
    "Knob": (1.0, 0.82, 0.36),
    "Window": (1.0, 0.86, 0.5),
    "Mat": (0.86, 0.58, 0.32),
    "MatStripe": (0.98, 0.84, 0.5),
    "Awning": (0.98, 0.5, 0.58),
    "AwningCream": (1.0, 0.95, 0.86),
    "Cushion": (0.66, 0.78, 0.98),
    "Glow": (1.0, 0.88, 0.5),
    "Metal": (0.3, 0.32, 0.38),
    "Water": (0.46, 0.8, 0.98),
    "Picket": (1.0, 0.97, 0.9),
    "Soil": (0.4, 0.27, 0.18),
    "Leaf": (0.42, 0.74, 0.36),
    "PetalPink": (1.0, 0.6, 0.72),
    "PetalYellow": (1.0, 0.86, 0.32),
    "PetalBlue": (0.56, 0.7, 1.0),
    "PetalWhite": (1.0, 1.0, 0.97),
    "Soot": (0.24, 0.2, 0.22),
    "CapMint": (0.66, 0.9, 0.82),
    "WallPink": (1.0, 0.88, 0.88),
    "DoorLilac": (0.76, 0.62, 0.92),
    "Display": (1.0, 0.95, 0.97),
    "SignText": (0.62, 0.24, 0.46),
    "Hanger": (0.86, 0.68, 0.36),
    "Basket": (0.84, 0.64, 0.38),
    "Muffin": (0.74, 0.47, 0.26),
    "Berry": (0.56, 0.2, 0.52),
    "Soup": (0.96, 0.62, 0.26),
    "Pie": (0.92, 0.72, 0.42),
    "Steam": (0.97, 0.97, 1.0),
}
ORDER = list(COLORS)


def material(name, color):
    mat = bpy.data.materials.new(name)
    mat.use_nodes = True
    bsdf = mat.node_tree.nodes.get("Principled BSDF")
    bsdf.inputs["Base Color"].default_value = (*color, 1.0)
    bsdf.inputs["Roughness"].default_value = 0.7
    mat.diffuse_color = (*color, 1.0)
    return mat


def T(x=0.0, y=0.0, z=0.0):
    return Matrix.Translation((x, y, z))


def S(x, y, z):
    return Matrix.Diagonal((x, y, z, 1.0))


def R(axis, degrees):
    return Matrix.Rotation(math.radians(degrees), 4, axis)


class Piece:
    """One object being built: parts appended to a bmesh, each with its material."""

    def __init__(self, name):
        self.name = name
        self.bm = bmesh.new()

    def add(self, part, mat):
        mesh = bpy.data.meshes.new("tmp")
        part.to_mesh(mesh)
        part.free()
        for poly in mesh.polygons:
            poly.material_index = ORDER.index(mat)
        self.bm.from_mesh(mesh)
        bpy.data.meshes.remove(mesh)

    def finish(self):
        mesh = bpy.data.meshes.new(self.name)
        self.bm.to_mesh(mesh)
        self.bm.free()
        for name in ORDER:
            mesh.materials.append(bpy.data.materials[name])
        for poly in mesh.polygons:
            poly.use_smooth = True
        obj = bpy.data.objects.new(self.name, mesh)
        bpy.context.scene.collection.objects.link(obj)
        return obj


def box(size, matrix, bevel=0.04):
    """A chunky box with rounded edges, `size` (x, y, z) centered at `matrix`."""
    part = bmesh.new()
    bmesh.ops.create_cube(part, size=1.0)
    bmesh.ops.transform(part, matrix=S(*size), verts=part.verts)
    if bevel > 0:
        bevel = min(bevel, min(size) * 0.45)
        bmesh.ops.bevel(part, geom=list(part.edges) + list(part.verts), offset=bevel, segments=3,
                        affect="EDGES", profile=0.5)
    bmesh.ops.transform(part, matrix=matrix, verts=part.verts)
    return part


def cylinder(radius, depth, matrix, segments=28, radius2=None):
    part = bmesh.new()
    bmesh.ops.create_cone(part, cap_ends=True, segments=segments, radius1=radius,
                          radius2=radius if radius2 is None else radius2, depth=depth, matrix=matrix)
    return part


def sphere(radius, matrix, scale=(1, 1, 1), segments=20, rings=10):
    part = bmesh.new()
    bmesh.ops.create_uvsphere(part, u_segments=segments, v_segments=rings, radius=radius,
                              matrix=matrix @ S(*scale))
    return part


def dome(radius, height, matrix, segments=28, rings=12):
    """The upper half of a squashed sphere (a mushroom cap), closed underneath."""
    part = sphere(1.0, Matrix(), segments=segments, rings=rings)
    bmesh.ops.delete(part, geom=[v for v in part.verts if v.co.z < -1e-4], context="VERTS")
    bmesh.ops.contextual_create(part, geom=[e for e in part.edges if e.is_boundary])
    bmesh.ops.transform(part, matrix=matrix @ S(radius, radius, height), verts=part.verts)
    return part


def spots_on_dome(piece, radius, height, base, count=6, size=0.16, mat="Spots", seed=0):
    """Little cream spots sitting on a cap of `radius` x `height` whose base is at `base` (a matrix)."""
    for i in range(count):
        a = 2 * math.pi * (i / count) + seed
        t = 0.55 if i % 2 == 0 else 0.3
        lat = math.acos(t)
        x, y = math.cos(a) * math.sin(lat) * radius, math.sin(a) * math.sin(lat) * radius
        z = math.cos(lat) * height
        r = radius * size * (1.0 if i % 3 else 0.75)
        piece.add(sphere(r, base @ T(x, y, z), scale=(1, 1, 0.45), segments=12, rings=6), mat)
    piece.add(sphere(radius * size * 1.1, base @ T(0, 0, height), scale=(1, 1, 0.4), segments=12, rings=6), mat)


def torus(major, minor, matrix, segments=32, sides=10):
    """A ring in the XY plane around the origin of `matrix`."""
    part = bmesh.new()
    rings = []
    for i in range(segments):
        a = 2 * math.pi * i / segments
        ring = []
        for j in range(sides):
            b = 2 * math.pi * j / sides
            r = major + minor * math.cos(b)
            ring.append(part.verts.new((r * math.cos(a), r * math.sin(a), minor * math.sin(b))))
        rings.append(ring)
    for i in range(segments):
        for j in range(sides):
            a, b = rings[i], rings[(i + 1) % segments]
            part.faces.new((a[j], b[j], b[(j + 1) % sides], a[(j + 1) % sides]))
    bmesh.ops.transform(part, matrix=matrix, verts=part.verts)
    return part


def cap_spots(piece, rx, ry, height, z0, count, size, mat="Spots", seed=0.0):
    """Round spots sitting on a mushroom cap (a half ellipsoid rx x ry x height on z = z0)."""
    for i in range(count):
        a = 2 * math.pi * i / count + seed
        t = 0.62 if i % 2 == 0 else 0.3
        s = math.sqrt(1 - t * t)
        x, y, z = math.cos(a) * s * rx, math.sin(a) * s * ry, z0 + t * height
        tilt = math.degrees(math.acos(t))
        r = size * (1.0 if i % 3 else 0.72)
        rot = R("Z", math.degrees(a) - 90) @ R("X", -tilt)
        piece.add(sphere(r, T(x, y, z) @ rot, scale=(1, 1, 0.3), segments=14, rings=7), mat)
    piece.add(sphere(size * 1.15, T(0, 0, z0 + height), scale=(1, 1, 0.3), segments=14, rings=7), mat)


def on_wall(rx, ry, angle, z, out=0.0):
    """A matrix on an elliptical wall (rx x ry) at `angle` degrees from the front (+Y), facing out."""
    a = math.radians(angle)
    return T(math.sin(a) * (rx + out), math.cos(a) * (ry + out), z) @ R("Z", -angle) @ R("X", -90)


def round_window(piece, rx, ry, angle, z, radius=0.55):
    m = on_wall(rx, ry, angle, z, out=0.02)
    piece.add(cylinder(radius, 0.16, m, segments=24), "Window")
    piece.add(torus(radius + 0.05, 0.1, m @ T(0, 0, 0.06), segments=24, sides=8), "Wood")
    piece.add(box((0.08, radius * 2, 0.06), m @ T(0, 0, 0.09), bevel=0.02), "Wood")
    piece.add(box((radius * 2, 0.08, 0.06), m @ T(0, 0, 0.09), bevel=0.02), "Wood")


def round_door(piece, ry, radius, door_mat, z=0.0):
    """A round door on the front wall: a disc on the ground with a frame, a knob and a step."""
    m = T(0, ry + 0.02, z + radius + 0.06) @ R("X", -90)
    piece.add(cylinder(radius, 0.2, m, segments=32), door_mat)
    piece.add(torus(radius + 0.06, 0.13, m @ T(0, 0, 0.1), segments=32, sides=8), "Wood")
    for k in range(-2, 3):
        piece.add(box((0.05, radius * 1.6, 0.04), m @ T(k * radius * 0.32, 0, 0.11), bevel=0.0), "WoodDark")
    piece.add(sphere(0.11, m @ T(radius * 0.55, 0, 0.18)), "Knob")
    piece.add(box((radius * 2.2, 0.7, 0.18), T(0, ry + 0.35, 0.03), bevel=0.06), "Stone")


def arched_door(piece, ry, width, height, door_mat):
    """A tall door with a round top on the front wall."""
    m = T(0, ry + 0.02, 0) @ R("X", -90)
    straight = height - width / 2
    piece.add(box((width, straight, 0.2), m @ T(0, -straight / 2, 0), bevel=0.03), door_mat)
    piece.add(cylinder(width / 2, 0.2, m @ T(0, -straight, 0), segments=24), door_mat)
    piece.add(sphere(0.09, m @ T(width * 0.32, -straight * 0.55, 0.14)), "Knob")
    piece.add(box((width + 0.3, 0.6, 0.16), T(0, ry + 0.3, 0.03), bevel=0.05), "Stone")


def mushroom_house(name, width, depth, height, cap_mat, spot_mat="Spots", wall_mat="Wall", spots=9):
    """The body of a mushroom house: a fat round stem for walls on a stone foundation, a big
    spotted cap overhanging it with cream gills underneath. Returns (piece, rx, ry, wall_top, cap)."""
    p = Piece(name)
    rx, ry = width / 2 * 0.8, depth / 2 * 0.8
    wall = height * 0.46
    p.add(cylinder(1.0, 0.9, T(0, 0, -0.3) @ S(rx + 0.25, ry + 0.25, 1), segments=40), "Stone")
    p.add(cylinder(1.0, wall + 0.2, T(0, 0, wall / 2 + 0.1) @ S(rx, ry, 1), segments=40, radius2=0.9), wall_mat)
    capx, capy = width / 2 * 1.04, depth / 2 * 1.04
    base = wall - 0.25
    cap_h = height - base
    p.add(cylinder(1.0, 0.3, T(0, 0, base) @ S(capx * 0.97, capy * 0.97, 1), segments=40), "Gills")
    p.add(dome(1.0, 1.0, T(0, 0, base) @ S(capx, capy, cap_h), segments=40, rings=16), cap_mat)
    cap_spots(p, capx, capy, cap_h, base, spots, min(capx, capy) * 0.16, spot_mat, seed=0.4)
    return p, rx, ry, wall, (capx, capy, base, cap_h)


def cap_point(cap, angle, t):
    """A point on a cap (capx, capy, base, height) at `angle` degrees from +Y and height fraction t."""
    capx, capy, base, h = cap
    a = math.radians(angle)
    s = math.sqrt(max(0.0, 1 - t * t))
    return math.sin(a) * s * capx, math.cos(a) * s * capy, base + t * h


def welcome_mat(piece, ry):
    piece.add(box((2.0, 1.1, 0.06), T(0, ry + 1.3, 0.03), bevel=0.03), "Mat")
    for k in (-1, 1):
        piece.add(box((1.7, 0.12, 0.02), T(0, ry + 1.3 + k * 0.3, 0.065), bevel=0.0), "MatStripe")
    piece.add(sphere(0.18, T(0, ry + 1.3, 0.07), scale=(1, 1, 0.12), segments=14, rings=6), "MatStripe")


def window_box(piece, rx, ry, angle, z, flowers=("PetalPink", "PetalYellow")):
    m = on_wall(rx, ry, angle, 0, out=0.25)
    piece.add(box((1.4, 0.4, 0.3), m @ T(0, -z, 0.05), bevel=0.05), "Wood")
    for k in range(4):
        piece.add(sphere(0.13, m @ T(-0.5 + k * 0.33, -z - 0.22, 0.05), segments=10, rings=5), flowers[k % len(flowers)])


def flower(piece, matrix, petal, scale=1.0):
    piece.add(cylinder(0.025 * scale, 0.32 * scale, matrix @ T(0, 0, 0.16 * scale), segments=8), "Leaf")
    top = matrix @ T(0, 0, 0.34 * scale)
    for k in range(5):
        a = 2 * math.pi * k / 5
        piece.add(sphere(0.06 * scale, top @ T(math.cos(a) * 0.07 * scale, math.sin(a) * 0.07 * scale, 0),
                         scale=(1, 1, 0.5), segments=10, rings=5), petal)
    piece.add(sphere(0.045 * scale, top @ T(0, 0, 0.02 * scale), segments=10, rings=5), "PetalYellow" if petal != "PetalYellow" else "PetalWhite")
    piece.add(sphere(0.06 * scale, matrix @ T(0.05 * scale, 0, 0.12 * scale) @ R("Y", 50), scale=(1.4, 0.6, 0.3), segments=10, rings=5), "Leaf")


def player_house():
    # Your house: the biggest, a red spotted cap, a round door, a welcome mat, flower boxes and a
    # little mushroom mailbox. (No window in the cap: it would read as an eye.)
    p, rx, ry, wall, cap = mushroom_house("PlayerHouse", 12, 12, 10, "Cap", spots=11)
    round_door(p, ry, 1.25, "DoorRed")
    welcome_mat(p, ry)
    for angle in (-42, 42):
        round_window(p, rx, ry, angle, 2.3, 0.6)
        window_box(p, rx, ry, angle, 1.45)
    for angle in (-100, 100, 160, -160):
        round_window(p, rx, ry, angle, 2.4, 0.55)
    # A little chimney on the cap.
    x, y, z = cap_point(cap, 140, 0.7)
    p.add(cylinder(0.38, 1.6, T(x, y, z + 0.5), segments=16), "Stone")
    p.add(cylinder(0.46, 0.25, T(x, y, z + 1.3), segments=16), "StoneDark")
    p.add(cylinder(0.3, 0.05, T(x, y, z + 1.43), segments=16), "Soot")
    # Mailbox by the path.
    p.add(cylinder(0.07, 1.0, T(1.9, ry + 1.6, 0.5), segments=10), "Wood")
    p.add(box((0.45, 0.6, 0.35), T(1.9, ry + 1.6, 1.1), bevel=0.1), "Wall")
    p.add(dome(0.42, 0.28, T(1.9, ry + 1.6, 1.25)), "Cap")
    p.add(sphere(0.07, T(2.05, ry + 1.6, 1.47), scale=(1, 1, 0.4), segments=10, rings=5), "Spots")
    p.finish()


def millie_house():
    # Millie's house: a pink cap with two chimneys shaped like cat ears, a round blue door.
    p, rx, ry, wall, cap = mushroom_house("MillieHouse", 10, 10, 8, "CapPink", spots=9)
    round_door(p, ry, 1.05, "DoorBlue")
    for angle in (-45, 45, 110, -110, 180):
        round_window(p, rx, ry, angle, 1.9, 0.5)
    window_box(p, rx, ry, 45, 1.15, ("PetalWhite", "PetalBlue"))
    for side in (-1, 1):
        x, y, z = cap_point(cap, side * 52, 0.72)
        ear = T(x, y, z - 0.2) @ R("Y", side * 18) @ R("Z", side * 8)
        p.add(cylinder(1.2, 2.8, ear @ T(0, 0, 1.4) @ S(1, 0.55, 1), segments=4, radius2=0.25), "CapPink")
        p.add(cylinder(0.75, 2.0, ear @ T(0, 0.45, 1.25) @ S(1, 0.25, 1), segments=4, radius2=0.1), "CapPinkInner")
        p.add(cylinder(0.28, 0.06, ear @ T(0, 0, 2.8) @ S(1, 0.55, 1), segments=12), "Soot")
    welcome_mat(p, ry)
    p.finish()


def red_cross(piece, matrix, size):
    piece.add(box((size, size * 0.32, size * 0.12), matrix, bevel=size * 0.04), "Cross")
    piece.add(box((size * 0.32, size, size * 0.12), matrix, bevel=size * 0.04), "Cross")


def clinic():
    # Dr. Jeremy's clinic: white walls, a white cap with pale spots and a big red cross on top
    # and over the door, and a little sign by the path.
    p, rx, ry, wall, cap = mushroom_house("Clinic", 12, 10, 8.5, "CapWhite", spot_mat="SpotsPale", spots=7)
    arched_door(p, ry, 1.7, 2.4, "DoorBlue")
    for angle in (-40, 40, 100, -100, 160, -160):
        round_window(p, rx, ry, angle, 2.0, 0.5)
    # A red cross on the cap's front and one standing on top.
    x, y, z = cap_point(cap, 0, 0.42)
    red_cross(p, T(x, y + 0.05, z) @ R("X", -65), 2.4)
    red_cross(p, T(0, 0, cap[2] + cap[3] + 0.55) @ R("X", 90), 1.3)
    p.add(cylinder(0.12, 0.4, T(0, 0, cap[2] + cap[3] - 0.1)), "WallWhite")
    red_cross(p, T(0, ry + 0.06, 2.95) @ R("X", 90), 0.7)
    # The little sign: a post with a white board and a red cross.
    sx, sy = 2.6, ry + 1.5
    p.add(cylinder(0.08, 1.4, T(sx, sy, 0.7), segments=10), "Wood")
    p.add(box((1.2, 0.12, 0.8), T(sx, sy, 1.5), bevel=0.06), "WallWhite")
    p.add(box((1.32, 0.08, 0.92), T(sx, sy - 0.03, 1.5), bevel=0.06), "Wood")
    red_cross(p, T(sx, sy + 0.08, 1.5) @ R("X", 90), 0.45)
    welcome_mat(p, ry)
    p.finish()


def shop_chair(piece, matrix):
    for x in (-1, 1):
        for y in (-1, 1):
            piece.add(cylinder(0.03, 0.42, matrix @ T(x * 0.18, y * 0.18, 0.21), segments=8), "WoodDark")
    piece.add(box((0.48, 0.48, 0.07), matrix @ T(0, 0, 0.45)), "Wood")
    piece.add(box((0.4, 0.4, 0.07), matrix @ T(0, 0, 0.51), bevel=0.03), "Cushion")
    piece.add(box((0.07, 0.05, 0.36), matrix @ T(0, -0.2, 0.66)), "Wood")
    piece.add(dome(0.24, 0.18, matrix @ T(0, -0.2, 0.82) @ R("X", -80)), "Cap")


def shop_lamp(piece, matrix, cap_mat):
    piece.add(cylinder(0.14, 0.05, matrix @ T(0, 0, 0.025)), "WoodDark")
    piece.add(cylinder(0.035, 0.6, matrix @ T(0, 0, 0.32)), "Wall")
    piece.add(sphere(0.09, matrix @ T(0, 0, 0.64), scale=(1, 1, 0.8)), "Glow")
    piece.add(dome(0.22, 0.18, matrix @ T(0, 0, 0.66)), cap_mat)


def furniture_shop():
    # The furniture shop's market stall: a wooden counter under a pink and cream striped awning,
    # shelves at the back, chairs and mushroom lamps on display.
    p = Piece("FurnitureShop")
    w, d = 7.0, 4.0
    p.add(box((w, d, 0.5), T(0, 0, -0.1), bevel=0.06), "Wood")
    for x in (-1, 1):
        for y in (-1, 1):
            p.add(cylinder(0.14, 3.0, T(x * (w / 2 - 0.25), y * (d / 2 - 0.25), 1.6), segments=12), "WoodDark")
    p.add(box((w - 0.4, 0.2, 2.6), T(0, -d / 2 + 0.25, 1.45), bevel=0.04), "Wood")
    for z in (0.9, 1.7):
        p.add(box((w - 0.8, 0.6, 0.08), T(0, -d / 2 + 0.6, z), bevel=0.02), "WoodDark")
    # Little things on the back shelves: cushions, a stool, a pot.
    for i, x in enumerate((-2.4, -1.4, -0.4, 0.6, 1.6, 2.5)):
        mat = ("Cushion", "CapPink", "CapYellow", "CapBlue", "Leaf", "CapOrange")[i]
        p.add(sphere(0.2, T(x, -d / 2 + 0.6, 1.14 if i % 2 else 1.94), scale=(1.2, 1, 0.8), segments=12, rings=6), mat)
    # The counter at the front.
    p.add(box((w - 1.2, 0.7, 1.0), T(0, d / 2 - 0.55, 0.65), bevel=0.06), "Wood")
    p.add(box((w - 1.0, 0.85, 0.1), T(0, d / 2 - 0.55, 1.18), bevel=0.04), "WoodDark")
    shop_lamp(p, T(-1.8, d / 2 - 0.55, 1.23), "CapPink")
    shop_lamp(p, T(1.9, d / 2 - 0.55, 1.23), "CapBlue")
    p.add(cylinder(0.3, 0.06, T(0.2, d / 2 - 0.55, 1.26)), "Wall")
    # Chairs on display in front of the counter, either side of where you stand.
    shop_chair(p, T(-2.75, d / 2 + 0.7, 0) @ R("Z", 200))
    shop_chair(p, T(2.75, d / 2 + 0.7, 0) @ R("Z", 160))
    shop_lamp(p, T(-2.2, d / 2 + 0.5, 0) @ S(1.6, 1.6, 1.6), "CapYellow")
    # The striped awning: sloping down from the back to past the front, a scalloped edge.
    stripes = 9
    sw = (w + 0.6) / stripes
    slope = math.degrees(math.atan2(0.9, d + 0.8))
    for i in range(stripes):
        x = -w / 2 - 0.3 + sw * (i + 0.5)
        mat = "Awning" if i % 2 == 0 else "AwningCream"
        p.add(box((sw + 0.01, d + 1.0, 0.14), T(x, 0.35, 3.15) @ R("X", -slope), bevel=0.03), mat)
        p.add(sphere(sw * 0.5, T(x, d / 2 + 0.82, 2.66), scale=(1, 0.35, 0.7), segments=14, rings=7), mat)
    # A sign board on top with a chair on it.
    p.add(box((2.6, 0.14, 0.8), T(0, -d / 2 + 0.3, 3.95), bevel=0.06), "AwningCream")
    p.add(box((2.8, 0.1, 0.95), T(0, -d / 2 + 0.25, 3.95), bevel=0.05), "WoodDark")
    p.add(box((0.4, 0.06, 0.08), T(0, -d / 2 + 0.39, 3.8), bevel=0.0), "Wood")
    p.add(box((0.06, 0.06, 0.25), T(-0.17, -d / 2 + 0.39, 3.67), bevel=0.0), "Wood")
    p.add(box((0.06, 0.06, 0.25), T(0.17, -d / 2 + 0.39, 3.67), bevel=0.0), "Wood")
    p.add(dome(0.2, 0.12, T(0, -d / 2 + 0.36, 4.0) @ R("X", 90)), "Cap")
    p.finish()


def fountain():
    # The town square's fountain: a round stone basin of water with a spotted mushroom in the
    # middle, water streaming from the cap's rim.
    p = Piece("Fountain")
    p.add(cylinder(3.45, 0.9, T(0, 0, 0.15), segments=48), "Stone")
    p.add(torus(3.3, 0.22, T(0, 0, 0.62), segments=48, sides=10), "StoneDark")
    p.add(cylinder(3.1, 0.1, T(0, 0, 0.5), segments=48), "Water")
    p.add(cylinder(0.85, 0.5, T(0, 0, 0.75), segments=24), "Stone")
    p.add(cylinder(0.32, 1.1, T(0, 0, 1.3), segments=20, radius2=0.26), "Wall")
    p.add(cylinder(1.3, 0.12, T(0, 0, 1.78), segments=32), "Gills")
    p.add(dome(1.35, 0.55, T(0, 0, 1.75), segments=32), "Cap")
    cap_spots(p, 1.35, 1.35, 0.55, 1.75, 7, 0.2, seed=0.2)
    for i in range(10):
        a = 2 * math.pi * i / 10
        x, y = math.cos(a), math.sin(a)
        p.add(cylinder(0.06, 1.2, T(x * 1.38, y * 1.38, 1.15), segments=8, radius2=0.04), "Water")
        p.add(sphere(0.12, T(x * 1.4, y * 1.4, 0.56), scale=(1, 1, 0.4), segments=10, rings=5), "Water")
    p.add(torus(1.42, 0.07, T(0, 0, 0.56), segments=32, sides=6), "Water")
    p.add(sphere(0.18, T(0, 0, 2.38), segments=12, rings=6), "Water")
    p.finish()


HOUSES = [
    # (cap, spots, door, door kind, chimney, flowers)
    ("CapOrange", "Spots", "DoorGreen", "round", True, ("PetalPink", "PetalWhite")),
    ("CapLilac", "Spots", "DoorRed", "arched", False, ("PetalYellow", "PetalBlue")),
    ("CapBlue", "Spots", "DoorRed", "round", False, ("PetalPink", "PetalYellow")),
    ("CapYellow", "Spots", "DoorBlue", "arched", True, ("PetalBlue", "PetalWhite")),
]


def house(index):
    cap_mat, spots, door, kind, chimney, flowers = HOUSES[index]
    p, rx, ry, wall, cap = mushroom_house(f"House{index}", 9, 9, 7, cap_mat, spot_mat=spots, spots=8 + index % 2)
    if kind == "round":
        round_door(p, ry, 0.95, door)
    else:
        arched_door(p, ry, 1.3, 2.1, door)
    for angle in (-48, 48, 115, -115, 180):
        round_window(p, rx, ry, angle, 1.7, 0.45)
    window_box(p, rx, ry, 48 if index % 2 else -48, 1.0, flowers)
    if chimney:
        x, y, z = cap_point(cap, 150 if index else -140, 0.66)
        p.add(cylinder(0.3, 1.3, T(x, y, z + 0.4), segments=14), "Stone")
        p.add(cylinder(0.37, 0.22, T(x, y, z + 1.05), segments=14), "StoneDark")
        p.add(cylinder(0.24, 0.05, T(x, y, z + 1.17), segments=14), "Soot")
    p.finish()


def text(body, size, matrix, depth=0.04):
    """Flat 3D lettering, centered, reading from +Y (its face toward +Y, upright along +Z)."""
    curve = bpy.data.curves.new("text", "FONT")
    curve.body = body
    curve.size = size
    curve.extrude = depth / 2
    curve.align_x = "CENTER"
    curve.align_y = "CENTER"
    obj = bpy.data.objects.new("text", curve)
    bpy.context.scene.collection.objects.link(obj)
    mesh = bpy.data.meshes.new_from_object(obj.evaluated_get(bpy.context.evaluated_depsgraph_get()))
    bpy.data.objects.remove(obj)
    bpy.data.curves.remove(curve)
    part = bmesh.new()
    part.from_mesh(mesh)
    bpy.data.meshes.remove(mesh)
    bmesh.ops.transform(part, matrix=matrix @ R("Z", 180) @ R("X", 90), verts=part.verts)
    return part


def bar(piece, a, b, radius, mat, matrix=Matrix()):
    """A round rod from point a to point b (x, z in the plane y = 0 of `matrix`)."""
    dx, dz = b[0] - a[0], b[1] - a[1]
    length = math.hypot(dx, dz)
    angle = math.degrees(math.atan2(dx, dz))
    m = matrix @ T((a[0] + b[0]) / 2, 0, (a[1] + b[1]) / 2) @ R("Y", angle)
    piece.add(cylinder(radius, length, m, segments=10), mat)
    piece.add(sphere(radius, matrix @ T(b[0], 0, b[1]), segments=10, rings=5), mat)


def front_flower_box(piece, x, y, z, width, flowers):
    piece.add(box((width, 0.4, 0.3), T(x, y + 0.2, z), bevel=0.05), "Wood")
    count = max(3, int(width / 0.33))
    for k in range(count):
        fx = x - width / 2 + 0.2 + k * (width - 0.4) / (count - 1)
        piece.add(sphere(0.14, T(fx, y + 0.22, z + 0.2), segments=10, rings=5), flowers[k % len(flowers)])
        piece.add(sphere(0.1, T(fx + 0.12, y + 0.3, z + 0.12), scale=(1.3, 0.7, 0.5), segments=8, rings=4), "Leaf")


def outfit_stand(piece, matrix, kind, mat):
    """A little outfit on a stand in the shop window: a round foot, a pole and the clothes."""
    piece.add(cylinder(0.16, 0.05, matrix @ T(0, 0, 0.025), segments=16), "WoodDark")
    piece.add(cylinder(0.025, 0.9, matrix @ T(0, 0, 0.45), segments=8), "Wood")
    if kind == "dress":
        piece.add(cylinder(0.3, 0.6, matrix @ T(0, 0, 0.6), segments=20, radius2=0.1), mat)
        piece.add(box((0.24, 0.14, 0.24), matrix @ T(0, 0, 0.98), bevel=0.06), mat)
        piece.add(torus(0.3, 0.03, matrix @ T(0, 0, 0.32), segments=20, sides=6), "Display")
    elif kind == "shirt":
        piece.add(box((0.4, 0.18, 0.42), matrix @ T(0, 0, 0.8), bevel=0.07), mat)
        for side in (-1, 1):
            piece.add(box((0.2, 0.15, 0.13), matrix @ T(side * 0.26, 0, 0.92) @ R("Y", side * 30), bevel=0.05), mat)
        piece.add(box((0.36, 0.17, 0.34), matrix @ T(0, 0, 0.42), bevel=0.06), "CapBlue")
    else:
        # A sun hat on top of the pole with a scarf hanging below it.
        piece.add(cylinder(0.3, 0.04, matrix @ T(0, 0, 0.92), segments=24), mat)
        piece.add(dome(0.16, 0.14, matrix @ T(0, 0, 0.93), segments=16), mat)
        piece.add(torus(0.16, 0.025, matrix @ T(0, 0, 0.96), segments=16, sides=6), "PetalPink")
        piece.add(box((0.12, 0.06, 0.45), matrix @ T(0.06, 0.04, 0.6) @ R("Y", 8), bevel=0.03), "PetalBlue")


def clothing_shop():
    # Abby's clothing shop: a pastel boutique mushroom house with a shop front - a striped awning
    # over the door and a big display window with little outfits on stands, flower boxes, and a
    # hanger-shaped sign reading "Abby's" on the cap. (No windows in the cap: they would read as eyes.)
    p, rx, ry, wall, cap = mushroom_house("ClothingShop", 10, 9, 8, "CapMint", wall_mat="WallPink", spots=9)
    capx, capy, base, cap_h = cap
    w, front, back, top = 7.0, ry + 0.5, 1.4, base - 0.1
    p.add(box((w, front - 0.5 - back, top), T(0, (back + front - 0.5) / 2, top / 2), bevel=0.08), "WallPink")
    # The facade, around the big window's opening (x from 0.8 to 3.1, z from 0.75 to 2.6).
    wx0, wx1, wz0, wz1 = 0.8, 3.1, 0.75, 2.6
    fy = front - 0.25
    p.add(box((wx0 + w / 2, 0.5, top), T((wx0 - w / 2) / 2, fy, top / 2), bevel=0.08), "WallPink")
    p.add(box((w / 2 - wx1, 0.5, top), T((wx1 + w / 2) / 2, fy, top / 2), bevel=0.08), "WallPink")
    p.add(box((wx1 - wx0, 0.5, wz0), T((wx0 + wx1) / 2, fy, wz0 / 2), bevel=0.06), "WallPink")
    p.add(box((wx1 - wx0, 0.5, top - wz1), T((wx0 + wx1) / 2, fy, (wz1 + top) / 2), bevel=0.06), "WallPink")
    # The big window: a glowing pane at the back of the opening, a wooden frame, outfits on stands.
    p.add(box((wx1 - wx0, 0.06, wz1 - wz0), T((wx0 + wx1) / 2, front - 0.52, (wz0 + wz1) / 2), bevel=0.0), "Window")
    for x in (wx0, wx1):
        p.add(box((0.14, 0.16, wz1 - wz0 + 0.14), T(x, front + 0.02, (wz0 + wz1) / 2), bevel=0.03), "Wood")
    for z in (wz0, wz1):
        p.add(box((wx1 - wx0 + 0.14, 0.16, 0.14), T((wx0 + wx1) / 2, front + 0.02, z), bevel=0.03), "Wood")
    p.add(box((wx1 - wx0, 0.5, 0.05), T((wx0 + wx1) / 2, front - 0.25, wz0 + 0.02), bevel=0.0), "Display")
    for k, (kind, mat) in enumerate((("dress", "PetalPink"), ("shirt", "CapYellow"), ("hat", "AwningCream"))):
        x = wx0 + (k + 0.5) * (wx1 - wx0) / 3
        outfit_stand(p, T(x, front - 0.25, wz0 + 0.045) @ S(0.95, 0.95, 0.95), kind, mat)
    front_flower_box(p, (wx0 + wx1) / 2, front, wz0 - 0.3, wx1 - wx0 + 0.2, ("PetalPink", "PetalWhite", "PetalYellow"))
    # The door, a small round window on the left with its own flower box, two little lanterns.
    arched_door(p, front, 1.5, 2.4, "DoorLilac")
    m = T(-2.3, front + 0.02, 1.8) @ R("X", -90)
    p.add(cylinder(0.55, 0.16, m, segments=24), "Window")
    p.add(torus(0.6, 0.1, m @ T(0, 0, 0.06), segments=24, sides=8), "Wood")
    p.add(box((0.08, 1.1, 0.06), m @ T(0, 0, 0.09), bevel=0.02), "Wood")
    p.add(box((1.1, 0.08, 0.06), m @ T(0, 0, 0.09), bevel=0.02), "Wood")
    front_flower_box(p, -2.3, front, 1.0, 1.4, ("PetalBlue", "PetalPink"))
    for x in (-1.15, 1.15):
        p.add(box((0.1, 0.25, 0.1), T(x, front + 0.1, 2.35), bevel=0.02), "Metal")
        p.add(sphere(0.13, T(x, front + 0.25, 2.25), segments=12, rings=6), "Glow")
        p.add(dome(0.18, 0.12, T(x, front + 0.25, 2.34), segments=14), "CapMint")
    # The striped awning over the whole shop front, sloping out from under the cap, scalloped.
    stripes, aw, out, drop = 11, w + 0.4, 1.1, 0.5
    sw = aw / stripes
    slope = math.degrees(math.atan2(drop, out))
    for i in range(stripes):
        x = -aw / 2 + sw * (i + 0.5)
        mat = "Awning" if i % 2 == 0 else "AwningCream"
        p.add(box((sw + 0.01, math.hypot(out, drop), 0.1), T(x, front + out / 2, top - 0.15 - drop / 2) @ R("X", -slope), bevel=0.03), mat)
        p.add(sphere(sw * 0.5, T(x, front + out + 0.02, top - 0.17 - drop), scale=(1, 0.3, 0.6), segments=14, rings=7), mat)
    # The sign: a big clothes hanger on the cap's front - a hook, two arms and a board reading "Abby's".
    x, y, z = cap_point(cap, 0, 0.36)
    sign = T(0, y + 0.35, z) @ R("X", 32)
    bar(p, (0.0, 1.05), (-1.55, 0.25), 0.07, "Hanger", sign)
    bar(p, (0.0, 1.05), (1.55, 0.25), 0.07, "Hanger", sign)
    p.add(cylinder(0.07, 0.35, sign @ T(0, 0, 1.2), segments=10), "Hanger")
    p.add(torus(0.22, 0.06, sign @ T(-0.22, 0, 1.4) @ R("X", 90), segments=20, sides=8), "Hanger")
    p.add(box((3.4, 0.16, 0.62), sign @ T(0, 0, 0.0), bevel=0.12), "Hanger")
    p.add(box((3.15, 0.12, 0.46), sign @ T(0, 0.04, 0.0), bevel=0.1), "Display")
    p.add(text("Abby's", 0.44, sign @ T(0, 0.12, 0.0)), "SignText")
    p.finish()


def muffin(piece, matrix):
    piece.add(cylinder(0.075, 0.08, matrix @ T(0, 0, 0.04), segments=12, radius2=0.09), "Muffin")
    piece.add(dome(0.1, 0.07, matrix @ T(0, 0, 0.08), segments=12, rings=5), "Muffin")
    for k in range(3):
        a = 2 * math.pi * k / 3
        piece.add(sphere(0.022, matrix @ T(math.cos(a) * 0.045, math.sin(a) * 0.045, 0.13), segments=8, rings=4), "Berry")


def pie(piece, matrix):
    piece.add(cylinder(0.2, 0.07, matrix @ T(0, 0, 0.035), segments=20, radius2=0.22), "Pie")
    piece.add(cylinder(0.17, 0.02, matrix @ T(0, 0, 0.075), segments=20), "Berry")
    piece.add(torus(0.19, 0.035, matrix @ T(0, 0, 0.075), segments=20, sides=6), "Pie")
    for k in (-1, 0, 1):
        piece.add(box((0.32, 0.03, 0.02), matrix @ T(0, k * 0.08, 0.09), bevel=0.0), "Pie")
        piece.add(box((0.03, 0.32, 0.02), matrix @ T(k * 0.08, 0, 0.095), bevel=0.0), "Pie")


def lantern(piece, matrix):
    """A little lantern hanging from a hook: a glowing bulb under a tiny spotted cap."""
    piece.add(cylinder(0.015, 0.22, matrix @ T(0, 0, -0.11), segments=6), "Metal")
    piece.add(sphere(0.11, matrix @ T(0, 0, -0.32), scale=(1, 1, 1.15), segments=14, rings=7), "Glow")
    piece.add(dome(0.14, 0.09, matrix @ T(0, 0, -0.22), segments=14), "Cap")
    piece.add(cylinder(0.05, 0.03, matrix @ T(0, 0, -0.45), segments=10), "Metal")


def food_stall():
    # "Mushroom Munchies", the food stall on the square: a cozy wooden stall under a red mushroom
    # cap with white spots, a sign board, baskets of berry muffins, a steaming pot of soup and pies
    # on the counter, more on the back shelf, and little lanterns that light up at night.
    p = Piece("FoodStall")
    w, d = 4.2, 2.4
    # A stone footing sunk into the ground, a plank floor on it.
    p.add(box((w, d, 0.4), T(0, 0, -0.12), bevel=0.06), "Stone")
    p.add(box((w - 0.2, d - 0.2, 0.14), T(0, 0, 0.12), bevel=0.04), "Wood")
    for x in (-1, 1):
        for y in (-1, 1):
            p.add(cylinder(0.09, 2.15, T(x * (w / 2 - 0.22), y * (d / 2 - 0.22), 1.2), segments=12), "WoodDark")
    # The back wall with a shelf of pies and muffin baskets.
    p.add(box((w - 0.5, 0.14, 1.95), T(0, -d / 2 + 0.25, 1.15), bevel=0.04), "Wood")
    for k in range(5):
        p.add(box((0.05, 0.02, 1.9), T(-1.4 + k * 0.7, -d / 2 + 0.33, 1.15), bevel=0.0), "WoodDark")
    p.add(box((w - 0.8, 0.4, 0.07), T(0, -d / 2 + 0.5, 1.5), bevel=0.02), "WoodDark")
    for x in (-1.2, 0.0, 1.2):
        pie(p, T(x, -d / 2 + 0.5, 1.535) @ S(0.8, 0.8, 0.8))
    for x in (-0.6, 0.6):
        p.add(cylinder(0.12, 0.2, T(x, -d / 2 + 0.5, 1.64), segments=12, radius2=0.1), "CapWhite")
        p.add(cylinder(0.1, 0.05, T(x, -d / 2 + 0.5, 1.765), segments=12), "Berry")
    # The counter at the front, planked, and its top.
    cy = d / 2 - 0.5
    p.add(box((w - 0.6, 0.55, 0.78), T(0, cy, 0.57), bevel=0.05), "Wood")
    for k in range(7):
        p.add(box((0.04, 0.02, 0.68), T(-1.5 + k * 0.5, cy + 0.285, 0.57), bevel=0.0), "WoodDark")
    p.add(box((w - 0.4, 0.7, 0.08), T(0, cy, 0.99), bevel=0.03), "WoodDark")
    top = 1.03
    # Two baskets of berry muffins on the left.
    for bx in (-1.45, -0.85):
        p.add(cylinder(0.25, 0.12, T(bx, cy, top + 0.06), segments=18, radius2=0.28), "Basket")
        p.add(torus(0.27, 0.03, T(bx, cy, top + 0.12), segments=18, sides=6), "Basket")
        for mx, my in ((-0.1, -0.07), (0.1, -0.07), (0.0, 0.1)):
            muffin(p, T(bx + mx, cy + my, top + 0.08))
    # A pot of soup in the middle, steam rising from it.
    p.add(cylinder(0.27, 0.06, T(0.05, cy, top + 0.03), segments=18), "Metal")
    p.add(cylinder(0.24, 0.3, T(0.05, cy, top + 0.21), segments=20, radius2=0.25), "Metal")
    p.add(torus(0.25, 0.03, T(0.05, cy, top + 0.36), segments=20, sides=6), "Metal")
    p.add(cylinder(0.225, 0.02, T(0.05, cy, top + 0.33), segments=20), "Soup")
    p.add(box((0.04, 0.04, 0.42), T(0.17, cy - 0.05, top + 0.45) @ R("Y", 25), bevel=0.0), "WoodDark")
    for k, (sx, sz, r) in enumerate(((0.0, 0.48, 0.12), (0.08, 0.66, 0.1), (-0.03, 0.82, 0.08))):
        p.add(sphere(r, T(0.05 + sx, cy, top + sz), scale=(1, 1, 0.8), segments=12, rings=6), "Steam")
    # Pies on the right, one cut.
    pie(p, T(0.85, cy, top))
    pie(p, T(1.4, cy - 0.05, top))
    p.add(box((0.2, 0.18, 0.05), T(1.45, cy + 0.25, top + 0.025) @ R("Z", 15), bevel=0.0), "Pie")
    # The roof: a red mushroom cap with white spots, cream gills beneath.
    rx, ry, roof, ch = w / 2 + 0.05, d / 2 + 0.15, 2.2, 0.75
    p.add(cylinder(1.0, 0.1, T(0, 0, roof + 0.02) @ S(rx - 0.08, ry - 0.08, 1), segments=32), "Gills")
    p.add(dome(1.0, ch, T(0, 0, roof) @ S(rx, ry, 1), segments=32), "Cap")
    cap_spots(p, rx, ry, ch, roof, 8, 0.24, seed=0.3)
    # The sign under the cap's front edge: "Mushroom Munchies".
    sy = d / 2 - 0.05
    p.add(box((3.3, 0.1, 0.42), T(0, sy, 2.07), bevel=0.06), "WoodDark")
    p.add(box((3.12, 0.06, 0.3), T(0, sy + 0.04, 2.07), bevel=0.04), "AwningCream")
    p.add(text("Mushroom Munchies", 0.23, T(0, sy + 0.08, 2.07)), "SignText")
    # Little lanterns hanging at the front corners and by the back wall.
    for x in (-1, 1):
        lantern(p, T(x * (w / 2 - 0.22), d / 2 - 0.05, roof - 0.02))
        lantern(p, T(x * 1.2, -d / 2 + 0.55, roof - 0.02) @ S(0.8, 0.8, 0.8))
    p.finish()


def street_lamp():
    # A mushroom street lamp: a dark post on a round foot, a glowing bulb hanging under a spotted
    # cap whose underside and spots glow too.
    p = Piece("StreetLamp")
    p.add(cylinder(0.32, 0.3, T(0, 0, 0.05), segments=16), "Stone")
    p.add(cylinder(0.09, 2.5, T(0, 0, 1.4), segments=12, radius2=0.07), "Metal")
    p.add(torus(0.1, 0.04, T(0, 0, 0.8), segments=12, sides=6), "Metal")
    p.add(sphere(0.3, T(0, 0, 2.62), scale=(1, 1, 0.95)), "Glow")
    p.add(cylinder(0.4, 0.08, T(0, 0, 2.86), segments=24), "Glow")
    p.add(dome(0.46, 0.32, T(0, 0, 2.86), segments=24), "Cap")
    cap_spots(p, 0.46, 0.46, 0.32, 2.86, 5, 0.09, "Glow")
    p.finish()


def bench():
    # A wooden park bench on two mushroom-stem legs, its seat facing front.
    p = Piece("Bench")
    for x in (-0.7, 0.7):
        p.add(cylinder(0.12, 0.42, T(x, 0, 0.21), segments=14, radius2=0.1), "Wall")
        p.add(dome(0.2, 0.08, T(x, 0, 0.0), segments=14), "Cap")
    for k in range(3):
        p.add(box((1.8, 0.16, 0.07), T(0, 0.2 - k * 0.18, 0.45), bevel=0.025), "Wood")
    for x in (-0.7, 0.7):
        p.add(box((0.08, 0.08, 0.5), T(x, -0.28, 0.68) @ R("X", -10), bevel=0.02), "WoodDark")
    for k in range(2):
        p.add(box((1.8, 0.06, 0.14), T(0, -0.31 - k * 0.03, 0.66 + k * 0.2) @ R("X", -10), bevel=0.025), "Wood")
    p.finish()


def fence():
    # One 3.5 m segment of picket fence along X (the Map's FenceSegment), 0.8 m tall: a post at
    # each end, two rails, rounded pickets.
    p = Piece("Fence")
    for x in (-1.68, 1.68):
        p.add(box((0.14, 0.14, 0.82), T(x, 0, 0.32), bevel=0.03), "Picket")
        p.add(sphere(0.09, T(x, 0, 0.72), segments=10, rings=5), "Picket")
    for z in (0.24, 0.52):
        p.add(box((3.4, 0.06, 0.08), T(0, -0.05, z), bevel=0.02), "Picket")
    for k in range(8):
        x = -1.4 + k * 0.4
        p.add(box((0.14, 0.05, 0.58), T(x, 0.02, 0.27), bevel=0.02), "Picket")
        p.add(cylinder(0.07, 0.05, T(x, 0.02, 0.56) @ R("X", 90), segments=12), "Picket")
    p.finish()


def planter():
    # A round stone planter (the Map's Planter), 1.2 m across and 0.7 m tall with its flowers.
    p = Piece("Planter")
    p.add(cylinder(0.6, 0.36, T(0, 0, 0.14), segments=28, radius2=0.56), "Stone")
    p.add(torus(0.55, 0.06, T(0, 0, 0.32), segments=28, sides=8), "Stone")
    p.add(cylinder(0.5, 0.06, T(0, 0, 0.31), segments=24), "Soil")
    petals = ("PetalPink", "PetalYellow", "PetalBlue", "PetalWhite", "PetalPink", "PetalYellow")
    for k in range(6):
        a = 2 * math.pi * k / 6
        flower(p, T(math.cos(a) * 0.3, math.sin(a) * 0.3, 0.32) @ R("Z", k * 47), petals[k], scale=0.95)
    flower(p, T(0, 0, 0.32), "PetalWhite", scale=1.05)
    for k in range(4):
        a = 2 * math.pi * k / 4 + 0.4
        p.add(sphere(0.13, T(math.cos(a) * 0.36, math.sin(a) * 0.36, 0.38), scale=(1, 0.8, 0.5), segments=10, rings=5), "Leaf")
    p.finish()


def crate():
    # A wooden crate (the Map's Crate), 0.71 x 0.71 m and 0.8 m tall: planks with darker edges.
    p = Piece("Crate")
    w, h = 0.71, 0.8
    p.add(box((w - 0.04, w - 0.04, h - 0.04), T(0, 0, h / 2), bevel=0.03), "Wood")
    for y in (-1, 1):
        for z in (0.06, h - 0.06):
            p.add(box((w, 0.06, 0.1), T(0, y * (w / 2 - 0.02), z), bevel=0.02), "WoodDark")
        p.add(box((0.08, 0.05, h * 1.05), T(0, y * (w / 2 - 0.01), h / 2) @ R("Y", 42), bevel=0.02), "WoodDark")
    for x in (-1, 1):
        for z in (0.06, h - 0.06):
            p.add(box((0.06, w, 0.1), T(x * (w / 2 - 0.02), 0, z), bevel=0.02), "WoodDark")
    p.finish()


def flowers():
    # A small clump of flowers (free ground decoration, walked over): 0.5 m across, 0.35 m tall.
    p = Piece("Flowers")
    petals = ("PetalPink", "PetalYellow", "PetalBlue")
    for k, (x, y) in enumerate(((-0.1, -0.06), (0.11, -0.03), (0.0, 0.12))):
        flower(p, T(x, y, 0) @ R("Z", k * 70), petals[k], scale=0.85 + 0.1 * k)
    for k in range(3):
        a = 2 * math.pi * k / 3 + 0.5
        p.add(sphere(0.1, T(math.cos(a) * 0.14, math.sin(a) * 0.14, 0.03), scale=(1, 0.8, 0.5), segments=10, rings=5), "Leaf")
    p.finish()


bpy.ops.wm.read_factory_settings(use_empty=True)
for name, color in COLORS.items():
    material(name, color)

player_house()
millie_house()
clinic()
furniture_shop()
fountain()
for i in range(len(HOUSES)):
    house(i)
clothing_shop()
food_stall()
street_lamp()
bench()
fence()
planter()
crate()
flowers()

path = os.path.join(HERE, "town.glb")
bpy.ops.export_scene.gltf(filepath=path, export_format="GLB", export_yup=True)
print("town: exported", path)
