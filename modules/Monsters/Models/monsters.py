# Builds the Mushroom Forest's monsters and exports them next to this script:
#   caterpillar.glb  and  ant.glb
# Run from the repository root:
#   blender -b --factory-startup --python modules/Monsters/Models/monsters.py
#
# From the drawings: the Mushroom Caterpillar (a round segmented body, big eyes, a spotted
# mushroom cap on its head) and the Mushroom Ant (a big round head with mushroom-tipped
# antennae, a worried face, a striped abdomen and six legs).
#
# Meters, feet at the origin, facing Blender +Y (Godot's -Z, forward, after the glTF export).
# Separate objects, each with its pivot where it moves, so the game can animate them:
#   caterpillar: Head (pivot under its middle), Seg1..Seg4 (body segments from the head back)
#   ant: Head (pivot at the neck), Thorax, Abdomen (pivot at the waist), LegL1..3 / LegR1..3
#   (pivot at the hip, front to back).
# Materials carry plain colors only; the game draws them toon-shaded with ink outlines.
import math
import os

import bmesh
import bpy
from mathutils import Matrix, Vector

HERE = os.path.dirname(os.path.abspath(__file__))

COLORS = {
    "Eye": (0.07, 0.04, 0.06),
    "EyeShine": (1.0, 1.0, 1.0),
    "Blush": (1.0, 0.45, 0.48),
    "Mouth": (0.4, 0.12, 0.14),
    "Cap": (0.9, 0.2, 0.2),
    "Spots": (1.0, 0.96, 0.9),
    "Stem": (0.98, 0.92, 0.8),
    # The caterpillar.
    "Body": (0.55, 0.82, 0.32),
    "BodyLight": (0.85, 0.93, 0.5),
    "Feet": (0.36, 0.58, 0.22),
    # The ant.
    "Shell": (0.72, 0.36, 0.2),
    "Stripe": (0.4, 0.2, 0.14),
    "Leg": (0.42, 0.22, 0.15),
    "Brow": (0.25, 0.12, 0.1),
}


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


def new_mesh_object(name, bm):
    mesh = bpy.data.meshes.new(name)
    bm.to_mesh(mesh)
    bm.free()
    obj = bpy.data.objects.new(name, mesh)
    bpy.context.scene.collection.objects.link(obj)
    for poly in mesh.polygons:
        poly.use_smooth = True
    return obj


