# Shared helpers for the Wardrobe's model scripts (one script per clothing item or hair style,
# each exporting <its name>.glb next to itself). Not run on its own.
#
# Meters, feet at the origin, facing Blender +Y (Godot's -Z after the glTF export), sized for the
# Player module's simple-shapes rig (modules/Player/Models/characters.py): a 1.80 m grown-up with
# normal proportions, a round head (0.27 m wide, 0.30 m tall) floating about 5 cm over a smooth
# tapered torso (oval, about 0.22 m deep: hips 0.34 m wide, waist 0.30, shoulders 0.40), round hands
# floating at the ends of invisible arms, two real legs (rounded tubes, radius LEG_R) from the hips
# into rounded shoes, no visible arms or neck. Each piece is one object per rig part it covers,
# named like the part and with its origin at that part's pivot, so the game parents it to the part
# with no offset (Fungtopia.Wardrobe.Attach):
#   Head (pivot at the neck, z 1.48: hats, glasses, clips, hair),
#   Body (pivot at the hips, z 0.90: tops and dresses over the torso, dresses flaring below the
#         hips over the legs' tops, skirts over the upper legs, bottoms' waist bands, backpacks on
#         its back, the scarf round its top),
#   LegL / LegR (pivots at the hips, x -0.10 / +0.10, z 0.90: trouser, jean, short and overall legs
#         round the leg's tube, the shoes on the feet at the legs' ends).
# Clothes follow the torso's outline (torso_rings) or the leg's tube a little out from it, so
# nothing pokes through it; there are no sleeves (no visible arms).
# The head's pieces are drawn on the design head of the old rig (radius HEAD_R 0.4, center HEAD_C,
# the neck at z 0.72) and Part("Head") maps them onto the real head (head_space), as characters.py
# draws its face and hair, so every hat, clip and hair style keeps its shape at the new size.
# Materials carry plain colors only; the game draws them toon-shaded with ink outlines. A hair
# style's main material is named "Hair" so the game can tint it with the chosen hair color.
# No eyes or faces on clothes: only creatures have eyes.
import math
import os
import sys

import bmesh
import bpy
from mathutils import Matrix, Vector

# The rig, from characters.py.
HIP_Z = 0.90
NECK_Z = 1.48
LEG_X, LEG_R = 0.10, 0.065
ANKLE_Z = 0.09
PIVOTS = {
    "Head": (0, 0, NECK_Z),
    "Body": (0, 0, HIP_Z),
    "LegL": (-LEG_X, 0, HIP_Z),
    "LegR": (LEG_X, 0, HIP_Z),
}
# The torso: oval rings from TORSO_BOTTOM to TORSO_TOP (rounded off at both ends), half widths
# through TORSO_WIDTH, half depth 0.06 + 0.3 x the half width.
TORSO_BOTTOM, TORSO_TOP = 0.88, 1.44
TORSO_WIDTH = ((0.88, 0.15), (0.95, 0.17), (1.10, 0.15), (1.32, 0.20), (1.44, 0.185))
# The head: the real one is HEAD_SIZE (radii) centered at HEAD_Z; its bottom is at about 1.49 m:
# clothes on the body stay below HEAD_GAP. Head pieces are drawn on the design head (HEAD_R at HEAD_C).
HEAD_Z = 1.64
HEAD_SIZE = (0.135, 0.13, 0.15)
HEAD_R = 0.4
HEAD_C = Vector((0, 0, 0.72 + HEAD_R * 0.92))
HEAD_GAP = 1.46
# The rig's shoes (each under its leg): center (y, z) and radii, cut flat at the ground.
SHOE_C = (0.03, 0.05)
SHOE_R = (0.075, 0.125, 0.07)
# Bottoms: a waist band round the torso's bottom up to BAND_TOP, legs down to TROUSER_HEM (shorts
# to SHORTS_HEM); tops come down to TOP_HEM, under the band's top.
BAND_TOP = 1.02
TOP_HEM = 0.97
TROUSER_HEM = 0.15
SHORTS_HEM = 0.64
# How far out from the body clothes sit: tops and dresses, then bottoms over them; legs.
TOP_OUT = 0.01
BOTTOM_OUT = 0.02
LEG_OUT = 0.014

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


