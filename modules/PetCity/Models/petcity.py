# Builds Pet City and exports it next to this script as petcity.glb.
# Run from the repository root:
#   blender -b --factory-startup --python modules/PetCity/Models/petcity.py
#
# Chunky, cute cartoon city pieces (one object each; materials carry plain colors only, the game
# draws them toon-shaded with ink outlines, recolors "Wall" and "Roof" per building and "CapRoof" (a
# block's mushroom cap roof) per style, and gives the mushroom caps a glossy highlight):
#   Tower3 .. Tower10   a tall city block on a 12 m x 12 m footprint: a 4 m shop floor (shop window,
#                       door, striped awning) and 3 .. 10 upper stories of 3 m with arched windows on
#                       every side; its top at 4 + 3 n m, where a roof goes.
#   RoofCap, RoofTerrace  the two roofs (base at 0, 3 m tall): a big spotted mushroom cap, or a flat
#                       terrace with a parapet and a little mushroom water tower.
#   PetStore            Lucy's Pet Store, 18 m x 12 m x 11 m: an open front under a big "Pet Store"
#                       sign with little hearts, a striped awning, shelves of pet food, a mushroom-cap
#                       roof. PetStoreInside: its pet beds, cages and the crow's perch.
#   Counter             the shop counter (5 m x 1 m), drawn at the store's open front.
#   Kitten, Puppy, Bunny, Hamster, MushroomCrow  the pets on display, at their spots in the store.
#   RoofCapWavy, RoofCapBell, RoofCapFlat  the other mushroom cap roofs (3 m tall at most).
#   LampCapDome .. LampCapCone  the city lamps' caps (one shape each; the game picks one per lamp).
#   Fungus*             the little fungi (fly agaric, bolete, chanterelle, parasol, ink cap, morel, enoki,
#                       amethyst, puffball, bracket, cup, coral, jelly, glow cap), each 1 m tall; the game
#                       scales, tilts and tints them ("FungusCap" / "FungusGlow" are white).
#   CityLamp, Bench, Planter, Statue  the Map's solid props (drawn scaled to each prop's size):
#                       a glowing street lamp's post and bulb (0.6 m across, 2.6 m tall), a bench (1.6 m long,
#                       0.9 m tall, its seat along X, facing +Y), a round planter (1.2 m across, 0.7 m
#                       tall), a verdigris Mushroom Cat statue on a pedestal (2.2 m across, 3.4 m tall).
# Every piece stands at the origin on the ground, centered on its footprint, its front toward
# Blender +Y (Godot -Z, as a Map building at rotation 0). The store's contents are placed in the
# store's own frame. Only creatures (the pets) have eyes; buildings and objects never do.
import itertools
import math
import os

import bmesh
import bpy
from mathutils import Matrix

HERE = os.path.dirname(os.path.abspath(__file__))

COLORS = {
    # Candy-bright paints, clean bright stone, lush greens and flowers in many hues (the same values
    # as PetCityLook.Paints in Core, which the tests check this .glb against).
    "Wall": (1.0, 0.7, 0.56),
    "Roof": (1.0, 0.32, 0.42),
    "Trim": (1.0, 0.96, 0.86),
    "Plinth": (0.84, 0.8, 0.92),
    "Window": (0.5, 0.8, 1.0),
    "Frame": (1.0, 0.99, 0.96),
    "Door": (0.86, 0.44, 0.2),
    "Awning": (1.0, 0.34, 0.5),
    "AwningStripe": (1.0, 0.98, 0.92),
    "FlowerBox": (0.86, 0.5, 0.26),
    "Leaf": (0.28, 0.82, 0.22),
    "LeafLight": (0.64, 0.92, 0.16),
    "LeafDeep": (0.1, 0.68, 0.32),
    "Flower": (1.0, 0.84, 0.1),
    "FlowerPink": (1.0, 0.36, 0.68),
    "FlowerBlue": (0.32, 0.56, 1.0),
    "FlowerPurple": (0.7, 0.36, 1.0),
    "FlowerOrange": (1.0, 0.52, 0.12),
    # The mushrooms (vivid: saturated bright caps, pure white spots, clean light cream stems; the
    # same values as PetCityLook in Core). FungusCap and FungusGlow are white: the game tints them
    # per mushroom (each kind its colors), as it does LampCap per lamp.
    "Spots": (1.0, 1.0, 1.0),
    "Stem": (1.0, 0.96, 0.86),
    "Cap": (1.0, 0.12, 0.12),
    "CapRoof": (1.0, 0.1, 0.16),
    "StoreCap": (1.0, 0.3, 0.6),
    "LampCap": (1.0, 0.3, 0.6),
    "LampGlow": (1.0, 0.9, 0.58),
    "FungusCap": (1.0, 1.0, 1.0),
    "FungusGlow": (1.0, 1.0, 1.0),
    "MorelPit": (0.56, 0.3, 0.12),
    "Ink": (0.3, 0.22, 0.46),
    "StoreWall": (0.62, 0.88, 1.0),
    "StoreRoof": (1.0, 0.56, 0.72),
    "FloorTile": (1.0, 0.92, 0.76),
    "SignBoard": (1.0, 0.98, 0.9),
    "SignTrim": (1.0, 0.4, 0.62),
    "SignText": (0.94, 0.18, 0.42),
    "Heart": (1.0, 0.3, 0.5),
    "Glow": (1.0, 0.9, 0.58),
    "Wood": (0.9, 0.56, 0.28),
    "WoodDark": (0.66, 0.36, 0.18),
    "CounterTop": (1.0, 0.62, 0.74),
    "Bag1": (0.3, 0.6, 1.0),
    "Bag2": (1.0, 0.78, 0.2),
    "Bag3": (0.4, 0.86, 0.36),
    "Bowl": (1.0, 0.32, 0.38),
    "Cushion": (0.72, 0.54, 1.0),
    "CushionIn": (1.0, 0.95, 0.9),
    "Bars": (1.0, 0.82, 0.3),
    "Straw": (1.0, 0.86, 0.42),
    "Stone": (0.92, 0.9, 0.97),
    "Bone": (1.0, 0.96, 0.88),
    "Bronze": (0.36, 0.86, 0.72),
    "Pot": (1.0, 0.52, 0.32),
    # The pets (only they have eyes).
    "Cream": (1.0, 0.96, 0.88),
    "Eye": (0.07, 0.04, 0.06),
    "EyeShine": (1.0, 1.0, 1.0),
    "Blush": (1.0, 0.5, 0.56),
    "Nose": (0.4, 0.16, 0.18),
    "CatFur": (1.0, 0.6, 0.28),
    "PupFur": (0.96, 0.86, 0.68),
    "PupEar": (0.66, 0.44, 0.3),
    "BunnyFur": (1.0, 0.98, 0.96),
    "BunnyEar": (1.0, 0.7, 0.76),
    "HamFur": (0.94, 0.7, 0.42),
    "HamLight": (1.0, 0.94, 0.86),
    "CrowBody": (0.16, 0.15, 0.27),
    "CrowLight": (0.36, 0.33, 0.52),
    "Beak": (1.0, 0.68, 0.24),
}
ORDER = list(COLORS)
GREENS = ("Leaf", "LeafLight", "LeafDeep")
FLOWERS = ("Flower", "FlowerPink", "FlowerBlue", "FlowerPurple", "FlowerOrange")
FLOWER_TURN = itertools.count()


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
        self.used = []

    def add(self, part, mat):
        mesh = bpy.data.meshes.new("tmp")
        part.to_mesh(mesh)
        part.free()
        self.add_mesh(mesh, mat)
        bpy.data.meshes.remove(mesh)

    def add_mesh(self, mesh, mat):
        if mat not in self.used:
            self.used.append(mat)
        for poly in mesh.polygons:
            poly.material_index = self.used.index(mat)
        self.bm.from_mesh(mesh)

    def finish(self, location=(0.0, 0.0, 0.0)):
        mesh = bpy.data.meshes.new(self.name)
        self.bm.to_mesh(mesh)
        self.bm.free()
        for name in self.used:
            mesh.materials.append(bpy.data.materials[name])
        for poly in mesh.polygons:
            poly.use_smooth = True
        obj = bpy.data.objects.new(self.name, mesh)
        obj.location = location
        bpy.context.scene.collection.objects.link(obj)
        return obj


