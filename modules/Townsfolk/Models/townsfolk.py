# Builds Pet City's background life and exports it next to this script:
#   folk.glb  cat.glb  dog.glb  crow.glb  butterfly.glb
# Run from the repository root:
#   blender -b --factory-startup --python modules/Townsfolk/Models/townsfolk.py
# or only some of them:
#   blender -b --factory-startup --python modules/Townsfolk/Models/townsfolk.py -- folk crow
#
# folk.glb is one base townsperson in the style of Saria's drawing ("you choose between these";
# Tony, 2026-10-26: "too elongated, the torso should be somewhat shorter and more rounded at the
# base, and the legs should be further apart but still within the base of the torso"), 1.30 m tall,
# built of a few clean simple shapes: a big plain round head (0.38 m across, its top at 1.30 m)
# floating 4 cm over the body with no neck, only two big dark oval eyes with white sparkles set low
# and wide on it and small soft rosy cheeks (no mouth, nose, ears or hair); a short body (0.36 to
# 0.88 m) round at the base, either Dress (a triangle from the front, 0.42 m wide low down, with a
# fully rounded, softly curved bottom) or Capsule (a soft egg: 0.28 m wide near the top, widest,
# 0.38 m, low down, curving round underneath); two big oval hands (0.18 m tall, 0.14 m wide, three
# finger grooves each) floating well out beside the body at its middle height, no arms; two plain
# tube legs (0.06 m radius), wide apart, from under the base (inside its outline seen from the
# front) to rounded feet. It carries every dress-up option as its own object, and the game shows one of
# each and tints them per townsperson: Dress or Capsule and BowTie (a big bow tie at the capsule's
# top), children of Body; the mushroom-cap hats HatDome, HatFlat, HatTall and Bow (a bow on the
# head), children of Head. Tinted materials: Skin, Outfit, Legs, Bow, Hat.
#
# cat.glb, dog.glb and crow.glb are simpler background Mushroom Cats, Dogs and Crows (tinted Fur,
# FurLight, FurDark, Feather), butterfly.glb a little butterfly (tinted Wing). Only heads have eyes;
# hats and objects never do.
#
# Meters, feet at the origin, facing Blender +Y (Godot's -Z, forward, after the glTF export).
# Separate objects with pivots where they move, so the game can animate them: Body (hip; empty, the
# body shapes hang from it), Head (its bottom), LegL / LegR (the thighs, from the hip) with ShinL /
# ShinR (from the knee, down to the foot), ArmL / ArmR (shoulder; empty, invisible), HandL / HandR
# (hand center). The leg rig's heights are Core/Stride.cs's. Pets: Body, Head (neck), LegFL / LegFR / LegBL /
# LegBR (shoulder or hip), Tail (root); the crow WingL / WingR (shoulder), LegL / LegR (hip), Tail.
# The butterfly: Body, WingL / WingR (on the body's axis). Materials carry plain colors only; the
# game draws them toon-shaded with ink outlines.
import math
import os
import sys

import bmesh
import bpy
from mathutils import Matrix, Vector

HERE = os.path.dirname(os.path.abspath(__file__))

COLORS = {
    # Tinted per townsperson / pet by the game; these are the defaults.
    "Skin": (0.98, 0.68, 0.52),
    "Outfit": (0.4, 0.6, 0.92),
    "Legs": (0.28, 0.32, 0.52),
    "Bow": (0.9, 0.2, 0.25),
    "Hat": (0.9, 0.22, 0.22),
    "Fur": (1.0, 0.58, 0.25),
    "FurLight": (1.0, 0.93, 0.82),
    "FurDark": (0.6, 0.34, 0.18),
    "Feather": (0.18, 0.16, 0.3),
    "Wing": (1.0, 0.55, 0.75),
    # Fixed.
    "Blush": (1.0, 0.45, 0.48),
    "Eye": (0.07, 0.04, 0.06),
    "EyeShine": (1.0, 1.0, 1.0),
    "Finger": (0.62, 0.36, 0.3),
    "HatSpots": (1.0, 0.96, 0.88),
    "Gills": (0.98, 0.9, 0.78),
    "Nose": (0.95, 0.5, 0.55),
    "Cap": (0.9, 0.22, 0.22),
    "Spots": (1.0, 0.96, 0.9),
    "Stem": (0.98, 0.92, 0.8),
    "Collar": (0.25, 0.55, 0.95),
    "Beak": (1.0, 0.66, 0.2),
    "Leg": (1.0, 0.62, 0.22),
    "BodyDark": (0.2, 0.14, 0.18),
    "WingSpot": (1.0, 0.97, 0.85),
}

