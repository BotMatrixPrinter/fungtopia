# Builds the two player characters and exports them next to this script:
#   girl.glb  and  boy.glb
# Run from the repository root:
#   blender -b --factory-startup --python modules/Player/Models/characters.py
#
# Simple clean shapes, from Saria's drawing "you choose between these" (girl and boy; Tony,
# 2026-10-26: follow it closely, keep the height, no mouth, no hair): about 1.47 m tall, the head a
# quarter of it, the body half, the legs a quarter. A big plain round ball of a head (0.38 m across,
# its top at 1.47 m) floating 4 cm above the body (no neck) with only two big dark oval eyes with
# white sparkles, set low on the face and wide apart, and small soft rosy cheeks: no mouth, no nose,
# no hair. The Girl's body is a tall cone (a triangle from the front: her pink spotted dress, 0.4 m
# wide at the bottom, narrow and round at the top), the Boy's a tall rounded capsule 0.32 m wide (his
# blue shirt) with a big red bow tie at its top, two buttons and a band of trousers round its
# bottom. Big oval hands (0.14 x 0.18 m) with three finger lines float well out beside the body at
# about its middle (no arms); two plain straight tube legs come out from under the body down to
# rounded feet (the shoes). A bow is carried on the back (its tips little mushroom-red caps). Both
# share one modular rig (skin body parts) with the outfit as separate child objects, so the game can
# swap them for the Wardrobe's clothes (see build()); hair styles are the Wardrobe's.
#
# Meters, feet at the origin, facing Blender +Y (Godot's -Z, forward, after the glTF export).
# Separate objects, each with its pivot where it moves, so the game can animate them:
#   Body, Head (pivot at the neck), LegL / LegR (pivot at the hip; the tube leg, the foot hangs
#   from it), ArmL / ArmR (pivot at the shoulder; empty, invisible), HandL / HandR (pivot at the hand's center; the game keeps them at the end of the
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
    "Finger": (0.62, 0.3, 0.26),
    "Wood": (0.62, 0.38, 0.2),
    "String": (0.95, 0.92, 0.82),
    "Shoe": (0.42, 0.24, 0.18),
    # Mushrooms, "vivid" (Tony, 2026-10-25; the game keeps Core/Mushrooms.cs in step): the bow's
    # tomato-red tips, and the nocked arrow's poison mushroom tip (like the Bow module's
    # arrow.glb): a rich purple cap, glowing green spots, a clean cream stem.
    "MushroomRed": (0.96, 0.1, 0.06),
    "PoisonCap": (0.62, 0.12, 0.95),
    "PoisonSpots": (0.5, 1.0, 0.25),
    "Stem": (1.0, 0.96, 0.86),
    "Shaft": (0.78, 0.6, 0.38),
    "Fletch": (0.5, 0.85, 0.45),
}

# Shoulder to hand center; the game keeps ArcheryPose.cs in step.
ARM_LENGTH = 0.46
# The held bow is this much bigger than the slung one, so it reads in the hand.
HAND_BOW_SCALE = 1.3

# Heights (meters): the hips (the body's and the legs' pivots), the neck (the head's pivot), the
# head (a ball of HEAD_R round HEAD_Z: its top at 1.47 m, its bottom 4 cm above the body's top).
HIP_Z = 0.42
NECK_Z = 1.07
HEAD_R, HEAD_Z = 0.19, 1.28
# The body, a solid of revolution from BODY_BOTTOM (radius R0) to BODY_TOP (radius R1), its bottom
# rim and its top rounded off by ROUND (bottom, top): the Girl's a cone (a triangle from the front),
# the Boy's a capsule.
BODY_BOTTOM, BODY_TOP = 0.36, 1.05
BODIES = {
    "girl": {"R0": 0.21, "R1": 0.035, "ROUND": (0.05, 0.09)},
    "boy": {"R0": 0.16, "R1": 0.16, "ROUND": (0.08, 0.14)},
}
BODY = BODIES["girl"]
# The legs: straight tubes (radius LEG_R) from the hips (LEG_X out) down into the rounded feet.
LEG_X, LEG_R = 0.08, 0.06
FOOT_C = (0.025, 0.056)          # the foot's center, forward (Blender y) and up, under its leg
FOOT_SIZE = (0.075, 0.1, 0.056)  # its radii: a little wider than the leg
# The hands: ovals (radii) at the ends of the invisible arms from the shoulders.
HAND_SIZE = (0.07, 0.055, 0.09)
SHOULDER = (0.06, NECK_Z - 0.01)
HAND_REACH = (0.273, 0.03, -0.373)  # shoulder to hand, outward (x), forward, up; ARM_LENGTH long
# The Boy's trousers: a band round the bottom of the body, up to here.
TROUSERS_TOP = 0.52