def face_out(g, axis_point):
    """Turns an open surface's faces (made consistent by recalc_face_normals, but either way out
    as it is not closed) to face away from its axis: axis_point(p) is the axis point nearest p.
    The game's ink outline is an inverted hull grown along the normals, so a surface facing in
    would draw black."""
    bmesh.ops.recalc_face_normals(g, faces=g.faces)
    out = sum(f.normal.dot(f.calc_center_median() - axis_point(f.calc_center_median())) * f.calc_area() for f in g.faces)
    if out < 0:
        bmesh.ops.reverse_faces(g, faces=g.faces)


class Part:
    """Gathers shapes into one mesh object (named like a rig part) with several materials."""

    def __init__(self, name):
        self.name = name
        self.pivot = Vector(PIVOTS[name])
        # The head's pieces are drawn on the design head and mapped onto the real one.
        self.space = head_space() if name == "Head" else None
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
        geom.transform(matrix)
        slot = self._slot(mat)
        for face in geom.faces:
            face.material_index = slot
        mesh = bpy.data.meshes.new("tmp")
        geom.to_mesh(mesh)
        geom.free()
        self.bm.from_mesh(mesh)
        bpy.data.meshes.remove(mesh)

    def sphere(self, mat, center, size, rotation=Matrix.Identity(4), segments=32, rings=16, cut=None, keep=None):
        """An ellipsoid with (x, y, z) radii. cut = (point, normal) removes the part on the
        normal's side of that plane; keep = another (point, normal) plane likewise (world)."""
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

    def lathe(self, mat, profile, cuts=(), segments=48):
        """A solid of revolution about the world Z axis: profile is its outline as (radius, z)
        points; where it starts or ends on the axis it is closed, elsewhere left open. cuts =
        (point, normal) planes, each removing the part on its normal's side (world coordinates)."""
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
            face_out(g, lambda p: Vector((0, 0, p.z)))
            for point, normal in cuts:
                bmesh.ops.bisect_plane(g, geom=g.verts[:] + g.edges[:] + g.faces[:],
                                       plane_co=Vector(point), plane_no=Vector(normal), clear_outer=True)
        self._add(make, mat, Matrix.Translation(-self.pivot))

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
            face_out(g, lambda p: min(pts, key=lambda q: (q - p).length))
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




def head_space():
    """The matrix taking a point drawn on the design head (radius HEAD_R, center HEAD_C) onto the
    real head (HEAD_SIZE, centered at HEAD_Z), as characters.py maps the face and hair."""
    scale = (HEAD_SIZE[0] / HEAD_R, HEAD_SIZE[0] / HEAD_R, HEAD_SIZE[2] / (HEAD_R * 0.94))
    return Matrix.Translation((0, 0, HEAD_Z)) @ Matrix.Diagonal((*scale, 1.0)) @ Matrix.Translation(-HEAD_C)


# The torso and shapes that sit on it.

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
    """The torso's half width and half depth at height z (as characters.py draws it; zero at its
    rounded bottom and top)."""
    base = smooth(TORSO_WIDTH, z)
    w = base
    for end, length in ((TORSO_BOTTOM, 0.05), (TORSO_TOP, 0.09)):
        u = abs(z - end) / length
        if u < 1:
            w *= math.sqrt(max(0.0, 1 - (1 - u) ** 2))
    return w, (0.06 + 0.3 * base) * (w / base)


