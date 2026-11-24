"""Living Island props - three palm variants (LOD0/LOD1, curved trunks, fronds, wind vertex colours).

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


# ============================================================================ palms
# Trunk UV: u around (0..1), v = height fraction. Frond UV: u across (0 left, .5 midrib, 1 right), v along.
# Vertex colour "Col" (FLOAT_COLOR, POINT): R trunk bend weight, G frond flutter (=v), B per-frond phase, A 1.
PALM_VARIANTS = [
    dict(curve="C", lean=0.8, lean_az=25.0, trunk_h=6.25, fronds=8, coconuts=4, seed=1101, crown_az=10.0),
    dict(curve="LEAN", lean=1.8, lean_az=-40.0, trunk_h=5.95, fronds=9, coconuts=4, seed=2203, crown_az=55.0),
    dict(curve="S", lean=0.55, lean_az=150.0, trunk_h=6.15, fronds=10, coconuts=3, seed=3307, crown_az=-20.0),
]
PALM_LOD = {
    0: dict(trunk_segs=32, trunk_sides=16, dome_rings=2, frond_segs=12, frond_across=3, coconuts=True),
    1: dict(trunk_segs=16, trunk_sides=10, dome_rings=1, frond_segs=6, frond_across=2, coconuts=False),
}


def palm_centerline(var, t):
    """Trunk axis point at height fraction t (0..1); z = t * trunk_h."""
    h = var["trunk_h"]
    lean = var["lean"]
    if var["curve"] == "C":        # gentle C: leans out, curves back up towards vertical
        off, side = lean * (1.0 - (1.0 - t) ** 1.7), 0.12 * math.sin(math.pi * t)
    elif var["curve"] == "LEAN":   # strong lean from the base, top turning up
        off, side = lean * (1.0 - (1.0 - t) ** 2.1), -0.18 * math.sin(math.pi * t)
    else:                          # S-curve
        off, side = lean * math.sin(1.5 * math.pi * t) + 0.32 * t, 0.15 * math.sin(math.pi * t)
    a = math.radians(var["lean_az"])
    d = Vector((math.cos(a), math.sin(a), 0.0))
    p = Vector((-math.sin(a), math.cos(a), 0.0))
    return d * off + p * side + UP * (t * h)


def palm_radius(var, z, h):
    t = clamp(z / h)
    r = lerp(0.22, 0.12, t ** 0.85)
    r += 0.10 * math.exp(-z / 0.22)                       # root flare
    r += 0.045 * smoothstep(h - 0.6, h - 0.1, z)          # slight crown-shaft bulge under the fronds
    return r


def palm_frond_params(var, cfg):
    rng = random.Random(var["seed"])
    n = int(clamp(var["fronds"], cfg["PalmFrondsMin"], cfg["PalmFrondsMax"]))
    fronds = []
    base_az = rng.uniform(0.0, TAU)
    for j in range(n):
        age = clamp(0.5 * (j % 2) + rng.uniform(0.0, 0.5))          # 0 young/upright .. 1 old/droopy
        elev0 = math.radians(lerp(60.0, 18.0, age) + rng.uniform(-6.0, 6.0))
        bend = math.radians(lerp(88.0, 145.0, age) + rng.uniform(-10.0, 10.0))
        bend = min(bend, elev0 + math.radians(rng.uniform(70.0, 88.0)))
        fronds.append(dict(
            az=base_az + TAU * j / n + rng.uniform(-0.28, 0.28) * TAU / n,
            elev0=elev0, bend=bend,
            length=lerp(2.65, 3.35, rng.random()),
            width=rng.uniform(0.92, 1.04),
            yaw=rng.uniform(-0.22, 0.22),
            roll=math.radians(rng.uniform(-22.0, 22.0)),
            dz=lerp(0.10, -0.06, age) + rng.uniform(-0.03, 0.03),
            r_att=rng.uniform(0.06, 0.11),
            phase=rng.random(),
        ))
    return fronds


def frond_spine(fp, origin, steps=240):
    """Fine polyline of the frond rachis: rises at elev0, bends down with gravity (more towards the tip)."""
    pts, dirs, sides = [Vector(origin)], [], []
    L = fp["length"]
    ds = L / steps
    p = Vector(origin)
    for k in range(steps + 1):
        s = min(k + 0.5, steps) * ds
        v = s / L
        az = fp["az"] + fp["yaw"] * v * v
        alpha = fp["elev0"] - fp["bend"] * v ** 1.6
        h = Vector((math.cos(az), math.sin(az), 0.0))
        d = h * math.cos(alpha) + UP * math.sin(alpha)
        side = Vector((-math.sin(az), math.cos(az), 0.0))
        side = Matrix.Rotation(fp["roll"] * v, 3, d) @ side
        dirs.append(d)
        sides.append(side)
        if k < steps:
            p = p + d * ds
            pts.append(p.copy())
    return pts, dirs, sides


def _sample(lst, v):
    x = v * (len(lst) - 1)
    i = min(int(x), len(lst) - 2)
    f = x - i
    return lst[i].lerp(lst[i + 1], f)


def frond_width(v):
    rise = smoothstep(0.04, 0.38, v)
    fall = 1.0 - 0.96 * (max(v - 0.38, 0.0) / 0.62) ** 1.4
    return lerp(0.10, 1.0, rise) * fall


def build_palm_lod(var, cfg, lod, fronds):
    q = PALM_LOD[lod]
    mb = MeshBuilder(["M_PalmTrunk", "M_PalmFrond"] + (["M_Coconut"] if q["coconuts"] else []))
    h = var["trunk_h"]
    rng = random.Random(var["seed"] * 7 + lod)
    noise_off = Vector((var["seed"] * 0.37, 1.3, 2.1))

    # ---- trunk rings (bottom ring horizontal at z = 0, later rings perpendicular to the axis)
    nseg, nside = q["trunk_segs"], q["trunk_sides"]
    ring_data = []                                   # (center, radius, frame, v_param)
    for i in range(nseg + 1):
        t = (i / nseg) ** 1.12
        c = palm_centerline(var, t)
        tan = (palm_centerline(var, min(t + 0.01, 1.0)) - palm_centerline(var, max(t - 0.01, 0.0))).normalized()
        tan = UP.lerp(tan, smoothstep(0.0, 0.45, c.z)).normalized()
        r = palm_radius(var, c.z, h)
        if lod == 0 and 0 < i < nseg - 1:
            r *= 1.028 if i % 2 == 0 else 0.986      # subtle ring bumps
        ring_data.append((c, r, frame_from_tangent(tan, Vector((1, 0, 0)))))
    # crown dome above the last ring
    c_top, r_top, fr_top = ring_data[-1]
    dome = [(0.10, 0.78), (0.19, 0.42)] if q["dome_rings"] == 2 else [(0.15, 0.6)]
    for dz, k in dome:
        ring_data.append((c_top + fr_top[2] * dz, r_top * k, fr_top))
    apex = c_top + fr_top[2] * (0.24)
    z_apex = apex.z

    def trunk_col(z):
        return (clamp((z / z_apex) ** 2), 0.0, 0.0, 1.0)

    rings = []
    for (c, r, (n_, b_, t_)) in ring_data:
        ring = []
        for k in range(nside):
            a = TAU * k / nside
            rr = r
            if lod == 0:
                rr *= 1.0 + 0.035 * noise.noise(noise_off + Vector((math.cos(a) * 1.5, math.sin(a) * 1.5, c.z * 1.3)))
            pos = c + (n_ * math.cos(a) + b_ * math.sin(a)) * rr
            if c.z < 1e-6:
                pos.z = 0.0
            ring.append(mb.v(pos, trunk_col(pos.z)))
        rings.append((ring, c.z / z_apex))
    for (r0, v0), (r1, v1) in zip(rings[:-1], rings[1:]):
        mb.ring_quads(r0, r1, v0, v1, "M_PalmTrunk")
    apex_i = mb.v(apex, trunk_col(apex.z))
    last_ring = rings[-1][0]
    n = len(last_ring)
    for k in range(n):
        k2 = (k + 1) % n
        u0, u1 = k / n, (k + 1) / n
        mb.f((last_ring[k], last_ring[k2], apex_i), [(u0, rings[-1][1]), (u1, rings[-1][1]), ((u0 + u1) * 0.5, 1.0)],
             "M_PalmTrunk")
    bottom = rings[0][0]
    mb.f(list(reversed(bottom)), [((nside - 1 - k) / nside, 0.0) for k in range(nside)], "M_PalmTrunk")

    # ---- fronds
    crown = c_top + fr_top[2] * 0.10
    for fp in fronds:
        hdir = Vector((math.cos(fp["az"]), math.sin(fp["az"]), 0.0))
        origin = crown + hdir * fp["r_att"] + UP * fp["dz"]
        pts, dirs, sides = frond_spine(fp, origin)
        segs, across = q["frond_segs"], q["frond_across"]
        rows = []
        for i in range(segs + 1):
            v = i / segs
            p = _sample(pts, v)
            d = _sample(dirs, v).normalized()
            sd = _sample(sides, v).normalized()
            upl = d.cross(sd).normalized()
            w = fp["width"] * frond_width(v)
            col = (1.0, v, fp["phase"], 1.0)
            if across == 3:
                drop = 0.12 * frond_width(v)
                left = p + sd * (0.5 * w) - upl * drop
                right = p - sd * (0.5 * w) - upl * drop
                rows.append(([mb.v(left, col), mb.v(p, col), mb.v(right, col)], v))
            else:
                rows.append(([mb.v(p + sd * (0.5 * w), col), mb.v(p - sd * (0.5 * w), col)], v))
        us = [0.0, 0.5, 1.0] if across == 3 else [0.0, 1.0]
        for (ra, va), (rb, vb) in zip(rows[:-1], rows[1:]):
            for c in range(across - 1):
                mb.f((ra[c], ra[c + 1], rb[c + 1], rb[c]),
                     [(us[c], va), (us[c + 1], va), (us[c + 1], vb), (us[c], vb)], "M_PalmFrond")

    # ---- coconuts (LOD0 only)
    if q["coconuts"]:
        nc = var["coconuts"]
        az0 = math.radians(var["crown_az"])
        ring_c = c_top - fr_top[2] * 0.12
        for k in range(nc):
            az = az0 + (k - (nc - 1) * 0.5) * 0.62 + rng.uniform(-0.12, 0.12)
            hd = Vector((math.cos(az), math.sin(az), 0.0))
            center = ring_c + hd * (r_top + 0.085) - UP * rng.uniform(0.06, 0.2)
            axis = (-UP * 0.8 + hd * 0.6).normalized()       # hanging, tilted outwards
            add_coconut(mb, center, axis, rng)
    return mb


def add_coconut(mb, center, axis, rng, segs=10, rings=7, radius=0.12, length=0.15):
    n_, b_, t_ = frame_from_tangent(axis)
    col = (1.0, 0.0, 0.0, 1.0)
    tri = rng.uniform(0.0, TAU)
    lat_rings = []
    for i in range(1, rings):
        th = math.pi * i / rings                  # 0 at the stalk end (axis start)
        ring = []
        for k in range(segs):
            a = TAU * k / segs
            r = radius * math.sin(th) * (1.0 + 0.07 * math.cos(3.0 * a + tri))
            pos = center + t_ * (-length * math.cos(th)) + (n_ * math.cos(a) + b_ * math.sin(a)) * r
            ring.append(mb.v(pos, col))
        lat_rings.append((ring, i / rings))
    top = mb.v(center - t_ * length, col)
    bot = mb.v(center + t_ * length * 1.08, col)
    for (r0, v0), (r1, v1) in zip(lat_rings[:-1], lat_rings[1:]):
        mb.ring_quads(r0, r1, v0, v1, "M_Coconut")
    first, last = lat_rings[0][0], lat_rings[-1][0]
    for k in range(segs):
        k2 = (k + 1) % segs
        mb.f((first[k2], first[k], top), [((k + 1) / segs, lat_rings[0][1]), (k / segs, lat_rings[0][1]),
                                         ((k + 0.5) / segs, 0.0)], "M_Coconut")
        mb.f((last[k], last[k2], bot), [(k / segs, lat_rings[-1][1]), ((k + 1) / segs, lat_rings[-1][1]),
                                       ((k + 0.5) / segs, 1.0)], "M_Coconut")


def build_palms(cfg, col):
    objs = []
    nvar = int(cfg["PalmVariants"])
    for vi in range(nvar):
        var = PALM_VARIANTS[vi % len(PALM_VARIANTS)]
        name = "SM_Palm_%02d" % (vi + 1)
        fronds = palm_frond_params(var, cfg)
        lods = {lod: build_palm_lod(var, cfg, lod, fronds) for lod in (0, 1)}
        top = max(c.z for c in lods[0].co)
        s = cfg["PalmModelHeight"] / top                     # exact modelled height
        for mb in lods.values():
            mb.transform(Matrix.Scale(s, 4))
        pair = []
        for lod, mb in lods.items():
            oname = "%s_LOD%d" % (name, lod)
            me = mb.build(oname, colors=True)
            finish_shading(me, 60.0)
            ob = new_object(oname, me, col, location=(vi * 7.0, lod * 8.0, 0.0))
            pair.append(ob)
        objs.append((name, pair))
        log("%s: fronds=%d scale=%.3f" % (name, len(fronds), s))
    return objs
