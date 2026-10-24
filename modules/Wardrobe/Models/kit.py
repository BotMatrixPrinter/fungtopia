# Shared helpers for the Wardrobe's model scripts (one script per clothing item or hair style,
# each exporting <its name>.glb next to itself). Not run on its own.
#
# Meters, feet at the origin, facing Blender +Y (Godot's -Z after the glTF export), sized for the
# Player module's chibi rig (modules/Player/Models/characters.py). Each piece is one object per
# rig part it covers, named like the part and with its origin at that part's pivot, so the game
# parents it to the part with no offset (Fungtopia.Wardrobe.Attach):
#   Head (pivot at the neck, z 0.72), Body (pivot at the hip, z 0.3),
#   LegL / LegR (pivot at the hip joints, x -0.11 / +0.11, z 0.32; the shoes go on these).
# Materials carry plain colors only; the game draws them toon-shaded with ink outlines. A hair
# style's main material is named "Hair" so the game can tint it with the chosen hair color.
# No eyes or faces on clothes: only creatures have eyes.
import math
import os
import sys

import bmesh
import bpy
from mathutils import Matrix, Vector

# The rig, from characters.py.
HIP_Z = 0.3
NECK_Z = 0.72
HEAD_R = 0.4
HEAD_C = Vector((0, 0, NECK_Z + HEAD_R * 0.92))
LEG_X = 0.11
PIVOTS = {
    "Head": (0, 0, NECK_Z),
    "Body": (0, 0, HIP_Z),
    "LegL": (-LEG_X, 0, HIP_Z + 0.02),
    "LegR": (LEG_X, 0, HIP_Z + 0.02),
}
# The chest the tops wrap around, and the shoes.
CHEST_C = Vector((0, 0, (HIP_Z + NECK_Z) / 2 - 0.02))
CHEST_R = (0.25, 0.22, 0.27)
SHOE_C = (0.04, 0.06)  # y, z of the shoe's center (x is the leg's)


def reset():
    bpy.ops.wm.read_factory_settings(use_empty=True)


def material(name, color):
    mat = bpy.data.materials.get(name)
    if mat is None:
        mat = bpy.data.materials.new(name)
        mat.use_nodes = True
    bsdf = mat.node_tree.nodes.get("Principled BSDF")
    bsdf.inputs["Base Color"].default_value = (*color, 1.0)
    bsdf.inputs["Roughness"].default_value = 0.8
    mat.diffuse_color = (*color, 1.0)
    return mat


