extends Node3D
## One open level: builds the 3D world, owns the placement model, editor, simulation, HUD and feedback.
## States: BUILD (editing, physics off) -> RUNNING <-> PAUSED -> SUCCESS / FAILED; Reset returns to BUILD (P-17).

signal exit_requested
signal next_requested(index: int)
signal run_finished(success: bool, time: float, ball_pos: Vector3)

enum State { BUILD, RUNNING, PAUSED, SUCCESS, FAILED }

const C := preload("res://scripts/core/config.gd")
const EnvB := preload("res://scripts/game/environment_builder.gd")
const LevelBuilder := preload("res://scripts/game/level_builder.gd")
const CameraRig := preload("res://scripts/game/camera_rig.gd")
const BuildEditor := preload("res://scripts/game/build_editor.gd")
const SimWorld := preload("res://scripts/game/sim_world.gd")
const Vfx := preload("res://scripts/game/vfx.gd")
const PlacementModel := preload("res://scripts/core/placement_model.gd")
const Registry := preload("res://scripts/objects/registry.gd")
const GoalCup := preload("res://scripts/objects/goal_cup.gd")
const Hud := preload("res://scripts/ui/hud.gd")

var level_index := 0
var level := {}
var state := State.BUILD
var model := PlacementModel.new()
var camera_rig: CameraRig
var editor: BuildEditor
var sim: SimWorld
var vfx: Vfx
var hud: Hud
var goal: GoalCup
var solution: Array = []
var time_scale_index := C.DEFAULT_TIME_SCALE_INDEX
var turbo := false
var level_time := 0.0          # real seconds spent in this level (hint unlock, P-19)
var hints_shown := 0
var menu_open := false
var last_time := 0.0
var last_stars := 0
var static_body_count := 0
var save_results := true       # dev tests (F10) do not write progress
var hold_world_release := false # set by tools that step physics outside the normal simulation (stack test, labs)

var _env: Environment
var _static_root: Node3D
var _preview_root: Node3D
var _results_timer := -1.0
var _resume_after_menu := false
var _calm_since_ms := -1

# shared by all sessions so worlds retired by a previous level are also released calmly
static var _retired: Array[World3D] = []


func setup(index: int, ui_parent: Control, screens_parent: Control) -> void:
	level_index = index
	level = C.LEVELS[index]
	name = "Level_%s" % level["id"]
	_env = EnvB.make_environment()
	swap_world()
	EnvB.add_light(self)
	camera_rig = CameraRig.new()
	camera_rig.name = "CameraRig"
	add_child(camera_rig)
	camera_rig.set_bounds(level["bounds"])
	_static_root = Node3D.new()
	_static_root.name = "Static"
	add_child(_static_root)
	LevelBuilder.build_decor(level, _static_root)
	goal = LevelBuilder.build_static(level, _static_root)
	static_body_count = _static_root.find_children("*", "CollisionObject3D", true, false).size()
	_preview_root = Node3D.new()
	_preview_root.name = "Preview"
	add_child(_preview_root)
	LevelBuilder.build_preview(level, _preview_root)
	model.setup(level["inventory"])
	editor = BuildEditor.new()
	editor.name = "Editor"
	add_child(editor)
	editor.setup(model, camera_rig, level, LevelBuilder.obstacles(level), LevelBuilder.forbidden(level))
	editor.message.connect(func(t: String) -> void: hud.flash(t))
	vfx = Vfx.new()
	vfx.name = "Vfx"
	add_child(vfx)
	solution = load_solution_records(String(level["id"]))
	hud = Hud.new()
	ui_parent.add_child(hud)
	hud.setup(self, screens_parent)
	camera_rig.safe_rect = hud.safe_rect()
	camera_rig.frame(true)
	_apply_time_scale()
	PhysicsServer3D.set_active(false)


func _exit_tree() -> void:
	if is_instance_valid(hud):
		hud.queue_free()
	Sfx.stop_loops()
	Engine.time_scale = 1.0
	Engine.physics_ticks_per_second = C.PHYS_HZ
	Engine.max_physics_steps_per_frame = C.NORMAL_MAX_STEPS_PER_FRAME


