"""Living Island props - glTF export, GLB inspection and budget checks.

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


# ============================================================================ export + verification
GLTF_OPTIONS = dict(
    export_format="GLB",
    use_selection=True,                    # only the asset's objects
    export_yup=True,                       # +Y up
    export_apply=True,                     # apply modifiers
    export_normals=True,
    export_texcoords=True,
    export_tangents=False,
    export_materials="EXPORT",
    export_image_format="NONE",            # no images / textures
    export_vertex_color="ACTIVE",          # palms: active/render colour attribute "Col" -> COLOR_0
    export_all_vertex_colors=False,
    export_active_vertex_color_when_no_material=False,
    export_attributes=False,
    use_mesh_edges=False,
    use_mesh_vertices=False,
    export_cameras=False,
    export_lights=False,
    export_animations=False,
    export_skins=False,
    export_morph=False,
    export_extras=False,
    export_draco_mesh_compression_enable=False,
    export_meshopt_compression_enable=False,
    export_gpu_instances=False,
    export_hierarchy_flatten_objs=False,
    check_existing=False,
)


def export_glb(name, objs):
    valid = {p.identifier for p in bpy.ops.export_scene.gltf.get_rna_type().properties}
    opts = {k: v for k, v in GLTF_OPTIONS.items() if k in valid}
    dropped = sorted(set(GLTF_OPTIONS) - set(opts))
    if dropped:
        log("glTF options not available in this Blender, skipped:", dropped)
    roots = [o for o in objs if o.parent is None]
    saved = {o.name: o.location.copy() for o in roots}
    for o in roots:
        o.location = (0.0, 0.0, 0.0)                 # file nodes sit at the origin; layout restored below
    bpy.context.view_layer.update()
    select_only(objs)
    path = os.path.join(MODELS_DIR, name + ".glb")
    try:
        bpy.ops.export_scene.gltf(filepath=path, **opts)
    finally:
        for o in roots:
            o.location = saved[o.name]
        bpy.context.view_layer.update()
    return path, opts


def inspect_glb(path):
    """Parse the GLB header + JSON chunk and summarise nodes / primitives / materials / accessors."""
    with open(path, "rb") as fh:
        data = fh.read()
    magic, version, total = struct.unpack_from("<4sII", data, 0)
    clen, ctype = struct.unpack_from("<I4s", data, 12)
    assert magic == b"glTF" and ctype == b"JSON", (magic, ctype)
    js = json.loads(data[20:20 + clen].decode("utf-8"))
    acc = js.get("accessors", [])
    mats = [m.get("name") for m in js.get("materials", [])]
    out = dict(file=os.path.basename(path), bytes=len(data), version=version,
               materials=mats, nodes=[], meshes=[], accessor_count=len(acc))
    for n in js.get("nodes", []):
        out["nodes"].append(dict(name=n.get("name"), mesh=n.get("mesh"), children=n.get("children", []),
                                 translation=n.get("translation"), rotation=n.get("rotation"), scale=n.get("scale")))
    for m in js.get("meshes", []):
        prims = []
        for p in m.get("primitives", []):
            attrs = {}
            for k, ai in p["attributes"].items():
                a = acc[ai]
                attrs[k] = dict(count=a["count"], type=a["type"], componentType=a["componentType"],
                                normalized=a.get("normalized", False))
            tris = acc[p["indices"]]["count"] // 3 if "indices" in p else None
            prims.append(dict(material=mats[p["material"]] if "material" in p else None, tris=tris,
                              attributes=attrs))
        out["meshes"].append(dict(name=m.get("name"), primitives=prims))
    return out


def print_glb(info):
    log("GLB %s (%d bytes) materials=%s accessors=%d" % (info["file"], info["bytes"], info["materials"],
                                                         info["accessor_count"]))
    for n in info["nodes"]:
        log("   node %-22s mesh=%s children=%s t=%s r=%s s=%s" % (n["name"], n["mesh"], n["children"],
                                                               n["translation"], n["rotation"], n["scale"]))
    for m in info["meshes"]:
        for p in m["primitives"]:
            attrs = ", ".join("%s[%d %s ct%d%s]" % (k, v["count"], v["type"], v["componentType"],
                                                     " norm" if v["normalized"] else "")
                              for k, v in p["attributes"].items())
            log("   mesh %-20s mat=%-12s tris=%s  %s" % (m["name"], p["material"], p["tris"], attrs))


def check_budgets(cfg, name, reports):
    problems = []
    tris = {k: v["tris"] for k, v in reports.items()}

    def over(value, limit, what):
        if value > limit:
            problems.append("%s: %s %d > %d" % (name, what, value, limit))
    if name.startswith("SM_Palm"):
        over(tris[name + "_LOD0"], cfg["PalmTrisLod0Max"], "LOD0 tris")
        over(tris[name + "_LOD1"], cfg["PalmTrisLod1Max"], "LOD1 tris")
        h = reports[name + "_LOD0"]["size"][2]
        if abs(h - cfg["PalmModelHeight"]) > 0.05:
            problems.append("%s: height %.3f != %.3f" % (name, h, cfg["PalmModelHeight"]))
    elif name.startswith("SM_Rock"):
        over(tris[name], cfg["RockTrisMax"], "tris")
    elif name == "SM_Lighthouse":
        over(sum(tris.values()), cfg["LighthouseTrisMax"], "tris")
    elif name == "SM_Rowboat":
        over(tris[name], cfg["RowboatTrisMax"], "tris")
    elif name == "SM_Driftwood":
        over(tris[name], cfg["DriftwoodTrisMax"], "tris")
    elif name == "SM_CampfireLogs":
        over(tris[name], cfg["CampfireLogsTrisMax"], "tris")
    for oname, r in reports.items():
        if r["loose_verts"] or r["zero_area_faces"] or r["nonmanifold_edges"] or r["inverted_closed_islands"]:
            problems.append("%s: topology %s" % (oname, {k: r[k] for k in ("loose_verts", "zero_area_faces",
                                                                         "nonmanifold_edges",
                                                                         "inverted_closed_islands")}))
    return problems


def palm_color_check(ob):
    me = ob.data
    attr = me.color_attributes.get("Col")
    if attr is None:
        return "missing Col"
    vals = [tuple(d.color) for d in attr.data]
    mn = [round(min(v[i] for v in vals), 3) for i in range(4)]
    mx = [round(max(v[i] for v in vals), 3) for i in range(4)]
    return "Col %s/%s active=%s render=%s min=%s max=%s" % (attr.data_type, attr.domain,
                                                          me.color_attributes.active_color_name,
                                                          me.color_attributes.default_color_name, mn, mx)