class Part:
    """Gathers shapes into one mesh object (named like a rig part) with several materials."""

    def __init__(self, name):
        self.name = name
        self.pivot = Vector(PIVOTS[name])
        self.bm = bmesh.new()
        self.materials = []

    def _slot(self, mat):
        if mat not in self.materials:
            self.materials.append(mat)
        return self.materials.index(mat)

    def _add(self, make, mat, matrix):
        geom = bmesh.new()
        make(geom)
        geom.transform(matrix)
        slot = self._slot(mat)
        for face in geom.faces:
            face.material_index = slot
        mesh = bpy.data.meshes.new("tmp")
        geom.to_mesh(mesh)
        geom.free()
        self.bm.from_mesh(mesh)
        bpy.data.meshes.remove(mesh)

    def sphere(self, mat, center, size, rotation=Matrix.Identity(4), segments=32, rings=16, cut=None, keep=None):
        """An ellipsoid with (x, y, z) radii. cut = (point, normal) removes the part on the
        normal's side of that plane; keep = another (point, normal) plane likewise (world)."""
        m = Matrix.Translation(Vector(center) - self.pivot) @ rotation @ Matrix.Diagonal((*size, 1.0))

        def make(g):
            bmesh.ops.create_uvsphere(g, u_segments=segments, v_segments=rings, radius=1.0)
            planes = [p for p in (cut, keep) if p is not None]
            if planes:
                g.transform(m)
                for point, normal in planes:
                    bmesh.ops.bisect_plane(g, geom=g.verts[:] + g.edges[:] + g.faces[:],
                                           plane_co=Vector(point) - self.pivot, plane_no=Vector(normal),
                                           clear_outer=True)
                g.transform(m.inverted())
        self._add(make, mat, m)

    def cone(self, mat, base_center, radius1, radius2, height, segments=32, rotation=Matrix.Identity(4)):
        """A (truncated) cone standing up on base_center."""
        m = Matrix.Translation(Vector(base_center) - self.pivot) @ rotation @ Matrix.Translation((0, 0, height / 2))
        self._add(lambda g: bmesh.ops.create_cone(g, cap_ends=True, segments=segments,
                                                  radius1=radius1, radius2=radius2, depth=height), mat, m)

    def box(self, mat, center, size, rotation=Matrix.Identity(4)):
        """A box with (x, y, z) half sizes, bevelled round."""
        m = Matrix.Translation(Vector(center) - self.pivot) @ rotation @ Matrix.Diagonal((*size, 1.0))

        def make(g):
            bmesh.ops.create_cube(g, size=2.0)
            bmesh.ops.bevel(g, geom=g.edges[:] + g.verts[:], offset=0.35, segments=3, affect="EDGES")
        self._add(make, mat, m)

    def capsule(self, mat, a, b, radius, segments=16):
        """A rounded rod from point a to point b."""
        a, b = Vector(a), Vector(b)
        axis = b - a
        rot = axis.normalized().to_track_quat("Z", "Y").to_matrix().to_4x4()
        m = Matrix.Translation((a + b) / 2 - self.pivot) @ rot
        self._add(lambda g: bmesh.ops.create_cone(g, cap_ends=True, segments=segments,
                                                  radius1=radius, radius2=radius, depth=axis.length), mat, m)
        self.sphere(mat, a, (radius, radius, radius), segments=segments, rings=8)
        self.sphere(mat, b, (radius, radius, radius), segments=segments, rings=8)

    def torus(self, mat, center, major, minor, rotation=Matrix.Identity(4), arc=1.0, segments=40, scale=(1, 1)):
        """A ring (or an arc of one, arc = fraction of a full turn) around local Z; scale stretches x, y."""
        def make(g):
            n = max(3, int(segments * arc))
            closed = arc >= 1.0
            rings = []
            for i in range(n if closed else n + 1):
                t = (i / n - (0 if closed else 0.5)) * arc * 2 * math.pi
                c = Vector((math.cos(t) * major * scale[0], math.sin(t) * major * scale[1], 0))
                out = Vector((math.cos(t), math.sin(t), 0))
                ring = [g.verts.new(c + out * math.cos(j / 10 * 2 * math.pi) * minor +
                                    Vector((0, 0, math.sin(j / 10 * 2 * math.pi) * minor))) for j in range(10)]
                rings.append(ring)
            count = len(rings)
            for i in range(count if closed else count - 1):
                for j in range(10):
                    k = (j + 1) % 10
                    i2 = (i + 1) % count
                    g.faces.new((rings[i][j], rings[i2][j], rings[i2][k], rings[i][k]))
            bmesh.ops.recalc_face_normals(g, faces=g.faces)
        m = Matrix.Translation(Vector(center) - self.pivot) @ rotation
        self._add(make, mat, m)

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
        obj.location = self.pivot
        return obj


# Shapes many pieces share.

def on_chest(angle_deg, z, out=0.0):
    """A point on the chest ellipsoid's surface at a heading (0 = front, +Y) and height, pushed out."""
    t = math.radians(angle_deg)
    dz = (z - CHEST_C.z) / CHEST_R[2]
    k = math.sqrt(max(0.0, 1 - dz * dz))
    return Vector((math.sin(t) * (CHEST_R[0] * k + out), math.cos(t) * (CHEST_R[1] * k + out), z))


def shirt(part, mat, bottom_z=0.33, puff=0.02, sleeves=True, sleeve_mat=None):
    """A rounded shirt over the chest from bottom_z up to the neck, with puffy short sleeves."""
    part.sphere(mat, CHEST_C + Vector((0, 0, 0.01)), (CHEST_R[0] + puff, CHEST_R[1] + puff, CHEST_R[2] + puff * 0.5),
                cut=((0, 0, bottom_z), (0, 0, -1)), keep=((0, 0, NECK_Z - 0.02), (0, 0, 1)))
    # A soft hem ring.
    hem_r = on_chest(90, bottom_z).x + puff
    part.torus(mat, (0, 0, bottom_z), hem_r, 0.022, scale=(1, CHEST_R[1] / CHEST_R[0]))
    if sleeves:
        for side in (-1, 1):
            part.sphere(sleeve_mat or mat, (side * 0.21, 0, NECK_Z - 0.12), (0.085, 0.085, 0.075))


def collar(part, mat, minor=0.025):
    part.torus(mat, (0, 0, NECK_Z - 0.03), 0.12, minor, scale=(1, 0.92))


