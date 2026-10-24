# Builds the starter pets and exports them next to this script:
#   cat.glb  and  dog.glb
# Run from the repository root:
#   blender -b --factory-startup --python modules/Pet/Models/pets.py
#
# From the drawings: the Mushroom Cat (big eyes, whiskers, two spotted mushroom caps for ears,
# a long tail) and the Mushroom Dog (fluffy, pointy ears, small mushrooms growing on its back,
# a collar with a tag).
#
# Meters, feet at the origin, facing Blender +Y (Godot's -Z, forward, after the glTF export).
# Separate objects, each with its pivot where it moves, so the game can animate them:
#   Body (pivot at its middle), Head (pivot at the neck), LegFL / LegFR / LegBL / LegBR (pivot at
#   the shoulder or hip), Tail (pivot at its root).
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
    "Blush": (1.0, 0.45, 0.5),
    "Mouth": (0.4, 0.12, 0.14),
    "Nose": (0.95, 0.5, 0.55),
    "Whisker": (0.3, 0.18, 0.16),
    "Cap": (0.9, 0.2, 0.2),
    "Spots": (1.0, 0.96, 0.9),
    "Stem": (0.98, 0.92, 0.8),
    # The cat.
    "CatFur": (1.0, 0.55, 0.22),
    "CatLight": (1.0, 0.93, 0.82),
    "CatStripe": (0.86, 0.4, 0.18),
    # The dog.
    "DogFur": (0.7, 0.42, 0.22),
    "DogLight": (1.0, 0.92, 0.8),
    "DogEar": (0.55, 0.32, 0.18),
    "DogNose": (0.12, 0.08, 0.08),
    "Tongue": (1.0, 0.5, 0.56),
    "Collar": (0.25, 0.55, 0.95),
    "Tag": (1.0, 0.8, 0.25),
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

    def cone(self, mat, base_center, radius1, radius2, height, rotation=Matrix.Identity(4), segments=32):
        """A (truncated) cone standing on base_center along the rotated Z axis."""
        m = Matrix.Translation(Vector(base_center) - self.pivot) @ rotation @ Matrix.Translation((0, 0, height / 2))
        self._add(lambda g: bmesh.ops.create_cone(g, cap_ends=True, segments=segments,
                                                  radius1=radius1, radius2=radius2, depth=height), mat, m)

    def capsule(self, mat, a, b, radius, segments=16, radius_b=None):
        """A rounded rod from point a to point b (tapering to radius_b)."""
        a, b = Vector(a), Vector(b)
        rb = radius if radius_b is None else radius_b
        axis = b - a
        length = axis.length
        rot = axis.normalized().to_track_quat("Z", "Y").to_matrix().to_4x4()
        mid = (a + b) / 2
        m = Matrix.Translation(mid - self.pivot) @ rot
        self._add(lambda g: bmesh.ops.create_cone(g, cap_ends=True, segments=segments,
                                                  radius1=radius, radius2=rb, depth=length), mat, m)
        self.sphere(mat, a, (radius, radius, radius), segments=segments, rings=8)
        self.sphere(mat, b, (rb, rb, rb), segments=segments, rings=8)

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


def little_mushroom(part, mats, base, size, tilt=Matrix.Identity(4)):
    """A tiny mushroom (stem and spotted cap) growing up from base along the tilted Z axis."""
    up = (tilt @ Vector((0, 0, 1, 0))).to_3d()
    part.cone(mats["Stem"], base, size * 0.32, size * 0.26, size * 0.9, rotation=tilt, segments=12)
    mushroom_cap(part, mats, Vector(base) + up * size * 0.8, size * 0.62, size * 0.5, tilt=tilt, spots=4)


def eyes(part, mats, center, forward_y, spacing, size, tilt_deg=14):
    """Two big round dark eyes with sparkles on the +Y face of a head at center."""
    for side in (-1, 1):
        eye_c = Vector(center) + Vector((side * spacing, forward_y, 0))
        tilt = Matrix.Rotation(math.radians(side * -tilt_deg), 4, "Z")
        part.sphere(mats["Eye"], eye_c, (size * 0.8, size * 0.55, size), rotation=tilt, segments=24, rings=12)
        part.sphere(mats["EyeShine"], eye_c + Vector((-size * 0.25, size * 0.45, size * 0.4)),
                    (size * 0.32, size * 0.2, size * 0.34), segments=12, rings=6)
        part.sphere(mats["EyeShine"], eye_c + Vector((size * 0.25, size * 0.5, -size * 0.35)),
                    (size * 0.15, size * 0.1, size * 0.15), segments=10, rings=5)


def smile(part, mats, center, size):
    """A little cat mouth: two small arcs side by side, like a 'w'."""
    for side in (-1, 1):
        part.torus(mats["Mouth"], Vector(center) + Vector((side * size, 0, 0)), size, size * 0.24,
                   rotation=Matrix.Rotation(math.radians(90), 4, "X") @ Matrix.Rotation(math.radians(-90), 4, "Z"),
                   arc=0.45, segments=24)


