# Builds the five townsfolk and exports them next to this script:
#   jeremy.glb  millie.glb  lucy.glb  abby.glb  pip.glb
# Run from the repository root:
#   blender -b --factory-startup --python modules/NPCs/Models/npcs.py
# or only some of them:
#   blender -b --factory-startup --python modules/NPCs/Models/npcs.py -- pip
#
# Grown-ups made of simple shapes, in Saria's style (floating head and floating round hands, like
# Animal Crossing; no visible arms or neck) but with normal proportions and real legs (Tony,
# 2026-10-25: "1.8 meters average", "they do have legs in the drawings", "make them look more
# normal"). For a 1.80 m character: rounded tube legs from the hips (0.90 m) down to rounded shoes,
# a smooth tapered torso from 0.88 to 1.44 m (shoulders 0.40 m wide, waist 0.30, hips 0.34, 0.22
# deep), round hands at the ends of invisible 0.66 m arms from the shoulders (1.40 m), and a round
# head (0.30 m tall, 0.27 wide, centered at 1.64) floating 5 cm over the torso. Each one is that
# rig scaled uniformly by their height / 1.80 (HEIGHTS). The outfits color the torso and legs.
#   Dr. Jeremy: big dark eyes with sparkles, a white doctor's coat with lapels, buttons and a name
#               badge open over a blue shirt and red tie, a stethoscope round his shoulders,
#               trousers.
#   Millie:     cat ears, happy ^ ^ closed eyes, honey bunches, a pink bow, a mint spotted dress
#               with a frilly hem, and a little curled cat tail.
#   Lucy:       a flower in her bob, half-lidded unimpressed eyes, a shirt saying "Pet Store"
#               with little hearts, a blue skirt.
#   Abby:       big dark eyes, a happy smile, hair in a tall top bun, a pink triangle dress with
#               a white collar and hem.
#   Pip:        the cook at the Mushroom Munchies food stall: big dark eyes, rosy freckled cheeks,
#               a puffy white chef's hat shaped like a mushroom cap with red spots, a cream apron
#               with a little muffin printed on it, a wooden spoon in the right hand.
#
# Meters, feet at the origin, facing Blender +Y (Godot's -Z, forward, after the glTF export).
# Separate objects with pivots where they move, so the game can animate them: Body (hip), Head
# (neck), LegL / LegR (hip; the legs, the shoes FootL / FootR hang from them), ArmL / ArmR
# (shoulder; empty, invisible), HandL / HandR (hand center, at the invisible arms' ends). Materials
# carry plain colors only; the game draws them toon-shaded with ink outlines.
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

# Each one's height (feet to the top of the head); the game's NPCsModule.Heights keeps in step.
HEIGHTS = {"jeremy": 1.86, "millie": 1.72, "lucy": 1.78, "abby": 1.75, "pip": 1.70}

# The rig of a 1.80 m character (build() scales it to each one's height): the hips (the body's
# and legs' pivots), the shoulders, the invisible arms' length to the hands' centers, the neck
# (the head's pivot) and the head.
TALL = 1.80
HIP_Z = 0.90
LEG_X, LEG_R = 0.10, 0.065
SHOULDER_X, SHOULDER_Z = 0.22, 1.40
ARM_LENGTH = 0.66
HAND_R = 0.055
NECK_Z = 1.48
HEAD_C = Vector((0, 0, 1.64))
HEAD_W, HEAD_D, HEAD_H = 0.27, 0.28, 0.30
TORSO_BOTTOM, TORSO_TOP = 0.88, 1.44

# The faces and hair are drawn on a sketch head (a round head of radius SKETCH_R centered at
# SKETCH_C over a neck at SKETCH_NECK) and then fitted to the real head (Head.build()).
SKETCH_R = 0.4
SKETCH_NECK = 0.72
SKETCH_C = Vector((0, 0, SKETCH_NECK + SKETCH_R * 0.92))


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


class Head(Part):
    """The head, drawn on the sketch head (SKETCH_C, SKETCH_R, pivot at SKETCH_NECK) and fitted to
    the real one when built: centered at HEAD_C, HEAD_W x HEAD_D x HEAD_H, pivot at the neck."""

    def __init__(self):
        super().__init__("Head", (0, 0, SKETCH_NECK))

    def build(self):
        skin = (SKETCH_R * 2, SKETCH_R * 2 * 0.96, SKETCH_R * 2 * 0.94)
        fit = (Matrix.Translation((0, 0, HEAD_C.z - NECK_Z))
               @ Matrix.Diagonal((HEAD_W / skin[0], HEAD_D / skin[1], HEAD_H / skin[2], 1.0))
               @ Matrix.Translation((0, 0, SKETCH_NECK - SKETCH_C.z)))
        self.bm.transform(fit)
        self.pivot = Vector((0, 0, NECK_Z))
        return super().build()


