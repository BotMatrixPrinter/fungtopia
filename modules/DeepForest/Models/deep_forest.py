# Builds the Deep Forest's props and exports them next to this script as deep_forest.glb.
# Run from the repository root:
#   blender -b --factory-startup --python modules/DeepForest/Models/deep_forest.py
#
# Shapes are built in Godot's axes (meters, Y up, -Z forward) and converted for the glTF export.
# Every object is a unit shape the game scales per instance (MultiMesh), no eyes or faces:
#   Stem    an old gnarled giant mushroom stem: radius 1 (flaring to ~1.45 at the foot), height 1.
#   Skirt   a giant stem's root flare: radius 1 at its top (y = 1), widening with root buttresses to
#           about 1.18 at its foot (y = 0); the game sinks its foot into the ground.
#   Cap     a broad old cap with a wavy, drooping rim: radius 1, top at y = 1, rim near y = 0;
#           UV.y is 1 on the top, 0 on the gills underneath.
#   Shelf   a bracket fungus shelf sticking out along +X from a stem's surface, about 1 m wide.
#   Stalk   a smooth, slightly tapering little mushroom stem with a soft bulb at its foot: radius 1
#           (1.25 at the bulb), height 1; for the medium and small fungi.
#   Caps of the medium and small fungi, each radius 1 (about), from y = 0 (rim or bottom) to y = 1
#   (top); UV.y is 1 on the top surface, 0 on gills or the underside:
#   Dome    a round, plump fly-agaric/bolete dome.
#   Flat    a wide flat parasol cap with a little bump in the middle.
#   Funnel  a chanterelle funnel: a dip in the middle, a wavy turned-up rim, narrowing to its foot.
#   Bell    a tall ink-cap bell, open underneath (the stem goes up inside it).
#   Cone    a morel's pointed cone (the shader draws its honeycomb).
#   Ball    a puffball, sitting on y = 0.
#   Cup     a scarlet cup fungus, its open bowl up (UV.y 1 inside the bowl).
#   Coral   a coral fungus: a tuft of branching, round-tipped arms.
#   Jelly   a jelly fungus: a soft clump of folded, rounded lobes.
#   Web     a flat round spider web in the XZ plane, radius 1 (thin strands; drawn double-sided).
#   Root    an arching root from the origin out along +X to x = 1, rising about 0.3 in between.
#   Fern    a low fern of drooping fronds, about 0.6 high and 1.2 across (drawn double-sided);
#           UV.y runs from 0 at the root to 1 at each frond's tip.
# Materials are plain colors only; the game draws everything with its own toon shaders.
import math
import os

import bmesh
import bpy

HERE = os.path.dirname(os.path.abspath(__file__))
TAU = 2.0 * math.pi


def hash1(i, k):
    """A fixed pseudo-random value in [0, 1): the same models every run."""
    h = (i * 2654435761 ^ k * 2246822519 ^ 0x5BD1E995) & 0xFFFFFFFF
    h = ((h ^ (h >> 15)) * 2246822519) & 0xFFFFFFFF
    h = ((h ^ (h >> 13)) * 3266489917) & 0xFFFFFFFF
    h ^= h >> 16
    return (h >> 8) / 16777216.0


def to_blender(p):
    """Godot (x, y up, z) to Blender (x, y = -z, z up); the glTF export turns it back."""
    return (p[0], -p[2], p[1])