def torso_rings(out=0.0, z0=0.92, z1=TORSO_TOP):
    """The torso's outline as (half width, half depth, z) oval rings from z0 to z1, offset out by
    out along its normal; closed on the axis at the true top, open at z0 (never below 0.92, clear
    of the legs, which come out of the torso's bottom) unless z0 is TORSO_BOTTOM: then it closes
    under the torso's rounded bottom too (a waist band's seat, the legs coming out through it)."""
    if z0 > TORSO_BOTTOM + 1e-6:
        z0 = max(z0, 0.92)
    n = 80
    zs = [TORSO_BOTTOM + (TORSO_TOP - TORSO_BOTTOM) * (0.5 - 0.5 * math.cos(math.pi * i / n)) for i in range(n + 1)]
    zs = [z for z in zs if z0 + 1e-6 < z < z1 - 1e-6] + [z0] + ([z1] if z1 < TORSO_TOP - 1e-6 else [TORSO_TOP])
    rings = []
    for z in sorted(zs):
        w, d = torso_size(z)
        dz = 1e-4
        lo, hi = max(z - dz, TORSO_BOTTOM), min(z + dz, TORSO_TOP)
        slope = (torso_size(hi)[0] - torso_size(lo)[0]) / (hi - lo)
        length = math.hypot(1.0, slope)
        nh, nz = 1 / length, -slope / length
        if w < 1e-6:
            nh, nz = 0.0, (1.0 if z > (TORSO_BOTTOM + TORSO_TOP) / 2 else -1.0)
        if z in (z0, z1) and w > 1e-6:
            nh, nz = 1.0, 0.0  # a cut edge stays at its height
        rings.append((w + out * nh, d + out * nh, z + out * nz))
    return rings


def oval_lathe(part, mat, rings, cuts=(), segments=48):
    """A surface through oval rings (half width x, half depth y, z); a ring of size 0 is a point on
    the axis (closing it), elsewhere the ends are left open. cuts = (point, normal) planes, each
    removing the part on its normal's side (world coordinates)."""
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
        face_out(g, lambda p: Vector((0, 0, p.z)))
        for point, normal in cuts:
            bmesh.ops.bisect_plane(g, geom=g.verts[:] + g.edges[:] + g.faces[:],
                                   plane_co=Vector(point), plane_no=Vector(normal), clear_outer=True)
    part._add(make, mat, Matrix.Translation(-part.pivot))


def body_point(angle_deg, z, out=0.0):
    """A point out from the torso's surface (along its normal) at a heading (0 = front, +Y; 90 =
    +X) and height, and that normal."""
    w, d = torso_size(z)
    dz = 0.004
    t = math.radians(angle_deg)
    p = Vector((math.sin(t) * w, math.cos(t) * d, z))
    # The oval's outward normal, tipped by the outline's slope.
    slope = (torso_size(min(z + dz, TORSO_TOP))[0] - torso_size(max(z - dz, TORSO_BOTTOM))[0]) / (2 * dz)
    radial = Vector((math.sin(t) * d, math.cos(t) * w, 0)).normalized()
    normal = (radial - Vector((0, 0, slope))).normalized()
    return p + normal * out, normal


def body_frame(angle_deg, z):
    """A rotation whose Z is the torso's normal there, Y up along the surface and X across it."""
    return surface_frame(body_point(angle_deg, z)[1])


def surface_frame(normal):
    """A rotation whose Z is the normal, Y up along the surface and X across it."""
    n = Vector(normal).normalized()
    across = Vector((0, 0, 1)).cross(n).normalized()
    up = n.cross(across)
    return Matrix((across, up, n)).transposed().to_4x4()


def waist_band(part, mat, top=BAND_TOP, out=BOTTOM_OUT):
    """A bottom's waist band: cloth round the torso's bottom, closed under it (the legs come out
    through it), up to top."""
    oval_lathe(part, mat, torso_rings(out, TORSO_BOTTOM, top))


