# Builds the two player characters and exports them next to this script:
#   girl.glb  and  boy.glb
# Run from the repository root:
#   blender -b --factory-startup --python modules/Player/Models/characters.py
#
# People made of simple shapes, from Saria's drawings (2026-10-25), grown up and about 1.8 m tall
# (Tony, 2026-10-25: "they do have legs in the drawings", "make them look more normal"), with a
# floating head and floating round hands like Animal Crossing: a round head with large dark oval
# eyes floating about 5 cm above a smooth tapered torso (no neck, no visible arms), two round hands
# floating at the ends of invisible arms, two real legs (rounded tubes from the hips) in rounded
# shoes, and a bow carried on the back (its tips little mushroom-red caps). The Girl has a ribbon
# bow on her head, pigtail puffs and a pink white-spotted dress; the Boy a blue shirt with a red
# bow tie and trousers on his legs. Both share one modular rig (skin body parts) with the outfit
# and hair as separate child objects, so the game can swap them for the Wardrobe's hair styles and
# clothes (see build()).
#
# Meters, feet at the origin, facing Blender +Y (Godot's -Z, forward, after the glTF export).
# Separate objects, each with its pivot where it moves, so the game can animate them:
#   Body (pivot at the hips), Head (pivot at the neck), LegL / LegR (pivot at the hip: a skin leg,
#   the shoe and trouser leg its children, so they swing), ArmL / ArmR (pivot at the shoulder; empty, invisible), HandL / HandR (pivot at the hand's center; the game keeps them at the end of the
#   arms), BackBow, and for shooting: HandBow (the same bow without its string, which the game
#   draws; pivot at the grip) and NockArrow (the nocked arrow; pivot at its nock). HandBow rests
#   exactly where BackBow is; its rotation is the sling, so its own frame is the held bow's: grip
#   at the origin, tips up and down (Godot +-Y), arc bulging forward (Godot -Z), tips behind.
#   NockArrow's own frame points forward (Godot -Z) from the nock; at rest it stands upright,
#   hidden inside the left leg and the torso (the game hides it until it is nocked). Materials carry plain colors only; the game draws them toon-shaded
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

# The body, for a 1.80 m character (feet at 0, top of the head at 1.80); a character of another
# height is this rig scaled by height / 1.80. The game keeps ArcheryPose.cs, BowGeometry.cs and the
# Wardrobe's kit.py in step.
HEIGHT = 1.8
HIP_Z = 0.90                 # Body's pivot and the legs' (LegL / LegR at x -/+LEG_X)
LEG_X, LEG_R = 0.10, 0.065   # the legs: rounded tubes from the hips down into the shoes
ANKLE_Z = 0.09               # where a leg's tube ends, inside its shoe
# The shoes: rounded, about 0.12 m tall and 0.25 m long, a little ahead of the leg.
SHOE_C = (0.03, 0.05)        # center (y, z) under the leg
SHOE_R = (0.075, 0.125, 0.07)
# The torso: a smooth tapered shape with oval cross-sections from TORSO_BOTTOM to TORSO_TOP:
# hips about 0.34 m wide, waist 0.30, shoulders 0.40, about 0.22 m deep.
TORSO_BOTTOM, TORSO_TOP = 0.88, 1.44
TORSO_WIDTH = ((0.88, 0.15), (0.95, 0.17), (1.10, 0.15), (1.32, 0.20), (1.44, 0.185))  # (z, half width)
SHOULDER_Z, SHOULDER_X = 1.40, 0.22
# The head: its pivot at the neck, a round shape 0.27 m wide and 0.30 m tall centered at HEAD_Z,
# floating about 5 cm above the torso.
NECK_Z = 1.48
HEAD_Z = 1.64
HEAD_SIZE = (0.135, 0.13, 0.15)  # radii: across, front to back, up
# The face and hair are drawn on a head of radius 0.4 (the old chibi head, center DESIGN_C) and
# mapped onto the real one (head_space), so they keep their shapes at the new size.
DESIGN_R = 0.4
DESIGN_C = Vector((0, 0, 0.72 + DESIGN_R * 0.92))
DESIGN_SCALE = (HEAD_SIZE[0] / DESIGN_R, HEAD_SIZE[0] / DESIGN_R, HEAD_SIZE[2] / (DESIGN_R * 0.94))

