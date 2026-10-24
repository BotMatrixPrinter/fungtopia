# Builds the two player characters and exports them next to this script:
#   girl.glb  and  boy.glb
# Run from the repository root:
#   blender -b --factory-startup --python modules/Player/Models/characters.py
#
# Chibi kids from the drawing: a big round head with large dark oval eyes, a simple rounded
# body, little round hands, stubby legs and a bow carried on the back. The Girl has a ribbon
# bow on her head and a triangle dress; the Boy a bow tie and a rounded body.
#
# Meters, feet at the origin, facing Blender +Y (Godot's -Z, forward, after the glTF export).
# Separate objects, each with its pivot where it moves, so the game can animate them:
#   Body, Head (pivot at the neck), LegL / LegR (pivot at the hip), ArmL / ArmR (pivot at the
#   shoulder), HandL / HandR (pivot at the hand's center; the game keeps them at the end of the
#   arms), BackBow, and for shooting: HandBow (the same bow without its string, which the game
#   draws; pivot at the grip) and NockArrow (the nocked arrow; pivot at its nock). HandBow rests
#   exactly where BackBow is; its rotation is the sling, so its own frame is the held bow's: grip
#   at the origin, tips up and down (Godot +-Y), arc bulging forward (Godot -Z), tips behind.
#   NockArrow's own frame points forward (Godot -Z) from the nock; at rest it stands upright,
#   hidden inside the body. Materials carry plain colors only; the game draws them toon-shaded
#   with ink outlines.
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
    "Wood": (0.62, 0.38, 0.2),
    "String": (0.95, 0.92, 0.82),
    "Shoe": (0.42, 0.24, 0.18),
    # The nocked arrow, like the Bow module's arrow.glb: a poison mushroom tip.
    "Shaft": (0.78, 0.6, 0.38),
    "PoisonCap": (0.56, 0.24, 0.72),
    "PoisonSpots": (0.55, 0.95, 0.35),
    "Stem": (0.95, 0.9, 0.78),
    "Fletch": (0.5, 0.85, 0.45),
}

# The nocked arrow, nock to tip; the game keeps CharacterModel.cs and ArcheryPose.cs in step.
NOCK_ARROW_LENGTH = 0.66

