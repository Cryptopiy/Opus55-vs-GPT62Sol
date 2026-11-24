extends RefCounted
## P-05 Plank: static 3.0 x 0.2 x 1.0 m box (ramp / platform / wall), friction 0.5.

const C := preload("res://scripts/core/config.gd")
const Common := preload("res://scripts/objects/obj_common.gd")
const Fp := preload("res://scripts/core/footprint.gd")
const ML := preload("res://scripts/core/mesh_lib.gd")
const Mats := preload("res://scripts/core/materials.gd")


static func defaults() -> Dictionary:
	return {}


## Planks are symmetric under a half turn, so keep the angle in [-90, 90) to make records canonical.
static func constrain(rec: Dictionary) -> void:
	rec["rot"] = fposmod(float(rec["rot"]) + 90.0, 180.0) - 90.0


static func footprint(rec: Dictionary) -> Array:
	return [Fp.rect(Common.origin(rec), C.PLANK_SIZE, Common.rot_rad(rec))]


static func _add_meshes(parent: Node3D) -> void:
	ML.add_box(parent, Vector3(C.PLANK_SIZE.x, C.PLANK_SIZE.y, C.OBJECT_DEPTH), Vector3.ZERO, Mats.rounded(C.COL_PLANK, 0.75))
	# two darker grain strips on the front face keep the plank readable as "wood" while staying flat-coloured
	var grain := Mats.rounded(C.COL_PLANK.darkened(0.18), 0.8)
	for i in 2:
		ML.add_box(parent, Vector3(C.PLANK_SIZE.x * (0.7 - 0.25 * i), 0.025, 0.02), Vector3(-0.2 + 0.5 * i, 0.03 - 0.06 * i, C.OBJECT_DEPTH * 0.5), grain)


static func build_visual(rec: Dictionary) -> Node3D:
	var root := Common.visual_root(rec)
	_add_meshes(root)
	return root


static func build_sim(rec: Dictionary, sim: Node) -> void:
	var body := Common.static_body(Common.xform(rec), C.PLANK_FRICTION)
	ML.add_box_shape(body, Vector3(C.PLANK_SIZE.x, C.PLANK_SIZE.y, C.OBJECT_DEPTH))
	_add_meshes(body)
	sim.add_node(body)
