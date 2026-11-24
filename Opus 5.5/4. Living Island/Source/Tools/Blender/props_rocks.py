"""Living Island props - five noise-displaced rock variants (SDF shapes projected onto a sphere mesh).

Part of build_island_props.py (split by responsibility; run the main script)."""
import bpy
import bmesh
import math
import os
import re
import json
import struct
import random
from mathutils import Vector, Matrix, noise
from props_core import *  # noqa: F401,F403 (shared helpers/constants)
from props_geometry import *  # noqa: F401,F403 (shared helpers/constants)


# ============================================================================ rocks
# Each rock = signed-distance shape (ellipsoids, plane cuts, layers, noise) sampled by projecting a dense
# icosphere radially onto it (outermost hit), then collapse-decimated to the triangle target.
ROCKS = [
    dict(kind="boulder", seed=501, target=1250, sharp=80.0, c0=(0.0, 0.0, 0.34)),
    dict(kind="slab", seed=502, target=1150, sharp=50.0, c0=(0.0, 0.0, 0.18)),
    dict(kind="tall", seed=503, target=1300, sharp=42.0, c0=(0.0, 0.0, 0.80)),
    dict(kind="split", seed=504, target=1350, sharp=55.0, c0=(0.0, 0.0, 0.18)),
    dict(kind="ledges", seed=505, target=1300, sharp=48.0, c0=(0.05, 0.0, 0.20)),
]


def sd_ellipsoid(p, c, r):
    q = Vector(((p.x - c[0]) / r[0], (p.y - c[1]) / r[1], (p.z - c[2]) / r[2]))
    return (q.length - 1.0) * min(r)


def smin(a, b, k):
    h = clamp(0.5 + 0.5 * (b - a) / k)
    return lerp(b, a, h) - k * h * (1.0 - h)


def smax(a, b, k):
    return -smin(-a, -b, k)


def random_cuts(rng, n, d_lo, d_hi, center, el_lo, el_hi):
    cuts = []
    for _ in range(n):
        az = rng.uniform(0.0, TAU)
        el = rng.uniform(el_lo, el_hi)
        nrm = Vector((math.cos(el) * math.cos(az), math.cos(el) * math.sin(az), math.sin(el)))
        cuts.append((nrm, Vector(center) + nrm * rng.uniform(d_lo, d_hi)))
    return cuts


def make_rock_sdf(kind, rng):
    o1 = Vector((rng.uniform(-50, 50), rng.uniform(-50, 50), rng.uniform(-50, 50)))
    o2 = Vector((rng.uniform(-50, 50), rng.uniform(-50, 50), rng.uniform(-50, 50)))
    if kind == "boulder":
        c, r = (0.0, 0.0, 0.34), (0.78, 0.64, 0.56)
        cuts = random_cuts(rng, 4, 0.50, 0.60, c, -0.1, 0.9)

        def sdf(p):
            d = sd_ellipsoid(p, c, r) + 0.07 * fbm(p * 1.3 + o1, 4) + 0.028 * fbm(p * 3.2 + o2, 3)
            for nrm, pt in cuts:
                d = smax(d, (p - pt).dot(nrm), 0.09)
            return max(d, -p.z)
    elif kind == "slab":
        c, r = (0.0, 0.0, 0.16), (1.0, 0.68, 0.36)
        top_n = Vector((0.07, -0.05, 1.0)).normalized()
        top_p = Vector((0.0, 0.0, 0.40))
        cuts = random_cuts(rng, 6, 0.52, 0.78, c, -0.25, 0.35)

        def sdf(p):
            d = sd_ellipsoid(p, c, r) + 0.035 * fbm(p * 1.8 + o1, 4)
            d += 0.010 * math.sin(p.z * 48.0 + 2.5 * noise.noise(p * 2.0 + o2))      # strata lines
            d = smax(d, (p - top_p).dot(top_n), 0.05)
            for nrm, pt in cuts:
                d = smax(d, (p - pt).dot(nrm), 0.03)
            return max(d, -p.z)
    elif kind == "tall":
        # tapered prism: 7 big side facets (tilted slightly upwards) + a ridged, slanted top
        c, r = (0.0, 0.0, 0.85), (0.70, 0.60, 1.20)
        cuts = []
        base_az = rng.uniform(0.0, TAU)
        for k in range(7):
            az = base_az + TAU * k / 7 + rng.uniform(-0.25, 0.25)
            el = rng.uniform(0.04, 0.2)
            nrm = Vector((math.cos(el) * math.cos(az), math.cos(el) * math.sin(az), math.sin(el)))
            sx = 1.0 if abs(math.cos(az)) > abs(math.sin(az)) else 0.82      # slightly flattened in y
            cuts.append((nrm, Vector(c) + nrm * rng.uniform(0.30, 0.38) * sx))
        top_az = rng.uniform(0.0, TAU)
        for k, el in enumerate((0.95, 1.05)):
            az = top_az + math.pi * k
            nrm = Vector((math.cos(el) * math.cos(az), math.cos(el) * math.sin(az), math.sin(el)))
            cuts.append((nrm, Vector(c) + nrm * (0.88 if k == 0 else 0.80)))

        def sdf(p):
            d = sd_ellipsoid(p, c, r) + 0.025 * fbm(p * 2.2 + o1, 4)
            for nrm, pt in cuts:
                d = max(d, (p - pt).dot(nrm) + 0.012 * noise.noise(p * 5.0 + o2))
            return max(d, -p.z)
    elif kind == "split":
        A, ra = (-0.34, 0.0, 0.40), (0.62, 0.56, 0.52)
        B, rb = (0.36, 0.06, 0.34), (0.55, 0.50, 0.46)
        cuts = random_cuts(rng, 3, 0.45, 0.55, (0.0, 0.0, 0.38), 0.2, 1.0)

        def sdf(p):
            d = smin(sd_ellipsoid(p, A, ra), sd_ellipsoid(p, B, rb), 0.12)
            d += 0.045 * fbm(p * 1.7 + o1, 4) + 0.012 * fbm(p * 5.0 + o2, 3)
            for nrm, pt in cuts:
                d = smax(d, (p - pt).dot(nrm), 0.1)
            # the crack: a smooth V groove between the two lumps (additive, so the radial sampling stays clean)
            wobble = 0.05 * noise.noise(Vector((0.0, p.y * 2.2, p.z * 2.2)) + o2)
            x = (p.x - 0.03 - wobble) / 0.058
            d += 0.21 * smoothstep(0.22, 0.80, p.z) * math.exp(-x * x)
            return max(d, -p.z)
    else:  # ledges: stacked, shrinking layers
        layers = [(-0.10, 0.24, (1.00, 0.76), (0.00, 0.00)),
                  (0.24, 0.43, (0.80, 0.60), (0.10, -0.04)),
                  (0.43, 0.60, (0.55, 0.42), (0.18, 0.03))]

        def sdf(p):
            d = 1e9
            zt = p.z + 0.05 * p.x - 0.035 * p.y                                    # dipping strata
            for k, (z0, z1, rr, off) in enumerate(layers):
                q = Vector(((p.x - off[0]) / rr[0], (p.y - off[1]) / rr[1]))
                dxy = (q.length - 1.0) * min(rr) + 0.12 * fbm(Vector((p.x, p.y, k * 3.1)) * 1.15 + o1, 3)
                ztop = z1 + 0.035 * noise.noise(Vector((p.x * 2.0, p.y * 2.0, k * 5.7)) + o2)
                dz = max(z0 - zt, zt - ztop)
                d = smin(d, max(dxy, dz), 0.035)
            d += 0.018 * fbm(p * 3.2 + o2, 3)
            return max(d, -p.z)
    return sdf