# Shoulder to hand center; the game keeps ArcheryPose.cs in step.
ARM_LENGTH = 0.66
# The hands hang down and a little out and forward from the shoulders (ArcheryPose.ArmLRestDirection).
ARM_REST = (0.08, 0.03, -0.64)   # the right arm; the left one is its mirror (Blender: x right, y forward, z up)
HAND_R = 0.055
# The bow (slung on the back) is this much bigger than the old one, for the taller body; the held
# bow is HAND_BOW_SCALE times bigger again, so it reads in the hand: about as tall as from the hip
# to above the head.
BOW_SCALE = 1.1
HAND_BOW_SCALE = 1.3

# The nocked arrow, nock to tip; the game keeps CharacterModel.cs and ArcheryPose.cs in step.
NOCK_ARROW_LENGTH = 0.95

KINDS = {
    "girl": {
        "Hair": (0.45, 0.22, 0.14),
        "Cloth": (0.92, 0.3, 0.5),     # the dress
        "Ribbon": (0.95, 0.24, 0.36),   # the bow on her head
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

    def __init__(self, name, pivot, rotation=Matrix.Identity(4), space=None):
        """rotation: the object's rotation; shapes are still given in world coordinates. space: a
        world-to-world matrix every shape goes through first (head_space for the head's pieces)."""
        self.name = name
        self.pivot = Vector(pivot)
        self.rotation = rotation
        self.space = space
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
        if self.space is not None:
            matrix = Matrix.Translation(-self.pivot) @ self.space @ Matrix.Translation(self.pivot) @ matrix
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
    return Part(name, part.pivot, rotation=part.rotation, space=part.space)


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


def head_space():
    """The matrix taking a point drawn on the design head (radius DESIGN_R, center DESIGN_C) onto
    the real head (HEAD_SIZE, centered at HEAD_Z)."""
    return (Matrix.Translation((0, 0, HEAD_Z)) @ Matrix.Diagonal((*DESIGN_SCALE, 1.0)) @
            Matrix.Translation(-DESIGN_C))


def smooth(knots, z):
    """The value at z of a smooth curve through (z, value) knots (eased between them, no overshoot)."""
    if z <= knots[0][0]:
        return knots[0][1]
    for (z0, v0), (z1, v1) in zip(knots, knots[1:]):
        if z <= z1:
            t = (z - z0) / (z1 - z0)
            return v0 + (v1 - v0) * (3 - 2 * t) * t * t
    return knots[-1][1]


def torso_size(z):
    """The torso's half width and half depth at height z (zero at its rounded bottom and top)."""
    w = smooth(TORSO_WIDTH, z)
    for end, length in ((TORSO_BOTTOM, 0.05), (TORSO_TOP, 0.09)):
        u = abs(z - end) / length
        if u < 1:
            w *= math.sqrt(max(0.0, 1 - (1 - u) ** 2))
    return w, (0.06 + 0.3 * w) * (w / max(smooth(TORSO_WIDTH, z), 1e-6))


def torso_rings(out=0.0, z0=TORSO_BOTTOM, z1=TORSO_TOP):
    """The torso's outline as (half width, half depth, z) rings from z0 to z1, offset out by out
    (along the outline's normal); closed on the axis where it reaches the true bottom or top."""
    n = 80
    rings = []
    for i in range(n + 1):
        # Denser near the rounded ends.
        t = i / n
        z = TORSO_BOTTOM + (TORSO_TOP - TORSO_BOTTOM) * (0.5 - 0.5 * math.cos(math.pi * t))
        if z < z0 - 1e-6 or z > z1 + 1e-6:
            continue
        w, d = torso_size(z)
        dz = 1e-4
        slope = (torso_size(min(z + dz, TORSO_TOP))[0] - torso_size(max(z - dz, TORSO_BOTTOM))[0]) / (
            min(z + dz, TORSO_TOP) - max(z - dz, TORSO_BOTTOM))
        length = math.hypot(1.0, slope)
        nh, nz = 1 / length, -slope / length
        if w < 1e-6:
            nh, nz = 0.0, (-1.0 if z < (TORSO_BOTTOM + TORSO_TOP) / 2 else 1.0)
        rings.append((w + out * nh, d + out * nh, z + out * nz))
    for z in (z0, z1):
        if TORSO_BOTTOM + 1e-6 < z < TORSO_TOP - 1e-6 and all(abs(r[2] - z) > 1e-5 for r in rings):
            w, d = torso_size(z)
            rings.append((w + out, d + out, z))
    rings.sort(key=lambda r: r[2])
    return rings


def oval_lathe(part, mat, rings, cuts=(), segments=48):
    """A surface through oval rings (half width x, half depth y, z); a ring of size 0 is a point."""
    def make(g):
        verts = []
        for w, d, z in rings:
            if w < 1e-6:
                verts.append([g.verts.new((0, 0, z))])
            else:
                verts.append([g.verts.new((w * math.cos(i / segments * 2 * math.pi),
                                           d * math.sin(i / segments * 2 * math.pi), z)) for i in range(segments)])
        for a, b in zip(verts, verts[1:]):
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
    part._add(make, mat, Matrix.Translation(-part.pivot))


def torso_point(angle, z, out=0.0):
    """A point out from the torso's surface at a heading (degrees, 0 = front, +Y; 90 = +X) and
    height, and the surface's normal there."""
    w, d = torso_size(z)
    t = math.radians(angle)
    p = Vector((math.sin(t) * w, math.cos(t) * d, z))
    normal = Vector((math.sin(t) / max(w, 1e-6) * d, math.cos(t), 0)).normalized()
    return p + normal * out, normal


def strap_cuts(half_width):
    """The two planes that leave a slanted band (the bow's strap) round the torso."""
    n = Vector((math.sin(math.radians(35)), 0, math.cos(math.radians(35))))
    c = Vector((0, 0, 1.18))
    return ((c + n * half_width, n), (c - n * half_width, -n))


def spot_on_torso(part, mat, angle, z, size, out):
    """A flat round spot on the torso's cloth, angle degrees round from the front, at height z."""
    center, normal = torso_point(angle, z, out + 0.003)
    part.sphere(mat, center, (size, size, size * 0.3), rotation=normal.to_track_quat("Z", "Y").to_matrix().to_4x4(),
                segments=16, rings=8)


def dress_rings(out, hem_z, hem_flare):
    """A dress: the torso's outline from its top down to the hips, flaring below them over the
    legs' tops to a round hem at hem_z, hem_flare out from the hips."""
    rings = [r for r in torso_rings(out) if r[2] >= 0.97]
    w, d, _ = rings[0]
    for k in range(1, 9):
        t = k / 8
        z = 0.97 + (hem_z - 0.97) * t
        grow = hem_flare * t ** 1.4
        rings.insert(0, (w + grow, d + grow * 1.6, z))
    return rings


def build(kind):
    bpy.ops.wm.read_factory_settings(use_empty=True)
    mats = {name: material(name, color) for name, color in COLORS.items()}
    mats.update({name: material(name, color) for name, color in KINDS[kind].items()})
    girl = kind == "girl"

    # The face and hair are drawn on the design head (radius DESIGN_R at DESIGN_C) and mapped onto
    # the real one by head_space(), so they keep their shapes.
    head_r = DESIGN_R
    head_c = DESIGN_C

    # The modular rig, the same for both: skin parts (material "Skin", tinted with the chosen skin
    # tone in the game), dark eyes ("Eye", tinted with the eye color), and the default outfit and
    # hair as separate objects parented to their part, which the game hides when something else
    # is worn there: Hair, DefaultTop (the Girl's dress; the Boy's shirt and bow tie),
    # DefaultBottom* (the Boy's trousers: a waist band on Body, a leg on each of LegL / LegR),
    # DefaultShoes* (the shoes, on the legs), DefaultHead (the Girl's ribbon bow).

    # Head: round, large dark oval eyes with sparkles, rosy cheeks, a small smile, hair.
    head = Part("Head", (0, 0, NECK_Z), space=head_space())
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
    head_obj = head.build()
    # Hair: a cap over the top and back of the head (cut open over the face), with a fringe.
    hair = piece("Hair", head)
    hair.sphere(mats["Hair"], head_c + Vector((0, -0.02, 0.03)), (head_r * 1.07, head_r * 1.06, head_r * 1.04),
                segments=40, rings=24, cut=(head_c + Vector((0, 0.1, -0.1)), Vector((0, 0.85, -0.53))))
    for x in (-0.22, -0.08, 0.08, 0.22):
        hair.sphere(mats["Hair"], head_c + Vector((x, head_r * 0.8 - abs(x) * 0.35, 0.24 - abs(x) * 0.3)),
                    (0.1, 0.07, 0.075), segments=16, rings=8)
    if girl:
        # Two pigtail puffs, and a big ribbon bow on top.
        for side in (-1, 1):
            hair.sphere(mats["Hair"], head_c + Vector((side * 0.42, -0.08, -0.12)), (0.13, 0.13, 0.17), segments=16, rings=8)
        ribbon = piece("DefaultHead", head)
        ribbon_bow(ribbon, mats["Ribbon"], head_c + Vector((0.12, 0.05, head_r * 0.98)), 0.13)
        ribbon.build(head_obj)
    else:
        # A cowlick tuft.
        hair.sphere(mats["Hair"], head_c + Vector((0.05, 0.05, head_r * 1.0)), (0.05, 0.05, 0.1),
                    rotation=Matrix.Rotation(math.radians(25), 4, "Y"), segments=12, rings=6)
    hair.build(head_obj)

    # Body: a smooth tapered skin torso (hips, waist, shoulders; oval, deeper at the chest), a few
    # cm below the head (no neck), with the bow's strap across it. The outfit covers it.
    body = Part("Body", (0, 0, HIP_Z))
    oval_lathe(body, mats["Skin"], torso_rings())
    oval_lathe(body, mats["Wood"], torso_rings(0.017, 0.96, 1.42), cuts=strap_cuts(0.028))
    body_obj = body.build()
    top = piece("DefaultTop", body)
    if girl:
        # The dress: the torso's shape down to the hips, flaring over the legs' tops to a hem at
        # about 0.62 m, with white spots like a mushroom cap.
        oval_lathe(top, mats["Cloth"], dress_rings(0.01, 0.62, 0.12))
        for a, z in ((5, 1.33), (-60, 1.30), (70, 1.28), (130, 1.32), (-140, 1.31), (180, 1.27),
                     (30, 1.16), (-35, 1.14), (100, 1.17), (-100, 1.18), (160, 1.15), (-170, 1.12)):
            spot_on_torso(top, mats["Spots"], a, z, 0.03, 0.01)
        for k in range(10):
            a = k / 10 * 360 + 18
            z = 0.72 + 0.1 * (k % 2)
            t = math.radians(a)
            grow = 0.12 * ((z - 0.97) / (0.62 - 0.97)) ** 1.4
            w, d = torso_size(0.97)
            w, d = w + 0.01 + grow, d + 0.01 + grow * 1.6
            normal = Vector((math.sin(t) * d / w, math.cos(t), -0.35)).normalized()
            c = Vector((math.sin(t) * w, math.cos(t) * d, z)) + normal * 0.004
            top.sphere(mats["Spots"], c, (0.035, 0.035, 0.01), rotation=normal.to_track_quat("Z", "Y").to_matrix().to_4x4(),
                       segments=16, rings=8)
    else:
        # The shirt covers the torso down over the waist band, with a bow tie at the top and two buttons.
        oval_lathe(top, mats["Cloth"], torso_rings(0.01, 0.97))
        ribbon_bow(top, mats["Ribbon"], torso_point(0, 1.36, 0.022)[0], 0.045)
        for z in (1.28, 1.18):
            spot_on_torso(top, mats["Spots"], 0, z, 0.014, 0.01)
    top.build(body_obj)
    if not girl:
        # The trousers' waist band, over the torso's bottom.
        band = piece("DefaultBottom", body)
        oval_lathe(band, mats["Legs"], torso_rings(0.018, TORSO_BOTTOM, 1.02))
        band.build(body_obj)

    # Legs: rounded skin tubes from the hips down into the shoes, each its own object at the hip's
    # pivot; the shoe (and the Boy's trouser leg) is its child, so they swing as the game turns it.
    for side, name in ((-1, "LegL"), (1, "LegR")):
        x = side * LEG_X
        leg = Part(name, (x, 0, HIP_Z))
        leg.capsule(mats["Skin"], (x, 0, HIP_Z), (x, 0, ANKLE_Z), LEG_R, segments=24)
        leg_obj = leg.build()
        shoe = piece("DefaultShoes" + name, leg)
        shoe.sphere(mats["Shoe"], (x, SHOE_C[0], SHOE_C[1]), SHOE_R, segments=24, rings=12, cut=((x, 0, 0), (0, 0, -1)))
        shoe.build(leg_obj)
        if not girl:
            trousers = piece("DefaultBottom" + name, leg)
            trousers.capsule(mats["Legs"], (x, 0, HIP_Z + 0.02), (x, 0, 0.16), LEG_R + 0.014, segments=24)
            trousers.build(leg_obj)

    # Arms: no meshes, only invisible pivots at the shoulders; the round hands float at the ends of
    # the arms (ARM_LENGTH from the shoulder, hanging down beside the hips), where the game keeps
    # them: it only turns the arms at the shoulder (ArcheryPose.ArmLength, ShoulderLRest, ArmLRestDirection).
    for side, name in ((-1, "ArmL"), (1, "ArmR")):
        shoulder = Vector((side * SHOULDER_X, 0, SHOULDER_Z))
        empty(name, shoulder)
        hand = shoulder + Vector((side * ARM_REST[0], ARM_REST[1], ARM_REST[2])).normalized() * ARM_LENGTH
        hand_part = Part("Hand" + name[-1], hand)
        hand_part.sphere(mats["Skin"], hand, (HAND_R, HAND_R * 0.96, HAND_R * 1.02), segments=24, rings=12)
        hand_part.build()

    # A wooden bow carried on the back, slung diagonally: the middle of its arc rests against the
    # back, its tips and string stick out behind.
    # HandBow is the same bow without the string (the game draws it, bent by the draw), with the
    # sling as its rotation: its own frame is the arc frame below, Godot's held-bow frame.
    grip = (0, -0.17, 1.16)
    major, half_arc = 0.42 * BOW_SCALE, 0.34 * math.pi
    tip_r = (0.045 * BOW_SCALE, 0.045 * BOW_SCALE, 0.04 * BOW_SCALE)
    wood_r = 0.028 * BOW_SCALE
    # Arc frame: local x is depth (the arc's middle at x = 0, tips toward -x), local y the span.
    frame = Matrix(((0, 0, 1, 0), (1, 0, 0, 0), (0, 1, 0, 0), (0, 0, 0, 1)))  # x->+Y, y->+Z, z->+X
    slant = Matrix.Rotation(math.radians(38), 4, "Y")
    sling = slant @ frame
    bow = Part("BackBow", grip)
    bow.torus(mats["Wood"], bow.pivot, major, wood_r,
              rotation=sling @ Matrix.Translation((-major, 0, 0)), arc=half_arc / math.pi)
    tips = []
    for sign in (-1, 1):
        local = Vector((major * math.cos(half_arc) - major, sign * major * math.sin(half_arc), 0, 1))
        tips.append(bow.pivot + (sling @ local).to_3d())
    bow.capsule(mats["String"], tips[0], tips[1], 0.009, segments=6)
    for tip in tips:
        bow.sphere(mats["MushroomRed"], tip, tip_r, segments=12, rings=6)
    bow.build()
    # HandBow: HAND_BOW_SCALE times the size, a grip wrap at its pivot, and its two limbs as child
    # objects at the same pivot (HandBowLimbTop / HandBowLimbBottom), which the game bends back
    # with the draw (turning them about the grip's X axis, never stretching them).
    k = HAND_BOW_SCALE
    hmajor = major * k
    held = Part("HandBow", grip, rotation=slant)
    wrap_a = held.pivot + (sling @ Vector((0, -0.06 * BOW_SCALE, 0, 1))).to_3d() - held.pivot
    wrap_b = held.pivot + (sling @ Vector((0, 0.06 * BOW_SCALE, 0, 1))).to_3d() - held.pivot
    held.capsule(mats["Ribbon"], held.pivot + wrap_a, held.pivot + wrap_b, 0.04 * BOW_SCALE * k, segments=12)
    held_obj = held.build()
    for sign, name in ((1, "HandBowLimbTop"), (-1, "HandBowLimbBottom")):
        limb = piece(name, held)
        limb.torus(mats["Wood"], limb.pivot, hmajor, wood_r * k,
                   rotation=sling @ Matrix.Translation((-hmajor, 0, 0)) @ Matrix.Rotation(sign * half_arc / 2, 4, "Z"),
                   arc=half_arc / (2 * math.pi), segments=120)
        local = Vector((hmajor * math.cos(half_arc) - hmajor, sign * hmajor * math.sin(half_arc), 0, 1))
        limb.sphere(mats["MushroomRed"], limb.pivot + (sling @ local).to_3d(), tuple(r * k for r in tip_r), segments=12, rings=6)
        limb.build(held_obj)

    # The nocked arrow: a poison mushroom tip (a purple cap with glowing green spots on a cream
    # stem), a wooden shaft and soft green fletching. Built pointing up (+Z) from its nock inside
    # the body, where it hides until the game nocks it; its own frame points +Y (Godot -Z).
    nock = Vector((-LEG_X, 0, 0.42))
    arrow = Part("NockArrow", nock, rotation=Matrix.Rotation(math.pi / 2, 4, "X"))
    top = nock.z + NOCK_ARROW_LENGTH
    cap = (0.05, 0.05, 0.055)
    cap_c = Vector((nock.x, 0, top - cap[2]))
    arrow.sphere(mats["PoisonCap"], cap_c, cap, cut=(cap_c + Vector((0, 0, -0.012)), Vector((0, 0, -1))))
    golden = math.pi * (3 - math.sqrt(5))
    for i in range(6):
        z = 0.3 + 0.6 * (i + 0.5) / 6
        r = math.sqrt(1 - z * z)
        n = Vector((math.cos(i * golden) * r, math.sin(i * golden) * r, z))
        p = cap_c + Vector((n.x * cap[0], n.y * cap[1], n.z * cap[2]))
        rot = n.to_track_quat("Z", "Y").to_matrix().to_4x4()
        arrow.sphere(mats["PoisonSpots"], p, (0.013, 0.013, 0.006), rotation=rot, segments=8, rings=4)
    arrow.capsule(mats["Stem"], (nock.x, 0, top - cap[2] - 0.04), (nock.x, 0, top - cap[2]), 0.024, segments=12)
    arrow.capsule(mats["Shaft"], nock, (nock.x, 0, top - cap[2] - 0.04), 0.011, segments=8)
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
