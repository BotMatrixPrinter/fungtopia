# Builds the four townsfolk and exports them next to this script:
#   jeremy.glb  millie.glb  lucy.glb  abby.glb
# Run from the repository root:
#   blender -b --factory-startup --python modules/NPCs/Models/npcs.py
#
# Chibi folk in the style of the player characters (modules/Player/Models/characters.py): a big
# round head, a simple rounded body, short arms with little round hands, stubby legs.
#   Dr. Jeremy: big dark eyes with sparkles, a white doctor's coat with buttons and a name badge,
#               a stethoscope round his neck.
#   Millie:     cat ears, happy ^ ^ closed eyes, a bow at her neck, a dress (and a little tail).
#   Lucy:       a flower in her hair, half-lidded unimpressed eyes, a shirt saying "Pet Store"
#               with little hearts.
#   Abby:       big dark eyes, a happy smile, hair in a tall top bun, a triangle dress.
#
# Meters, feet at the origin, facing Blender +Y (Godot's -Z, forward, after the glTF export).
# Separate objects with pivots where they move, so the game can animate them: Body (hip), Head
# (neck), LegL / LegR (hip), ArmL / ArmR (shoulder), HandL / HandR (hand center). Materials carry
# plain colors only; the game draws them toon-shaded with ink outlines.
import math
import os

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

    def __init__(self, name, pivot, rotation=Matrix.Identity(4)):
        """rotation: the object's rotation; shapes are still given in world coordinates."""
        self.name = name
        self.pivot = Vector(pivot)
        self.rotation = rotation
        self.unrotate = rotation.inverted()
        self.bm = bmesh.new()
        self.materials = []

    def _slot(self, mat):
        if mat not in self.materials:
            self.materials.append(mat)
        return self.materials.index(mat)

    def _add(self, make, mat, matrix):
        geom = bmesh.new()
        make(geom)
        geom.transform(self.unrotate @ matrix)
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
        obj.rotation_euler = self.rotation.to_euler()
        return obj

    def box(self, mat, center, size, rotation=Matrix.Identity(4)):
        """A box: size is its full (x, y, z) extent."""
        m = Matrix.Translation(Vector(center) - self.pivot) @ rotation @ Matrix.Diagonal((*size, 1.0))
        self._add(lambda g: bmesh.ops.create_cube(g, size=1.0), mat, m)

    def world_mesh(self, mat, geom):
        """Adds a bmesh already in world coordinates (freed here)."""
        self._add(lambda g: _copy_into(geom, g), mat, Matrix.Translation(-self.pivot))


def ribbon_bow(part, mat, center, size):
    """A ribbon bow: two puffy loops and a knot, spanning x."""
    cx, cy, cz = center
    for side in (-1, 1):
        part.sphere(mat, (cx + side * size * 0.9, cy, cz + size * 0.1),
                    (size * 0.85, size * 0.45, size * 0.6),
                    rotation=Matrix.Rotation(math.radians(-18 * side), 4, "Y"))
    part.sphere(mat, (cx, cy + size * 0.12, cz), (size * 0.38, size * 0.4, size * 0.38))



def _copy_into(source, target):
    mesh = bpy.data.meshes.new("tmp_copy")
    source.to_mesh(mesh)
    source.free()
    target.from_mesh(mesh)
    bpy.data.meshes.remove(mesh)


def heart(part, mat, center, size):
    """A little heart lying on a surface that faces +Y: two lobes and a point."""
    cx, cy, cz = center
    for side in (-1, 1):
        part.sphere(mat, (cx + side * size * 0.5, cy, cz + size * 0.25), (size * 0.58, size * 0.3, size * 0.58),
                    segments=12, rings=6)
    part.sphere(mat, (cx, cy, cz - size * 0.35), (size * 0.62, size * 0.3, size * 0.62),
                rotation=Matrix.Rotation(math.radians(45), 4, "Y"), segments=4, rings=2)