# The townsperson, at 1.30 m (Look.ModelHeight); the leg rig as Core/Stride.cs.
HEIGHT = 1.30
HEAD_R = 0.19
HEAD_C = Vector((0, 0, HEIGHT - HEAD_R))
NECK_Z = HEAD_C.z - HEAD_R
HIP_Z, KNEE_Z, ANKLE_Z = 0.42, 0.22, 0.07
LEG_R, LEG_X = 0.06, 0.11
# The two bodies, smooth solids of revolution from BODY_BOTTOM to BODY_TOP (4 cm under the head):
# their outlines as (radius, z) points from the axis under the round bottom to the axis over the
# top, smoothed through them (as the Player's characters.py). Dress: a triangle from the front,
# narrow and round at the top, 0.42 m wide low down, with a fully rounded, softly curved bottom.
# Capsule: a soft egg, 0.28 m wide near the top, widest (0.38 m) low down, curving round underneath.
BODY_BOTTOM, BODY_TOP = 0.36, 0.88
DRESS = [(0, 0.36), (0.07, 0.362), (0.125, 0.37), (0.168, 0.386), (0.197, 0.41), (0.21, 0.44),
         (0.2, 0.48), (0.165, 0.57), (0.128, 0.665), (0.092, 0.76), (0.064, 0.828), (0.042, 0.862),
         (0.02, 0.877), (0, 0.88)]
CAPSULE = [(0, 0.36), (0.07, 0.363), (0.125, 0.376), (0.163, 0.4), (0.184, 0.433), (0.19, 0.465),
           (0.185, 0.51), (0.172, 0.575), (0.157, 0.645), (0.143, 0.72), (0.131, 0.785), (0.108, 0.835),
           (0.072, 0.866), (0.035, 0.878), (0, 0.88)]
BOW_TIE_Z = 0.79
HAND_Z = (BODY_BOTTOM + BODY_TOP) / 2
HAND_X = 0.31
SHOULDER_X, SHOULDER_Z = 0.17, 0.81


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

    def lathe(self, mat, profile, cuts=(), segments=48, scale=(1.0, 1.0, 1.0)):
        """A solid of revolution about the world Z axis through the origin: profile is its outline as
        (radius, z) points, starting and ending on the axis. cuts = (point, normal) planes, each
        removing the part on its normal's side (world coordinates); scale stretches it about the axis."""
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
        self._add(make, mat, Matrix.Translation(-self.pivot) @ Matrix.Diagonal((*scale, 1.0)))

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


def empty(name, pivot):
    """An object without a mesh: only a pivot the game turns (and children hang from)."""
    obj = bpy.data.objects.new(name, None)
    bpy.context.scene.collection.objects.link(obj)
    obj.location = pivot
    return obj


def _copy_into(source, target):
    mesh = bpy.data.meshes.new("tmp_copy")
    source.to_mesh(mesh)
    source.free()
    target.from_mesh(mesh)
    bpy.data.meshes.remove(mesh)


def child(part, parent):
    """Builds part as a child of parent (both pivots at the same point)."""
    obj = part.build()
    obj.parent = parent
    obj.location = part.pivot - parent.location
    return obj


def cone_rot(part, mat, base, r1, r2, height, rotation, segments=24):
    """A (truncated) cone standing on base along the rotated Z axis."""
    m = Matrix.Translation(Vector(base) - part.pivot) @ rotation @ Matrix.Translation((0, 0, height / 2))
    part._add(lambda g: bmesh.ops.create_cone(g, cap_ends=True, segments=segments, radius1=r1, radius2=r2,
                                              depth=height), mat, m)


