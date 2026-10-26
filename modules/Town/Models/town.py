# Builds Mushroom Town's buildings and props and exports them next to this script as town.glb.
# Run from the repository root:
#   blender -b --factory-startup --python modules/Town/Models/town.py
#
# One object per Fungtopia.Town TownPiece: PlayerHouse (12 x 12 m, 10 m tall), MillieHouse
# (10 x 10 x 8), Clinic (12 x 10 x 8.5), FurnitureShop (7 x 4 x 3.5), Fountain (7 x 7 x 2.2),
# House0..House3 (9 x 9 x 7, four looks), ClothingShop (10 x 9 x 8), FoodStall (4.2 x 2.4 x 3), StreetLamp, Bench, Fence (one 3.5 m segment),
# Planter, Crate, Flowers0..3 (a small clump, four palettes) and Mushroom<Kind> (the fourteen garden
# mushrooms of TownMushrooms): each the size of its footprint in the Map Contract (or the Core's), standing on the ground at the
# origin (a little foundation below it, for uneven ground), its front (door, counter, seat)
# toward Blender +Y (Godot -Z, a building's front at rotation 0). Chunky and cute; no eyes or
# faces on anything (only creatures have them). Materials carry plain colors only; the game draws
# them toon-shaded with ink outlines (the mushroom caps, vivid and saturated, with a glossy band and a
# rim), and makes Glow (lamp bulbs, glowing caps and spots) and Window light up at night.
import math
import os

import bmesh
import bpy
from mathutils import Matrix, Vector

HERE = os.path.dirname(os.path.abspath(__file__))

