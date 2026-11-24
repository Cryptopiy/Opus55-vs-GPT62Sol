"""Living Island props - shared core: paths, GameConfig.cs parsing, math helpers, materials, MeshBuilder, scene helpers.

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


PROJECT_ROOT = r"<PROJECT_ROOT>"
CONFIG_PATH = os.path.join(PROJECT_ROOT, "Assets", "Scripts", "Config", "GameConfig.cs")
MODELS_DIR = os.path.join(PROJECT_ROOT, "Assets", "Models")
EXPECTED_BLEND = os.path.join(PROJECT_ROOT, "LivingIslandOpus5.5.blend")

FAMILIES = ["Palms", "Rocks", "Lighthouse", "Rowboat", "Driftwood", "Campfire"]
REQUIRED_CONFIG = [
    "PalmVariants", "PalmTrisLod0Max", "PalmTrisLod1Max", "PalmFrondsMin", "PalmFrondsMax",
    "PalmTrunkSegmentsMin", "PalmModelHeight", "RockVariants", "RockTrisMax", "LighthouseTrisMax",
    "LighthouseModelHeight", "RowboatTrisMax", "DriftwoodTrisMax", "CampfireLogsTrisMax",
]

TAU = 2.0 * math.pi
UP = Vector((0.0, 0.0, 1.0))
REPORT = {}          # filled while building: per asset tris / bbox / materials (printed + used for notes)


def log(*args):
    print("[props]", *args)


# ============================================================================ config
def read_config(path=CONFIG_PATH):
    """Parse `public const int|float Name = value[f];` lines of GameConfig.cs."""
    rx = re.compile(r"^\s*public\s+const\s+(int|float)\s+(\w+)\s*=\s*"
                    r"([-+]?(?:\d+\.?\d*|\.\d+)(?:[eE][-+]?\d+)?)[fF]?\s*;")
    cfg = {}
    with open(path, encoding="utf-8") as fh:
        for line in fh:
            m = rx.match(line)
            if m:
                kind, name, value = m.groups()
                cfg[name] = int(value) if kind == "int" else float(value)
    missing = [n for n in REQUIRED_CONFIG if n not in cfg]
    if missing:
        raise RuntimeError("GameConfig.cs is missing: " + ", ".join(missing))
    return {n: cfg[n] for n in REQUIRED_CONFIG}


# ============================================================================ small math helpers
def lerp(a, b, t):
    return a + (b - a) * t


def clamp(x, lo=0.0, hi=1.0):
    return lo if x < lo else hi if x > hi else x


def smoothstep(e0, e1, x):
    t = clamp((x - e0) / (e1 - e0))
    return t * t * (3.0 - 2.0 * t)


def frame_from_tangent(t, ref=None):
    """Right-handed frame (N, B, T) with N x B = T. Ring points c + r(cos a N + sin a B) wind CCW around T."""
    t = Vector(t).normalized()
    if ref is None:
        ref = Vector((1.0, 0.0, 0.0)) if abs(t.x) < 0.9 else Vector((0.0, 1.0, 0.0))
    n = (ref - t * ref.dot(t)).normalized()
    b = t.cross(n)
    return n, b, t


def fbm(p, octaves=4, lac=2.0, gain=0.5, basis="PERLIN_ORIGINAL"):
    amp, freq, total, norm = 1.0, 1.0, 0.0, 0.0
    for _ in range(octaves):
        total += amp * noise.noise(p * freq, noise_basis=basis)
        norm += amp
        amp *= gain
        freq *= lac
    return total / norm


# ============================================================================ materials
MATERIALS = {
    "M_PalmTrunk": dict(base=(0.42, 0.33, 0.24), rough=0.9),
    "M_PalmFrond": dict(base=(0.24, 0.46, 0.13), rough=0.6, double=True),     # single cards: two-sided
    "M_Coconut":   dict(base=(0.30, 0.22, 0.10), rough=0.7),
    "M_Rock":      dict(base=(0.45, 0.43, 0.40), rough=0.85),
    "M_LH_White":  dict(base=(0.92, 0.92, 0.90), rough=0.6),
    "M_LH_Red":    dict(base=(0.70, 0.08, 0.06), rough=0.55),
    "M_LH_Stone":  dict(base=(0.55, 0.53, 0.50), rough=0.9),
    "M_LH_Dark":   dict(base=(0.10, 0.09, 0.08), rough=0.8),
    "M_LH_Window": dict(base=(0.15, 0.14, 0.10), rough=0.3),
    "M_LH_Metal":  dict(base=(0.12, 0.12, 0.13), rough=0.4, metal=0.6),
    "M_LH_Glass":  dict(base=(0.60, 0.75, 0.80), rough=0.05),
    "M_LH_Lamp":   dict(base=(1.00, 0.90, 0.60), rough=0.3, emit=(1.00, 0.85, 0.50), strength=5.0),
    "M_BoatPaint": dict(base=(0.16, 0.32, 0.50), rough=0.6),
    "M_Wood":      dict(base=(0.55, 0.38, 0.22), rough=0.8),
    "M_WoodLight": dict(base=(0.70, 0.55, 0.35), rough=0.75),
    "M_Driftwood": dict(base=(0.62, 0.57, 0.50), rough=0.9),
    "M_Charcoal":  dict(base=(0.05, 0.045, 0.04), rough=0.95),
    "M_Ash":       dict(base=(0.33, 0.31, 0.30), rough=1.0),
}


def _socket(node, *names):
    for n in names:
        s = node.inputs.get(n)
        if s is not None:
            return s
    return None


def get_material(name):
    """Create or reset a plain Principled BSDF material with the exact name."""
    spec = MATERIALS[name]
    mat = bpy.data.materials.get(name) or bpy.data.materials.new(name)
    try:
        mat.use_nodes = True          # deprecated (always on) in Blender 5, harmless
    except Exception:
        pass
    nt = mat.node_tree
    bsdf = next((n for n in nt.nodes if n.type == "BSDF_PRINCIPLED"), None)
    out = next((n for n in nt.nodes if n.type == "OUTPUT_MATERIAL"), None)
    if bsdf is None:
        bsdf = nt.nodes.new("ShaderNodeBsdfPrincipled")
    if out is None:
        out = nt.nodes.new("ShaderNodeOutputMaterial")
    if not out.inputs[0].is_linked:
        nt.links.new(bsdf.outputs[0], out.inputs[0])
    base = tuple(spec["base"]) + (1.0,)
    _socket(bsdf, "Base Color").default_value = base
    _socket(bsdf, "Roughness").default_value = spec["rough"]
    _socket(bsdf, "Metallic").default_value = spec.get("metal", 0.0)
    emit = _socket(bsdf, "Emission Color", "Emission")
    strength = _socket(bsdf, "Emission Strength")
    if "emit" in spec:
        emit.default_value = tuple(spec["emit"]) + (1.0,)
        strength.default_value = spec["strength"]
    else:
        if emit is not None:
            emit.default_value = (0.0, 0.0, 0.0, 1.0)
        if strength is not None:
            strength.default_value = 0.0
    mat.use_backface_culling = not spec.get("double", False)   # -> glTF doubleSided flag
    mat.diffuse_color = base                      # viewport colour only
    mat.roughness = spec["rough"]
    mat.metallic = spec.get("metal", 0.0)
    return mat


# ============================================================================ mesh builder
class MeshBuilder:
    """Collects verts / faces with per-corner UVs, per-face material slots and per-vertex colours."""

    def __init__(self, materials):
        self.materials = list(materials)          # slot order
        self.co, self.col = [], []
        self.faces, self.uvs, self.mats = [], [], []

    def mat_index(self, name):
        return self.materials.index(name)

    def v(self, co, col=(0.0, 0.0, 0.0, 1.0)):
        self.co.append(Vector(co))
        self.col.append(tuple(col))
        return len(self.co) - 1

    def f(self, idx, uvs=None, mat=None):
        idx = tuple(idx)
        if uvs is None:
            uvs = [(0.0, 0.0)] * len(idx)
        self.faces.append(idx)
        self.uvs.append([tuple(u) for u in uvs])
        self.mats.append(0 if mat is None else (self.mat_index(mat) if isinstance(mat, str) else mat))
        return len(self.faces) - 1

    # --- tubes: rings are lists of vertex indices, wound CCW around the tube direction
    def ring_quads(self, r0, r1, v0, v1, mat, u0=0.0, u1=1.0, closed=True):
        n = len(r0)
        count = n if closed else n - 1
        for i in range(count):
            j = (i + 1) % n
            ua = u0 + (u1 - u0) * i / (n if closed else n - 1)
            ub = u0 + (u1 - u0) * (i + 1) / (n if closed else n - 1)
            self.f((r0[i], r0[j], r1[j], r1[i]), [(ua, v0), (ub, v0), (ub, v1), (ua, v1)], mat)

    def cap(self, ring, mat, top=True, uv_center=(0.5, 0.5), uv_radius=0.5, fan_center=None):
        """Close a ring. top=True -> normal along the tube direction (ring order), else reversed."""
        n = len(ring)
        order = list(ring) if top else list(reversed(ring))
        uvs = []
        for k in range(n):
            a = TAU * (k if top else (n - k) % n) / n
            uvs.append((uv_center[0] + uv_radius * math.cos(a), uv_center[1] + uv_radius * math.sin(a)))
        if fan_center is None:
            self.f(order, uvs, mat)
        else:
            for k in range(n):
                k2 = (k + 1) % n
                self.f((order[k], order[k2], fan_center), [uvs[k], uvs[k2], uv_center], mat)

    def append(self, other):
        """Merge another builder (material names are re-mapped)."""
        off = len(self.co)
        self.co += other.co
        self.col += other.col
        for face, uv, m in zip(other.faces, other.uvs, other.mats):
            name = other.materials[m]
            if name not in self.materials:
                self.materials.append(name)
            self.faces.append(tuple(i + off for i in face))
            self.uvs.append(uv)
            self.mats.append(self.materials.index(name))

    def transform(self, mat4, start=0):
        for i in range(start, len(self.co)):
            self.co[i] = mat4 @ self.co[i]

    def build(self, name, colors=False):
        me = bpy.data.meshes.get(name)
        if me is not None and me.users == 0:
            bpy.data.meshes.remove(me)
        me = bpy.data.meshes.new(name)
        me.from_pydata([tuple(c) for c in self.co], [], list(self.faces))
        uv = me.uv_layers.new(name="UVMap")
        flat = [c for face_uv in self.uvs for uvc in face_uv for c in uvc]
        uv.data.foreach_set("uv", flat)
        for mname in self.materials:
            me.materials.append(get_material(mname))
        me.polygons.foreach_set("material_index", self.mats)
        if colors:
            attr = me.color_attributes.new("Col", "FLOAT_COLOR", "POINT")
            attr.data.foreach_set("color", [c for col in self.col for c in col])
            me.color_attributes.active_color = attr
            me.color_attributes.render_color_index = me.color_attributes.find("Col")
        me.update()
        return me


# ============================================================================ scene helpers
def get_collection(name):
    scene_col = bpy.context.scene.collection
    col = bpy.data.collections.get(name)
    if col is None:
        col = bpy.data.collections.new(name)
    if col.name not in scene_col.children:
        scene_col.children.link(col)
    return col


def clear_family(name):
    col = bpy.data.collections.get(name)
    if col is None:
        return
    for ob in list(col.all_objects):
        data = ob.data
        bpy.data.objects.remove(ob, do_unlink=True)
        if data is not None and data.users == 0 and isinstance(data, bpy.types.Mesh):
            bpy.data.meshes.remove(data)


def new_object(name, mesh, col, location=(0.0, 0.0, 0.0), parent=None):
    old = bpy.data.objects.get(name)
    if old is not None:
        bpy.data.objects.remove(old, do_unlink=True)
    ob = bpy.data.objects.new(name, mesh)
    col.objects.link(ob)
    if parent is not None:
        ob.parent = parent
        ob.matrix_parent_inverse = Matrix.Identity(4)
    ob.location = location
    return ob


def select_only(objs):
    for o in bpy.context.view_layer.objects:
        o.select_set(False)
    for o in objs:
        o.select_set(True)
    bpy.context.view_layer.objects.active = objs[0]


def smart_uv(ob, angle_deg=66.0, margin=0.01):
    select_only([ob])
    bpy.ops.object.mode_set(mode="EDIT")
    bpy.ops.mesh.select_all(action="SELECT")
    bpy.ops.uv.smart_project(angle_limit=math.radians(angle_deg), island_margin=margin)
    bpy.ops.object.mode_set(mode="OBJECT")


def finish_shading(me, sharp_angle_deg=None):
    """All faces smooth; edges sharper than the angle get split normals (exported as corner normals)."""
    me.shade_smooth()
    if sharp_angle_deg is not None:
        me.set_sharp_from_angle(angle=math.radians(sharp_angle_deg))
    me.update()


def mesh_report(ob):
    """Triangles of the evaluated mesh + topology health + world bbox (object space + location)."""
    dg = bpy.context.evaluated_depsgraph_get()
    eo = ob.evaluated_get(dg)
    me = eo.to_mesh()
    me.calc_loop_triangles()
    tris = len(me.loop_triangles)
    bm = bmesh.new()
    bm.from_mesh(me)
    loose = sum(1 for v in bm.verts if not v.link_faces)
    zero = sum(1 for f in bm.faces if f.calc_area() < 1e-8)
    nonmanifold = sum(1 for e in bm.edges if len(e.link_faces) > 2 or len(e.link_faces) == 0)
    boundary = sum(1 for e in bm.edges if e.is_boundary)
    # closed islands must have a positive signed volume (outward normals)
    bm.faces.ensure_lookup_table()
    seen, bad_islands, closed_islands = set(), 0, 0
    for f in bm.faces:
        if f.index in seen:
            continue
        stack, island = [f], []
        seen.add(f.index)
        while stack:
            g = stack.pop()
            island.append(g)
            for e in g.edges:
                for h in e.link_faces:
                    if h.index not in seen:
                        seen.add(h.index)
                        stack.append(h)
        closed = all(not e.is_boundary for g in island for e in g.edges)
        if closed:
            closed_islands += 1
            vol = 0.0
            for g in island:
                vs = [v.co for v in g.verts]
                for k in range(1, len(vs) - 1):
                    vol += vs[0].dot(vs[k].cross(vs[k + 1])) / 6.0
            if vol <= 0.0:
                bad_islands += 1
    xs = [v.co.x for v in bm.verts]
    ys = [v.co.y for v in bm.verts]
    zs = [v.co.z for v in bm.verts]
    bm.free()
    eo.to_mesh_clear()
    info = dict(tris=tris, loose_verts=loose, zero_area_faces=zero, nonmanifold_edges=nonmanifold,
                boundary_edges=boundary, closed_islands=closed_islands, inverted_closed_islands=bad_islands,
                bbox_min=(round(min(xs), 3), round(min(ys), 3), round(min(zs), 3)),
                bbox_max=(round(max(xs), 3), round(max(ys), 3), round(max(zs), 3)),
                size=(round(max(xs) - min(xs), 3), round(max(ys) - min(ys), 3), round(max(zs) - min(zs), 3)),
                materials=[m.name for m in ob.data.materials])
    return info