def box(size, matrix, bevel=0.06, segments=2):
    """A chunky box with rounded edges, `size` (x, y, z) centered at `matrix`."""
    part = bmesh.new()
    bmesh.ops.create_cube(part, size=1.0)
    bmesh.ops.transform(part, matrix=S(*size), verts=part.verts)
    if bevel > 0:
        bevel = min(bevel, min(size) * 0.45)
        bmesh.ops.bevel(part, geom=list(part.edges) + list(part.verts), offset=bevel, segments=segments,
                        affect="EDGES", profile=0.5)
    bmesh.ops.transform(part, matrix=matrix, verts=part.verts)
    return part


def cylinder(radius, depth, matrix, segments=24, radius2=None):
    part = bmesh.new()
    bmesh.ops.create_cone(part, cap_ends=True, segments=segments, radius1=radius,
                          radius2=radius if radius2 is None else radius2, depth=depth, matrix=matrix)
    return part


def sphere(radius, matrix, scale=(1, 1, 1), segments=16, rings=8):
    part = bmesh.new()
    bmesh.ops.create_uvsphere(part, u_segments=segments, v_segments=rings, radius=radius,
                              matrix=matrix @ S(*scale))
    return part


def dome(radius, height, matrix, segments=32, rings=10):
    """The upper half of a squashed sphere (a mushroom cap), closed underneath."""
    part = sphere(1.0, Matrix(), segments=segments, rings=rings * 2)
    bmesh.ops.delete(part, geom=[v for v in part.verts if v.co.z < -1e-4], context="VERTS")
    bmesh.ops.contextual_create(part, geom=[e for e in part.edges if e.is_boundary])
    bmesh.ops.transform(part, matrix=matrix @ S(radius, radius, height), verts=part.verts)
    return part


def revolve(profile, matrix, segments=20, wobble=0.0, waves=0):
    """A closed surface of revolution about Z from `profile`, (r, z) points from the top (r = 0) round to
    the bottom (r = 0); `wobble` waves the rim up and down `waves` times (a wavy cap)."""
    part = bmesh.new()
    rmax = max(r for r, _ in profile) or 1.0
    rings = []
    for r, z in profile:
        if r <= 1e-6:
            rings.append([part.verts.new((0.0, 0.0, z))])
            continue
        ring = []
        for k in range(segments):
            a = 2 * math.pi * k / segments
            dz = wobble * math.sin(waves * a) * (r / rmax) ** 2 if waves else 0.0
            ring.append(part.verts.new((math.cos(a) * r, math.sin(a) * r, z + dz)))
        rings.append(ring)
    for top, bottom in zip(rings, rings[1:]):
        for k in range(segments):
            k2 = (k + 1) % segments
            if len(top) == 1 and len(bottom) == 1:
                continue
            if len(top) == 1:
                part.faces.new((top[0], bottom[k2], bottom[k]))
            elif len(bottom) == 1:
                part.faces.new((top[k], top[k2], bottom[0]))
            else:
                part.faces.new((top[k], top[k2], bottom[k2], bottom[k]))
    bmesh.ops.recalc_face_normals(part, faces=part.faces)
    bmesh.ops.transform(part, matrix=matrix, verts=part.verts)
    return part


def spots_on_dome(piece, radius, height, base, count=7, size=0.14, mat="Spots", seed=0.0):
    """White spots sitting on a cap of `radius` x `height` whose base is at `base` (a matrix)."""
    for i in range(count):
        a = 2 * math.pi * (i / count) + seed
        t = 0.62 if i % 2 == 0 else 0.3
        lat = math.acos(t)
        x, y = math.cos(a) * math.sin(lat) * radius, math.sin(a) * math.sin(lat) * radius
        z = math.cos(lat) * height
        r = radius * size * (1.0 if i % 3 else 0.75)
        piece.add(sphere(r, base @ T(x, y, z), scale=(1, 1, 0.4), segments=12, rings=6), mat)
    piece.add(sphere(radius * size * 1.1, base @ T(0, 0, height), scale=(1, 1, 0.35), segments=12, rings=6), mat)


def heart(piece, size, matrix, depth=0.12, mat="Heart"):
    """A puffy heart standing upright, facing Blender +Y, `size` meters tall, centered at `matrix`."""
    part = bmesh.new()
    verts = []
    n = 28
    for i in range(n):
        t = 2 * math.pi * i / n
        x = 16 * math.sin(t) ** 3
        z = 13 * math.cos(t) - 5 * math.cos(2 * t) - 2 * math.cos(3 * t) - math.cos(4 * t)
        verts.append(part.verts.new((x / 34 * size, -depth / 2, (z + 2) / 34 * size)))
    front = part.faces.new(verts)
    ext = bmesh.ops.extrude_face_region(part, geom=[front])
    moved = [g for g in ext["geom"] if isinstance(g, bmesh.types.BMVert)]
    bmesh.ops.translate(part, vec=(0, depth, 0), verts=moved)
    bmesh.ops.recalc_face_normals(part, faces=part.faces)
    bmesh.ops.transform(part, matrix=matrix, verts=part.verts)
    piece.add(part, mat)


def text(piece, body, size, matrix, depth=0.12, mat="SignText"):
    """Chunky 3D letters reading along Blender +X, facing +Y, centered at `matrix`."""
    curve = bpy.data.curves.new("Text", type="FONT")
    curve.body = body
    curve.size = size
    curve.extrude = depth / 2
    curve.bevel_depth = size * 0.025
    curve.align_x = "CENTER"
    curve.align_y = "CENTER"
    obj = bpy.data.objects.new("Text", curve)
    bpy.context.scene.collection.objects.link(obj)
    depsgraph = bpy.context.evaluated_depsgraph_get()
    mesh = bpy.data.meshes.new_from_object(obj.evaluated_get(depsgraph))
    mesh.transform(matrix @ R("Z", 180) @ R("X", 90))
    piece.add_mesh(mesh, mat)
    bpy.data.meshes.remove(mesh)
    bpy.data.objects.remove(obj)
    bpy.data.curves.remove(curve)