def shirt_text(part, mat, text, center, size, body_c, body_r, lift=0.006):
    """Text written on the front of an ellipsoid body (center body_c, radii body_r), hugging it."""
    curve = bpy.data.curves.new("Text", type="FONT")
    curve.body = text
    curve.size = size
    curve.extrude = 0.004
    curve.align_x = "CENTER"
    curve.align_y = "CENTER"
    obj = bpy.data.objects.new("Text", curve)
    bpy.context.scene.collection.objects.link(obj)
    depsgraph = bpy.context.evaluated_depsgraph_get()
    mesh = bpy.data.meshes.new_from_object(obj.evaluated_get(depsgraph))
    bpy.data.objects.remove(obj)
    bpy.data.curves.remove(curve)
    geom = bmesh.new()
    geom.from_mesh(mesh)
    bpy.data.meshes.remove(mesh)
    rx, ry, rz = body_r
    for v in geom.verts:
        # Text space: x right, y up, z out of the page; on the shirt (seen from the front, +Y):
        # right is -X, up +Z, out +Y.
        x = center[0] - v.co.x
        z = center[2] + v.co.y
        u = ((x - body_c[0]) / rx) ** 2 + ((z - body_c[2]) / rz) ** 2
        surface = body_c[1] + ry * math.sqrt(max(0.0, 1.0 - u))
        v.co = Vector((x, surface + lift + v.co.z, z))
    part.world_mesh(mat, geom)


HIP_Z = 0.3
NECK_Z = 0.72
HEAD_R = 0.4
HEAD_C = Vector((0, 0, NECK_Z + HEAD_R * 0.92))


def round_eyes(head, mats):
    """Big dark oval eyes with sparkles (like the player characters)."""
    for side in (-1, 1):
        eye_c = HEAD_C + Vector((side * 0.15, HEAD_R * 0.86, -0.04))
        tilt = Matrix.Rotation(math.radians(side * -20), 4, "Z")
        head.sphere(mats["Eye"], eye_c, (0.08, 0.06, 0.115), rotation=tilt, segments=24, rings=12)
        head.sphere(mats["EyeShine"], eye_c + Vector((-0.022, 0.05, 0.045)), (0.03, 0.02, 0.034), segments=12, rings=6)
        head.sphere(mats["EyeShine"], eye_c + Vector((0.025, 0.055, -0.035)), (0.014, 0.01, 0.014), segments=10, rings=5)


def happy_eyes(head, mats):
    """Closed happy ^ ^ eyes: little arches."""
    for side in (-1, 1):
        eye_c = HEAD_C + Vector((side * 0.15, HEAD_R * 0.9, -0.05))
        head.torus(mats["Eye"], eye_c, 0.06, 0.016,
                   rotation=Matrix.Rotation(math.radians(90), 4, "X") @ Matrix.Rotation(math.radians(90), 4, "Z")
                   @ Matrix.Rotation(math.radians(side * 4), 4, "Z"), arc=0.42)


def sleepy_eyes(head, mats):
    """Half-lidded unimpressed eyes: the top half of each eye is hidden under a flat lid."""
    for side in (-1, 1):
        eye_c = HEAD_C + Vector((side * 0.15, HEAD_R * 0.86, -0.06))
        head.sphere(mats["Eye"], eye_c, (0.075, 0.06, 0.1), segments=24, rings=12,
                    cut=(eye_c + Vector((0, 0, -0.005)), Vector((0, 0, 1))))
        head.sphere(mats["EyeShine"], eye_c + Vector((-0.02, 0.055, -0.03)), (0.02, 0.012, 0.02), segments=10, rings=5)
        # The lid: a heavy line across the cut.
        head.capsule(mats["Lid"], eye_c + Vector((-0.085, 0.01, -0.004)), eye_c + Vector((0.085, 0.01, -0.004)),
                     0.016, segments=10)


def blush(head, mats):
    for side in (-1, 1):
        head.sphere(mats["Blush"], HEAD_C + Vector((side * 0.25, HEAD_R * 0.74, -0.15)), (0.06, 0.025, 0.035),
                    rotation=Matrix.Rotation(math.radians(side * -38), 4, "Z"), segments=16, rings=8)


def smile(head, mats, big=False):
    head.torus(mats["Mouth"], HEAD_C + Vector((0, HEAD_R * 0.92, -0.17)), 0.045 if big else 0.035, 0.01,
               rotation=Matrix.Rotation(math.radians(90), 4, "X") @ Matrix.Rotation(math.radians(-90), 4, "Z"),
               arc=0.45 if big else 0.42)


