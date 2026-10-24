# Builds the Mushroom Coin (M¢) and exports it next to this script as coin.glb.
# Run from the repository root:
#   blender -b --factory-startup --python modules/Wallet/Models/coin.py
#
# A chunky gold coin with a fat raised rim and, on both faces, a little embossed spotted
# mushroom with big round eyes. Meters, centered at the origin, standing upright: its faces look
# along Blender -Y / +Y (Godot +Z / -Z after the glTF export). One object, "Coin".
# Materials carry plain colors only; the game draws them toon-shaded with ink outlines.
import math
import os

import bmesh
import bpy
from mathutils import Matrix

HERE = os.path.dirname(os.path.abspath(__file__))

RADIUS = 0.13
THICK = 0.04

COLORS = {
    "Gold": (1.0, 0.78, 0.18),
    "GoldRim": (0.93, 0.6, 0.1),
    "Cap": (0.92, 0.32, 0.18),
    "Spots": (1.0, 0.95, 0.75),
    "Stem": (1.0, 0.9, 0.62),
    "Eye": (0.2, 0.1, 0.05),
    "EyeShine": (1.0, 1.0, 1.0),
}
ORDER = list(COLORS)


def material(name, color):
    mat = bpy.data.materials.new(name)
    mat.use_nodes = True
    bsdf = mat.node_tree.nodes.get("Principled BSDF")
    bsdf.inputs["Base Color"].default_value = (*color, 1.0)
    bsdf.inputs["Roughness"].default_value = 0.6
    mat.diffuse_color = (*color, 1.0)
    return mat


def add(bm, part, mat):
    """Appends bmesh `part` to `bm`, its faces using material `mat`."""
    mesh = bpy.data.meshes.new("tmp")
    part.to_mesh(mesh)
    part.free()
    for poly in mesh.polygons:
        poly.material_index = ORDER.index(mat)
    bm.from_mesh(mesh)
    bpy.data.meshes.remove(mesh)


def cylinder(radius, depth, matrix, segments=40):
    part = bmesh.new()
    bmesh.ops.create_cone(part, cap_ends=True, segments=segments, radius1=radius, radius2=radius, depth=depth,
                          matrix=matrix)
    return part


def sphere(radius, matrix, scale=(1, 1, 1), segments=20, rings=10):
    part = bmesh.new()
    bmesh.ops.create_uvsphere(part, u_segments=segments, v_segments=rings, radius=radius,
                              matrix=matrix @ Matrix.Diagonal((*scale, 1.0)))
    return part


def torus(ring_radius, tube_radius, matrix, segments=40, sides=12):
    """A ring around the Z axis."""
    part = bmesh.new()
    grid = []
    for i in range(segments):
        a = 2 * math.pi * i / segments
        row = []
        for j in range(sides):
            b = 2 * math.pi * j / sides
            r = ring_radius + tube_radius * math.cos(b)
            row.append(part.verts.new((r * math.cos(a), r * math.sin(a), tube_radius * math.sin(b))))
        grid.append(row)
    for i in range(segments):
        for j in range(sides):
            i2, j2 = (i + 1) % segments, (j + 1) % sides
            part.faces.new((grid[i][j], grid[i2][j], grid[i2][j2], grid[i][j2]))
    bmesh.ops.transform(part, matrix=matrix, verts=part.verts)
    return part


def dome(radius, height, matrix, segments=28, rings=16):
    """The upper half of a squashed sphere (a mushroom cap), closed underneath."""
    part = sphere(1.0, Matrix(), segments=segments, rings=rings)
    bmesh.ops.delete(part, geom=[v for v in part.verts if v.co.z < -1e-4], context="VERTS")
    bmesh.ops.contextual_create(part, geom=[e for e in part.edges if e.is_boundary])
    bmesh.ops.transform(part, matrix=matrix @ Matrix.Diagonal((radius, radius, height, 1.0)), verts=part.verts)
    return part


bpy.ops.wm.read_factory_settings(use_empty=True)
for name, color in COLORS.items():
    material(name, color)

# Z-axis shapes turned to lie along Y, so the coin's faces look along -Y / +Y.
FACE_ON = Matrix.Rotation(math.radians(90), 4, "X")
FACE = THICK * 0.4  # distance from the middle to a face

bm = bmesh.new()
add(bm, cylinder(RADIUS * 0.9, THICK * 0.8, FACE_ON), "Gold")
add(bm, torus(RADIUS * 0.88, THICK * 0.55, FACE_ON), "GoldRim")

for side in (-1, 1):
    # The emblem on the face looking along side * Y; the +Y one is the -Y one turned around Z.
    turn = Matrix.Rotation(math.pi, 4, "Z") if side > 0 else Matrix()

    def on_face(x, out, z):
        return turn @ Matrix.Translation((x, -FACE - out, z))

    flat = 0.35  # emblem shapes are flattened against the face
    add(bm, sphere(0.032, on_face(0, 0, -0.04), scale=(1.0, flat, 1.3)), "Stem")
    add(bm, dome(0.075, 0.06, on_face(0, 0, -0.012) @ Matrix.Diagonal((1, flat, 1, 1))), "Cap")
    for x, z, r in ((-0.042, 0.012, 0.012), (0.04, 0.02, 0.014), (0.0, 0.042, 0.011)):
        add(bm, sphere(r, on_face(x, 0.016, z), scale=(1, 0.5, 1), segments=12, rings=6), "Spots")
    for x in (-0.016, 0.016):
        add(bm, sphere(0.011, on_face(x, 0.008, -0.04), scale=(1, 0.5, 1.15), segments=12, rings=6), "Eye")
        add(bm, sphere(0.004, on_face(x + 0.004, 0.0135, -0.035), segments=8, rings=4), "EyeShine")

mesh = bpy.data.meshes.new("Coin")
bm.to_mesh(mesh)
bm.free()
for name in ORDER:
    mesh.materials.append(bpy.data.materials[name])
for poly in mesh.polygons:
    poly.use_smooth = True
coin = bpy.data.objects.new("Coin", mesh)
bpy.context.scene.collection.objects.link(coin)

path = os.path.join(HERE, "coin.glb")
bpy.ops.export_scene.gltf(filepath=path, export_format="GLB", export_yup=True)
print("coin: exported", path)