# The body: smooth shapes of elliptic cross-sections round the Z axis (the torso, skirts).

def loft(part, mat, rings, cuts=(), segments=48):
    """A smooth solid through rings (a, b, z) from bottom to top: ellipses of half width a (x) and
    half depth b (y) at height z, a = 0 a point on the axis (the first and last must be). cuts =
    (point, normal) planes, each removing the part on its normal's side (world coordinates)."""
    def make(g):
        loops = []
        for a, b, z in rings:
            if a < 1e-6:
                loops.append([g.verts.new((0, 0, z))])
            else:
                loops.append([g.verts.new((a * math.cos(i / segments * 2 * math.pi),
                                           b * math.sin(i / segments * 2 * math.pi), z)) for i in range(segments)])
        for lo, hi in zip(loops, loops[1:]):
            for i in range(segments):
                j = (i + 1) % segments
                if len(lo) == 1 and len(hi) > 1:
                    g.faces.new((lo[0], hi[j], hi[i]))
                elif len(hi) == 1 and len(lo) > 1:
                    g.faces.new((lo[i], lo[j], hi[0]))
                elif len(lo) > 1:
                    g.faces.new((lo[i], lo[j], hi[j], hi[i]))
        bmesh.ops.recalc_face_normals(g, faces=g.faces)
        for point, normal in cuts:
            bmesh.ops.bisect_plane(g, geom=g.verts[:] + g.edges[:] + g.faces[:],
                                   plane_co=Vector(point), plane_no=Vector(normal), clear_outer=True)
    part._add(make, mat, Matrix.Translation(-part.pivot))


def _cap(t, p=2.2):
    """A superellipse quarter: the width left at t of the way into a rounded end."""
    return (1 - min(1.0, t) ** p) ** (1 / p)