def hair_cap(head, mats, fringe=True, back=1.0):
    head.sphere(mats["Hair"], HEAD_C + Vector((0, -0.02, 0.03)), (HEAD_R * 1.07, HEAD_R * 1.06 * back, HEAD_R * 1.04),
                segments=40, rings=24, cut=(HEAD_C + Vector((0, 0.1, -0.1)), Vector((0, 0.85, -0.53))))
    if fringe:
        for x in (-0.22, -0.08, 0.08, 0.22):
            head.sphere(mats["Hair"], HEAD_C + Vector((x, HEAD_R * 0.8 - abs(x) * 0.35, 0.24 - abs(x) * 0.3)),
                        (0.1, 0.07, 0.075), segments=16, rings=8)


def limbs(mats, sleeve, legs, shoulder_x=0.2):
    for side, name in ((-1, "LegL"), (1, "LegR")):
        x = side * 0.11
        leg = Part(name, (x, 0, HIP_Z + 0.02))
        leg.capsule(mats[legs], (x, 0, HIP_Z + 0.02), (x, 0, 0.1), 0.065)
        leg.sphere(mats["Shoe"], (x, 0.04, 0.06), (0.085, 0.12, 0.065), segments=20, rings=10)
        leg.build()
    shoulder_z = NECK_Z - 0.1
    for side, name in ((-1, "ArmL"), (1, "ArmR")):
        sx = side * shoulder_x
        arm = Part(name, (sx, 0, shoulder_z))
        hand = Vector((sx + side * 0.13, 0.02, shoulder_z - 0.22))
        arm.capsule(mats[sleeve], (sx, 0, shoulder_z), hand, 0.055)
        arm.build()
        hand_part = Part("Hand" + name[-1], hand)
        hand_part.sphere(mats["Skin"], hand, (0.075, 0.075, 0.075), segments=20, rings=10)
        hand_part.build()


def jeremy(mats):
    head = Part("Head", (0, 0, NECK_Z))
    head.sphere(mats["Skin"], HEAD_C, (HEAD_R, HEAD_R * 0.96, HEAD_R * 0.94), segments=40, rings=24)
    round_eyes(head, mats)
    blush(head, mats)
    smile(head, mats)
    hair_cap(head, mats, fringe=False)
    # A neat side parting: a swoop of hair over the forehead.
    head.sphere(mats["Hair"], HEAD_C + Vector((-0.08, HEAD_R * 0.62, 0.27)), (0.26, 0.12, 0.09),
                rotation=Matrix.Rotation(math.radians(-12), 4, "Y"), segments=20, rings=10)
    # Little ears.
    for side in (-1, 1):
        head.sphere(mats["Skin"], HEAD_C + Vector((side * HEAD_R * 0.97, 0.02, -0.06)), (0.05, 0.04, 0.07),
                    segments=12, rings=6)
    head.build()

    body = Part("Body", (0, 0, HIP_Z))
    # The white doctor's coat flaring to the knees, over a blue shirt and a red tie.
    body.cone(mats["Coat"], (0, 0, HIP_Z - 0.16), 0.3, 0.17, NECK_Z - HIP_Z + 0.16, segments=40)
    body.sphere(mats["Coat"], (0, 0, NECK_Z - 0.01), (0.18, 0.16, 0.06), segments=24, rings=8)
    body.cone(mats["Shirt"], (0, 0.12, NECK_Z - 0.2), 0.035, 0.07, 0.2, segments=16)
    body.cone(mats["Tie"], (0, 0.175, NECK_Z - 0.2), 0.03, 0.012, 0.17, segments=8)
    # Lapels.
    for side in (-1, 1):
        body.box(mats["Lapel"], (side * 0.07, 0.165, NECK_Z - 0.1), (0.05, 0.02, 0.14),
                 rotation=Matrix.Rotation(math.radians(-side * 28), 4, "Y") @ Matrix.Rotation(math.radians(-20), 4, "X"))
    # Buttons down the front, a pocket with a name badge.
    for z in (0.43, 0.33, 0.23):
        r = 0.3 - (z - (HIP_Z - 0.16)) / (NECK_Z - HIP_Z + 0.16) * 0.13
        body.sphere(mats["Button"], (0.03, r + 0.006, z), (0.022, 0.012, 0.022), segments=12, rings=6)
    body.box(mats["Badge"], (-0.11, 0.2, NECK_Z - 0.17), (0.09, 0.012, 0.05),
             rotation=Matrix.Rotation(math.radians(-14), 4, "X") @ Matrix.Rotation(math.radians(-24), 4, "Z"))
    body.box(mats["BadgeStripe"], (-0.11, 0.205, NECK_Z - 0.155), (0.092, 0.012, 0.014),
             rotation=Matrix.Rotation(math.radians(-14), 4, "X") @ Matrix.Rotation(math.radians(-24), 4, "Z"))
    # The stethoscope: a ring round the neck, two tubes down the chest, a shiny chest piece.
    body.torus(mats["Tube"], (0, 0.02, NECK_Z - 0.02), 0.15, 0.014,
               rotation=Matrix.Rotation(math.radians(14), 4, "X"))
    for side in (-1, 1):
        body.capsule(mats["Tube"], (side * 0.13, 0.11, NECK_Z - 0.06), (side * 0.03 + 0.07, 0.2, NECK_Z - 0.3), 0.014,
                     segments=8)
    body.sphere(mats["Metal"], (0.1, 0.21, NECK_Z - 0.33), (0.045, 0.022, 0.045), segments=16, rings=8)
    body.build()
    limbs(mats, "Coat", "Trousers", shoulder_x=0.19)


