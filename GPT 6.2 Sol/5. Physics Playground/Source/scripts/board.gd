class_name LabBoard
extends Node3D

signal feedback(event: String, point: Vector3, strength: float)

var level: Dictionary
var ball: RigidBody3D
var ballspec: Dictionary
var bodies: Array[RigidBody3D] = []
var pieces: Array[Node3D] = []
var goal_sensor: Area3D
var elapsed := 0.0
var goal_elapsed := 0.0
var outcome := ""
var active := false
var minimum_ball_y := INF
var maximum_ball_y := -INF
var maximum_ball_x := -INF
var max_rope_stretch := 0.0
var machine: LabMachine
var mechanisms: LabMechanisms
var measured_step_ms := 0.0
var was_contact := false
var last_ball_velocity := Vector3.ZERO

func assemble(data: Dictionary, placements: Array, simulate: bool) -> void:
	level = data
	active = simulate
	for spec in level.fixtures:
		_create_fixture(spec)
	_create_receiver()
	_create_ball()
	mechanisms = LabMechanisms.new()
	mechanisms.board = self
	add_child(mechanisms)
	if level.id == "L5":
		machine = LabMachine.new()
		add_child(machine)
		machine.assemble(self)
	for spec in placements:
		pieces.append(mechanisms.build(spec))
	mechanisms.arm_contacts()

func _create_fixture(spec: Array) -> void:
	var size := Vector3(spec[1].x, spec[1].y, 1.75)
	var body := LabGeometry.solid(self, size, LabConfig.in_plane(spec[0]), spec[2], 0.68, null)
	LabGeometry.rounded(body, size, LabGeometry.material(Color("#546a84")), minf(size.y * 0.23, 0.09))
	LabGeometry.block(body, Vector3(maxf(size.x - 0.025, 0.02), 0.037, 0.075),
		LabGeometry.material(Color("#90aac3")), Vector3(0, size.y * 0.5 - 0.041, size.z * 0.5 - 0.013))

func _create_ball() -> void:
	ballspec = LabConfig.BALLS[level.ball]
	ball = LabGeometry.mobile(self, LabConfig.in_plane(level.start), 0, ballspec.mass, ballspec.friction,
		ballspec.bounce, active)
	ball.name = "Ball"
	ball.contact_monitor = true
	ball.max_contacts_reported = 6
	var collision := SphereShape3D.new()
	collision.radius = ballspec.radius
	LabGeometry.shape(ball, collision)
	LabGeometry.sphere(ball, ballspec.radius,
		LabGeometry.material(ballspec.color, 0.6 if level.ball == "steel" else 0.04))
	var ring := TorusMesh.new()
	ring.inner_radius = ballspec.radius * 0.96
	ring.outer_radius = ballspec.radius * 1.018
	ring.rings = 20
	ring.ring_segments = 10
	var stripe := LabGeometry.mesh(ball, ring, LabGeometry.material(ballspec.color.darkened(0.32)))
	stripe.rotation.z = 0.48
	bodies.append(ball)

func _create_receiver() -> void:
	var target := LabConfig.in_plane(level.goal)
	var base := LabGeometry.solid(self, Vector3(1.90, 0.20, 1.32), target + Vector3(0, -0.61, 0), 0, 0.96,
		LabGeometry.material(Color("#286076")))
	base.physics_material_override = LabGeometry.physics_material(0.96, 0.9, true)
	var right := LabGeometry.solid(self, Vector3(0.20, 0.95, 1.32), target + Vector3(0.89, -0.16, 0), 0, 0.8,
		LabGeometry.material(Color("#2d677c")))
	right.physics_material_override = base.physics_material_override
	if level.id in ["L2", "L3", "L4"]:
		var left := LabGeometry.solid(self, Vector3(0.16, 0.54, 1.32), target + Vector3(-0.88, -0.25, 0), 0, 0.8,
			LabGeometry.material(Color("#2d677c")))
		left.physics_material_override = base.physics_material_override
	LabGeometry.block(self, Vector3(1.96, 0.09, 0.08),
		LabGeometry.material(Color("#55e1be"), 0.12, 1.35), target + Vector3(0, -0.36, 0.72))
	var rear := LabGeometry.block(self, Vector3(1.96, 0.60, 0.12),
		LabGeometry.material(Color("#376f84")), target + Vector3(0, -0.27, -0.68))
	rear.cast_shadow = GeometryInstance3D.SHADOW_CASTING_SETTING_OFF
	goal_sensor = Area3D.new()
	goal_sensor.name = "ReceiverSensor"
	goal_sensor.collision_layer = 0
	goal_sensor.collision_mask = LabConfig.BODY_LAYER
	add_child(goal_sensor)
	goal_sensor.position = target
	LabGeometry.box_shape(goal_sensor, Vector3(LabConfig.CATCH_WIDTH, LabConfig.CATCH_HEIGHT, LabConfig.CATCH_DEPTH))

func step(delta: float) -> void:
	if not active or not outcome.is_empty():
		return
	var started := Time.get_ticks_usec()
	elapsed += delta
	var position_now := ball.position
	minimum_ball_y = minf(minimum_ball_y, position_now.y)
	maximum_ball_y = maxf(maximum_ball_y, position_now.y)
	maximum_ball_x = maxf(maximum_ball_x, position_now.x)
	if not position_now.is_finite() or position_now.y < LabConfig.KILL_Y:
		outcome = "Ball lost"
	elif position_now.distance_squared_to(Vector3.ZERO) > 1000000.0:
		outcome = "Ball escaped"
	else:
		_contact_feedback()
		_contain_bodies()
		if machine:
			machine.step(delta)
		mechanisms.step(delta)
		var local_ball := goal_sensor.to_local(ball.global_position).abs()
		var half := Vector3(LabConfig.CATCH_WIDTH, LabConfig.CATCH_HEIGHT, LabConfig.CATCH_DEPTH) * 0.5
		if local_ball.x <= half.x and local_ball.y <= half.y and local_ball.z <= half.z:
			goal_elapsed += delta
			# The receiver has a deadening liner, represented by drag below the rim.
			if local_ball.y < 0.45:
				ball.apply_central_force(-ball.linear_velocity * ball.mass * 9.0)
			if goal_elapsed + 0.00001 >= LabConfig.GOAL_HOLD:
				outcome = "Complete"
		else:
			goal_elapsed = 0.0
		if elapsed + 0.00001 >= LabConfig.TIME_LIMIT and outcome.is_empty():
			outcome = "Too slow"
	measured_step_ms = lerpf(measured_step_ms, float(Time.get_ticks_usec() - started) / 1000.0, 0.08)

func _contain_bodies() -> void:
	for body in bodies:
		if body == ball or body.freeze:
			continue
		var point := body.position
		if not point.is_finite() or point.y < LabConfig.KILL_Y or point.length_squared() > 1000000.0:
			body.freeze = true
			body.hide()
			body.collision_layer = 0
			body.collision_mask = 0
			if not point.is_finite():
				body.position = Vector3(0, LabConfig.KILL_Y - 1.0, 0)

func _contact_feedback() -> void:
	var contact := ball.get_contact_count() > 0
	if contact and not was_contact:
		var strength := (ball.linear_velocity - last_ball_velocity).length()
		if strength > 0.5:
			feedback.emit(level.ball, ball.position, strength)
	was_contact = contact
	last_ball_velocity = ball.linear_velocity

func live_bodies() -> int:
	var count := 0
	for body in bodies:
		if not body.sleeping and not body.freeze:
			count += 1
	return count