def spots_on_dome(part, mat, center, radii, spots, size, tilt=Matrix.Identity(4)):
    """Little flat spots on the upper half of an ellipsoid dome (center, radii)."""
    c = Vector(center)
    for i in range(spots):
        a = i * 2.39996 + 0.4
        up = 0.35 + 0.55 * ((i * 0.618) % 1.0)
        if i == 0:
            up = 0.97
        d = Vector((math.cos(a) * math.sqrt(1 - up * up), math.sin(a) * math.sqrt(1 - up * up), up))
        p = Vector((d.x * radii[0], d.y * radii[1], d.z * radii[2]))
        n = Vector((d.x / radii[0], d.y / radii[1], d.z / radii[2])).normalized()
        world = c + (tilt @ Vector((p.x, p.y, p.z, 0))).to_3d()
        normal = (tilt @ Vector((n.x, n.y, n.z, 0))).to_3d()
        s = size * (0.8 + 0.4 * ((i * 0.37) % 1.0))
        part.sphere(mat, world, (s, s, s * 0.35), rotation=normal.to_track_quat("Z", "Y").to_matrix().to_4x4(),
                    segments=12, rings=6)


def mushroom_cap(part, mats, center, radii, spots, size, tilt=Matrix.Identity(4), gills=True):
    """A spotted mushroom cap: the top half of an ellipsoid (radii) on center, cream gills under it."""
    c = Vector(center)
    down = (tilt @ Vector((0, 0, -1, 0))).to_3d()
    part.sphere(mats["Hat"] if "Hat" in mats else mats["Cap"], c, radii, rotation=tilt, segments=36, rings=18,
                cut=(c + down * 0.002, down))
    if gills:
        part.sphere(mats["Gills"], c + down * 0.006, (radii[0] * 0.95, radii[1] * 0.95, radii[2] * 0.08),
                    rotation=tilt, segments=36, rings=6)
    spots_on_dome(part, mats["HatSpots"] if "HatSpots" in mats else mats["Spots"], c, radii, spots, size, tilt)


# The townsperson.

def bow(part, mat, center, width, rotation=Matrix.Identity(4)):
    """A big bow: two rounded wings (flattened cones widening out from the middle) and a round
    knot, width across, facing +Y (turned by rotation about its center)."""
    c = Vector(center)
    for side in (-1, 1):
        # Along local X, out from the knot; flattened front to back.
        turn = Matrix.Rotation(math.radians(side * 90), 4, "Y")
        m = Matrix.Translation(c - part.pivot) @ rotation @ Matrix.Diagonal((1.0, 0.45, 1.0, 1.0)) @ turn @ \
            Matrix.Translation((0, 0, width * 0.22))
        part._add(lambda g: bmesh.ops.create_cone(g, cap_ends=True, segments=24, radius1=width * 0.07,
                                                  radius2=width * 0.2, depth=width * 0.44), mat, m)
        tip = c + (rotation @ Vector((side * width * 0.44, 0, 0, 0))).to_3d()
        part.sphere(mat, tip, (width * 0.06, width * 0.09, width * 0.2), rotation=rotation, segments=16, rings=8)
    part.sphere(mat, c + (rotation @ Vector((0, width * 0.03, 0, 0))).to_3d(), (width * 0.1, width * 0.09, width * 0.12),
                rotation=rotation, segments=20, rings=10)


def smooth_outline(points):
    """A body's outline (radius, z) points smoothed through them (a Catmull-Rom curve, mirrored
    across the axis at both ends so it meets it level), as dense points from the axis under its
    bottom round its side to the axis over its top."""
    pts = [(-points[1][0], points[1][1])] + points + [(-points[-2][0], points[-2][1])]
    curve = []
    for i in range(1, len(pts) - 2):
        p0, p1, p2, p3 = pts[i - 1:i + 3]
        n = max(2, int(math.dist(p1, p2) / 0.008))
        for k in range(n + (1 if i == len(pts) - 3 else 0)):
            t = k / n
            curve.append(tuple(0.5 * (2 * p1[c] + (-p0[c] + p2[c]) * t + (2 * p0[c] - 5 * p1[c] + 4 * p2[c] - p3[c]) * t * t +
                                      (-p0[c] + 3 * p1[c] - 3 * p2[c] + p3[c]) * t ** 3) for c in (0, 1)))
    return [(0.0 if r < 1e-6 else r, z) for r, z in curve]


