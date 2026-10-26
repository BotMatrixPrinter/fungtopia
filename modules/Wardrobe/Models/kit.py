# Shared helpers for the Wardrobe's model scripts (one script per clothing item or hair style,
# each exporting <its name>.glb next to itself). Not run on its own.
#
# Meters, feet at the origin, facing Blender +Y (Godot's -Z after the glTF export), sized for the
# Player module's simple-shapes rig (modules/Player/Models/characters.py): a big round ball of a
# head (radius 0.19 round z 1.28) floating 4 cm over the body, a body that is a solid of
# revolution (the Girl's a tall cone, a triangle from the front; the Boy's a tall rounded capsule),
# big oval hands floating well out beside it, no arms, and two straight tube legs coming out from
# under it down to rounded feet. Each piece is one object per rig part it covers, named like the
# part and with its origin at that part's pivot, so the game parents it to the part with no offset
# (Fungtopia.Wardrobe.Attach):
#   Head (pivot at the neck, z 1.07: hats, glasses, clips, hair),
#   Body (pivot at the hips, z 0.42: tops and dresses over the body, the bottoms' band round its
#         lower edge, backpacks on its back, the scarf round its top). The two bodies differ, so
#         an item that fits both characters holds two body objects instead: BodyGirl (over the
#         cone) and BodyBoy (over the capsule), both at the Body pivot (see bodies()); a Girl-only
#         item has one Body, over the cone.
#   LegL / LegR (pivots at the hips, x -0.08 / +0.08, z 0.42; the tube legs and the rounded feet
#         hanging from them: trouser legs round the tubes, the shoes over the feet).
# Clothes on the body follow its outline (body_profile) a little out from it, so nothing pokes
# through it; there are no sleeves (no arms). Materials carry plain colors only; the game draws
# them toon-shaded with ink outlines. A hair style's main material is named "Hair" so the game can
# tint it with the chosen hair color. No eyes or faces on clothes: only creatures have eyes.
import math
import os
import sys

import bmesh
import bpy
from mathutils import Matrix, Vector

# The rig, from characters.py.
HIP_Z = 0.42
NECK_Z = 1.07
LEG_X, LEG_R = 0.08, 0.06
PIVOTS = {
    "Head": (0, 0, NECK_Z),
    "Body": (0, 0, HIP_Z),
    "BodyGirl": (0, 0, HIP_Z),
    "BodyBoy": (0, 0, HIP_Z),
    "LegL": (-LEG_X, 0, HIP_Z),
    "LegR": (LEG_X, 0, HIP_Z),
}
# The head: a ball of RIG_HEAD_R round RIG_HEAD_C. Head pieces are drawn on a bigger design head
# (HEAD_R round HEAD_C, the rig's old head) and Part("Head") scales them uniformly onto the rig's,
# so every hat, clip, pair of glasses and hair style keeps its shape. The eyes (big dark ovals, low
# on the face and wide apart) look out along EYE_DIRS from the head's center.
RIG_HEAD_R, RIG_HEAD_C = 0.19, Vector((0, 0, 1.28))
HEAD_R, HEAD_C = 0.4, Vector((0, 0, 1.088))
HEAD_SCALE = RIG_HEAD_R / HEAD_R
EYE_DIRS = [Vector((side * 0.42, 0.89, -0.17)).normalized() for side in (-1, 1)]
# The body: a solid of revolution from BODY_BOTTOM (radius R0) to BODY_TOP (radius R1), its
# bottom rim and its top rounded off by ROUND (bottom, top), per character. bodies() (or
# use_body()) picks the one the shapes below follow.
BODY_BOTTOM, BODY_TOP = 0.36, 1.05
BODY_H = BODY_TOP - BODY_BOTTOM
BODIES = {
    "girl": {"R0": 0.21, "R1": 0.035, "ROUND": (0.05, 0.09)},
    "boy": {"R0": 0.16, "R1": 0.16, "ROUND": (0.08, 0.14)},
}
BODY = BODIES["girl"]
# The head's bottom is at 1.09 m: clothes on the body stay below HEAD_GAP.
HEAD_GAP = RIG_HEAD_C.z - RIG_HEAD_R - 0.03


def at(f):
    """The height a fraction f of the way up the body."""
    return BODY_BOTTOM + f * BODY_H


