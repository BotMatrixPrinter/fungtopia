# Builds the travel signpost and exports it next to this script as signpost.glb.
# Run from the repository root:
#   blender -b --factory-startup --python modules/Travel/Models/signpost.py
#
# Meters, facing Blender +Y (Godot's -Z, forward, after the glTF export), Z up, the foot of the
# post at the origin. Objects:
#   "Post": a chunky wooden post on a little grassy mound, topped by a red mushroom cap with
#     white spots and big round eyes looking forward.
#   "Board0", "Board1", "Board2": wooden arrow boards, top to bottom, each pointing along +X from
#     the post. Their origins are on the post's axis at the board's height, so the game turns
#     each one about the vertical axis to point at its place and puts the place's name on it.
# Materials carry plain colors only; the game draws them toon-shaded with ink outlines.
import math
import os

import bmesh
import bpy
from mathutils import Matrix, Vector

HERE = os.path.dirname(os.path.abspath(__file__))

COLORS = {
    "Wood": (0.6, 0.38, 0.2),
    "BoardWood": (0.86, 0.64, 0.4),
    "Nails": (0.36, 0.24, 0.16),
    "Grass": (0.45, 0.72, 0.32),
    "Cap": (0.9, 0.2, 0.24),
    "Spots": (1.0, 0.96, 0.9),
    "EyeWhite": (1.0, 1.0, 1.0),
    "Pupil": (0.1, 0.06, 0.08),
    "Sparkle": (1.0, 1.0, 1.0),
}

POST_HEIGHT = 2.15
POST_RADIUS = 0.1
BOARD_HEIGHTS = (1.78, 1.47, 1.16)
BOARD_LENGTH = 1.25
BOARD_TALL = 0.26
BOARD_THICK = 0.06


def material(name, color):
    mat = bpy.data.materials.new(name)
    mat.use_nodes = True
    bsdf = mat.node_tree.nodes.get("Principled BSDF")
    bsdf.inputs["Base Color"].default_value = (*color, 1.0)
    bsdf.inputs["Roughness"].default_value = 0.8
    mat.diffuse_color = (*color, 1.0)
    return mat


class Part:
    """Gathers shapes into one mesh object with several materials."""

    def __init__(self, name, origin=(0, 0, 0)):
        self.name = name
        self.origin = Vector(origin)
        self.bm = bmesh.new()
        self.materials = []

    def _add(self, make, mat, matrix):
        geom = bmesh.new()
        make(geom)
        geom.transform(Matrix.Translation(-self.origin) @ matrix)
        if mat not in self.materials:
            self.materials.append(mat)
        slot = self.materials.index(mat)
        for face in geom.faces:
            face.material_index = slot
        mesh = bpy.data.meshes.new("tmp")
        geom.to_mesh(mesh)
        geom.free()
        self.bm.from_mesh(mesh)
        bpy.data.meshes.remove(mesh)

    def sphere(self, mat, center, size, rotation=Matrix.Identity(4), segments=24, rings=12, cut=None):
        """An ellipsoid of (x, y, z) radii; cut = local z below which it is removed (a dome)."""
        m = Matrix.Translation(Vector(center)) @ rotation @ Matrix.Diagonal((*size, 1.0))

        def make(g):
            bmesh.ops.create_uvsphere(g, u_segments=segments, v_segments=rings, radius=1.0)
            if cut is not None:
                bmesh.ops.bisect_plane(g, geom=g.verts[:] + g.edges[:] + g.faces[:],
                                       plane_co=(0, 0, cut), plane_no=(0, 0, -1), clear_outer=True)
                edges = [e for e in g.edges if e.is_boundary]
                if edges:
                    bmesh.ops.holes_fill(g, edges=edges)
        self._add(make, mat, m)

    def rod(self, mat, a, b, radius1, radius2=None, segments=14):
        """A (tapered) cylinder from point a to point b."""
        a, b = Vector(a), Vector(b)
        axis = b - a
        rot = axis.normalized().to_track_quat("Z", "Y").to_matrix().to_4x4()
        m = Matrix.Translation((a + b) / 2) @ rot
        r2 = radius1 if radius2 is None else radius2
        self._add(lambda g: bmesh.ops.create_cone(g, cap_ends=True, segments=segments, radius1=radius1,
                                                  radius2=r2, depth=axis.length), mat, m)

    def prism(self, mat, outline, thick, matrix):
        """A slab: the (x, z) outline extruded along y by thick, centered, with softened edges."""
        def make(g):
            half = thick / 2
            front = [g.verts.new((x, -half, z)) for x, z in outline]
            back = [g.verts.new((x, half, z)) for x, z in outline]
            g.faces.new(front[::-1])
            g.faces.new(back)
            n = len(outline)
            for i in range(n):
                j = (i + 1) % n
                g.faces.new((front[i], front[j], back[j], back[i]))
            bmesh.ops.recalc_face_normals(g, faces=g.faces)
            bmesh.ops.bevel(g, geom=g.edges[:], offset=0.012, segments=2, affect="EDGES")
        self._add(make, mat, matrix)

    def build(self, smooth=True):
        mesh = bpy.data.meshes.new(self.name)
        self.bm.to_mesh(mesh)
        self.bm.free()
        obj = bpy.data.objects.new(self.name, mesh)
        obj.location = self.origin
        bpy.context.scene.collection.objects.link(obj)
        for poly in mesh.polygons:
            poly.use_smooth = smooth
        for mat in self.materials:
            obj.data.materials.append(mat)
        return obj


