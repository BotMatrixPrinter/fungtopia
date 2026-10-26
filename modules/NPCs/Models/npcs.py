# Builds the five townsfolk and exports them next to this script:
#   jeremy.glb  millie.glb  lucy.glb  abby.glb  pip.glb
# Run from the repository root:
#   blender -b --factory-startup --python modules/NPCs/Models/npcs.py
# or only some of them:
#   blender -b --factory-startup --python modules/NPCs/Models/npcs.py -- pip
#
# Folk made of a few clean simple shapes, in the player characters' style (Saria's drawing, see the
# Player README's Models), about 1.47 m tall: a big round head (0.38 m across, a quarter of the
# height) floating a few cm over the body (no neck), the body about half the height (a tall
# cone, a triangle from the front, for a dress; a tall rounded capsule otherwise), two straight tube
# legs with rounded feet under it (a quarter of the height), and two big oval hands with finger
# marks floating well out beside the body at about its middle height (no arms).
#   Dr. Jeremy: big dark eyes with sparkles, a neat side parting, a white doctor's coat (the capsule)
#               with lapels, buttons and a name badge open over a blue shirt and red tie, a
#               stethoscope round its top.
#   Millie:     cat ears, happy ^ ^ closed eyes, honey bunches, a pink bow, a mint spotted dress
#               (the cone) with a frilly hem, and a little curled cat tail.
#   Lucy:       a flower in her purple bob, half-lidded unimpressed eyes, a flat little mouth, a shirt
#               (the capsule) saying "Pet Store" with little hearts, a skirt band round the bottom.
#   Abby:       big dark eyes, a happy smile, hair in a tall top bun, the cone her pink triangle
#               dress with a white collar and hem.
#   Pip:        the cook at the Mushroom Munchies food stall: big dark eyes, rosy freckled cheeks,
#               a big smile, a puffy white chef's hat shaped like a mushroom cap with red spots, a
#               cream apron with a little muffin printed on it over the capsule, a wooden spoon in
#               the right hand; standing on a little wooden step stool (to reach over the counter).
#
# Meters, feet at the origin, facing Blender +Y (Godot's -Z, forward, after the glTF export).
# Separate objects with pivots where they move, so the game can animate them: Body (hip), Head
# (neck), LegL / LegR (hip; the tube leg with its rounded foot), ArmL / ArmR (shoulder; empty,
# invisible), HandL / HandR (hand center, at the invisible arms' ends). Materials carry plain
# colors only; the game draws them toon-shaded with ink outlines.
import math
import os
import sys

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
    "Finger": (0.74, 0.4, 0.32),
}

# The rig: the neck (the head's pivot), the hip (the body's and legs' pivot), the shoulders and
# the invisible arms' ends, the hands' centers.
HIP_Z = 0.46
NECK_Z = 1.08
SHOULDER_X, SHOULDER_Z = 0.15, 0.98
HAND_C = Vector((0.32, 0.02, 0.7))
LEG_X, LEG_R = 0.08, 0.06

# The head is drawn at the design size below (a ball of radius HEAD_R round HEAD_C, its features
# placed on it) and then scaled into place: a ball 0.38 m across with its top at about 1.47 m.
HEAD_R = 0.4
HEAD_C = Vector((0, 0, 1.088))
HEAD_SCALE = 0.475
HEAD_AT = Vector((0, 0, 1.29))
HEAD_FIT = Matrix.Translation(HEAD_AT) @ Matrix.Scale(HEAD_SCALE, 4) @ Matrix.Translation(-HEAD_C)

# The body, from BODY_BOTTOM to BODY_TOP: a cone (a dress: BODY_R0 round its bottom, narrowing to a
# rounded top) or a capsule (a rounded tall box, round from above). use_body picks one.
BODY_BOTTOM, BODY_TOP = 0.36, 1.06
BODIES = {
    # radius at the bottom, at the top, rounding of the bottom rim and of the top
    "cone": (0.2, 0.035, 0.04, 0.1),
    "capsule": (0.16, 0.16, 0.07, 0.1),
}
BODY_R0, BODY_R1, ROUND0, ROUND1 = BODIES["cone"]
# Where a band round the bottom of the body (Lucy's skirt, Pip's trousers) ends.
BAND_TOP = 0.55


