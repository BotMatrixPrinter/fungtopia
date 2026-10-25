# Builds the player's bow and the poison mushroom arrow and exports them next to this script:
#   bow.glb  and  arrow.glb
# Run from the repository root:
#   blender -b --factory-startup --python modules/Bow/Models/bow.py
#
# Meters, facing Blender +Y (Godot's -Z, forward, after the glTF export), Z up.
# bow.glb: object "Bow", a chunky wooden bow held upright, the grip at the origin, its arc
#   bulging forward and its tips (little red spotted mushroom caps) at the back, where the game
#   draws the string. The tips are at y = -TIP_BACK, z = +-TIP_HALF_SPAN.
# arrow.glb: object "Arrow", the tip at the origin pointing forward: a little purple mushroom
#   cap with green spots, a wooden shaft behind it and soft green fletching at the end.
# Materials carry plain colors (plus the caps' gloss and the spots' glow, see GLOSSY and GLOWING);
# the game draws them toon-shaded with ink outlines.
import math
import os

import bmesh
import bpy
from mathutils import Matrix, Vector

HERE = os.path.dirname(os.path.abspath(__file__))

COLORS = {
    "Wood": (0.62, 0.38, 0.2),
    "Grip": (0.45, 0.25, 0.14),
    "TipCap": (0.94, 0.07, 0.1),
    "Spots": (1.0, 1.0, 1.0),
    "Shaft": (0.78, 0.6, 0.38),
    "PoisonCap": (0.66, 0.14, 0.95),
    "PoisonSpots": (0.5, 1.0, 0.3),
    "Stem": (1.0, 0.96, 0.84),
    "Fletch": (0.5, 0.85, 0.45),
}

MAJOR = 0.42                  # radius of the bow's arc
HALF_ARC = 0.34 * math.pi     # half the arc's angle
TIP_BACK = MAJOR - MAJOR * math.cos(HALF_ARC)
TIP_HALF_SPAN = MAJOR * math.sin(HALF_ARC)
ARROW_LENGTH = 0.72

# Vivid mushrooms (Tony, 2026-10-25): the caps are glossy (a low roughness, which the game draws
# as a toon specular band and a rim light) and the poison spots glow with their whole color.
GLOSSY = {"TipCap", "PoisonCap"}
GLOWING = {"PoisonSpots"}


def material(name, color):
    mat = bpy.data.materials.get(name)
    if mat is None:
        mat = bpy.data.materials.new(name)
        mat.use_nodes = True
    bsdf = mat.node_tree.nodes.get("Principled BSDF")
    bsdf.inputs["Base Color"].default_value = (*color, 1.0)
    bsdf.inputs["Roughness"].default_value = 0.35 if name in GLOSSY else 0.8
    if name in GLOWING:
        bsdf.inputs["Emission Color"].default_value = (*color, 1.0)
        bsdf.inputs["Emission Strength"].default_value = 1.0
    mat.diffuse_color = (*color, 1.0)
    return mat


class Part:
    """Gathers shapes into one mesh object with several materials."""

    def __init__(self, name):
        self.name = name
        self.bm = bmesh.new()
        self.materials = []

    def _add(self, make, mat, matrix):
        geom = bmesh.new()
        make(geom)
        geom.transform(matrix)
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
                # Close the bottom so the dome is a solid cap.
                edges = [e for e in g.edges if e.is_boundary]
                if edges:
                    bmesh.ops.holes_fill(g, edges=edges)
        self._add(make, mat, m)

    def rod(self, mat, a, b, radius1, radius2=None, segments=12):
        """A (tapered) cylinder from point a to point b."""
        a, b = Vector(a), Vector(b)
        axis = b - a
        rot = axis.normalized().to_track_quat("Z", "Y").to_matrix().to_4x4()
        m = Matrix.Translation((a + b) / 2) @ rot
        r2 = radius1 if radius2 is None else radius2
        self._add(lambda g: bmesh.ops.create_cone(g, cap_ends=True, segments=segments, radius1=radius1,
                                                  radius2=r2, depth=axis.length), mat, m)

    def arc(self, mat, minor, segments=36):
        """The bow's limbs: an arc in the y/z plane bulging toward +y, tips back at y = -TIP_BACK."""
        def make(g):
            rings = []
            for i in range(segments + 1):
                t = (i / segments - 0.5) * 2 * HALF_ARC
                c = Vector((0, math.cos(t) * MAJOR - MAJOR, math.sin(t) * MAJOR))
                out = Vector((0, math.cos(t), math.sin(t)))
                # Chunkier in the middle, slimmer toward the tips.
                r = minor * (1.25 - 0.5 * abs(t) / HALF_ARC)
                ring = []
                for j in range(10):
                    s = j / 10 * 2 * math.pi
                    ring.append(g.verts.new(c + out * math.cos(s) * r + Vector((math.sin(s) * r, 0, 0))))
                rings.append(ring)
            for i in range(segments):
                for j in range(10):
                    k = (j + 1) % 10
                    g.faces.new((rings[i][j], rings[i + 1][j], rings[i + 1][k], rings[i][k]))
            bmesh.ops.recalc_face_normals(g, faces=g.faces)
        self._add(make, mat, Matrix.Identity(4))

    def build(self):
        mesh = bpy.data.meshes.new(self.name)
        self.bm.to_mesh(mesh)
        self.bm.free()
        obj = bpy.data.objects.new(self.name, mesh)
        bpy.context.scene.collection.objects.link(obj)
        for poly in mesh.polygons:
            poly.use_smooth = True
        for mat in self.materials:
            obj.data.materials.append(mat)
        return obj