# Bottoms are the lower band of the body, up to BAND_TOP; tops come down to TOP_HEM, under it.
BAND_TOP = at(0.29)
TOP_HEM = BAND_TOP - 0.04
# How far out from the body clothes sit: tops and dresses (under the bow's strap, 1.2 cm out),
# then bottoms over them. Layers over a top (stripes, a vest) stay within TOP_OVER of it, still
# under the strap.
TOP_OUT = 0.004
TOP_OVER = TOP_OUT + 0.004
BOTTOM_OUT = 0.018
# Cloth wrapping under the body's bottom rim stops this far from the axis, clear of the legs.
WRAP_R = LEG_X + LEG_R + 0.012
# Trouser legs: round the leg tubes, LEG_OUT out from them, from just under the body down.
LEG_OUT = 0.012
LEG_TOP = BODY_BOTTOM - 0.004
# The rounded feet (each under its leg's pivot): center y, z and radii, from characters.py.
FOOT_C = (0.025, 0.056)
FOOT_R = (0.075, 0.1, 0.056)
# The hands float beside the body, about (+-0.331, 0.03, 0.69) with radii (0.07, 0.055, 0.09):
# clothes keep clear of them (straps pass well inside them).

# Vivid mushrooms (the game's rule; the same values as Core/MushroomLook.cs): a saturated cherry-red
# cap, crisp pure white spots, a clean light cream stem and gills.
CAP_RED = (0.95, 0.06, 0.1)
SPOT_WHITE = (1.0, 1.0, 1.0)
STEM_CREAM = (1.0, 0.95, 0.84)


def reset():
    bpy.ops.wm.read_factory_settings(use_empty=True)


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