class Part:
    """Gathers shapes into one mesh object with several materials."""

    def __init__(self, name, pivot):
        self.name = name
        self.pivot = Vector(pivot)
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

    def sphere(self, mat, center, size, rotation=Matrix.Identity(4), segments=32, rings=16, cut=None):
        """An ellipsoid: size is its (x, y, z) radii in world meters. cut = (point, normal) removes
        the part on the normal's side of that plane (world coordinates)."""
        m = Matrix.Translation(Vector(center) - self.pivot) @ rotation @ Matrix.Diagonal((*size, 1.0))

        def make(g):
            bmesh.ops.create_uvsphere(g, u_segments=segments, v_segments=rings, radius=1.0)
            if cut is not None:
                g.transform(m)
                bmesh.ops.bisect_plane(g, geom=g.verts[:] + g.edges[:] + g.faces[:],
                                       plane_co=Vector(cut[0]) - self.pivot, plane_no=Vector(cut[1]), clear_outer=True)
                g.transform(m.inverted())
        self._add(make, mat, m)

    def cone(self, mat, base_center, radius1, radius2, height, segments=32):
        """A (truncated) cone standing up on base_center."""
        m = Matrix.Translation(Vector(base_center) - self.pivot + Vector((0, 0, height / 2)))
        self._add(lambda g: bmesh.ops.create_cone(g, cap_ends=True, segments=segments,
                                                  radius1=radius1, radius2=radius2, depth=height), mat, m)

    def capsule(self, mat, a, b, radius, segments=16):
        """A rounded rod from point a to point b."""
        a, b = Vector(a), Vector(b)
        axis = b - a
        length = axis.length
        rot = axis.normalized().to_track_quat("Z", "Y").to_matrix().to_4x4()
        mid = (a + b) / 2
        m = Matrix.Translation(mid - self.pivot) @ rot
        self._add(lambda g: bmesh.ops.create_cone(g, cap_ends=True, segments=segments,
                                                  radius1=radius, radius2=radius, depth=length), mat, m)
        self.sphere(mat, a, (radius, radius, radius), segments=segments, rings=8)
        self.sphere(mat, b, (radius, radius, radius), segments=segments, rings=8)

    def torus(self, mat, center, major, minor, rotation=Matrix.Identity(4), arc=1.0, segments=40):
        """A ring (or an arc of one, arc = fraction of a full turn) around local Z."""
        def make(g):
            n = max(3, int(segments * arc))
            rings = []
            for i in range(n + 1):
                t = (i / n - 0.5) * arc * 2 * math.pi
                c = Vector((math.cos(t) * major, math.sin(t) * major, 0))
                out = Vector((math.cos(t), math.sin(t), 0))
                ring = []
                for j in range(10):
                    s = j / 10 * 2 * math.pi
                    ring.append(g.verts.new(c + out * math.cos(s) * minor + Vector((0, 0, math.sin(s) * minor))))
                rings.append(ring)
            for i in range(n):
                for j in range(10):
                    k = (j + 1) % 10
                    g.faces.new((rings[i][j], rings[i + 1][j], rings[i + 1][k], rings[i][k]))
            bmesh.ops.recalc_face_normals(g, faces=g.faces)
        m = Matrix.Translation(Vector(center) - self.pivot) @ rotation
        self._add(make, mat, m)

    def build(self):
        obj = new_mesh_object(self.name, self.bm)
        for mat in self.materials:
            obj.data.materials.append(mat)
        obj.location = self.pivot
        return obj


def mushroom_cap(part, mats, center, radius, height, tilt=Matrix.Identity(4), spots=7):
    """A spotted mushroom cap: a squashed dome with white spots, resting on center."""
    c = Vector(center)
    part.sphere(mats["Cap"], c, (radius, radius, height), rotation=tilt, segments=32, rings=16,
                cut=(c - (tilt @ Vector((0, 0, 0.02 * height, 0))).to_3d(), (tilt @ Vector((0, 0, -1, 0))).to_3d()))
    for i in range(spots):
        a = i * 2.39996 + 0.4
        k = 0.35 + 0.5 * ((i * 0.618) % 1.0)
        local = Vector((math.cos(a) * k, math.sin(a) * k, 0))
        z = math.sqrt(max(0.0, 1 - local.length_squared))
        p = Vector((local.x * radius, local.y * radius, z * height))
        n = Vector((local.x / radius, local.y / radius, z / height)).normalized()
        world = c + (tilt @ Vector((p.x, p.y, p.z, 0))).to_3d()
        normal = (tilt @ Vector((n.x, n.y, n.z, 0))).to_3d()
        rot = normal.to_track_quat("Z", "Y").to_matrix().to_4x4()
        s = radius * (0.13 + 0.06 * ((i * 0.37) % 1.0))
        part.sphere(mats["Spots"], world, (s, s, s * 0.35), rotation=rot, segments=12, rings=6)


def eyes(part, mats, center, forward_y, spacing, size, tilt_deg=18):
    """Two big round dark eyes with sparkles on the +Y face of a head at center."""
    for side in (-1, 1):
        eye_c = Vector(center) + Vector((side * spacing, forward_y, 0))
        tilt = Matrix.Rotation(math.radians(side * -tilt_deg), 4, "Z")
        part.sphere(mats["Eye"], eye_c, (size * 0.8, size * 0.55, size), rotation=tilt, segments=24, rings=12)
        part.sphere(mats["EyeShine"], eye_c + Vector((-size * 0.25, size * 0.45, size * 0.4)),
                    (size * 0.32, size * 0.2, size * 0.34), segments=12, rings=6)
        part.sphere(mats["EyeShine"], eye_c + Vector((size * 0.25, size * 0.5, -size * 0.35)),
                    (size * 0.15, size * 0.1, size * 0.15), segments=10, rings=5)


