# Builds Pet City's background life and exports it next to this script:
#   folk.glb  cat.glb  dog.glb  crow.glb  butterfly.glb
# Run from the repository root:
#   blender -b --factory-startup --python modules/Townsfolk/Models/townsfolk.py
# or only some of them:
#   blender -b --factory-startup --python modules/Townsfolk/Models/townsfolk.py -- folk crow
#
# folk.glb is one base townsperson in the NPCs' style (modules/NPCs/Models/npcs.py, the same rig):
# a big round head floating a few cm over a smooth cone body (a triangle from the front, no
# neck), two round hands floating beside it, two little round feet under it, no arms or legs. It
# carries every dress-up option as its own object, and the game shows one of each and tints them
# per townsperson: the hair styles HairShort, HairBob, HairBuns, HairSpiky, HairLong and the
# mushroom-cap hats HatDome, HatFlat, HatTall (children of Head), Trousers (a band round the
# bottom of the cone, hidden for a dress) and Collar (children of Body). Tinted materials: Skin,
# Hair, Outfit, Trim, Trousers, Hat, Shoe.
#
# cat.glb, dog.glb and crow.glb are simpler background Mushroom Cats, Dogs and Crows (tinted Fur,
# FurLight, FurDark, Feather), butterfly.glb a little butterfly (tinted Wing). Only heads have eyes;
# hats and objects never do.
#
# Meters, feet at the origin, facing Blender +Y (Godot's -Z, forward, after the glTF export).
# Separate objects with pivots where they move, so the game can animate them: Body (hip), Head
# (neck), LegL / LegR (hip; empty, the feet FootL / FootR hang from them), ArmL / ArmR (shoulder;
# empty, invisible), HandL / HandR (hand center). Pets: Body, Head (neck), LegFL / LegFR / LegBL /
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
    "Hair": (0.45, 0.28, 0.16),
    "Outfit": (0.4, 0.6, 0.92),
    "Trim": (1.0, 0.97, 0.9),
    "Trousers": (0.35, 0.38, 0.55),
    "Hat": (0.9, 0.22, 0.22),
    "Shoe": (0.42, 0.24, 0.18),
    "Fur": (1.0, 0.58, 0.25),
    "FurLight": (1.0, 0.93, 0.82),
    "FurDark": (0.6, 0.34, 0.18),
    "Feather": (0.18, 0.16, 0.3),
    "Wing": (1.0, 0.55, 0.75),
    # Fixed.
    "Blush": (1.0, 0.45, 0.48),
    "Eye": (0.07, 0.04, 0.06),
    "EyeShine": (1.0, 1.0, 1.0),
    "Mouth": (0.55, 0.16, 0.2),
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

# The rig, as the NPCs' (npcs.py).
HIP_Z = 0.3
NECK_Z = 0.72
HEAD_R = 0.4
HEAD_C = Vector((0, 0, NECK_Z + HEAD_R * 0.92))
SHOULDER_X, SHOULDER_Z = 0.23, NECK_Z - 0.07
ARM_LENGTH = 0.42
BODY_BOTTOM, BODY_TOP = 0.13, 0.61
BODY_R0, BODY_R1 = 0.3, 0.05
BAND_TOP = 0.24


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


# The cone body.

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
    """The body cone's radius at height z (its straight side, before the rounding)."""
    return BODY_R0 + (BODY_R1 - BODY_R0) * (z - BODY_BOTTOM) / (BODY_TOP - BODY_BOTTOM)


def body_profile(offset=0.0, z0=BODY_BOTTOM, z1=BODY_TOP):
    """The outline of the body cone (offset out by offset, for things over it) from z0 to z1;
    rounded at its true bottom rim and top, flat where it is cut off."""
    bottom, top = z0 <= BODY_BOTTOM, z1 >= BODY_TOP
    z0, z1 = (z0 - offset if bottom else z0), (z1 + offset if top else z1)
    corners = [(0, z0), (body_radius(z0) + offset, z0), (body_radius(z1) + offset, z1), (0, z1)]
    points = rounded(corners, [0, 0.06 if bottom else 0, 0.11 if top else 0, 0], steps=8)
    # Rings every few cm up the straight side, for smooth shading (and the toon outline).
    dense = [points[0]]
    for p, q in zip(points, points[1:]):
        n = max(1, int(math.dist(p, q) / 0.03))
        dense += [(p[0] + (q[0] - p[0]) * k / n, p[1] + (q[1] - p[1]) * k / n) for k in range(1, n + 1)]
    return dense


SLOPE = (BODY_R0 - BODY_R1) / (BODY_TOP - BODY_BOTTOM)


def cone_normal(angle):
    """The cone's outward normal angle degrees round from the front (+Y; positive toward -X, the left)."""
    t = math.radians(angle + 90)
    return Vector((math.cos(t), math.sin(t), SLOPE)).normalized()