def millie(mats):
    head = Part("Head", (0, 0, NECK_Z))
    head.sphere(mats["Skin"], HEAD_C, (HEAD_R, HEAD_R * 0.96, HEAD_R * 0.94), segments=40, rings=24)
    happy_eyes(head, mats)
    blush(head, mats)
    smile(head, mats, big=True)
    hair_cap(head, mats)
    # Two puffy bunches at the sides.
    for side in (-1, 1):
        head.sphere(mats["Hair"], HEAD_C + Vector((side * 0.38, -0.06, -0.18)), (0.12, 0.12, 0.17), segments=16, rings=8)
    # Cat ears: hair-colored triangles with pink insides.
    for side in (-1, 1):
        tip_dir = Matrix.Rotation(math.radians(side * -24), 4, "Y")
        base = HEAD_C + Vector((side * 0.25, -0.02, HEAD_R * 0.7))
        m = Matrix.Translation(base) @ tip_dir
        head._add(lambda g: bmesh.ops.create_cone(g, cap_ends=True, segments=12, radius1=0.16, radius2=0.0,
                                                  depth=0.32), mats["Hair"], Matrix.Translation(-head.pivot) @ m
                  @ Matrix.Translation((0, 0, 0.16)) @ Matrix.Diagonal((1.0, 0.55, 1.0, 1.0)))
        head._add(lambda g: bmesh.ops.create_cone(g, cap_ends=True, segments=12, radius1=0.1, radius2=0.0,
                                                  depth=0.2), mats["EarInner"], Matrix.Translation(-head.pivot) @ m
                  @ Matrix.Translation((0, 0.06, 0.17)) @ Matrix.Diagonal((1.0, 0.3, 1.0, 1.0)))
    head.build()

    body = Part("Body", (0, 0, HIP_Z))
    # A flared dress with a frilly hem and white spots, and a big bow at her neck.
    body.cone(mats["Dress"], (0, 0, HIP_Z - 0.08), 0.32, 0.13, NECK_Z - HIP_Z + 0.1, segments=40)
    body.sphere(mats["Dress"], (0, 0, NECK_Z - 0.01), (0.14, 0.13, 0.06), segments=24, rings=8)
    body.torus(mats["Frill"], (0, 0, HIP_Z - 0.07), 0.315, 0.03, segments=48)
    for a, z in ((20, 0.33), (-40, 0.28), (75, 0.37), (-95, 0.3), (140, 0.33), (-150, 0.35), (110, 0.5), (-70, 0.47)):
        t = math.radians(a + 90)
        r = 0.32 - (z - (HIP_Z - 0.08)) / (NECK_Z - HIP_Z + 0.1) * 0.19 + 0.005
        body.sphere(mats["Spots"], (math.cos(t) * r, math.sin(t) * r, z), (0.032, 0.032, 0.032), segments=12, rings=6)
    ribbon_bow(body, mats["Bow"], (0, 0.13, NECK_Z - 0.06), 0.085)
    # A little curled cat tail.
    pts = [Vector((0, -0.2, 0.3)), Vector((0.05, -0.33, 0.33)), Vector((0.1, -0.4, 0.43)), Vector((0.1, -0.38, 0.55))]
    for a, b in zip(pts, pts[1:]):
        body.capsule(mats["Hair"], a, b, 0.04, segments=10)
    body.build()
    limbs(mats, "Skin", "Skin", shoulder_x=0.16)


