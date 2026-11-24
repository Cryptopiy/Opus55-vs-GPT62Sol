extends Node3D

const C = preload("res://scripts/config.gd")
var board: LabBoard
var studio: LabStudio
var camera: LabViewRig
var workbench: LabWorkbench
var audio: LabSound
var effects: LabEffects
var hud: LabHud
var progress := LabProgress.new()
var history := LabBlueprint.new()
var level_index := 0
var placements: Array = []
var mode := "build"
var rate := 1
var level_age := 0.0
var suspended := false
var screenshot_busy := false
var dev_report := ""
var job := ""
var original_layout: Array = []
var repeat_layout: Array = []
var run_positions := PackedVector3Array()
var run_outcomes := PackedStringArray()
var stack_motion := 0.0
var stack_home := PackedVector3Array()
var stack_settle := 0.0

func _ready() -> void:
	process_mode = Node.PROCESS_MODE_ALWAYS
	Engine.max_fps = 0
	progress.restore()
	studio = LabStudio.new()
	add_child(studio)
	camera = LabViewRig.new()
	add_child(camera)
	audio = LabSound.new()
	add_child(audio)
	effects = LabEffects.new()
	add_child(effects)
	workbench = LabWorkbench.new()
	add_child(workbench)
	workbench.setup(self, camera)
	hud = LabHud.new()
	add_child(hud)
	hud.setup(self)
	load_level(0)

func load_level(index: int) -> void:
	level_index = index
	placements = []
	level_age = 0
	history.clear()
	workbench.new_level()
	dev_report = ""
	var level: Dictionary = C.LEVELS[index]
	camera.frame(level.frame)
	studio.decorate(level)
	hud.build_inventory(level)
	reset_attempt()
	hud.tutorial.visible = index == 0 and not progress.tutorial_seen

func rebuild(simulate: bool, override_level: Dictionary = {}, override_parts: Array = []) -> void:
	if is_instance_valid(board):
		audio.loop_devices(board, false)
		remove_child(board)
		board.queue_free()
	board = LabBoard.new()
	board.process_mode = Node.PROCESS_MODE_PAUSABLE
	add_child(board)
	var data: Dictionary = C.LEVELS[level_index] if override_level.is_empty() else override_level
	var parts: Array = placements if override_level.is_empty() else override_parts
	board.assemble(data, parts, simulate)
	board.feedback.connect(_feedback)
	effects.clear()
	effects.board = board
	audio.loop_devices(board, simulate and job.is_empty())

func reset_attempt() -> void:
	if mode == "test":
		return
	suspended = false
	hud.modal.close()
	mode = "build"
	camera.win_target = false
	get_tree().paused = false
	workbench.clear()
	rebuild(false)
	refresh_ui()

func toggle_play() -> void:
	if suspended or mode == "test":
		return
	match mode:
		"build":
			workbench.clear()
			workbench.hint.hide()
			dismiss_tutorial()
			mode = "run"
			rebuild(true)
		"run":
			mode = "paused"
			audio.loop_devices(board, false)
		"paused":
			mode = "run"
			audio.loop_devices(board, true)
		"result":
			reset_attempt()
	get_tree().paused = mode == "paused"
	refresh_ui()

func _process(delta: float) -> void:
	if not suspended and mode != "test":
		level_age += delta / float(C.TIME_SCALES[rate])

func _physics_process(delta: float) -> void:
	if suspended or mode not in ["run", "test"]:
		return
	board.step(delta)
	if job == "stack":
		_stack_step()
		return
	if board.outcome.is_empty():
		return
	for body in board.bodies:
		body.freeze = true
	if job == "repeat":
		run_positions.append(board.ball.position)
		run_outcomes.append(board.outcome)
		mode = "pending"
		call_deferred("_continue_determinism")
		return
	mode = "result"
	audio.loop_devices(board, false)
	var complete := board.outcome == "Complete"
	var earned := C.stars_for(board.level, placements.size(), board.elapsed) if complete else 0
	if complete:
		progress.accept(level_index, earned)
		effects.feedback("win", board.goal_sensor.position, 1)
		camera.win_target = true
	audio.play("win" if complete else "fail", 1)
	hud.modal.result(complete, earned)
	refresh_ui()