def eyes(piece, matrix, spacing, size):
    """Big round cute eyes with sparkles, looking along Blender +Y from `matrix`."""
    for side in (-1, 1):
        piece.add(sphere(size, matrix @ T(side * spacing, 0, 0), scale=(1, 0.55, 1.2), segments=12, rings=6), "Eye")
        piece.add(sphere(size * 0.35, matrix @ T(side * spacing - size * 0.3, size * 0.5, size * 0.45),
                         segments=8, rings=4), "EyeShine")


def blush(piece, matrix, spacing, size):
    for side in (-1, 1):
        piece.add(sphere(size, matrix @ T(side * spacing, 0, 0), scale=(1.2, 0.4, 0.7), segments=10, rings=5), "Blush")


# City blocks ---------------------------------------------------------------------------------------

BLOCK = 12.0
GROUND_FLOOR = 4.0
STORY = 3.0


def on_faces(piece, build):
    """Runs `build(piece, frame, half_width)` once per side; each frame looks out along +Y from that face."""
    for k in range(4):
        build(piece, R("Z", 90 * k) @ T(0, BLOCK / 2, 0), BLOCK / 2)


def window(piece, frame, x, z, flower=False):
    """An arched window with a cream frame and a cross bar, its sill at z, on a wall at frame's y = 0."""
    w, h = 1.3, 1.5
    # The frame sits behind the glass, so only its rim shows around it.
    piece.add(box((w + 0.24, 0.16, h + 0.2), frame @ T(x, 0.0, z + h / 2), bevel=0.05, segments=1), "Frame")
    piece.add(cylinder((w + 0.24) / 2, 0.16, frame @ T(x, 0.0, z + h) @ R("X", 90), segments=14), "Frame")
    piece.add(box((w, 0.2, h), frame @ T(x, 0.04, z + h / 2), bevel=0), "Window")
    piece.add(cylinder(w / 2, 0.2, frame @ T(x, 0.04, z + h) @ R("X", 90), segments=14), "Window")
    piece.add(box((w, 0.26, 0.07), frame @ T(x, 0.05, z + h * 0.62), bevel=0), "Frame")
    piece.add(box((0.07, 0.26, h + w / 2 - 0.05), frame @ T(x, 0.05, z + (h + w / 2) / 2), bevel=0), "Frame")
    piece.add(box((w + 0.4, 0.34, 0.14), frame @ T(x, 0.14, z - 0.05), bevel=0.04, segments=1), "Trim")
    if flower:
        piece.add(box((w + 0.1, 0.36, 0.3), frame @ T(x, 0.36, z - 0.24), bevel=0.06, segments=1), "FlowerBox")
        k = int(round(x * 3 + z * 7))
        for i in range(4):
            fx = x - 0.45 + i * 0.3
            piece.add(sphere(0.15, frame @ T(fx, 0.38, z - 0.02), scale=(1, 1, 0.8), segments=10, rings=5),
                      GREENS[(k + i) % len(GREENS)])
            if i % 2 == 0:
                piece.add(sphere(0.08, frame @ T(fx + 0.06, 0.48, z + 0.08), segments=8, rings=4),
                          FLOWERS[next(FLOWER_TURN) % len(FLOWERS)])


def tower(stories):
    p = Piece(f"Tower{stories}")
    top = GROUND_FLOOR + stories * STORY
    # A sturdy plinth that reaches below the ground (for slopes), the walls, trim bands.
    p.add(box((BLOCK + 0.3, BLOCK + 0.3, 1.6), T(0, 0, -0.5), bevel=0.1), "Plinth")
    p.add(box((BLOCK, BLOCK, top), T(0, 0, top / 2), bevel=0.2), "Wall")
    p.add(box((BLOCK + 0.5, BLOCK + 0.5, 0.35), T(0, 0, GROUND_FLOOR), bevel=0.1), "Trim")
    p.add(box((BLOCK + 0.7, BLOCK + 0.7, 0.45), T(0, 0, top - 0.1), bevel=0.12), "Trim")
    # Rounded corner pillars.
    for sx in (-1, 1):
        for sy in (-1, 1):
            p.add(cylinder(0.42, top - 0.4, T(sx * BLOCK / 2, sy * BLOCK / 2, top / 2 - 0.1), segments=16), "Trim")

    def upper(piece, frame, half):
        for s in range(stories):
            z = GROUND_FLOOR + s * STORY + 0.7
            for i in range(4):
                x = (i - 1.5) * (BLOCK / 4)
                window(piece, frame, x, z, flower=(s % 2 == 0 and i % 3 == 0))

    on_faces(p, upper)

    def shop_floor(piece, frame, half):
        # Two small shop windows on each side.
        for x in (-3.0, 3.0):
            piece.add(box((3.0, 0.2, 2.0), frame @ T(x, 0.02, 1.9), bevel=0), "Window")
            piece.add(box((3.3, 0.16, 2.3), frame @ T(x, 0.0, 1.9), bevel=0.06, segments=1), "Frame")

    for k in (1, 2, 3):
        shop_floor(p, R("Z", 90 * k) @ T(0, BLOCK / 2, 0), BLOCK / 2)

    # The front: a wide shop window, a round-topped door and a striped awning over both.
    f = T(0, BLOCK / 2, 0)
    p.add(box((5.6, 0.18, 2.6), f @ T(-2.2, 0.0, 1.75), bevel=0.06, segments=1), "Frame")
    p.add(box((5.2, 0.22, 2.3), f @ T(-2.2, 0.02, 1.75), bevel=0), "Window")
    p.add(box((0.08, 0.26, 2.3), f @ T(-2.2, 0.04, 1.75), bevel=0), "Frame")
    p.add(box((1.8, 0.18, 2.5), f @ T(3.2, 0.02, 1.25), bevel=0.06, segments=1), "Door")
    p.add(cylinder(0.9, 0.18, f @ T(3.2, 0.02, 2.5) @ R("X", 90), segments=16), "Door")
    p.add(sphere(0.1, f @ T(3.75, 0.14, 1.3), segments=8, rings=4), "Flower")
    for i in range(10):
        x = -5.4 + i * 1.2 + 0.6
        mat = "Awning" if i % 2 == 0 else "AwningStripe"
        p.add(box((1.2, 1.5, 0.12), f @ T(x, 0.75, 3.55) @ R("X", -14), bevel=0), mat)
        p.add(sphere(0.6, f @ T(x, 1.47, 3.36), scale=(1, 0.25, 0.55), segments=12, rings=6), mat)
    return p.finish()


def roof_cap():
    p = Piece("RoofCap")
    # A big spotted mushroom cap covering the whole top, on a short cream collar.
    p.add(cylinder(BLOCK * 0.5, 0.5, T(0, 0, 0.25), segments=32), "Trim")
    p.add(dome(BLOCK * 0.72, 2.6, T(0, 0, 0.4), segments=40), "CapRoof")
    spots_on_dome(p, BLOCK * 0.72, 2.6, T(0, 0, 0.4), count=9, size=0.12, seed=0.4)
    return p.finish()


def roof_cap_variant(name, profile, wobble=0.0, waves=0, spots=True):
    p = Piece(name)
    p.add(cylinder(BLOCK * 0.5, 0.5, T(0, 0, 0.25), segments=32), "Trim")
    p.add(revolve(profile, T(0, 0, 0.4), segments=40, wobble=wobble, waves=waves), "CapRoof")
    if spots:
        spots_on_dome(p, BLOCK * 0.62, 1.9, T(0, 0, 0.4), count=9, size=0.12, seed=0.4)
    return p.finish()


