# Builds the home's furniture and exports it next to this script as furniture.glb.
# Run from the repository root:
#   blender -b --factory-startup --python modules/Home/Models/furniture.py
#
# Chunky, cute, mushroom-themed furniture: one object per kind, named like Fungtopia.Home's
# FurnitureKind (BasicBed, MushroomBed, Rug, Lamp, Table, Chair, PottedMushroom, Painting, PetBed,
# Bookshelf, ToadstoolStool) plus the CatalogBook on its stand and the Wardrobe that comes with
# the house (no face: only creatures have eyes). Each stands on the floor at the
# origin, centered on its footprint (width along X, depth along Blender Y = Godot Z, a little
# smaller than its 0.5 m grid cells), its back toward Blender +Y (Godot -Z, a wall) and its front
# toward Blender -Y (Godot +Z, into the room). Materials carry plain colors only; the game draws
# them toon-shaded with ink outlines.
import math
import os

import bmesh
import bpy
from mathutils import Matrix

HERE = os.path.dirname(os.path.abspath(__file__))

COLORS = {
    "Wood": (0.72, 0.46, 0.28),
    "WoodDark": (0.5, 0.3, 0.18),
    "Cap": (0.9, 0.3, 0.26),
    "CapPink": (0.98, 0.6, 0.68),
    "CapLilac": (0.72, 0.58, 0.9),
    "Spots": (1.0, 0.96, 0.86),
    "Stem": (1.0, 0.92, 0.74),
    "Sheet": (0.98, 0.95, 0.9),
    "Blanket": (0.98, 0.58, 0.62),
    "Pillow": (0.86, 0.94, 1.0),
    "Rug": (0.62, 0.84, 0.66),
    "RugDots": (1.0, 0.95, 0.72),
    "Glow": (1.0, 0.9, 0.55),
    "Pot": (0.86, 0.5, 0.34),
    "Soil": (0.36, 0.24, 0.16),
    "Leaf": (0.46, 0.76, 0.38),
    "Canvas": (0.96, 0.98, 0.9),
    "Sky": (0.62, 0.84, 1.0),
    "Cushion": (0.66, 0.78, 0.98),
    "BookRed": (0.9, 0.36, 0.36),
    "BookBlue": (0.4, 0.58, 0.92),
    "BookGreen": (0.44, 0.74, 0.46),
    "BookYellow": (0.98, 0.8, 0.36),
    "Page": (1.0, 0.98, 0.92),
    "Eye": (0.16, 0.08, 0.06),
    "EyeShine": (1.0, 1.0, 1.0),
    "Blush": (1.0, 0.55, 0.6),
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


def face(piece, matrix, scale=1.0, blush=True):
    """Big round cute eyes (with sparkles) and rosy cheeks, looking along Blender -Y from `matrix`."""
    for side in (-1, 1):
        piece.add(sphere(0.035 * scale, matrix @ T(side * 0.05 * scale, 0, 0), scale=(1, 0.5, 1.2),
                         segments=14, rings=7), "Eye")
        piece.add(sphere(0.012 * scale, matrix @ T(side * 0.05 * scale + 0.012 * scale, -0.018 * scale, 0.018 * scale),
                         segments=8, rings=4), "EyeShine")
        if blush:
            piece.add(sphere(0.022 * scale, matrix @ T(side * 0.095 * scale, 0.004 * scale, -0.04 * scale),
                             scale=(1.2, 0.35, 0.7), segments=10, rings=5), "Blush")


def bed(name, width, mushroom):
    p = Piece(name)
    depth = 1.9
    # Wooden frame on stubby round feet, a mattress, a spotted blanket and a fat pillow.
    for x in (-1, 1):
        for y in (-1, 1):
            p.add(cylinder(0.06, 0.12, T(x * (width / 2 - 0.1), y * (depth / 2 - 0.1), 0.06)), "WoodDark")
    p.add(box((width, depth, 0.16), T(0, 0, 0.2)), "Wood")
    p.add(box((width - 0.08, depth - 0.1, 0.18), T(0, 0, 0.36), bevel=0.07), "Sheet")
    p.add(box((width - 0.02, depth * 0.62, 0.08), T(0, -depth * 0.18, 0.46), bevel=0.04), "Blanket")
    for i, (x, y) in enumerate(((-0.25, -0.5), (0.2, -0.2), (-0.1, 0.1), (0.3, -0.7), (-0.32, -0.15))):
        if abs(x) < width / 2 - 0.08:
            p.add(sphere(0.06, T(x, y, 0.5), scale=(1, 1, 0.3), segments=12, rings=6), "Spots")
    p.add(box((width * 0.7, 0.34, 0.14), T(0, depth / 2 - 0.3, 0.52), bevel=0.06), "Pillow")
    # Headboard against the wall.
    if mushroom:
        # A big upright mushroom cap with a face, standing on a short stem.
        back = depth / 2 - 0.1
        p.add(box((width * 0.3, 0.14, 0.5), T(0, back, 0.5)), "Stem")
        p.add(dome(width * 0.6, 0.62, T(0, back, 0.72) @ S(1, 0.22, 1)), "Cap")
        front = back - 0.125
        face(p, T(0, front, 0.98), scale=1.8)
        for x, z, r in ((-0.6, 0.85, 0.08), (0.62, 0.86, 0.07), (-0.32, 1.18, 0.06), (0.36, 1.15, 0.065), (0, 1.3, 0.05)):
            p.add(sphere(r, T(x * width * 0.6, front + 0.01, z), scale=(1, 0.35, 1), segments=12, rings=6), "Spots")
    else:
        p.add(box((width, 0.1, 0.62), T(0, depth / 2 - 0.05, 0.42)), "Wood")
        p.add(sphere(0.06, T(-width / 2 + 0.05, depth / 2 - 0.05, 0.78)), "WoodDark")
        p.add(sphere(0.06, T(width / 2 - 0.05, depth / 2 - 0.05, 0.78)), "WoodDark")
    return p.finish()


def rug():
    p = Piece("Rug")
    # A soft oval rug with a cream border and polka dots.
    p.add(cylinder(1.0, 0.03, T(0, 0, 0.015) @ S(0.95, 0.7, 1), segments=40), "RugDots")
    p.add(cylinder(0.9, 0.04, T(0, 0, 0.02) @ S(0.95, 0.7, 1), segments=40), "Rug")
    for i in range(7):
        a = 2 * math.pi * i / 7
        p.add(cylinder(0.08, 0.01, T(math.cos(a) * 0.55, math.sin(a) * 0.35, 0.042), segments=14), "RugDots")
    p.add(cylinder(0.12, 0.01, T(0, 0, 0.042), segments=16), "RugDots")
    return p.finish()


def lamp():
    p = Piece("Lamp")
    # A little mushroom floor lamp: a stem on a round foot, a glowing cap shade with eyes.
    p.add(cylinder(0.17, 0.05, T(0, 0, 0.025)), "WoodDark")
    p.add(cylinder(0.04, 0.85, T(0, 0, 0.47)), "Stem")
    p.add(sphere(0.1, T(0, 0, 0.9), scale=(1, 1, 0.8)), "Glow")
    p.add(dome(0.24, 0.2, T(0, 0, 0.92)), "CapPink")
    spots_on_dome(p, 0.24, 0.2, T(0, 0, 0.92), count=5, size=0.2)
    face(p, T(0, -0.2, 1.0), scale=0.9, blush=False)
    return p.finish()


def table():
    p = Piece("Table")
    # A round mushroom table: a fat stem and a wide flat cap with spots.
    p.add(cylinder(0.24, 0.06, T(0, 0, 0.03)), "Stem")
    p.add(cylinder(0.12, 0.6, T(0, 0, 0.33), radius2=0.1), "Stem")
    p.add(cylinder(0.46, 0.08, T(0, 0, 0.66)), "Wood")
    p.add(dome(0.46, 0.06, T(0, 0, 0.7)), "Cap")
    for i in range(5):
        a = 2 * math.pi * i / 5 + 0.3
        p.add(sphere(0.06, T(math.cos(a) * 0.3, math.sin(a) * 0.3, 0.74), scale=(1, 1, 0.3), segments=12, rings=6), "Spots")
    return p.finish()


def chair():
    p = Piece("Chair")
    # A small wooden chair with a cushion and a mushroom-cap back.
    for x in (-1, 1):
        for y in (-1, 1):
            p.add(cylinder(0.025, 0.36, T(x * 0.15, y * 0.15, 0.18)), "WoodDark")
    p.add(box((0.4, 0.4, 0.06), T(0, 0, 0.38)), "Wood")
    p.add(box((0.34, 0.34, 0.06), T(0, 0, 0.43), bevel=0.03), "Cushion")
    p.add(box((0.06, 0.04, 0.3), T(0, 0.17, 0.55)), "Wood")
    p.add(dome(0.2, 0.15, T(0, 0.17, 0.68) @ R("X", 80)), "Cap")
    p.add(sphere(0.035, T(-0.07, 0.13, 0.74), scale=(1, 0.5, 1), segments=10, rings=5), "Spots")
    p.add(sphere(0.03, T(0.08, 0.13, 0.7), scale=(1, 0.5, 1), segments=10, rings=5), "Spots")
    return p.finish()


def potted_mushroom():
    p = Piece("PottedMushroom")
    # A terracotta pot with a cheeky little mushroom (with a face) and two leaves.
    p.add(cylinder(0.13, 0.22, T(0, 0, 0.11), radius2=0.17), "Pot")
    p.add(cylinder(0.18, 0.05, T(0, 0, 0.23)), "Pot")
    p.add(cylinder(0.155, 0.02, T(0, 0, 0.25)), "Soil")
    p.add(cylinder(0.05, 0.2, T(0, 0, 0.35), radius2=0.045), "Stem")
    p.add(dome(0.16, 0.13, T(0, 0, 0.44)), "Cap")
    spots_on_dome(p, 0.16, 0.13, T(0, 0, 0.44), count=5, size=0.22)
    face(p, T(0, -0.05, 0.37), scale=0.55, blush=True)
    p.add(sphere(0.06, T(0.09, 0, 0.29) @ R("Y", 40), scale=(1.4, 0.6, 0.3)), "Leaf")
    p.add(sphere(0.05, T(-0.09, 0.02, 0.28) @ R("Y", -40), scale=(1.4, 0.6, 0.3)), "Leaf")
    return p.finish()


def painting():
    p = Piece("Painting")
    # A little easel holding a painting of a mushroom under a blue sky.
    p.add(box((0.04, 0.04, 1.0), T(-0.15, -0.02, 0.48) @ R("Y", -8)), "Wood")
    p.add(box((0.04, 0.04, 1.0), T(0.15, -0.02, 0.48) @ R("Y", 8)), "Wood")
    p.add(box((0.04, 0.04, 0.9), T(0, 0.17, 0.44) @ R("X", -18)), "Wood")
    p.add(box((0.4, 0.06, 0.04), T(0, -0.06, 0.42)), "WoodDark")
    p.add(box((0.44, 0.05, 0.44), T(0, -0.05, 0.66)), "WoodDark")
    p.add(box((0.38, 0.02, 0.38), T(0, -0.08, 0.66), bevel=0.0), "Sky")
    p.add(box((0.38, 0.021, 0.1), T(0, -0.081, 0.52), bevel=0.0), "Leaf")
    p.add(box((0.04, 0.02, 0.1), T(0.02, -0.092, 0.6), bevel=0.0), "Stem")
    p.add(dome(0.1, 0.08, T(0.02, -0.095, 0.64) @ S(1, 0.15, 1)), "Cap")
    p.add(sphere(0.03, T(0.13, -0.095, 0.79), scale=(1, 0.2, 1), segments=10, rings=5), "Glow")
    return p.finish()


def pet_bed():
    p = Piece("PetBed")
    # A round puffy pet bed: a fat ring cushion around a soft middle, spotted like a cap.
    p.add(cylinder(0.42, 0.08, T(0, 0, 0.04)), "Cushion")
    for i in range(14):
        a = 2 * math.pi * i / 14
        p.add(sphere(0.12, T(math.cos(a) * 0.36, math.sin(a) * 0.36, 0.14), scale=(1, 1, 0.9), segments=14, rings=7),
              "CapLilac")
    p.add(cylinder(0.3, 0.05, T(0, 0, 0.1)), "Sheet")
    for i in range(5):
        a = 2 * math.pi * i / 5
        p.add(sphere(0.04, T(math.cos(a) * 0.38, math.sin(a) * 0.38, 0.25), scale=(1, 1, 0.4), segments=10, rings=5),
              "Spots")
    return p.finish()


def bookshelf():
    p = Piece("Bookshelf")
    # A wide wooden bookshelf full of colorful books, a little mushroom on top.
    w, d, h = 1.4, 0.42, 1.3
    p.add(box((0.06, d, h), T(-w / 2 + 0.03, 0, h / 2), bevel=0.02), "Wood")
    p.add(box((0.06, d, h), T(w / 2 - 0.03, 0, h / 2), bevel=0.02), "Wood")
    p.add(box((w, 0.04, h), T(0, d / 2 - 0.02, h / 2), bevel=0.0), "WoodDark")
    for z in (0.04, 0.45, 0.86, h - 0.03):
        p.add(box((w, d, 0.06), T(0, 0, z), bevel=0.02), "Wood")
    colors = ["BookRed", "BookBlue", "BookGreen", "BookYellow"]
    for shelf, z in enumerate((0.07, 0.48)):
        x = -w / 2 + 0.1
        i = shelf
        while x < w / 2 - 0.15:
            bw = 0.07 + 0.02 * ((i * 7) % 3)
            bh = 0.26 + 0.04 * ((i * 5) % 3)
            p.add(box((bw, 0.28, bh), T(x + bw / 2, -0.02, z + bh / 2), bevel=0.012), colors[i % 4])
            x += bw + 0.012
            i += 1
    p.add(sphere(0.14, T(-0.3, 0, 0.98), scale=(1, 1, 0.7)), "Leaf")
    p.add(cylinder(0.035, 0.12, T(0.35, 0, 0.95)), "Stem")
    p.add(dome(0.1, 0.08, T(0.35, 0, 1.0)), "Cap")
    p.add(cylinder(0.035, 0.12, T(0.35, 0, h + 0.06)), "Stem")
    p.add(dome(0.12, 0.1, T(0.35, 0, h + 0.11)), "CapPink")
    spots_on_dome(p, 0.12, 0.1, T(0.35, 0, h + 0.11), count=4, size=0.22)
    face(p, T(0.35, -0.04, h + 0.06), scale=0.4, blush=False)
    return p.finish()


def toadstool_stool():
    p = Piece("ToadstoolStool")
    # A toadstool to sit on: a fat stem, a red spotted cap seat, and a sleepy-cute face.
    p.add(cylinder(0.12, 0.32, T(0, 0, 0.16), radius2=0.1), "Stem")
    p.add(dome(0.22, 0.14, T(0, 0, 0.3)), "Cap")
    spots_on_dome(p, 0.22, 0.14, T(0, 0, 0.3), count=6, size=0.18)
    face(p, T(0, -0.11, 0.17), scale=0.6)
    return p.finish()


def catalog_book():
    p = Piece("CatalogBook")
    # A mushroom-stem lectern with an open pink catalog book on it.
    p.add(cylinder(0.25, 0.06, T(0, 0, 0.03)), "WoodDark")
    p.add(cylinder(0.09, 0.8, T(0, 0, 0.45), radius2=0.07), "Stem")
    p.add(box((0.46, 0.36, 0.05), T(0, 0, 0.86) @ R("X", 20)), "Wood")
    tilt = T(0, -0.01, 0.9) @ R("X", 20)
    for side in (-1, 1):
        p.add(box((0.2, 0.3, 0.03), tilt @ T(side * 0.105, 0, 0) @ R("Y", side * -6), bevel=0.01), "Page")
    p.add(box((0.44, 0.32, 0.02), tilt @ T(0, 0, -0.02), bevel=0.01), "CapPink")
    p.add(sphere(0.03, tilt @ T(-0.1, -0.02, 0.025), scale=(1, 1, 0.3), segments=10, rings=5), "Cap")
    p.add(box((0.1, 0.012, 0.004), tilt @ T(0.1, 0.05, 0.02), bevel=0.0), "BookBlue")
    p.add(box((0.1, 0.012, 0.004), tilt @ T(0.1, 0.0, 0.02), bevel=0.0), "BookBlue")
    return p.finish()


def wardrobe():
    p = Piece("Wardrobe")
    # A big, chunky wooden wardrobe (1.5 x 1 m of floor, 2.2 m tall): stubby round feet, two
    # rounded doors with mushroom-shaped panels and round knobs, a drawer below, and a spotted
    # mushroom-cap crown on top. No face.
    w, d, h = 1.38, 0.78, 1.9
    for x in (-1, 1):
        for y in (-1, 1):
            p.add(sphere(0.08, T(x * (w / 2 - 0.12), y * (d / 2 - 0.12), 0.07), scale=(1, 1, 0.9)), "WoodDark")
    p.add(box((w, d, h - 0.1), T(0, 0, 0.1 + (h - 0.1) / 2), bevel=0.09), "Wood")
    p.add(box((w + 0.06, d + 0.06, 0.1), T(0, 0, h + 0.02), bevel=0.04), "WoodDark")
    front = -d / 2
    # The drawer with a little handle.
    p.add(box((w - 0.16, 0.05, 0.26), T(0, front - 0.005, 0.32), bevel=0.04), "WoodDark")
    p.add(sphere(0.04, T(0, front - 0.04, 0.32), scale=(1.6, 0.8, 0.8), segments=12, rings=6), "Stem")
    # Two doors, a seam between them; each door has a mushroom panel (stem and spotted cap).
    door_w, door_h, door_z = w / 2 - 0.1, 1.28, 1.15
    for side in (-1, 1):
        x = side * (door_w / 2 + 0.03)
        p.add(box((door_w, 0.04, door_h), T(x, front - 0.01, door_z), bevel=0.05), "Wood")
        p.add(box((0.12, 0.03, 0.26), T(x, front - 0.035, door_z - 0.17), bevel=0.02), "Stem")
        p.add(sphere(0.2, T(x, front - 0.03, door_z), scale=(1, 0.2, 0.55)), "Cap")
        for dx, dz, r in ((-0.09, 0.02, 0.032), (0.08, 0.04, 0.028), (0.0, 0.07, 0.024)):
            p.add(sphere(r, T(x + dx, front - 0.066, door_z + dz), scale=(1, 0.4, 1), segments=10, rings=5), "Spots")
        p.add(sphere(0.045, T(side * 0.07, front - 0.05, door_z - 0.35), segments=14, rings=7), "Stem")
    p.add(box((0.02, 0.05, door_h), T(0, front - 0.012, door_z), bevel=0.0), "WoodDark")
    # The mushroom-cap crown on top, with spots.
    p.add(cylinder(0.16, 0.12, T(0, 0, h + 0.13)), "Stem")
    p.add(dome(0.62, 0.3, T(0, 0, h + 0.17) @ S(1, 0.62, 1)), "Cap")
    spots_on_dome(p, 0.62, 0.3, T(0, 0, h + 0.17) @ S(1, 0.62, 1), count=7, size=0.13)
    return p.finish()


bpy.ops.wm.read_factory_settings(use_empty=True)
for name, color in COLORS.items():
    material(name, color)

bed("BasicBed", 0.92, mushroom=False)
bed("MushroomBed", 1.4, mushroom=True)
rug()
lamp()
table()
chair()
potted_mushroom()
painting()
pet_bed()
bookshelf()
toadstool_stool()
catalog_book()
wardrobe()

path = os.path.join(HERE, "furniture.glb")
bpy.ops.export_scene.gltf(filepath=path, export_format="GLB", export_yup=True)
print("furniture: exported", path)