class Part:
    """Gathers shapes into one mesh object (named like a rig part) with several materials. Shapes
    are given in world coordinates; on the Head they are drawn on the design head (HEAD_C,
    HEAD_R) and scaled onto the rig's."""

    def __init__(self, name):
        self.name = name
        self.pivot = Vector(PIVOTS[name])
        self.bm = bmesh.new()
        self.materials = []
        self.design = Matrix.Identity(4)
        if name == "Head":
            # Design head to rig head, about the pivot (the shapes' matrices are pivot-relative).
            self.design = (Matrix.Translation(RIG_HEAD_C - self.pivot) @ Matrix.Scale(HEAD_SCALE, 4) @
                           Matrix.Translation(self.pivot - HEAD_C))

    def _slot(self, mat):
        if mat not in self.materials:
            self.materials.append(mat)
        return self.materials.index(mat)

    def _add(self, make, mat, matrix):
        geom = bmesh.new()
        make(geom)
        geom.transform(self.design @ matrix)
        slot = self._slot(mat)
        for face in geom.faces:
            face.material_index = slot
        mesh = bpy.data.meshes.new("tmp")
        geom.to_mesh(mesh)
        geom.free()
        self.bm.from_mesh(mesh)
        bpy.data.meshes.remove(mesh)

    def sphere(self, mat, center, size, rotation=Matrix.Identity(4), segments=32, rings=16, cut=None, keep=None,
               fill=False):
        """An ellipsoid with (x, y, z) radii. cut = (point, normal) removes the part on the
        normal's side of that plane; keep = another (point, normal) plane likewise (world); fill
        closes the cut faces flat."""
        m = Matrix.Translation(Vector(center) - self.pivot) @ rotation @ Matrix.Diagonal((*size, 1.0))

        def make(g):
            bmesh.ops.create_uvsphere(g, u_segments=segments, v_segments=rings, radius=1.0)
            planes = [p for p in (cut, keep) if p is not None]
            if planes:
                g.transform(m)
                for point, normal in planes:
                    bmesh.ops.bisect_plane(g, geom=g.verts[:] + g.edges[:] + g.faces[:],
                                           plane_co=Vector(point) - self.pivot, plane_no=Vector(normal),
                                           clear_outer=True)
                if fill:
                    bmesh.ops.holes_fill(g, edges=g.edges[:], sides=0)
                    bmesh.ops.recalc_face_normals(g, faces=g.faces)
                g.transform(m.inverted())
        self._add(make, mat, m)

    def cone(self, mat, base_center, radius1, radius2, height, segments=32, rotation=Matrix.Identity(4)):
        """A (truncated) cone standing up on base_center."""
        m = Matrix.Translation(Vector(base_center) - self.pivot) @ rotation @ Matrix.Translation((0, 0, height / 2))
        self._add(lambda g: bmesh.ops.create_cone(g, cap_ends=True, segments=segments,
                                                  radius1=radius1, radius2=radius2, depth=height), mat, m)

    def box(self, mat, center, size, rotation=Matrix.Identity(4)):
        """A box with (x, y, z) half sizes, bevelled round."""
        m = Matrix.Translation(Vector(center) - self.pivot) @ rotation @ Matrix.Diagonal((*size, 1.0))

        def make(g):
            bmesh.ops.create_cube(g, size=2.0)
            bmesh.ops.bevel(g, geom=g.edges[:] + g.verts[:], offset=0.35, segments=3, affect="EDGES")
        self._add(make, mat, m)

    def capsule(self, mat, a, b, radius, segments=16):
        """A rounded rod from point a to point b."""
        a, b = Vector(a), Vector(b)
        axis = b - a
        rot = axis.normalized().to_track_quat("Z", "Y").to_matrix().to_4x4()
        m = Matrix.Translation((a + b) / 2 - self.pivot) @ rot
        self._add(lambda g: bmesh.ops.create_cone(g, cap_ends=True, segments=segments,
                                                  radius1=radius, radius2=radius, depth=axis.length), mat, m)
        self.sphere(mat, a, (radius, radius, radius), segments=segments, rings=8)
        self.sphere(mat, b, (radius, radius, radius), segments=segments, rings=8)

    def torus(self, mat, center, major, minor, rotation=Matrix.Identity(4), arc=1.0, segments=40, scale=(1, 1)):
        """A ring (or an arc of one, arc = fraction of a full turn) around local Z; scale stretches x, y."""
        def make(g):
            n = max(3, int(segments * arc))
            closed = arc >= 1.0
            rings = []
            for i in range(n if closed else n + 1):
                t = (i / n - (0 if closed else 0.5)) * arc * 2 * math.pi
                c = Vector((math.cos(t) * major * scale[0], math.sin(t) * major * scale[1], 0))
                out = Vector((math.cos(t), math.sin(t), 0))
                ring = [g.verts.new(c + out * math.cos(j / 10 * 2 * math.pi) * minor +
                                    Vector((0, 0, math.sin(j / 10 * 2 * math.pi) * minor))) for j in range(10)]
                rings.append(ring)
            count = len(rings)
            for i in range(count if closed else count - 1):
                for j in range(10):
                    k = (j + 1) % 10
                    i2 = (i + 1) % count
                    g.faces.new((rings[i][j], rings[i2][j], rings[i2][k], rings[i][k]))
            bmesh.ops.recalc_face_normals(g, faces=g.faces)
        m = Matrix.Translation(Vector(center) - self.pivot) @ rotation
        self._add(make, mat, m)

    def lathe(self, mat, profile, cuts=(), segments=48, axis=(0.0, 0.0)):
        """A solid of revolution about the vertical axis through (x, y) = axis (the world Z axis by
        default): profile is its outline as (radius, z) points; where it starts or ends on the axis
        it is closed, elsewhere left open. cuts = (point, normal) planes, each removing the part on
        its normal's side (world coordinates)."""
        def make(g):
            rings = []
            for r, z in profile:
                if r < 1e-6:
                    rings.append([g.verts.new((axis[0], axis[1], z))])
                else:
                    rings.append([g.verts.new((axis[0] + r * math.cos(i / segments * 2 * math.pi),
                                               axis[1] + r * math.sin(i / segments * 2 * math.pi), z))
                                  for i in range(segments)])
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

    def loft(self, mat, sections, segments=32):
        """An open tube through horizontal oval sections, bottom to top: each is ((x, y) center,
        (x, y) radii, z)."""
        def make(g):
            rings = [[g.verts.new((cx + rx * math.cos(i / segments * 2 * math.pi) - self.pivot.x,
                                   cy + ry * math.sin(i / segments * 2 * math.pi) - self.pivot.y, z - self.pivot.z))
                      for i in range(segments)] for (cx, cy), (rx, ry), z in sections]
            for a, b in zip(rings, rings[1:]):
                for i in range(segments):
                    j = (i + 1) % segments
                    g.faces.new((a[i], a[j], b[j], b[i]))
            bmesh.ops.recalc_face_normals(g, faces=g.faces)
        self._add(make, mat, Matrix.Identity(4))

    def tube(self, mat, points, radius, closed=False, ring=10):
        """A round tube through the points (world); an open one has round ends."""
        def make(g):
            pts = [Vector(p) - self.pivot for p in points]
            n = len(pts)
            rings = []
            for i, p in enumerate(pts):
                a = pts[(i - 1) % n] if closed or i > 0 else p
                b = pts[(i + 1) % n] if closed or i < n - 1 else p
                t = (b - a).normalized()
                side = t.cross(Vector((0, 0, 1)))
                if side.length < 1e-4:
                    side = t.cross(Vector((1, 0, 0)))
                side.normalize()
                up = side.cross(t)
                rings.append([g.verts.new(p + (side * math.cos(j / ring * 2 * math.pi) +
                                               up * math.sin(j / ring * 2 * math.pi)) * radius) for j in range(ring)])
            for i in range(n if closed else n - 1):
                for j in range(ring):
                    k = (j + 1) % ring
                    i2 = (i + 1) % n
                    g.faces.new((rings[i][j], rings[i2][j], rings[i2][k], rings[i][k]))
            bmesh.ops.recalc_face_normals(g, faces=g.faces)
        self._add(make, mat, Matrix.Identity(4))
        if not closed:
            for p in (points[0], points[-1]):
                self.sphere(mat, p, (radius, radius, radius), segments=ring, rings=6)

    def build(self):
        mesh = bpy.data.meshes.new(self.name)
        self.bm.to_mesh(mesh)
        self.bm.free()
        obj = bpy.data.objects.new(self.name, mesh)
        bpy.context.scene.collection.objects.link(obj)
        for poly in mesh.polygons:
            poly.use_smooth = True
        for mat in self.materials:
            obj.data.materials.append(mat)
        obj.location = self.pivot
        return obj