def outline_r(points, z):
    """The radius of a smoothed outline at height z."""
    for (r0, z0), (r1, z1) in zip(points, points[1:]):
        if z1 > z0 and z0 <= z <= z1:
            return r0 + (r1 - r0) * (z - z0) / (z1 - z0)
    return 0.0


def folk(mats):
    # The head: a big plain round ball, only two big dark oval eyes with sparkles (low and wide)
    # and small soft cheeks.
    head = Part("Head", (0, 0, NECK_Z))
    head.sphere(mats["Skin"], HEAD_C, (HEAD_R, HEAD_R, HEAD_R), segments=48, rings=24)
    for side in (-1, 1):
        d = Vector((side * 0.44, 1.0, -0.2)).normalized()
        eye_c = HEAD_C + d * HEAD_R * 0.96
        facing = d.to_track_quat("Z", "Y").to_matrix().to_4x4()
        head.sphere(mats["Eye"], eye_c, (0.036, 0.052, 0.02), rotation=facing, segments=24, rings=12)
        head.sphere(mats["EyeShine"], eye_c + (facing @ Vector((-side * 0.01, 0.02, 0.016, 0))).to_3d(),
                    (0.012, 0.014, 0.006), rotation=facing, segments=12, rings=6)
        cheek = Vector((side * 0.68, 0.7, -0.32)).normalized()
        head.sphere(mats["Blush"], HEAD_C + cheek * HEAD_R * 0.985, (0.03, 0.018, 0.006),
                    rotation=cheek.to_track_quat("Z", "Y").to_matrix().to_4x4(), segments=16, rings=8)
    head_obj = head.build()

    # Mushroom-cap hats on top of the head (no faces), tipped back a little; a bow on the head.
    top = HEAD_C + Vector((0, -0.01, HEAD_R * 0.7))
    tip = Matrix.Rotation(math.radians(-8), 4, "X")
    for name, radii, spots, size in (("HatDome", (0.22, 0.21, 0.14), 7, 0.03),
                                     ("HatFlat", (0.28, 0.27, 0.07), 9, 0.026),
                                     ("HatTall", (0.2, 0.19, 0.24), 6, 0.026)):
        hat = Part(name, head.pivot)
        hat.cone(mats["Hat"], top + Vector((0, 0, -0.02)), HEAD_R * 0.68, HEAD_R * 0.72, 0.03, segments=40)
        mushroom_cap(hat, mats, top + Vector((0, 0, 0.01)), radii, spots, size, tilt=tip)
        if name == "HatFlat":
            hat.torus(mats["Hat"], top + Vector((0, 0, 0.01)), 0.27, 0.012, rotation=tip, segments=48)
        child(hat, head_obj)
    head_bow = Part("Bow", head.pivot)
    d = Vector((0.5, 0.15, 0.85)).normalized()
    bow(head_bow, mats["Bow"], HEAD_C + d * (HEAD_R + 0.01), 0.2,
        rotation=Matrix.Rotation(math.radians(30), 4, "Y") @ Matrix.Rotation(math.radians(-35), 4, "X"))
    child(head_bow, head_obj)

    # The body: an empty at the hip, with its two shapes and the bow tie under it.
    body_obj = empty("Body", (0, 0, HIP_Z))
    dress = Part("Dress", body_obj.location)
    dress.lathe(mats["Outfit"], smooth_outline(DRESS))
    child(dress, body_obj)
    capsule = Part("Capsule", body_obj.location)
    egg = smooth_outline(CAPSULE)
    capsule.lathe(mats["Outfit"], egg)
    child(capsule, body_obj)
    bow_tie = Part("BowTie", body_obj.location)
    r = outline_r(egg, BOW_TIE_Z)
    bow(bow_tie, mats["Bow"], (0, r + 0.01, BOW_TIE_Z), 2 * r)
    child(bow_tie, body_obj)

    # Big oval hands floating well out beside the body at its middle height, three finger grooves
    # each (on the outer side, toward the front); invisible arms from the shoulders hold them.
    for side, name in ((-1, "L"), (1, "R")):
        shoulder = Vector((side * SHOULDER_X, 0, SHOULDER_Z))
        empty("Arm" + name, shoulder)
        c = Vector((side * HAND_X, 0.02, HAND_Z))
        hand = Part("Hand" + name, c)
        hand.sphere(mats["Skin"], c, (0.07, 0.05, 0.09), segments=28, rings=14)
        rx, ry, rz = 0.07, 0.05, 0.09
        for dx in (-0.034, 0.0, 0.034):
            # Dark grooves down the lower front of the hand, turned a little out.
            dx += side * 0.008
            dz = -0.028
            dy = ry * math.sqrt(1 - (dx / rx) ** 2 - (dz / rz) ** 2)
            n = Vector((dx / rx ** 2, dy / ry ** 2, dz / rz ** 2)).normalized()
            hand.sphere(mats["Finger"], c + Vector((dx, dy, dz)) - n * 0.002, (0.006, 0.004, 0.026),
                        rotation=n.to_track_quat("Y", "Z").to_matrix().to_4x4(), segments=10, rings=10)
        hand.build()

    # Tube legs from under the body's round base, wide apart but inside it: thighs at the hips, shins at the knees down to rounded feet a
    # little wider than the legs.
    for side, name in ((-1, "L"), (1, "R")):
        x = side * LEG_X
        thigh = Part("Leg" + name, (x, 0, HIP_Z))
        thigh.capsule(mats["Legs"], (x, 0, HIP_Z + 0.05), (x, 0, KNEE_Z), LEG_R, segments=20)
        thigh_obj = thigh.build()
        shin = Part("Shin" + name, (x, 0, KNEE_Z))
        shin.capsule(mats["Legs"], (x, 0, KNEE_Z), (x, 0, ANKLE_Z), LEG_R, segments=20)
        shin.sphere(mats["Legs"], (x, 0.025, 0.065), (0.075, 0.095, 0.065), segments=24, rings=12)
        child(shin, thigh_obj)


