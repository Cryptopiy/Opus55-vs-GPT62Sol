extends RefCounted
## L5 machinery: kinematic moving platform (x = -2..2 at y = 5, period 6 s), switch plate (mass >= 5 kg for
## 0.5 s) and the hinged door it opens (90 deg over 1 s, stays open). All motion is driven from simulation
## ticks (never from frame time) so runs are deterministic.

const C := preload("res://scripts/core/config.gd")
const Common := preload("res://scripts/objects/obj_common.gd")
const Fp := preload("res://scripts/core/footprint.gd")
const ML := preload("res://scripts/core/mesh_lib.gd")
const Mats := preload("res://scripts/core/materials.gd")


static func mover_x(t: float) -> float:
	return MoverSim.x_at(t)


## Placement obstacles: the mover's whole travel envelope, the door and its swing, the switch plate.
static func footprints(level: Dictionary) -> Array:
	var out := []
	if level.get("mover", false):
		var w := C.MOVER_X_RANGE.y - C.MOVER_X_RANGE.x + C.MOVER_SIZE.x
		out.append(Fp.rect(Vector2((C.MOVER_X_RANGE.x + C.MOVER_X_RANGE.y) * 0.5, C.MOVER_Y), Vector2(w, C.MOVER_SIZE.y + 0.2)))
	if level.has("door"):
		var h: Vector2 = level["door"]
		var L := C.DOOR_SIZE.y
		out.append(Fp.rect(h + Vector2(0, -L * 0.5), C.DOOR_SIZE))
		out.append(Fp.rect(h + Vector2(L * 0.5, -L * 0.5), Vector2(L + C.DOOR_SIZE.x, L + C.DOOR_SIZE.x)))
	if level.has("switch"):
		out.append(Fp.rect(level["switch"], C.SWITCH_SIZE + Vector2(0, 0.1)))
	return out


static func _mover_meshes(parent: Node3D) -> void:
	ML.add_box(parent, Vector3(C.MOVER_SIZE.x, C.MOVER_SIZE.y, C.OBJECT_DEPTH * 1.1), Vector3.ZERO, Mats.rounded(C.COL_MOVER, 0.45, 0.2))
	var stripe := Mats.emissive(C.COL_MOVER.lightened(0.4), 1.2)
	ML.add_box(parent, Vector3(C.MOVER_SIZE.x * 0.9, 0.04, 0.03), Vector3(0, 0, C.OBJECT_DEPTH * 0.56), stripe)


static func _door_meshes(parent: Node3D) -> void:
	ML.add_box(parent, Vector3(C.DOOR_SIZE.x, C.DOOR_SIZE.y, C.OBJECT_DEPTH * 1.2), Vector3(0, -C.DOOR_SIZE.y * 0.5, 0), Mats.rounded(C.COL_DOOR, 0.5, 0.3))
	var hinge := ML.add_mesh(parent, ML.cylinder(0.12, C.OBJECT_DEPTH * 1.3, 16), Vector3.ZERO, Mats.standard(C.COL_DOOR.darkened(0.4), 0.4, 0.7))
	hinge.rotation.x = PI * 0.5
	var warn := Mats.rounded(C.COL_SWITCH_OFF, 0.5)
	for i in 3:
		ML.add_box(parent, Vector3(C.DOOR_SIZE.x * 1.05, 0.18, 0.04), Vector3(0, -0.6 - 0.8 * i, C.OBJECT_DEPTH * 0.6), warn, 30.0)


## Returns the plate top (animated when pressed) and its emissive material.
static func _switch_meshes(parent: Node3D) -> Array:
	var base_h := 0.12
	ML.add_box(parent, Vector3(C.SWITCH_SIZE.x + 0.2, base_h, C.OBJECT_DEPTH * 1.1), Vector3(0, -C.SWITCH_SIZE.y * 0.5 - base_h * 0.5 + 0.06, 0), Mats.rounded(C.COL_DOOR, 0.6))
	var mat := Mats.emissive_unique(C.COL_SWITCH_OFF, 2.5)
	var top := ML.add_box(parent, Vector3(C.SWITCH_SIZE.x, C.SWITCH_SIZE.y, C.OBJECT_DEPTH), Vector3(0, 0.02, 0), mat)
	return [top, mat]


static func build_visuals(level: Dictionary, parent: Node3D) -> void:
	if level.get("mover", false):
		var m := Node3D.new()
		m.position = Vector3(mover_x(0.0), C.MOVER_Y, 0)
		parent.add_child(m)
		_mover_meshes(m)
		# travel rail behind the platform shows where it will move
		var rail_w := C.MOVER_X_RANGE.y - C.MOVER_X_RANGE.x + C.MOVER_SIZE.x
		var rail := ML.add_box(parent, Vector3(rail_w, 0.06, 0.06), Vector3((C.MOVER_X_RANGE.x + C.MOVER_X_RANGE.y) * 0.5, C.MOVER_Y, -0.62), Mats.rounded(C.COL_MOVER.darkened(0.3), 0.6))
		rail.cast_shadow = GeometryInstance3D.SHADOW_CASTING_SETTING_OFF
	if level.has("door"):
		var d := Node3D.new()
		var h: Vector2 = level["door"]
		d.position = Vector3(h.x, h.y, 0)
		parent.add_child(d)
		_door_meshes(d)
	if level.has("switch"):
		var s := Node3D.new()
		var p: Vector2 = level["switch"]
		s.position = Vector3(p.x, p.y, 0)
		parent.add_child(s)
		_switch_meshes(s)