# The body and shapes that sit on it.

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


def use_body(kind):
    """Makes the shapes below follow that character's body ("girl" or "boy")."""
    global BODY
    BODY = BODIES[kind]


def bodies(both=True):
    """The body objects of an item, each a Part to fill in and build, with the shapes following its
    body while it is filled in: BodyGirl (the cone) then BodyBoy (the capsule) for an item that fits
    both characters, a single Body (the cone) for a Girl-only one. Use as
        for body in bodies(): ...; body.build()"""
    for name, kind in (("BodyGirl", "girl"), ("BodyBoy", "boy")) if both else (("Body", "girl"),):
        use_body(kind)
        yield Part(name)
    use_body("girl")


def body_radius(z):
    """The body's radius at height z (its straight side, before the rounding)."""
    return BODY["R0"] + (BODY["R1"] - BODY["R0"]) * (z - BODY_BOTTOM) / (BODY_TOP - BODY_BOTTOM)


def _outline(offset):
    """The body's whole outline as characters.py draws it, offset out by offset, as dense (radius,
    z) points from the axis under its bottom, round the bottom rim, up the side and over the top
    back to the axis."""
    z0, z1 = BODY_BOTTOM - offset, BODY_TOP + offset
    corners = [(0, z0), (body_radius(z0) + offset, z0), (body_radius(z1) + offset, z1), (0, z1)]
    points = rounded(corners, [0, BODY["ROUND"][0], BODY["ROUND"][1], 0], steps=8)
    # Rings every few cm, for smooth shading (and the toon outline).
    dense = [points[0]]
    for p, q in zip(points, points[1:]):
        n = max(1, int(math.dist(p, q) / 0.02))
        dense += [(p[0] + (q[0] - p[0]) * k / n, p[1] + (q[1] - p[1]) * k / n) for k in range(1, n + 1)]
    return dense


def _cross(p, q, value, index):
    """The point between p and q where coordinate index reaches value."""
    t = (value - p[index]) / (q[index] - p[index])
    return (p[0] + (q[0] - p[0]) * t, p[1] + (q[1] - p[1]) * t)