def cloth(part, mat, z0=0.92, z1=TORSO_TOP, out=TOP_OUT, cuts=()):
    """Cloth over the torso from z0 to z1, out from it by out (closed over the top at TORSO_TOP)."""
    oval_lathe(part, mat, torso_rings(out, z0, z1), cuts=cuts)


def flared(out, top, hem_z, flare, stretch=1.6, power=2.3):
    """Rings (bottom up) of a skirt or a dress's skirt: from the torso's outline at height top
    (offset out) flaring down over the legs' tops to a round hem at hem_z, flare further out at the
    sides (and stretch times that at the front and back, so the legs can swing inside it). The
    flare grows as t ** power (t from 0 at the top to 1 at the hem): slowly at first, so the skirt
    stays clear of the hands hanging beside the hips, then out to the hem below them."""
    w, d = torso_size(top)
    w, d = w + out, d + out
    rings = []
    for k in range(13):
        t = k / 12
        grow = flare * t ** power
        rings.append((w + grow, d + grow * stretch, top + (hem_z - top) * t))
    return rings[::-1]


DRESS_FLARE = 0.10


def dress(part, mat, out, hem_z=0.62, flare=DRESS_FLARE, waist=TOP_HEM):
    """A dress: over the torso from its top down to waist, flaring below it over the legs to a hem
    at hem_z. Returns its rings (bottom up) for hem_point."""
    rings = flared(out, waist, hem_z, flare)[:-1] + torso_rings(out, waist)
    oval_lathe(part, mat, rings)
    return rings


def hem_ring(part, mat, rings, z, radius, out=0.0):
    """A round cord all round a flared skirt (rings as flared() gives) at height z."""
    p, _ = hem_point(rings, 90, z, out + radius * 0.6)
    q, _ = hem_point(rings, 0, z, out + radius * 0.6)
    part.torus(mat, (0, 0, p.z), p.x, radius, scale=(1, q.y / p.x), segments=64)


def on_skirt(part, mat, rings, angle_deg, z0, z1, radius, out=0.0, steps=8):
    """A round cord (a pleat, a seam) lying down a flared skirt at a heading from z0 to z1."""
    pts = [hem_point(rings, angle_deg, z0 + (z1 - z0) * k / steps, out + radius * 0.6)[0] for k in range(steps + 1)]
    part.tube(mat, pts, radius)


def hem_point(rings, angle_deg, z, out=0.0):
    """A point out from a flared skirt's surface (rings as flared() gives, bottom up) at a heading
    and height, and its normal there."""
    for (w0, d0, z0), (w1, d1, z1) in zip(rings, rings[1:]):
        if z0 <= z <= z1:
            f = (z - z0) / (z1 - z0)
            w, d = w0 + (w1 - w0) * f, d0 + (d1 - d0) * f
            slope = (w1 - w0) / (z1 - z0)
            break
    else:
        w, d, _ = rings[0]
        slope = 0.0
    t = math.radians(angle_deg)
    p = Vector((math.sin(t) * w, math.cos(t) * d, z))
    normal = (Vector((math.sin(t) * d, math.cos(t) * w, 0)).normalized() - Vector((0, 0, slope))).normalized()
    return p + normal * out, normal


def on_body(part, mat, path, radius, out=TOP_OUT, closed=False, steps=6):
    """A round cord (a hem, belt, stripe, strap) lying on the torso over the cloth: path is (angle,
    z) points, smoothed between them."""
    n = len(path)
    pts = []
    for i in range(n if closed else n - 1):
        p0, p1, p2, p3 = (path[(i + k) % n] if closed else path[min(max(i + k, 0), n - 1)] for k in (-1, 0, 1, 2))
        # Headings the short way round from p1 (so 330 -> 0 goes on through 345, not back).
        def near(q, ref):
            return (ref[0] + (q[0] - ref[0] + 180) % 360 - 180, q[1])
        p0, p2 = near(p0, p1), near(p2, p1)
        p3 = near(p3, p2)
        for s in range(steps):
            t = s / steps
            a = [0.5 * (2 * p1[c] + (-p0[c] + p2[c]) * t + (2 * p0[c] - 5 * p1[c] + 4 * p2[c] - p3[c]) * t * t +
                        (-p0[c] + 3 * p1[c] - 3 * p2[c] + p3[c]) * t ** 3) for c in (0, 1)]
            pts.append(body_point(a[0], a[1], out + radius + 0.003)[0])
    if not closed:
        pts.append(body_point(path[-1][0], path[-1][1], out + radius + 0.003)[0])
    part.tube(mat, pts, radius, closed=closed)