static func build_sim(level: Dictionary, sim: Node) -> void:
	if level.get("mover", false):
		var body := AnimatableBody3D.new()
		body.sync_to_physics = true
		body.transform = Transform3D(Basis(), Vector3(mover_x(0.0), C.MOVER_Y, 0))
		body.physics_material_override = Mats.phys(0.8, 0.0)
		body.collision_layer = C.LAYER_STATIC
		body.collision_mask = C.LAYER_DYNAMIC | C.LAYER_ROPE
		ML.add_box_shape(body, Vector3(C.MOVER_SIZE.x, C.MOVER_SIZE.y, C.OBJECT_DEPTH * 1.1))
		_mover_meshes(body)
		var mover := MoverSim.new()
		mover.body = body
		body.add_child(mover)
		sim.add_node(body)
		sim.add_ticker(mover)
	var door: DoorSim = null
	if level.has("door"):
		var h: Vector2 = level["door"]
		var body := AnimatableBody3D.new()
		body.sync_to_physics = true
		body.transform = Transform3D(Basis(), Vector3(h.x, h.y, 0))
		body.physics_material_override = Mats.phys(0.6, 0.0)
		body.collision_layer = C.LAYER_STATIC
		body.collision_mask = C.LAYER_DYNAMIC | C.LAYER_ROPE
		ML.add_box_shape(body, Vector3(C.DOOR_SIZE.x, C.DOOR_SIZE.y, C.OBJECT_DEPTH * 1.2), Vector3(0, -C.DOOR_SIZE.y * 0.5, 0))
		_door_meshes(body)
		door = DoorSim.new()
		door.body = body
		body.add_child(door)
		sim.add_node(body)
		sim.add_ticker(door)
	if level.has("switch"):
		var p: Vector2 = level["switch"]
		var body := Common.static_body(Transform3D(Basis(), Vector3(p.x, p.y, 0)), 0.7)
		ML.add_box_shape(body, Vector3(C.SWITCH_SIZE.x, C.SWITCH_SIZE.y, C.OBJECT_DEPTH))
		var sw := SwitchSim.new()
		var parts := _switch_meshes(body)
		sw.top = parts[0]
		sw.mat = parts[1]
		sw.rest_y = sw.top.position.y
		sw.door = door
		sw.sensor = Common.sensor(Vector3(C.SWITCH_SIZE.x * 0.95, 0.35, C.OBJECT_DEPTH * 0.9), Vector3(0, C.SWITCH_SIZE.y * 0.5 + 0.175, 0))
		body.add_child(sw.sensor)
		body.add_child(sw)
		sim.add_node(body)
		sim.add_ticker(sw)


class MoverSim extends Node:
	var body: AnimatableBody3D

	## Smooth back-and-forth between x = -2 and x = 2 (period 6 s), starting at x = -2 at t = 0.
	static func x_at(t: float) -> float:
		var mid := (C.MOVER_X_RANGE.x + C.MOVER_X_RANGE.y) * 0.5
		var amp := (C.MOVER_X_RANGE.y - C.MOVER_X_RANGE.x) * 0.5
		return mid - amp * cos(TAU * t / C.MOVER_PERIOD)

	func sim_tick(sim: Node) -> void:
		# target pose for the step about to run (sync_to_physics turns it into a kinematic move with velocity,
		# so objects resting on the platform are carried along)
		var t := float(sim.steps + 1) * C.PHYS_DT
		body.position = Vector3(x_at(t), C.MOVER_Y, 0)


class DoorSim extends Node:
	var body: AnimatableBody3D
	var open_at := -1.0
	var is_open := false

	func trigger(t: float) -> void:
		if open_at < 0.0:
			open_at = t
			Sfx.play(&"door", -4.0, 1.0)

	func sim_tick(sim: Node) -> void:
		if open_at < 0.0:
			return
		var t := float(sim.steps + 1) * C.PHYS_DT
		var f := clampf((t - open_at) / C.DOOR_OPEN_TIME, 0.0, 1.0)
		f = f * f * (3.0 - 2.0 * f)
		body.rotation.z = deg_to_rad(C.DOOR_OPEN_DEG) * f
		is_open = f >= 1.0


class SwitchSim extends Node:
	var sensor: Area3D
	var top: MeshInstance3D
	var mat: StandardMaterial3D
	var rest_y := 0.0
	var door: DoorSim
	var held := 0.0
	var latched := false
	var _was_pressed := false

	func sim_tick(sim: Node) -> void:
		var pressed := false
		for b: RigidBody3D in sim.dynamic_bodies:
			if b.mass >= C.SWITCH_MIN_MASS and sensor.overlaps_body(b):
				pressed = true
				break
		held = held + C.PHYS_DT if pressed else 0.0
		if pressed and not _was_pressed:
			Sfx.play(&"switch", -2.0, 1.0)
		_was_pressed = pressed
		if not latched and held >= C.SWITCH_HOLD:
			latched = true
			mat.albedo_color = C.COL_SWITCH_ON
			mat.emission = C.COL_SWITCH_ON
			if door:
				door.trigger(float(sim.steps) * C.PHYS_DT)
		top.position.y = rest_y - (C.SWITCH_PRESS_DEPTH if pressed or latched else 0.0)