def body_profile(offset=0.0, z0=BODY_BOTTOM, z1=BODY_TOP):
    """The outline of the body offset out by offset, from z0 to z1: at the true bottom it wraps
    under the rim and ends open WRAP_R from the axis, clear of the legs; at the true top it closes
    on the axis; cut off at z0 or z1 it ends there, open."""
    pts = _outline(offset)
    if z0 <= BODY_BOTTOM:
        i = next(i for i in range(1, len(pts)) if pts[i][0] >= WRAP_R)
        pts = [_cross(pts[i - 1], pts[i], WRAP_R, 0)] + pts[i:]
    else:
        i = next(i for i in range(1, len(pts)) if pts[i][1] >= z0)
        pts = [_cross(pts[i - 1], pts[i], z0, 1)] + pts[i:]
    if z1 < BODY_TOP:
        i = next(i for i in range(1, len(pts)) if pts[i][1] > z1)
        pts = pts[:i] + [_cross(pts[i - 1], pts[i], z1, 1)]
    return pts


def surface_r(z):
    """The body's radius at height z, rounding included."""
    pts = _outline(0.0)
    for (r0, z0), (r1, z1) in zip(pts, pts[1:]):
        if z1 > z0 and z0 <= z <= z1:
            return r0 + (r1 - r0) * (z - z0) / (z1 - z0)
    return 0.0


def body_point(angle_deg, z, out=0.0):
    """A point out from the body's surface (along its normal) at a heading (0 = front, +Y; 90 =
    +X) and height, and that normal."""
    dz = 0.004
    slope = (surface_r(z + dz) - surface_r(z - dz)) / (2 * dz)
    t = math.radians(angle_deg)
    radial = Vector((math.sin(t), math.cos(t), 0))
    normal = (radial - Vector((0, 0, slope))).normalized()
    return radial * surface_r(z) + Vector((0, 0, z)) + normal * out, normal


def body_frame(angle_deg, z):
    """A rotation whose Z is the body's normal there, Y up along the surface and X across it."""
    _, n = body_point(angle_deg, z)
    across = Vector((0, 0, 1)).cross(n).normalized()
    up = n.cross(across)
    return Matrix((across, up, n)).transposed().to_4x4()


def cloth(part, mat, z0=BODY_BOTTOM, z1=BODY_TOP, out=TOP_OUT, cuts=()):
    """Cloth over the body from z0 to z1, out from it by out."""
    part.lathe(mat, body_profile(out, z0, z1), cuts=cuts)


def on_body(part, mat, path, radius, out=TOP_OUT, closed=False, steps=6):
    """A round cord (a hem, belt, stripe, strap) lying on the body over the cloth: path is (angle,
    z) points, smoothed between them (the short way round between headings, so a closed path can
    run on past 360 degrees back to its start)."""
    n = len(path)
    pts = []
    for i in range(n if closed else n - 1):
        window = [path[(i + k) % n] if closed else path[min(max(i + k, 0), n - 1)] for k in (-1, 0, 1, 2)]
        unwrapped = [window[1]]
        for k in (0, 2, 3):
            ref = unwrapped[-1] if k > 1 else window[1]
            a = ref[0] + (window[k][0] - ref[0] + 180) % 360 - 180
            unwrapped.insert(0, (a, window[k][1])) if k == 0 else unwrapped.append((a, window[k][1]))
        p0, p1, p2, p3 = unwrapped
        for s in range(steps):
            t = s / steps
            a = [0.5 * (2 * p1[c] + (-p0[c] + p2[c]) * t + (2 * p0[c] - 5 * p1[c] + 4 * p2[c] - p3[c]) * t * t +
                        (-p0[c] + 3 * p1[c] - 3 * p2[c] + p3[c]) * t ** 3) for c in (0, 1)]
            pts.append(body_point(a[0], a[1], out + radius + 0.003)[0])
    if not closed:
        pts.append(body_point(path[-1][0], path[-1][1], out + radius + 0.003)[0])
    part.tube(mat, pts, radius, closed=closed)