def lucy(mats):
    head = Part("Head", (0, 0, NECK_Z))
    head.sphere(mats["Skin"], HEAD_C, (HEAD_R, HEAD_R * 0.96, HEAD_R * 0.94), segments=40, rings=24)
    sleepy_eyes(head, mats)
    # A small flat unimpressed mouth.
    head.capsule(mats["Mouth"], HEAD_C + Vector((-0.04, HEAD_R * 0.93, -0.18)), HEAD_C + Vector((0.04, HEAD_R * 0.93, -0.18)),
                 0.009, segments=8)
    hair_cap(head, mats, fringe=False)
    # A straight-cut fringe and a bob down to the chin.
    head.sphere(mats["Hair"], HEAD_C + Vector((0, HEAD_R * 0.55, 0.22)), (0.34, 0.16, 0.11), segments=24, rings=12)
    for side in (-1, 1):
        head.sphere(mats["Hair"], HEAD_C + Vector((side * 0.33, -0.02, -0.16)), (0.13, 0.3, 0.24), segments=20, rings=10)
    # A flower in her hair: five petals round a yellow middle, above her right ear.
    flower = HEAD_C + Vector((0.24, 0.27, 0.25))
    tilt = Matrix.Rotation(math.radians(30), 4, "Y") @ Matrix.Rotation(math.radians(-55), 4, "X")
    for k in range(5):
        a = k * 2 * math.pi / 5
        off = (tilt @ Vector((math.cos(a) * 0.075, math.sin(a) * 0.075, 0, 1))).to_3d()
        head.sphere(mats["Petal"], flower + off, (0.06, 0.06, 0.06), rotation=tilt @ Matrix.Diagonal((1, 1, 0.45, 1)),
                    segments=12, rings=6)
    head.sphere(mats["FlowerMiddle"], flower + (tilt @ Vector((0, 0, 0.02, 1))).to_3d(), (0.038, 0.038, 0.038),
                segments=12, rings=6)
    head.build()

    body = Part("Body", (0, 0, HIP_Z))
    body_c = (0, 0, (HIP_Z + NECK_Z) / 2 - 0.02)
    body_r = (0.25, 0.22, 0.27)
    body.sphere(mats["Shirt"], body_c, body_r, segments=32, rings=16)
    body.sphere(mats["Skirt"], (0, 0, HIP_Z + 0.04), (0.245, 0.215, 0.1), segments=32, rings=10)
    shirt_text(body, mats["Print"], "Pet", (0, 0, 0.565), 0.085, body_c, body_r)
    shirt_text(body, mats["Print"], "Store", (0, 0, 0.485), 0.085, body_c, body_r)
    for x, z, s in ((-0.13, 0.4, 0.022), (0.12, 0.41, 0.018), (0.0, 0.415, 0.016), (-0.15, 0.6, 0.016), (0.15, 0.6, 0.02)):
        u = (x / body_r[0]) ** 2 + ((z - body_c[2]) / body_r[2]) ** 2
        y = body_r[1] * math.sqrt(max(0.0, 1 - u))
        heart(body, mats["Heart"], (x, y, z), s)
    body.build()
    limbs(mats, "Shirt", "Legs", shoulder_x=0.22)