class Shape:
    """A smooth rounded shape: keys (z, a, b) from bottom to top, a smooth curve through them, then
    rounded ends bottom_round below the first and top_round above the last."""

    def __init__(self, keys, bottom_round, top_round):
        table = []
        # The rounded bottom, from the axis up to the first key.
        z, a, b = keys[0]
        for k in range(8):
            t = math.cos(k / 8 * math.pi / 2)
            table.append((z - bottom_round * t, a * _cap(t), b * _cap(t)))
        # A Catmull-Rom curve through the keys.
        pts = [keys[0]] + list(keys) + [keys[-1]]
        for i in range(1, len(pts) - 2):
            p0, p1, p2, p3 = (Vector(p) for p in pts[i - 1:i + 3])
            steps = max(2, int((p2.x - p1.x) / 0.01))
            for k in range(steps):
                t = k / steps
                q = 0.5 * (2 * p1 + (p2 - p0) * t + (2 * p0 - 5 * p1 + 4 * p2 - p3) * t * t
                           + (3 * p1 - p0 - 3 * p2 + p3) * t ** 3)
                table.append(tuple(q))
        table.append(tuple(keys[-1]))
        z, a, b = keys[-1]
        for k in range(1, 9):
            t = math.sin(k / 8 * math.pi / 2)
            table.append((z + top_round * t, a * _cap(t), b * _cap(t)))
        self.table = table
        self.z0, self.z1 = table[0][0], table[-1][0]

    def ab(self, z):
        """The half width and depth at height z."""
        t = self.table
        if z <= t[0][0]:
            return t[0][1], t[0][2]
        for (z0, a0, b0), (z1, a1, b1) in zip(t, t[1:]):
            if z <= z1:
                f = (z - z0) / max(z1 - z0, 1e-9)
                return a0 + (a1 - a0) * f, b0 + (b1 - b0) * f
        return t[-1][1], t[-1][2]

    def rings(self, offset=0.0, z0=None, z1=None):
        """The rings for loft(), offset out by offset (for things over it) from z0 to z1: rounded
        at the shape's true ends, flat where it is cut off."""
        whole_bottom = z0 is None or z0 <= self.z0
        whole_top = z1 is None or z1 >= self.z1
        out = []
        if not whole_bottom:
            a, b = self.ab(z0)
            out += [(0, 0, z0), (a + offset, b + offset, z0)]
        for z, a, b in self.table:
            if (whole_bottom or z > z0) and (whole_top or z < z1):
                if a < 1e-6:
                    out.append((0, 0, z - offset if z == self.z0 else z + offset))
                else:
                    out.append((a + offset, b + offset, z))
        if not whole_top:
            a, b = self.ab(z1)
            out += [(a + offset, b + offset, z1), (0, 0, z1)]
        return out

    def _at(self, angle, z):
        t = math.radians(angle + 90)
        a, b = self.ab(z)
        return Vector((a * math.cos(t), b * math.sin(t), z))

    def normal(self, angle, z):
        """The outward normal angle degrees round from the front (+Y; positive toward -X, the left)."""
        z = min(max(z, self.z0 + 0.004), self.z1 - 0.004)
        along = self._at(angle + 1, z) - self._at(angle - 1, z)
        up = self._at(angle, z + 0.003) - self._at(angle, z - 0.003)
        return along.cross(up).normalized()

    def point(self, angle, z, lift=0.0):
        """The point on the side angle degrees round from the front at height z, lifted off it."""
        return self._at(angle, z) + self.normal(angle, z) * lift

    def front(self, x, z, lift=0.0):
        """The point on the front at x (right +), height z, lifted off it (out along +Y)."""
        a, b = self.ab(z)
        a, b = a + lift, b + lift
        return Vector((x, b * math.sqrt(max(0.0, 1 - (x / a) ** 2)), z))

    def band(self, part, mat, z, minor, lift=0.0):
        """A ring round the shape at height z."""
        a, b = self.ab(z)
        a, b = a + lift, b + lift

        def make(g):
            loops = []
            for i in range(48):
                t = i / 48 * 2 * math.pi
                c = Vector((a * math.cos(t), b * math.sin(t), z))
                out = Vector((math.cos(t) / a, math.sin(t) / b, 0)).normalized()
                loops.append([g.verts.new(c + out * math.cos(k / 10 * 2 * math.pi) * minor
                                          + Vector((0, 0, math.sin(k / 10 * 2 * math.pi) * minor))) for k in range(10)])
            for i in range(48):
                for k in range(10):
                    g.faces.new((loops[i][k], loops[(i + 1) % 48][k], loops[(i + 1) % 48][(k + 1) % 10], loops[i][(k + 1) % 10]))
            bmesh.ops.recalc_face_normals(g, faces=g.faces)
        part._add(make, mat, Matrix.Translation(-part.pivot))

    def patch(self, part, mat, angle, z, size, lift=0.006, roll=0.0, segments=16, rings=8, box=False):
        """A flat shape lying on the side: size is (across, up, thickness) radii (a box: full
        extents), roll turns it about the normal (degrees)."""
        normal = self.normal(angle, z)
        rot = normal.to_track_quat("Z", "Y").to_matrix().to_4x4() @ Matrix.Rotation(math.radians(roll), 4, "Z")
        center = self.point(angle, z, lift)
        if box:
            part.box(mat, center, size, rotation=rot)
        else:
            part.sphere(mat, center, size, rotation=rot, segments=segments, rings=rings)

    def text(self, part, mat, text, z, size, lift=0.006):
        """Text written across the front at height z, hugging it."""
        curve = bpy.data.curves.new("Text", type="FONT")
        curve.body = text
        curve.size = size
        curve.extrude = 0.003
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
            # Text space: x right, y up, z out of the page; on the front (seen from the front, +Y):
            # right is -X, up +Z, out +Y.
            v.co = self.front(-v.co.x, z + v.co.y, lift + v.co.z)
        part.world_mesh(mat, geom)


# The torso: hips 0.34 m wide, a 0.30 m waist, 0.40 m shoulders, about 0.22 m deep, rounded off at
# the hips (0.88 m) and over the shoulders (1.44 m).
TORSO = Shape([(0.90, 0.170, 0.112), (0.97, 0.165, 0.106), (1.08, 0.150, 0.100), (1.18, 0.163, 0.108),
               (1.27, 0.186, 0.112), (1.36, 0.200, 0.110)], 0.02, TORSO_TOP - 1.36)