def ring(part, mat, z, radius, out=TOP_OUT, segments=48):
    """A cord all round the body at height z."""
    on_body(part, mat, [(k / 12 * 360, z) for k in range(12)], radius, out, closed=True, steps=max(1, segments // 12))


def spot(part, mat, angle_deg, z, size, out=TOP_OUT, thickness=0.3):
    """A flat round spot on the cloth."""
    p, _ = body_point(angle_deg, z, out + 0.003)
    part.sphere(mat, p, (size, size, size * thickness), rotation=body_frame(angle_deg, z), segments=16, rings=8)


def flower(part, petal_mat, middle_mat, center, normal, size, petals=5):
    """A little flat flower facing along normal."""
    n = Vector(normal).normalized()
    rot = n.to_track_quat("Z", "Y").to_matrix().to_4x4()
    c = Vector(center)
    for i in range(petals):
        t = i / petals * 2 * math.pi
        offset = (rot @ Vector((math.cos(t) * size, math.sin(t) * size, 0, 1))).to_3d()
        part.sphere(petal_mat, c + offset, (size * 0.75, size * 0.75, size * 0.35), rotation=rot, segments=12, rings=6)
    part.sphere(middle_mat, c + n * size * 0.2, (size * 0.55, size * 0.55, size * 0.4), rotation=rot, segments=12, rings=6)



def legs():
    """The two leg objects (LegL, LegR), each a Part to fill in and build."""
    for name in ("LegL", "LegR"):
        yield Part(name)


def leg_tube(part, mat, z0, z1=LEG_TOP, out=LEG_OUT, segments=32):
    """A trouser leg: an open tube round the part's leg tube (out from it by out) from z0 up to z1."""
    r = LEG_R + out
    n = max(2, int((z1 - z0) / 0.02))
    part.lathe(mat, [(r, z0 + (z1 - z0) * k / n) for k in range(n + 1)], segments=segments, axis=(part.pivot.x, 0.0))


def leg_ring(part, mat, z, minor, out=LEG_OUT):
    """A round cord (a cuff, a hem) all round the part's trouser leg at height z."""
    part.torus(mat, (part.pivot.x, 0, z), LEG_R + out + minor * 0.8, minor, segments=32)


def shoe(part, mat, sole_mat, grow=(0.012, 0.014, 0.012), sole=0.022):
    """A round shoe over the part's rounded foot: an upper a little bigger than the foot (grow
    more on each radius) on a sole, flat on the ground, from z 0 up to sole. Returns the upper's
    center and radii."""
    x = part.pivot.x
    c = Vector((x, FOOT_C[0], FOOT_C[1]))
    size = tuple(r + g for r, g in zip(FOOT_R, grow))
    part.sphere(mat, c, size, cut=((x, 0, sole), (0, 0, -1)), segments=32, rings=16)
    part.sphere(sole_mat, (x, c.y, sole / 2), (size[0] * 1.06, size[1] * 1.05, sole * 0.9),
                cut=((x, 0, 0), (0, 0, -1)), keep=((x, 0, sole), (0, 0, 1)), segments=32, rings=12, fill=True)
    return c, size


def eye(side):
    """The eye's center on the design head (side -1 left, +1 right) and its outward direction."""
    n = EYE_DIRS[0 if side < 0 else 1]
    return HEAD_C + n * HEAD_R * 0.9, n


def hair_cap(part, mat, fringe=True, back_drop=0.0):
    """The shared hair cap over the top and back of the head (open over the face), with a fringe."""
    part.sphere(mat, HEAD_C + Vector((0, -0.02, 0.03)), (HEAD_R * 1.07, HEAD_R * 1.06, HEAD_R * 1.04),
                segments=40, rings=24, cut=(HEAD_C + Vector((0, 0.1, -0.1 - back_drop)), Vector((0, 0.85, -0.53))))
    if fringe:
        for x in (-0.22, -0.08, 0.08, 0.22):
            part.sphere(mat, HEAD_C + Vector((x, HEAD_R * 0.8 - abs(x) * 0.35, 0.24 - abs(x) * 0.3)),
                        (0.1, 0.07, 0.075), segments=16, rings=8)


def export(script_file):
    """Exports the scene as <script name>.glb next to the script."""
    out = os.path.splitext(os.path.abspath(script_file))[0] + ".glb"
    bpy.ops.export_scene.gltf(filepath=out, export_format="GLB", export_apply=True,
                              export_yup=True, export_materials="EXPORT")
    print(f"{os.path.basename(script_file)}: wrote {out}")
