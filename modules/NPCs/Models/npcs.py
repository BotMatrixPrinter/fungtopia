# Builds the five townsfolk and exports them next to this script:
#   jeremy.glb  millie.glb  lucy.glb  abby.glb  pip.glb
# Run from the repository root:
#   blender -b --factory-startup --python modules/NPCs/Models/npcs.py
# or only some of them:
#   blender -b --factory-startup --python modules/NPCs/Models/npcs.py -- pip
#
# Folk made of simple shapes, in the player characters' style (modules/Player/Models/characters.py,
# from Saria's drawings, floating hands and head like Animal Crossing): a big round head floating
# a few cm over a smooth cone body (a triangle from the front, no neck), two round hands floating
# beside it, two little round feet under it, no arms or legs. The clothes color the cone.
#   Dr. Jeremy: big dark eyes with sparkles, a white doctor's coat with lapels, buttons and a name
#               badge open over a blue shirt and red tie, a stethoscope round the top of the cone.
#   Millie:     cat ears, happy ^ ^ closed eyes, honey bunches, a pink bow, a mint spotted dress
#               with a frilly hem, and a little curled cat tail.
#   Lucy:       a flower in her bob, half-lidded unimpressed eyes, a shirt saying "Pet Store"
#               with little hearts, a skirt as a band round the bottom.
#   Abby:       big dark eyes, a happy smile, hair in a tall top bun, the cone a pink triangle
#               dress with a white collar and hem.
#   Pip:        the cook at the Mushroom Munchies food stall: big dark eyes, rosy freckled cheeks,
#               a puffy white chef's hat shaped like a mushroom cap with red spots, a cream apron
#               with a little muffin printed on it, a wooden spoon in the right hand; standing on
#               a little wooden step stool (to reach over the counter).
#
# Meters, feet at the origin, facing Blender +Y (Godot's -Z, forward, after the glTF export).
# Separate objects with pivots where they move, so the game can animate them: Body (hip), Head
# (neck), LegL / LegR (hip; empty, the feet FootL / FootR hang from them), ArmL / ArmR (shoulder;
# empty, invisible), HandL / HandR (hand center, at the invisible arms' ends). Materials carry
# plain colors only; the game draws them toon-shaded with ink outlines.
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
}

# The rig, as the player characters' (characters.py): the neck (the head's pivot), the hip (the
# body's and legs' pivot), the shoulders and the invisible arms' length to the hands' centers.
HIP_Z = 0.3
NECK_Z = 0.72
HEAD_R = 0.4
HEAD_C = Vector((0, 0, NECK_Z + HEAD_R * 0.92))
SHOULDER_X, SHOULDER_Z = 0.23, NECK_Z - 0.07
ARM_LENGTH = 0.42

# The body: a cone, a triangle from the front, from BODY_BOTTOM (radius BODY_R0) to BODY_TOP
# (radius BODY_R1, rounded off), a few cm below the head (its bottom is at about 0.71 m).
BODY_BOTTOM, BODY_TOP = 0.13, 0.61
BODY_R0, BODY_R1 = 0.3, 0.05
# Where a band round the bottom of the cone (Lucy's skirt, Pip's trousers) ends.
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
    """Text written across the front of the cone at height z, hugging it."""
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


def rig(mats, feet="Shoe"):
    """The invisible arms and legs: empty pivots at the shoulders and hips. The round hands float
    beside the body at the arms' ends (a little below its middle), the little round feet hang from
    the legs under it."""
    for side, name in ((-1, "LegL"), (1, "LegR")):
        x = side * 0.125
        leg = empty(name, (x, 0, HIP_Z + 0.02))
        foot = Part("Foot" + name[-1], leg.location)
        foot.sphere(mats[feet], (x, 0.085, 0.062), (0.095, 0.125, 0.064), segments=24, rings=12)
        foot.build().parent = leg
        bpy.data.objects["Foot" + name[-1]].location = (0, 0, 0)
    for side, name in ((-1, "ArmL"), (1, "ArmR")):
        shoulder = Vector((side * SHOULDER_X, 0, SHOULDER_Z))
        empty(name, shoulder)
        hand = shoulder + Vector((side * 0.13, 0.02, -0.22)).normalized() * ARM_LENGTH
        hand_part = Part("Hand" + name[-1], hand)
        hand_part.sphere(mats["Skin"], hand, (0.082, 0.078, 0.085), segments=24, rings=12)
        hand_part.build()


