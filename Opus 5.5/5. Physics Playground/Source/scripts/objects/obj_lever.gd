extends RefCounted
## P-07 Lever / seesaw: 5 m plank (10 kg, angular damping 0.5) on a fulcrum, HingeJoint3D about Z, limits +-35 deg.
## Record: x/y = fulcrum pivot (the placement point), rot = initial tilt (clamped to the limits),
## "fulcrum" = pivot offset from the plank centre along the plank, moved in 0.5 m steps with Alt+drag.

const C := preload("res://scripts/core/config.gd")
const Common := preload("res://scripts/objects/obj_common.gd")
const Fp := preload("res://scripts/core/footprint.gd")
const ML := preload("res://scripts/core/mesh_lib.gd")
const Mats := preload("res://scripts/core/materials.gd")

const STAND_WIDTH := 0.7
const STAND_GAP := 0.12   # apex sits just under the plank so the stand never blocks the swing


static func defaults() -> Dictionary:
	return {"fulcrum": 0.0}


static func constrain(rec: Dictionary) -> void:
	rec["rot"] = clampf(float(rec["rot"]), -C.LEVER_LIMIT_DEG, C.LEVER_LIMIT_DEG)
	rec["fulcrum"] = clampf(snappedf(float(rec.get("fulcrum", 0.0)), C.LEVER_FULCRUM_STEP), -C.LEVER_FULCRUM_MAX, C.LEVER_FULCRUM_MAX)


## Plank centre in world space (the plank is offset from the pivot by -fulcrum along its axis).
static func plank_center(rec: Dictionary) -> Vector2:
	var dir := Vector2.RIGHT.rotated(Common.rot_rad(rec))
	return Common.origin(rec) - dir * float(rec.get("fulcrum", 0.0))


static func footprint(rec: Dictionary) -> Array:
	var o := Common.origin(rec)
	return [
		Fp.rect(plank_center(rec), Vector2(C.LEVER_LENGTH, C.LEVER_THICKNESS), Common.rot_rad(rec)),
		Fp.rect(o + Vector2(0, -STAND_GAP - C.LEVER_STAND_HEIGHT * 0.5), Vector2(STAND_WIDTH * 0.6, C.LEVER_STAND_HEIGHT)),
	]


## Editor handle (Alt+drag) sits on the pivot.
static func handles(rec: Dictionary) -> Array[Vector2]:
	return [Common.origin(rec)]


## New record values when the fulcrum handle is dragged to `p`: the plank stays put, the pivot slides along it.
static func drag_fulcrum(rec: Dictionary, p: Vector2) -> Dictionary:
	var out := rec.duplicate(true)
	var dir := Vector2.RIGHT.rotated(Common.rot_rad(rec))
	var center := plank_center(rec)
	var f := clampf(snappedf((p - center).dot(dir), C.LEVER_FULCRUM_STEP), -C.LEVER_FULCRUM_MAX, C.LEVER_FULCRUM_MAX)
	var pivot := center + dir * f
	out["fulcrum"] = f
	out["x"] = pivot.x
	out["y"] = pivot.y
	return out


static func _add_stand_meshes(parent: Node3D) -> void:
	ML.add_mesh(parent, ML.prism(STAND_WIDTH, C.LEVER_STAND_HEIGHT, C.OBJECT_DEPTH * 0.8), Vector3(0, -STAND_GAP, 0), Mats.standard(C.COL_LEVER_STAND, 0.7))
	var pin := ML.add_mesh(parent, ML.cylinder(0.09, C.OBJECT_DEPTH * 1.05, 16), Vector3.ZERO, Mats.standard(C.COL_LEVER_STAND.darkened(0.3), 0.4, 0.6))
	pin.rotation.x = PI * 0.5


static func _add_plank_meshes(parent: Node3D, fulcrum: float) -> void:
	ML.add_box(parent, Vector3(C.LEVER_LENGTH, C.LEVER_THICKNESS, C.OBJECT_DEPTH * 0.9), Vector3(-fulcrum, 0, 0), Mats.rounded(C.COL_LEVER, 0.55))
	# white end caps make the plank ends easy to read while it swings
	var cap := Mats.rounded(Color(0.96, 0.94, 0.92), 0.6)
	for side: float in [-1.0, 1.0]:
		ML.add_box(parent, Vector3(0.12, C.LEVER_THICKNESS * 1.06, C.OBJECT_DEPTH * 0.92), Vector3(-fulcrum + side * (C.LEVER_LENGTH * 0.5 - 0.06), 0, 0), cap)


static func build_visual(rec: Dictionary) -> Node3D:
	var root := Node3D.new()
	root.position = Vector3(float(rec["x"]), float(rec["y"]), 0)
	_add_stand_meshes(root)
	var plank := Node3D.new()
	plank.rotation.z = Common.rot_rad(rec)
	root.add_child(plank)
	_add_plank_meshes(plank, float(rec.get("fulcrum", 0.0)))
	return root


static func build_sim(rec: Dictionary, sim: Node) -> void:
	var pivot := Vector3(float(rec["x"]), float(rec["y"]), 0)
	var fulcrum := float(rec.get("fulcrum", 0.0))
	var stand := Common.static_body(Transform3D(Basis(), pivot), C.LEVER_FRICTION)
	var stand_shape := ML.add_prism_shape(stand, STAND_WIDTH, C.LEVER_STAND_HEIGHT, C.OBJECT_DEPTH * 0.8)
	stand_shape.position = Vector3(0, -STAND_GAP, 0)
	_add_stand_meshes(stand)
	sim.add_node(stand)
	# The plank is created flat so the hinge's zero angle is horizontal: the +-35 deg limits are then absolute,
	# and the initial tilt is applied after the joint exists (verified in the Jolt prototype).
	var plank := Common.rigid_body(Transform3D(Basis(), pivot), C.LEVER_MASS, C.LEVER_FRICTION)
	plank.angular_damp_mode = RigidBody3D.DAMP_MODE_REPLACE
	plank.angular_damp = C.LEVER_ANGULAR_DAMP
	ML.add_box_shape(plank, Vector3(C.LEVER_LENGTH, C.LEVER_THICKNESS, C.OBJECT_DEPTH * 0.9), Vector3(-fulcrum, 0, 0))
	_add_plank_meshes(plank, fulcrum)
	sim.add_node(plank)
	var hinge := HingeJoint3D.new()
	hinge.set_flag(HingeJoint3D.FLAG_USE_LIMIT, true)
	hinge.set_param(HingeJoint3D.PARAM_LIMIT_LOWER, deg_to_rad(-C.LEVER_LIMIT_DEG))
	hinge.set_param(HingeJoint3D.PARAM_LIMIT_UPPER, deg_to_rad(C.LEVER_LIMIT_DEG))
	Common.connect_joint(hinge, sim, pivot, stand, plank)
	plank.global_transform = Transform3D(Basis(Vector3.BACK, Common.rot_rad(rec)), pivot)
	sim.add_dynamic(plank, &"lever", C.LEVER_LENGTH * 0.5)