def roof_caps():
    """The other mushroom cap roofs (3 m at most, like RoofCap): wavy, a bell and a flat parasol with a knob."""
    w = BLOCK * 0.72
    roof_cap_variant("RoofCapWavy", [(0, 2.2), (w * 0.35, 2.05), (w * 0.65, 1.55), (w * 0.9, 0.8), (w, 0.2),
                                     (w * 0.9, 0.0), (0, 0.0)], wobble=0.35, waves=7)
    roof_cap_variant("RoofCapBell", [(0, 2.6), (w * 0.3, 2.5), (w * 0.55, 2.1), (w * 0.72, 1.3), (w * 0.82, 0.3),
                                     (w * 0.92, -0.2), (w * 0.7, 0.0), (0, 0.0)])
    roof_cap_variant("RoofCapFlat", [(0, 2.7), (w * 0.12, 2.6), (w * 0.22, 2.0), (w * 0.6, 1.75), (w * 0.95, 1.0),
                                     (w, 0.6), (w * 0.9, 0.45), (0, 0.4)], spots=False)


def roof_terrace():
    p = Piece("RoofTerrace")
    # A flat terrace behind a parapet, a little mushroom water tower and potted mushrooms.
    p.add(box((BLOCK + 0.2, BLOCK + 0.2, 0.3), T(0, 0, 0.15), bevel=0.05), "Plinth")
    for k in range(4):
        frame = R("Z", 90 * k)
        p.add(box((BLOCK + 0.6, 0.45, 0.9), frame @ T(0, BLOCK / 2, 0.45), bevel=0.12), "Roof")
    p.add(cylinder(0.25, 1.0, T(2.5, -2.0, 0.8), segments=12), "WoodDark")
    p.add(cylinder(1.4, 1.6, T(2.5, -2.0, 2.0), segments=24), "Stem")
    p.add(dome(1.9, 1.1, T(2.5, -2.0, 2.8)), "Cap")
    spots_on_dome(p, 1.9, 1.1, T(2.5, -2.0, 2.8), count=6, size=0.16)
    for x, y in ((-3.5, 3.0), (-1.5, 3.4), (3.6, 3.2), (-3.8, -3.4)):
        p.add(cylinder(0.4, 0.5, T(x, y, 0.55), radius2=0.5, segments=14), "Pot")
        p.add(cylinder(0.44, 0.06, T(x, y, 0.8), segments=14), "LeafDeep")
    return p.finish()


# Lucy's Pet Store ---------------------------------------------------------------------------------

STORE_W, STORE_D, STORE_H = 18.0, 12.0, 11.0
OPEN_HALF = 6.5  # half the open front's width
OPEN_TOP = 5.4


def pet_store():
    p = Piece("PetStore")
    fy = STORE_D / 2
    p.add(box((STORE_W + 0.3, STORE_D + 0.3, 1.6), T(0, 0, -0.5), bevel=0.1), "Plinth")
    p.add(box((STORE_W - 1.0, STORE_D - 1.0, 0.2), T(0, 0, 0.35), bevel=0), "FloorTile")
    for i in range(8):
        for j in range(5):
            if (i + j) % 2 == 0:
                p.add(box((2.0, 2.0, 0.02), T(-7.5 + i * 2.14, -4.4 + j * 2.0, 0.46), bevel=0), "StoreRoof")
    # Walls: back, sides, and the front around the open shopfront.
    p.add(box((STORE_W, 0.6, 9.2), T(0, -fy + 0.3, 4.6), bevel=0.15), "StoreWall")
    for sx in (-1, 1):
        p.add(box((0.6, STORE_D, 9.2), T(sx * (STORE_W / 2 - 0.3), 0, 4.6), bevel=0.15), "StoreWall")
        px = sx * (OPEN_HALF + (STORE_W / 2 - OPEN_HALF) / 2)
        p.add(box((STORE_W / 2 - OPEN_HALF, 0.6, 9.2), T(px, fy - 0.3, 4.6), bevel=0.15), "StoreWall")
        # A round display window in each front pier, a heart in it.
        p.add(cylinder(0.85, 0.7, T(px, fy - 0.3, 3.0) @ R("X", 90), segments=24), "Frame")
        p.add(cylinder(0.7, 0.76, T(px, fy - 0.3, 3.0) @ R("X", 90), segments=24), "Window")
        heart(p, 0.7, T(px, fy + 0.12, 2.75), depth=0.1)
        p.add(cylinder(0.32, 9.0, T(sx * OPEN_HALF, fy, 4.5), segments=16), "Trim")
    p.add(box((STORE_W, 0.6, 9.2 - OPEN_TOP), T(0, fy - 0.3, (9.2 + OPEN_TOP) / 2), bevel=0.15), "StoreWall")
    p.add(box((STORE_W + 0.6, STORE_D + 0.6, 0.5), T(0, 0, 9.35), bevel=0.15), "Trim")
    # The roof: a big pink spotted mushroom cap.
    p.add(dome(STORE_W * 0.56, 1.7, T(0, 0, 9.55) @ S(1, 0.72, 1), segments=40), "StoreCap")
    spots_on_dome(p, STORE_W * 0.56, 1.7, T(0, 0, 9.55) @ S(1, 0.72, 1), count=9, size=0.08, seed=0.3)

    # The big sign: a cream board with a pink border, "Pet Store" and little hearts.
    sz = 7.4
    p.add(box((12.6, 0.4, 2.6), T(0, fy + 0.15, sz), bevel=0.25), "SignTrim")
    p.add(box((12.0, 0.42, 2.1), T(0, fy + 0.2, sz), bevel=0.2), "SignBoard")
    text(p, "Pet Store", 1.55, T(0, fy + 0.46, sz - 0.08), depth=0.16)
    for x, z, s in ((-5.2, 0.0, 0.85), (5.2, 0.0, 0.85), (-4.5, 0.75, 0.42), (4.6, -0.7, 0.4), (0.0, 1.55, 0.7),
                    (-1.2, 1.4, 0.4), (1.2, 1.4, 0.4)):
        heart(p, s, T(x, fy + 0.5, sz + z - s * 0.45), depth=0.14)
    # A striped awning over the open front, with a scalloped edge.
    n = 13
    for i in range(n):
        x = -OPEN_HALF + (i + 0.5) * (2 * OPEN_HALF / n)
        mat = "Awning" if i % 2 == 0 else "AwningStripe"
        p.add(box((2 * OPEN_HALF / n, 1.6, 0.12), T(x, fy + 0.8, OPEN_TOP + 0.1) @ R("X", -16), bevel=0), mat)
        p.add(sphere(OPEN_HALF / n, T(x, fy + 1.58, OPEN_TOP - 0.12), scale=(1, 0.25, 0.6), segments=12, rings=6), mat)

    # Inside: shelves of pet food and bowls on the back wall, warm lamps under the ceiling.
    by = -fy + 0.9
    for z in (1.6, 3.2):
        p.add(box((10.0, 0.8, 0.14), T(0, by, z), bevel=0.04, segments=1), "Wood")
        for i in range(9):
            x = -4.4 + i * 1.1
            bag = ("Bag1", "Bag2", "Bag3")[(i + int(z)) % 3]
            if (i + int(z)) % 4 == 3:
                p.add(cylinder(0.32, 0.2, T(x, by, z + 0.17), radius2=0.42, segments=16), "Bowl")
            else:
                p.add(box((0.7, 0.45, 0.95), T(x, by, z + 0.55), bevel=0.15), bag)
                heart(p, 0.28, T(x, by + 0.26, z + 0.48), depth=0.04, mat="AwningStripe")
    for x in (-4.0, 0.0, 4.0):
        p.add(cylinder(0.05, 1.2, T(x, 0, 8.4), segments=8), "WoodDark")
        p.add(sphere(0.35, T(x, 0, 7.6), segments=14, rings=7), "Glow")
        p.add(dome(0.6, 0.45, T(x, 0, 7.75)), "SignTrim")
    return p.finish()


