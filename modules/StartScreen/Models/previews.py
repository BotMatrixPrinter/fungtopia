# Builds the start screen's preview models and its backdrop, and exports them next to this
# script:
#   girl.glb, boy.glb, cat.glb, dog.glb  (the choices, shown on little turntables)
#   town.glb                             (Mushroom Town, seen soft and blurred behind the logo)
# Run from the repository root:
#   blender -b --factory-startup --python modules/StartScreen/Models/previews.py
#
# From Tony's drawings, in the game's chibi style: kids with big round heads, large dark oval
# eyes with sparkles and rosy cheeks (the Girl with a ribbon bow, pigtail puffs and a spotted
# pink dress; the Boy with a bow tie and a blue shirt), the Mushroom Cat (orange, mushroom-cap
# ears, a long tail) and the Mushroom Dog (fluffy brown, little mushrooms on its back, a blue
# collar). The previews only turn on their turntables, so each is a few static objects.
#
# Meters, feet at the origin, facing Blender +Y (Godot's -Z) after the glTF export. Materials
# carry plain colors only; the game draws them toon-shaded with ink outlines.
import math
import os
import random

import bmesh
import bpy
from mathutils import Matrix, Vector

HERE = os.path.dirname(os.path.abspath(__file__))

COLORS = {
    "Skin": (0.98, 0.68, 0.52),
    "Blush": (1.0, 0.45, 0.48),
    "Eye": (0.07, 0.04, 0.06),
    "EyeShine": (1.0, 1.0, 1.0),
    "Mouth": (0.55, 0.16, 0.2),
    "Shoe": (0.42, 0.24, 0.18),
    "Spots": (1.0, 0.96, 0.9),
    "Cap": (0.9, 0.2, 0.2),
    "Stem": (0.98, 0.92, 0.8),
    "Nose": (0.95, 0.5, 0.55),
    "GirlHair": (0.45, 0.22, 0.14),
    "Dress": (0.92, 0.3, 0.5),
    "Ribbon": (0.95, 0.24, 0.36),
    "BoyHair": (0.3, 0.18, 0.1),
    "Shirt": (0.2, 0.46, 0.9),
    "Trousers": (0.3, 0.3, 0.5),
    "BowTie": (0.92, 0.22, 0.24),
    "CatFur": (1.0, 0.55, 0.22),
    "CatLight": (1.0, 0.93, 0.82),
    "CatStripe": (0.86, 0.4, 0.18),
    "DogFur": (0.7, 0.42, 0.22),
    "DogLight": (1.0, 0.92, 0.8),
    "DogEar": (0.55, 0.32, 0.18),
    "DogNose": (0.12, 0.08, 0.08),
    "Tongue": (1.0, 0.5, 0.56),
    "Collar": (0.25, 0.55, 0.95),
    "Tag": (1.0, 0.8, 0.25),
    # Mushroom Town.
    "Grass": (0.55, 0.8, 0.42),
    "GrassDark": (0.4, 0.68, 0.36),
    "Path": (0.93, 0.83, 0.64),
    "Wall": (1.0, 0.94, 0.82),
    "Door": (0.6, 0.38, 0.24),
    "Window": (1.0, 0.85, 0.45),
    "CapPink": (0.95, 0.49, 0.55),
    "CapGold": (1.0, 0.72, 0.3),
    "CapBlue": (0.45, 0.62, 0.95),
    "Trunk": (0.96, 0.88, 0.74),
    "TreeCap": (0.78, 0.25, 0.3),
    "Leaf": (0.36, 0.62, 0.34),
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


def export(name):
    out = os.path.join(HERE, f"{name}.glb")
    bpy.ops.export_scene.gltf(filepath=out, export_format="GLB", export_apply=True,
                              export_yup=True, export_materials="EXPORT")
    print(f"previews.py: wrote {out}")


def fresh():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    return {name: material(name, color) for name, color in COLORS.items()}


def eyes(part, mats, center, spacing, size, forward):
    """Two big dark oval eyes with two sparkles each, rosy cheeks below them."""
    for side in (-1, 1):
        eye_c = Vector(center) + Vector((side * spacing, forward, 0))
        tilt = Matrix.Rotation(math.radians(side * -20), 4, "Z")
        part.sphere(mats["Eye"], eye_c, (size * 0.7, size * 0.55, size), rotation=tilt, segments=24, rings=12)
        part.sphere(mats["EyeShine"], eye_c + Vector((-size * 0.2, size * 0.45, size * 0.4)),
                    (size * 0.27, size * 0.18, size * 0.3), segments=12, rings=6)
        part.sphere(mats["EyeShine"], eye_c + Vector((size * 0.22, size * 0.5, -size * 0.32)),
                    (size * 0.13, size * 0.09, size * 0.13), segments=10, rings=5)
        part.sphere(mats["Blush"], eye_c + Vector((side * size * 0.9, -size * 0.25, -size * 1.25)),
                    (size * 0.55, size * 0.22, size * 0.32), rotation=Matrix.Rotation(math.radians(side * -38), 4, "Z"),
                    segments=16, rings=8)


def ribbon_bow(part, mat, center, size):
    cx, cy, cz = center
    for side in (-1, 1):
        part.sphere(mat, (cx + side * size * 0.9, cy, cz + size * 0.1), (size * 0.85, size * 0.45, size * 0.6),
                    rotation=Matrix.Rotation(math.radians(-18 * side), 4, "Y"))
    part.sphere(mat, (cx, cy + size * 0.12, cz), (size * 0.38, size * 0.4, size * 0.38))


def mushroom(part, cap_mat, center, radius, stem_height, mats, spots=6, stem_mat=None):
    """A little mushroom: a stem and a dome cap with cream spots."""
    c = Vector(center)
    part.cone(stem_mat or mats["Stem"], c, radius * 0.4, radius * 0.32, stem_height, segments=16)
    top = c + Vector((0, 0, stem_height))
    part.sphere(cap_mat, top, (radius, radius, radius * 0.7), segments=24, rings=12, cut=(top, (0, 0, -1)))
    for i in range(spots):
        a = i / spots * 2 * math.pi + 0.4
        up = 0.55 if i % 2 == 0 else 0.3
        d = Vector((math.cos(a) * math.sqrt(1 - up * up), math.sin(a) * math.sqrt(1 - up * up), up))
        p = top + Vector((d.x * radius, d.y * radius, d.z * radius * 0.7))
        part.sphere(mats["Spots"], p, (radius * 0.16, radius * 0.16, radius * 0.1),
                    rotation=d.to_track_quat("Z", "Y").to_matrix().to_4x4(), segments=10, rings=5)


def kid(kind):
    mats = fresh()
    girl = kind == "girl"
    hair = mats["GirlHair" if girl else "BoyHair"]
    cloth = mats["Dress" if girl else "Shirt"]
    legs_mat = mats["Skin" if girl else "Trousers"]
    hip_z, neck_z, head_r = 0.3, 0.72, 0.4
    head_c = Vector((0, 0, neck_z + head_r * 0.92))

    p = Part(kind.capitalize(), (0, 0, 0))
    p.sphere(mats["Skin"], head_c, (head_r, head_r * 0.96, head_r * 0.94), segments=40, rings=24)
    eyes(p, mats, head_c + Vector((0, 0, -0.04)), 0.15, 0.11, head_r * 0.86)
    p.torus(mats["Mouth"], head_c + Vector((0, head_r * 0.92, -0.17)), 0.035, 0.009,
            rotation=Matrix.Rotation(math.radians(90), 4, "X") @ Matrix.Rotation(math.radians(-90), 4, "Z"), arc=0.42)
    p.sphere(hair, head_c + Vector((0, -0.02, 0.03)), (head_r * 1.07, head_r * 1.06, head_r * 1.04),
             segments=40, rings=24, cut=(head_c + Vector((0, 0.1, -0.1)), Vector((0, 0.85, -0.53))))
    for x in (-0.22, -0.08, 0.08, 0.22):
        p.sphere(hair, head_c + Vector((x, head_r * 0.8 - abs(x) * 0.35, 0.24 - abs(x) * 0.3)),
                 (0.1, 0.07, 0.075), segments=16, rings=8)
    if girl:
        for side in (-1, 1):
            p.sphere(hair, head_c + Vector((side * 0.4, -0.08, -0.12)), (0.12, 0.12, 0.16), segments=16, rings=8)
        ribbon_bow(p, mats["Ribbon"], head_c + Vector((0.12, 0.05, head_r * 0.98)), 0.13)
        p.cone(cloth, (0, 0, hip_z - 0.08), 0.33, 0.13, neck_z - hip_z + 0.1, segments=40)
        p.sphere(cloth, (0, 0, neck_z - 0.01), (0.14, 0.13, 0.06), segments=24, rings=8)
        for a, z in ((20, 0.32), (-40, 0.29), (75, 0.36), (-95, 0.3), (140, 0.33), (-150, 0.34),
                     (5, 0.48), (-70, 0.47), (110, 0.5)):
            t = math.radians(a + 90)
            r = 0.33 - (z - (hip_z - 0.08)) / (neck_z - hip_z + 0.1) * 0.2 + 0.005
            p.sphere(mats["Spots"], (math.cos(t) * r, math.sin(t) * r, z), (0.035, 0.035, 0.035), segments=12, rings=6)
    else:
        p.sphere(hair, head_c + Vector((0.05, 0.05, head_r * 1.0)), (0.05, 0.05, 0.1),
                 rotation=Matrix.Rotation(math.radians(25), 4, "Y"), segments=12, rings=6)
        p.sphere(cloth, (0, 0, (hip_z + neck_z) / 2 - 0.02), (0.25, 0.22, 0.27), segments=32, rings=16)
        p.sphere(legs_mat, (0, 0, hip_z + 0.04), (0.235, 0.205, 0.1), segments=32, rings=10)
        ribbon_bow(p, mats["BowTie"], (0, 0.17, neck_z - 0.08), 0.07)
        for z in (0.44, 0.36):
            p.sphere(mats["Spots"], (0, 0.215, z), (0.018, 0.012, 0.018), segments=10, rings=5)
    for side in (-1, 1):
        x = side * 0.11
        p.capsule(legs_mat, (x, 0, hip_z + 0.02), (x, 0, 0.1), 0.065)
        p.sphere(mats["Shoe"], (x, 0.04, 0.06), (0.085, 0.12, 0.065), segments=20, rings=10)
        # Arms: short and round, one waving hello.
        shoulder = Vector((side * (0.17 if girl else 0.22), 0, neck_z - 0.1))
        hand = shoulder + (Vector((side * 0.2, 0.04, 0.18)) if side > 0 else Vector((side * 0.13, 0.02, -0.22)))
        p.capsule(mats["Skin"] if girl else cloth, shoulder, hand, 0.05)
        p.sphere(mats["Skin"], hand, (0.075, 0.075, 0.075), segments=20, rings=10)
    p.build()
    export(kind)


def cat():
    mats = fresh()
    p = Part("Cat", (0, 0, 0))
    fur, light = mats["CatFur"], mats["CatLight"]
    p.sphere(fur, (0, -0.02, 0.3), (0.2, 0.26, 0.18), segments=32, rings=16)
    p.sphere(light, (0, 0.06, 0.25), (0.14, 0.18, 0.12), segments=24, rings=12)
    for y in (-0.12, 0.0):
        p.sphere(mats["CatStripe"], (0, y, 0.47), (0.12, 0.035, 0.03), segments=16, rings=8)
    for x in (-0.1, 0.1):
        for y in (-0.16, 0.12):
            p.capsule(fur, (x, y, 0.25), (x, y + 0.02, 0.05), 0.055)
            p.sphere(light, (x, y + 0.04, 0.045), (0.06, 0.07, 0.045), segments=16, rings=8)
    head_c = Vector((0, 0.22, 0.62))
    p.sphere(fur, head_c, (0.27, 0.24, 0.23), segments=40, rings=20)
    p.sphere(light, head_c + Vector((0, 0.17, -0.09)), (0.13, 0.08, 0.08), segments=20, rings=10)
    eyes(p, mats, head_c + Vector((0, 0, 0.02)), 0.11, 0.085, 0.19)
    p.sphere(mats["Nose"], head_c + Vector((0, 0.25, -0.05)), (0.03, 0.02, 0.022), segments=12, rings=6)
    for side in (-1, 1):
        p.torus(mats["Mouth"], head_c + Vector((side * 0.022, 0.245, -0.085)), 0.02, 0.006,
                rotation=Matrix.Rotation(math.radians(90), 4, "X") @ Matrix.Rotation(math.radians(-90), 4, "Z"), arc=0.45)
        for k in (-1, 1):
            a = head_c + Vector((side * 0.13, 0.2, -0.07 + k * 0.02))
            p.capsule(mats["Mouth"], a, a + Vector((side * 0.14, 0.0, k * 0.03)), 0.006, segments=6)
        # Mushroom-cap ears on short stems.
        mushroom(p, mats["Cap"], head_c + Vector((side * 0.15, -0.02, 0.17)), 0.12, 0.08, mats, spots=4)
    # A long curving tail with a cream tip.
    prev = Vector((0, -0.26, 0.3))
    for i in range(1, 9):
        t = i / 8
        cur = Vector((math.sin(t * 2.2) * 0.12, -0.26 - t * 0.2, 0.3 + t * 0.4))
        p.capsule(light if i == 8 else fur, prev, cur, 0.045 - t * 0.01)
        prev = cur
    p.build()
    export("cat")


def dog():
    mats = fresh()
    p = Part("Dog", (0, 0, 0))
    fur, light = mats["DogFur"], mats["DogLight"]
    rnd = random.Random(4)
    p.sphere(fur, (0, -0.02, 0.32), (0.22, 0.28, 0.19), segments=32, rings=16)
    for _ in range(10):
        a = rnd.uniform(0, 2 * math.pi)
        p.sphere(fur, (math.cos(a) * 0.17, -0.02 + math.sin(a) * 0.22, 0.32 + rnd.uniform(-0.05, 0.08)),
                 (0.09, 0.09, 0.08), segments=12, rings=6)
    p.sphere(light, (0, 0.16, 0.3), (0.14, 0.12, 0.15), segments=24, rings=12)
    for x in (-0.11, 0.11):
        for y in (-0.17, 0.13):
            p.capsule(fur, (x, y, 0.27), (x, y + 0.02, 0.06), 0.06)
            p.sphere(light, (x, y + 0.04, 0.05), (0.065, 0.075, 0.05), segments=16, rings=8)
    for x, y, r in ((-0.07, -0.12, 0.07), (0.08, -0.02, 0.085), (-0.04, 0.1, 0.06)):
        mushroom(p, mats["Cap"], (x, y, 0.47), r, 0.06, mats, spots=4)
    p.torus(mats["Collar"], (0, 0.2, 0.47), 0.13, 0.025, rotation=Matrix.Rotation(math.radians(-60), 4, "X"))
    p.sphere(mats["Tag"], (0, 0.33, 0.4), (0.035, 0.015, 0.035), segments=12, rings=6)
    head_c = Vector((0, 0.24, 0.66))
    p.sphere(fur, head_c, (0.26, 0.23, 0.22), segments=40, rings=20)
    for side in (-1, 1):
        p.sphere(light, head_c + Vector((side * 0.18, 0.06, -0.08)), (0.1, 0.09, 0.09), segments=16, rings=8)
        p.cone(mats["DogEar"], head_c + Vector((side * 0.15, -0.02, 0.14)), 0.08, 0.005, 0.18, segments=16)
    p.sphere(light, head_c + Vector((0, 0.19, -0.07)), (0.12, 0.1, 0.08), segments=20, rings=10)
    p.sphere(mats["DogNose"], head_c + Vector((0, 0.29, -0.03)), (0.04, 0.03, 0.03), segments=12, rings=6)
    p.sphere(mats["Tongue"], head_c + Vector((0, 0.26, -0.13)), (0.035, 0.02, 0.04), segments=12, rings=6)
    eyes(p, mats, head_c + Vector((0, 0, 0.04)), 0.11, 0.08, 0.18)
    # A curled fluffy tail.
    for i in range(6):
        a = i / 6 * 1.6 * math.pi
        p.sphere(fur, (0, -0.3 - math.sin(a) * 0.06, 0.42 + (1 - math.cos(a)) * 0.06), (0.06, 0.06, 0.06),
                 segments=12, rings=6)
    p.build()
    export("dog")


def house(p, mats, center, facing, cap):
    """A mushroom house: a fat cream stem with a door and round windows, a big spotted cap."""
    c = Vector(center)
    rot = Matrix.Rotation(facing, 4, "Z")
    fwd = (rot @ Vector((0, 1, 0, 0))).to_3d()
    side = (rot @ Vector((1, 0, 0, 0))).to_3d()
    p.cone(mats["Wall"], c, 1.6, 1.35, 2.6, segments=32)
    p.sphere(mats["Door"], c + fwd * 1.5 + Vector((0, 0, 0.6)), (0.45, 0.2, 0.75), rotation=rot, segments=16, rings=8)
    for s in (-1, 1):
        p.sphere(mats["Window"], c + fwd * 1.36 + side * s * 0.85 + Vector((0, 0, 1.6)), (0.28, 0.12, 0.28),
                 rotation=rot, segments=16, rings=8)
    mushroom(p, cap, c + Vector((0, 0, 2.3)), 2.5, 0.01, mats, spots=8, stem_mat=mats["Wall"])


def town():
    mats = fresh()
    p = Part("Town", (0, 0, 0))
    rnd = random.Random(7)
    p.cone(mats["Grass"], (0, 0, -0.3), 60, 60, 0.3, segments=48)
    p.cone(mats["Path"], (0, 0, -0.29), 7, 7, 0.3, segments=40)
    for i in range(14):
        a = rnd.uniform(0, 2 * math.pi)
        r = rnd.uniform(9, 40)
        p.sphere(mats["GrassDark"], (math.cos(a) * r, math.sin(a) * r, 0), (rnd.uniform(1, 2.2),) * 2 + (0.5,),
                 segments=16, rings=8)
    caps = (mats["Cap"], mats["CapPink"], mats["CapGold"], mats["CapBlue"])
    for i, (x, y) in enumerate(((-9, 4), (9, 3), (-5, 13), (6, 14), (-15, 12), (16, 12), (0, 22))):
        house(p, mats, (x, y, 0), math.atan2(x, -y) + math.pi, caps[i % 4])
    # Giant mushroom trees around the town.
    for x, y, h in ((-24, 18, 11), (24, 20, 12), (-18, 30, 14), (14, 32, 13), (-32, 6, 9), (32, 6, 10)):
        mushroom(p, mats["TreeCap"], (x, y, 0), h * 0.45, h, mats, spots=9, stem_mat=mats["Trunk"])
    for i in range(18):
        a = rnd.uniform(0, 2 * math.pi)
        r = rnd.uniform(6, 20)
        mushroom(p, caps[i % 4], (math.cos(a) * r, math.sin(a) * r + 6, 0), rnd.uniform(0.3, 0.6),
                 rnd.uniform(0.3, 0.6), mats, spots=4)
    p.build()
    export("town")


kid("girl")
kid("boy")
cat()
dog()
town()