def project_star(bm, sdf, c0, r_max=1.7, step=0.05, iters=10):
    c0 = Vector(c0)
    for v in bm.verts:
        d = v.co.normalized()
        r, prev = r_max, r_max
        while r > 0.0 and sdf(c0 + d * r) > 0.0:
            prev = r
            r -= step
        lo, hi = max(r, 0.0), prev
        for _ in range(iters):
            mid = 0.5 * (lo + hi)
            if sdf(c0 + d * mid) > 0.0:
                hi = mid
            else:
                lo = mid
        v.co = c0 + d * (0.5 * (lo + hi))


def build_rocks(cfg, col):
    objs = []
    for i in range(int(cfg["RockVariants"])):
        spec = ROCKS[i % len(ROCKS)]
        name = "SM_Rock_%02d" % (i + 1)
        rng = random.Random(spec["seed"])
        sdf = make_rock_sdf(spec["kind"], rng)
        bm = bmesh.new()
        bmesh.ops.create_icosphere(bm, subdivisions=5, radius=1.0)
        project_star(bm, sdf, spec["c0"])
        tmp = bpy.data.meshes.new(name + "_hi")
        bm.to_mesh(tmp)
        bm.free()
        ob = new_object(name, tmp, col, location=(i * 3.2, -9.0, 0.0))
        target = min(spec["target"], int(cfg["RockTrisMax"] * 0.93))
        mod = ob.modifiers.new("Decimate", "DECIMATE")
        mod.decimate_type = "COLLAPSE"
        mod.ratio = target / len(tmp.polygons)
        mod.use_collapse_triangulate = True
        dg = bpy.context.evaluated_depsgraph_get()
        low = bpy.data.meshes.new_from_object(ob.evaluated_get(dg))
        ob.modifiers.remove(mod)
        old = bpy.data.meshes.get(name)
        if old is not None and old.users == 0:
            bpy.data.meshes.remove(old)
        low.name = name
        ob.data = low
        bpy.data.meshes.remove(tmp)
        # cleanup + recentre: x/y on the bbox centre, flat ground contact 4 cm below z = 0
        bm = bmesh.new()
        bm.from_mesh(low)
        bmesh.ops.remove_doubles(bm, verts=bm.verts, dist=1e-4)
        bmesh.ops.dissolve_degenerate(bm, edges=bm.edges, dist=1e-5)
        bmesh.ops.triangulate(bm, faces=bm.faces)
        bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
        xs = [v.co.x for v in bm.verts]
        ys = [v.co.y for v in bm.verts]
        zmin = min(v.co.z for v in bm.verts)
        shift = Vector((-(min(xs) + max(xs)) * 0.5, -(min(ys) + max(ys)) * 0.5, -zmin - 0.04))
        for v in bm.verts:
            v.co += shift
        bm.to_mesh(low)
        bm.free()
        low.materials.clear()
        low.materials.append(get_material("M_Rock"))
        smart_uv(ob, 66.0, 0.01)
        finish_shading(low, spec["sharp"])
        objs.append((name, [ob]))
        log("%s (%s): tris=%d" % (name, spec["kind"], len(low.polygons)))
    return objs