# Vibrant, like a cheerful cartoon: lush bright greens with yellow-green highlights, warm golden
# wood, clean bright stone, candy-bright paints and flowers in many hues; never washed-out white
# nor neon (every paint keeps some depth so the ink-outlined toon shading still reads).
COLORS = {
    "Wall": (1.0, 0.95, 0.84),
    "WallWhite": (0.97, 0.97, 0.95),
    "Stone": (0.84, 0.81, 0.76),
    "StoneDark": (0.66, 0.62, 0.6),
    "Wood": (0.88, 0.54, 0.24),
    "WoodDark": (0.56, 0.28, 0.12),
    "Cap": (0.97, 0.1, 0.1),
    "CapPink": (1.0, 0.36, 0.64),
    "CapPinkInner": (1.0, 0.78, 0.86),
    "CapWhite": (1.0, 1.0, 1.0),
    "CapOrange": (1.0, 0.46, 0.06),
    "CapLilac": (0.68, 0.34, 1.0),
    "CapBlue": (0.18, 0.52, 1.0),
    "CapYellow": (1.0, 0.8, 0.08),
    "Gills": (1.0, 0.9, 0.72),
    "Spots": (1.0, 1.0, 1.0),
    "SpotsPale": (0.92, 0.9, 0.94),
    "Cross": (0.96, 0.14, 0.18),
    "DoorRed": (1.0, 0.3, 0.32),
    "DoorBlue": (0.26, 0.6, 1.0),
    "DoorGreen": (0.28, 0.95, 0.4),
    "Knob": (1.0, 0.8, 0.2),
    "Window": (1.0, 0.86, 0.42),
    "WindowBar": (0.88, 0.54, 0.24),
    "Mat": (1.0, 0.56, 0.24),
    "MatStripe": (1.0, 0.88, 0.36),
    "Awning": (1.0, 0.36, 0.54),
    "AwningCream": (1.0, 0.95, 0.84),
    "Cushion": (0.4, 0.7, 1.0),
    "Glow": (1.0, 0.88, 0.5),
    "Metal": (0.16, 0.4, 0.42),
    "Water": (0.3, 0.8, 1.0),
    "Picket": (1.0, 0.98, 0.92),
    "Soil": (0.58, 0.34, 0.16),
    "Leaf": (0.26, 0.8, 0.2),
    "LeafLight": (0.66, 0.95, 0.16),
    "PetalPink": (1.0, 0.42, 0.7),
    "PetalYellow": (1.0, 0.86, 0.12),
    "PetalBlue": (0.3, 0.58, 1.0),
    "PetalWhite": (1.0, 1.0, 0.97),
    "PetalPurple": (0.68, 0.34, 1.0),
    "PetalOrange": (1.0, 0.52, 0.1),
    "PetalRed": (1.0, 0.18, 0.28),
    "Soot": (0.24, 0.2, 0.22),
    "CapMint": (0.26, 0.96, 0.72),
    "WallPink": (1.0, 0.8, 0.86),
    "DoorLilac": (0.72, 0.4, 1.0),
    "Display": (1.0, 0.95, 0.97),
    "SignText": (0.7, 0.16, 0.5),
    "Hanger": (0.96, 0.7, 0.28),
    "Basket": (0.94, 0.66, 0.3),
    "Muffin": (0.84, 0.48, 0.2),
    "Berry": (0.66, 0.14, 0.6),
    "Soup": (1.0, 0.6, 0.16),
    "Pie": (0.98, 0.74, 0.36),
    "Steam": (0.97, 0.97, 1.0),
    "FabricPink": (1.0, 0.42, 0.68),
    "FabricYellow": (1.0, 0.82, 0.2),
    "FabricBlue": (0.3, 0.62, 1.0),
    "FabricOrange": (1.0, 0.54, 0.16),
    "Jar": (0.99, 0.98, 0.96),
    # The garden mushrooms and fungi (vivid caps, as every mushroom), and their details.
    "CapBrown": (0.96, 0.58, 0.3),
    "CapGold": (1.0, 0.74, 0.08),
    "CapTan": (1.0, 0.7, 0.36),
    "CapMorel": (0.98, 0.66, 0.28),
    "CapAmethyst": (0.74, 0.3, 1.0),
    "CapScarlet": (1.0, 0.1, 0.18),
    "CapCoral": (1.0, 0.36, 0.5),
    "CapJelly": (1.0, 0.62, 0.04),
    "CapBracket": (1.0, 0.55, 0.12),
    "CapGlowBlue": (0.18, 0.72, 1.0),
    "CapGlowGreen": (0.32, 1.0, 0.5),
    "Scales": (0.72, 0.38, 0.16),
    "Ink": (0.3, 0.24, 0.4),
    "MorelPit": (0.62, 0.34, 0.12),
    "CupInner": (0.86, 0.04, 0.14),
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
        """Appends `part` in material `mat` (or a tuple: each face in mat[its material_index])."""
        mats = mat if isinstance(mat, tuple) else (mat,)
        mesh = bpy.data.meshes.new("tmp")
        part.to_mesh(mesh)
        part.free()
        for poly in mesh.polygons:
            poly.material_index = ORDER.index(mats[min(poly.material_index, len(mats) - 1)])
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


# Windows are built in clear layers, with no two faces nearer than CLEAR (Tony, 2026-10-26: surfaces
# a centimeter or two apart z-fought and flickered on a real GPU): a thin pane CLEAR in front of the
# wall, the crossing bars (one piece) CLEAR in front of the pane, the frame CLEAR in front of the bars.
CLEAR = 0.03
PANE = 0.03
BARS_BACK, BARS = PANE + CLEAR, 0.04
FRAME_BACK, FRAME = BARS_BACK + BARS + CLEAR, 0.12
WINDOW_DEPTH = FRAME_BACK + FRAME


def stem_out(rx, ry, wall, angle, z, radius):
    """on_wall's `out` that sets a flat disc of `radius` (facing out at `angle`, centered at height z)
    CLEAR in front of a house's stem everywhere: the stem tapers from rx x ry at the ground to 0.9 of
    it at wall + 0.2, so its lower part bulges out and its sides curve away."""
    a = math.radians(angle)
    side = (math.cos(a), -math.sin(a))

    def gap(out):
        x0, y0 = math.sin(a) * (rx + out), math.cos(a) * (ry + out)
        worst = math.inf
        for k in range(48):
            b = 2 * math.pi * k / 48
            for share in (0.0, 0.5, 1.0):
                h, v = radius * share * math.cos(b), radius * share * math.sin(b)
                f = 1 - 0.1 * (z + v) / (wall + 0.2)
                e = math.hypot((x0 + side[0] * h) / (f * rx), (y0 + side[1] * h) / (f * ry))
                # At least this far outside the ellipse (its support is never under the shorter axis).
                worst = min(worst, (e - 1) * f * min(rx, ry))
        return worst

    out = -0.5
    while gap(out) < CLEAR:
        out += 0.005
    return out


def plus(arm, width, depth, matrix):
    """Two bars crossing as one flat piece (a plus, no faces inside it): arms `arm` from the middle."""
    part = bmesh.new()
    a, w = arm, width / 2
    outline = [(a, -w), (a, w), (w, w), (w, a), (-w, a), (-w, w), (-a, w), (-a, -w), (-w, -w), (-w, -a), (w, -a), (w, -w)]
    back = [part.verts.new((x, y, -depth / 2)) for x, y in outline]
    front = [part.verts.new((x, y, depth / 2)) for x, y in outline]
    part.faces.new(front)
    part.faces.new(list(reversed(back)))
    for i in range(len(outline)):
        j = (i + 1) % len(outline)
        part.faces.new((back[i], back[j], front[j], front[i]))
    bmesh.ops.bevel(part, geom=list(part.edges), offset=0.012, segments=2, affect="EDGES", profile=0.5)
    bmesh.ops.transform(part, matrix=matrix, verts=part.verts)
    return part


def window_layers(piece, m, radius, frame):
    """A round window on a wall, `m` on the wall's surface facing out: pane, bars and frame in layers."""
    piece.add(cylinder(radius, PANE, m @ T(0, 0, PANE / 2), segments=24), "Window")
    piece.add(plus(radius, 0.08, BARS, m @ T(0, 0, BARS_BACK + BARS / 2)), "WindowBar")
    piece.add(torus(radius + 0.05, 0.1, m @ T(0, 0, FRAME_BACK + FRAME / 2) @ S(1, 1, FRAME / 0.2), segments=24, sides=8), frame)


def round_window(piece, rx, ry, wall, angle, z, radius=0.55, frame="Wood"):
    """A round window on a house's stem; returns how far out (on_wall's `out`) its frame's front is."""
    out = stem_out(rx, ry, wall, angle, z, radius)
    window_layers(piece, on_wall(rx, ry, angle, z, out=out), radius, frame)
    return out + WINDOW_DEPTH


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
    # Shelf fungi growing on the stem and small mushrooms of many kinds on the foundation, round
    # the sides and back (each house its own mix).
    seed = sum(map(ord, name))
    for k, angle in enumerate((82, -82, 135)):
        bracket_fungi(p, rx, ry, wall, angle + seed % 9, wall * (0.2 + 0.1 * k), 0.5 - 0.06 * k)
    kinds = [kind for kind, _, _, _ in MUSHROOMS]
    picks = [(72 + seed % 9, 1.6), (96, 1.1), (-78 - seed % 7, 1.4), (-104, 0.95), (150, 1.3), (178, 1.0), (-160, 1.5)]
    foot_mushrooms(p, rx, ry, [(angle, kinds[(seed + 5 * i) % len(kinds)], scale) for i, (angle, scale) in enumerate(picks)])
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


def window_box(piece, rx, ry, angle, z, flowers=("PetalPink", "PetalYellow"), frame_front=0.15):
    """A flower box under a window, CLEAR in front of its frame's front (on_wall's `out`), whose bottom it overlaps."""
    m = on_wall(rx, ry, angle, 0, out=frame_front + CLEAR + 0.1)
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
    piece.add(sphere(0.06 * scale, matrix @ T(0.05 * scale, 0, 0.12 * scale) @ R("Y", 50), scale=(1.4, 0.6, 0.3), segments=10, rings=5), "LeafLight")


# ---- Garden mushrooms and fungi: cartoon-simple, cute, no faces ----
# One object per Fungtopia.Town MushroomKind (Mushroom<Kind>), normalized to the size TownMushrooms
# says (width across x and y, height to its top), so the game scales each instance from there.
# The same builders put small mushrooms on the houses' foundations and in the planters.


def lathe(profile, matrix, segments=20, wave=0.0, waves=5, mat=None):
    """A closed surface of revolution about +Z through `profile`, a list of (radius, z) from the
    top center round to the bottom center (each end on the axis). `wave` ripples it (wavy caps)."""
    part = bmesh.new()
    rmax = max(r for r, _ in profile) or 1.0
    rings = []
    for r, z in profile:
        if r <= 0.0:
            rings.append([part.verts.new((0.0, 0.0, z))])
            continue
        k = r / rmax
        ring = []
        for j in range(segments):
            a = 2 * math.pi * j / segments
            rr = r * (1 + wave * k * math.sin(waves * a))
            zz = z + wave * k * k * rmax * 0.5 * math.cos(waves * a)
            ring.append(part.verts.new((rr * math.cos(a), rr * math.sin(a), zz)))
        rings.append(ring)
    for a, b in zip(rings, rings[1:]):
        for j in range(segments):
            if len(a) == 1 and len(b) == 1:
                continue
            if len(a) == 1:
                part.faces.new((a[0], b[j], b[(j + 1) % segments]))
            elif len(b) == 1:
                part.faces.new((a[j], b[0], a[(j + 1) % segments]))
            else:
                part.faces.new((a[j], b[j], b[(j + 1) % segments], a[(j + 1) % segments]))
    bmesh.ops.recalc_face_normals(part, faces=list(part.faces))
    bmesh.ops.transform(part, matrix=matrix, verts=part.verts)
    return part


def radius_at(profile, z):
    """The outermost radius of a lathe profile at height z (for things sitting on its surface)."""
    best = 0.0
    for (r0, z0), (r1, z1) in zip(profile, profile[1:]):
        if min(z0, z1) <= z <= max(z0, z1) and z0 != z1:
            best = max(best, r0 + (r1 - r0) * (z - z0) / (z1 - z0))
    return best


def rod(piece, a, b, radius, mat, matrix=Matrix(), radius2=None, segments=8):
    """A round rod from point a to point b (3D, in `matrix`'s space)."""
    a, b = Vector(a), Vector(b)
    d = b - a
    rot = Vector((0, 0, 1)).rotation_difference(d).to_matrix().to_4x4()
    m = matrix @ Matrix.Translation((a + b) / 2) @ rot
    piece.add(cylinder(radius, d.length, m, segments=segments, radius2=radius2), mat)


def dome_spots(piece, matrix, radius, height, count, size, mat="Spots", seed=0.0):
    """Crisp round spots on a dome cap of `radius` x `height` whose base is at `matrix`."""
    for i in range(count):
        a = 2 * math.pi * i / count + seed
        t = 0.66 if i % 2 == 0 else 0.32
        sn = math.sqrt(1 - t * t)
        tilt = math.degrees(math.acos(t))
        r = size * (1.0 if i % 3 else 0.7)
        rot = R("Z", math.degrees(a) - 90) @ R("X", -tilt)
        piece.add(sphere(r, matrix @ T(math.cos(a) * sn * radius, math.sin(a) * sn * radius, t * height) @ rot,
                         scale=(1, 1, 0.32), segments=10, rings=5), mat)
    piece.add(sphere(size * 1.1, matrix @ T(0, 0, height), scale=(1, 1, 0.3), segments=10, rings=5), mat)


def stem(piece, matrix, radius, height, foot=1.2, mat="Wall"):
    piece.add(lathe([(0, height), (radius * 0.9, height), (radius, height * 0.5), (radius * foot, height * 0.08),
                     (radius * foot * 0.9, 0), (0, 0)], matrix, segments=14), mat)


def fly_agaric(p, m):
    stem(p, m, 0.045, 0.2)
    p.add(torus(0.05, 0.012, m @ T(0, 0, 0.15), segments=16, sides=6), "Wall")
    p.add(cylinder(0.135, 0.02, m @ T(0, 0, 0.185), segments=20), "Gills")
    p.add(dome(0.15, 0.12, m @ T(0, 0, 0.18), segments=22, rings=8), "Cap")
    dome_spots(p, m @ T(0, 0, 0.18), 0.15, 0.12, 7, 0.026, seed=0.3)


def porcini(p, m):
    p.add(lathe([(0, 0.15), (0.06, 0.15), (0.085, 0.1), (0.095, 0.05), (0.075, 0.0), (0, 0)], m, segments=16), "Wall")
    p.add(cylinder(0.14, 0.03, m @ T(0, 0, 0.145), segments=22), "Gills")
    p.add(lathe([(0, 0.26), (0.07, 0.25), (0.13, 0.21), (0.152, 0.17), (0.14, 0.14), (0.06, 0.135), (0, 0.135)],
                m, segments=22), "CapBrown")


def chanterelle(p, m):
    stem(p, m, 0.028, 0.07, foot=1.1)
    p.add(lathe([(0, 0.18), (0.04, 0.19), (0.08, 0.205), (0.11, 0.22), (0.112, 0.208), (0.09, 0.18),
                 (0.05, 0.12), (0.03, 0.06), (0, 0.06)], m, segments=24, wave=0.1, waves=6), "CapGold")


PARASOL = [(0, 0.38), (0.03, 0.372), (0.1, 0.35), (0.17, 0.318), (0.18, 0.306), (0.16, 0.3), (0.05, 0.31), (0, 0.31)]


def parasol(p, m):
    p.add(sphere(0.034, m @ T(0, 0, 0.022), scale=(1, 1, 0.75), segments=12, rings=6), "Wall")
    rod(p, (0, 0, 0.02), (0, 0, 0.32), 0.016, "Wall", m, radius2=0.012)
    p.add(torus(0.024, 0.008, m @ T(0, 0, 0.23), segments=14, sides=6), "Wall")
    p.add(cylinder(0.16, 0.012, m @ T(0, 0, 0.305), segments=22), "Gills")
    p.add(lathe(PARASOL, m, segments=24), "CapTan")
    p.add(sphere(0.03, m @ T(0, 0, 0.375), scale=(1, 1, 0.45), segments=10, rings=5), "Scales")
    for ring, (rad, n) in enumerate(((0.07, 6), (0.12, 9), (0.155, 12))):
        z = 0.38 - (0.38 - 0.318) * (rad / 0.17) ** 1.4
        for k in range(n):
            a = 2 * math.pi * (k + 0.5 * ring) / n
            p.add(sphere(0.011, m @ T(math.cos(a) * rad, math.sin(a) * rad, z + 0.003), scale=(1.2, 1, 0.35), segments=8, rings=4), "Scales")


INKCAP = [(0, 0.34), (0.03, 0.335), (0.058, 0.3), (0.074, 0.22), (0.075, 0.13), (0.08, 0.075), (0.06, 0.08),
          (0.02, 0.1), (0, 0.1)]


def ink_cap(p, m):
    stem(p, m, 0.024, 0.11)
    p.add(lathe(INKCAP, m, segments=22, wave=0.04, waves=9), "CapWhite")
    p.add(torus(0.076, 0.011, m @ T(0, 0, 0.078), segments=20, sides=6), "Ink")
    for row, z in enumerate((0.15, 0.2, 0.25, 0.29)):
        r = radius_at(INKCAP, z)
        for k in range(7):
            a = 2 * math.pi * (k + 0.5 * row) / 7
            p.add(sphere(0.012, m @ T(math.cos(a) * r, math.sin(a) * r, z) @ R("Z", math.degrees(a)), scale=(0.5, 1, 1.4), segments=8, rings=4), "Gills")


MOREL = [(0, 0.26), (0.02, 0.252), (0.044, 0.21), (0.056, 0.15), (0.052, 0.1), (0.036, 0.075), (0, 0.075)]


def morel(p, m):
    stem(p, m, 0.032, 0.09, foot=1.15)
    p.add(lathe(MOREL, m, segments=18), "CapMorel")
    for row in range(6):
        z = 0.095 + row * 0.027
        r = radius_at(MOREL, z)
        n = max(4, int(r * 130))
        for k in range(n):
            a = 2 * math.pi * (k + 0.5 * (row % 2)) / n
            p.add(sphere(0.014, m @ T(math.cos(a) * (r - 0.004), math.sin(a) * (r - 0.004), z) @ R("Z", math.degrees(a)), scale=(0.3, 1, 1.25), segments=8, rings=4), "MorelPit")


def enoki(p, m):
    p.add(sphere(0.05, m @ T(0, 0, 0.012), scale=(1, 1, 0.45), segments=14, rings=6), "Wall")
    for k in range(8):
        a = 2 * math.pi * k / 8 + 0.3 * (k % 2)
        spread = 0.035 + 0.045 * ((k * 5) % 3) / 2
        top = (math.cos(a) * spread, math.sin(a) * spread, 0.2 + 0.08 * ((k * 3) % 5) / 4)
        rod(p, (math.cos(a) * 0.012, math.sin(a) * 0.012, 0.01), top, 0.0085, "Wall", m, segments=6)
        p.add(dome(0.022, 0.016, m @ T(*top), segments=12, rings=5), "CapWhite")


AMETHYST = [(0, 0.2), (0.04, 0.196), (0.08, 0.175), (0.104, 0.14), (0.11, 0.124), (0.092, 0.12), (0.03, 0.124), (0, 0.124)]


def amethyst(p, m):
    stem(p, m, 0.026, 0.13)
    p.add(cylinder(0.09, 0.01, m @ T(0, 0, 0.123), segments=18), "Gills")
    p.add(lathe(AMETHYST, m, segments=24, wave=0.12, waves=5), "CapAmethyst")


PUFFBALL = [(0, 0.16), (0.03, 0.157), (0.07, 0.138), (0.095, 0.1), (0.1, 0.07), (0.085, 0.03), (0.05, 0.004), (0, 0)]


def puffball(p, m):
    p.add(lathe(PUFFBALL, m, segments=22), "CapWhite")
    for row, z in enumerate((0.115, 0.14)):
        r = radius_at(PUFFBALL, z)
        for k in range(8 - row * 3):
            a = 2 * math.pi * (k + 0.5 * row) / (8 - row * 3)
            p.add(sphere(0.009, m @ T(math.cos(a) * r, math.sin(a) * r, z), segments=8, rings=4), "Gills")
    p.add(sphere(0.012, m @ T(0, 0, 0.158), segments=8, rings=4), "Gills")


def cup_fungus(p, m):
    p.add(sphere(0.12, m @ T(0, 0, 0.0), scale=(1, 0.85, 0.15), segments=16, rings=6), "Leaf")
    for (x, y, s, tilt) in ((0.0, 0.0, 1.0, 0), (0.07, 0.035, 0.75, 18), (-0.06, 0.045, 0.62, -22), (0.02, -0.07, 0.55, 25)):
        cm = m @ T(x, y, 0.0) @ R("Z", x * 900) @ R("X", tilt) @ S(s, s, s)
        p.add(lathe([(0, 0.035), (0.03, 0.045), (0.05, 0.07), (0.056, 0.082), (0.06, 0.1), (0.055, 0.075),
                     (0.035, 0.035), (0.016, 0.006), (0, 0)], cm, segments=18), "CapScarlet")
        p.add(cylinder(0.04, 0.008, cm @ T(0, 0, 0.055), segments=16), "CupInner")


def coral(p, m):
    def branch(a, direction, length, radius, depth):
        b = a + direction * length
        rod(p, a, b, radius, "CapCoral", m, radius2=radius * 0.8)
        if depth == 0:
            p.add(sphere(radius * 1.25, m @ T(*b), segments=8, rings=4), "CapOrange")
            return
        side = Vector((direction.y, -direction.x, 0)).normalized() if abs(direction.z) < 0.99 else Vector((1, 0, 0))
        for k, turn in enumerate((-32, 30)):
            d = (Matrix.Rotation(math.radians(turn), 3, side) @ direction).normalized()
            d = (d + Vector((0, 0, 0.35))).normalized()
            branch(b, d, length * 0.72, radius * 0.75, depth - 1)
    for k in range(4):
        a = 2 * math.pi * k / 4 + 0.4
        d = Vector((math.cos(a) * 0.45, math.sin(a) * 0.45, 1)).normalized()
        branch(Vector((0, 0, 0)), d, 0.085, 0.014, 2)


def jelly(p, m):
    p.add(cylinder(0.03, 0.22, m @ T(0, 0, 0.026) @ R("Y", 90), segments=12), "WoodDark")
    for (x, y, z, s) in ((-0.06, 0.02, 0.04, 0.9), (0.02, -0.025, 0.045, 1.0), (0.08, 0.02, 0.035, 0.7), (0.0, 0.04, 0.03, 0.6)):
        p.add(lathe([(0, 0.075), (0.03, 0.08), (0.06, 0.06), (0.07, 0.035), (0.05, 0.0), (0, 0.0)],
                    m @ T(x, y, z) @ S(s, s, s), segments=18, wave=0.25, waves=4), "CapJelly")


GLOWCONE = [(0, 0.26), (0.02, 0.246), (0.05, 0.19), (0.08, 0.142), (0.086, 0.13), (0.07, 0.132), (0, 0.142)]


def glow_blue(p, m):
    for (x, y, s, lean) in ((0.0, 0.0, 1.0, 0), (0.055, 0.04, 0.55, 18)):
        gm = m @ T(x, y, 0) @ R("X", lean) @ S(s, s, s)
        stem(p, gm, 0.018, 0.145)
        p.add(lathe(GLOWCONE, gm, segments=18), "CapGlowBlue")


def glow_green(p, m):
    for (x, y, s, lean) in ((0.0, 0.0, 1.0, 0), (0.07, 0.035, 0.7, 20), (-0.05, 0.06, 0.55, -24)):
        gm = m @ T(x, y, 0) @ R("Y", lean) @ S(s, s, s)
        stem(p, gm, 0.02, 0.12)
        p.add(lathe([(0, 0.2), (0.04, 0.192), (0.07, 0.165), (0.078, 0.14), (0.06, 0.125), (0, 0.13)],
                    gm, segments=18, wave=0.15, waves=5), "CapGlowGreen")


# (kind, builder, width, height): the order and sizes of Fungtopia.Town TownMushrooms.Shapes.
MUSHROOMS = [
    ("FlyAgaric", fly_agaric, 0.3, 0.32),
    ("Porcini", porcini, 0.3, 0.26),
    ("Chanterelle", chanterelle, 0.22, 0.22),
    ("Parasol", parasol, 0.36, 0.38),
    ("InkCap", ink_cap, 0.16, 0.34),
    ("Morel", morel, 0.14, 0.26),
    ("Enoki", enoki, 0.2, 0.3),
    ("Amethyst", amethyst, 0.22, 0.22),
    ("Puffball", puffball, 0.2, 0.16),
    ("CupFungus", cup_fungus, 0.24, 0.1),
    ("Coral", coral, 0.24, 0.22),
    ("Jelly", jelly, 0.22, 0.14),
    ("GlowBlue", glow_blue, 0.2, 0.26),
    ("GlowGreen", glow_green, 0.24, 0.2),
]
MUSHROOM_BUILDERS = {kind: build for kind, build, _, _ in MUSHROOMS}


def garden_mushroom(kind, build, width, height):
    p = Piece(f"Mushroom{kind}")
    build(p, Matrix())
    # Exactly `width` across (about its stem) and `height` tall, as the game expects.
    across = 2 * max(max(abs(v.co.x), abs(v.co.y)) for v in p.bm.verts)
    top = max(v.co.z for v in p.bm.verts)
    for v in p.bm.verts:
        v.co.x *= width / across
        v.co.y *= width / across
        v.co.z *= height / top
    p.finish()


def bracket_fungi(piece, rx, ry, wall, angle, z, size):
    """A little stack of shelf (bracket) fungi growing out of a big stem's wall."""
    a = math.radians(angle)
    for k, (dz, s) in enumerate(((0.0, 1.0), (size * 0.55, 0.75), (-size * 0.5, 0.6))):
        f = 1 - 0.1 * (z + dz) / (wall + 0.2)
        m = T(math.sin(a) * rx * f, math.cos(a) * ry * f, z + dz) @ R("Z", -angle + (k - 1) * 9)
        r = size * s
        piece.add(sphere(r, m @ T(0, r * 0.1, 0), scale=(1, 0.72, 0.28), segments=18, rings=7), "CapBracket")
        piece.add(sphere(r * 0.96, m @ T(0, r * 0.1, -r * 0.1), scale=(1, 0.7, 0.14), segments=16, rings=5), "Gills")


def foot_mushrooms(piece, rx, ry, picks):
    """Small mushrooms by a house's stone foundation (inside its footprint), round its stem: (angle, kind, scale)."""
    for angle, kind, scale in picks:
        a = math.radians(angle)
        m = T(math.sin(a) * (rx + 0.5), math.cos(a) * (ry + 0.5), 0.15) @ R("Z", angle * 3) @ S(scale, scale, scale)
        MUSHROOM_BUILDERS[kind](piece, m)


def player_house():
    # Your house: the biggest, a red spotted cap, a round door, a welcome mat, flower boxes and a
    # little mushroom mailbox. (No window in the cap: it would read as an eye.)
    p, rx, ry, wall, cap = mushroom_house("PlayerHouse", 12, 12, 10, "Cap", spots=11)
    round_door(p, ry, 1.25, "DoorRed")
    welcome_mat(p, ry)
    for angle in (-42, 42):
        front = round_window(p, rx, ry, wall, angle, 2.3, 0.6, "DoorRed")
        window_box(p, rx, ry, angle, 1.45, frame_front=front)
    for angle in (-100, 100, 160, -160):
        round_window(p, rx, ry, wall, angle, 2.4, 0.55, "DoorRed")
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
    fronts = {angle: round_window(p, rx, ry, wall, angle, 1.9, 0.5, "DoorBlue") for angle in (-45, 45, 110, -110, 180)}
    window_box(p, rx, ry, 45, 1.15, ("PetalWhite", "PetalBlue"), frame_front=fronts[45])
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
        round_window(p, rx, ry, wall, angle, 2.0, 0.5, "DoorBlue")
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
        mat = ("Cushion", "FabricPink", "FabricYellow", "FabricBlue", "Leaf", "FabricOrange")[i]
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
    fronts = {angle: round_window(p, rx, ry, wall, angle, 1.7, 0.45, door) for angle in (-48, 48, 115, -115, 180)}
    box_at = 48 if index % 2 else -48
    window_box(p, rx, ry, box_at, 1.0, flowers, frame_front=fronts[box_at])
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


def holed_slab(xs, zs, y0, y1, pocket=None, bevel=0.0):
    """A slab from y0 (back) to y1 (front, facing +Y) over x xs[0]..xs[3], z zs[0]..zs[3], with an
    opening xs[1]..xs[2] x zs[1]..zs[2]: right through it (a frame), or `pocket` deep (a shop
    window). One closed piece, no faces inside; the pocket's floor has material_index 1."""
    part = bmesh.new()
    front = [[part.verts.new((x, y1, z)) for x in xs] for z in zs]
    back = [[part.verts.new((x, y0, z)) for x in xs] for z in zs]
    for i in range(3):
        for j in range(3):
            if (i, j) != (1, 1):
                part.faces.new((front[i][j], front[i][j + 1], front[i + 1][j + 1], front[i + 1][j]))
            if (i, j) != (1, 1) or pocket is not None:
                part.faces.new((back[i][j], back[i + 1][j], back[i + 1][j + 1], back[i][j + 1]))
    rim = [(0, j) for j in range(3)] + [(i, 3) for i in range(3)] + [(3, j) for j in range(3, 0, -1)] + [(i, 0) for i in range(3, 0, -1)]
    for k, (i, j) in enumerate(rim):
        i2, j2 = rim[(k + 1) % len(rim)]
        part.faces.new((front[i][j], back[i][j], back[i2][j2], front[i2][j2]))
    hole = [(1, 1), (1, 2), (2, 2), (2, 1)]
    if pocket is None:
        inner = [back[i][j] for i, j in hole]
    else:
        inner = [part.verts.new((xs[j], y1 - pocket, zs[i])) for i, j in hole]
        part.faces.new(inner)
    for k, (i, j) in enumerate(hole):
        k2 = (k + 1) % 4
        i2, j2 = hole[k2]
        face = part.faces.new((front[i][j], front[i2][j2], inner[k2], inner[k]))
        if pocket is not None and i == i2 == 1:
            face.material_index = 1
    bmesh.ops.recalc_face_normals(part, faces=part.faces)
    if bevel > 0:
        # The outside edges and the opening's rim only (bevelling a pocket's inner corners would
        # fill them in towards whatever stands inside).
        deep = set(inner) if pocket is not None else set()
        sharp = [e for e in part.edges if len(e.link_faces) == 2 and e.calc_face_angle(0.0) > 0.1
                 and not any(v in deep for v in e.verts)]
        bmesh.ops.bevel(part, geom=sharp, offset=bevel, segments=3, affect="EDGES", profile=0.5)
    return part


def front_flower_box(piece, x, y, z, width, flowers):
    """A flower box on a flat front at y, CLEAR in front of it."""
    y += CLEAR
    piece.add(box((width, 0.4, 0.3), T(x, y + 0.2, z), bevel=0.05), "Wood")
    count = max(3, int(width / 0.33))
    step = (width - 0.4) / (count - 1)
    for k in range(count):
        fx = x - width / 2 + 0.2 + k * step
        piece.add(sphere(0.14, T(fx, y + 0.18, z + 0.2), segments=10, rings=5), flowers[k % len(flowers)])
        # Small leaves between the flowers, in front of them (clear of the box).
        if k < count - 1:
            piece.add(sphere(0.06, T(fx + step / 2, y + 0.32, z + 0.205), scale=(1.3, 0.7, 0.5), segments=8, rings=4), "Leaf")


def outfit_stand(piece, matrix, kind, mat):
    """A little outfit on a stand in the shop window: a round foot (sunk 0.025 into the shelf), a pole
    and the clothes (no two faces nearer than CLEAR: the pole ends well inside the foot and the clothes)."""
    def pole(z0, z1):
        piece.add(cylinder(0.025, z1 - z0, matrix @ T(0, 0, (z0 + z1) / 2), segments=8), "Wood")

    piece.add(cylinder(0.16, 0.08, matrix @ T(0, 0, 0.04), segments=16), "WoodDark")
    if kind == "dress":
        pole(0.0525, 1.0)
        piece.add(cylinder(0.3, 0.6, matrix @ T(0, 0, 0.6), segments=20, radius2=0.1), mat)
        piece.add(box((0.24, 0.14, 0.24), matrix @ T(0, 0, 0.98), bevel=0.06), mat)
        piece.add(torus(0.28, 0.03, matrix @ T(0, 0, 0.36), segments=20, sides=6), "Display")
    elif kind == "shirt":
        pole(0.0525, 0.8)
        piece.add(box((0.4, 0.18, 0.42), matrix @ T(0, 0, 0.8), bevel=0.07), mat)
        for side in (-1, 1):
            piece.add(box((0.2, 0.15, 0.13), matrix @ T(side * 0.26, 0, 0.92) @ R("Y", side * 30), bevel=0.05), mat)
        piece.add(box((0.36, 0.17, 0.34), matrix @ T(0, 0, 0.39), bevel=0.06), "FabricBlue")
    else:
        # A sun hat on top of the pole with a scarf hanging below it.
        pole(0.0525, 0.99)
        piece.add(cylinder(0.3, 0.06, matrix @ T(0, 0, 0.92), segments=24), mat)
        piece.add(dome(0.16, 0.14, matrix @ T(0, 0, 0.92), segments=16), mat)
        piece.add(box((0.12, 0.06, 0.45), matrix @ T(0.15, 0.04, 0.6) @ R("Y", 8), bevel=0.03), "PetalBlue")


def clothing_shop():
    # Abby's clothing shop: a pastel boutique mushroom house with a shop front - a striped awning
    # over the door and a big display window with little outfits on stands, flower boxes, and a
    # hanger-shaped sign reading "Abby's" on the cap. (No windows in the cap: they would read as eyes.)
    p, rx, ry, wall, cap = mushroom_house("ClothingShop", 10, 9, 8, "CapMint", wall_mat="WallPink", spots=9)
    capx, capy, base, cap_h = cap
    w, front, back, top = 7.0, ry + 0.5, 1.4, base - 0.1
    # The shop front, one piece from the stem to the facade, with the big window's opening (x from 0.8
    # to 3.1, z from 0.75 to 2.6) a pocket 0.7 deep; its floor is the display shelf.
    wx0, wx1, wz0, wz1 = 0.8, 3.1, 0.75, 2.6
    shelf, depth = wz0 + 0.045, 0.7
    p.add(holed_slab((-w / 2, wx0, wx1, w / 2), (0, shelf, wz1, top), back, front, pocket=depth, bevel=0.06),
          ("WallPink", "Display"))
    # The big window: a glowing pane CLEAR in front of the pocket's back, reaching under the facade
    # all round (CLEAR clear of the pocket's sides), a wooden frame CLEAR in front of the facade, and
    # outfits on stands, their feet sunk into the shelf.
    pane_y = front - depth + CLEAR + PANE / 2
    p.add(box((wx1 - wx0 - 2 * CLEAR, PANE, wz1 - shelf - 2 * CLEAR), T((wx0 + wx1) / 2, pane_y, (shelf + wz1) / 2), bevel=0.0), "Window")
    p.add(holed_slab((wx0 - 0.07, wx0 + 0.07, wx1 - 0.07, wx1 + 0.07), (wz0 - 0.07, wz0 + 0.07, wz1 - 0.07, wz1 + 0.07),
                     front + CLEAR, front + CLEAR + 0.12, bevel=0.03), "Wood")
    for k, (kind, mat) in enumerate((("dress", "PetalPink"), ("shirt", "FabricYellow"), ("hat", "AwningCream"))):
        x = wx0 + (k + 0.5) * (wx1 - wx0) / 3
        outfit_stand(p, T(x, front - 0.3, shelf - 0.025) @ S(0.95, 0.95, 0.95), kind, mat)
    # Its flower box sits just below the frame (the flowers clear of it).
    front_flower_box(p, (wx0 + wx1) / 2, front, wz0 - 0.07 - CLEAR - 0.34, wx1 - wx0 + 0.2, ("PetalPink", "PetalWhite", "PetalYellow"))
    # The door, a small round window on the left with its own flower box below it, two little lanterns.
    arched_door(p, front, 1.5, 2.4, "DoorLilac")
    window_layers(p, T(-2.3, front + CLEAR, 1.8) @ R("X", -90), 0.55, "Wood")
    front_flower_box(p, -2.3, front, 1.8 - 0.55 - 0.15 - CLEAR - 0.34, 1.4, ("PetalBlue", "PetalPink"))
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
        p.add(cylinder(0.12, 0.2, T(x, -d / 2 + 0.5, 1.64), segments=12, radius2=0.1), "Jar")
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
        p.add(sphere(0.13, T(math.cos(a) * 0.36, math.sin(a) * 0.36, 0.38), scale=(1, 0.8, 0.5), segments=10, rings=5),
              "Leaf" if k % 2 else "LeafLight")
    # Two little mushrooms growing among the flowers.
    fly_agaric(p, T(0.2, -0.18, 0.32) @ S(0.75, 0.75, 0.75))
    amethyst(p, T(-0.24, 0.12, 0.32) @ R("Z", 40) @ S(0.8, 0.8, 0.8))
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


# The flower clumps' palettes (Fungtopia.Town TownLayout.FlowerVariants), many hues.
FLOWER_PALETTES = [
    ("PetalPink", "PetalYellow", "PetalBlue"),
    ("PetalRed", "PetalOrange", "PetalWhite"),
    ("PetalPurple", "PetalPink", "PetalYellow"),
    ("PetalBlue", "PetalWhite", "PetalOrange"),
]


def flowers(index):
    # A small clump of flowers (free ground decoration, walked over): 0.5 m across, 0.35 m tall,
    # in lush green leaves and grass with yellow-green highlights.
    p = Piece(f"Flowers{index}")
    petals = FLOWER_PALETTES[index]
    for k, (x, y) in enumerate(((-0.1, -0.06), (0.11, -0.03), (0.0, 0.12))):
        flower(p, T(x, y, 0) @ R("Z", k * 70 + index * 25), petals[k], scale=0.85 + 0.1 * ((k + index) % 3))
    for k in range(3):
        a = 2 * math.pi * k / 3 + 0.5 + index
        p.add(sphere(0.1, T(math.cos(a) * 0.14, math.sin(a) * 0.14, 0.03), scale=(1, 0.8, 0.5), segments=10, rings=5),
              "LeafLight" if k == index % 3 else "Leaf")
    for k in range(7):
        a = 2 * math.pi * k / 7 + index * 0.4
        r = 0.12 + 0.07 * (k % 2)
        h = 0.14 + 0.05 * ((k * 3) % 4) / 3
        base = T(math.cos(a) * r, math.sin(a) * r, 0)
        p.add(cylinder(0.022, h, base @ R("Y", 18 * math.cos(a)) @ R("X", -18 * math.sin(a)) @ T(0, 0, h / 2), segments=5,
                       radius2=0.002), "LeafLight" if k % 3 == 0 else "Leaf")
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
for i in range(len(FLOWER_PALETTES)):
    flowers(i)
for kind, build, width, height in MUSHROOMS:
    garden_mushroom(kind, build, width, height)

path = os.path.join(HERE, "town.glb")
bpy.ops.export_scene.gltf(filepath=path, export_format="GLB", export_yup=True)
print("town: exported", path)