def abby(mats):
    head = Part("Head", (0, 0, NECK_Z))
    head.sphere(mats["Skin"], HEAD_C, (HEAD_R, HEAD_R * 0.96, HEAD_R * 0.94), segments=40, rings=24)
    round_eyes(head, mats)
    blush(head, mats)
    smile(head, mats, big=True)
    hair_cap(head, mats)
    # A tall top bun: a round bun on a little hair stack, tied with a band.
    top = HEAD_C + Vector((0, -0.03, HEAD_R * 0.98))
    head.sphere(mats["Hair"], top, (0.14, 0.13, 0.1), segments=24, rings=12)
    head.sphere(mats["Band"], top + Vector((0, 0, 0.08)), (0.1, 0.095, 0.035), segments=20, rings=8)
    head.sphere(mats["Hair"], top + Vector((0, 0, 0.2)), (0.17, 0.16, 0.16), segments=28, rings=14)
    head.build()

    body = Part("Body", (0, 0, HIP_Z))
    # A triangle dress: wide at the hem, narrowing up to the neck, with a white collar and a hem stripe.
    body.cone(mats["Dress"], (0, 0, HIP_Z - 0.12), 0.36, 0.1, NECK_Z - HIP_Z + 0.13, segments=40)
    body.sphere(mats["Dress"], (0, 0, NECK_Z - 0.01), (0.11, 0.1, 0.04), segments=24, rings=8)
    body.torus(mats["Collar"], (0, 0, NECK_Z - 0.02), 0.11, 0.025, segments=36)
    body.torus(mats["Hem"], (0, 0, HIP_Z - 0.08), 0.34, 0.022, segments=48)
    body.build()
    limbs(mats, "Skin", "Skin", shoulder_x=0.15)


KINDS = {
    "jeremy": (jeremy, {
        "Hair": (0.35, 0.22, 0.14),
        "Coat": (0.97, 0.97, 0.95),
        "Lapel": (0.86, 0.88, 0.9),
        "Shirt": (0.55, 0.75, 0.95),
        "Tie": (0.85, 0.22, 0.25),
        "Button": (0.5, 0.55, 0.65),
        "Badge": (1.0, 1.0, 1.0),
        "BadgeStripe": (0.3, 0.55, 0.9),
        "Tube": (0.2, 0.22, 0.3),
        "Metal": (0.78, 0.82, 0.88),
        "Trousers": (0.3, 0.32, 0.45),
    }),
    "millie": (millie, {
        "Hair": (0.98, 0.72, 0.4),
        "EarInner": (1.0, 0.62, 0.7),
        "Dress": (0.55, 0.82, 0.62),
        "Frill": (1.0, 0.97, 0.9),
        "Spots": (1.0, 0.97, 0.9),
        "Bow": (0.98, 0.35, 0.55),
    }),
    "lucy": (lucy, {
        "Hair": (0.3, 0.2, 0.36),
        "Lid": (0.85, 0.55, 0.45),
        "Petal": (1.0, 0.6, 0.78),
        "FlowerMiddle": (1.0, 0.85, 0.3),
        "Shirt": (0.98, 0.84, 0.88),
        "Print": (0.72, 0.2, 0.4),
        "Heart": (0.95, 0.3, 0.4),
        "Skirt": (0.35, 0.4, 0.62),
        "Legs": (0.35, 0.4, 0.62),
    }),
    "abby": (abby, {
        "Hair": (0.55, 0.3, 0.18),
        "Band": (1.0, 0.45, 0.6),
        "Dress": (0.98, 0.5, 0.62),
        "Collar": (1.0, 0.97, 0.9),
        "Hem": (1.0, 0.97, 0.9),
    }),
}


def build(kind):
    bpy.ops.wm.read_factory_settings(use_empty=True)
    make, colors = KINDS[kind]
    mats = {name: material(name, color) for name, color in COLORS.items()}
    mats.update({name: material(name, color) for name, color in colors.items()})
    make(mats)
    out = os.path.join(HERE, f"{kind}.glb")
    bpy.ops.export_scene.gltf(filepath=out, export_format="GLB", export_apply=True,
                              export_yup=True, export_materials="EXPORT")
    print(f"npcs.py: wrote {out}")


for name in KINDS:
    build(name)