def counter():
    p = Piece("Counter")
    # The shop counter: wooden front with a paw print, a pink top, a bell and a jar of treats.
    p.add(box((5.0, 1.0, 1.0), T(0, 0, 0.5), bevel=0.12), "Wood")
    p.add(box((5.3, 1.25, 0.16), T(0, 0, 1.08), bevel=0.06), "CounterTop")
    for x in (-1.5, 1.5):
        p.add(sphere(0.2, T(x, 0.5, 0.45), scale=(1.1, 0.25, 0.9), segments=12, rings=6), "AwningStripe")
        for k in range(4):
            a = math.radians(-50 + k * 33)
            p.add(sphere(0.09, T(x + math.sin(a) * 0.32, 0.5, 0.45 + math.cos(a) * 0.3), scale=(1, 0.3, 1.1),
                         segments=10, rings=5), "AwningStripe")
    p.add(cylinder(0.2, 0.05, T(-1.6, 0, 1.19), segments=16), "Bars")
    p.add(dome(0.14, 0.14, T(-1.6, 0, 1.2)), "Bars")
    p.add(cylinder(0.25, 0.5, T(1.7, 0, 1.41), segments=16), "Window")
    p.add(cylinder(0.27, 0.08, T(1.7, 0, 1.68), segments=16), "SignTrim")
    for k in range(5):
        p.add(sphere(0.08, T(1.62 + (k % 2) * 0.16, -0.05 + (k % 3) * 0.05, 1.25 + k * 0.07),
                     scale=(1.4, 0.7, 0.7), segments=8, rings=4), "Bone")
    heart(p, 0.45, T(0, 0.52, 0.5), depth=0.06)
    return p.finish()


def pet_bed(p, x, y, color):
    p.add(cylinder(0.75, 0.14, T(x, y, 0.52), segments=24), color)
    for i in range(16):
        a = 2 * math.pi * i / 16
        p.add(sphere(0.22, T(x + math.cos(a) * 0.68, y + math.sin(a) * 0.68, 0.66), segments=12, rings=6), color)
    p.add(cylinder(0.56, 0.08, T(x, y, 0.62), segments=24), "CushionIn")


def cage(p, x, y, z):
    """A round golden cage on a wooden stand, straw inside, its door toward the front."""
    p.add(cylinder(0.12, z - 0.4, T(x, y, 0.4 + (z - 0.4) / 2), segments=10), "Wood")
    p.add(cylinder(0.5, 0.06, T(x, y, 0.45), segments=16), "Wood")
    p.add(cylinder(0.85, 0.16, T(x, y, z), segments=24), "WoodDark")
    p.add(cylinder(0.78, 0.08, T(x, y, z + 0.1), segments=24), "Straw")
    for i in range(14):
        a = 2 * math.pi * i / 14
        p.add(cylinder(0.025, 1.1, T(x + math.cos(a) * 0.8, y + math.sin(a) * 0.8, z + 0.63), segments=6), "Bars")
    p.add(dome(0.84, 0.5, T(x, y, z + 1.15), segments=24), "Bars")
    p.add(sphere(0.1, T(x, y, z + 1.72), segments=8, rings=4), "Bars")
    p.add(cylinder(0.81, 0.06, T(x, y, z + 0.62), segments=24), "Bars")


def store_inside():
    p = Piece("PetStoreInside")
    # Pet beds on the right, cages on the left, the crow's perch by the counter.
    pet_bed(p, 3.0, 0.5, "Cushion")
    pet_bed(p, 5.4, -1.6, "SignTrim")
    pet_bed(p, 2.0, -2.6, "Bag1")
    cage(p, -5.4, -1.8, 1.8)
    cage(p, -3.2, -3.0, 1.4)
    # The perch: a wooden stand with a branch, a seed cup and a price tag with hearts.
    x, y = -3.4, 1.4
    p.add(cylinder(0.55, 0.12, T(x, y, 0.5), segments=20), "WoodDark")
    p.add(cylinder(0.08, 2.3, T(x, y, 1.6), segments=10), "Wood")
    p.add(cylinder(0.07, 1.4, T(x, y, 2.75) @ R("Y", 90), segments=10), "Wood")
    p.add(cylinder(0.18, 0.18, T(x + 0.6, y, 2.6), radius2=0.24, segments=12), "Bowl")
    p.add(box((0.9, 0.08, 0.5), T(x - 0.2, y + 0.12, 1.4), bevel=0.06), "SignBoard")
    heart(p, 0.26, T(x - 0.2, y + 0.18, 1.3), depth=0.04)
    return p.finish()


# The pets on display (store frame; they face the front, Blender +Y) -------------------------------


def kitten():
    p = Piece("Kitten")
    # An orange kitten curled in the lilac bed, two spotted mushroom caps for ears.
    p.add(sphere(0.42, T(0, -0.05, 0.3), scale=(1.2, 1.0, 0.65)), "CatFur")
    p.add(sphere(0.34, T(0, 0.28, 0.55)), "CatFur")
    p.add(sphere(0.18, T(0, 0.52, 0.48), scale=(1.2, 0.6, 0.8)), "Cream")
    for side in (-1, 1):
        cap = T(side * 0.2, 0.25, 0.85) @ R("Y", side * 25)
        p.add(dome(0.16, 0.14, cap), "Cap")
        p.add(sphere(0.035, cap @ T(0, 0.08, 0.1), scale=(1, 1, 0.5), segments=8, rings=4), "Spots")
    eyes(p, T(0, 0.58, 0.6), 0.12, 0.065)
    blush(p, T(0, 0.58, 0.48), 0.2, 0.05)
    p.add(sphere(0.03, T(0, 0.66, 0.52), segments=8, rings=4), "Nose")
    p.add(sphere(0.12, T(0.45, -0.3, 0.2), scale=(2.4, 0.8, 0.8)), "CatFur")
    return p.finish(location=(3.0, 0.5, 0.62))


def puppy():
    p = Piece("Puppy")
    # A fluffy cream puppy sitting in the pink bed, floppy brown ears, a little mushroom on its back.
    p.add(sphere(0.38, T(0, -0.1, 0.38), scale=(1.0, 1.1, 0.9)), "PupFur")
    p.add(sphere(0.36, T(0, 0.12, 0.85)), "PupFur")
    p.add(sphere(0.17, T(0, 0.42, 0.75), scale=(1.1, 0.7, 0.8)), "Cream")
    p.add(sphere(0.06, T(0, 0.55, 0.8), segments=8, rings=4), "Nose")
    for side in (-1, 1):
        p.add(sphere(0.14, T(side * 0.34, 0.08, 0.8) @ R("Y", side * 20), scale=(0.6, 0.7, 1.4)), "PupEar")
        p.add(sphere(0.11, T(side * 0.18, 0.22, 0.12), scale=(1, 1.3, 0.7)), "PupFur")
    eyes(p, T(0, 0.43, 0.94), 0.13, 0.07)
    blush(p, T(0, 0.43, 0.8), 0.22, 0.05)
    p.add(cylinder(0.04, 0.12, T(0.05, -0.3, 0.75), segments=8), "Stem")
    p.add(dome(0.13, 0.1, T(0.05, -0.3, 0.8)), "Cap")
    return p.finish(location=(5.4, -1.6, 0.62))