class Builder:
    """Collects vertices (Godot axes), per-vertex UVs and faces into one object."""

    def __init__(self):
        self.verts, self.uvs, self.faces = [], [], []

    def vert(self, p, uv=(0.0, 0.0)):
        self.verts.append(to_blender(p))
        self.uvs.append(uv)
        return len(self.verts) - 1

    def lathe(self, profile, segments, radius_at=None, center=(0.0, 0.0, 0.0)):
        """A closed surface of revolution about Y. profile: (r, y, v) from the top pole (r = 0)
        to the bottom pole (r = 0); radius_at(angle, y, r) may make it gnarled or wavy."""
        cx, cy, cz = center
        top = self.vert((cx, cy + profile[0][1], cz), (0.5, profile[0][2]))
        rings = []
        for r, y, v in profile[1:-1]:
            ring = []
            for s in range(segments):
                a = TAU * s / segments
                rr = radius_at(a, y, r) if radius_at else r
                yy = y
                if isinstance(rr, tuple):
                    rr, yy = rr
                ring.append(self.vert((cx + rr * math.cos(a), cy + yy, cz + rr * math.sin(a)), (s / segments, v)))
            rings.append(ring)
        bottom = self.vert((cx, cy + profile[-1][1], cz), (0.5, profile[-1][2]))
        n = segments
        for s in range(n):
            self.faces.append((top, rings[0][(s + 1) % n], rings[0][s]))
        for k in range(len(rings) - 1):
            a, b = rings[k], rings[k + 1]
            for s in range(n):
                t = (s + 1) % n
                self.faces.append((a[s], a[t], b[t], b[s]))
        for s in range(n):
            self.faces.append((bottom, rings[-1][s], rings[-1][(s + 1) % n]))

    def ribbon(self, points, width, up=(0.0, 1.0, 0.0), uv_v=0.0):
        """A flat strip along points (Godot axes), width across, facing `up`; UV.y is uv_v, or
        runs 0..1 along the strip when uv_v is None."""
        prev = None
        for i, p in enumerate(points):
            q = points[min(i + 1, len(points) - 1)]
            o = points[max(i - 1, 0)]
            d = (q[0] - o[0], q[1] - o[1], q[2] - o[2])
            # side = d x up
            sx = d[1] * up[2] - d[2] * up[1]
            sy = d[2] * up[0] - d[0] * up[2]
            sz = d[0] * up[1] - d[1] * up[0]
            ln = math.sqrt(sx * sx + sy * sy + sz * sz) or 1.0
            w = width(i / (len(points) - 1)) if callable(width) else width
            sx, sy, sz = sx / ln * w * 0.5, sy / ln * w * 0.5, sz / ln * w * 0.5
            v = uv_v if uv_v is not None else i / (len(points) - 1)
            a = self.vert((p[0] - sx, p[1] - sy, p[2] - sz), (0.0, v))
            b = self.vert((p[0] + sx, p[1] + sy, p[2] + sz), (1.0, v))
            if prev:
                self.faces.append((prev[0], prev[1], b, a))
            prev = (a, b)

    def build(self, name, color, recalc=True):
        mesh = bpy.data.meshes.new(name)
        mesh.from_pydata(self.verts, [], self.faces)
        uv = mesh.uv_layers.new(name="UVMap")
        for poly in mesh.polygons:
            for li in poly.loop_indices:
                u, v = self.uvs[mesh.loops[li].vertex_index]
                uv.data[li].uv = (u, v)
        mesh.validate()
        if recalc:
            bm = bmesh.new()
            bm.from_mesh(mesh)
            bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
            bm.to_mesh(mesh)
            bm.free()
        for poly in mesh.polygons:
            poly.use_smooth = True
        mat = bpy.data.materials.new(name)
        mat.diffuse_color = (*color, 1.0)
        mesh.materials.append(mat)
        obj = bpy.data.objects.new(name, mesh)
        bpy.context.scene.collection.objects.link(obj)
        return obj


def stem():
    b = Builder()
    rows = 14
    profile = [(0.0, 1.0, 1.0)]
    for k in range(rows + 1):
        t = 1.0 - k / rows  # 1 at the top, 0 at the foot
        r = 0.82 + 0.18 * (1.0 - t) + 0.5 * (1.0 - t) ** 5
        profile.append((r, t, t))
    profile.append((0.0, 0.0, 0.0))

    def gnarl(a, y, r):
        # Old and knobbly: ridges that twist up the stem, bulging more near the foot.
        ridges = 0.07 * math.sin(5.0 * a + 4.0 * y) + 0.04 * math.sin(9.0 * a - 7.0 * y + 1.3)
        knot = 0.06 * math.exp(-((y - 0.35) ** 2) / 0.004) * max(0.0, math.cos(a - 1.1))
        return r * (1.0 + ridges * (0.6 + 0.6 * (1.0 - y)) + knot)

    b.lathe(profile, 24, gnarl)
    return b.build("Stem", (0.45, 0.38, 0.4))