def skirt(top, hem, a_hem, b_hem):
    """A skirt (or coat tails) from inside the torso at height top, past the hips, flaring out to
    its hem (half width a_hem, half depth b_hem)."""
    a0, b0 = TORSO.ab(top)
    a1, b1 = TORSO.ab(0.92)
    return Shape([(hem + 0.02, a_hem, b_hem), (0.92, a1 + 0.012, b1 + 0.012), (top, a0 - 0.006, b0 - 0.006)], 0.02, 0.0)


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


def rig(mats, legs="Skin"):
    """The legs and invisible arms. The legs are rounded tubes (colored legs: skin, trousers...)
    hanging from the hips down into rounded shoes (FootL / FootR, their children). The arms are
    empty pivots at the shoulders; the round hands float at their ends, beside the thighs."""
    for side, name in ((-1, "LegL"), (1, "LegR")):
        hip = Vector((side * LEG_X, 0, HIP_Z))
        leg = Part(name, hip)
        leg.capsule(mats[legs], hip + Vector((0, 0, 0.0)), (hip.x, 0, 0.1), LEG_R, segments=20)
        leg_obj = leg.build()
        # The shoe: rounded, 0.12 m tall and 0.24 m long, flat underneath, its toe forward.
        foot = Part("Foot" + name[-1], hip)
        foot.sphere(mats["Shoe"], (hip.x + side * 0.004, 0.045, 0.05), (0.072, 0.12, 0.07), segments=24, rings=12,
                    cut=((0, 0, 0), (0, 0, -1)))
        foot.build().parent = leg_obj
        bpy.data.objects["Foot" + name[-1]].location = (0, 0, 0)
    for side, name in ((-1, "ArmL"), (1, "ArmR")):
        shoulder = Vector((side * SHOULDER_X, 0, SHOULDER_Z))
        empty(name, shoulder)
        hand = shoulder + Vector((side * 0.12, 0.02, -0.64)).normalized() * ARM_LENGTH
        hand_part = Part("Hand" + name[-1], hand)
        hand_part.sphere(mats["Skin"], hand, (HAND_R, HAND_R * 0.95, HAND_R * 1.03), segments=24, rings=12)
        hand_part.build()


# Heads (round, the face drawn on the sketch head and fitted to the real one).

def round_head(mats):
    head = Head()
    head.sphere(mats["Skin"], SKETCH_C, (SKETCH_R, SKETCH_R * 0.96, SKETCH_R * 0.94), segments=40, rings=24)
    return head


def round_eyes(head, mats):
    """Big dark oval eyes with sparkles (like the player characters)."""
    for side in (-1, 1):
        eye_c = SKETCH_C + Vector((side * 0.15, SKETCH_R * 0.86, -0.04))
        tilt = Matrix.Rotation(math.radians(side * -20), 4, "Z")
        head.sphere(mats["Eye"], eye_c, (0.08, 0.06, 0.115), rotation=tilt, segments=24, rings=12)
        head.sphere(mats["EyeShine"], eye_c + Vector((-0.022, 0.05, 0.045)), (0.03, 0.02, 0.034), segments=12, rings=6)
        head.sphere(mats["EyeShine"], eye_c + Vector((0.025, 0.055, -0.035)), (0.014, 0.01, 0.014), segments=10, rings=5)


def happy_eyes(head, mats):
    """Closed happy ^ ^ eyes: little arches."""
    for side in (-1, 1):
        eye_c = SKETCH_C + Vector((side * 0.15, SKETCH_R * 0.9, -0.05))
        head.torus(mats["Eye"], eye_c, 0.06, 0.016,
                   rotation=Matrix.Rotation(math.radians(90), 4, "X") @ Matrix.Rotation(math.radians(90), 4, "Z")
                   @ Matrix.Rotation(math.radians(side * 4), 4, "Z"), arc=0.42)


def sleepy_eyes(head, mats):
    """Half-lidded unimpressed eyes: the top half of each eye is hidden under a flat lid."""
    for side in (-1, 1):
        eye_c = SKETCH_C + Vector((side * 0.15, SKETCH_R * 0.86, -0.06))
        head.sphere(mats["Eye"], eye_c, (0.075, 0.06, 0.1), segments=24, rings=12,
                    cut=(eye_c + Vector((0, 0, -0.005)), Vector((0, 0, 1))))
        head.sphere(mats["EyeShine"], eye_c + Vector((-0.02, 0.055, -0.03)), (0.02, 0.012, 0.02), segments=10, rings=5)
        # The lid: a heavy line across the cut.
        head.capsule(mats["Lid"], eye_c + Vector((-0.085, 0.01, -0.004)), eye_c + Vector((0.085, 0.01, -0.004)),
                     0.016, segments=10)