def band(part, mat, center, size, y0, y1):
    """The slab y0 <= y <= y1 (world) of an ellipsoid with radii size around center."""
    m = Matrix.Translation(Vector(center) - part.pivot) @ Matrix.Diagonal((*size, 1.0))

    def make(g):
        bmesh.ops.create_uvsphere(g, u_segments=32, v_segments=24, radius=1.0)
        g.transform(m)
        for y, normal in ((y1, (0, 1, 0)), (y0, (0, -1, 0))):
            bmesh.ops.bisect_plane(g, geom=g.verts[:] + g.edges[:] + g.faces[:],
                                   plane_co=Vector((0, y, 0)) - part.pivot, plane_no=Vector(normal), clear_outer=True)
        g.transform(m.inverted())
    part._add(make, mat, m)


def export(name):
    out = os.path.join(HERE, f"{name}.glb")
    bpy.ops.export_scene.gltf(filepath=out, export_format="GLB", export_apply=True,
                              export_yup=True, export_materials="EXPORT")
    print(f"monsters.py: wrote {out}")


def caterpillar():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    mats = {name: material(name, color) for name, color in COLORS.items()}

    # Four round body segments behind the head, getting a little smaller toward the tail.
    for i in range(4):
        r = 0.27 - i * 0.025
        y = -0.36 - i * 0.36 + i * i * 0.012
        seg = Part(f"Seg{i + 1}", (0, y, 0))
        c = Vector((0, y, r * 0.95))
        seg.sphere(mats["Body"], c, (r * 1.05, r, r * 0.95), segments=28, rings=14)
        # A pale belly band and a little dot on the back.
        seg.sphere(mats["BodyLight"], c + Vector((0, 0, -r * 0.35)), (r * 0.9, r * 0.82, r * 0.62), segments=24, rings=10)
        seg.sphere(mats["Spots"], c + Vector((0, 0, r * 0.93)), (r * 0.2, r * 0.2, r * 0.07), segments=12, rings=6)
        for side in (-1, 1):
            seg.sphere(mats["Feet"], (side * r * 0.75, y, 0.06), (0.07, 0.08, 0.06), segments=12, rings=6)
        seg.build()

    # The head: big and round, big eyes, rosy cheeks, a smile, a spotted mushroom cap.
    head_r = 0.36
    head = Part("Head", (0, 0.02, 0))
    hc = Vector((0, 0.02, head_r * 1.02))
    head.sphere(mats["Body"], hc, (head_r * 1.02, head_r * 0.95, head_r), segments=40, rings=24)
    eyes(head, mats, hc + Vector((0, 0, 0.02)), head_r * 0.82, 0.14, 0.1)
    for side in (-1, 1):
        head.sphere(mats["Blush"], hc + Vector((side * 0.24, head_r * 0.7, -0.12)), (0.06, 0.025, 0.035),
                    rotation=Matrix.Rotation(math.radians(side * -40), 4, "Z"), segments=16, rings=8)
        head.sphere(mats["Feet"], (side * 0.2, 0.08, 0.06), (0.07, 0.08, 0.06), segments=12, rings=6)
    head.torus(mats["Mouth"], hc + Vector((0, head_r * 0.93, -0.13)), 0.04, 0.01,
               rotation=Matrix.Rotation(math.radians(90), 4, "X") @ Matrix.Rotation(math.radians(-90), 4, "Z"), arc=0.42)
    head.cone(mats["Stem"], hc + Vector((0, -0.04, head_r * 0.8)), 0.09, 0.08, 0.12, segments=16)
    mushroom_cap(head, mats, hc + Vector((0, -0.04, head_r * 0.92)), 0.34, 0.24,
                 tilt=Matrix.Rotation(math.radians(-12), 4, "X"))
    head.build()
    export("caterpillar")