def legs(mats, fur, paw, hip_z, front_y, back_y, spread, radius):
    """Four stubby legs with round paws, each its own object pivoting at the shoulder or hip."""
    for name, x, y in (("LegFL", -spread, front_y), ("LegFR", spread, front_y),
                       ("LegBL", -spread, back_y), ("LegBR", spread, back_y)):
        top = Vector((x, y, hip_z))
        foot = Vector((x, y + 0.01, radius * 0.75))
        leg = Part(name, top)
        leg.capsule(mats[fur], top, foot, radius, segments=16, radius_b=radius * 0.92)
        leg.sphere(mats[paw], foot + Vector((0, 0.025, -radius * 0.2)), (radius * 1.08, radius * 1.25, radius * 0.7),
                   segments=16, rings=8)
        leg.build()


def export(name):
    out = os.path.join(HERE, f"{name}.glb")
    bpy.ops.export_scene.gltf(filepath=out, export_format="GLB", export_apply=True,
                              export_yup=True, export_materials="EXPORT")
    print(f"pets.py: wrote {out}")


def cat():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    mats = {name: material(name, color) for name, color in COLORS.items()}

    # Body: a round loaf with a cream belly and a few soft stripes on the back.
    bc = Vector((0, -0.02, 0.33))
    body = Part("Body", bc)
    body.sphere(mats["CatFur"], bc, (0.19, 0.29, 0.18), segments=36, rings=18)
    body.sphere(mats["CatLight"], bc + Vector((0, 0.04, -0.06)), (0.16, 0.25, 0.13), segments=28, rings=12)
    for dy in (0.1, -0.02, -0.14):
        body.sphere(mats["CatStripe"], bc + Vector((0, dy, 0.16)), (0.13, 0.03, 0.035), segments=16, rings=8)
    body.build()

    legs(mats, "CatFur", "CatLight", 0.27, 0.15, -0.17, 0.1, 0.06)

    # Head: big and round with huge eyes, a pink nose, a 'w' mouth, whiskers and mushroom-cap ears.
    neck = Vector((0, 0.18, 0.44))
    head = Part("Head", neck)
    hr = 0.26
    hc = Vector((0, 0.3, 0.64))
    head.sphere(mats["CatFur"], hc, (hr * 1.08, hr * 0.94, hr * 0.96), segments=40, rings=24)
    head.sphere(mats["CatLight"], hc + Vector((0, hr * 0.62, -0.09)), (0.12, 0.08, 0.08), segments=24, rings=12)
    eyes(head, mats, hc + Vector((0, 0, 0.03)), hr * 0.8, 0.105, 0.075)
    head.sphere(mats["Nose"], hc + Vector((0, hr * 0.93, -0.05)), (0.028, 0.018, 0.02), segments=16, rings=8)
    smile(head, mats, hc + Vector((0, hr * 0.9, -0.09)), 0.018)
    for side in (-1, 1):
        head.sphere(mats["Blush"], hc + Vector((side * 0.17, hr * 0.7, -0.07)), (0.045, 0.02, 0.028),
                    rotation=Matrix.Rotation(math.radians(side * -40), 4, "Z"), segments=16, rings=8)
        # Three whiskers fanning out from each cheek.
        for k, dz in enumerate((0.02, -0.01, -0.04)):
            root = hc + Vector((side * 0.1, hr * 0.84, -0.05 + dz * 0.5))
            tip = root + Vector((side * 0.2, -0.03, dz * 1.6 + 0.01))
            head.capsule(mats["Whisker"], root, tip, 0.005, segments=6)
        # The ears: little spotted mushroom caps on short stems, tipped outward.
        tilt = Matrix.Rotation(math.radians(side * 28), 4, "Y") @ Matrix.Rotation(math.radians(-8), 4, "X")
        base = hc + Vector((side * 0.13, -0.02, hr * 0.78))
        up = (tilt @ Vector((0, 0, 1, 0))).to_3d()
        head.cone(mats["Stem"], base, 0.05, 0.045, 0.09, rotation=tilt, segments=16)
        mushroom_cap(head, mats, base + up * 0.08, 0.11, 0.09, tilt=tilt, spots=5)
    head.build()

    # Tail: long, curving up and back, with a cream tip; pivot at its root.
    root = Vector((0, -0.29, 0.38))
    tail = Part("Tail", root)
    points = [root, Vector((0, -0.42, 0.44)), Vector((0, -0.5, 0.58)), Vector((0, -0.5, 0.74)),
              Vector((0, -0.44, 0.84))]
    for i in range(len(points) - 1):
        r0 = 0.05 - i * 0.004
        mat = mats["CatLight"] if i == len(points) - 2 else mats["CatFur"]
        tail.capsule(mat, points[i], points[i + 1], r0, segments=14, radius_b=r0 - 0.004)
    tail.build()
    export("cat")