## Fresh World3D => fresh physics space: every body gets the same Jolt body ID on every run (P-02).
## The replaced world is retired, not freed: destroying a Jolt space right before stepping again intermittently
## stalls the engine's physics job pool (engine warning). Retired worlds (~0.3 MB each, spaces inactive) are
## freed only after the level has been calmly in build mode for a while (see _release_retired_worlds).
func swap_world() -> void:
	var vp := get_viewport()
	var old := vp.world_3d
	vp.world_3d = EnvB.make_world(_env)
	if old:
		PhysicsServer3D.space_set_active(old.space, false)
		_retired.append(old)
	_calm_since_ms = -1


## Frees retired worlds once the session has been in build mode (physics off) for RETIRE_CALM_TIME seconds.
func _release_retired_worlds() -> void:
	if state != State.BUILD or menu_open or hold_world_release or _retired.is_empty():
		_calm_since_ms = -1
		return
	var now := Time.get_ticks_msec()
	if _calm_since_ms < 0:
		_calm_since_ms = now
	elif now - _calm_since_ms >= int(C.RETIRE_CALM_TIME * 1000.0):
		_retired.clear()
		_calm_since_ms = -1


## True while retired worlds are still waiting to be freed (tests wait for this before the next fast run).
func has_retired_worlds() -> bool:
	return not _retired.is_empty()


static func load_solution_records(level_id: String) -> Array:
	var path := "res://levels/solutions/%s.json" % level_id
	if not FileAccess.file_exists(path):
		return []
	var data: Variant = JSON.parse_string(FileAccess.get_file_as_string(path))
	if typeof(data) != TYPE_DICTIONARY:
		push_error("Bad solution file " + path)
		return []
	var out: Array = []
	for o: Dictionary in data["objects"]:
		var rec := Registry.new_record(String(o["type"]), Vector2(float(o["x"]), float(o["y"])), float(o.get("rot", 0.0)))
		for key: String in ["length", "fulcrum"]:
			if o.has(key):
				rec[key] = float(o[key])
		Registry.module(String(rec["type"])).constrain(rec)
		out.append(rec)
	return out


# ------------------------------------------------------------------ simulation control (P-17)
func toggle_play() -> void:
	match state:
		State.BUILD:
			_start_sim()
		State.RUNNING:
			sim.set_running(false)
			state = State.PAUSED
			_update_loops()
		State.PAUSED:
			sim.set_running(true)
			state = State.RUNNING
			_update_loops()


func _start_sim() -> void:
	editor.set_enabled(false)
	_preview_root.visible = false
	swap_world()
	sim = SimWorld.new()
	add_child(sim)
	# Jolt bakes a soft body's stiffness from the *current* ticks-per-second when it is created, so objects are
	# always built at the nominal 120 Hz; the speed setting (which changes ticks/s, never the step) is restored after.
	Engine.physics_ticks_per_second = C.PHYS_HZ
	sim.build(level, model.records, goal, static_body_count)
	_apply_time_scale()
	sim.succeeded.connect(_on_success)
	sim.failed.connect(_on_fail)
	sim.impact.connect(_on_impact)
	vfx.attach_ball(sim.ball, String(level["ball"]))
	sim.set_running(true)
	state = State.RUNNING
	_update_loops()
	hud.on_state_changed()


func reset() -> void:
	if state == State.BUILD:
		return
	sim.stop_now()
	sim.teardown()
	remove_child(sim)
	sim.queue_free()
	sim = null
	state = State.BUILD
	_results_timer = -1.0
	editor.set_enabled(true)
	_preview_root.visible = true
	goal.set_progress(0.0)
	vfx.clear()
	_update_loops()
	hud.on_state_changed()


func set_time_scale_index(i: int) -> void:
	time_scale_index = clampi(i, 0, C.TIME_SCALES.size() - 1)
	_apply_time_scale()


## The physics step stays exactly 1/120 s: a faster speed runs more ticks per second instead (P-02).
func _apply_time_scale() -> void:
	var s := C.TURBO_SCALE if turbo else C.TIME_SCALES[time_scale_index]
	Engine.time_scale = s
	Engine.physics_ticks_per_second = int(round(C.PHYS_HZ * s))
	Engine.max_physics_steps_per_frame = C.TURBO_MAX_STEPS_PER_FRAME if turbo else C.NORMAL_MAX_STEPS_PER_FRAME


func set_turbo(on: bool) -> void:
	turbo = on
	_apply_time_scale()


func current_time_scale() -> float:
	return C.TURBO_SCALE if turbo else C.TIME_SCALES[time_scale_index]


## F9: replace the player's placements with the reference solution (undoable as one step).
func load_solution() -> void:
	if solution.is_empty():
		hud.flash("No reference solution for this level")
		return
	reset()
	model.replace_all(solution)
	hud.flash("Reference solution loaded - press Space")