# Heads (big and round, as the player characters').

def round_head(mats):
    head = Part("Head", (0, 0, NECK_Z))
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

    body = Part("Body", (0, 0, HIP_Z))
    # The white doctor's coat colors the cone, open at the top in a V over a blue shirt and red tie.
    v_low, k = 0.4, 0.42
    body.lathe(mats["Coat"], body_profile())
    body.lathe(mats["Shirt"], body_profile(0.006, v_low, BODY_TOP),
               cuts=(((0, 0, v_low), Vector((1, 0, -k))), ((0, 0, v_low), Vector((-1, 0, -k))),
                     ((0, 0.02, 0), Vector((0, -1, 0)))))
    patch(body, mats["Tie"], 0, 0.5, (0.022, 0.07, 0.01), lift=0.012, segments=12, rings=6)
    patch(body, mats["Tie"], 0, 0.565, (0.026, 0.02, 0.012), lift=0.012, segments=12, rings=6)
    # Lapels along the V, buttons down the front below it, a name badge on the chest.
    for side in (-1, 1):
        x = 0.05
        a = math.degrees(math.atan2(x, body_radius(0.48)))
        patch(body, mats["Lapel"], -side * a, 0.48, (0.03, 0.09, 0.01), lift=0.011, roll=side * 22, segments=12, rings=6)
    for z in (0.34, 0.26, 0.18):
        patch(body, mats["Button"], 0, z, (0.018, 0.018, 0.008), lift=0.006, segments=12, rings=6)
    patch(body, mats["Badge"], 34, 0.37, (0.08, 0.045, 0.012), lift=0.008, roll=-8, box=True)
    patch(body, mats["BadgeStripe"], 34, 0.385, (0.082, 0.012, 0.014), lift=0.009, roll=-8, box=True)
    # The stethoscope: a ring round the top of the cone, two tubes down the front, a shiny chest piece.
    band(body, mats["Tube"], 0.53, 0.012, lift=0.012)
    piece = on_cone(-16, 0.31, 0.02)
    for a in (-38, 30):
        body.capsule(mats["Tube"], on_cone(a, 0.53, 0.014), piece, 0.011, segments=8)
    patch(body, mats["Metal"], -16, 0.31, (0.04, 0.04, 0.02), lift=0.022, segments=16, rings=8)
    body.build()
    rig(mats)


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

    body = Part("Body", (0, 0, HIP_Z))
    # The mint dress colors the cone, with white spots, a frilly hem and a pink bow at the top.
    body.lathe(mats["Dress"], body_profile())
    band(body, mats["Frill"], BODY_BOTTOM + 0.03, 0.026, lift=0.004)
    for a, z in ((20, 0.22), (-40, 0.2), (75, 0.25), (-95, 0.22), (140, 0.24), (-150, 0.25), (180, 0.2),
                 (5, 0.37), (-70, 0.36), (110, 0.38), (-130, 0.4), (35, 0.47)):
        patch(body, mats["Spots"], a, z, (0.035, 0.035, 0.01), lift=0.004, segments=16, rings=8)
    ribbon_bow(body, mats["Bow"], on_cone(0, 0.54, 0.03), 0.07)
    # A little cat tail, curling up from the back of the cone.
    pts = [on_cone(180, 0.24, -0.02), on_cone(175, 0.24, 0.11), on_cone(165, 0.32, 0.19), on_cone(160, 0.44, 0.2),
           on_cone(165, 0.5, 0.14)]
    for a, b in zip(pts, pts[1:]):
        body.capsule(mats["Hair"], a, b, 0.038, segments=10)
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

    body = Part("Body", (0, 0, HIP_Z))
    # The pale pink shirt colors the cone above a skirt band round its bottom, "Pet Store" across
    # the front with little hearts around.
    body.lathe(mats["Shirt"], body_profile(0.0, BAND_TOP - 0.03, BODY_TOP))
    body.lathe(mats["Skirt"], body_profile(0.01, BODY_BOTTOM - 0.006, BAND_TOP))
    cone_text(body, mats["Print"], "Pet", 0.45, 0.07)
    cone_text(body, mats["Print"], "Store", 0.375, 0.07)
    for a, z, s in ((50, 0.3, 0.02), (-45, 0.31, 0.018), (0, 0.295, 0.016), (40, 0.5, 0.016), (-38, 0.51, 0.018)):
        heart(body, mats["Heart"], on_cone(a, z, 0.006), s, cone_normal(a))
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

    body = Part("Body", (0, 0, HIP_Z))
    # The cone is her pink triangle dress, with a white collar at the top and a white hem.
    body.lathe(mats["Dress"], body_profile())
    band(body, mats["Collar"], 0.545, 0.022, lift=0.004)
    for side in (-1, 1):
        patch(body, mats["Collar"], side * 22, 0.51, (0.045, 0.03, 0.01), lift=0.01, roll=-side * 30, segments=12, rings=6)
    band(body, mats["Hem"], BODY_BOTTOM + 0.035, 0.022, lift=0.004)
    body.build()
    rig(mats)