def on_cone(angle, z, lift=0.0):
    """The point on the cone's side angle degrees round from the front at height z, lifted off it."""
    t = math.radians(angle + 90)
    r = body_radius(z)
    return Vector((math.cos(t) * r, math.sin(t) * r, z)) + cone_normal(angle) * lift


def front(x, z, lift=0.0):
    """The point on the front of the cone at x (right +), height z, lifted off it (out along +Y)."""
    r = body_radius(z) + lift
    return Vector((x, math.sqrt(max(0.0, r * r - x * x)), z))


def patch(part, mat, angle, z, size, lift=0.006, roll=0.0, segments=16, rings=8, box=False):
    """A flat shape lying on the cone's side: size is (across, up the slope, thickness) radii (a box:
    full extents), roll turns it about the normal (degrees)."""
    normal = cone_normal(angle)
    rot = normal.to_track_quat("Z", "Y").to_matrix().to_4x4() @ Matrix.Rotation(math.radians(roll), 4, "Z")
    center = on_cone(angle, z, lift)
    if box:
        part.box(mat, center, size, rotation=rot)
    else:
        part.sphere(mat, center, size, rotation=rot, segments=segments, rings=rings)


def band(part, mat, z, minor, lift=0.0):
    """A ring round the cone at height z."""
    part.torus(mat, (0, 0, z), body_radius(z) + lift, minor, segments=48)



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

def rig(mats):
    for side, name in ((-1, "LegL"), (1, "LegR")):
        x = side * 0.125
        leg = empty(name, (x, 0, HIP_Z + 0.02))
        foot = Part("Foot" + name[-1], leg.location)
        foot.sphere(mats["Shoe"], (x, 0.085, 0.062), (0.095, 0.125, 0.064), segments=24, rings=12)
        child(foot, leg)
    for side, name in ((-1, "ArmL"), (1, "ArmR")):
        shoulder = Vector((side * SHOULDER_X, 0, SHOULDER_Z))
        empty(name, shoulder)
        hand = shoulder + Vector((side * 0.13, 0.02, -0.22)).normalized() * ARM_LENGTH
        hand_part = Part("Hand" + name[-1], hand)
        hand_part.sphere(mats["Skin"], hand, (0.082, 0.078, 0.085), segments=24, rings=12)
        hand_part.build()