def blush(head, mats):
    for side in (-1, 1):
        head.sphere(mats["Blush"], SKETCH_C + Vector((side * 0.25, SKETCH_R * 0.74, -0.15)), (0.06, 0.025, 0.035),
                    rotation=Matrix.Rotation(math.radians(side * -38), 4, "Z"), segments=16, rings=8)


def smile(head, mats, big=False):
    head.torus(mats["Mouth"], SKETCH_C + Vector((0, SKETCH_R * 0.92, -0.17)), 0.045 if big else 0.035, 0.01,
               rotation=Matrix.Rotation(math.radians(90), 4, "X") @ Matrix.Rotation(math.radians(-90), 4, "Z"),
               arc=0.45 if big else 0.42)


def hair_cap(head, mats, fringe=True, back=1.0):
    head.sphere(mats["Hair"], SKETCH_C + Vector((0, -0.02, 0.03)), (SKETCH_R * 1.07, SKETCH_R * 1.06 * back, SKETCH_R * 1.04),
                segments=40, rings=24, cut=(SKETCH_C + Vector((0, 0.1, -0.1)), Vector((0, 0.85, -0.53))))
    if fringe:
        for x in (-0.22, -0.08, 0.08, 0.22):
            head.sphere(mats["Hair"], SKETCH_C + Vector((x, SKETCH_R * 0.8 - abs(x) * 0.35, 0.24 - abs(x) * 0.3)),
                        (0.1, 0.07, 0.075), segments=16, rings=8)


def freckles(head, mats):
    """A few freckles across each rosy cheek and the nose."""
    for side in (-1, 1):
        for dx, dz in ((0.0, 0.0), (0.045, 0.02), (0.035, -0.03), (-0.04, 0.015)):
            head.sphere(mats["Freckle"], SKETCH_C + Vector((side * (0.22 + dx), SKETCH_R * 0.8 - dx * 0.4, -0.11 + dz)),
                        (0.011, 0.008, 0.011), segments=8, rings=4)


# The five.