def bunny():
    p = Piece("Bunny")
    # A white bunny in the tall cage, long pink-lined ears, a tiny mushroom cap hat.
    p.add(sphere(0.3, T(0, -0.05, 0.27), scale=(1.0, 1.1, 0.9)), "BunnyFur")
    p.add(sphere(0.25, T(0, 0.12, 0.6)), "BunnyFur")
    for side in (-1, 1):
        p.add(sphere(0.07, T(side * 0.1, 0.1, 0.95) @ R("Y", side * 12), scale=(1, 0.6, 3.2)), "BunnyFur")
        p.add(sphere(0.04, T(side * 0.1, 0.14, 0.95) @ R("Y", side * 12), scale=(1, 0.4, 2.6)), "BunnyEar")
    p.add(dome(0.13, 0.09, T(0.1, 0.05, 0.82) @ R("Y", 15)), "Cap")
    eyes(p, T(0, 0.33, 0.65), 0.1, 0.055)
    blush(p, T(0, 0.33, 0.55), 0.16, 0.04)
    p.add(sphere(0.03, T(0, 0.37, 0.56), segments=8, rings=4), "BunnyEar")
    p.add(sphere(0.1, T(0, -0.36, 0.22), segments=10, rings=5), "BunnyFur")
    return p.finish(location=(-5.4, -1.8, 1.94))


def hamster():
    p = Piece("Hamster")
    # A round little hamster in the small cage.
    p.add(sphere(0.26, T(0, 0, 0.22), scale=(1.1, 1.0, 0.85)), "HamFur")
    p.add(sphere(0.17, T(0, 0.16, 0.16), scale=(1.2, 0.8, 0.7)), "HamLight")
    for side in (-1, 1):
        p.add(sphere(0.07, T(side * 0.15, 0.06, 0.42), scale=(1, 0.5, 1)), "HamFur")
    eyes(p, T(0, 0.24, 0.3), 0.09, 0.045)
    blush(p, T(0, 0.25, 0.2), 0.15, 0.04)
    p.add(sphere(0.025, T(0, 0.28, 0.24), segments=8, rings=4), "Nose")
    return p.finish(location=(-3.2, -3.0, 1.54))


def mushroom_crow():
    p = Piece("MushroomCrow")
    # The Mushroom Crow on its perch: a round dark body with spots, a spotted cap, big eyes, a beak.
    p.add(sphere(0.32, T(0, 0, 0.32), scale=(1.0, 1.05, 0.95)), "CrowBody")
    p.add(sphere(0.22, T(0, 0.12, 0.24), scale=(1, 0.8, 0.9)), "CrowLight")
    p.add(sphere(0.25, T(0, 0.08, 0.68)), "CrowBody")
    for side in (-1, 1):
        p.add(sphere(0.16, T(side * 0.3, -0.02, 0.32) @ R("Y", side * 20), scale=(0.35, 0.9, 0.75)), "CrowBody")
        p.add(cylinder(0.025, 0.12, T(side * 0.08, 0.02, 0.03), segments=6), "Beak")
    p.add(sphere(0.12, T(0, -0.32, 0.28) @ R("X", -30), scale=(0.9, 1.4, 0.3)), "CrowBody")
    cap = T(0, 0.04, 0.86) @ R("X", -10)
    p.add(dome(0.27, 0.2, cap), "Cap")
    spots_on_dome(p, 0.27, 0.2, cap, count=5, size=0.18)
    eyes(p, T(0, 0.3, 0.72), 0.1, 0.06)
    blush(p, T(0, 0.3, 0.6), 0.16, 0.04)
    p.add(cylinder(0.07, 0.16, T(0, 0.38, 0.62) @ R("X", -90), radius2=0.0, segments=10), "Beak")
    for i, (x, y) in enumerate(((-0.18, -0.1), (0.2, 0.0), (0.0, -0.22))):
        p.add(sphere(0.04, T(x, y, 0.55), scale=(1, 1, 0.4), segments=8, rings=4), "Cream")
    return p.finish(location=(-3.4, 1.4, 2.82))


# The Map's props -----------------------------------------------------------------------------------


def city_lamp():
    p = Piece("CityLamp")
    # A slim post on a round foot, a glowing bulb under a little spotted mushroom cap.
    p.add(cylinder(0.3, 0.2, T(0, 0, 0.1), segments=20), "Plinth")
    p.add(cylinder(0.09, 2.1, T(0, 0, 1.2), radius2=0.07, segments=12), "Trim")
    p.add(sphere(0.18, T(0, 0, 2.3), segments=14, rings=7), "LampGlow")
    return p.finish()


LAMP_CAPS = {
    # name: (profile from the top round to the bottom, wobble, waves, spotted); top at 2.6 m at most.
    "LampCapDome": ([(0, 0.22), (0.15, 0.19), (0.26, 0.11), (0.3, 0.02), (0.26, 0.0), (0, 0.0)], 0, 0, True),
    "LampCapBell": ([(0, 0.22), (0.1, 0.21), (0.18, 0.12), (0.23, -0.04), (0.27, -0.14), (0.2, -0.08), (0, 0.0)], 0, 0, False),
    "LampCapFlat": ([(0, 0.16), (0.06, 0.15), (0.1, 0.1), (0.3, 0.07), (0.34, 0.02), (0.3, 0.0), (0, 0.0)], 0, 0, False),
    "LampCapFunnel": ([(0, 0.06), (0.2, 0.12), (0.32, 0.2), (0.34, 0.16), (0.22, 0.04), (0.12, 0.0), (0, 0.0)], 0.03, 6, False),
    "LampCapWavy": ([(0, 0.2), (0.12, 0.18), (0.24, 0.1), (0.31, 0.03), (0.27, 0.0), (0, 0.0)], 0.04, 6, True),
    "LampCapCone": ([(0, 0.22), (0.08, 0.16), (0.18, 0.07), (0.28, 0.0), (0.2, -0.02), (0, 0.0)], 0, 0, False),
}


def lamp_caps():
    """The city lamps' mushroom caps, one shape per object; the game picks one per lamp (and its color)."""
    for name, (profile, wobble, waves, spotted) in LAMP_CAPS.items():
        p = Piece(name)
        p.add(revolve(profile, T(0, 0, 2.38), segments=24, wobble=wobble, waves=waves), "LampCap")
        if spotted:
            spots_on_dome(p, 0.26, 0.19, T(0, 0, 2.38), count=5, size=0.08)
        p.finish()