# The nocked arrow, nock to tip; the game keeps CharacterModel.cs and ArcheryPose.cs in step.
NOCK_ARROW_LENGTH = 0.78

KINDS = {
    "girl": {
        "Cloth": (0.92, 0.3, 0.5),     # the dress
        "Ribbon": (0.95, 0.24, 0.36),   # the bow on her head
        "Spots": (1.0, 0.96, 0.9),
    },
    "boy": {
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

    def lathe(self, mat, profile, cuts=(), segments=48):
        """A solid of revolution about the world Z axis through the origin: profile is its outline as
        (radius, z) points, starting and ending on the axis. cuts = (point, normal) planes, each
        removing the part on its normal's side (world coordinates)."""
        def make(g):
            rings = []
            for r, z in profile:
                if r < 1e-6:
                    rings.append([g.verts.new((0, 0, z))])
                else:
                    rings.append([g.verts.new((r * math.cos(i / segments * 2 * math.pi),
                                               r * math.sin(i / segments * 2 * math.pi), z)) for i in range(segments)])
            for a, b in zip(rings, rings[1:]):
                for i in range(segments):
                    j = (i + 1) % segments
                    if len(a) == 1 and len(b) > 1:
                        g.faces.new((a[0], b[j], b[i]))
                    elif len(b) == 1 and len(a) > 1:
                        g.faces.new((a[i], a[j], b[0]))
                    elif len(a) > 1:
                        g.faces.new((a[i], a[j], b[j], b[i]))
            bmesh.ops.recalc_face_normals(g, faces=g.faces)
            for point, normal in cuts:
                bmesh.ops.bisect_plane(g, geom=g.verts[:] + g.edges[:] + g.faces[:],
                                       plane_co=Vector(point), plane_no=Vector(normal), clear_outer=True)
        self._add(make, mat, Matrix.Translation(-self.pivot))

    def build(self, parent=None):
        """The object; with a parent (a built part with the same pivot) it is its child at no offset."""
        obj = new_mesh_object(self.name, self.bm)
        for mat in self.materials:
            obj.data.materials.append(mat)
        obj.location = self.pivot
        obj.rotation_euler = self.rotation.to_euler()
        if parent is not None:
            # Same pivot and rotation as the parent: no offset from it.
            obj.parent = parent
            obj.location = (0, 0, 0)
            obj.rotation_euler = (0, 0, 0)
        return obj


def piece(name, part):
    """A built-in piece of the default outfit (or the hair) on a part: same pivot, its own object."""
    return Part(name, part.pivot, rotation=part.rotation)


def ribbon_bow(part, mat, center, size):
    """A ribbon bow: two puffy loops and a knot, spanning x."""
    cx, cy, cz = center
    for side in (-1, 1):
        part.sphere(mat, (cx + side * size * 0.9, cy, cz + size * 0.1),
                    (size * 0.85, size * 0.45, size * 0.6),
                    rotation=Matrix.Rotation(math.radians(-18 * side), 4, "Y"))
    part.sphere(mat, (cx, cy + size * 0.12, cz), (size * 0.38, size * 0.4, size * 0.38))


def empty(name, pivot):
    """An object without a mesh: only a pivot the game turns (and children hang from)."""
    obj = bpy.data.objects.new(name, None)
    bpy.context.scene.collection.objects.link(obj)
    obj.location = pivot
    return obj


def rounded(corners, radii, steps=6):
    """The polyline through corners, each corner cut round (a quadratic curve) radii[i] along its edges."""
    points = [corners[0]]
    for i in range(1, len(corners) - 1):
        c, d = Vector(corners[i]), radii[i]
        if d <= 0:
            points.append(corners[i])
            continue
        a = c + (Vector(corners[i - 1]) - c).normalized() * d
        b = c + (Vector(corners[i + 1]) - c).normalized() * d
        for k in range(steps + 1):
            t = k / steps
            p = a * (1 - t) ** 2 + c * 2 * t * (1 - t) + b * t * t
            points.append((p.x, p.y))
    points.append(corners[-1])
    return points


def body_radius(z):
    """The body's radius at height z (its straight side, before the rounding)."""
    return BODY["R0"] + (BODY["R1"] - BODY["R0"]) * (z - BODY_BOTTOM) / (BODY_TOP - BODY_BOTTOM)


def body_profile(offset=0.0, z0=BODY_BOTTOM, z1=BODY_TOP):
    """The outline of the body (offset out by offset, for the clothes over it) from z0 to z1;
    rounded at its true bottom rim and top, flat where it is cut off."""
    bottom, top = z0 <= BODY_BOTTOM, z1 >= BODY_TOP
    z0, z1 = (z0 - offset if bottom else z0), (z1 + offset if top else z1)
    corners = [(0, z0), (body_radius(z0) + offset, z0), (body_radius(z1) + offset, z1), (0, z1)]
    points = rounded(corners, [0, BODY["ROUND"][0] if bottom else 0, BODY["ROUND"][1] if top else 0, 0], steps=8)
    # Rings every few cm up the straight side, for smooth shading (and the toon outline).
    dense = [points[0]]
    for p, q in zip(points, points[1:]):
        n = max(1, int(math.dist(p, q) / 0.03))
        dense += [(p[0] + (q[0] - p[0]) * k / n, p[1] + (q[1] - p[1]) * k / n) for k in range(1, n + 1)]
    return dense


def strap_cuts(half_width):
    """The two planes that leave a slanted band (the bow's strap) round the body."""
    n = Vector((math.sin(math.radians(35)), 0, math.cos(math.radians(35))))
    c = Vector((0, 0, 0.74))
    return ((c + n * half_width, n), (c - n * half_width, -n))


def spot_on_body(part, mat, angle, z, size):
    """A flat round spot on the body's cloth, angle degrees round from the front, at height z."""
    t = math.radians(angle + 90)
    normal = Vector((math.cos(t), math.sin(t), (BODY["R0"] - BODY["R1"]) / (BODY_TOP - BODY_BOTTOM))).normalized()
    r = body_radius(z) + 0.008
    center = Vector((math.cos(t) * r, math.sin(t) * r, z)) + normal * 0.003
    part.sphere(mat, center, (size, size, size * 0.3), rotation=normal.to_track_quat("Z", "Y").to_matrix().to_4x4(),
                segments=16, rings=8)


def finger_lines(part, mat, center, size, count=3):
    """Short dark finger lines down the front of an oval hand's top (grooves between the fingers)."""
    for k in range(count):
        x = (k - (count - 1) / 2) * size[0] * 0.38
        points = []
        for j in range(6):
            z = size[2] * (0.25 + 0.5 * j / 5)
            depth = 1 - (x / size[0]) ** 2 - (z / size[2]) ** 2
            points.append(Vector(center) + Vector((x, size[1] * math.sqrt(max(depth, 0.0)) + 0.001, z)))
        for p, q in zip(points, points[1:]):
            part.capsule(mat, p, q, 0.0055, segments=8)


def build(kind):
    global BODY
    bpy.ops.wm.read_factory_settings(use_empty=True)
    mats = {name: material(name, color) for name, color in COLORS.items()}
    mats.update({name: material(name, color) for name, color in KINDS[kind].items()})
    girl = kind == "girl"
    BODY = BODIES[kind]
    hip_z, neck_z = HIP_Z, NECK_Z
    head_c = Vector((0, 0, HEAD_Z))

    # The modular rig, the same for both: skin parts (material "Skin", tinted with the chosen skin
    # tone in the game), dark eyes ("Eye", tinted with the eye color), and the default outfit as
    # separate objects parented to their part, which the game hides when something else is worn
    # there: DefaultTop (the Girl's dress; the Boy's shirt and bow tie), DefaultBottom (the Boy's
    # trousers), DefaultShoes* (the rounded feet), DefaultHead (the Girl's ribbon bow). No hair:
    # the Wardrobe's hair styles go on the bald head.

    # Head: a big plain round ball with two big dark oval eyes (each with a white sparkle), set low
    # on the face and wide apart, and small soft rosy cheeks. No mouth, no nose, no hair.
    head = Part("Head", (0, 0, neck_z))
    head.sphere(mats["Skin"], head_c, (HEAD_R, HEAD_R, HEAD_R), segments=48, rings=28)
    for side in (-1, 1):
        n = Vector((side * 0.42, 0.89, -0.17)).normalized()
        face = n.to_track_quat("Y", "Z").to_matrix().to_4x4()
        up = (face @ Vector((0, 0, 1, 0))).to_3d()
        across = (face @ Vector((1, 0, 0, 0))).to_3d()
        eye_c = head_c + n * HEAD_R * 0.9
        head.sphere(mats["Eye"], eye_c, (0.034, 0.03, 0.05), rotation=face, segments=24, rings=12)
        head.sphere(mats["EyeShine"], eye_c + n * 0.022 + up * 0.02 - across * side * 0.008, (0.012, 0.008, 0.015),
                    rotation=face, segments=12, rings=6)
        head.sphere(mats["EyeShine"], eye_c + n * 0.025 - up * 0.017 + across * side * 0.011, (0.006, 0.005, 0.006),
                    rotation=face, segments=10, rings=5)
        cheek = Vector((side * 0.68, 0.66, -0.33)).normalized()
        head.sphere(mats["Blush"], head_c + cheek * HEAD_R * 0.965, (0.028, 0.01, 0.016),
                    rotation=cheek.to_track_quat("Y", "Z").to_matrix().to_4x4(), segments=16, rings=8)
    head_obj = head.build()
    if girl:
        # Her big ribbon bow on top of the round head.
        ribbon = piece("DefaultHead", head)
        ribbon_bow(ribbon, mats["Ribbon"], head_c + Vector((0.05, 0.02, HEAD_R * 0.97)), 0.065)
        ribbon.build(head_obj)

    # Body: a smooth skin shape (the Girl's cone, the Boy's capsule) under the head, the legs coming
    # out from under it, with the bow's strap across it. The outfit colors it (a slightly bigger
    # shape over it).
    body = Part("Body", (0, 0, hip_z))
    body.lathe(mats["Skin"], body_profile())
    body.lathe(mats["Wood"], body_profile(0.012, BODY_BOTTOM + 0.12, BODY_TOP - 0.06), cuts=strap_cuts(0.035))
    body_obj = body.build()
    top = piece("DefaultTop", body)
    if girl:
        # The dress colors the whole triangle, with white spots like a mushroom cap.
        top.lathe(mats["Cloth"], body_profile(0.008))
        for a, z in ((20, 0.44), (-40, 0.42), (75, 0.47), (-95, 0.44), (140, 0.45), (-150, 0.46), (180, 0.41),
                     (5, 0.6), (-70, 0.58), (110, 0.6), (-130, 0.62), (40, 0.75), (-15, 0.78), (160, 0.74), (-100, 0.73)):
            spot_on_body(top, mats["Spots"], a, z, 0.035)
    else:
        # The shirt colors the capsule above the trousers, with a big bow tie at the top (about as
        # wide as the body) and two buttons.
        top.lathe(mats["Cloth"], body_profile(0.008, TROUSERS_TOP - 0.03, BODY_TOP + 0.008))
        ribbon_bow(top, mats["Ribbon"], (0, BODY["R0"] + 0.025, 0.93), 0.095)
        for z in (0.8, 0.68):
            spot_on_body(top, mats["Spots"], 0, z, 0.022)
    top.build(body_obj)
    if not girl:
        # The trousers: a band round the bottom of the capsule.
        trousers = piece("DefaultBottom", body)
        trousers.lathe(mats["Legs"], body_profile(0.016, BODY_BOTTOM - 0.008, TROUSERS_TOP))
        trousers.build(body_obj)

    # Legs: plain straight skin tubes from the hips (inside the body) down to the rounded feet
    # (the shoes), which hang from them, so they swing as the game turns the legs.
    for side, name in ((-1, "LegL"), (1, "LegR")):
        x = side * LEG_X
        leg = Part(name, (x, 0, hip_z))
        leg.capsule(mats["Skin"], (x, 0, hip_z + 0.02), (x, 0, FOOT_C[1] + 0.02), LEG_R, segments=24)
        leg_obj = leg.build()
        shoe = piece("DefaultShoes" + name, leg)
        shoe.sphere(mats["Shoe"], (x, FOOT_C[0], FOOT_C[1]), FOOT_SIZE, segments=24, rings=12)
        shoe.build(leg_obj)

    # Arms: no meshes, only invisible pivots at the shoulders; the big oval hands float well out
    # beside the body, about its middle, at the ends of the arms (ARM_LENGTH from the shoulder),
    # where the game keeps them: it only turns the arms at the shoulder (ArcheryPose.ArmLength,
    # ShoulderLRest, ArmLRestDirection). Three finger lines down the front of each.
    for side, name in ((-1, "ArmL"), (1, "ArmR")):
        shoulder = Vector((side * SHOULDER[0], 0, SHOULDER[1]))
        empty(name, shoulder)
        hand = shoulder + Vector((side * HAND_REACH[0], HAND_REACH[1], HAND_REACH[2])).normalized() * ARM_LENGTH
        hand_part = Part("Hand" + name[-1], hand)
        hand_part.sphere(mats["Skin"], hand, HAND_SIZE, segments=32, rings=16)
        finger_lines(hand_part, mats["Finger"], hand, HAND_SIZE)
        hand_part.build()

    # A little wooden bow carried on the back, slung diagonally: the middle of its arc rests
    # against the back, its tips and string stick out behind.
    # HandBow is the same bow without the string (the game draws it, bent by the draw), with the
    # sling as its rotation: its own frame is the arc frame below, Godot's held-bow frame.
    grip = (0, -(body_radius(0.74) + 0.035), 0.74)
    major, half_arc = 0.42, 0.34 * math.pi
    # Arc frame: local x is depth (the arc's middle at x = 0, tips toward -x), local y the span.
    frame = Matrix(((0, 0, 1, 0), (1, 0, 0, 0), (0, 1, 0, 0), (0, 0, 0, 1)))  # x->+Y, y->+Z, z->+X
    slant = Matrix.Rotation(math.radians(38), 4, "Y")
    sling = slant @ frame
    bow = Part("BackBow", grip)
    bow.torus(mats["Wood"], bow.pivot, major, 0.028,
              rotation=sling @ Matrix.Translation((-major, 0, 0)), arc=half_arc / math.pi)
    tips = []
    for sign in (-1, 1):
        local = Vector((major * math.cos(half_arc) - major, sign * major * math.sin(half_arc), 0, 1))
        tips.append(bow.pivot + (sling @ local).to_3d())
    bow.capsule(mats["String"], tips[0], tips[1], 0.009, segments=6)
    for tip in tips:
        bow.sphere(mats["MushroomRed"], tip, (0.045, 0.045, 0.04), segments=12, rings=6)
    bow.build()
    # HandBow: HAND_BOW_SCALE times the size, a grip wrap at its pivot, and its two limbs as child
    # objects at the same pivot (HandBowLimbTop / HandBowLimbBottom), which the game bends back
    # with the draw (turning them about the grip's X axis, never stretching them).
    k = HAND_BOW_SCALE
    hmajor = major * k
    held = Part("HandBow", grip, rotation=slant)
    wrap_a = held.pivot + (sling @ Vector((0, -0.06, 0, 1))).to_3d() - held.pivot
    wrap_b = held.pivot + (sling @ Vector((0, 0.06, 0, 1))).to_3d() - held.pivot
    held.capsule(mats["Ribbon"], held.pivot + wrap_a, held.pivot + wrap_b, 0.04 * k, segments=12)
    held_obj = held.build()
    for sign, name in ((1, "HandBowLimbTop"), (-1, "HandBowLimbBottom")):
        limb = piece(name, held)
        limb.torus(mats["Wood"], limb.pivot, hmajor, 0.028 * k,
                   rotation=sling @ Matrix.Translation((-hmajor, 0, 0)) @ Matrix.Rotation(sign * half_arc / 2, 4, "Z"),
                   arc=half_arc / (2 * math.pi), segments=120)
        local = Vector((hmajor * math.cos(half_arc) - hmajor, sign * hmajor * math.sin(half_arc), 0, 1))
        limb.sphere(mats["MushroomRed"], limb.pivot + (sling @ local).to_3d(), (0.045 * k, 0.045 * k, 0.04 * k), segments=12, rings=6)
        limb.build(held_obj)

    # The nocked arrow: a poison mushroom tip (a purple cap with glowing green spots on a cream
    # stem), a wooden shaft and soft green fletching. Built pointing up (+Z) from its nock inside
    # the body, where it hides until the game nocks it; its own frame points +Y (Godot -Z).
    nock = Vector((0, 0, 0.3))
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
