"""Living Island props - generic solids (revolve, box, tube along a path).

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


# ============================================================================ generic solids
def revolve(mb, prof, sides, mats, phase=0.0, closed_profile=False, center=(0.0, 0.0)):
    """Surface of revolution around Z. prof = [(r, z), ...] traversed counter-clockwise in the (r, z)
    half-plane (bottom outwards, side upwards, top inwards) -> outward normals. r == 0 -> single pole vertex.
    mats: one material name or a list with one name per profile segment."""
    nseg = len(prof) if closed_profile else len(prof) - 1
    if isinstance(mats, str):
        mats = [mats] * nseg
    rings = []
    for (r, z) in prof:
        if r < 1e-7:
            rings.append(mb.v((center[0], center[1], z)))
        else:
            rings.append([mb.v((center[0] + r * math.cos(phase + TAU * k / sides),
                                center[1] + r * math.sin(phase + TAU * k / sides), z)) for k in range(sides)])
    total = max(len(prof) - 1, 1)
    for i in range(nseg):
        a, b = rings[i], rings[(i + 1) % len(rings)]
        va, vb = i / total, (i + 1) / total
        if isinstance(a, list) and isinstance(b, list):
            mb.ring_quads(a, b, va, vb, mats[i])
        elif isinstance(a, list):                       # converge to a pole (top)
            for k in range(sides):
                k2 = (k + 1) % sides
                mb.f((a[k], a[k2], b), [(k / sides, va), ((k + 1) / sides, va), ((k + 0.5) / sides, vb)], mats[i])
        elif isinstance(b, list):                       # start at a pole (bottom)
            for k in range(sides):
                k2 = (k + 1) % sides
                mb.f((a, b[k2], b[k]), [((k + 0.5) / sides, va), ((k + 1) / sides, vb), (k / sides, vb)], mats[i])
    return rings


def add_box(mb, center, size, mat, rot=None, skip=()):
    """Axis box (optionally rotated by a 3x3 matrix) with outward normals. skip: e.g. ('-z', '+z')."""
    c = Vector(center)
    hs = Vector(size) * 0.5
    rot = rot or Matrix.Identity(3)
    corner = {}

    def P(s):
        if s not in corner:
            corner[s] = mb.v(c + rot @ Vector((s[0] * hs.x, s[1] * hs.y, s[2] * hs.z)))
        return corner[s]
    for i, axis in enumerate("xyz"):
        j, k = (i + 1) % 3, (i + 2) % 3
        for sgn in (1, -1):
            if ("+" if sgn > 0 else "-") + axis in skip:
                continue
            quad = []
            for (a, b) in ((-1, -1), (1, -1), (1, 1), (-1, 1)):
                s = [0, 0, 0]
                s[i], s[j], s[k] = sgn, a, b
                quad.append(P(tuple(s)))
            if sgn < 0:
                quad.reverse()
            mb.f(quad, [(0, 0), (1, 0), (1, 1), (0, 1)], mat)


def add_tube_path(mb, pts, radii, sides, mat, cap_start=True, cap_end=True, ref=None, v_range=(0.0, 1.0),
                  shape=None, col=(0.0, 0.0, 0.0, 1.0), mats=None):
    """Tube along a polyline; radii per point; shape(i, a) -> radius multiplier. UV u around, v along."""
    rings = []
    n = len(pts)
    for i, p in enumerate(pts):
        t = (pts[min(i + 1, n - 1)] - pts[max(i - 1, 0)]).normalized()
        nn, bb, tt = frame_from_tangent(t, ref)
        ring = []
        for k in range(sides):
            a = TAU * k / sides
            r = radii[i] * (shape(i, a) if shape else 1.0)
            ring.append(mb.v(p + (nn * math.cos(a) + bb * math.sin(a)) * r, col))
        rings.append(ring)
    for i in range(n - 1):
        va = lerp(v_range[0], v_range[1], i / (n - 1))
        vb = lerp(v_range[0], v_range[1], (i + 1) / (n - 1))
        mb.ring_quads(rings[i], rings[i + 1], va, vb, mats[i] if mats else mat)
    if cap_start:
        mb.cap(rings[0], mats[0] if mats else mat, top=False)
    if cap_end:
        mb.cap(rings[-1], mats[-1] if mats else mat, top=True)
    return rings