def bench():
    p = Piece("Bench")
    # A wooden bench: seat along X, backrest behind it (-Y), so it faces +Y; little heart on the back.
    for x in (-0.65, 0.65):
        p.add(box((0.1, 0.5, 0.42), T(x, 0, 0.21), bevel=0.03), "WoodDark")
        p.add(box((0.1, 0.1, 0.9), T(x, -0.22, 0.45), bevel=0.03), "WoodDark")
    p.add(box((1.6, 0.5, 0.1), T(0, 0, 0.47), bevel=0.04), "Wood")
    for z in (0.66, 0.82):
        p.add(box((1.6, 0.07, 0.12), T(0, -0.24, z), bevel=0.03), "Wood")
    heart(p, 0.16, T(0, -0.19, 0.74), depth=0.04)
    return p.finish()


def planter():
    p = Piece("Planter")
    # A round terracotta pot with leaves, yellow flowers and a tiny mushroom.
    p.add(cylinder(0.6, 0.45, T(0, 0, 0.225), radius2=0.5, segments=24), "Pot")
    p.add(cylinder(0.62, 0.08, T(0, 0, 0.45), segments=24), "Trim")
    for i in range(7):
        a = 2 * math.pi * i / 7
        p.add(sphere(0.16, T(math.cos(a) * 0.45, math.sin(a) * 0.45, 0.5), scale=(1, 1, 0.75), segments=10, rings=5),
              GREENS[i % len(GREENS)])
        p.add(sphere(0.07, T(math.cos(a + 0.45) * 0.47, math.sin(a + 0.45) * 0.47, 0.6), segments=8, rings=4),
              FLOWERS[i % len(FLOWERS)])
    # Soil in the middle, where the game plants its little mushrooms.
    p.add(cylinder(0.36, 0.06, T(0, 0, 0.5), segments=20), "LeafDeep")
    p.add(sphere(0.34, T(0, 0, 0.5), scale=(1, 1, 0.09), segments=16, rings=4), "LeafDeep")
    return p.finish()


def statue():
    p = Piece("Statue")
    # A big verdigris Mushroom Cat sitting on a stone pedestal, looking forward (+Y). No eyes: a statue.
    p.add(box((2.2, 2.2, 0.3), T(0, 0, 0.15), bevel=0.08), "Plinth")
    p.add(box((1.8, 1.8, 0.9), T(0, 0, 0.75), bevel=0.08), "Stone")
    p.add(box((2.0, 2.0, 0.2), T(0, 0, 1.3), bevel=0.06), "Plinth")
    heart(p, 0.5, T(0, 0.91, 0.75), depth=0.06)
    p.add(sphere(0.6, T(0, 0, 1.95), scale=(1, 0.9, 1.05), segments=20, rings=10), "Bronze")
    for x in (-0.25, 0.25):
        p.add(sphere(0.17, T(x, 0.45, 1.5), scale=(1, 1.3, 0.8), segments=10, rings=5), "Bronze")
    p.add(cylinder(0.12, 0.9, T(0.55, -0.45, 1.75) @ R("Y", 30), segments=10), "Bronze")
    p.add(sphere(0.55, T(0, 0.1, 2.75), segments=20, rings=10), "Bronze")
    p.add(sphere(0.22, T(0, 0.58, 2.62), scale=(1.3, 0.8, 0.8), segments=12, rings=6), "Bronze")
    p.add(sphere(0.07, T(0, 0.76, 2.7), segments=8, rings=4), "Bronze")
    for x in (-0.33, 0.33):
        p.add(cylinder(0.17, 0.3, T(x, 0.1, 3.25) @ R("Y", 20 if x > 0 else -20), radius2=0.0, segments=10), "Bronze")
    # Its little mushroom cap hat.
    p.add(dome(0.32, 0.18, T(0.12, 0.05, 3.18) @ R("Y", 12)), "Bronze")
    return p.finish()


# The little fungi (decoration under 0.4 m, planted by the game) ---------------------------------
# Each kind is built 1 m tall (its top at z = 1), standing at the origin; the game scales, stretches
# and tilts each one, and tints its "FungusCap" / "FungusGlow" per mushroom (PetCityLook.Fungi).
# A bracket fungus is built against a stem at -Y (Godot +Z), sticking out toward +Y.


def stem(p, r1, r2, height, z0=0.0, segments=12):
    p.add(cylinder(r1, height, T(0, 0, z0 + height / 2), radius2=r2, segments=segments), "Stem")


def fly_agaric():
    p = Piece("FungusFlyAgaric")
    stem(p, 0.12, 0.08, 0.66)
    p.add(cylinder(0.13, 0.04, T(0, 0, 0.5), radius2=0.1, segments=12), "Stem")
    p.add(dome(0.34, 0.36, T(0, 0, 0.64), segments=20, rings=6), "FungusCap")
    spots_on_dome(p, 0.34, 0.36, T(0, 0, 0.64), count=7, size=0.15)
    return p.finish()


def bolete():
    p = Piece("FungusBolete")
    p.add(revolve([(0, 0.6), (0.14, 0.58), (0.2, 0.35), (0.22, 0.12), (0.16, 0.0), (0, 0.0)], T(), segments=14), "Stem")
    p.add(revolve([(0, 1.0), (0.22, 0.97), (0.38, 0.86), (0.45, 0.66), (0.4, 0.54), (0.2, 0.52), (0, 0.55)], T(),
                  segments=20), "FungusCap")
    return p.finish()


def chanterelle():
    p = Piece("FungusChanterelle")
    # A golden funnel with a wavy rim; its stem flows into the cap.
    p.add(revolve([(0, 0.82), (0.18, 0.86), (0.34, 0.94), (0.4, 1.0), (0.38, 0.9), (0.24, 0.7), (0.1, 0.4),
                   (0.08, 0.05), (0.1, 0.0), (0, 0.0)], T(), segments=20, wobble=0.06, waves=6), "FungusCap")
    return p.finish()


def parasol():
    p = Piece("FungusParasol")
    stem(p, 0.05, 0.03, 0.92, segments=10)
    p.add(cylinder(0.06, 0.03, T(0, 0, 0.66), radius2=0.045, segments=10), "Stem")
    p.add(revolve([(0, 1.0), (0.06, 0.98), (0.1, 0.93), (0.42, 0.88), (0.48, 0.84), (0.44, 0.82), (0, 0.86)], T(),
                  segments=24), "FungusCap")
    spots_on_dome(p, 0.38, 0.06, T(0, 0, 0.88), count=8, size=0.07)
    return p.finish()


def ink_cap():
    p = Piece("FungusInkCap")
    stem(p, 0.07, 0.05, 0.5, segments=10)
    p.add(revolve([(0, 1.0), (0.08, 0.98), (0.14, 0.88), (0.17, 0.66), (0.18, 0.42), (0.16, 0.36), (0, 0.4)], T(),
                  segments=16), "FungusCap")
    # A shaggy, inky rim.
    p.add(cylinder(0.185, 0.05, T(0, 0, 0.39), radius2=0.17, segments=16), "Ink")
    for i in range(10):
        a = 2 * math.pi * i / 10
        z = 0.55 + (i % 3) * 0.12
        p.add(sphere(0.035, T(math.cos(a) * 0.17, math.sin(a) * 0.17, z), scale=(1, 1, 1.6), segments=6, rings=3), "Spots")
    return p.finish()