## Pause-menu Restart: clean level (placements cleared, undoable), camera re-framed.
func restart_level() -> void:
	reset()
	model.replace_all([])
	camera_rig.frame(false)


func set_menu_open(open: bool) -> void:
	menu_open = open
	editor.enabled = not open and state == State.BUILD
	if state == State.RUNNING and open:
		_resume_after_menu = true
		sim.set_running(false)
	elif not open and _resume_after_menu:
		_resume_after_menu = false
		if state == State.RUNNING:
			sim.set_running(true)
	_update_loops()


func request_exit() -> void:
	exit_requested.emit()


func request_next() -> void:
	next_requested.emit(level_index + 1)


# ------------------------------------------------------------------ results
func stars_for(objects: int, time: float) -> int:
	if objects > int(level["par"]):
		return 1
	return 3 if time <= float(level["par_time"]) + 0.0001 else 2


## Stars the player would get if the level were completed now (HUD preview, P-20).
func preview_stars() -> int:
	var t := sim.sim_time() if sim else 0.0
	if state == State.SUCCESS:
		t = last_time
	return stars_for(model.count(), t)


func _on_success(t: float) -> void:
	state = State.SUCCESS
	last_time = t
	last_stars = stars_for(model.count(), t)
	if save_results:
		Save.record_result(String(level["id"]), last_stars, t)
	vfx.confetti(goal.hole)
	camera_rig.success_zoom(goal.hole)
	Sfx.play(&"chime", 0.0, 1.0)
	_results_timer = C.RESULTS_DELAY
	_update_loops()
	hud.on_state_changed()
	run_finished.emit(true, t, sim.ball_position())


func _on_fail(reason: String) -> void:
	state = State.FAILED
	sim.set_running(false)
	Sfx.play(&"fail", -2.0, 1.0)
	_update_loops()
	hud.on_state_changed()
	hud.show_banner("%s - press R to reset" % reason, true)
	run_finished.emit(false, sim.sim_time(), sim.ball_position())


func _on_impact(kind: StringName, pos: Vector3, dv: float, ball_hit: bool) -> void:
	if turbo:
		return
	if ball_hit:
		Sfx.play_impact(String(level["ball"]), dv)
	if dv > C.DUST_MIN_DV:
		vfx.dust(pos, dv)
	if kind == &"box_h" and dv > C.SHAKE_HEAVY_MIN_DV:
		camera_rig.shake(dv * C.SHAKE_PER_DV)


func _update_loops() -> void:
	vfx.sim_running = state == State.RUNNING and not menu_open
	var on := state == State.RUNNING and not menu_open and sim != null and not turbo
	Sfx.set_loop(&"hum", on and sim.conveyor_count > 0)
	Sfx.set_loop(&"whoosh", on and sim.fan_count > 0)


# ------------------------------------------------------------------ hints (P-19)
func hint_available() -> bool:
	return level_time >= C.HINT_DELAY and hints_shown < solution.size()


func show_next_hint() -> void:
	if not hint_available():
		return
	hints_shown += 1
	editor.show_hints(solution.slice(0, hints_shown))
	hud.flash("Hint %d/%d: ghost shows one object of the reference solution" % [hints_shown, solution.size()])


func _process(delta: float) -> void:
	_release_retired_worlds()
	var real := delta / maxf(Engine.time_scale, 0.001)
	if not menu_open:
		level_time += real
	if _results_timer > 0.0:
		_results_timer -= real
		if _results_timer <= 0.0:
			if sim:
				sim.set_running(false)
			Sfx.play(&"jingle", -2.0, 1.0)
			hud.show_results(last_stars, model.count(), last_time)


# ------------------------------------------------------------------ keys (P-17, P-23)
func _unhandled_input(event: InputEvent) -> void:
	var k := event as InputEventKey
	if not k or not k.pressed or k.echo or menu_open:
		return
	match k.keycode:
		KEY_SPACE:
			toggle_play()
		KEY_R:
			reset()
		KEY_BRACKETLEFT:
			set_time_scale_index(time_scale_index - 1)
		KEY_BRACKETRIGHT:
			set_time_scale_index(time_scale_index + 1)
		KEY_F:
			camera_rig.frame(false)
		KEY_F9:
			load_solution()
		_:
			return
	hud.on_state_changed()
	get_viewport().set_input_as_handled()
