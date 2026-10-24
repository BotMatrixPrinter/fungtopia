# Builds the Food module's foods and exports them next to this script as food.glb.
# Run from the repository root:
#   blender -b --factory-startup --python modules/Food/Models/food.py
#
# Three cozy foods, one object each, named like the Food module's kinds: BerryMuffin (a fat
# muffin in a pink paper cup with blueberries and raspberries on its domed top), MushroomSoup
# (a round wooden bowl of creamy soup with little mushroom slices and a herb leaf) and SporePie
# (a golden lattice pie in a dish, a spotted mushroom cap baked in the middle, glowing spores),
# plus Crumb (a tiny crumb, for the eating effect). No faces: only creatures have eyes. Each sits
# at the origin on its base, about 0.25 m across (hand-held size), front toward Blender -Y
# (Godot +Z). Materials carry plain colors only; the game draws them toon-shaded with ink outlines.
import math
import os

import bmesh
import bpy
from mathutils import Matrix

HERE = os.path.dirname(os.path.abspath(__file__))

COLORS = {
    "Paper": (0.98, 0.62, 0.72),
    "PaperDark": (0.9, 0.48, 0.6),
    "Muffin": (0.86, 0.6, 0.34),
    "MuffinTop": (0.8, 0.52, 0.28),
    "Blueberry": (0.32, 0.36, 0.78),
    "Raspberry": (0.92, 0.28, 0.4),
    "Sugar": (1.0, 0.98, 0.94),
    "Bowl": (0.72, 0.46, 0.28),
    "BowlDark": (0.52, 0.32, 0.2),
    "Soup": (0.96, 0.8, 0.5),
    "Cap": (0.9, 0.3, 0.26),
    "Stem": (1.0, 0.92, 0.74),
    "Spots": (1.0, 0.96, 0.86),
    "Leaf": (0.46, 0.76, 0.38),
    "Crust": (0.94, 0.7, 0.36),
    "CrustDark": (0.8, 0.52, 0.24),
    "Filling": (0.7, 0.34, 0.6),
    "Dish": (0.66, 0.84, 0.96),
    "Glow": (0.86, 1.0, 0.7),
    "Crumb": (0.88, 0.64, 0.36),
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


def ring(major, minor, matrix, segments=32, tube=10):
    """A torus around Blender Z (a rim, a crust edge)."""
    part = bmesh.new()
    for i in range(segments):
        a = 2 * math.pi * i / segments
        for j in range(tube):
            b = 2 * math.pi * j / tube
            r = major + minor * math.cos(b)
            part.verts.new((r * math.cos(a), r * math.sin(a), minor * math.sin(b)))
    part.verts.ensure_lookup_table()
    for i in range(segments):
        for j in range(tube):
            i2, j2 = (i + 1) % segments, (j + 1) % tube
            part.faces.new([part.verts[i * tube + j], part.verts[i2 * tube + j],
                            part.verts[i2 * tube + j2], part.verts[i * tube + j2]])
    bmesh.ops.recalc_face_normals(part, faces=part.faces)
    bmesh.ops.transform(part, matrix=matrix, verts=part.verts)
    return part


def bowl(radius, depth, matrix, segments=32, rings=16):
    """The lower half of a squashed sphere, open on top (a bowl), its rim at `matrix`."""
    part = sphere(1.0, Matrix(), segments=segments, rings=rings)
    bmesh.ops.delete(part, geom=[v for v in part.verts if v.co.z > 1e-4], context="VERTS")
    bmesh.ops.transform(part, matrix=matrix @ S(radius, radius, depth), verts=part.verts)
    return part


def berry_muffin():
    p = Piece("BerryMuffin")
    # A pleated paper cup (wider at the top), a fat domed muffin top spilling over it.
    p.add(cylinder(0.075, 0.1, T(0, 0, 0.05), segments=24, radius2=0.095), "Paper")
    for i in range(12):
        a = 2 * math.pi * i / 12
        p.add(box((0.012, 0.012, 0.098), T(math.cos(a) * 0.087, math.sin(a) * 0.087, 0.05) @ R("Z", math.degrees(a)) @ R("Y", 11), bevel=0.004), "PaperDark")
    p.add(cylinder(0.098, 0.03, T(0, 0, 0.11), segments=28), "Muffin")
    p.add(dome(0.118, 0.095, T(0, 0, 0.115)), "MuffinTop")
    # Berries pressed into the top, a sprinkle of sugar.
    for x, y, z, r, mat in ((0.0, 0.0, 0.205, 0.024, "Raspberry"), (-0.055, -0.04, 0.18, 0.02, "Blueberry"),
                            (0.06, -0.03, 0.178, 0.019, "Blueberry"), (0.03, 0.06, 0.18, 0.02, "Blueberry"),
                            (-0.045, 0.05, 0.18, 0.018, "Raspberry"), (0.005, -0.075, 0.165, 0.017, "Blueberry")):
        p.add(sphere(r, T(x, y, z), segments=14, rings=7), mat)
    for x, y in ((0.02, -0.02), (-0.02, 0.025), (0.075, 0.03), (-0.075, 0.0), (0.04, -0.07)):
        p.add(sphere(0.007, T(x, y, 0.19 - 0.25 * (x * x + y * y) * 10), segments=8, rings=4), "Sugar")
    return p.finish()


def mushroom_soup():
    p = Piece("MushroomSoup")
    # A round wooden bowl on a foot, a thick rim, creamy soup inside.
    p.add(cylinder(0.05, 0.02, T(0, 0, 0.01), segments=24), "BowlDark")
    p.add(bowl(0.13, 0.12, T(0, 0, 0.15)), "Bowl")
    p.add(ring(0.13, 0.014, T(0, 0, 0.15)), "BowlDark")
    p.add(cylinder(0.124, 0.012, T(0, 0, 0.134), segments=32), "Soup")
    # Little mushroom slices floating, a herb leaf, and a steam-free cozy look.
    for x, y, a in ((-0.05, -0.02, 20), (0.045, 0.035, -30), (0.03, -0.06, 60)):
        p.add(dome(0.032, 0.018, T(x, y, 0.139) @ R("Z", a)), "Cap")
        p.add(box((0.016, 0.03, 0.006), T(x, y - 0.012, 0.141) @ R("Z", a), bevel=0.002), "Stem")
    p.add(sphere(0.022, T(-0.015, 0.06, 0.142) @ R("Z", 35), scale=(1.6, 0.7, 0.25), segments=12, rings=6), "Leaf")
    return p.finish()


def spore_pie():
    p = Piece("SporePie")
    # A shallow blue pie dish, a golden crust with a braided edge and a lattice over purple filling.
    p.add(cylinder(0.13, 0.03, T(0, 0, 0.015), segments=36, radius2=0.145), "Dish")
    p.add(cylinder(0.128, 0.035, T(0, 0, 0.04), segments=36), "Crust")
    p.add(cylinder(0.118, 0.01, T(0, 0, 0.06), segments=36), "Filling")
    for i in range(20):
        a = 2 * math.pi * i / 20
        p.add(sphere(0.022, T(math.cos(a) * 0.122, math.sin(a) * 0.122, 0.062), scale=(1.2, 1, 0.75), segments=10, rings=5), "Crust" if i % 2 else "CrustDark")
    for k in (-2, -1, 1, 2):
        length = 2 * math.sqrt(max(0.0, 0.112 ** 2 - (k * 0.042) ** 2))
        p.add(box((length, 0.016, 0.01), T(0, k * 0.042, 0.068), bevel=0.004), "Crust")
        p.add(box((0.016, length, 0.01), T(k * 0.042, 0, 0.072), bevel=0.004), "Crust")
    # A spotted mushroom cap baked in the middle, glowing spores around it.
    p.add(cylinder(0.022, 0.03, T(0, 0, 0.08), segments=16), "Stem")
    p.add(dome(0.05, 0.04, T(0, 0, 0.09)), "Cap")
    spots_on_dome(p, 0.05, 0.04, T(0, 0, 0.09), count=5, size=0.2)
    for i in range(6):
        a = 2 * math.pi * i / 6 + 0.4
        p.add(sphere(0.008, T(math.cos(a) * 0.075, math.sin(a) * 0.075, 0.09 + 0.012 * (i % 3)), segments=8, rings=4), "Glow")
    return p.finish()


def crumb():
    p = Piece("Crumb")
    p.add(box((0.014, 0.012, 0.01), R("Z", 25) @ R("X", 15), bevel=0.003), "Crumb")
    return p.finish()


bpy.ops.wm.read_factory_settings(use_empty=True)
for name, color in COLORS.items():
    material(name, color)

berry_muffin()
mushroom_soup()
spore_pie()
crumb()

path = os.path.join(HERE, "food.glb")
bpy.ops.export_scene.gltf(filepath=path, export_format="GLB", export_yup=True)
print("food: exported", path)