def muffin_print(part, mats, z, size, lift=0.018):
    """A little muffin printed on the front of the cone (the apron) at height z: a pleated pink
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

    body = Part("Body", (0, 0, HIP_Z))
    # The coral shirt colors the cone above a trousers band round its bottom; the cream apron
    # covers the middle of its front from the trousers up to the chest, tied round the cone with a bow at the back.
    body.lathe(mats["Shirt"], body_profile(0.0, BAND_TOP - 0.03, BODY_TOP))
    body.lathe(mats["Trousers"], body_profile(0.01, BODY_BOTTOM - 0.006, BAND_TOP))
    body.lathe(mats["Apron"], body_profile(0.014, BODY_BOTTOM + 0.02, 0.5),
               cuts=(((0, 0.04, 0), Vector((0, -1, 0))), ((0.15, 0, 0), Vector((1, 0, 0))), ((-0.15, 0, 0), Vector((-1, 0, 0)))))
    band(body, mats["Apron"], 0.5, 0.014, lift=0.012)
    band(body, mats["Apron"], 0.3, 0.016, lift=0.016)
    ribbon_bow(body, mats["Apron"], on_cone(180, 0.3, 0.04), 0.05)
    muffin_print(body, mats, 0.36, 0.15)
    body.build()
    rig(mats)

    # A wooden spoon in the right hand, pointing down (into the pot while stirring).
    h = bpy.data.objects["HandR"].location.copy()
    spoon = Part("Spoon", h)
    spoon.capsule(mats["Wood"], h + Vector((0, 0.01, 0.07)), h + Vector((0, 0.01, -0.3)), 0.014, segments=10)
    spoon.sphere(mats["Wood"], h + Vector((0, 0.01, -0.36)), (0.05, 0.022, 0.07), segments=16, rings=8)
    spoon.build().parent = bpy.data.objects["HandR"]
    bpy.data.objects["Spoon"].location = (0, 0, 0)

    # Pip is short, so stands on a little step stool behind the counter: lift everything onto it.
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
    }),
    "abby": (abby, {
        "Hair": (0.55, 0.3, 0.18),
        "Band": (1.0, 0.45, 0.6),
        "Dress": (0.98, 0.5, 0.62),
        "Collar": (1.0, 0.97, 0.9),
        "Hem": (1.0, 0.97, 0.9),
    }),
    "pip": (pip, {
        "Hair": (0.85, 0.42, 0.18),
        "Freckle": (0.66, 0.36, 0.22),
        "Hat": (1.0, 1.0, 0.98),
        "Gills": (0.98, 0.9, 0.78),
        "HatSpots": (0.9, 0.2, 0.22),
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
