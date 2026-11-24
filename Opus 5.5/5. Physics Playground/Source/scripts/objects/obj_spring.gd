extends RefCounted
## P-09 Spring pad: static pad; a body touching its top gets an impulse that makes its velocity along the pad's
## local up exactly 12 m/s (cooldown 0.3 s), with a compress animation and a "boing".
## Record: x/y = centre of the 1.0 x 0.3 pad, rot = pad orientation (local up = launch direction).

const C := preload("res://scripts/core/config.gd")
const Common := preload("res://scripts/objects/obj_common.gd")
const Fp := preload("res://scripts/core/footprint.gd")
const ML := preload("res://scripts/core/mesh_lib.gd")
const Mats := preload("res://scripts/core/materials.gd")

const BASE_H := 0.1
const PLATE_H := 0.08


static func defaults() -> Dictionary:
	return {}


static func constrain(rec: Dictionary) -> void:
	rec["rot"] = fposmod(float(rec["rot"]) + 180.0, 360.0) - 180.0


static func footprint(rec: Dictionary) -> Array:
	return [Fp.rect(Common.origin(rec), C.SPRING_SIZE, Common.rot_rad(rec))]


## Returns the plate node so the simulation can animate it.
static func _add_meshes(parent: Node3D) -> Node3D:
	var h := C.SPRING_SIZE.y
	ML.add_box(parent, Vector3(C.SPRING_SIZE.x, BASE_H, C.OBJECT_DEPTH), Vector3(0, -h * 0.5 + BASE_H * 0.5, 0), Mats.rounded(C.COL_SPRING.darkened(0.55), 0.6, 0.3))
	var coils := Node3D.new()
	coils.name = "Coils"
	coils.position = Vector3(0, -h * 0.5 + BASE_H, 0)
	parent.add_child(coils)
	var coil_mat := Mats.standard(Color(0.75, 0.77, 0.8), 0.3, 0.9)
	for i in 3:
		var y := (h - BASE_H - PLATE_H) * (float(i) + 0.5) / 3.0
		for side: float in [-0.28, 0.28]:
			var c := ML.add_mesh(coils, ML.cylinder(0.11 - 0.015 * (i % 2), 0.035, 16), Vector3(side, y, 0), coil_mat)
			c.scale = Vector3(1, 1, 2.6)
	var plate := Node3D.new()
	plate.name = "Plate"
	plate.position = Vector3(0, h * 0.5 - PLATE_H * 0.5, 0)
	parent.add_child(plate)
	ML.add_box(plate, Vector3(C.SPRING_SIZE.x * 0.94, PLATE_H, C.OBJECT_DEPTH * 0.96), Vector3.ZERO, Mats.rounded(C.COL_SPRING, 0.45))
	# up-arrow chevron on the front face shows the launch direction
	var arrow := Mats.rounded(C.COL_SPRING.darkened(0.5), 0.5)
	ML.add_box(plate, Vector3(0.22, 0.03, 0.02), Vector3(-0.07, 0, C.OBJECT_DEPTH * 0.48), arrow, 30.0)
	ML.add_box(plate, Vector3(0.22, 0.03, 0.02), Vector3(0.07, 0, C.OBJECT_DEPTH * 0.48), arrow, -30.0)
	return plate


static func build_visual(rec: Dictionary) -> Node3D:
	var root := Common.visual_root(rec)
	_add_meshes(root)
	return root


static func build_sim(rec: Dictionary, sim: Node) -> void:
	var body := Common.static_body(Common.xform(rec), 0.6)
	ML.add_box_shape(body, Vector3(C.SPRING_SIZE.x, C.SPRING_SIZE.y, C.OBJECT_DEPTH))
	var pad := SpringSim.new()
	pad.plate = _add_meshes(body)
	pad.coils = body.get_node("Coils") as Node3D
	pad.rest_y = pad.plate.position.y
	pad.sensor = Common.sensor(Vector3(C.SPRING_SIZE.x * 0.9, C.SPRING_TRIGGER_HEIGHT, C.OBJECT_DEPTH * 0.9),
		Vector3(0, C.SPRING_SIZE.y * 0.5 + C.SPRING_TRIGGER_HEIGHT * 0.5, 0))
	body.add_child(pad.sensor)
	body.add_child(pad)
	sim.add_node(body)
	sim.add_ticker(pad)


class SpringSim extends Node3D:
	var sensor: Area3D
	var plate: Node3D
	var coils: Node3D
	var rest_y := 0.0
	var _cooldown := 0.0
	var _anim := 0.0

	func sim_tick(sim: Node) -> void:
		_cooldown = maxf(0.0, _cooldown - C.PHYS_DT)
		_anim = maxf(0.0, _anim - C.PHYS_DT)
		if _cooldown > 0.0:
			return
		var up := sensor.global_basis.y
		var fired := false
		for b: RigidBody3D in sim.dynamic_bodies:
			if not sensor.overlaps_body(b):
				continue
			var vn := b.linear_velocity.dot(up)
			if vn > C.SPRING_MAX_APPROACH:
				continue
			# impulse that sets the velocity component along local up to exactly 12 m/s
			b.apply_central_impulse(up * (C.SPRING_LAUNCH_SPEED - vn) * b.mass)
			sim.mark_launched(b)
			fired = true
		if fired:
			_cooldown = C.SPRING_COOLDOWN
			_anim = C.SPRING_ANIM_TIME
			Sfx.play(&"boing", -3.0, 1.0)

	func _process(_delta: float) -> void:
		# compress quickly, then overshoot back (visual only, driven by the sim-time timer)
		var t := 1.0 - _anim / C.SPRING_ANIM_TIME if _anim > 0.0 else 1.0
		var squash := sin(t * PI) * exp(-2.5 * t) if _anim > 0.0 else 0.0
		plate.position.y = rest_y - squash * 0.12
		coils.scale.y = 1.0 - squash * 0.6
