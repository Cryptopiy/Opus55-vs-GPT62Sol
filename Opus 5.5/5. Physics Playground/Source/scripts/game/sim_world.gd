extends Node3D
## Simulation world, created at every Play and freed on Reset.
## Determinism (P-02): the level session swaps in a fresh World3D before building this node, then bodies are
## created in a fixed order (ball, level machinery, player objects sorted canonically). All gameplay logic
## runs here once per physics tick, before the physics step, in a fixed order, using the fixed 1/120 s step.

signal succeeded(time: float)
signal failed(reason: String)
signal impact(kind: StringName, pos: Vector3, dv: float, ball_hit: bool)

const C := preload("res://scripts/core/config.gd")
const Common := preload("res://scripts/objects/obj_common.gd")
const Registry := preload("res://scripts/objects/registry.gd")
const Ball := preload("res://scripts/objects/ball.gd")
const Machinery := preload("res://scripts/objects/machinery.gd")
const GoalCup := preload("res://scripts/objects/goal_cup.gd")

var dynamic_bodies: Array[RigidBody3D] = []
var cloths: Array[SoftBody3D] = []
var conveyor_count := 0
var fan_count := 0
var ball: RigidBody3D
var ball_type := ""
var goal: GoalCup
var steps := 0                 # physics steps executed so far (state time = steps * PHYS_DT)
var running := false           # physics server stepping
var finished := false
var success_time := -1.0
var fail_reason := ""
var body_count := 0            # all physics bodies of the run (incl. static scenery), for the F1 overlay

var _tickers: Array[Node] = []
var _kinds: Array[StringName] = []
var _radii := PackedFloat32Array()
var _prev_vel := PackedVector3Array()
var _impact_cd := PackedFloat32Array()
var _skip_impact := PackedByteArray()
var _want_running := false
var _hold := 0.0
var _hold_start := 0.0


func _init() -> void:
	name = "Sim"
	# run before every other physics callback so all sim logic of a tick happens in one fixed order
	process_physics_priority = -100


func build(level: Dictionary, records: Array, goal_node: GoalCup, static_bodies: int) -> void:
	goal = goal_node
	ball_type = level["ball"]
	ball = Ball.build_sim(ball_type, level["ball_pos"], self)
	Machinery.build_sim(level, self)
	var sorted := records.duplicate()
	sorted.sort_custom(func(a: Dictionary, b: Dictionary) -> bool:
		return Common.sort_key(a, Registry.type_index(a["type"])) < Common.sort_key(b, Registry.type_index(b["type"])))
	for rec: Dictionary in sorted:
		Registry.module(String(rec["type"])).build_sim(rec, self)
	body_count = static_bodies + find_children("*", "CollisionObject3D", true, false).size() + cloths.size()


func add_node(n: Node) -> void:
	add_child(n)


func add_dynamic(b: RigidBody3D, kind: StringName, radius: float) -> void:
	dynamic_bodies.append(b)
	_kinds.append(kind)
	_radii.append(radius)
	_prev_vel.append(Vector3.ZERO)
	_impact_cd.append(0.0)
	_skip_impact.append(0)


func add_ticker(t: Node) -> void:
	_tickers.append(t)


## Bodies launched by a spring this tick: their big velocity change is not an "impact".
func mark_launched(b: RigidBody3D) -> void:
	var i := dynamic_bodies.find(b)
	if i >= 0:
		_skip_impact[i] = 1


func kind_of(b: RigidBody3D) -> StringName:
	var i := dynamic_bodies.find(b)
	return _kinds[i] if i >= 0 else &""


func sim_time() -> float:
	return float(steps) * C.PHYS_DT


## Requested state is applied at the start of the next physics tick, so a tick is never half-simulated.
func set_running(on: bool) -> void:
	_want_running = on


func stop_now() -> void:
	_want_running = false
	running = false
	PhysicsServer3D.set_active(false)


## Removes every simulation node in exact reverse creation order (LIFO). Jolt then hands out the same body IDs,
## in the same order, at the next Play, so a persistent physics space replays bit-identically.
func teardown() -> void:
	for i in range(get_child_count() - 1, -1, -1):
		var c := get_child(i)
		remove_child(c)
		c.queue_free()


func _physics_process(_delta: float) -> void:
	if _want_running != running:
		running = _want_running
		PhysicsServer3D.set_active(running)
	if not running:
		return
	if not finished:
		_evaluate()
		# a success / failure listener may have reset the level (removing this node) synchronously
		if not is_inside_tree():
			return
	for t in _tickers:
		t.call(&"sim_tick", self)
	steps += 1


func _evaluate() -> void:
	var t := sim_time()
	for i in dynamic_bodies.size():
		var b := dynamic_bodies[i]
		if b.freeze:
			continue
		var p := b.global_position
		# P-03 kill plane / P-28 safety net: NaN or runaway bodies are frozen and hidden, never left to explode
		if not p.is_finite() or p.length() > C.ESCAPE_LIMIT or p.y < C.KILL_Y:
			if b == ball:
				_fail("Ball lost" if p.y < C.KILL_Y else "Ball escaped")
				return
			b.freeze = true
			b.visible = false
			continue
		var v := b.linear_velocity
		var dv_vec := v - _prev_vel[i]
		_prev_vel[i] = v
		_impact_cd[i] -= C.PHYS_DT
		if _skip_impact[i] == 1:
			_skip_impact[i] = 0
			continue
		var dv := dv_vec.length()
		if dv > C.IMPACT_MIN_DV and _impact_cd[i] <= 0.0:
			_impact_cd[i] = C.IMPACT_COOLDOWN
			var contact := p - dv_vec.normalized() * _radii[i]
			impact.emit(_kinds[i], contact, dv, b == ball)
	# P-13: ball centre inside the sensor continuously for 1.0 s
	if goal.contains_ball(ball):
		if _hold <= 0.0:
			_hold_start = t
		_hold += C.PHYS_DT
		if _hold >= C.GOAL_HOLD_TIME - 0.0001:
			finished = true
			success_time = _hold_start
			goal.set_progress(1.0)
			succeeded.emit(success_time)
			return
	else:
		_hold = 0.0
	goal.set_progress(_hold / C.GOAL_HOLD_TIME)
	if t >= C.SIM_TIMEOUT:
		_fail("Too slow")


func _fail(reason: String) -> void:
	finished = true
	fail_reason = reason
	goal.set_progress(0.0)
	failed.emit(reason)


## Largest rope stretch measured so far in this run (P-08 evidence for the F1 overlay / dev tools).
func rope_stretch() -> float:
	var m := 0.0
	for tk in _tickers:
		var v: Variant = tk.get(&"max_stretch")
		if v != null:
			m = maxf(m, float(v))
	return m


func ball_position() -> Vector3:
	return ball.global_position if is_instance_valid(ball) else Vector3.ZERO