KINDS = {
    "girl": {
        "Hair": (0.45, 0.22, 0.14),
        "Cloth": (0.92, 0.3, 0.5),     # the dress
        "Ribbon": (0.95, 0.24, 0.36),   # the bow on her head
        "Legs": (0.98, 0.68, 0.52),
        "Spots": (1.0, 0.96, 0.9),
    },
    "boy": {
        "Hair": (0.3, 0.18, 0.1),
        "Cloth": (0.2, 0.46, 0.9),    # the shirt
        "Ribbon": (0.92, 0.22, 0.24),   # the bow tie
        "Legs": (0.3, 0.3, 0.5),        # trousers
        "Spots": (1.0, 0.96, 0.9),
    },
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


def ribbon_bow(part, mat, center, size):
    """A ribbon bow: two puffy loops and a knot, spanning x."""
    cx, cy, cz = center
    for side in (-1, 1):
        part.sphere(mat, (cx + side * size * 0.9, cy, cz + size * 0.1),
                    (size * 0.85, size * 0.45, size * 0.6),
                    rotation=Matrix.Rotation(math.radians(-18 * side), 4, "Y"))
    part.sphere(mat, (cx, cy + size * 0.12, cz), (size * 0.38, size * 0.4, size * 0.38))


def build(kind):
    bpy.ops.wm.read_factory_settings(use_empty=True)
    mats = {name: material(name, color) for name, color in COLORS.items()}
    mats.update({name: material(name, color) for name, color in KINDS[kind].items()})
    girl = kind == "girl"

    hip_z = 0.3
    neck_z = 0.72
    head_r = 0.4
    head_c = Vector((0, 0, neck_z + head_r * 0.92))

    # Head: big and round, large dark oval eyes with sparkles, rosy cheeks, a small smile, hair.
    head = Part("Head", (0, 0, neck_z))
    head.sphere(mats["Skin"], head_c, (head_r, head_r * 0.96, head_r * 0.94), segments=40, rings=24)
    for side in (-1, 1):
        ex = side * 0.15
        eye_c = head_c + Vector((ex, head_r * 0.86, -0.04))
        tilt = Matrix.Rotation(math.radians(side * -20), 4, "Z")
        head.sphere(mats["Eye"], eye_c, (0.078, 0.06, 0.112), rotation=tilt, segments=24, rings=12)
        head.sphere(mats["EyeShine"], eye_c + Vector((-0.022, 0.05, 0.045)), (0.03, 0.02, 0.034), segments=12, rings=6)
        head.sphere(mats["EyeShine"], eye_c + Vector((0.025, 0.055, -0.035)), (0.014, 0.01, 0.014), segments=10, rings=5)
        head.sphere(mats["Blush"], head_c + Vector((side * 0.25, head_r * 0.74, -0.15)), (0.06, 0.025, 0.035),
                    rotation=Matrix.Rotation(math.radians(side * -38), 4, "Z"), segments=16, rings=8)
    head.torus(mats["Mouth"], head_c + Vector((0, head_r * 0.92, -0.17)), 0.035, 0.009,
               rotation=Matrix.Rotation(math.radians(90), 4, "X") @ Matrix.Rotation(math.radians(-90), 4, "Z"), arc=0.42)
    # Hair: a cap over the top and back of the head (cut open over the face), with a fringe.
    head.sphere(mats["Hair"], head_c + Vector((0, -0.02, 0.03)), (head_r * 1.07, head_r * 1.06, head_r * 1.04),
                segments=40, rings=24, cut=(head_c + Vector((0, 0.1, -0.1)), Vector((0, 0.85, -0.53))))
    for x in (-0.22, -0.08, 0.08, 0.22):
        head.sphere(mats["Hair"], head_c + Vector((x, head_r * 0.8 - abs(x) * 0.35, 0.24 - abs(x) * 0.3)),
                    (0.1, 0.07, 0.075), segments=16, rings=8)
    if girl:
        # Two little pigtail puffs and a big ribbon bow on top.
        for side in (-1, 1):
            head.sphere(mats["Hair"], head_c + Vector((side * 0.4, -0.08, -0.12)), (0.12, 0.12, 0.16), segments=16, rings=8)
        ribbon_bow(head, mats["Ribbon"], head_c + Vector((0.12, 0.05, head_r * 0.98)), 0.13)
    else:
        # A cowlick tuft.
        head.sphere(mats["Hair"], head_c + Vector((0.05, 0.05, head_r * 1.0)), (0.05, 0.05, 0.1),
                    rotation=Matrix.Rotation(math.radians(25), 4, "Y"), segments=12, rings=6)
    head.build()

    # Body.
    body = Part("Body", (0, 0, hip_z))
    if girl:
        # A triangle dress flaring out over the legs, with white spots like a mushroom cap.
        body.cone(mats["Cloth"], (0, 0, hip_z - 0.08), 0.33, 0.13, neck_z - hip_z + 0.1, segments=40)
        body.sphere(mats["Cloth"], (0, 0, neck_z - 0.01), (0.14, 0.13, 0.06), segments=24, rings=8)
        for i, (a, z) in enumerate(((20, 0.32), (-40, 0.29), (75, 0.36), (-95, 0.3), (140, 0.33),
                                    (-150, 0.34), (5, 0.48), (-70, 0.47), (110, 0.5))):
            t = math.radians(a + 90)
            r = 0.33 - (z - (hip_z - 0.08)) / (neck_z - hip_z + 0.1) * 0.2 + 0.005
            body.sphere(mats["Spots"], (math.cos(t) * r, math.sin(t) * r, z), (0.035, 0.035, 0.035),
                        segments=12, rings=6)
    else:
        # A rounded body in a shirt, with a bow tie at the collar.
        body.sphere(mats["Cloth"], (0, 0, (hip_z + neck_z) / 2 - 0.02), (0.25, 0.22, 0.27), segments=32, rings=16)
        body.sphere(mats["Legs"], (0, 0, hip_z + 0.04), (0.235, 0.205, 0.1), segments=32, rings=10)
        ribbon_bow(body, mats["Ribbon"], (0, 0.17, neck_z - 0.08), 0.07)
        for z in (0.44, 0.36):
            body.sphere(mats["Spots"], (0, 0.215, z), (0.018, 0.012, 0.018), segments=10, rings=5)
    # The bow's strap across the chest.
    strap = Matrix.Rotation(math.radians(35), 4, "Y")
    body.sphere(mats["Wood"], (0, 0, (hip_z + neck_z) / 2 + 0.06),
                (0.27 if not girl else 0.21, 0.24 if not girl else 0.2, 0.025), rotation=strap, segments=32, rings=6)
    body.build()

    # Legs: stubby, with round shoes; pivot at the hip.
    for side, name in ((-1, "LegL"), (1, "LegR")):
        x = side * 0.11
        leg = Part(name, (x, 0, hip_z + 0.02))
        leg.capsule(mats["Legs"], (x, 0, hip_z + 0.02), (x, 0, 0.1), 0.065)
        leg.sphere(mats["Shoe"], (x, 0.04, 0.06), (0.085, 0.12, 0.065), segments=20, rings=10)
        leg.build()

    # Arms: short and round with little round hands; pivot at the shoulder.
    shoulder_z = neck_z - 0.1
    for side, name in ((-1, "ArmL"), (1, "ArmR")):
        sx = side * (0.17 if girl else 0.22)
        arm = Part(name, (sx, 0, shoulder_z))
        hand = Vector((sx + side * 0.13, 0.02, shoulder_z - 0.22))
        arm.capsule(mats["Cloth"] if not girl else mats["Skin"], (sx, 0, shoulder_z), hand, 0.05)
        arm.build()
        hand_part = Part("Hand" + name[-1], hand)
        hand_part.sphere(mats["Skin"], hand, (0.075, 0.075, 0.075), segments=20, rings=10)
        hand_part.build()

    # A little wooden bow carried on the back, slung diagonally: the middle of its arc rests
    # against the back, its tips and string stick out behind.
    # HandBow is the same bow without the string (the game draws it, bent by the draw), with the
    # sling as its rotation: its own frame is the arc frame below, Godot's held-bow frame.
    grip = (0, -0.25, (hip_z + neck_z) / 2 + 0.04)
    major, half_arc = 0.42, 0.34 * math.pi
    # Arc frame: local x is depth (the arc's middle at x = 0, tips toward -x), local y the span.
    frame = Matrix(((0, 0, 1, 0), (1, 0, 0, 0), (0, 1, 0, 0), (0, 0, 0, 1)))  # x->+Y, y->+Z, z->+X
    slant = Matrix.Rotation(math.radians(38), 4, "Y")
    sling = slant @ frame
    for name in ("BackBow", "HandBow"):
        bow = Part(name, grip, rotation=slant if name == "HandBow" else Matrix.Identity(4))
        bow.torus(mats["Wood"], bow.pivot, major, 0.028,
                  rotation=sling @ Matrix.Translation((-major, 0, 0)), arc=half_arc / math.pi)
        tips = []
        for sign in (-1, 1):
            local = Vector((major * math.cos(half_arc) - major, sign * major * math.sin(half_arc), 0, 1))
            tips.append(bow.pivot + (sling @ local).to_3d())
        if name == "BackBow":
            bow.capsule(mats["String"], tips[0], tips[1], 0.009, segments=6)
        for tip in tips:
            bow.sphere(mats["Ribbon"], tip, (0.045, 0.045, 0.04), segments=12, rings=6)
        bow.build()

    # The nocked arrow: a poison mushroom tip (a purple cap with glowing green spots on a cream
    # stem), a wooden shaft and soft green fletching. Built pointing up (+Z) from its nock inside
    # the body, where it hides until the game nocks it; its own frame points +Y (Godot -Z).
    nock = Vector((0, 0, 0.2))
    arrow = Part("NockArrow", nock, rotation=Matrix.Rotation(math.pi / 2, 4, "X"))
    top = nock.z + NOCK_ARROW_LENGTH
    cap = (0.05, 0.05, 0.055)
    cap_c = Vector((0, 0, top - cap[2]))
    arrow.sphere(mats["PoisonCap"], cap_c, cap, cut=(cap_c + Vector((0, 0, -0.012)), Vector((0, 0, -1))))
    golden = math.pi * (3 - math.sqrt(5))
    for i in range(6):
        z = 0.3 + 0.6 * (i + 0.5) / 6
        r = math.sqrt(1 - z * z)
        n = Vector((math.cos(i * golden) * r, math.sin(i * golden) * r, z))
        p = cap_c + Vector((n.x * cap[0], n.y * cap[1], n.z * cap[2]))
        rot = n.to_track_quat("Z", "Y").to_matrix().to_4x4()
        arrow.sphere(mats["PoisonSpots"], p, (0.013, 0.013, 0.006), rotation=rot, segments=8, rings=4)
    arrow.capsule(mats["Stem"], (0, 0, top - cap[2] - 0.04), (0, 0, top - cap[2]), 0.024, segments=12)
    arrow.capsule(mats["Shaft"], nock, (0, 0, top - cap[2] - 0.04), 0.011, segments=8)
    for k in range(3):
        a = k * 2 * math.pi / 3
        rot = Matrix.Rotation(a, 4, "Z") @ Matrix.Rotation(math.pi / 2, 4, "Y")
        center = nock + Vector((0, 0, 0.07)) + (Matrix.Rotation(a, 4, "Z") @ Vector((0.028, 0, 0)))
        arrow.sphere(mats["Fletch"], center, (0.065, 0.006, 0.028), rotation=rot, segments=12, rings=6)
    arrow.build()

    out = os.path.join(HERE, f"{kind}.glb")
    bpy.ops.export_scene.gltf(filepath=out, export_format="GLB", export_apply=True,
                              export_yup=True, export_materials="EXPORT")
    print(f"characters.py: wrote {out}")


for kind in ("girl", "boy"):
    build(kind)
