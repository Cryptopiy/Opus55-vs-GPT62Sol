extends RefCounted
## P-06 Box: dynamic 1 x 1 x 1 m cube, 5 kg ("box_s") or 20 kg ("box_h"), friction 0.6.

const C := preload("res://scripts/core/config.gd")
const Common := preload("res://scripts/objects/obj_common.gd")
const Fp := preload("res://scripts/core/footprint.gd")
const ML := preload("res://scripts/core/mesh_lib.gd")
const Mats := preload("res://scripts/core/materials.gd")


static func defaults() -> Dictionary:
	return {}


## A cube looks identical every 90 degrees: keep the angle in [-45, 45) so records are canonical.
static func constrain(rec: Dictionary) -> void:
	rec["rot"] = fposmod(float(rec["rot"]) + 45.0, 90.0) - 45.0


static func footprint(rec: Dictionary) -> Array:
	return [Fp.rect(Common.origin(rec), Vector2(C.BOX_SIZE, C.BOX_SIZE), Common.rot_rad(rec))]


static func is_heavy(rec: Dictionary) -> bool:
	return String(rec["type"]) == "box_h"


static func _add_meshes(parent: Node3D, heavy: bool) -> void:
	var s := C.BOX_SIZE
	if heavy:
		ML.add_box(parent, Vector3(s, s, s), Vector3.ZERO, Mats.rounded(C.COL_BOX_H, 0.45, 0.35))
		# dark steel bands mark the heavy box
		var band := Mats.rounded(C.COL_BOX_H.darkened(0.45), 0.5, 0.4)
		ML.add_box(parent, Vector3(s * 1.02, s * 0.16, s * 1.02), Vector3(0, s * 0.27, 0), band)
		ML.add_box(parent, Vector3(s * 1.02, s * 0.16, s * 1.02), Vector3(0, -s * 0.27, 0), band)
	else:
		ML.add_box(parent, Vector3(s, s, s), Vector3.ZERO, Mats.rounded(C.COL_BOX_S, 0.8))
		# crate look: lighter inset panel with a diagonal brace on the front face
		var panel := Mats.rounded(C.COL_BOX_S.lightened(0.18), 0.85)
		ML.add_box(parent, Vector3(s * 0.74, s * 0.74, 0.03), Vector3(0, 0, s * 0.5), panel)
		ML.add_box(parent, Vector3(s * 0.95, 0.09, 0.04), Vector3(0, 0, s * 0.5 + 0.02), Mats.rounded(C.COL_BOX_S.darkened(0.2), 0.85), 45.0)


static func build_visual(rec: Dictionary) -> Node3D:
	var root := Common.visual_root(rec)
	_add_meshes(root, is_heavy(rec))
	return root


static func build_sim(rec: Dictionary, sim: Node) -> void:
	var heavy := is_heavy(rec)
	var body := Common.rigid_body(Common.xform(rec), C.BOX_H_MASS if heavy else C.BOX_S_MASS, C.BOX_FRICTION)
	ML.add_box_shape(body, Vector3(C.BOX_SIZE, C.BOX_SIZE, C.BOX_SIZE))
	_add_meshes(body, heavy)
	sim.add_node(body)
	sim.add_dynamic(body, &"box_h" if heavy else &"box_s", C.BOX_SIZE * 0.5)