def morel():
    p = Piece("FungusMorel")
    stem(p, 0.13, 0.1, 0.4)
    p.add(revolve([(0, 1.0), (0.08, 0.92), (0.16, 0.72), (0.2, 0.52), (0.18, 0.36), (0, 0.36)], T(), segments=16),
          "FungusCap")
    # The honeycomb: rows of dark pits.
    for row in range(4):
        z = 0.45 + row * 0.13
        r = 0.2 - row * 0.035
        for i in range(7):
            a = 2 * math.pi * (i + 0.5 * row) / 7
            p.add(sphere(0.045, T(math.cos(a) * r, math.sin(a) * r, z), scale=(0.6, 0.6, 1.1), segments=6, rings=3),
                  "MorelPit")
    return p.finish()


def enoki():
    p = Piece("FungusEnoki")
    # A bunch of tall thin stems with tiny caps, of different heights, from one base.
    p.add(sphere(0.12, T(0, 0, 0.03), scale=(1, 1, 0.5), segments=10, rings=4), "Stem")
    for i, (x, y, h, lean) in enumerate(((0, 0, 1.0, 0), (0.07, 0.02, 0.86, 9), (-0.06, 0.05, 0.92, -8),
                                         (0.02, -0.07, 0.74, 12), (-0.05, -0.04, 0.8, -12), (0.08, -0.05, 0.64, 16),
                                         (-0.08, 0.0, 0.7, -16))):
        m = T(x, y, 0) @ R("Y" if i % 2 else "X", lean)
        p.add(cylinder(0.018, h - 0.04, m @ T(0, 0, (h - 0.04) / 2), segments=6), "Stem")
        p.add(dome(0.05, 0.05, m @ T(0, 0, h - 0.05), segments=10, rings=3), "FungusCap")
    return p.finish()


def amethyst():
    p = Piece("FungusAmethyst")
    stem(p, 0.09, 0.07, 0.62)
    p.add(revolve([(0, 1.0), (0.16, 0.97), (0.3, 0.86), (0.4, 0.7), (0.42, 0.62), (0.36, 0.6), (0, 0.62)], T(),
                  segments=20, wobble=0.04, waves=5), "FungusCap")
    return p.finish()


def puffball():
    p = Piece("FungusPuffball")
    p.add(cylinder(0.16, 0.14, T(0, 0, 0.07), radius2=0.24, segments=12), "Stem")
    p.add(revolve([(0, 1.0), (0.24, 0.96), (0.4, 0.82), (0.47, 0.56), (0.4, 0.28), (0.24, 0.12), (0, 0.1)], T(),
                  segments=20), "FungusCap")
    for i in range(9):
        a = 2 * math.pi * i / 9
        r, z = (0.22, 0.92) if i % 2 else (0.38, 0.74)
        p.add(sphere(0.03, T(math.cos(a) * r, math.sin(a) * r, z), segments=6, rings=3), "Spots")
    return p.finish()


def bracket():
    p = Piece("FungusBracket")
    # Three stacked half-shelves against a stem (at -Y), outward toward +Y, banded rims.
    for i, (z, w, d) in enumerate(((0.8, 0.5, 0.42), (0.45, 0.42, 0.34), (0.12, 0.3, 0.26))):
        shelf = T(0, 0, z) @ S(w, d, 0.2)
        part = sphere(1.0, Matrix(), segments=16, rings=6)
        bmesh.ops.delete(part, geom=[v for v in part.verts if v.co.y < -1e-4], context="VERTS")
        bmesh.ops.contextual_create(part, geom=[e for e in part.edges if e.is_boundary])
        bmesh.ops.transform(part, matrix=shelf, verts=part.verts)
        p.add(part, "FungusCap")
        p.add(cylinder(1.0, 0.05, T(0, 0, z - 0.02) @ S(w * 1.02, d * 1.02, 1), segments=16), "Stem")
    return p.finish()


def cup_fungus():
    p = Piece("FungusCup")
    # A scarlet cup: a deep bowl open on top, cream inside lip.
    p.add(revolve([(0, 0.55), (0.3, 0.62), (0.42, 0.84), (0.46, 1.0), (0.5, 0.98), (0.48, 0.7), (0.3, 0.28),
                   (0.12, 0.06), (0, 0.0)], T(), segments=20, wobble=0.04, waves=5), "FungusCap")
    return p.finish()


def coral():
    p = Piece("FungusCoral")
    # Branching arms, each forking twice, with round tips.
    p.add(cylinder(0.1, 0.3, T(0, 0, 0.15), radius2=0.08, segments=10), "FungusCap")

    def branch(m, length, radius, depth):
        p.add(cylinder(radius, length, m @ T(0, 0, length / 2), radius2=radius * 0.75, segments=8), "FungusCap")
        tip = m @ T(0, 0, length)
        if depth == 0:
            p.add(sphere(radius * 1.1, tip, segments=8, rings=4), "FungusCap")
            return
        for k, spread in enumerate((-28, 28)):
            branch(tip @ R("Z", 90 * k + depth * 40) @ R("X", spread), length * 0.72, radius * 0.72, depth - 1)

    for k in range(3):
        branch(T(0, 0, 0.28) @ R("Z", 120 * k) @ R("X", 22), 0.34, 0.07, 2)
    return p.finish()


def jelly():
    p = Piece("FungusJelly")
    # Glossy ruffled lobes, like a little ear jelly.
    for i, (x, y, z, s) in enumerate(((0, 0, 0.55, 0.32), (0.18, 0.08, 0.35, 0.26), (-0.16, 0.1, 0.32, 0.24),
                                      (0.04, -0.16, 0.25, 0.22), (0.0, 0.05, 0.85, 0.18))):
        p.add(sphere(s, T(x, y, z) @ R("Z", i * 50) @ R("X", 25), scale=(1, 0.45, 0.8), segments=12, rings=6), "FungusCap")
    return p.finish()


def glow_cap():
    p = Piece("FungusGlowCap")
    # Bioluminescent: slim stems, three conical caps that glow at night.
    for x, y, h, r, lean in ((0, 0, 1.0, 0.2, 0), (0.18, 0.06, 0.66, 0.14, 14), (-0.14, 0.1, 0.48, 0.12, -16)):
        m = T(x, y, 0) @ R("Y", lean)
        p.add(cylinder(0.03, h - 0.1, m @ T(0, 0, (h - 0.1) / 2), segments=8), "Stem")
        p.add(revolve([(0, h), (r * 0.4, h - r * 0.4), (r, h - r * 0.9), (r * 0.8, h - r), (0, h - r * 0.8)], m,
                      segments=14), "FungusGlow")
    return p.finish()


FUNGI = (fly_agaric, bolete, chanterelle, parasol, ink_cap, morel, enoki, amethyst, puffball, bracket, cup_fungus,
         coral, jelly, glow_cap)


bpy.ops.wm.read_factory_settings(use_empty=True)
for name, color in COLORS.items():
    material(name, color)

for n in range(3, 11):
    tower(n)
roof_cap()
roof_caps()
roof_terrace()
pet_store()
counter()
store_inside()
kitten()
puppy()
bunny()
hamster()
mushroom_crow()
city_lamp()
lamp_caps()
bench()
planter()
statue()
for build in FUNGI:
    build()

path = os.path.join(HERE, "petcity.glb")
bpy.ops.export_scene.gltf(filepath=path, export_format="GLB", export_yup=True)
print("petcity: exported", path)