def skirt():
    b = Builder()
    rows = 8
    profile = [(0.0, 1.0, 1.0)]
    for k in range(rows + 1):
        t = 1.0 - k / rows  # 1 at the top, 0 at the foot
        profile.append((1.0 + 0.12 * (1.0 - t) ** 2, t, t))
    profile.append((0.0, 0.0, 0.0))

    def buttress(a, y, r):
        # Root buttresses fanning into the ground: lobes that bulge more the lower they are.
        lobes = max(0.0, math.cos(7.0 * a + 0.6 * math.sin(3.0 * a))) ** 2
        return r * (1.0 + 0.06 * (1.0 - y) ** 1.5 * lobes - 0.015 * y)

    b.lathe(profile, 28, buttress)
    return b.build("Skirt", (0.42, 0.33, 0.32))


def cap():
    b = Builder()
    profile = [(0.0, 1.0, 1.0)]
    rows = 10
    for k in range(1, rows + 1):
        t = k / rows  # 0 at the top, 1 at the rim
        r = math.sin(t * math.pi * 0.5) * (1.0 - 0.03 * t)
        y = math.cos(t * math.pi * 0.5) ** 0.8 * 0.95 + 0.05 - 0.12 * t * t
        profile.append((r, y, 1.0))
    # Under the rim, back in along the gills to the stem.
    profile.append((0.97, -0.1, 0.0))
    profile.append((0.7, 0.08, 0.0))
    profile.append((0.35, 0.15, 0.0))
    profile.append((0.0, 0.18, 0.0))

    def wavy(a, y, r):
        # A slow wave and a droop along the rim, stronger the nearer the rim.
        edge = max(0.0, r - 0.55) / 0.45
        wave = 0.05 * math.sin(7.0 * a + 0.4) + 0.03 * math.sin(13.0 * a + 2.0)
        return r * (1.0 + 0.6 * wave * edge), y + 0.9 * wave * edge

    b.lathe(profile, 36, wavy)
    return b.build("Cap", (0.42, 0.18, 0.32))


def shelf():
    b = Builder()
    profile = [(0.0, 0.12, 1.0)]
    for k in range(1, 7):
        t = k / 7
        profile.append((math.sin(t * math.pi * 0.5), 0.12 * math.cos(t * math.pi * 0.5) ** 0.6, 1.0))
    profile.append((0.95, -0.05, 0.0))
    profile.append((0.5, -0.06, 0.0))
    profile.append((0.0, -0.06, 0.0))

    def fan(a, y, r):
        # A half-moon reaching out along +X; the back half is flattened into the stem.
        reach = 0.5 + 0.5 * math.cos(a)
        return r * (0.12 + 0.88 * reach) * 0.5, y

    b.lathe(profile, 20, fan, center=(0.05, 0.0, 0.0))
    return b.build("Shelf", (0.5, 0.32, 0.22))


def stalk():
    b = Builder()
    rows = 8
    profile = [(0.0, 1.0, 0.0)]
    for k in range(rows + 1):
        t = 1.0 - k / rows  # 1 at the top, 0 at the foot
        profile.append((0.85 + 0.15 * (1.0 - t) + 0.25 * math.exp(-t * t / 0.02), t, 0.0))
    profile.append((0.0, 0.0, 0.0))
    b.lathe(profile, 12)
    return b.build("Stalk", (0.93, 0.87, 0.72))


