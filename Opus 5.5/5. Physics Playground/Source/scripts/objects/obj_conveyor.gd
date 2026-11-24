extends RefCounted
## P-10 Conveyor: 3 m static belt with constant_linear_velocity = 2 m/s along its local X; chevron tread
## texture scrolls by exactly the belt distance travelled in simulation time.

const C := preload("res://scripts/core/config.gd")
const Common := preload("res://scripts/objects/obj_common.gd")
const Fp := preload("res://scripts/core/footprint.gd")
const ML := preload("res://scripts/core/mesh_lib.gd")
const Mats := preload("res://scripts/core/materials.gd")


static func defaults() -> Dictionary:
	return {}


static func constrain(rec: Dictionary) -> void:
	rec["rot"] = fposmod(float(rec["rot"]) + 180.0, 360.0) - 180.0


static func footprint(rec: Dictionary) -> Array:
	return [Fp.rect(Common.origin(rec), C.CONVEYOR_SIZE, Common.rot_rad(rec))]


## Returns the belt mesh instance (its "scroll" instance uniform is animated in simulation).
static func _add_meshes(parent: Node3D) -> MeshInstance3D:
	var sz := Vector3(C.CONVEYOR_SIZE.x, C.CONVEYOR_SIZE.y, C.OBJECT_DEPTH)
	var belt := ML.add_box(parent, sz, Vector3.ZERO, Mats.conveyor())
	var roller_mat := Mats.standard(Color(0.7, 0.72, 0.76), 0.35, 0.8)
	for side: float in [-1.0, 1.0]:
		var r := ML.add_mesh(parent, ML.cylinder(C.CONVEYOR_SIZE.y * 0.42, C.OBJECT_DEPTH * 1.04, 16), Vector3(side * C.CONVEYOR_SIZE.x * 0.5, 0, 0), roller_mat)
		r.rotation.x = PI * 0.5
	return belt


static func build_visual(rec: Dictionary) -> Node3D:
	var root := Common.visual_root(rec)
	_add_meshes(root)
	return root


static func build_sim(rec: Dictionary, sim: Node) -> void:
	var xf := Common.xform(rec)
	var body := Common.static_body(xf, C.CONVEYOR_FRICTION)
	# Jolt applies this as a surface velocity: bodies resting on the belt are carried along local +X
	body.constant_linear_velocity = xf.basis.x * C.CONVEYOR_SPEED
	ML.add_box_shape(body, Vector3(C.CONVEYOR_SIZE.x, C.CONVEYOR_SIZE.y, C.OBJECT_DEPTH))
	var belt := ConveyorSim.new()
	belt.belt = _add_meshes(body)
	body.add_child(belt)
	sim.add_node(body)
	sim.add_ticker(belt)
	sim.conveyor_count += 1


class ConveyorSim extends Node:
	var belt: MeshInstance3D
	var scroll := 0.0

	func sim_tick(_sim: Node) -> void:
		scroll = fposmod(scroll + C.CONVEYOR_SPEED * C.PHYS_DT, 1000.0)
		belt.set_instance_shader_parameter("scroll", scroll)