def ant():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    mats = {name: material(name, color) for name, color in COLORS.items()}
    hip_z = 0.38

    # Thorax: a small round middle, where the legs join.
    thorax = Part("Thorax", (0, 0, hip_z))
    thorax.sphere(mats["Shell"], (0, 0, hip_z), (0.17, 0.2, 0.16), segments=24, rings=12)
    thorax.build()

    # Abdomen: a big round striped back end.
    waist = Vector((0, -0.16, hip_z + 0.02))
    abdomen = Part("Abdomen", waist)
    ac = Vector((0, -0.48, hip_z + 0.1))
    abdomen.sphere(mats["Shell"], ac, (0.27, 0.33, 0.27), segments=32, rings=16)
    for dy in (0.12, -0.04, -0.19):
        # A dark stripe: a slab of a slightly bigger copy of the abdomen.
        band(abdomen, mats["Stripe"], ac, (0.285, 0.345, 0.285), ac.y + dy - 0.035, ac.y + dy + 0.035)
    abdomen.capsule(mats["Shell"], waist, ac + Vector((0, 0.2, 0)), 0.07, segments=12)
    abdomen.build()

    # Head: big and round, with big eyes, a worried face and mushroom-tipped antennae.
    neck = Vector((0, 0.14, hip_z + 0.06))
    head = Part("Head", neck)
    head_r = 0.3
    hc = Vector((0, 0.36, hip_z + 0.26))
    head.sphere(mats["Shell"], hc, (head_r, head_r * 0.92, head_r * 0.95), segments=40, rings=24)
    eyes(head, mats, hc + Vector((0, 0, 0.0)), head_r * 0.8, 0.12, 0.085, tilt_deg=10)
    # Worried brows: raised in the middle, sloping down outward.
    for side in (-1, 1):
        a = hc + Vector((side * 0.05, head_r * 0.84, 0.18))
        b = hc + Vector((side * 0.17, head_r * 0.76, 0.09))
        head.capsule(mats["Brow"], a, b, 0.014, segments=8)
        head.sphere(mats["Blush"], hc + Vector((side * 0.2, head_r * 0.72, -0.1)), (0.05, 0.02, 0.03),
                    rotation=Matrix.Rotation(math.radians(side * -40), 4, "Z"), segments=16, rings=8)
    # A small wobbly frown.
    head.torus(mats["Mouth"], hc + Vector((0, head_r * 0.92, -0.14)), 0.03, 0.009,
               rotation=Matrix.Rotation(math.radians(90), 4, "X") @ Matrix.Rotation(math.radians(90), 4, "Z"), arc=0.4)
    for side in (-1, 1):
        root = hc + Vector((side * 0.1, 0.1, head_r * 0.85))
        bend = root + Vector((side * 0.08, 0.06, 0.16))
        tip = bend + Vector((side * 0.06, 0.08, 0.1))
        head.capsule(mats["Leg"], root, bend, 0.016, segments=8)
        head.capsule(mats["Leg"], bend, tip, 0.016, segments=8)
        head.cone(mats["Stem"], tip - Vector((0, 0, 0.01)), 0.022, 0.02, 0.03, segments=10)
        mushroom_cap(head, mats, tip + Vector((0, 0, 0.02)), 0.075, 0.06, spots=4,
                     tilt=Matrix.Rotation(math.radians(side * -20), 4, "Y"))
    head.build()

    # Six legs: thin, bent at the knee, little round feet; pivot at the hip.
    for i, y in enumerate((0.1, 0.0, -0.1)):
        for side, prefix in ((-1, "LegL"), (1, "LegR")):
            hip = Vector((side * 0.12, y, hip_z - 0.02))
            knee = Vector((side * 0.34, y + (0.08 if i == 0 else -0.08 if i == 2 else 0.0), hip_z + 0.06))
            foot = Vector((side * 0.44, y + (0.16 if i == 0 else -0.16 if i == 2 else 0.0), 0.04))
            leg = Part(f"{prefix}{i + 1}", hip)
            leg.capsule(mats["Leg"], hip, knee, 0.03, segments=10)
            leg.capsule(mats["Leg"], knee, foot, 0.026, segments=10)
            leg.sphere(mats["Leg"], foot, (0.045, 0.05, 0.035), segments=12, rings=6)
            leg.build()
    export("ant")


caterpillar()
ant()