def dome():
    b = Builder()
    profile = [(0.0, 1.0, 1.0)]
    for k in range(1, 9):
        t = k / 8
        profile.append((math.sin(t * math.pi * 0.55) / math.sin(math.pi * 0.55), 0.12 + 0.88 * math.cos(t * math.pi * 0.5) ** 0.7, 1.0))
    profile.append((0.92, 0.04, 0.0))
    profile.append((0.5, 0.12, 0.0))
    profile.append((0.0, 0.16, 0.0))
    b.lathe(profile, 24)
    return b.build("Dome", (0.8, 0.1, 0.1))


def flat():
    b = Builder()
    profile = [(0.0, 1.0, 1.0), (0.12, 0.95, 1.0), (0.22, 0.78, 1.0)]
    for k in range(1, 6):
        t = k / 5
        profile.append((0.22 + 0.78 * t, 0.78 - 0.5 * t * t, 1.0))
    profile.append((0.95, 0.12, 0.0))
    profile.append((0.5, 0.3, 0.0))
    profile.append((0.0, 0.4, 0.0))

    def wave(a, y, r):
        edge = max(0.0, r - 0.6) / 0.4
        return r, y + 0.06 * math.sin(6.0 * a + 0.7) * edge

    b.lathe(profile, 28, wave)
    return b.build("Flat", (0.8, 0.6, 0.4))


def funnel():
    b = Builder()
    profile = [(0.0, 0.78, 1.0)]
    for k in range(1, 7):
        t = k / 6
        profile.append((t, 0.78 + 0.22 * t ** 1.6, 1.0))
    profile.append((1.02, 0.9, 0.0))
    for k in range(1, 6):
        t = k / 5
        profile.append((1.0 - 0.8 * t ** 0.7, 0.9 - 0.9 * t, 0.0))
    profile.append((0.0, 0.0, 0.0))

    def wavy(a, y, r):
        edge = max(0.0, r - 0.7) / 0.3
        w = 0.08 * math.sin(5.0 * a + 0.3) + 0.04 * math.sin(11.0 * a + 1.0)
        return r * (1.0 + w * edge), y + 0.6 * w * edge

    b.lathe(profile, 28, wavy)
    return b.build("Funnel", (1.0, 0.7, 0.1))


def bell():
    b = Builder()
    profile = [(0.0, 1.0, 1.0)]
    for k in range(1, 10):
        t = k / 9
        profile.append((math.sin(t * math.pi * 0.5) ** 0.7, math.cos(t * math.pi * 0.5) ** 0.45, 1.0))
    profile.append((0.92, 0.05, 0.0))
    profile.append((0.7, 0.6, 0.0))
    profile.append((0.0, 0.85, 0.0))

    def shaggy(a, y, r):
        return r * (1.0 + 0.05 * math.sin(14.0 * a) * (1.0 - y)), y

    b.lathe(profile, 24, shaggy)
    return b.build("Bell", (0.9, 0.85, 0.95))


def cone():
    b = Builder()
    profile = [(0.0, 1.0, 1.0)]
    for k in range(1, 9):
        t = k / 8
        profile.append((t ** 0.75 * (1.0 + 0.05 * math.sin(t * 9.0)), 1.0 - t, 1.0))
    profile.append((0.5, 0.0, 0.0))
    profile.append((0.0, 0.0, 0.0))
    b.lathe(profile, 18)
    return b.build("Cone", (0.8, 0.6, 0.3))


def ball():
    b = Builder()
    profile = [(0.0, 1.0, 1.0)]
    for k in range(1, 10):
        t = k / 10
        profile.append((math.sin(t * math.pi) * (1.0 - 0.15 * t * t), 0.5 + 0.5 * math.cos(t * math.pi), 1.0))
    profile.append((0.0, 0.0, 1.0))
    b.lathe(profile, 20)
    return b.build("Ball", (0.95, 0.92, 1.0))