# The pets: simpler background Mushroom Cats, Dogs and Crows.

def pet_eyes(part, mats, center, forward, spacing, size):
    for side in (-1, 1):
        eye_c = Vector(center) + Vector((side * spacing, forward, 0))
        part.sphere(mats["Eye"], eye_c, (size * 0.8, size * 0.5, size), rotation=Matrix.Rotation(math.radians(side * -14), 4, "Z"),
                    segments=16, rings=8)
        part.sphere(mats["EyeShine"], eye_c + Vector((-size * 0.25, size * 0.4, size * 0.35)), (size * 0.3, size * 0.2, size * 0.3),
                    segments=8, rings=4)


def four_legs(mats, hip_z, front_y, back_y, spread, radius, mat="Fur"):
    for name, x, y in (("LegFL", -spread, front_y), ("LegFR", spread, front_y),
                       ("LegBL", -spread, back_y), ("LegBR", spread, back_y)):
        leg = Part(name, (x, y, hip_z))
        leg.capsule(mats[mat], (x, y, hip_z), (x, y + 0.01, radius), radius, segments=12)
        leg.sphere(mats["FurLight"], (x, y + 0.02, radius * 0.8), (radius * 1.15, radius * 1.3, radius * 0.8), segments=12, rings=6)
        leg.build()