def folk(mats):
    head = Part("Head", (0, 0, NECK_Z))
    head.sphere(mats["Skin"], HEAD_C, (HEAD_R, HEAD_R * 0.96, HEAD_R * 0.94), segments=40, rings=24)
    for side in (-1, 1):
        # Big dark oval eyes with sparkles.
        eye_c = HEAD_C + Vector((side * 0.15, HEAD_R * 0.86, -0.04))
        tilt = Matrix.Rotation(math.radians(side * -20), 4, "Z")
        head.sphere(mats["Eye"], eye_c, (0.08, 0.06, 0.115), rotation=tilt, segments=24, rings=12)
        head.sphere(mats["EyeShine"], eye_c + Vector((-0.022, 0.05, 0.045)), (0.03, 0.02, 0.034), segments=12, rings=6)
        head.sphere(mats["EyeShine"], eye_c + Vector((0.025, 0.055, -0.035)), (0.014, 0.01, 0.014), segments=10, rings=5)
        head.sphere(mats["Blush"], HEAD_C + Vector((side * 0.25, HEAD_R * 0.74, -0.15)), (0.06, 0.025, 0.035),
                    rotation=Matrix.Rotation(math.radians(side * -38), 4, "Z"), segments=16, rings=8)
        head.sphere(mats["Skin"], HEAD_C + Vector((side * HEAD_R * 0.97, 0.02, -0.06)), (0.05, 0.04, 0.07),
                    segments=12, rings=6)
    head.torus(mats["Mouth"], HEAD_C + Vector((0, HEAD_R * 0.92, -0.17)), 0.035, 0.01,
               rotation=Matrix.Rotation(math.radians(90), 4, "X") @ Matrix.Rotation(math.radians(-90), 4, "Z"), arc=0.42)
    head_obj = head.build()
    hair_mats = {"Hair": mats["Hair"]}

    def hair_cap(part, back=1.0):
        part.sphere(mats["Hair"], HEAD_C + Vector((0, -0.02, 0.03)), (HEAD_R * 1.07, HEAD_R * 1.06 * back, HEAD_R * 1.04),
                    segments=40, rings=24, cut=(HEAD_C + Vector((0, 0.1, -0.1)), Vector((0, 0.85, -0.53))))

    def fringe(part, z=0.24):
        for x in (-0.22, -0.08, 0.08, 0.22):
            part.sphere(mats["Hair"], HEAD_C + Vector((x, HEAD_R * 0.8 - abs(x) * 0.35, z - abs(x) * 0.3)),
                        (0.1, 0.07, 0.075), segments=16, rings=8)

    # Short: a neat cap with a fringe.
    hair = Part("HairShort", head.pivot)
    hair_cap(hair)
    fringe(hair)
    child(hair, head_obj)
    # Bob: a straight fringe and sides down to the chin.
    hair = Part("HairBob", head.pivot)
    hair_cap(hair)
    hair.sphere(mats["Hair"], HEAD_C + Vector((0, HEAD_R * 0.55, 0.22)), (0.34, 0.16, 0.11), segments=24, rings=12)
    for side in (-1, 1):
        hair.sphere(mats["Hair"], HEAD_C + Vector((side * 0.33, -0.02, -0.14)), (0.13, 0.3, 0.22), segments=20, rings=10)
    child(hair, head_obj)
    # Buns: two round buns at the sides of the head.
    hair = Part("HairBuns", head.pivot)
    hair_cap(hair)
    fringe(hair)
    for side in (-1, 1):
        hair.sphere(mats["Hair"], HEAD_C + Vector((side * 0.36, -0.05, 0.12)), (0.14, 0.14, 0.14), segments=20, rings=10)
    child(hair, head_obj)
    # Spiky: little pointed tufts all round.
    hair = Part("HairSpiky", head.pivot)
    hair_cap(hair, back=0.98)
    for k in range(9):
        a = math.radians(-80 + k * 20)
        d = Vector((math.sin(a) * 0.9, math.cos(a) * 0.55 + 0.1, 0.45)).normalized()
        base = HEAD_C + d * HEAD_R * 0.95
        cone_rot(hair, mats["Hair"], base, 0.09, 0.0, 0.16, d.to_track_quat("Z", "Y").to_matrix().to_4x4(), segments=10)
    child(hair, head_obj)
    # Long: a cap, a fringe and long hair down the back.
    hair = Part("HairLong", head.pivot)
    hair_cap(hair)
    fringe(hair)
    hair.sphere(mats["Hair"], HEAD_C + Vector((0, -0.12, -0.22)), (0.4, 0.3, 0.38), segments=32, rings=16)
    for side in (-1, 1):
        hair.sphere(mats["Hair"], HEAD_C + Vector((side * 0.36, -0.06, -0.24)), (0.1, 0.2, 0.26), segments=16, rings=8)
    child(hair, head_obj)

    # Mushroom-cap hats, on top of the head (over any hair), tipped back a little.
    top = HEAD_C + Vector((0, -0.02, HEAD_R * 0.62))
    tip = Matrix.Rotation(math.radians(-8), 4, "X")
    hat = Part("HatDome", head.pivot)
    hat.cone(mats["Hat"], top + Vector((0, 0, -0.04)), HEAD_R * 0.86, HEAD_R * 0.9, 0.06, segments=40)
    mushroom_cap(hat, mats, top + Vector((0, 0, 0.02)), (0.46, 0.44, 0.3), 7, 0.06, tilt=tip)
    child(hat, head_obj)
    hat = Part("HatFlat", head.pivot)
    hat.cone(mats["Hat"], top + Vector((0, 0, -0.04)), HEAD_R * 0.86, HEAD_R * 0.9, 0.06, segments=40)
    mushroom_cap(hat, mats, top + Vector((0, 0, 0.02)), (0.6, 0.58, 0.14), 9, 0.055, tilt=tip)
    hat.torus(mats["Hat"], top + Vector((0, 0, 0.02)), 0.57, 0.025, rotation=tip, segments=48)
    child(hat, head_obj)
    hat = Part("HatTall", head.pivot)
    hat.cone(mats["Hat"], top + Vector((0, 0, -0.04)), HEAD_R * 0.86, HEAD_R * 0.9, 0.06, segments=40)
    mushroom_cap(hat, mats, top + Vector((0, 0, 0.02)), (0.42, 0.4, 0.5), 6, 0.055, tilt=tip)
    child(hat, head_obj)

    body = Part("Body", (0, 0, HIP_Z))
    body.lathe(mats["Outfit"], body_profile())
    band(body, mats["Trim"], BODY_BOTTOM + 0.03, 0.022, lift=0.004)
    body_obj = body.build()
    trousers = Part("Trousers", body.pivot)
    trousers.lathe(mats["Trousers"], body_profile(0.01, BODY_BOTTOM - 0.006, BAND_TOP))
    child(trousers, body_obj)
    collar = Part("Collar", body.pivot)
    band(collar, mats["Trim"], 0.545, 0.024, lift=0.004)
    for side in (-1, 1):
        patch(collar, mats["Trim"], side * 22, 0.51, (0.045, 0.03, 0.01), lift=0.01, roll=-side * 30, segments=12, rings=6)
    child(collar, body_obj)
    rig(mats)


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