def cup():
    b = Builder()
    profile = [(0.0, 0.0, 0.0)]
    for k in range(1, 7):
        t = k / 6
        profile.append((math.sin(t * math.pi * 0.5), 1.0 - math.cos(t * math.pi * 0.5), 0.0))
    profile.append((1.02, 1.0, 1.0))
    for k in range(1, 6):
        t = k / 5
        profile.append((0.92 * math.cos(t * math.pi * 0.5), 1.0 - 0.75 * math.sin(t * math.pi * 0.5), 1.0))
    profile.append((0.0, 0.25, 1.0))
    profile.reverse()

    def frill(a, y, r):
        return r * (1.0 + 0.05 * math.sin(7.0 * a) * y), y + 0.04 * math.sin(7.0 * a) * y

    b.lathe(profile, 20, frill)
    return b.build("Cup", (0.95, 0.1, 0.12))


def blob(b, center, radius, segments=10):
    """A small round lump (a sphere of radius about `radius`), all of it top surface."""
    cx, cy, cz = center
    profile = [(0.0, radius, 1.0)]
    for k in range(1, 6):
        t = k / 6
        profile.append((radius * math.sin(t * math.pi), radius * math.cos(t * math.pi), 1.0))
    profile.append((0.0, -radius, 1.0))
    b.lathe(profile, segments, center=center)


def tube(b, a, c, ra, rc, segs=7):
    """A tapering tube from point a (radius ra) to point c (radius rc), capped by a round tip."""
    dx, dy, dz = c[0] - a[0], c[1] - a[1], c[2] - a[2]
    ln = math.sqrt(dx * dx + dy * dy + dz * dz)
    d = (dx / ln, dy / ln, dz / ln)
    ref = (1.0, 0.0, 0.0) if abs(d[1]) > 0.9 else (0.0, 1.0, 0.0)
    u = (d[1] * ref[2] - d[2] * ref[1], d[2] * ref[0] - d[0] * ref[2], d[0] * ref[1] - d[1] * ref[0])
    ul = math.sqrt(sum(x * x for x in u))
    u = tuple(x / ul for x in u)
    w = (d[1] * u[2] - d[2] * u[1], d[2] * u[0] - d[0] * u[2], d[0] * u[1] - d[1] * u[0])
    rings = []
    for p, r in ((a, ra), (c, rc)):
        ring = []
        for s in range(segs):
            t = TAU * s / segs
            ring.append(b.vert(tuple(p[i] + r * (math.cos(t) * u[i] + math.sin(t) * w[i]) for i in range(3)), (s / segs, 1.0)))
        rings.append(ring)
    for s in range(segs):
        t = (s + 1) % segs
        b.faces.append((rings[0][s], rings[0][t], rings[1][t], rings[1][s]))
    bottom = b.vert(a, (0.5, 1.0))
    for s in range(segs):
        b.faces.append((bottom, rings[0][(s + 1) % segs], rings[0][s]))
    blob(b, c, rc * 1.25, 8)


def coral():
    b = Builder()
    # A trunk splitting twice into upturned arms, each ending in a round tip, all inside radius 1, height 1.
    def branch(a, d, r, depth, k):
        c = (a[0] + d[0], a[1] + d[1], a[2] + d[2])
        tube(b, a, c, r, r * 0.72)
        if depth == 0:
            return
        for j in range(3 if depth == 2 else 2):
            ang = TAU * (j + hash1(k, j)) / (3 if depth == 2 else 2)
            spread = 0.55 + 0.25 * hash1(k, j + 5)
            ln = math.sqrt(d[0] ** 2 + d[1] ** 2 + d[2] ** 2) * 0.85
            nd = (d[0] * 0.4 + spread * ln * math.cos(ang), ln * 0.85, d[2] * 0.4 + spread * ln * math.sin(ang))
            branch(c, nd, r * 0.72, depth - 1, k * 4 + j + 1)

    for j in range(3):
        ang = TAU * j / 3 + 0.4
        branch((0.15 * math.cos(ang), 0.0, 0.15 * math.sin(ang)), (0.12 * math.cos(ang), 0.3, 0.12 * math.sin(ang)), 0.13, 2, j + 1)
    return b.build("Coral", (1.0, 0.45, 0.55))