def cat(mats):
    body = Part("Body", (0, 0, 0.2))
    body.sphere(mats["Fur"], (0, 0, 0.2), (0.12, 0.2, 0.11), segments=28, rings=14)
    body.sphere(mats["FurLight"], (0, 0.06, 0.16), (0.09, 0.13, 0.07), segments=20, rings=10)
    body.build()
    neck = Vector((0, 0.16, 0.27))
    head = Part("Head", neck)
    hc = neck + Vector((0, 0.04, 0.07))
    head.sphere(mats["Fur"], hc, (0.13, 0.11, 0.11), segments=28, rings=14)
    head.sphere(mats["FurLight"], hc + Vector((0, 0.08, -0.04)), (0.07, 0.04, 0.05), segments=16, rings=8)
    head.sphere(mats["Nose"], hc + Vector((0, 0.115, -0.015)), (0.016, 0.01, 0.012), segments=8, rings=4)
    pet_eyes(head, mats, hc + Vector((0, 0.085, 0.015)), 0.0, 0.05, 0.03)
    for side in (-1, 1):
        tilt = Matrix.Rotation(math.radians(side * 25), 4, "Y")
        ear = hc + Vector((side * 0.075, -0.01, 0.085))
        cone_rot(head, mats["Stem"], ear + Vector((0, 0, -0.02)), 0.022, 0.018, 0.04, tilt, segments=10)
        mushroom_cap(head, {"Cap": mats["Cap"], "Gills": mats["Stem"], "Spots": mats["Spots"]},
                     ear + (tilt @ Vector((0, 0, 0.02, 0))).to_3d(), (0.055, 0.055, 0.045), 4, 0.012, tilt=tilt, gills=False)
    head.build()
    four_legs(mats, 0.13, 0.1, -0.1, 0.06, 0.035)
    root = Vector((0, -0.19, 0.22))
    tail = Part("Tail", root)
    pts = [root, root + Vector((0, -0.08, 0.06)), root + Vector((0, -0.1, 0.17)), root + Vector((0, -0.06, 0.24))]
    for a, b in zip(pts, pts[1:]):
        tail.capsule(mats["Fur"], a, b, 0.025, segments=10)
    tail.build()


def dog(mats):
    body = Part("Body", (0, 0, 0.22))
    body.sphere(mats["Fur"], (0, 0, 0.22), (0.13, 0.21, 0.12), segments=28, rings=14)
    body.sphere(mats["FurLight"], (0, 0.08, 0.18), (0.1, 0.12, 0.08), segments=20, rings=10)
    # A tiny mushroom growing on its back.
    cone_rot(body, mats["Stem"], (0, -0.03, 0.32), 0.022, 0.018, 0.05, Matrix.Identity(4), segments=10)
    mushroom_cap(body, {"Cap": mats["Cap"], "Gills": mats["Stem"], "Spots": mats["Spots"]},
                 (0, -0.03, 0.365), (0.06, 0.06, 0.045), 4, 0.013, gills=False)
    body.build()
    neck = Vector((0, 0.17, 0.3))
    head = Part("Head", neck)
    hc = neck + Vector((0, 0.04, 0.08))
    head.sphere(mats["Fur"], hc, (0.13, 0.12, 0.12), segments=28, rings=14)
    head.sphere(mats["FurLight"], hc + Vector((0, 0.1, -0.04)), (0.075, 0.07, 0.055), segments=16, rings=8)
    head.sphere(mats["Nose"], hc + Vector((0, 0.165, -0.02)), (0.028, 0.02, 0.022), segments=10, rings=5)
    pet_eyes(head, mats, hc + Vector((0, 0.09, 0.025)), 0.0, 0.055, 0.03)
    for side in (-1, 1):
        head.sphere(mats["FurDark"], hc + Vector((side * 0.12, -0.01, 0.01)), (0.04, 0.06, 0.1),
                    rotation=Matrix.Rotation(math.radians(side * 20), 4, "Y"), segments=14, rings=7)
    head.torus(mats["Collar"], neck + Vector((0, 0.0, -0.02)), 0.08, 0.016,
               rotation=Matrix.Rotation(math.radians(-30), 4, "X"), segments=24)
    head.build()
    four_legs(mats, 0.15, 0.11, -0.11, 0.065, 0.04)
    root = Vector((0, -0.2, 0.26))
    tail = Part("Tail", root)
    tail.capsule(mats["Fur"], root, root + Vector((0, -0.07, 0.12)), 0.03, segments=10)
    tail.sphere(mats["FurLight"], root + Vector((0, -0.08, 0.14)), (0.035, 0.035, 0.04), segments=10, rings=5)
    tail.build()


