class_name LabMachine
extends Node3D

var board: LabBoard
var shuttle: AnimatableBody3D
var door: AnimatableBody3D
var switch_mesh: MeshInstance3D
var switch_sensor: Area3D
var loads: Array[RigidBody3D] = []
var switch_time := 0.0
var opened_at := -1.0
var switch_off := LabGeometry.material(Color("#e4a24b"), 0.12, 1.0)
var switch_on := LabGeometry.material(Color("#57efb5"), 0.12, 1.7)

func assemble(owner_board: LabBoard) -> void:
	board = owner_board
	shuttle = _animated(Vector3(LabConfig.SHUTTLE_X.x, LabConfig.SHUTTLE_Y, 0))
	var size := LabConfig.SHUTTLE_SIZE
	LabGeometry.box_shape(shuttle, size)
	LabGeometry.block(shuttle, size, LabGeometry.material(Color("#637a99")))
	LabGeometry.block(shuttle, Vector3(2.53, 0.075, 1.38), LabGeometry.material(Color("#8fb8de")), Vector3(0, 0.11, 0))
	door = _animated(Vector3(LabConfig.DOOR_X, LabConfig.DOOR_HEIGHT, 0))
	var door_size := Vector3(0.25, LabConfig.DOOR_HEIGHT, 1.14)
	var center := Vector3(0, -LabConfig.DOOR_HEIGHT * 0.5, 0)
	LabGeometry.box_shape(door, door_size, center)
	LabGeometry.block(door, door_size, LabGeometry.material(Color("#566d89")), center)
	for index in range(14):
		LabGeometry.block(door, Vector3(0.05, 0.16, 1.15), switch_off, Vector3(0.12, -0.42 - index * 0.53, 0))
	var plate_pos := LabConfig.in_plane(LabConfig.SWITCH_POS)
	var plate := LabGeometry.solid(self, LabConfig.SWITCH_SIZE, plate_pos, 0, 0.75,
		LabGeometry.material(Color("#39546a")))
	switch_mesh = LabGeometry.block(plate, Vector3(1.14, 0.055, 1.13), switch_off, Vector3(0, 0.11, 0))
	switch_sensor = Area3D.new()
	switch_sensor.collision_layer = 0
	switch_sensor.collision_mask = LabConfig.BODY_LAYER
	add_child(switch_sensor)
	switch_sensor.position = plate_pos + Vector3(0, 0.12, 0)
	LabGeometry.box_shape(switch_sensor, Vector3(1.16, 0.11, 1.12))
	switch_sensor.body_entered.connect(_load_entered)
	switch_sensor.body_exited.connect(_load_exited)

func _load_entered(body: Node3D) -> void:
	if body is RigidBody3D and body.mass >= LabConfig.SWITCH_MASS:
		loads.append(body)

func _load_exited(body: Node3D) -> void:
	if body is RigidBody3D:
		loads.erase(body)

func _animated(point: Vector3) -> AnimatableBody3D:
	var body := AnimatableBody3D.new()
	body.collision_layer = LabConfig.SOLID_LAYER
	body.collision_mask = LabConfig.BODY_LAYER
	body.physics_material_override = LabGeometry.physics_material(0.72)
	body.position = point
	add_child(body)
	return body

func step(delta: float) -> void:
	var phase := 0.5 - 0.5 * cos(board.elapsed * TAU / LabConfig.SHUTTLE_PERIOD)
	shuttle.position = Vector3(lerpf(LabConfig.SHUTTLE_X.x, LabConfig.SHUTTLE_X.y, phase), LabConfig.SHUTTLE_Y, 0)
	if opened_at < 0.0:
		# The thin sensor sits along the plate surface and counts a load at any shape contact.
		var pressed := not loads.is_empty()
		switch_time = switch_time + delta if pressed else 0.0
		switch_mesh.position.y = 0.06 if pressed else 0.11
		if switch_time + 0.00001 >= LabConfig.SWITCH_HOLD:
			opened_at = board.elapsed
			switch_mesh.material_override = switch_on
			board.feedback.emit("switch", switch_sensor.position, 1.0)
			board.feedback.emit("door", door.position, 1.0)
	if opened_at >= 0.0:
		var travel := clampf((board.elapsed - opened_at) / LabConfig.DOOR_DURATION, 0.0, 1.0)
		door.rotation_degrees.z = LabConfig.DOOR_ANGLE * travel