func commit(updated: Array, cue: String) -> void:
	history.record(placements)
	placements = updated
	rebuild(false)
	audio.play(cue, 1)
	refresh_ui()

func remaining(kind: String) -> int:
	var count: int = int(C.LEVELS[level_index].kit.get(kind, 0))
	for piece in placements:
		if piece.type == kind:
			count -= 1
	return count

func refresh_ui() -> void:
	hud.refresh()

func _toggle_gameplay_ui() -> void:
	# Hide whole branches so their descendants retain their own visibility state.
	var shown := not hud.root.visible
	hud.root.visible = shown
	studio.labels.visible = shown
	workbench.hint_overlay.visible = shown

func dismiss_tutorial() -> void:
	hud.tutorial.hide()
	if not progress.tutorial_seen and level_index == 0:
		progress.tutorial_seen = true
		progress.save()

func show_hint() -> void:
	if mode == "build" and level_age >= C.HINT_DELAY:
		workbench.cycle_hint()

func load_reference() -> void:
	if mode == "test" or mode == "pending":
		return
	placements = LabBlueprint.read_reference(C.LEVELS[level_index])
	history.clear()
	reset_attempt()
	workbench.hint.hide()
	dismiss_tutorial()

func set_rate(value: int) -> void:
	rate = clampi(value, 0, C.TIME_SCALES.size() - 1)
	var multiplier: float = C.TIME_SCALES[rate]
	# Scale the tick cadence with the clock so every solver step still advances 1/120 second.
	Engine.physics_ticks_per_second = roundi(C.TICK_HZ * multiplier)
	Engine.time_scale = multiplier
	refresh_ui()

func open_levels() -> void:
	if job.is_empty():
		suspended = true
		get_tree().paused = true
		audio.loop_devices(board, false)
		hud.modal.levels()

func choose_level(index: int) -> void:
	if progress.unlocked(index):
		load_level(index)

func close_overlay() -> void:
	hud.modal.close()
	suspended = false
	get_tree().paused = mode == "paused"
	if mode == "run":
		audio.loop_devices(board, true)
	elif mode == "result":
		var success := board.outcome == "Complete"
		hud.modal.result(success, C.stars_for(board.level, placements.size(), board.elapsed) if success else 0)

func quit_game() -> void:
	get_tree().quit()

func _feedback(event: String, point: Vector3, strength: float) -> void:
	if not job.is_empty():
		return
	audio.play(event, strength)
	effects.feedback(event, point, strength)
	if event == "heavy":
		camera.kick = minf(0.17, strength * 0.010)

func _history_step(redo: bool) -> void:
	if mode != "build":
		return
	workbench.clear()
	placements = history.redo(placements) if redo else history.undo(placements)
	rebuild(false)
	refresh_ui()

func _input(event: InputEvent) -> void:
	if event is InputEventKey and event.pressed and not event.echo:
		if _key(event):
			get_viewport().set_input_as_handled()
			return

func _unhandled_input(event: InputEvent) -> void:
	if not suspended and not hud.modal.visible:
		if camera.pointer(event):
			if mode == "build":
				workbench.update_cursor(get_viewport().get_mouse_position())
			get_viewport().set_input_as_handled()
		elif workbench.handle(event):
			get_viewport().set_input_as_handled()

func _key(event: InputEventKey) -> bool:
	match event.keycode:
		KEY_F1:
			hud.debug_panel.visible = not hud.debug_panel.visible
			refresh_ui()
		KEY_F8:
			_toggle_gameplay_ui()
		KEY_F12:
			_capture()
		KEY_ESCAPE:
			if not job.is_empty():
				return true
			if suspended:
				close_overlay()
			else:
				suspended = true
				get_tree().paused = true
				audio.loop_devices(board, false)
				hud.modal.pause_menu()
		KEY_F9:
			load_reference()
		KEY_F10:
			if job.is_empty():
				suspended = true
				get_tree().paused = true
				audio.loop_devices(board, false)
				hud.modal.developer()
		_:
			if suspended:
				return false
			match event.keycode:
				KEY_SPACE:
					toggle_play()
				KEY_R:
					reset_attempt()
				KEY_F:
					camera.frame(board.level.frame)
				KEY_BRACKETLEFT:
					if job.is_empty():
						set_rate(rate - 1)
				KEY_BRACKETRIGHT:
					if job.is_empty():
						set_rate(rate + 1)
				KEY_Z, KEY_Y:
					if not event.ctrl_pressed:
						return false
					_history_step(event.keycode == KEY_Y)
				_:
					if event.keycode < KEY_1 or event.keycode > KEY_9 or mode != "build":
						return false
					var kinds := C.KIT.keys()
					workbench.choose(kinds[event.keycode - KEY_1])
	return true

