extends Node3D

const C = preload("res://scripts/config.gd")
var board: LabBoard
var phase := 0
var minimum_y := INF
var maximum_x := -INF
var peak_speed := 0.0
var stretch_at_rest := 0.0
var rest_bob := Vector3.ZERO
var rest_motion := 0.0
var verified := 0
var failed := 0

func _ready() -> void:
	_check(Engine.physics_ticks_per_second == C.TICK_HZ
		and ProjectSettings.get_setting("physics/3d/physics_engine") == "Jolt Physics"
		and is_equal_approx(ProjectSettings.get_setting("physics/3d/default_gravity"), C.GRAVITY),
		"Jolt, 120 ticks and gravity 9.81")
	_setup()

func _setup() -> void:
	if is_instance_valid(board):
		remove_child(board)
		board.queue_free()
	if phase >= 8:
		print("MECHANISMS ", verified, " passed, ", failed, " failed")
		get_tree().quit(failed)
		return
	var level: Dictionary = C.LEVELS[0].duplicate()
	level.fixtures = [[Vector2(0, -0.25), Vector2(27, 0.5), 0.0]]
	var part: Dictionary
	var supplemental: Array = []
	match phase:
		0:
			level.start = Vector2(-8, 0.25)
			part = C.placement("rope", Vector2(-4, 14), 0, {"rope_vector": Vector2(6, 0)})
		1:
			level.start = Vector2(0, 3.15)
			part = C.placement("cloth", Vector2(0, 2), 0)
		2:
			level.start = Vector2(-0.75, 0.7)
			part = C.placement("conveyor", Vector2(0, 0.22), 0)
		3:
			level.start = Vector2(0, 0.25)
			part = C.placement("fan", Vector2(-0.75, 0.6), 0)
		4:
			level.start = Vector2(-4, 8.35)
			part = C.placement("rope", Vector2(-4, 14), 0, {"rope_vector": Vector2(0, -6)})
		5:
			level.start = Vector2(0, 1.3)
			part = C.placement("spring", Vector2(0, 0.12), 0)
		6:
			level = C.LEVELS[4]
		7:
			level.start = Vector2(-7, 0.25)
			part = C.placement("spring", Vector2(0, 0.12), 0)
			supplemental.append(C.placement("box_s", Vector2(0, 1.18), 45.0))
	board = LabBoard.new()
	add_child(board)
	var parts: Array = [] if phase == 6 else [part]
	parts.append_array(supplemental)
	board.assemble(level, parts, true)
	minimum_y = INF
	maximum_x = -INF
	peak_speed = 0.0

func _physics_process(delta: float) -> void:
	if board == null or phase >= 8:
		return
	board.step(delta)
	minimum_y = minf(minimum_y, board.ball.position.y)
	maximum_x = maxf(maximum_x, board.ball.position.x)
	if phase == 7:
		peak_speed = maxf(peak_speed, board.bodies[1].linear_velocity.y)
	else:
		peak_speed = maxf(peak_speed, board.ball.linear_velocity.x if phase != 5 else board.ball.linear_velocity.y)
	if phase == 0 and board.elapsed > 12.0:
		var rope: LabRope = board.pieces[0]
		_check(rope.segments.size() == int(C.ROPE_MAX / C.ROPE_SEGMENT)
			and rope.joints.size() == rope.segments.size() + 1
			and board.max_rope_stretch < 0.05,
			"Six-metre swinging rope: %d capsules, %.2f%% maximum stretch" % [rope.segments.size(), board.max_rope_stretch * 100.0])
		_next()
	elif phase == 1 and board.elapsed > 4.0:
		var hammock: LabHammock = board.pieces[0]
		var soft: SoftBody3D = hammock.cloth
		var pins := 0
		for index in range(C.CLOTH_VERTICES * C.CLOTH_VERTICES):
			if soft.is_point_pinned(index):
				pins += 1
		var sag: float = 2.0 - (minimum_y - board.ballspec.radius)
		_check(pins == 4 and absf(sag - C.CLOTH_TARGET_SAG) <= 0.08 and board.ball.position.is_finite()
			and board.ball.position.y > minimum_y + 0.20,
			"Ten-by-ten soft mesh: %d pins, %.3f m peak loaded sag, ball rebounded" % [pins, sag])
		_next()
	elif phase == 2 and board.elapsed > 2.0:
		var belt: StaticBody3D = board.pieces[0]
		_check(is_equal_approx(belt.constant_linear_velocity.x, C.CONVEYOR_SPEED) and maximum_x > 0.2,
			"Conveyor surface 2 m/s moved the resting ball by %.2f m" % (maximum_x + 0.75))
		_next()
	elif phase == 3 and board.elapsed > 2.0:
		_check(maximum_x > C.FAN_REACH and peak_speed > 4.0, "Fan moved a 1 kg ball %.2f m" % maximum_x)
		_next()
	elif phase == 4:
		var rope: LabRope = board.pieces[0]
		if board.elapsed >= 3.0 and rest_bob.is_zero_approx():
			rest_bob = rope.bob.position
		if not rest_bob.is_zero_approx():
			rest_motion = maxf(rest_motion, rope.bob.position.distance_to(rest_bob))
		stretch_at_rest = maxf(stretch_at_rest, board.max_rope_stretch)
		if board.elapsed > 10.0:
			_check(board.ball.position.y > 8.0 and rest_motion < 0.025 and stretch_at_rest < 0.05,
				"Rope cradle held the ball; resting bob drift %.4f m, maximum stretch %.2f%%" % [rest_motion, stretch_at_rest * 100.0])
			_next()
	elif phase == 5 and board.elapsed > 1.10:
		_check(peak_speed > 7.0, "Spring launched a falling steel ball upward to %.2f m/s" % peak_speed)
		_next()
	elif phase == 6 and not board.outcome.is_empty():
		_check(board.machine.opened_at < 0.0 and board.outcome != "Complete",
			"The Machine gate stayed closed and blocked the ball without a crate")
		_next()
	elif phase == 7 and board.elapsed > 1.1:
		_check(peak_speed > 10.0, "A rotated crate launched from its actual corner contact at %.2f m/s" % peak_speed)
		_next()

func _check(condition: bool, description: String) -> void:
	if condition:
		verified += 1
	else:
		failed += 1
	print("  ", "PASS" if condition else "FAIL", "  ", description)

func _next() -> void:
	phase += 1
	set_physics_process(false)
	call_deferred("_advance")

func _advance() -> void:
	_setup()
	set_physics_process(true)