def spots(part, mat, center, size, count, seed, up_only=True):
    """Little round spots scattered over the upper half of an ellipsoid cap."""
    golden = math.pi * (3 - math.sqrt(5))
    for i in range(count):
        z = 0.25 + 0.7 * ((i + 0.5) / count) if up_only else 1 - 2 * (i + 0.5) / count
        r = math.sqrt(max(0.0, 1 - z * z))
        a = i * golden + seed
        n = Vector((math.cos(a) * r, math.sin(a) * r, z))
        p = Vector(center) + Vector((n.x * size[0], n.y * size[1], n.z * size[2]))
        rot = n.to_track_quat("Z", "Y").to_matrix().to_4x4()
        s = 0.22 * min(size[0], size[1])
        part.sphere(mat, p, (s, s, s * 0.35), rotation=rot, segments=10, rings=5)


def reset():
    bpy.ops.wm.read_factory_settings(use_empty=True)


def export(name):
    out = os.path.join(HERE, f"{name}.glb")
    bpy.ops.export_scene.gltf(filepath=out, export_format="GLB", export_apply=True,
                              export_yup=True, export_materials="EXPORT")
    print(f"bow.py: wrote {out}")


def build_bow():
    reset()
    mats = {k: material(k, c) for k, c in COLORS.items()}
    bow = Part("Bow")
    bow.arc(mats["Wood"], 0.03)
    # A chunky leather-brown grip in the middle of the arc.
    bow.rod(mats["Grip"], (0, 0, -0.08), (0, 0, 0.08), 0.045)
    # Little red mushroom caps with white spots on both tips, domes facing outward.
    for sign in (-1, 1):
        tip = Vector((0, -TIP_BACK, sign * TIP_HALF_SPAN))
        out = Vector((0, -0.3, sign)).normalized()
        rot = out.to_track_quat("Z", "Y").to_matrix().to_4x4()
        size = (0.06, 0.06, 0.05)
        bow.sphere(mats["TipCap"], tip, size, rotation=rot, cut=-0.15)
        golden = math.pi * (3 - math.sqrt(5))
        for i in range(4):
            a = i * golden * 2.1
            n = Vector((math.cos(a) * 0.6, math.sin(a) * 0.6, 0.8))
            p = tip + (rot @ Vector((n.x * size[0], n.y * size[1], n.z * size[2]))).to_3d()
            srot = (rot.to_3x3() @ n).to_track_quat("Z", "Y").to_matrix().to_4x4()
            bow.sphere(mats["Spots"], p, (0.014, 0.014, 0.006), rotation=srot, segments=8, rings=4)
    bow.build()
    export("bow")


def build_arrow():
    reset()
    mats = {k: material(k, c) for k, c in COLORS.items()}
    arrow = Part("Arrow")
    # Forward is +Y: the cap's dome points forward, its tip at the origin.
    forward = Matrix.Rotation(-math.pi / 2, 4, "X")  # local +Z -> +Y
    cap_size = (0.055, 0.055, 0.06)
    cap_center = (0, -cap_size[2], 0)
    arrow.sphere(mats["PoisonCap"], cap_center, cap_size, rotation=forward, cut=-0.2)
    golden = math.pi * (3 - math.sqrt(5))
    for i in range(6):
        z = 0.3 + 0.6 * (i + 0.5) / 6
        r = math.sqrt(1 - z * z)
        a = i * golden
        n = Vector((math.cos(a) * r, math.sin(a) * r, z))
        local = Vector((n.x * cap_size[0], n.y * cap_size[1], n.z * cap_size[2]))
        p = Vector(cap_center) + (forward.to_3x3() @ local)
        srot = (forward.to_3x3() @ n).to_track_quat("Z", "Y").to_matrix().to_4x4()
        arrow.sphere(mats["PoisonSpots"], p, (0.014, 0.014, 0.006), rotation=srot, segments=8, rings=4)
    # A little cream stem under the cap, then the shaft.
    arrow.rod(mats["Stem"], (0, -cap_size[2] - 0.035, 0), (0, -cap_size[2] + 0.005, 0), 0.026, 0.03)
    arrow.rod(mats["Shaft"], (0, -ARROW_LENGTH, 0), (0, -cap_size[2] - 0.03, 0), 0.012, segments=8)
    # Three soft round green feathers at the end.
    for k in range(3):
        a = k * 2 * math.pi / 3
        rot = Matrix.Rotation(a, 4, "Y") @ Matrix.Rotation(math.pi / 2, 4, "Z")
        center = Vector((0, -ARROW_LENGTH + 0.07, 0)) + (Matrix.Rotation(a, 4, "Y") @ Vector((0.03, 0, 0, 1))).to_3d()
        arrow.sphere(mats["Fletch"], center, (0.07, 0.03, 0.006), rotation=rot, segments=12, rings=6)
    arrow.build()
    export("arrow")


build_bow()
build_arrow()