def build():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    mats = {k: material(k, c) for k, c in COLORS.items()}

    post = Part("Post")
    post.sphere(mats["Grass"], (0, 0, 0), (0.42, 0.42, 0.14), cut=0.0)
    post.rod(mats["Wood"], (0, 0, 0), (0, 0, POST_HEIGHT), POST_RADIUS * 1.15, POST_RADIUS)
    # The mushroom cap on top, with white spots.
    cap_center = Vector((0, 0, POST_HEIGHT - 0.02))
    cap_size = (0.36, 0.36, 0.3)
    post.sphere(mats["Cap"], cap_center, cap_size, cut=-0.05)
    golden = math.pi * (3 - math.sqrt(5))
    for i in range(9):
        z = 0.35 + 0.6 * (i + 0.5) / 9
        r = math.sqrt(1 - z * z)
        a = i * golden + 0.9
        n = Vector((math.cos(a) * r, math.sin(a) * r, z))
        # Keep the front low band free for the eyes.
        if n.y > 0.55 and n.z < 0.6:
            continue
        p = cap_center + Vector((n.x * cap_size[0], n.y * cap_size[1], n.z * cap_size[2]))
        rot = n.to_track_quat("Z", "Y").to_matrix().to_4x4()
        post.sphere(mats["Spots"], p, (0.055, 0.055, 0.02), rotation=rot, segments=10, rings=5)
    # Big round eyes on the front of the cap, looking forward (+Y).
    for side in (-1, 1):
        n = Vector((side * 0.36, 0.86, 0.36)).normalized()
        p = cap_center + Vector((n.x * cap_size[0], n.y * cap_size[1], n.z * cap_size[2]))
        rot = n.to_track_quat("Z", "Y").to_matrix().to_4x4()
        post.sphere(mats["EyeWhite"], p, (0.085, 0.1, 0.035), rotation=rot, segments=16, rings=8)
        out = n * 0.03
        post.sphere(mats["Pupil"], p + out, (0.058, 0.072, 0.025), rotation=rot, segments=16, rings=8)
        post.sphere(mats["Sparkle"], p + out * 1.7 + Vector((side * -0.012, 0, 0.03)), (0.02, 0.02, 0.01),
                    rotation=rot, segments=8, rings=4)
    post.build()

    for k, h in enumerate(BOARD_HEIGHTS):
        board = Part(f"Board{k}", origin=(0, 0, h))
        t = BOARD_TALL / 2
        tip = BOARD_LENGTH
        outline = [(0.0, -t), (tip - 0.2, -t), (tip, 0.0), (tip - 0.2, t), (0.0, t)]
        board.prism(mats["BoardWood"], outline, BOARD_THICK, Matrix.Translation((0, 0, h)))
        # Two nails into the post, on both faces.
        for y in (-1, 1):
            for dz in (-0.06, 0.06):
                board.sphere(mats["Nails"], (0.08, y * (BOARD_THICK / 2 + 0.004), h + dz), (0.018, 0.008, 0.018),
                             segments=8, rings=4)
        board.build(smooth=False)

    out = os.path.join(HERE, "signpost.glb")
    bpy.ops.export_scene.gltf(filepath=out, export_format="GLB", export_apply=True,
                              export_yup=True, export_materials="EXPORT")
    print(f"signpost.py: wrote {out}")


build()