def jeremy(mats):
    head = round_head(mats)
    round_eyes(head, mats)
    blush(head, mats)
    smile(head, mats)
    hair_cap(head, mats, fringe=False)
    # A neat side parting: a swoop of hair over the forehead.
    head.sphere(mats["Hair"], SKETCH_C + Vector((-0.08, SKETCH_R * 0.62, 0.27)), (0.26, 0.12, 0.09),
                rotation=Matrix.Rotation(math.radians(-12), 4, "Y"), segments=20, rings=10)
    # Little ears.
    for side in (-1, 1):
        head.sphere(mats["Skin"], SKETCH_C + Vector((side * SKETCH_R * 0.97, 0.02, -0.06)), (0.05, 0.04, 0.07),
                    segments=12, rings=6)
    head.build()

    body = Part("Body", (0, 0, HIP_Z))
    # The white doctor's coat over the torso, its tails down to the knees, open at the top in a V
    # over a blue shirt and red tie.
    tails = skirt(1.0, 0.6, 0.205, 0.15)
    loft(body, mats["Coat"], TORSO.rings())
    loft(body, mats["Coat"], tails.rings())
    v_low, k = 1.2, 0.42
    loft(body, mats["Shirt"], TORSO.rings(0.004, v_low, None),
         cuts=(((0, 0, v_low), Vector((1, 0, -k))), ((0, 0, v_low), Vector((-1, 0, -k))),
               ((0, 0.02, 0), Vector((0, -1, 0)))))
    TORSO.patch(body, mats["Tie"], 0, 1.3, (0.017, 0.055, 0.008), lift=0.009, segments=12, rings=6)
    TORSO.patch(body, mats["Tie"], 0, 1.37, (0.02, 0.014, 0.009), lift=0.009, segments=12, rings=6)
    # Lapels along the V, buttons down the front below it and the coat tails, a name badge on the chest.
    for side in (-1, 1):
        a = math.degrees(math.asin(0.04 / TORSO.ab(1.3)[0]))
        TORSO.patch(body, mats["Lapel"], -side * a, 1.3, (0.02, 0.065, 0.008), lift=0.008, roll=side * 22, segments=12, rings=6)
    for z in (1.13, 1.04, 0.95):
        TORSO.patch(body, mats["Button"], 0, z, (0.012, 0.012, 0.006), lift=0.005, segments=12, rings=6)
    for z in (0.84, 0.74):
        tails.patch(body, mats["Button"], 0, z, (0.012, 0.012, 0.006), lift=0.005, segments=12, rings=6)
    TORSO.patch(body, mats["Badge"], 36, 1.24, (0.06, 0.034, 0.008), lift=0.006, roll=-6, box=True)
    TORSO.patch(body, mats["BadgeStripe"], 36, 1.252, (0.062, 0.009, 0.01), lift=0.007, roll=-6, box=True)
    # The stethoscope: a ring round his shoulders, two tubes down the front, a shiny chest piece.
    TORSO.band(body, mats["Tube"], 1.415, 0.009, lift=0.008)
    piece = TORSO.point(-16, 1.17, 0.016)
    for a in (-40, 32):
        bend = TORSO.point(a * 0.6 - 6, 1.3, 0.018)
        body.capsule(mats["Tube"], TORSO.point(a, 1.415, 0.01), bend, 0.008, segments=8)
        body.capsule(mats["Tube"], bend, piece, 0.008, segments=8)
    TORSO.patch(body, mats["Metal"], -16, 1.17, (0.03, 0.03, 0.014), lift=0.018, segments=16, rings=8)
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
        head.sphere(mats["Hair"], SKETCH_C + Vector((side * 0.38, -0.06, -0.18)), (0.12, 0.12, 0.17), segments=16, rings=8)
    # Cat ears: hair-colored triangles with pink insides.
    for side in (-1, 1):
        tip_dir = Matrix.Rotation(math.radians(side * -24), 4, "Y")
        base = SKETCH_C + Vector((side * 0.25, -0.02, SKETCH_R * 0.7))
        m = Matrix.Translation(base) @ tip_dir
        head._add(lambda g: bmesh.ops.create_cone(g, cap_ends=True, segments=12, radius1=0.16, radius2=0.0,
                                                  depth=0.32), mats["Hair"], Matrix.Translation(-head.pivot) @ m
                  @ Matrix.Translation((0, 0, 0.16)) @ Matrix.Diagonal((1.0, 0.55, 1.0, 1.0)))
        head._add(lambda g: bmesh.ops.create_cone(g, cap_ends=True, segments=12, radius1=0.1, radius2=0.0,
                                                  depth=0.2), mats["EarInner"], Matrix.Translation(-head.pivot) @ m
                  @ Matrix.Translation((0, 0.06, 0.17)) @ Matrix.Diagonal((1.0, 0.3, 1.0, 1.0)))
    head.build()

    body = Part("Body", (0, 0, HIP_Z))
    # The mint dress over the torso and a flared skirt, with white spots, a frilly hem and a pink
    # bow at the top.
    dress = skirt(1.0, 0.62, 0.27, 0.23)
    loft(body, mats["Dress"], TORSO.rings())
    loft(body, mats["Dress"], dress.rings())
    dress.band(body, mats["Frill"], 0.645, 0.02, lift=0.004)
    for a, z in ((20, 0.74), (-40, 0.72), (75, 0.78), (-95, 0.74), (140, 0.76), (-150, 0.79), (180, 0.72),
                 (5, 0.86), (-70, 0.88), (110, 0.86), (-130, 0.9)):
        dress.patch(body, mats["Spots"], a, z, (0.026, 0.026, 0.008), lift=0.004, segments=16, rings=8)
    for a, z in ((10, 1.12), (-60, 1.18), (60, 1.22), (-25, 1.26), (120, 1.15), (-140, 1.2), (180, 1.27)):
        TORSO.patch(body, mats["Spots"], a, z, (0.024, 0.024, 0.008), lift=0.004, segments=16, rings=8)
    ribbon_bow(body, mats["Bow"], TORSO.point(0, 1.33, 0.02), 0.045)
    # A little cat tail, curling up from the back of the skirt.
    pts = [dress.point(180, 0.78, -0.02), dress.point(178, 0.78, 0.07), dress.point(172, 0.84, 0.14),
           dress.point(166, 0.94, 0.16), dress.point(170, 1.0, 0.12)]
    for a, b in zip(pts, pts[1:]):
        body.capsule(mats["Hair"], a, b, 0.026, segments=10)
    body.build()
    rig(mats)