def jelly():
    b = Builder()
    for k in range(7):
        a = TAU * k / 7 + 0.5 * hash1(k, 7)
        d = 0.15 + 0.45 * hash1(k, 8)
        r = 0.32 + 0.12 * hash1(k, 9)
        y = r * 0.9 + 0.35 * hash1(k, 10) * (1.0 - d)
        blob(b, (d * math.cos(a), min(y, 1.0 - r), d * math.sin(a)), r, 12)
    return b.build("Jelly", (1.0, 0.55, 0.1))


def web():
    b = Builder()
    spokes = 9
    up = (0.0, 1.0, 0.0)
    angles = [TAU * (s + 0.3 * (hash1(s, 1) - 0.5)) / spokes for s in range(spokes)]
    for s, a in enumerate(angles):
        reach = 0.85 + 0.15 * hash1(s, 2)
        b.ribbon([(0.0, 0.0, 0.0), (reach * math.cos(a), 0.0, reach * math.sin(a))], 0.018, up)
    # The spiral, sagging a little between spokes.
    turns, points = 6, []
    total = turns * spokes * 4
    for k in range(total + 1):
        t = k / total
        a = angles[0] + TAU * turns * t
        r = 0.12 + 0.75 * t
        sag = 0.92 + 0.08 * abs(math.cos(spokes * a * 0.5))
        points.append((r * sag * math.cos(a), 0.0, r * sag * math.sin(a)))
    b.ribbon(points, 0.012, up)
    return b.build("Web", (0.85, 0.86, 0.95), recalc=False)


def root():
    b = Builder()
    rows, segs = 12, 8
    profile_pts = []
    for k in range(rows + 1):
        t = k / rows
        x = t
        y = 0.3 * math.sin(t * math.pi) ** 0.8 - 0.08 * t
        z = 0.06 * math.sin(t * 5.0)
        profile_pts.append((x, y, z, 0.22 * (1.0 - t) + 0.04))
    first = len(b.verts)
    rings = []
    for k, (x, y, z, r) in enumerate(profile_pts):
        ring = []
        for s in range(segs):
            a = TAU * s / segs
            ring.append(b.vert((x, y + r * math.cos(a), z + r * math.sin(a)), (s / segs, k / rows)))
        rings.append(ring)
    for k in range(rows):
        for s in range(segs):
            t = (s + 1) % segs
            b.faces.append((rings[k][s], rings[k][t], rings[k + 1][t], rings[k + 1][s]))
    start = b.vert(profile_pts[0][:3])
    end = b.vert(profile_pts[-1][:3])
    for s in range(segs):
        t = (s + 1) % segs
        b.faces.append((start, rings[0][t], rings[0][s]))
        b.faces.append((end, rings[-1][s], rings[-1][t]))
    return b.build("Root", (0.36, 0.26, 0.22))


def fern():
    b = Builder()
    fronds = 7
    for f in range(fronds):
        a = TAU * f / fronds + 0.4 * hash1(f, 3)
        length = 0.5 + 0.15 * hash1(f, 4)
        lift = 0.55 + 0.25 * hash1(f, 5)
        pts = []
        for k in range(7):
            t = k / 6
            d = length * t
            y = lift * math.sin(t * math.pi * 0.75) * (1.0 - 0.3 * t)
            pts.append((d * math.cos(a), y, d * math.sin(a)))
        b.ribbon(pts, lambda t: 0.16 * math.sin(math.pi * (0.15 + 0.85 * t)) + 0.02, uv_v=None)
    return b.build("Fern", (0.2, 0.36, 0.26), recalc=False)


def main():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    for make in (stem, skirt, cap, shelf, web, root, fern, stalk, dome, flat, funnel, bell, cone, ball, cup, coral, jelly):
        make()
    out = os.path.join(HERE, "deep_forest.glb")
    bpy.ops.export_scene.gltf(filepath=out, export_format="GLB", export_apply=True,
                              export_yup=True, export_materials="EXPORT")
    print(f"deep_forest.py: wrote {out}")


main()