def dog():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    mats = {name: material(name, color) for name, color in COLORS.items()}

    # Body: a fluffy round body of soft clumps, a cream chest, small mushrooms on the back and
    # a blue collar with a golden tag at the neck.
    bc = Vector((0, -0.02, 0.36))
    body = Part("Body", bc)
    body.sphere(mats["DogFur"], bc, (0.21, 0.3, 0.2), segments=36, rings=18)
    for i in range(10):
        a = i * 2.39996
        y = -0.24 + 0.48 * (i / 9)
        p = bc + Vector((math.cos(a) * 0.15, y, 0.04 + math.sin(a) * 0.12))
        body.sphere(mats["DogFur"], p, (0.09, 0.09, 0.08), segments=16, rings=8)
    body.sphere(mats["DogLight"], bc + Vector((0, 0.18, -0.02)), (0.15, 0.13, 0.15), segments=24, rings=12)
    for dx, dy, size, lean in ((0.06, 0.06, 0.14, -14), (-0.08, -0.07, 0.115, 16), (0.03, -0.2, 0.09, -8)):
        tilt = Matrix.Rotation(math.radians(lean), 4, "Y") @ Matrix.Rotation(math.radians(dy * -60), 4, "X")
        little_mushroom(body, mats, bc + Vector((dx, dy, 0.22 - abs(dy) * 0.15)), size, tilt=tilt)
    collar_c = Vector((0, 0.2, 0.47))
    body.torus(mats["Collar"], collar_c, 0.13, 0.026, rotation=Matrix.Rotation(math.radians(-35), 4, "X"))
    tag_c = collar_c + Vector((0, 0.13, -0.08))
    body.cone(mats["Tag"], tag_c, 0.035, 0.035, 0.012, rotation=Matrix.Rotation(math.radians(80), 4, "X"), segments=20)
    body.build()

    legs(mats, "DogFur", "DogLight", 0.3, 0.16, -0.18, 0.11, 0.07)

    # Head: round with big eyes, a cream muzzle with a black nose, a tongue, and pointy ears.
    neck = Vector((0, 0.2, 0.48))
    head = Part("Head", neck)
    hr = 0.25
    hc = Vector((0, 0.32, 0.68))
    head.sphere(mats["DogFur"], hc, (hr * 1.05, hr * 0.95, hr * 0.95), segments=40, rings=24)
    # Fluffy cheeks.
    for side in (-1, 1):
        head.sphere(mats["DogFur"], hc + Vector((side * 0.17, 0.04, -0.09)), (0.1, 0.09, 0.09), segments=16, rings=8)
    muzzle = hc + Vector((0, hr * 0.8, -0.08))
    head.sphere(mats["DogLight"], muzzle, (0.11, 0.08, 0.075), segments=24, rings=12)
    head.sphere(mats["DogNose"], muzzle + Vector((0, 0.075, 0.035)), (0.035, 0.022, 0.025), segments=16, rings=8)
    head.sphere(mats["EyeShine"], muzzle + Vector((-0.01, 0.094, 0.045)), (0.009, 0.004, 0.006), segments=8, rings=4)
    head.torus(mats["Mouth"], muzzle + Vector((0, 0.07, -0.025)), 0.03, 0.007,
               rotation=Matrix.Rotation(math.radians(90), 4, "X") @ Matrix.Rotation(math.radians(-90), 4, "Z"), arc=0.42)
    head.sphere(mats["Tongue"], muzzle + Vector((0.0, 0.06, -0.06)), (0.026, 0.014, 0.03), segments=12, rings=6)
    eyes(head, mats, hc + Vector((0, 0, 0.04)), hr * 0.8, 0.1, 0.072)
    for side in (-1, 1):
        head.sphere(mats["Blush"], hc + Vector((side * 0.16, hr * 0.68, -0.05)), (0.04, 0.018, 0.026),
                    rotation=Matrix.Rotation(math.radians(side * -40), 4, "Z"), segments=16, rings=8)
        # Pointy ears, tipped out a little, with a darker inside.
        tilt = Matrix.Rotation(math.radians(side * 22), 4, "Y") @ Matrix.Rotation(math.radians(-6), 4, "X")
        base = hc + Vector((side * 0.13, -0.02, hr * 0.72))
        head.cone(mats["DogFur"], base, 0.08, 0.008, 0.2, rotation=tilt, segments=20)
        head.cone(mats["DogEar"], base + (tilt @ Vector((0, 0.035, 0.01, 0))).to_3d(), 0.05, 0.006, 0.15,
                  rotation=tilt, segments=16)
    head.build()

    # Tail: a short fluffy plume curling up; pivot at its root.
    root = Vector((0, -0.3, 0.42))
    tail = Part("Tail", root)
    for i, (y, z, r) in enumerate(((-0.33, 0.45, 0.06), (-0.37, 0.52, 0.07), (-0.38, 0.6, 0.075),
                                   (-0.35, 0.67, 0.07), (-0.31, 0.71, 0.055))):
        mat = mats["DogLight"] if i == 4 else mats["DogFur"]
        tail.sphere(mat, (0, y, z), (r, r, r), segments=16, rings=8)
    tail.build()
    export("dog")


cat()
dog()
