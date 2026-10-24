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
#   Glow    a small glowing mushroom, 0.75 high: UV.y is 1 on its cap, 0 on its stem.
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


def glow():
    b = Builder()
    stem_profile = [(0.0, 0.62, 0.0)]
    for k in range(7):
        t = 1.0 - k / 6
        stem_profile.append((0.07 + 0.03 * (1.0 - t) ** 2, 0.62 * t, 0.0))
    stem_profile.append((0.0, 0.0, 0.0))
    b.lathe(stem_profile, 10, lambda a, y, r: r * (1.0 + 0.1 * math.sin(3.0 * a + 8.0 * y)))
    cap_profile = [(0.0, 0.78, 1.0)]
    for k in range(1, 7):
        t = k / 6
        cap_profile.append((0.3 * math.sin(t * math.pi * 0.5), 0.56 + 0.22 * math.cos(t * math.pi * 0.5), 1.0))
    cap_profile.append((0.2, 0.56, 1.0))
    cap_profile.append((0.0, 0.6, 1.0))
    b.lathe(cap_profile, 16)
    return b.build("Glow", (0.6, 0.85, 0.95))


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
    for make in (stem, skirt, cap, shelf, glow, web, root, fern):
        make()
    out = os.path.join(HERE, "deep_forest.glb")
    bpy.ops.export_scene.gltf(filepath=out, export_format="GLB", export_apply=True,
                              export_yup=True, export_materials="EXPORT")
    print(f"deep_forest.py: wrote {out}")


main()