def hips(part, mat, top_z=0.4, puff=0.025):
    """The hips of a bottom: the lower chest down to just below the hip."""
    part.sphere(mat, (0, 0, HIP_Z + 0.04), (0.235 + puff, 0.205 + puff, 0.12), keep=((0, 0, top_z), (0, 0, 1)))
    part.sphere(mat, CHEST_C, (CHEST_R[0] + puff * 0.6, CHEST_R[1] + puff * 0.6, CHEST_R[2]),
                keep=((0, 0, top_z), (0, 0, 1)), cut=((0, 0, HIP_Z + 0.04), (0, 0, -1)))


def leg_tube(part, mat, top_z, bottom_z, radius, flare=0.0):
    """A trouser or shorts leg around the part's leg, from top_z down to bottom_z."""
    x = part.pivot.x
    part.cone(mat, (x, 0, bottom_z), radius + flare, radius, top_z - bottom_z, segments=24)
    part.torus(mat, (x, 0, bottom_z), radius + flare, 0.014, segments=24)


def skirt(part, mat, top_z, bottom_z, top_r, bottom_r, ruffle=True, segments=40):
    """A flared skirt (or the lower part of a dress) around the hips."""
    part.cone(mat, (0, 0, bottom_z), bottom_r, top_r, top_z - bottom_z, segments=segments)
    if ruffle:
        part.torus(mat, (0, 0, bottom_z), bottom_r, 0.025, segments=segments)


def cone_surface(top_z, bottom_z, top_r, bottom_r, angle_deg, z, out=0.006):
    """A point on a cone's side (0 = front), for spots and flowers on dresses."""
    f = (z - bottom_z) / (top_z - bottom_z)
    r = bottom_r + (top_r - bottom_r) * f + out
    t = math.radians(angle_deg)
    return Vector((math.sin(t) * r, math.cos(t) * r, z))


def flower(part, petal_mat, middle_mat, center, normal, size, petals=5):
    """A little flat flower facing along normal."""
    n = Vector(normal).normalized()
    rot = n.to_track_quat("Z", "Y").to_matrix().to_4x4()
    c = Vector(center)
    for i in range(petals):
        t = i / petals * 2 * math.pi
        offset = (rot @ Vector((math.cos(t) * size, math.sin(t) * size, 0, 1))).to_3d()
        part.sphere(petal_mat, c + offset, (size * 0.75, size * 0.75, size * 0.35), rotation=rot, segments=12, rings=6)
    part.sphere(middle_mat, c + n * size * 0.2, (size * 0.55, size * 0.55, size * 0.4), rotation=rot, segments=12, rings=6)


def shoe(part, mat, sole_mat, size=(0.095, 0.13, 0.072), y=None, z=None):
    """A round shoe on the part's foot, with a sole."""
    x = part.pivot.x
    y = SHOE_C[0] if y is None else y
    z = SHOE_C[1] if z is None else z
    part.sphere(mat, (x, y, z + 0.005), size, cut=((x, y, z - size[2] * 0.45), (0, 0, -1)), segments=24, rings=12)
    part.sphere(sole_mat, (x, y, z - size[2] * 0.45), (size[0] * 1.04, size[1] * 1.04, 0.022), segments=24, rings=8)


def hair_cap(part, mat, fringe=True, back_drop=0.0):
    """The shared hair cap over the top and back of the head (open over the face), with a fringe."""
    part.sphere(mat, HEAD_C + Vector((0, -0.02, 0.03)), (HEAD_R * 1.07, HEAD_R * 1.06, HEAD_R * 1.04),
                segments=40, rings=24, cut=(HEAD_C + Vector((0, 0.1, -0.1 - back_drop)), Vector((0, 0.85, -0.53))))
    if fringe:
        for x in (-0.22, -0.08, 0.08, 0.22):
            part.sphere(mat, HEAD_C + Vector((x, HEAD_R * 0.8 - abs(x) * 0.35, 0.24 - abs(x) * 0.3)),
                        (0.1, 0.07, 0.075), segments=16, rings=8)


def export(script_file):
    """Exports the scene as <script name>.glb next to the script."""
    out = os.path.splitext(os.path.abspath(script_file))[0] + ".glb"
    bpy.ops.export_scene.gltf(filepath=out, export_format="GLB", export_apply=True,
                              export_yup=True, export_materials="EXPORT")
    print(f"{os.path.basename(script_file)}: wrote {out}")