def lucy(mats):
    head = round_head(mats)
    sleepy_eyes(head, mats)
    # A small flat unimpressed mouth.
    head.capsule(mats["Mouth"], SKETCH_C + Vector((-0.04, SKETCH_R * 0.93, -0.18)), SKETCH_C + Vector((0.04, SKETCH_R * 0.93, -0.18)),
                 0.009, segments=8)
    hair_cap(head, mats, fringe=False)
    # A straight-cut fringe and a bob down to the chin.
    head.sphere(mats["Hair"], SKETCH_C + Vector((0, SKETCH_R * 0.55, 0.22)), (0.34, 0.16, 0.11), segments=24, rings=12)
    for side in (-1, 1):
        head.sphere(mats["Hair"], SKETCH_C + Vector((side * 0.33, -0.02, -0.14)), (0.13, 0.3, 0.22), segments=20, rings=10)
    # A flower in her hair: five petals round a yellow middle, above her right ear.
    flower = SKETCH_C + Vector((0.24, 0.27, 0.25))
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
    # The pale pink shirt over the torso above a blue skirt, "Pet Store" across the front with
    # little hearts around.
    loft(body, mats["Shirt"], TORSO.rings(0.0, 0.95, None))
    loft(body, mats["Skirt"], skirt(1.0, 0.68, 0.22, 0.18).rings())
    TORSO.text(body, mats["Print"], "Pet", 1.295, 0.058)
    TORSO.text(body, mats["Print"], "Store", 1.222, 0.058)
    for a, z, s in ((48, 1.17, 0.014), (-45, 1.18, 0.013), (0, 1.155, 0.012), (40, 1.35, 0.012), (-38, 1.355, 0.013)):
        heart(body, mats["Heart"], TORSO.point(a, z, 0.005), s, TORSO.normal(a, z))
    body.build()
    rig(mats)


def abby(mats):
    head = round_head(mats)
    round_eyes(head, mats)
    blush(head, mats)
    smile(head, mats, big=True)
    hair_cap(head, mats)
    # A tall top bun: a round bun on a little hair stack, tied with a band.
    top = SKETCH_C + Vector((0, -0.03, SKETCH_R * 0.98))
    head.sphere(mats["Hair"], top, (0.14, 0.13, 0.1), segments=24, rings=12)
    head.sphere(mats["Band"], top + Vector((0, 0, 0.08)), (0.1, 0.095, 0.035), segments=20, rings=8)
    head.sphere(mats["Hair"], top + Vector((0, 0, 0.2)), (0.17, 0.16, 0.16), segments=28, rings=14)
    head.build()

    body = Part("Body", (0, 0, HIP_Z))
    # Her pink triangle dress: the torso and a skirt flaring wide, a white collar and a white hem.
    dress = skirt(1.02, 0.56, 0.28, 0.23)
    loft(body, mats["Dress"], TORSO.rings())
    loft(body, mats["Dress"], dress.rings())
    TORSO.band(body, mats["Collar"], 1.405, 0.014, lift=0.004)
    for side in (-1, 1):
        TORSO.patch(body, mats["Collar"], side * 20, 1.375, (0.032, 0.02, 0.008), lift=0.008, roll=-side * 30,
                    segments=12, rings=6)
    dress.band(body, mats["Hem"], 0.59, 0.018, lift=0.004)
    body.build()
    rig(mats)


def muffin_print(part, mats, z, size, lift=0.018):
    """A little muffin printed on the front of the torso (the apron) at height z: a pleated pink
    cup, a domed brown top with blue berries and a red cherry."""
    def on(x, dz, more=0.0):
        return TORSO.front(x, z + dz, lift + more)
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