def crow(mats):
    neck = Vector((0, 0.05, 0.22))
    head = Part("Head", neck)
    hc = neck + Vector((0, 0.02, 0.06))
    head.sphere(mats["Feather"], hc, (0.075, 0.07, 0.07), segments=24, rings=12)
    pet_eyes(head, mats, hc + Vector((0, 0.05, 0.015)), 0.0, 0.035, 0.022)
    cone_rot(head, mats["Beak"], hc + Vector((0, 0.06, -0.015)), 0.022, 0.0, 0.06,
             Matrix.Rotation(math.radians(-90), 4, "X"), segments=10)
    head.build()
    body = Part("Body", (0, 0, 0.14))
    body.sphere(mats["Feather"], (0, 0, 0.14), (0.09, 0.11, 0.09), segments=24, rings=12)
    mushroom_cap(body, {"Cap": mats["Cap"], "Gills": mats["Stem"], "Spots": mats["Spots"]},
                 (0, -0.03, 0.215), (0.06, 0.07, 0.04), 4, 0.012, gills=False)
    body.build()
    for side, name in ((-1, "WingL"), (1, "WingR")):
        shoulder = Vector((side * 0.075, 0.02, 0.17))
        wing = Part(name, shoulder)
        wing.sphere(mats["Feather"], shoulder + Vector((side * 0.03, -0.04, -0.03)), (0.03, 0.09, 0.06),
                    rotation=Matrix.Rotation(math.radians(side * 15), 4, "Y"), segments=16, rings=8)
        wing.build()
    root = Vector((0, -0.1, 0.13))
    tail = Part("Tail", root)
    for a in (-20, 0, 20):
        d = Matrix.Rotation(math.radians(a), 4, "Z") @ Vector((0, -0.09, 0.03, 0))
        tail.sphere(mats["Feather"], root + d.to_3d() * 0.6, (0.02, 0.06, 0.01),
                    rotation=Matrix.Rotation(math.radians(a), 4, "Z"), segments=12, rings=6)
    tail.build()
    for side, name in ((-1, "LegL"), (1, "LegR")):
        hip = Vector((side * 0.035, 0.0, 0.07))
        leg = Part(name, hip)
        leg.capsule(mats["Leg"], hip, (hip.x, 0.01, 0.01), 0.008, segments=8)
        for a in (-25, 0, 25):
            d = Matrix.Rotation(math.radians(a), 4, "Z") @ Vector((0, 0.035, 0, 0))
            leg.capsule(mats["Leg"], (hip.x, 0.01, 0.006), Vector((hip.x, 0.01, 0.006)) + d.to_3d(), 0.006, segments=6)
        leg.build()


def butterfly(mats):
    body = Part("Body", (0, 0, 0))
    body.capsule(mats["BodyDark"], (0, -0.025, 0), (0, 0.025, 0), 0.006, segments=8)
    body.sphere(mats["BodyDark"], (0, 0.033, 0.002), (0.008, 0.008, 0.008), segments=10, rings=5)
    for side in (-1, 1):
        body.capsule(mats["BodyDark"], (side * 0.003, 0.038, 0.004), (side * 0.014, 0.06, 0.014), 0.0015, segments=4)
    body.build()
    for side, name in ((-1, "WingL"), (1, "WingR")):
        wing = Part(name, (0, 0, 0))
        wing.sphere(mats["Wing"], (side * 0.04, 0.012, 0), (0.038, 0.03, 0.003),
                    rotation=Matrix.Rotation(math.radians(side * 20), 4, "Z"), segments=16, rings=6)
        wing.sphere(mats["Wing"], (side * 0.03, -0.022, 0), (0.026, 0.022, 0.003),
                    rotation=Matrix.Rotation(math.radians(side * -25), 4, "Z"), segments=16, rings=6)
        wing.sphere(mats["WingSpot"], (side * 0.05, 0.016, 0.003), (0.011, 0.011, 0.002), segments=10, rings=4)
        wing.build()


KINDS = {"folk": folk, "cat": cat, "dog": dog, "crow": crow, "butterfly": butterfly}


def build(kind):
    bpy.ops.wm.read_factory_settings(use_empty=True)
    mats = {name: material(name, color) for name, color in COLORS.items()}
    KINDS[kind](mats)
    out = os.path.join(HERE, f"{kind}.glb")
    bpy.ops.export_scene.gltf(filepath=out, export_format="GLB", export_apply=True,
                              export_yup=True, export_materials="EXPORT")
    print(f"townsfolk.py: wrote {out}")


wanted = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else list(KINDS)
for name in wanted:
    build(name)
