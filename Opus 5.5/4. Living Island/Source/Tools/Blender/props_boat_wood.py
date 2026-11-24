"""Living Island props - rowboat, driftwood log seat and campfire logs.

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


# ============================================================================ rowboat
# Clinker hull from stations along Y (stern -> bow). Every half-section is one chain: outer keel -> lapped
# planks -> rub rail -> gunwale top -> inner skin -> inner keel; both halves share the centre-line vertices.
BOAT = dict(y0=-1.66, y1=1.66, bmax=0.65, planks=6, lap=0.014, rail=0.022, skin=0.03,
            stations=18, t_end=0.985, rake_stern=0.14, rake_bow=0.15)


def boat_breadth(t):
    if t < 0.42:
        return BOAT["bmax"] * (0.72 + 0.28 * math.sin(0.5 * math.pi * t / 0.42))
    return BOAT["bmax"] * max(0.0, 1.0 - ((t - 0.42) / 0.58) ** 1.8)


def boat_keel_z(t):
    z = -0.19 + 0.42 * max(0.0, (t - 0.6) / 0.4) ** 2.2
    if t < 0.3:
        z += 0.09 * (1.0 - t / 0.3) ** 2
    return z


def boat_sheer_z(t):
    z = 0.45 + 0.16 * smoothstep(0.55, 1.0, t) ** 1.3
    if t < 0.35:
        z += 0.06 * (1.0 - t / 0.35) ** 2
    return z


def boat_fullness(t):
    return lerp(lerp(2.4, 2.6, smoothstep(0.0, 0.4, t)), 1.45, smoothstep(0.45, 1.0, t))


def boat_section(t, s):
    """Base hull point (right side, x >= 0) and outward section normal at girth fraction s (0 keel .. 1 sheer)."""
    b, zk, zs, n = boat_breadth(t), boat_keel_z(t), boat_sheer_z(t), boat_fullness(t)
    # arc-length parametrisation of the superellipse quarter
    samples = 48
    pts = []
    for i in range(samples + 1):
        th = 0.5 * math.pi * i / samples
        pts.append(Vector((b * math.sin(th) ** (2.0 / n), zs - (zs - zk) * math.cos(th) ** (2.0 / n))))
    acc = [0.0]
    for i in range(samples):
        acc.append(acc[-1] + (pts[i + 1] - pts[i]).length)
    target = s * acc[-1]
    i = 0
    while i < samples - 1 and acc[i + 1] < target:
        i += 1
    f = (target - acc[i]) / max(acc[i + 1] - acc[i], 1e-9)
    p = pts[i].lerp(pts[i + 1], f)
    tan = (pts[min(i + 1, samples)] - pts[max(i, 0)])
    if tan.length < 1e-9:
        tan = Vector((1.0, 0.0))
    tan.normalize()
    nrm = Vector((tan.y, -tan.x))
    return p, nrm


def boat_y(t):
    return lerp(BOAT["y0"], BOAT["y1"], t)


def boat_rake(t, z):
    zk, zs = boat_keel_z(t), boat_sheer_z(t)
    f = clamp((z - zk) / max(zs - zk, 1e-6), 0.0, 1.2)
    w_stern = 1.0 - smoothstep(0.0, 0.15, t)
    w_bow = smoothstep(0.75, 1.0, t) ** 1.5
    return -BOAT["rake_stern"] * f * w_stern + BOAT["rake_bow"] * f * w_bow


def boat_half_breadth_at(t, z, inner=True):
    lo, hi = 0.0, 1.0
    for _ in range(30):
        mid = 0.5 * (lo + hi)
        if boat_section(t, mid)[0].y < z:
            lo = mid
        else:
            hi = mid
    p, n = boat_section(t, 0.5 * (lo + hi))
    return p.x - (BOAT["skin"] if inner else 0.0) * n.x


def build_rowboat(cfg, col):
    mb = MeshBuilder(["M_BoatPaint", "M_Wood", "M_WoodLight"])
    P, nst = BOAT["planks"], BOAT["stations"]
    lap, rail, skin = BOAT["lap"], BOAT["rail"], BOAT["skin"]
    ts = []
    for i in range(nst):
        u = i / (nst - 1)
        ts.append(BOAT["t_end"] * (0.7 * u + 0.3 * (1.0 - (1.0 - u) ** 2)))
    plank_s = [k / P * 0.93 for k in range(P)] + [0.93]      # plank boundaries; 0.93..1.0 = rub rail band

    def chain(t, side):
        """List of (position, material-of-next-segment)."""
        sx = 1.0 if side > 0 else -1.0
        out = []

        def P3(p2, off_n=0.0, n2=None):
            q = p2 + (n2 * off_n if n2 is not None else Vector((0.0, 0.0)))
            return Vector((sx * q.x, boat_y(t), q.y))
        keel, _ = boat_section(t, 0.0)
        out.append((Vector((0.0, boat_y(t), keel.y)), "M_BoatPaint"))
        for k in range(1, P):
            p, n = boat_section(t, plank_s[k])
            out.append((P3(p), "M_BoatPaint"))
            out.append((P3(p, lap, n), "M_BoatPaint"))
        p, n = boat_section(t, 0.93)
        out.append((P3(p), "M_Wood"))                          # top plank meets the rub rail
        out.append((P3(p, rail, n), "M_Wood"))
        p, n = boat_section(t, 1.0)
        out.append((P3(p, rail, n), "M_Wood"))                 # rail top, gunwale cap goes inwards
        yin = boat_y(t) if t > 1e-6 else boat_y(t) + 0.035     # transom board thickness
        for s in (1.0, 0.85, 0.66, 0.46, 0.27, 0.1):
            p, n = boat_section(t, s)
            q = p - n * skin
            out.append((Vector((sx * q.x, yin, q.y)), "M_Wood"))
        keel_in = boat_section(t, 0.0)[0].y + skin
        out.append((Vector((0.0, yin, keel_in)), "M_Wood"))
        return out

    right, left = [], []
    for t in ts:
        cr, cl = chain(t, 1), chain(t, -1)
        idx_r, idx_l = [], []
        for j, (pos, _) in enumerate(cr):
            if j == 0 or j == len(cr) - 1:
                idx_r.append(None)                                # filled below (shared centre-line)
            else:
                idx_r.append(mb.v(pos))
        for j, (pos, _) in enumerate(cl):
            idx_l.append(None if j in (0, len(cl) - 1) else mb.v(pos))
        k0, k1 = mb.v(cr[0][0]), mb.v(cr[-1][0])
        idx_r[0] = idx_l[0] = k0
        idx_r[-1] = idx_l[-1] = k1
        right.append((idx_r, [m for _, m in cr], t))
        left.append((idx_l, [m for _, m in cl], t))
    for i in range(nst - 1):
        (ra, mats, ta), (rb, _, tb) = right[i], right[i + 1]
        (la, _, _), (lb, _, _) = left[i], left[i + 1]
        n = len(ra)
        for j in range(n - 1):
            mb.f((ra[j], rb[j], rb[j + 1], ra[j + 1]), None, mats[j])
            mb.f((la[j], la[j + 1], lb[j + 1], lb[j]), None, mats[j])
    # transom: outer board (-Y), inner board (+Y), top edge
    ra, mats, _ = right[0]
    la, _, _ = left[0]
    n_out = 2 * P + 2                      # outer chain: keel .. rail top
    outer_loop = ra[:n_out] + list(reversed(la[1:n_out]))
    cen = sum((mb.co[i] for i in outer_loop), Vector()) / len(outer_loop)
    c_i = mb.v(cen)
    for k in range(len(outer_loop)):
        mb.f((outer_loop[k], outer_loop[(k + 1) % len(outer_loop)], c_i), None, "M_BoatPaint")
    inner_r = ra[n_out:]                   # gunwale-in .. keel-in
    inner_l = la[n_out:]
    inner_loop = inner_r + list(reversed(inner_l[:-1]))
    cen = sum((mb.co[i] for i in inner_loop), Vector()) / len(inner_loop)
    c_i = mb.v(cen)
    for k in range(len(inner_loop)):
        mb.f((inner_loop[k], inner_loop[(k + 1) % len(inner_loop)], c_i), None, "M_Wood")
    mb.f((la[n_out - 1], ra[n_out - 1], ra[n_out], la[n_out]), None, "M_Wood")

    # rake stern / bow (shear in Y proportional to height inside the section)
    for i in range(len(mb.co)):
        c = mb.co[i]
        t = clamp((c.y - BOAT["y0"]) / (BOAT["y1"] - BOAT["y0"]))
        c.y += boat_rake(t, c.z)

    # keel + stem post (swept rectangle along the centre line)
    path, wid, d_out, d_in = [], [], [], []
    for t in ts + [1.0]:
        path.append(Vector((0.0, boat_y(t), boat_keel_z(t))))
        b = smoothstep(0.8, 1.0, t)
        wid.append(lerp(0.035, 0.05, b))
        d_out.append(lerp(0.06, 0.05, b))
        d_in.append(lerp(0.025, 0.07, b))
    zk1, zs1 = boat_keel_z(1.0), boat_sheer_z(1.0)
    for f in (0.2, 0.4, 0.6, 0.8, 1.0, 1.12):
        z = lerp(zk1, zs1, f)
        path.append(Vector((0.0, boat_y(1.0) + boat_rake(1.0, z), z)))
        wid.append(0.05)
        d_out.append(0.05)
        d_in.append(0.07)
    rings = []
    for i, p in enumerate(path):
        tan = (path[min(i + 1, len(path) - 1)] - path[max(i - 1, 0)]).normalized()
        n_p = Vector((0.0, tan.z, -tan.y)).normalized()
        x = Vector((1.0, 0.0, 0.0))
        corners = [(-wid[i], -d_in[i]), (wid[i], -d_in[i]), (wid[i], d_out[i]), (-wid[i], d_out[i])]
        rings.append([mb.v(p + x * a + n_p * b) for a, b in corners])
    for i in range(len(rings) - 1):
        mb.ring_quads(rings[i], rings[i + 1], i / len(rings), (i + 1) / len(rings), "M_Wood")
    mb.cap(rings[0], "M_Wood", top=False)
    mb.cap(rings[-1], "M_Wood", top=True)

    # thwart (bench) + its two side cleats, oarlocks on the gunwale
    yb = -0.05
    tb = (yb - BOAT["y0"]) / (BOAT["y1"] - BOAT["y0"])
    ztop = 0.175
    hb = boat_half_breadth_at(tb, ztop - 0.035) + 0.012
    add_box(mb, (0.0, yb, ztop - 0.0175), (2 * hb, 0.24, 0.035), "M_Wood")
    for sx in (-1.0, 1.0):
        hc = boat_half_breadth_at(tb, ztop - 0.07)
        add_box(mb, (sx * (hc - 0.02), yb, ztop - 0.055), (0.07, 0.30, 0.04), "M_Wood", skip=("+z",))
        to = (-0.42 - BOAT["y0"]) / (BOAT["y1"] - BOAT["y0"])
        p, n = boat_section(to, 1.0)
        add_box(mb, (sx * (p.x + 0.004), -0.42, p.y + 0.035), (0.075, 0.14, 0.07), "M_Wood", skip=("-z",))

    # oars, lying on the thwart along the boat (handle aft, blade forward)
    for sx, yaw in ((1.0, -0.01), (-1.0, 0.015)):
        oar = MeshBuilder(["M_WoodLight"])
        ys = [0.0, 0.12, 0.15, 0.22, 0.9, 1.55, 1.66, 1.80, 1.98, 2.14, 2.24, 2.30]
        wx = [0.017, 0.017, 0.022, 0.026, 0.027, 0.023, 0.022, 0.040, 0.066, 0.072, 0.062, 0.040]
        wz = [0.017, 0.017, 0.022, 0.026, 0.027, 0.023, 0.020, 0.014, 0.011, 0.010, 0.009, 0.008]
        pts = [Vector((0.0, y, 0.0)) for y in ys]

        def shape(i, a, wx=wx, wz=wz):
            return 1.0 / math.sqrt((math.cos(a) / wx[i]) ** 2 + (math.sin(a) / wz[i]) ** 2)
        add_tube_path(oar, pts, [1.0] * len(pts), 8, "M_WoodLight", shape=shape)
        m = Matrix.Translation((sx * 0.15, -1.3, ztop + 0.027)) @ Matrix.Rotation(yaw, 4, "Z") \
            @ Matrix.Rotation(-0.012 * sx, 4, "Y")
        oar.transform(m)
        mb.append(oar)

    me = mb.build("SM_Rowboat")
    ob = new_object("SM_Rowboat", me, col, location=(22.0, 0.0, 0.0))
    # centre the hull length on the origin (x is symmetric, z = waterline already)
    ys_hull = [v.co.y for v in me.vertices]
    shift = -(min(ys_hull) + max(ys_hull)) * 0.5
    me.transform(Matrix.Translation((0.0, shift, 0.0)))
    me.update()
    smart_uv(ob, 66.0, 0.004)
    finish_shading(me, 40.0)
    log("SM_Rowboat: tris=%d" % sum(len(p.vertices) - 2 for p in me.polygons))
    return [("SM_Rowboat", [ob])]

# ============================================================================ driftwood
def build_driftwood(cfg, col):
    mb = MeshBuilder(["M_Driftwood"])
    rng = random.Random(901)
    L, N, S = 2.4, 26, 14
    o = Vector((3.3, 7.1, 1.9))

    def center(u):
        return Vector((lerp(-L / 2, L / 2, u), 0.11 * math.sin(math.pi * u) - 0.03 * u, 0.035 * math.sin(math.pi * u)))

    def radius(u):
        return lerp(0.195, 0.155, u) + 0.03 * math.exp(-u / 0.06)

    def shape(u, a):
        n1 = noise.noise(o + Vector((math.cos(a) * 1.2, math.sin(a) * 1.2, u * 7.0)))
        twist = 5.0 * a + 2.2 * u + 0.7 * noise.noise(o * 0.5 + Vector((u * 3.0, 0.0, 0.0)))
        groove = max(0.0, math.cos(twist)) ** 8
        return 1.0 + 0.12 * n1 - 0.10 * groove

    rings, frames = [], []
    for i in range(N + 1):
        u = i / N
        c = center(u)
        tan = (center(min(u + 0.01, 1.0)) - center(max(u - 0.01, 0.0))).normalized()
        n_, b_, t_ = frame_from_tangent(tan, Vector((0.0, 0.0, 1.0)))
        R = radius(u)
        ring = []
        for k in range(S):
            a = TAU * k / S
            p = c + (n_ * math.cos(a) + b_ * math.sin(a)) * (R * shape(u, a))
            if i == 0:                                  # thick end: rough, mostly flat break
                p -= t_ * (0.035 * rng.random())
            elif i == N:                                # thin end: splintered break
                p += t_ * (0.10 * rng.random() ** 1.5)
            ring.append(mb.v(p))
        rings.append(ring)
        frames.append((c, n_, b_, t_, R))
    for i in range(N):
        mb.ring_quads(rings[i], rings[i + 1], i / N, (i + 1) / N, "M_Driftwood")
    c0, _, _, t0, _ = frames[0]
    mb.cap(rings[0], "M_Driftwood", top=False, fan_center=mb.v(c0 - t0 * 0.012))
    c1, _, _, t1, _ = frames[-1]
    mb.cap(rings[-1], "M_Driftwood", top=True, fan_center=mb.v(c1 + t1 * 0.045))

    # broken branch stubs (pointing up / sideways)
    for (u_b, a_b, tilt, length, r0) in ((0.22, 1.05, 0.35, 0.16, 0.085),
                                         (0.58, -1.20, -0.30, 0.13, 0.075),
                                         (0.83, 0.55, 0.50, 0.20, 0.065)):
        i = int(round(u_b * N))
        c, n_, b_, t_, R = frames[i]
        d = (n_ * math.cos(a_b) + b_ * math.sin(a_b))
        dirv = (d * math.cos(tilt) + t_ * math.sin(tilt)).normalized()
        start = c + d * (R * 0.35)
        dists = [0.0, R * 0.75, R * 0.75 + length * 0.5, R * 0.75 + length]
        pts = [start + dirv * s for s in dists]
        srings = add_tube_path(mb, pts, [r0 * 1.3, r0 * 1.1, r0 * 0.85, r0 * 0.7], 7, "M_Driftwood",
                               cap_start=False, cap_end=False, v_range=(0.0, 0.25),
                               shape=lambda j, a: 1.0 + 0.08 * math.sin(3 * a + j))
        tip = srings[-1]
        for vi in tip:
            mb.co[vi] += dirv * (0.05 * rng.random())
        mb.cap(tip, "M_Driftwood", top=True, fan_center=mb.v(pts[-1] + dirv * 0.015))

    # lying on the ground: lowest point at z = 0, x/y centred on the bounding box
    xs = [c.x for c in mb.co]
    ys = [c.y for c in mb.co]
    zmin = min(c.z for c in mb.co)
    mb.transform(Matrix.Translation((-(min(xs) + max(xs)) * 0.5, -(min(ys) + max(ys)) * 0.5, -zmin)))
    me = mb.build("SM_Driftwood")
    ob = new_object("SM_Driftwood", me, col, location=(22.0, -6.0, 0.0))
    finish_shading(me, 60.0)
    log("SM_Driftwood: tris=%d" % sum(len(p.vertices) - 2 for p in me.polygons))
    return [("SM_Driftwood", [ob])]


# ============================================================================ campfire logs
def build_campfire(cfg, col):
    mb = MeshBuilder(["M_Wood", "M_Charcoal", "M_Ash"])
    rng = random.Random(1201)
    o = Vector((11.0, -4.0, 2.5))
    # ---- ash / ember mound (faces up)
    sides = 20
    radii = [0.10, 0.21, 0.31, 0.40, 0.47, 0.53]
    center = mb.v((0.0, 0.0, 0.078))
    rings = []
    for r in radii:
        ring = []
        for k in range(sides):
            a = TAU * k / sides
            rr = r * (1.0 + 0.10 * noise.noise(o + Vector((math.cos(a) * 1.3, math.sin(a) * 1.3, r * 2.0))))
            h = 0.078 * max(0.0, 1.0 - (r / 0.5) ** 2) ** 1.2 + 0.010 * noise.noise(o * 2.0 + Vector((a, r * 5, 0)))
            if r == radii[-1]:
                h = -0.012
            ring.append(mb.v((rr * math.cos(a), rr * math.sin(a), h)))
        rings.append(ring)
    for k in range(sides):
        k2 = (k + 1) % sides
        mb.f((center, rings[0][k], rings[0][k2]), [(0.5, 0.5), (0.5 + 0.1 * math.cos(TAU * k / sides),
              0.5 + 0.1 * math.sin(TAU * k / sides)), (0.5 + 0.1 * math.cos(TAU * k2 / sides),
              0.5 + 0.1 * math.sin(TAU * k2 / sides))], "M_Ash")
    for i in range(len(rings) - 1):
        mb.ring_quads(rings[i + 1], rings[i], (i + 1) / len(rings), i / len(rings), "M_Ash")

    def mound_h(x, y):
        r = math.hypot(x, y)
        return 0.078 * max(0.0, 1.0 - (r / 0.5) ** 2) ** 1.2

    # ---- a few charcoal chunks in the middle
    for k in range(5):
        a = rng.uniform(0.0, TAU)
        r = rng.uniform(0.04, 0.17)
        x, y = r * math.cos(a), r * math.sin(a)
        rot = (Matrix.Rotation(rng.uniform(0, TAU), 3, "Z") @ Matrix.Rotation(rng.uniform(-0.4, 0.4), 3, "X"))
        add_box(mb, (x, y, mound_h(x, y) + 0.012), (rng.uniform(0.06, 0.09), rng.uniform(0.04, 0.06), 0.035),
                "M_Charcoal", rot=rot, skip=("-z",))

    # ---- teepee of 5 logs: bottom ends on the ground, charred tips crossing above the centre
    n_logs = 5
    phase = rng.uniform(0.0, TAU)
    for k in range(n_logs):
        start_v = len(mb.co)
        phi = phase + TAU * k / n_logs + rng.uniform(-0.15, 0.15)
        r_log = rng.uniform(0.062, 0.078)
        length = rng.uniform(0.76, 0.84)
        R0 = rng.uniform(0.38, 0.43)
        out = Vector((math.cos(phi), math.sin(phi), 0.0))
        tang = Vector((-math.sin(phi), math.cos(phi), 0.0))
        bot = out * R0
        top_xy = -out * 0.06 + tang * rng.uniform(0.02, 0.06)
        horiz = (top_xy - bot).length
        h = math.sqrt(max(length ** 2 - horiz ** 2, 0.05))
        top = Vector((top_xy.x, top_xy.y, h))
        axis = (top - bot).normalized()
        bend = axis.cross(UP).normalized() * rng.uniform(-0.025, 0.025)
        segs = 6
        pts = [bot.lerp(top, j / segs) + bend * math.sin(math.pi * j / segs) for j in range(segs + 1)]
        rad = [r_log * (1.0 - 0.12 * j / segs) for j in range(segs + 1)]
        mats = ["M_Charcoal" if (j + 0.5) / segs > 0.62 else "M_Wood" for j in range(segs)]
        seed = Vector((k * 3.7, 1.0, 0.0))
        add_tube_path(mb, pts, rad, 8, "M_Wood", ref=UP, mats=mats,
                      shape=lambda j, a, seed=seed: 1.0 + 0.14 * noise.noise(seed + Vector((math.cos(a) * 1.4,
                                                                                            math.sin(a) * 1.4, j * 0.8))))
        zmin = min(c.z for c in mb.co[start_v:])
        for vi in range(start_v, len(mb.co)):
            mb.co[vi].z += -0.015 - zmin

    me = mb.build("SM_CampfireLogs")
    ob = new_object("SM_CampfireLogs", me, col, location=(26.0, -6.0, 0.0))
    finish_shading(me, 50.0)
    log("SM_CampfireLogs: tris=%d" % sum(len(p.vertices) - 2 for p in me.polygons))
    return [("SM_CampfireLogs", [ob])]