func _capture() -> void:
	if screenshot_busy or DisplayServer.get_name() == "headless":
		return
	screenshot_busy = true
	await RenderingServer.frame_post_draw
	var image := get_viewport().get_texture().get_image()
	var directory := ProjectSettings.globalize_path(C.SCREENSHOT_PATH)
	var error := DirAccess.make_dir_recursive_absolute(directory)
	if error == OK:
		var stamp := Time.get_datetime_string_from_system().replace(":", "-")
		var path := directory.path_join("Contraption-Lab_%s_%03d.png" % [stamp, Time.get_ticks_msec() % 1000])
		if image.save_png(path) == OK:
			print("Screenshot saved ", path)
	screenshot_busy = false

func start_determinism() -> void:
	if not job.is_empty():
		return
	repeat_layout = LabBlueprint.read_reference(C.LEVELS[level_index])
	original_layout = placements
	run_positions.clear()
	run_outcomes.clear()
	job = "repeat"
	_continue_determinism()

func _continue_determinism() -> void:
	if run_positions.size() >= C.REPEAT_RUNS:
		var spread := 0.0
		var same := true
		for first in range(run_positions.size()):
			same = same and run_outcomes[first] == "Complete"
			for second in range(first):
				spread = maxf(spread, run_positions[first].distance_to(run_positions[second]))
		var pass_test := same and spread <= C.REPEAT_TOLERANCE
		_finish_test("%s  ·  %d runs  ·  %.4f m maximum spread" % ["PASS" if pass_test else "FAIL", C.REPEAT_RUNS, spread])
		return
	mode = "test"
	suspended = false
	get_tree().paused = false
	placements = repeat_layout
	dev_report = "Reference run %d of %d…" % [run_positions.size() + 1, C.REPEAT_RUNS]
	hud.modal.update_report(dev_report)
	rebuild(true)

func start_stack() -> void:
	if not job.is_empty():
		return
	job = "stack"
	original_layout = placements
	stack_home.clear()
	stack_motion = 0.0
	mode = "test"
	suspended = false
	get_tree().paused = false
	dev_report = "Measuring a three-box stack…"
	hud.modal.update_report(dev_report)
	var test_level: Dictionary = C.LEVELS[2].duplicate()
	test_level.start = Vector2(-10, 0.3)
	var parts := []
	for i in range(3):
		parts.append(C.placement("box_s", Vector2(0, 0.505 + i * 1.005), 0.0))
	rebuild(true, test_level, parts)

func _stack_step() -> void:
	if stack_home.is_empty() and board.elapsed > 0.80:
		stack_settle = board.elapsed
		for i in range(1, board.bodies.size()):
			stack_home.append(board.bodies[i].position)
	if not stack_home.is_empty():
		for i in range(stack_home.size()):
			var point := board.bodies[1 + i].position
			stack_motion = maxf(stack_motion, point.distance_to(stack_home[i]))
		if board.elapsed - stack_settle >= C.STACK_DURATION:
			mode = "pending"
			call_deferred("_finish_test", "%s  ·  3 boxes  ·  %.4f m drift over %.0f s" % [
				"PASS" if stack_motion <= C.REPEAT_TOLERANCE else "FAIL", stack_motion, C.STACK_DURATION])

func _finish_test(result_text: String) -> void:
	job = ""
	mode = "build"
	placements = original_layout
	suspended = true
	rebuild(false)
	get_tree().paused = true
	dev_report = result_text
	hud.modal.update_report(dev_report)
	print("DEV ", C.LEVELS[level_index].id, " ", dev_report)
	refresh_ui()
