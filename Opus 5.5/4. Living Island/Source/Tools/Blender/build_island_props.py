"""
build_island_props.py  -  Living Island prop builder (Blender 5.2, bpy/bmesh only, no images/textures)

Builds every prop of the game procedurally, organises the .blend into one collection per asset
family and exports one binary glTF per asset into Assets/Models.

The code is split by responsibility into props_*.py next to this file; this script loads
(and reloads) them, so edits are picked up when it is run again in the same Blender session.

Re-runnable: everything this script made before (collections Palms/Rocks/Lighthouse/Rowboat/Driftwood/
Campfire, their meshes and the M_* materials) is rebuilt from scratch on every run.

Run inside the open Blender (e.g. through the Blender MCP):
    path = r"<project root>\\Tools\\Blender\\build_island_props.py"
    exec(compile(open(path, encoding="utf-8").read(), path, "exec"), {"__name__": "__main__"})
Running it with any other __name__ only defines the functions (handy for building one family at a time).

Numeric budgets are read from Assets/Scripts/Config/GameConfig.cs ("Blender props" block).
Conventions: metric, Z-up, 1 unit = 1 m, transforms applied, glTF exported +Y up.
"""
import importlib
import os
import sys

import bpy

PROPS_DIR = "<PROJECT_ROOT>"
if PROPS_DIR not in sys.path:
    sys.path.insert(0, PROPS_DIR)

import props_core, props_geometry, props_palms, props_rocks, props_lighthouse, props_boat_wood, props_export  # noqa: E401,E402
for _m in (props_core, props_geometry, props_palms, props_rocks, props_lighthouse, props_boat_wood, props_export):
    importlib.reload(_m)   # dependency order: core first, so star-imports below see fresh code

from props_core import *         # noqa: F401,F403,E402
from props_geometry import *     # noqa: F401,F403,E402
from props_palms import *        # noqa: F401,F403,E402
from props_rocks import *        # noqa: F401,F403,E402
from props_lighthouse import *   # noqa: F401,F403,E402
from props_boat_wood import *    # noqa: F401,F403,E402
from props_export import *       # noqa: F401,F403,E402


# ============================================================================ main
BUILDERS = [("Palms", "build_palms"), ("Rocks", "build_rocks"), ("Lighthouse", "build_lighthouse"),
            ("Rowboat", "build_rowboat"), ("Driftwood", "build_driftwood"), ("Campfire", "build_campfire")]


def main(families=None, export=True, save=True):
    here = os.path.normcase(os.path.abspath(bpy.data.filepath))
    if here != os.path.normcase(os.path.abspath(EXPECTED_BLEND)):
        raise RuntimeError("Refusing to run: open file is %r, expected %r" % (bpy.data.filepath, EXPECTED_BLEND))
    cfg = read_config()
    log("config:", cfg)
    if bpy.context.view_layer.objects.active is not None and bpy.context.mode != "OBJECT":
        bpy.ops.object.mode_set(mode="OBJECT")
    for helper in ("Cube", "__focus"):
        ob = bpy.data.objects.get(helper)
        if ob is not None:
            data = ob.data
            bpy.data.objects.remove(ob, do_unlink=True)
            if data is not None and data.users == 0:
                bpy.data.meshes.remove(data)
    os.makedirs(MODELS_DIR, exist_ok=True)
    g = globals()
    summary, problems = {}, []
    for fam, fn_name in BUILDERS:
        if families and fam not in families:
            continue
        clear_family(fam)
        col = get_collection(fam)
        for name, objs in g[fn_name](cfg, col):
            reports = {o.name: mesh_report(o) for o in objs}
            for oname, r in reports.items():
                log("  %-22s tris=%-5d size=%s min=%s max=%s mats=%s" % (oname, r["tris"], r["size"], r["bbox_min"],
                                                                     r["bbox_max"], r["materials"]))
                if oname.startswith("SM_Palm"):
                    log("     ", palm_color_check(bpy.data.objects[oname]))
            problems += check_budgets(cfg, name, reports)
            entry = dict(objects=reports)
            if export:
                path, opts = export_glb(name, objs)
                info = inspect_glb(path)
                print_glb(info)
                entry["glb"] = info
                entry["export_options"] = opts
            summary[name] = entry
        if save:
            bpy.ops.wm.save_mainfile()
            log("saved after", fam)
    # drop the unused default material of the start-up file
    mat = bpy.data.materials.get("Material")
    if mat is not None and mat.users == 0:
        bpy.data.materials.remove(mat)
    if save:
        bpy.ops.wm.save_mainfile()
    REPORT["summary"] = summary
    REPORT["problems"] = problems
    log("PROBLEMS:", problems if problems else "none")
    return summary, problems


if __name__ == "__main__":
    main()
