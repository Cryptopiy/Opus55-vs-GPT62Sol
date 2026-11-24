"""Living Island props - lighthouse with separate rotating lamp.

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


# ============================================================================ lighthouse
LH_MATS = ["M_LH_White", "M_LH_Red", "M_LH_Stone", "M_LH_Dark", "M_LH_Window", "M_LH_Metal", "M_LH_Glass"]
LH = dict(
    cols=64, plinth_r=3.35, plinth_h=0.75, tower_r0=2.62, tower_r1=1.72, body_top=13.0,
    bands=[0.75, 3.35, 5.3, 7.25, 9.2, 11.15, 13.0],                  # W R W R W R
    door=dict(az=-90.0, cols=4, z0=0.95, z1=2.95, frame=0.09, depth=0.22),
    windows=[(-25.0, 4.30), (65.0, 6.25), (155.0, 8.20), (245.0, 10.15)],   # (azimuth deg, centre z)
    win_cols=3, win_h=0.85, win_frame=0.05, win_depth=0.13,
    deck_z0=13.45, deck_z1=13.67, deck_r=2.5, rail_r=2.4, rail_h=1.0, posts=24,
    lantern_sides=12, lantern_base_r=1.32, glass_r=1.2, glass_z0=14.15, glass_z1=16.15,
)


def lh_radius(z):
    t = clamp((z - LH["plinth_h"]) / (LH["body_top"] - LH["plinth_h"]))
    return LH["tower_r1"] + (LH["tower_r0"] - LH["tower_r1"]) * (1.0 - t) ** 1.1


def build_lighthouse(cfg, col):
    mb = MeshBuilder(LH_MATS)
    cols = LH["cols"]
    dang = TAU / cols
    # ---- plinth (no bottom face: it sits on the ground) + steps to the door
    pr, ph = LH["plinth_r"], LH["plinth_h"]
    revolve(mb, [(pr, 0.0), (pr - 0.08, ph - 0.13), (pr - 0.17, ph), (LH["tower_r0"] - 0.12, ph)], cols,
            "M_LH_Stone")
    door_dir = Vector((math.cos(math.radians(LH["door"]["az"])), math.sin(math.radians(LH["door"]["az"])), 0.0))
    rot_door = Matrix.Rotation(math.radians(LH["door"]["az"]) - math.pi / 2, 3, "Z")
    for k, (hgt, out) in enumerate(((0.25, 0.70), (0.50, 0.38))):
        depth = out + 0.30
        cpos = door_dir * (pr - 0.30 + depth * 0.5) + Vector((0.0, 0.0, hgt * 0.5))
        add_box(mb, cpos, (1.5 - 0.1 * k, depth, hgt), "M_LH_Stone", rot=rot_door, skip=("-z",))

    # ---- tower body + cornice as one grid of rings
    levels = set(LH["bands"])
    d = LH["door"]
    levels.update([d["z0"], d["z1"]])
    for _, zc in LH["windows"]:
        levels.update([zc - LH["win_h"] * 0.5, zc + LH["win_h"] * 0.5])
    levels = sorted(levels)
    fine = []
    for a, b in zip(levels[:-1], levels[1:]):
        n = max(1, int(math.ceil((b - a) / 1.0)))
        fine += [a + (b - a) * i / n for i in range(n)]
    fine.append(levels[-1])
    prof = [(lh_radius(z), z) for z in fine]
    cornice = [(1.78, 13.16), (1.92, 13.30), (2.06, 13.45)]
    prof += cornice
    rings = []
    for (r, z) in prof:
        rings.append([mb.v((r * math.cos(k * dang), r * math.sin(k * dang), z)) for k in range(cols)])

    def band_mat(zm):
        if zm >= LH["bands"][-1]:
            return "M_LH_White"
        for i in range(len(LH["bands"]) - 1):
            if LH["bands"][i] <= zm < LH["bands"][i + 1]:
                return "M_LH_White" if i % 2 == 0 else "M_LH_Red"
        return "M_LH_White"

    def col_range(az_deg, ncols):
        c0 = int(round(math.radians(az_deg) % TAU / dang - ncols * 0.5)) % cols
        return [(c0 + i) % cols for i in range(ncols)]

    openings = []
    door_cols = col_range(d["az"], d["cols"])
    win_specs = [(col_range(az, LH["win_cols"]), zc - LH["win_h"] * 0.5, zc + LH["win_h"] * 0.5)
                 for az, zc in LH["windows"]]
    door_faces, win_faces = [], [[] for _ in win_specs]
    for i in range(len(prof) - 1):
        z0, z1 = prof[i][1], prof[i + 1][1]
        zm = 0.5 * (z0 + z1)
        mat = band_mat(zm)
        for k in range(cols):
            k2 = (k + 1) % cols
            fi = mb.f((rings[i][k], rings[i][k2], rings[i + 1][k2], rings[i + 1][k]),
                      [(k / cols, z0 / 14.0), ((k + 1) / cols, z0 / 14.0), ((k + 1) / cols, z1 / 14.0),
                       (k / cols, z1 / 14.0)], mat)
            if k in door_cols and d["z0"] <= zm <= d["z1"]:
                door_faces.append(fi)
            for w, (wc, wz0, wz1) in enumerate(win_specs):
                if k in wc and wz0 <= zm <= wz1:
                    win_faces[w].append(fi)
    openings.append(dict(faces=door_faces, frame=d["frame"], depth=d["depth"], frame_mat="M_LH_Stone",
                         reveal_mat="M_LH_Stone", inner_mat="M_LH_Dark"))
    for wf in win_faces:
        openings.append(dict(faces=wf, frame=LH["win_frame"], depth=LH["win_depth"], frame_mat="M_LH_White",
                             reveal_mat="M_LH_White", inner_mat="M_LH_Window"))

    # ---- gallery deck, posts, rails (metal)
    z0, z1, dr = LH["deck_z0"], LH["deck_z1"], LH["deck_r"]
    revolve(mb, [(2.0, z0), (dr, z0), (dr, z1), (1.10, z1)], cols, "M_LH_Metal")
    rr, rh = LH["rail_r"], LH["rail_h"]
    for k in range(LH["posts"]):
        a = TAU * (k + 0.5) / LH["posts"]
        add_box(mb, (rr * math.cos(a), rr * math.sin(a), z1 + rh * 0.5 - 0.02), (0.05, 0.05, rh - 0.02),
                "M_LH_Metal", rot=Matrix.Rotation(a, 3, "Z"), skip=("-z", "+z"))
    for (zc, w, h) in ((z1 + rh, 0.075, 0.06), (z1 + rh * 0.5, 0.04, 0.04)):
        revolve(mb, [(rr - w / 2, zc - h / 2), (rr + w / 2, zc - h / 2), (rr + w / 2, zc + h / 2),
                     (rr - w / 2, zc + h / 2)], cols, "M_LH_Metal", closed_profile=True)

    # ---- lantern: base wall, glass, mullions, bars, floor, ceiling, roof, finial, lamp pedestal
    ls = LH["lantern_sides"]
    ph_l = math.pi / ls
    g0, g1, gr = LH["glass_z0"], LH["glass_z1"], LH["glass_r"]
    revolve(mb, [(LH["lantern_base_r"], z1), (LH["lantern_base_r"], g0), (gr - 0.03, g0)], ls, "M_LH_White",
            phase=ph_l)
    revolve(mb, [(gr, g0), (gr, g1)], ls, "M_LH_Glass", phase=ph_l)
    revolve(mb, [(gr - 0.03, g0 + 0.002), (0.0, g0 + 0.002)], ls, "M_LH_Metal", phase=ph_l)   # floor (up)
    revolve(mb, [(0.0, g1 - 0.002), (gr - 0.03, g1 - 0.002)], ls, "M_LH_Metal", phase=ph_l)   # ceiling (down)
    for k in range(ls):
        a = ph_l + TAU * k / ls
        add_box(mb, (gr * math.cos(a), gr * math.sin(a), 0.5 * (g0 + g1)), (0.07, 0.07, g1 - g0), "M_LH_Metal",
                rot=Matrix.Rotation(a, 3, "Z"), skip=("-z", "+z"))
    for zc in (g0 + 0.04, g1 - 0.04, 0.5 * (g0 + g1)):
        w, h = 0.08, 0.06 if zc != 0.5 * (g0 + g1) else 0.035
        revolve(mb, [(gr - w / 2, zc - h / 2), (gr + w / 2, zc - h / 2), (gr + w / 2, zc + h / 2),
                     (gr - w / 2, zc + h / 2)], ls, "M_LH_Metal", phase=ph_l, closed_profile=True)
    roof = [(gr - 0.02, g1), (1.47, g1), (1.47, g1 + 0.08), (1.28, g1 + 0.30), (1.0, g1 + 0.62),
            (0.66, g1 + 0.93), (0.33, g1 + 1.14), (0.12, g1 + 1.23), (0.0, g1 + 1.25)]
    revolve(mb, roof, 24, "M_LH_Red")
    stem_z0 = g1 + 1.21
    revolve(mb, [(0.065, stem_z0), (0.065, stem_z0 + 0.32), (0.0, stem_z0 + 0.32)], 8, "M_LH_Metal")
    ball_r = 0.19
    top = 18.0
    bc = top - ball_r
    ball = [(0.0, bc - ball_r)] + [(ball_r * math.sin(math.pi * i / 7), bc - ball_r * math.cos(math.pi * i / 7))
                                    for i in range(1, 7)] + [(0.0, top)]
    revolve(mb, ball, 12, "M_LH_Metal")
    lamp_z = 0.5 * (g0 + g1)
    revolve(mb, [(0.15, g0), (0.12, g0 + 0.15), (0.09, lamp_z - 0.34), (0.0, lamp_z - 0.34)], 8, "M_LH_Metal")

    # ---- mesh + openings (inset frame, then recess)
    me = mb.build("SM_Lighthouse")
    bm = bmesh.new()
    bm.from_mesh(me)
    bm.faces.ensure_lookup_table()
    groups = [([bm.faces[i] for i in o["faces"]], o) for o in openings]
    for faces, o in groups:
        r1 = bmesh.ops.inset_region(bm, faces=faces, thickness=o["frame"], depth=0.0, use_even_offset=True)
        r2 = bmesh.ops.inset_region(bm, faces=faces, thickness=0.0, depth=-o["depth"], use_even_offset=True)
        for f in r1["faces"]:
            f.material_index = LH_MATS.index(o["frame_mat"])
        for f in r2["faces"]:
            f.material_index = LH_MATS.index(o["reveal_mat"])
        for f in faces:
            f.material_index = LH_MATS.index(o["inner_mat"])
    bm.to_mesh(me)
    bm.free()
    s = cfg["LighthouseModelHeight"] / top
    me.transform(Matrix.Scale(s, 4))
    me.update()
    ob = new_object("SM_Lighthouse", me, col, location=(-12.0, 4.0, 0.0))
    smart_uv(ob, 66.0, 0.004)
    finish_shading(me, 35.0)

    # ---- rotating lamp (separate child object, origin = lamp centre, lens barrel along local +Y)
    lm = MeshBuilder(["M_LH_Lamp"])
    revolve(lm, [(0.0, -0.30), (0.26, -0.30), (0.28, -0.22), (0.28, 0.22), (0.26, 0.30), (0.0, 0.30)], 16,
            "M_LH_Lamp")
    revolve(lm, [(0.0, 0.27), (0.10, 0.27), (0.10, 0.40), (0.0, 0.40)], 8, "M_LH_Lamp")
    barrel = MeshBuilder(["M_LH_Lamp"])
    revolve(barrel, [(0.0, 0.0), (0.19, 0.0), (0.19, 0.50), (0.22, 0.54), (0.22, 0.62), (0.16, 0.66),
                     (0.0, 0.69)], 16, "M_LH_Lamp")
    barrel.transform(Matrix.Rotation(-math.pi / 2, 4, "X"))          # +Z -> +Y
    lm.append(barrel)
    lm.transform(Matrix.Scale(s, 4))
    lme = lm.build("SM_Lighthouse_Lamp")
    lamp = new_object("SM_Lighthouse_Lamp", lme, col, location=(0.0, 0.0, lamp_z * s), parent=ob)
    smart_uv(lamp, 66.0, 0.01)
    finish_shading(lme, 35.0)
    REPORT["lamp_height"] = lamp_z * s
    log("SM_Lighthouse: tris=%d, lamp centre z=%.3f" % (sum(len(p.vertices) - 2 for p in me.polygons), lamp_z * s))
    return [("SM_Lighthouse", [ob, lamp])]