def use_body(kind):
    global BODY_R0, BODY_R1, ROUND0, ROUND1, _side
    BODY_R0, BODY_R1, ROUND0, ROUND1 = BODIES[kind]
    _side = body_profile()


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

    def __init__(self, name, pivot, rotation=Matrix.Identity(4), fit=Matrix.Identity(4)):
        """rotation: the object's rotation; shapes are still given in world coordinates. fit: moves
        the shapes from the coordinates they are given in into the world (the head's HEAD_FIT)."""
        self.name = name
        self.pivot = Vector(pivot)
        self.fit = Matrix.Translation(-self.pivot) @ fit @ Matrix.Translation(self.pivot)
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
        geom.transform(self.unrotate @ self.fit @ matrix)
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

    def capsule(self, mat, a, b, radius, segments=16, rings=1):
        """A rounded rod from point a to point b (its side cut into rings, for smooth shading)."""
        a, b = Vector(a), Vector(b)
        axis = b - a
        length = axis.length / rings
        rot = axis.normalized().to_track_quat("Z", "Y").to_matrix().to_4x4()
        for k in range(rings):
            mid = a + axis * ((k + 0.5) / rings)
            m = Matrix.Translation(mid - self.pivot) @ rot
            self._add(lambda g: bmesh.ops.create_cone(g, cap_ends=False, segments=segments,
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


# The body.

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


def straight_radius(z):
    """The body's radius at height z along its straight side, before the rounding."""
    return BODY_R0 + (BODY_R1 - BODY_R0) * (z - BODY_BOTTOM) / (BODY_TOP - BODY_BOTTOM)


def body_profile(offset=0.0, z0=BODY_BOTTOM, z1=BODY_TOP):
    """The outline of the body (offset out by offset, for things over it) from z0 to z1; rounded
    at its true bottom rim and top, flat where it is cut off."""
    bottom, top = z0 <= BODY_BOTTOM, z1 >= BODY_TOP
    z0, z1 = (z0 - offset if bottom else z0), (z1 + offset if top else z1)
    corners = [(0, z0), (straight_radius(z0) + offset, z0), (straight_radius(z1) + offset, z1), (0, z1)]
    points = rounded(corners, [0, ROUND0 if bottom else 0, ROUND1 if top else 0, 0], steps=8)
    # Rings every few cm up the side, for smooth shading (and the toon outline).
    dense = [points[0]]
    for p, q in zip(points, points[1:]):
        n = max(1, int(math.dist(p, q) / 0.03))
        dense += [(p[0] + (q[0] - p[0]) * k / n, p[1] + (q[1] - p[1]) * k / n) for k in range(1, n + 1)]
    return dense


_side = body_profile()


def _side_at(z):
    """The body's outline segment ((r, z), (r, z)) at height z, up its side."""
    pts = [p for p in _side if p[0] > 1e-6]
    for p, q in zip(pts, pts[1:]):
        if p[1] <= z <= q[1] and q[1] > p[1]:
            return p, q
    return (pts[0], pts[1]) if z < pts[0][1] else (pts[-2], pts[-1])


def body_radius(z):
    """The body's radius at height z (rounding and all)."""
    p, q = _side_at(z)
    t = 0.0 if q[1] == p[1] else max(0.0, min(1.0, (z - p[1]) / (q[1] - p[1])))
    return p[0] + (q[0] - p[0]) * t


def cone_normal(angle, z):
    """The body's outward normal angle degrees round from the front (+Y; positive toward -X, the
    left) at height z."""
    p, q = _side_at(z)
    dr, dz = q[0] - p[0], q[1] - p[1]
    t = math.radians(angle + 90)
    return Vector((math.cos(t) * dz, math.sin(t) * dz, -dr)).normalized()


def on_cone(angle, z, lift=0.0):
    """The point on the body's side angle degrees round from the front at height z, lifted off it."""
    t = math.radians(angle + 90)
    r = body_radius(z)
    return Vector((math.cos(t) * r, math.sin(t) * r, z)) + cone_normal(angle, z) * lift


def front(x, z, lift=0.0):
    """The point on the front of the body at x (right +), height z, lifted off it (out along +Y)."""
    r = body_radius(z) + lift
    return Vector((x, math.sqrt(max(0.0, r * r - x * x)), z))


def patch(part, mat, angle, z, size, lift=0.006, roll=0.0, segments=16, rings=8, box=False):
    """A flat shape lying on the body's side: size is (across, up the slope, thickness) radii (a box:
    full extents), roll turns it about the normal (degrees)."""
    normal = cone_normal(angle, z)
    rot = normal.to_track_quat("Z", "Y").to_matrix().to_4x4() @ Matrix.Rotation(math.radians(roll), 4, "Z")
    center = on_cone(angle, z, lift)
    if box:
        part.box(mat, center, size, rotation=rot)
    else:
        part.sphere(mat, center, size, rotation=rot, segments=segments, rings=rings)


def band(part, mat, z, minor, lift=0.0):
    """A ring round the body at height z."""
    part.torus(mat, (0, 0, z), body_radius(z) + lift, minor, segments=48)


def heart(part, mat, center, size, normal):
    """A little heart lying on a surface facing normal: two lobes and a point."""
    rot = Vector(normal).to_track_quat("Y", "Z").to_matrix().to_4x4()
    c = Vector(center)

    def at(dx, dz):
        return c + (rot @ Vector((dx, 0, dz, 1))).to_3d()
    for side in (-1, 1):
        part.sphere(mat, at(side * size * 0.5, size * 0.25), (size * 0.58, size * 0.3, size * 0.58),
                    rotation=rot, segments=12, rings=6)
    part.sphere(mat, at(0, -size * 0.35), (size * 0.62, size * 0.3, size * 0.62),
                rotation=rot @ Matrix.Rotation(math.radians(45), 4, "Y"), segments=4, rings=2)


def cone_text(part, mat, text, z, size, lift=0.006):
    """Text written across the front of the body at height z, hugging it."""
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
    for v in geom.verts:
        # Text space: x right, y up, z out of the page; on the cone (seen from the front, +Y):
        # right is -X, up +Z, out +Y.
        v.co = front(-v.co.x, z + v.co.y, lift + v.co.z)
    part.world_mesh(mat, geom)


def rig(mats, legs="Legs"):
    """The legs: two straight tubes from inside the body down to rounded feet a little wider than
    them. The arms: empty pivots at the shoulders; the big oval hands float well out beside the
    body at about its middle height, at the invisible arms' ends, each with three finger marks."""
    for side, name in ((-1, "LegL"), (1, "LegR")):
        x = side * LEG_X
        leg = Part(name, (x, 0, HIP_Z))
        leg.capsule(mats[legs], (x, 0, HIP_Z + 0.05), (x, 0, 0.08), LEG_R, segments=20, rings=14)
        leg.sphere(mats["Shoe"], (x, 0.025, 0.056), (0.074, 0.088, 0.056), segments=24, rings=12)
        leg.build()
    for side, name in ((-1, "ArmL"), (1, "ArmR")):
        empty(name, (side * SHOULDER_X, 0, SHOULDER_Z))
        c = Vector((side * HAND_C.x, HAND_C.y, HAND_C.z))
        hand = Part("Hand" + name[-1], c)
        size = (0.07, 0.05, 0.09)
        hand.sphere(mats["Skin"], c, size, segments=24, rings=14)
        # Finger marks: short dark lines down the front of the lower half, hugging the oval.
        for dx in (-0.024, 0.0, 0.024):
            pts = []
            for dz in (-0.018, -0.042, -0.066):
                y = size[1] * math.sqrt(max(0.0, 1 - (dx / size[0]) ** 2 - (dz / size[2]) ** 2))
                pts.append(c + Vector((dx, y + 0.002, dz)))
            for p, q in zip(pts, pts[1:]):
                hand.capsule(mats["Finger"], p, q, 0.0055, segments=8)
        hand.build()


# Heads (big and round, as the player characters'), drawn at the design size (HEAD_R round
# HEAD_C) and fitted into place (HEAD_FIT).

def round_head(mats):
    head = Part("Head", (0, 0, NECK_Z), fit=HEAD_FIT)
    head.sphere(mats["Skin"], HEAD_C, (HEAD_R, HEAD_R * 0.96, HEAD_R * 0.94), segments=40, rings=24)
    return head


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


def freckles(head, mats):
    """A few freckles across each rosy cheek and the nose."""
    for side in (-1, 1):
        for dx, dz in ((0.0, 0.0), (0.045, 0.02), (0.035, -0.03), (-0.04, 0.015)):
            head.sphere(mats["Freckle"], HEAD_C + Vector((side * (0.22 + dx), HEAD_R * 0.8 - dx * 0.4, -0.11 + dz)),
                        (0.011, 0.008, 0.011), segments=8, rings=4)


# The five.

def jeremy(mats):
    head = round_head(mats)
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

    use_body("capsule")
    body = Part("Body", (0, 0, HIP_Z))
    # The white doctor's coat is the capsule, open at the top in a V over a blue shirt and red tie.
    v_low, k = 0.8, 0.36
    body.lathe(mats["Coat"], body_profile())
    body.lathe(mats["Shirt"], body_profile(0.005, v_low, BODY_TOP),
               cuts=(((0, 0, v_low), Vector((1, 0, -k))), ((0, 0, v_low), Vector((-1, 0, -k))),
                     ((0, 0.02, 0), Vector((0, -1, 0)))))
    patch(body, mats["Tie"], 0, 0.88, (0.017, 0.055, 0.008), lift=0.01, segments=12, rings=6)
    patch(body, mats["Tie"], 0, 0.95, (0.02, 0.016, 0.01), lift=0.01, segments=12, rings=6)
    # Lapels along the V, buttons down the front below it, a name badge on the chest.
    for side in (-1, 1):
        x = 0.04
        a = math.degrees(math.atan2(x, body_radius(0.88)))
        patch(body, mats["Lapel"], -side * a, 0.88, (0.022, 0.07, 0.008), lift=0.009, roll=side * 18, segments=12, rings=6)
    for z in (0.72, 0.6, 0.48):
        patch(body, mats["Button"], 0, z, (0.014, 0.014, 0.006), lift=0.005, segments=12, rings=6)
    patch(body, mats["Badge"], 36, 0.79, (0.07, 0.04, 0.01), lift=0.007, roll=-6, box=True)
    patch(body, mats["BadgeStripe"], 36, 0.803, (0.072, 0.01, 0.012), lift=0.008, roll=-6, box=True)
    # The stethoscope: a ring round the top of the coat, two tubes down the front, a shiny chest piece.
    band(body, mats["Tube"], 0.95, 0.01, lift=0.01)
    piece = on_cone(-18, 0.72, 0.016)
    for a in (-42, 34):
        body.capsule(mats["Tube"], on_cone(a, 0.95, 0.011), piece, 0.009, segments=8)
    patch(body, mats["Metal"], -18, 0.72, (0.032, 0.032, 0.016), lift=0.018, segments=16, rings=8)
    body.build()
    rig(mats, legs="Trousers")


def millie(mats):
    head = round_head(mats)
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

    use_body("cone")
    body = Part("Body", (0, 0, HIP_Z))
    # The mint dress is the cone, with white spots, a frilly hem and a pink bow at the top.
    body.lathe(mats["Dress"], body_profile())
    band(body, mats["Frill"], BODY_BOTTOM + 0.025, 0.02, lift=0.003)
    for a, z in ((20, 0.46), (-40, 0.44), (75, 0.5), (-95, 0.46), (140, 0.48), (-150, 0.5), (180, 0.44),
                 (5, 0.64), (-70, 0.62), (110, 0.66), (-130, 0.68), (35, 0.8), (-25, 0.84), (160, 0.78)):
        patch(body, mats["Spots"], a, z, (0.026, 0.026, 0.008), lift=0.003, segments=16, rings=8)
    ribbon_bow(body, mats["Bow"], on_cone(0, 0.96, 0.025), 0.05)
    # A little cat tail, curling up from the back of the dress.
    pts = [on_cone(180, 0.52, -0.02), on_cone(175, 0.52, 0.08), on_cone(165, 0.58, 0.14), on_cone(160, 0.67, 0.15),
           on_cone(165, 0.72, 0.1)]
    for a, b in zip(pts, pts[1:]):
        body.capsule(mats["Hair"], a, b, 0.028, segments=10)
    body.build()
    rig(mats)


def lucy(mats):
    head = round_head(mats)
    sleepy_eyes(head, mats)
    # A small flat unimpressed mouth.
    head.capsule(mats["Mouth"], HEAD_C + Vector((-0.04, HEAD_R * 0.93, -0.18)), HEAD_C + Vector((0.04, HEAD_R * 0.93, -0.18)),
                 0.009, segments=8)
    hair_cap(head, mats, fringe=False)
    # A straight-cut fringe and a bob down to the chin.
    head.sphere(mats["Hair"], HEAD_C + Vector((0, HEAD_R * 0.55, 0.22)), (0.34, 0.16, 0.11), segments=24, rings=12)
    for side in (-1, 1):
        head.sphere(mats["Hair"], HEAD_C + Vector((side * 0.33, -0.02, -0.14)), (0.13, 0.3, 0.22), segments=20, rings=10)
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

    use_body("capsule")
    body = Part("Body", (0, 0, HIP_Z))
    # The pale pink shirt is the capsule above a skirt band round its bottom, "Pet Store" across
    # the front with little hearts around.
    body.lathe(mats["Shirt"], body_profile(0.0, BAND_TOP - 0.03, BODY_TOP))
    body.lathe(mats["Skirt"], body_profile(0.012, BODY_BOTTOM - 0.006, BAND_TOP))
    cone_text(body, mats["Print"], "Pet", 0.82, 0.06)
    cone_text(body, mats["Print"], "Store", 0.75, 0.06)
    for a, z, s in ((48, 0.66, 0.016), (-44, 0.67, 0.015), (0, 0.65, 0.013), (42, 0.89, 0.013), (-40, 0.9, 0.015)):
        heart(body, mats["Heart"], on_cone(a, z, 0.005), s, cone_normal(a, z))
    body.build()
    rig(mats)


def abby(mats):
    head = round_head(mats)
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

    use_body("cone")
    body = Part("Body", (0, 0, HIP_Z))
    # The cone is her pink triangle dress, with a white collar at the top and a white hem.
    body.lathe(mats["Dress"], body_profile())
    band(body, mats["Collar"], 0.97, 0.016, lift=0.003)
    for side in (-1, 1):
        patch(body, mats["Collar"], side * 24, 0.94, (0.032, 0.022, 0.008), lift=0.008, roll=-side * 30, segments=12, rings=6)
    band(body, mats["Hem"], BODY_BOTTOM + 0.03, 0.018, lift=0.003)
    body.build()
    rig(mats)


def muffin_print(part, mats, z, size, lift=0.018):
    """A little muffin printed on the front of the body (the apron) at height z: a pleated pink
    cup, a domed brown top with blue berries and a red cherry."""
    def on(x, dz, more=0.0):
        return front(x, z + dz, lift + more)
    # The cup: a trapezoid of flattened pleats, wider at the top.
    for k in range(5):
        t = (k - 2) / 2
        part.capsule(mats["Cup"], on(t * size * 0.5, -size * 0.05), on(t * size * 0.36, -size * 0.5), size * 0.11, segments=8)
    # The muffin top, berries and a cherry.
    part.sphere(mats["MuffinTop"], on(0, size * 0.12), (size * 0.62, size * 0.06, size * 0.34), segments=16, rings=8)
    for dx, dz in ((-0.25, 0.16), (0.2, 0.22), (0.0, 0.05), (0.33, 0.04)):
        part.sphere(mats["Berry"], on(dx * size, dz * size, 0.02 * size), (size * 0.09, size * 0.06, size * 0.09),
                    segments=8, rings=4)
    part.sphere(mats["Cherry"], on(-size * 0.05, size * 0.46, 0.02 * size), (size * 0.12, size * 0.07, size * 0.12),
                segments=10, rings=5)


STOOL_H = 0.45


def pip(mats):
    head = round_head(mats)
    round_eyes(head, mats)
    blush(head, mats)
    freckles(head, mats)
    smile(head, mats, big=True)
    # Short ginger hair under the hat: the back of the head, a fringe and tufts over the ears.
    head.sphere(mats["Hair"], HEAD_C + Vector((0, -0.04, 0.0)), (HEAD_R * 1.02, HEAD_R, HEAD_R * 0.96),
                segments=40, rings=24, cut=(HEAD_C + Vector((0, 0.12, 0.0)), Vector((0, 1, 0))))
    for x in (-0.24, -0.12, 0.0, 0.12, 0.24):
        head.sphere(mats["Hair"], HEAD_C + Vector((x, HEAD_R * 0.78 - abs(x) * 0.4, 0.22 - abs(x) * 0.2)),
                    (0.085, 0.06, 0.06), segments=14, rings=7)
    for side in (-1, 1):
        head.sphere(mats["Hair"], HEAD_C + Vector((side * HEAD_R * 0.92, -0.04, 0.05)), (0.08, 0.17, 0.15),
                    segments=16, rings=8)
        head.sphere(mats["Skin"], HEAD_C + Vector((side * HEAD_R * 0.97, 0.03, -0.08)), (0.05, 0.04, 0.07),
                    segments=12, rings=6)
    # The chef's hat: a white band round the top of the head under a puffy white mushroom cap,
    # puffed up like a toque, with a few red spots and cream gills beneath its rim.
    band_z = HEAD_C.z + HEAD_R * 0.6
    head.cone(mats["Hat"], (0, -0.01, band_z), HEAD_R * 0.86, HEAD_R * 0.92, 0.08, segments=40)
    cap_c = Vector((0, -0.01, band_z + 0.06))
    cap_r = (0.47, 0.45, 0.24)
    # The cap's rim curls under a little, puffy, with cream gills beneath.
    head.torus(mats["Hat"], cap_c + Vector((0, 0, 0.01)), 0.42, 0.045, segments=48)
    head.sphere(mats["Gills"], cap_c + Vector((0, 0, -0.005)), (0.43, 0.41, 0.03), segments=40, rings=8)
    head.sphere(mats["Hat"], cap_c, cap_r, segments=40, rings=20,
                cut=(cap_c + Vector((0, 0, -0.01)), Vector((0, 0, -1))))
    for a, up, r in ((75, 0.55, 0.075), (25, 0.3, 0.06), (130, 0.32, 0.065), (-20, 0.62, 0.05), (200, 0.6, 0.055),
                     (-75, 0.3, 0.06), (95, 0.95, 0.05)):
        t = math.radians(a)
        d = Vector((math.cos(t) * math.sqrt(1 - up * up), math.sin(t) * math.sqrt(1 - up * up), up))
        spot = cap_c + Vector((d.x * cap_r[0], d.y * cap_r[1], d.z * cap_r[2]))
        normal = Vector((d.x / cap_r[0], d.y / cap_r[1], d.z / cap_r[2])).normalized()
        head.sphere(mats["HatSpots"], spot, (r, r, r * 0.35),
                    rotation=normal.to_track_quat("Z", "Y").to_matrix().to_4x4(), segments=14, rings=6)
    head.build()

    use_body("capsule")
    body = Part("Body", (0, 0, HIP_Z))
    # The coral shirt is the capsule above a trousers band round its bottom; the cream apron
    # covers the middle of its front from the trousers up to the chest, tied round it with a bow at the back.
    body.lathe(mats["Shirt"], body_profile(0.0, BAND_TOP - 0.03, BODY_TOP))
    body.lathe(mats["Trousers"], body_profile(0.01, BODY_BOTTOM - 0.006, BAND_TOP))
    body.lathe(mats["Apron"], body_profile(0.014, BODY_BOTTOM + 0.03, 0.9),
               cuts=(((0, 0.04, 0), Vector((0, -1, 0))), ((0.11, 0, 0), Vector((1, 0, 0))), ((-0.11, 0, 0), Vector((-1, 0, 0)))))
    band(body, mats["Apron"], 0.9, 0.011, lift=0.01)
    band(body, mats["Apron"], 0.66, 0.013, lift=0.014)
    ribbon_bow(body, mats["Apron"], on_cone(180, 0.66, 0.03), 0.04)
    muffin_print(body, mats, 0.75, 0.1)
    body.build()
    rig(mats, legs="Trousers")

    # A wooden spoon in the right hand, pointing down (into the pot while stirring).
    h = bpy.data.objects["HandR"].location.copy()
    spoon = Part("Spoon", h)
    spoon.capsule(mats["Wood"], h + Vector((0, 0.01, 0.08)), h + Vector((0, 0.01, -0.28)), 0.013, segments=10)
    spoon.sphere(mats["Wood"], h + Vector((0, 0.01, -0.33)), (0.045, 0.02, 0.062), segments=16, rings=8)
    spoon.build().parent = bpy.data.objects["HandR"]
    bpy.data.objects["Spoon"].location = (0, 0, 0)

    # The counter top is about as high as Pip's shoulders, so Pip stands on a little step stool
    # behind it (to stir the pot on it and be seen over it): lift everything onto it.
    for obj in bpy.data.objects:
        if obj.parent is None:
            obj.location.z += STOOL_H
    stool = Part("Stool", (0, 0, 0))
    stool.box(mats["Wood"], (0, 0, STOOL_H - 0.03), (0.5, 0.36, 0.06))
    for x in (-0.2, 0.2):
        stool.box(mats["WoodDark"], (x, 0, (STOOL_H - 0.06) / 2), (0.05, 0.3, STOOL_H - 0.06))
    stool.box(mats["WoodDark"], (0, 0, STOOL_H * 0.35), (0.4, 0.04, 0.05))
    stool.build()


KINDS = {
    "jeremy": (jeremy, {
        "Hair": (0.35, 0.22, 0.14),
        "Trousers": (0.3, 0.33, 0.45),
        "Coat": (0.97, 0.97, 0.95),
        "Lapel": (0.86, 0.88, 0.9),
        "Shirt": (0.55, 0.75, 0.95),
        "Tie": (0.85, 0.22, 0.25),
        "Button": (0.5, 0.55, 0.65),
        "Badge": (1.0, 1.0, 1.0),
        "BadgeStripe": (0.3, 0.55, 0.9),
        "Tube": (0.2, 0.22, 0.3),
        "Metal": (0.78, 0.82, 0.88),
    }),
    "millie": (millie, {
        "Hair": (0.98, 0.72, 0.4),
        "EarInner": (1.0, 0.62, 0.7),
        "Dress": (0.55, 0.82, 0.62),
        "Frill": (1.0, 0.97, 0.9),
        "Spots": (1.0, 0.97, 0.9),
        "Bow": (0.98, 0.35, 0.55),
        "Legs": (0.98, 0.68, 0.52),
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
        "Legs": (0.36, 0.27, 0.44),
    }),
    "abby": (abby, {
        "Hair": (0.55, 0.3, 0.18),
        "Band": (1.0, 0.45, 0.6),
        "Dress": (0.98, 0.5, 0.62),
        "Collar": (1.0, 0.97, 0.9),
        "Hem": (1.0, 0.97, 0.9),
        "Legs": (0.98, 0.68, 0.52),
    }),
    "pip": (pip, {
        "Hair": (0.85, 0.42, 0.18),
        "Freckle": (0.66, 0.36, 0.22),
        # The mushroom cap, vivid like every mushroom in the game: crisp pure white, pure cherry red
        # spots, clean light cream gills (NpcFigure adds a glossy highlight and rim light, MushroomLook).
        "Hat": (1.0, 1.0, 1.0),
        "Gills": (1.0, 0.95, 0.82),
        "HatSpots": (0.95, 0.06, 0.1),
        "Shirt": (0.92, 0.38, 0.36),
        "Apron": (0.95, 0.8, 0.56),
        "Cup": (0.97, 0.55, 0.68),
        "MuffinTop": (0.72, 0.45, 0.25),
        "Berry": (0.35, 0.38, 0.85),
        "Cherry": (0.9, 0.15, 0.2),
        "Trousers": (0.45, 0.35, 0.3),
        "Wood": (0.8, 0.58, 0.36),
        "WoodDark": (0.55, 0.36, 0.22),
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


wanted = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else list(KINDS)
for name in wanted:
    build(name)