def pip(mats):
    head = round_head(mats)
    round_eyes(head, mats)
    blush(head, mats)
    freckles(head, mats)
    smile(head, mats, big=True)
    # Short ginger hair under the hat: the back of the head, a fringe and tufts over the ears.
    head.sphere(mats["Hair"], SKETCH_C + Vector((0, -0.04, 0.0)), (SKETCH_R * 1.02, SKETCH_R, SKETCH_R * 0.96),
                segments=40, rings=24, cut=(SKETCH_C + Vector((0, 0.12, 0.0)), Vector((0, 1, 0))))
    for x in (-0.24, -0.12, 0.0, 0.12, 0.24):
        head.sphere(mats["Hair"], SKETCH_C + Vector((x, SKETCH_R * 0.78 - abs(x) * 0.4, 0.22 - abs(x) * 0.2)),
                    (0.085, 0.06, 0.06), segments=14, rings=7)
    for side in (-1, 1):
        head.sphere(mats["Hair"], SKETCH_C + Vector((side * SKETCH_R * 0.92, -0.04, 0.05)), (0.08, 0.17, 0.15),
                    segments=16, rings=8)
        head.sphere(mats["Skin"], SKETCH_C + Vector((side * SKETCH_R * 0.97, 0.03, -0.08)), (0.05, 0.04, 0.07),
                    segments=12, rings=6)
    # The chef's hat: a white band round the top of the head under a puffy white mushroom cap,
    # puffed up like a toque, with a few red spots and cream gills beneath its rim.
    band_z = SKETCH_C.z + SKETCH_R * 0.6
    head.cone(mats["Hat"], (0, -0.01, band_z), SKETCH_R * 0.86, SKETCH_R * 0.92, 0.12, segments=40)
    cap_c = Vector((0, -0.01, band_z + 0.1))
    cap_r = (0.47, 0.45, 0.36)
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
    # The coral shirt over the torso, the trousers from the hips down the legs; the cream apron
    # covers the front from the chest down to the knees, tied round the waist with a bow at the back.
    loft(body, mats["Shirt"], TORSO.rings(0.0, 0.96, None))
    loft(body, mats["Trousers"], TORSO.rings(0.004, None, 0.98))
    loft(body, mats["Apron"], TORSO.rings(0.012, 0.93, 1.3),
         cuts=(((0, 0.04, 0), Vector((0, -1, 0))), ((0.14, 0, 0), Vector((1, 0, 0))), ((-0.14, 0, 0), Vector((-1, 0, 0)))))
    hips = TORSO.ab(0.92)[1]
    body.box(mats["Apron"], (0, hips + 0.02, 0.765), (0.28, 0.014, 0.33))
    TORSO.band(body, mats["Apron"], 1.0, 0.01, lift=0.016)
    ribbon_bow(body, mats["Apron"], TORSO.point(180, 1.0, 0.03), 0.035)
    for side in (-1, 1):
        body.capsule(mats["Apron"], TORSO.front(side * 0.12, 1.29, 0.014), TORSO.point(-side * 55, 1.42, 0.008), 0.009,
                     segments=8)
    muffin_print(body, mats, 1.13, 0.1)
    body.build()
    rig(mats, legs="Trousers")

    # A wooden spoon in the right hand, pointing down (into the pot while stirring).
    h = bpy.data.objects["HandR"].location.copy()
    spoon = Part("Spoon", h)
    spoon.capsule(mats["Wood"], h + Vector((0, 0.01, 0.07)), h + Vector((0, 0.01, -0.3)), 0.014, segments=10)
    spoon.sphere(mats["Wood"], h + Vector((0, 0.01, -0.36)), (0.05, 0.022, 0.07), segments=16, rings=8)
    spoon.build().parent = bpy.data.objects["HandR"]
    bpy.data.objects["Spoon"].location = (0, 0, 0)


KINDS = {
    "jeremy": (jeremy, {
        "Hair": (0.35, 0.22, 0.14),
        "Trousers": (0.3, 0.33, 0.42),
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
    }),
}


def build(kind):
    bpy.ops.wm.read_factory_settings(use_empty=True)
    make, colors = KINDS[kind]
    mats = {name: material(name, color) for name, color in COLORS.items()}
    mats.update({name: material(name, color) for name, color in colors.items()})
    make(mats)
    # The same rig scaled uniformly to their height (the shapes and the pivots alike).
    s = HEIGHTS[kind] / TALL
    for obj in bpy.data.objects:
        obj.location *= s
        if obj.type == "MESH":
            obj.data.transform(Matrix.Scale(s, 4))
    out = os.path.join(HERE, f"{kind}.glb")
    bpy.ops.export_scene.gltf(filepath=out, export_format="GLB", export_apply=True,
                              export_yup=True, export_materials="EXPORT")
    print(f"npcs.py: wrote {out}")


wanted = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else list(KINDS)
for name in wanted:
    build(name)