def ring(part, mat, z, radius, out=TOP_OUT, segments=48):
    """A cord all round the torso at height z."""
    on_body(part, mat, [(k / 12 * 360, z) for k in range(12)], radius, out, closed=True, steps=max(1, segments // 12))


def spot(part, mat, angle_deg, z, size, out=TOP_OUT, thickness=0.3):
    """A flat round spot on the cloth."""
    p, _ = body_point(angle_deg, z, out + 0.003)
    part.sphere(mat, p, (size, size, size * thickness), rotation=body_frame(angle_deg, z), segments=16, rings=8)


# The legs: rounded tubes of radius LEG_R from the hips (the pivots) down to ANKLE_Z.

def leg_tube(part, mat, z0, z1, out=LEG_OUT, segments=24):
    """Cloth round the part's leg from z0 up to z1, out from it by out: a tube whose bottom edge
    turns in round to the leg (open above, its top hidden in the torso or the waist band)."""
    x = part.pivot.x
    r = LEG_R + out
    lip = r - LEG_R - 0.002
    profile = [(r - lip + lip * math.cos(math.radians(a)), z0 + lip + lip * math.sin(math.radians(a))) for a in range(-90, 1, 15)]
    profile += [(r, z0 + lip + (z1 - z0 - lip) * k / 12) for k in range(1, 13)]
    def make(g):
        rings = [[g.verts.new((x + rr * math.cos(i / segments * 2 * math.pi),
                               rr * math.sin(i / segments * 2 * math.pi), z)) for i in range(segments)]
                 for rr, z in profile]
        for a, b in zip(rings, rings[1:]):
            for i in range(segments):
                j = (i + 1) % segments
                g.faces.new((a[i], a[j], b[j], b[i]))
        face_out(g, lambda p: Vector((x, 0, p.z)))
    part._add(make, mat, Matrix.Translation(-part.pivot))


def leg_seam(part, mat, angle_deg, z0, z1, radius, out=LEG_OUT):
    """A round cord (a seam) down the part's leg at a heading (0 = front, 90 = +X) from z0 to z1."""
    t = math.radians(angle_deg)
    r = LEG_R + out + radius * 0.6
    x, y = part.pivot.x + math.sin(t) * r, math.cos(t) * r
    part.tube(mat, [(x, y, z0 + (z1 - z0) * k / 6) for k in range(7)], radius)


def leg_cuff(part, mat, z, radius, out=LEG_OUT):
    """A round cuff (a hem) round the part's leg at height z, over cloth out from it by out."""
    part.torus(mat, (part.pivot.x, 0, z), LEG_R + out + radius * 0.6, radius, segments=32)


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



def shoe(part, mat, sole_mat, size=(0.082, 0.132, 0.074), sole=0.03):
    """A rounded shoe over the rig's shoe at the end of the part's leg: an upper from the ground up
    round the ankle on a sole from the ground up to sole. Returns the upper's center."""
    x = part.pivot.x
    c = Vector((x, SHOE_C[0], SHOE_C[1]))
    part.sphere(mat, c, size, cut=((x, 0, sole * 0.4), (0, 0, -1)), segments=24, rings=12)
    part.sphere(sole_mat, (x, c.y, sole / 2), (size[0] * 1.04, size[1] * 1.04, sole / 2), segments=24, rings=8)
    return c


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
