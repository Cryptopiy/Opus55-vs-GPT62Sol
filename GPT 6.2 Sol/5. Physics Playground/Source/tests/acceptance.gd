extends Node

const C = preload("res://scripts/config.gd")
const MAIN = preload("res://main.tscn")
var app: Node3D
var errors := 0
var arguments: PackedStringArray
var fx_fps := PackedInt32Array()
var heavy_contact := false

func _ready() -> void:
	arguments = OS.get_cmdline_user_args()
	print("TEST PROFILE ", OS.get_user_data_dir())
	app = MAIN.instantiate()
	add_child(app)
	call_deferred("_run")

func _run() -> void:
	await get_tree().process_frame
	if arguments.has("persist"):
		_check(app.progress.best(0) == 3 and app.progress.unlocked(4), "A fresh process restored three stars and all unlocked boards")
	elif arguments.has("soak"):
		await _soak()
	elif arguments.has("ui-capture"):
		await _ui_capture()
	elif arguments.has("graphics"):
		await _graphics()
	else:
		await _acceptance()
	print("ACCEPTANCE ", "PASS" if errors == 0 else "FAIL " + str(errors))
	get_tree().quit(errors)

func _acceptance() -> void:
	_check(app.hud.title.is_visible_in_tree(), "Gameplay UI starts visible")
	app.open_levels()
	var tiles: HBoxContainer = app.hud.modal.stack.get_child(2)
	var states := tiles.get_child_count() == C.LEVELS.size()
	for index in range(tiles.get_child_count()):
		states = states and tiles.get_child(index).disabled != app.progress.unlocked(index)
	_check(states, "Five level tiles reflect saved unlocks")
	app.close_overlay()
	_check(C.stars_for(C.LEVELS[0], 2, 7.0) == 2 and C.stars_for(C.LEVELS[0], 3, 5.0) == 1,
		"Part par awards two stars and an over-par build awards one")
	for index in range(C.LEVELS.size()):
		_check(app.progress.unlocked(index), "Level " + str(index + 1) + " unlocked")
		app.choose_level(index)
		await get_tree().process_frame
		_key(KEY_F9)
		await get_tree().process_frame
		_check(not app.placements.is_empty(), "%s F9 loaded reference" % app.board.level.id)
		var built := []
		for part in app.placements:
			_check(LabBlueprint.valid(part, app.board.level, built), "%s %s placement legal in build mode" % [app.board.level.id, part.type])
			built.append(part)
		_key(KEY_SPACE)
		await get_tree().process_frame
		heavy_contact = false
		app.board.feedback.connect(_impact)
		_check_ball_configuration()
		await _wait_run()
		_check(app.board.outcome == "Complete", "%s Space outcome %s in %.3fs with %d parts" % [
			app.board.level.id, app.board.outcome, app.board.elapsed, app.placements.size()])
		_check(app.board.goal_elapsed + 0.00001 >= C.GOAL_HOLD and app.hud.modal.current == "result"
			and app.camera.win_target and app.effects.active_particles() > 0,
			"%s held the receiver for one second and fired win feedback" % app.board.level.id)
		if index == 2:
			_check(heavy_contact, "Counterweight registered a heavy landing for the dust and camera")
		if index == 4:
			_check(app.board.machine.opened_at >= C.SWITCH_HOLD
				and absf(app.board.machine.door.rotation_degrees.z - C.DOOR_ANGLE) < 0.1
				and is_equal_approx(app.board.machine.shuttle.position.y, C.SHUTTLE_Y),
				"Machine held the load and finished opening 90 degrees while the shuttle stayed at y=5")
		if index == 0:
			_check(app.placements.size() == 2 and app.board.elapsed < 6.0 and app.progress.best(0) == 3,
				"L1 two parts under six seconds saved three stars")
		if index in [1, 4]:
			_key(KEY_F10)
			await get_tree().process_frame
			_check(app.hud.modal.current == "dev", "F10 opened Developer tests (now %s)" % app.hud.modal.current)
			app.start_determinism()
			await _wait_job()
			_check(app.dev_report.begins_with("PASS"), "%s %s" % [app.board.level.id, app.dev_report])
			_key(KEY_ESCAPE)
			await get_tree().process_frame
	_key(KEY_F10)
	await get_tree().process_frame
	app.start_stack()
	await _wait_job()
	_check(app.dev_report.begins_with("PASS"), app.dev_report)
	_key(KEY_ESCAPE)
	await get_tree().process_frame
	await _edit()
	await _other_controls()
	await _ui_visibility()

func _ui_visibility() -> void:
	app.load_level(0)
	await get_tree().process_frame
	app.hud.tutorial.show()
	if not app.hud.debug_panel.visible:
		_key(KEY_F1)
	app.level_age = C.HINT_DELAY
	app.show_hint()
	_key(KEY_1)
	_pointer(Vector2(-9, 7.5))

	var shown := _visible_gameplay_ui()
	_check(app.hud.debug_panel.is_visible_in_tree() and app.hud.tutorial.is_visible_in_tree()
		and app.workbench.hint.is_visible_in_tree() and app.studio.labels.get_child_count() > 0,
		"F8 test starts with the overlay, tutorial, hint and world annotations visible")
	_key(KEY_F8)
	_check(_visible_gameplay_ui().is_empty(), "F8 hides every gameplay UI branch")
	_check(app.workbench.ghost.is_visible_in_tree(), "F8 preserves the placement preview")
	_key(KEY_F8)
	_check(_visible_gameplay_ui() == shown, "F8 restores the same HUD and world annotations")

	_key(KEY_F8)
	_key(KEY_F1)
	_key(KEY_F1)
	_click(Vector2(-9, 7.5))
	_key(KEY_1)
	_click(Vector2(-9, 7.5))
	_check(app.placements.size() == 1 and app.workbench.outline.is_visible_in_tree(),
		"Placement controls and selection outlines work with the UI hidden")
	_key(KEY_R)
	app.show_hint()
	_check(app.workbench.hint.visible and _visible_gameplay_ui().is_empty(),
		"UI stays hidden across restart and a newly created hint")

	_key(KEY_ESCAPE)
	_check(app.hud.modal.current == "pause" and app.hud.modal.is_visible_in_tree(),
		"Esc opens a visible pause screen with gameplay UI hidden")
	_key(KEY_F8)
	_check(get_tree().paused and app.hud.modal.is_visible_in_tree() and app.hud.title.is_visible_in_tree(),
		"F8 works during pause without closing the screen or resuming play")
	_key(KEY_F8)
	_key(KEY_ESCAPE)
	app.open_levels()
	_check(app.hud.modal.current == "levels" and app.hud.modal.is_visible_in_tree(),
		"Level select stays visible with gameplay UI hidden")
	app.choose_level(1)
	_check(app.level_index == 1 and _visible_gameplay_ui().is_empty(),
		"New inventory and world annotations stay hidden across level changes")

	_key(KEY_F9)
	_key(KEY_SPACE)
	await get_tree().physics_frame
	var active_board: LabBoard = app.board
	var elapsed := active_board.elapsed
	_key(KEY_F8)
	_key(KEY_F8)
	_check(app.board == active_board and app.mode == "run" and not get_tree().paused,
		"F8 leaves the running simulation active")
	await _wait_run()
	_check(app.board.elapsed > elapsed and app.board.outcome == "Complete"
		and app.hud.modal.is_visible_in_tree() and app.hud.modal.current == "result"
		and not app.hud.root.is_visible_in_tree(),
		"The hidden-UI run keeps scoring and shows the results screen")
	_key(KEY_R)
	_check(_visible_gameplay_ui().is_empty(), "Retry from results preserves the hidden UI")
	_key(KEY_F8)
	_check(app.hud.title.is_visible_in_tree() and app.studio.labels.is_visible_in_tree(),
		"F8 restores the HUD and annotations on the next attempt")

func _visible_gameplay_ui() -> Array[Node]:
	var shown := _visible_visuals(app.hud.root)
	shown.append_array(_visible_visuals(app.studio.labels))
	shown.append_array(_visible_visuals(app.workbench.hint_overlay))
	return shown

func _visible_visuals(branch: Node) -> Array[Node]:
	var shown: Array[Node] = []
	if (branch is CanvasItem or branch is Node3D) and branch.is_visible_in_tree():
		shown.append(branch)
	for child in branch.get_children(true):
		shown.append_array(_visible_visuals(child))
	return shown

func _check_ball_configuration() -> void:
	var ball: RigidBody3D = app.board.ball
	var settings: Dictionary = app.board.ballspec
	var ball_shape: SphereShape3D = ball.get_child(0).shape
	var surface := ball.physics_material_override
	_check(ball.axis_lock_linear_z and ball.axis_lock_angular_x and ball.axis_lock_angular_y
		and is_equal_approx(ball.mass, settings.mass) and is_equal_approx(ball_shape.radius, settings.radius)
		and is_equal_approx(surface.bounce, settings.bounce) and is_equal_approx(surface.friction, settings.friction),
		"%s ball uses its mass, radius, material and plane locks" % app.board.level.ball)

func _impact(event: String, _point: Vector3, _strength: float) -> void:
	if event == "heavy":
		heavy_contact = true

func _edit() -> void:
	app.load_level(0)
	app.dismiss_tutorial()
	await get_tree().process_frame
	var histories := []
	for index in range(3):
		_key(KEY_1)
		_pointer(Vector2(-9 + 5 * index, 7.5))
		_click(Vector2(-9 + 5 * index, 7.5))
		await get_tree().process_frame
		histories.append(app.placements.duplicate(true))
		if not app.workbench.tool.is_empty():
			_key(KEY_1)
	_check(app.placements.size() == 3 and app.remaining("plank") == 0, "Mouse place and inventory reach cap")
	if app.placements.size() != 3:
		return
	for index in range(3):
		_click(app.placements[index].position)
		_key(KEY_E)
		await get_tree().process_frame
		histories.append(app.placements.duplicate(true))
	_check(app.placements[1].rotation == C.ROTATION_SNAP, "Selected rotation snaps to 15 degrees")
	for index in range(3):
		var point: Vector2 = app.placements[index].position
		_mouse_button(MOUSE_BUTTON_LEFT, true, point)
		_pointer(point + Vector2(0.25, -0.5), MOUSE_BUTTON_MASK_LEFT)
		_mouse_button(MOUSE_BUTTON_LEFT, false, point + Vector2(0.25, -0.5))
		await get_tree().process_frame
		histories.append(app.placements.duplicate(true))
	_check(app.placements[0].position == Vector2(-8.75, 7.0), "Mouse drag moves and snaps a selected object")
	_click(app.placements[1].position)
	_key(KEY_DELETE)
	await get_tree().process_frame
	histories.append(app.placements.duplicate(true))
	_check(app.placements.size() == 2 and app.remaining("plank") == 1, "Delete refunds the part")
	_check(app.history.undo_steps.size() == 10, "Ten edits are in history")
	for index in range(10):
		_key(KEY_Z, true)
		await get_tree().process_frame
		_check(app.placements == (histories[8 - index] if index < 9 else []), "Undo %d restores the matching layout" % (index + 1))
	for index in range(10):
		_key(KEY_Y, true)
		await get_tree().process_frame
		_check(app.placements == histories[index], "Redo %d restores the matching layout" % (index + 1))
	_click(app.placements[0].position)
	_mouse_button(MOUSE_BUTTON_WHEEL_UP, true, app.placements[0].position, true)
	_check(app.placements[0].rotation == C.ROTATION_SNAP + C.FINE_ROTATION, "Ctrl+wheel adds five degrees")
	_key(KEY_1)
	_pointer(Vector2(-8, 0))
	_check(app.workbench.ghost.visible and not app.workbench.can_commit, "Overlap with the floor shows the invalid preview")
	_pointer(Vector2(-8, 7.75))
	_check(app.workbench.ghost.visible and app.workbench.can_commit, "Open space shows the valid preview")

func _other_controls() -> void:
	app.load_level(0)
	_key(KEY_F9)
	await get_tree().process_frame
	_key(KEY_SPACE)
	await get_tree().physics_frame
	await get_tree().physics_frame
	_key(KEY_SPACE)
	await get_tree().process_frame
	var time: float = app.board.elapsed
	for index in range(10):
		await get_tree().physics_frame
	_check(app.board.elapsed == time and app.mode == "paused", "Space holds the physics clock")
	_key(KEY_SPACE)
	await get_tree().process_frame
	_key(KEY_R)
	await get_tree().process_frame
	_check(app.mode == "build" and app.board.ball.position == C.in_plane(C.LEVELS[0].start),
		"Reset restores ball and placed starting state")
	_key(KEY_F1)
	_key(KEY_ESCAPE)
	await get_tree().process_frame
	_check(app.hud.debug_panel.visible and app.hud.modal.current == "pause", "F1 and Esc show diagnostics and pause")
	_key(KEY_ESCAPE)
	await get_tree().process_frame
	_check(app.hud.hint_button.disabled, "Hint stays locked before 60 seconds")
	await get_tree().create_timer(C.HINT_DELAY + 0.1).timeout
	app.refresh_ui()
	_check(not app.hud.hint_button.disabled, "Hint unlocks after the level clock reaches 60 seconds")
	app.show_hint()
	_check(app.workbench.hint.visible and app.workbench.hint_index == 1, "Hint shows the first reference object")
	app.show_hint()
	_check(app.workbench.hint.get_child_count() == 1 and app.workbench.hint_index == 2,
		"A second hint replaces the first ghost")
	_key(KEY_BRACKETRIGHT)
	_check(Engine.time_scale == 2.0 and Engine.physics_ticks_per_second == 2 * C.TICK_HZ,
		"2× preserves the solver step through cadence")
	_key(KEY_BRACKETLEFT)
	_key(KEY_BRACKETLEFT)
	_check(Engine.time_scale == 0.25 and Engine.physics_ticks_per_second == C.TICK_HZ / 4.0,
		"¼× preserves the solver step through cadence")
	_key(KEY_BRACKETRIGHT)
	_mouse_button(MOUSE_BUTTON_MIDDLE, true, Vector2(0, 4))
	_pointer(Vector2(0, 4), MOUSE_BUTTON_MASK_MIDDLE, Vector2(-900, 0))
	_check(is_equal_approx(app.camera.yaw, C.CAMERA_LIMIT), "Middle drag orbit stops at 45 degrees")
	_mouse_button(MOUSE_BUTTON_MIDDLE, false, Vector2(0, 4))
	_key(KEY_F)
	_check(is_equal_approx(app.camera.yaw, C.CAMERA_YAW), "F restores the 15-degree frame")
	app.load_level(1)
	_key(KEY_5)
	_pointer(Vector2(-10, 9.75))
	_check(not app.workbench.can_commit, "Swing Time rejects an anchor below the permitted band")
	_pointer(Vector2(-10, 12))
	_mouse_button(MOUSE_BUTTON_LEFT, true, Vector2(-10, 12))
	_pointer(Vector2(-3, 12), MOUSE_BUTTON_MASK_LEFT)
	_mouse_button(MOUSE_BUTTON_LEFT, false, Vector2(-3, 12))
	var rope_placed: bool = app.placements.size() == 1
	if rope_placed:
		rope_placed = is_equal_approx(LabRope.vector_for(app.placements[0]).length(), C.ROPE_MAX)
	_check(rope_placed, "Dragging a legal rope anchor clamps the cable to six metres")
	if rope_placed:
		var initial := LabRope.vector_for(app.placements[0])
		_click(app.placements[0].position)
		_key(KEY_E)
		var turned := LabRope.vector_for(app.placements[0])
		_check(is_equal_approx(rad_to_deg(initial.angle_to(turned)), C.ROTATION_SNAP),
			"Rotating a selected rope rotates the cable and cradle")
	app.load_level(1)
	_key(KEY_SPACE)
	await get_tree().process_frame
	await _wait_run()
	_check(app.board.outcome == "Ball lost" and app.board.ball.position.y < C.KILL_Y, "The kill plane fails a dropped ball")
	app.load_level(2)
	_key(KEY_SPACE)
	await get_tree().process_frame
	await _wait_run()
	_check(app.board.outcome == "Too slow" and is_equal_approx(app.board.elapsed, C.TIME_LIMIT), "Timeout fires at 25 simulation seconds")

func _soak() -> void:
	var start := Time.get_ticks_msec()
	var lap := 0
	var next_size := 0
	var requested := [Vector2i(1600, 900), Vector2i(1440, 900), Vector2i(1680, 720)]
	var stress := arguments.has("stress")
	var samples := PackedInt32Array()
	var render_before_capture := 0
	app.dismiss_tutorial()
	_key(KEY_F1)
	while Time.get_ticks_msec() - start < 104000:
		var now := Time.get_ticks_msec() - start
		if next_size < requested.size() and now >= next_size * 29000:
			DisplayServer.window_set_size(requested[next_size])
			await get_tree().create_timer(1.0).timeout
			_check(DisplayServer.window_get_size() == requested[next_size], "Window resized to " + str(requested[next_size]))
			var frame_rect: Rect2 = app.hud.root.get_rect()
			var controls_rect: Rect2 = app.hud.play_button.get_global_rect()
			_check(frame_rect.encloses(controls_rect), "Inventory and transport fit the resized layout")
			next_size += 1
		if stress and lap % 2 == 0:
			app.load_level(4)
			app.placements = _full_kit()
			if lap == 0:
				var built := []
				for part in app.placements:
					_check(LabBlueprint.valid(part, app.board.level, built), "Full kit legal placement " + str(part.type))
					built.append(part)
			app.reset_attempt()
		else:
			app.load_level(lap % C.LEVELS.size())
			_key(KEY_F9)
			if app.board.level.id == "L4":
				var cloth := C.placement("cloth", Vector2(-9, 4), 0)
				if LabBlueprint.valid(cloth, app.board.level, app.placements):
					app.placements.append(cloth)
					app.reset_attempt()
		await get_tree().create_timer(0.30).timeout
		_key(KEY_SPACE)
		var with_full_kit: bool = stress and lap % 2 == 0
		while app.mode == "run" and Time.get_ticks_msec() - start < 104000:
			await get_tree().create_timer(0.5).timeout
			if with_full_kit:
				samples.append(Engine.get_frames_per_second())
			if arguments.has("capture") and render_before_capture < next_size:
				_key(KEY_F12)
				render_before_capture = next_size
		if app.mode == "result":
			_check(app.board.outcome == "Complete", "Soak " + app.board.level.id + " " + app.board.outcome)
		lap += 1
	if not samples.is_empty():
		samples.sort()
		print("FULL KIT WINDOWED FPS min/median/max=", samples[0], "/", samples[samples.size() / 2], "/", samples[-1])
	print("SOAK elapsed ", (Time.get_ticks_msec() - start) / 1000.0, " s  attempts=", lap)

func _ui_capture() -> void:
	_key(KEY_F1)
	app.level_age = C.HINT_DELAY
	app.show_hint()
	var rate_button: Button = app.hud.rates[0]
	var hover := InputEventMouseMotion.new()
	hover.position = get_viewport().get_stretch_transform() * rate_button.get_global_rect().get_center()
	hover.global_position = hover.position
	Input.parse_input_event(hover)
	Input.flush_buffered_events()
	await get_tree().create_timer(1.0).timeout
	_check(_has_visible_popup(get_viewport()), "A gameplay tooltip is open before F8")

	var shown := await _capture_png()
	_key(KEY_F8)
	var hidden := await _capture_png()
	_check(not _has_visible_popup(get_viewport()), "F8 dismisses the active gameplay tooltip")
	_key(KEY_F8)
	var restored := await _capture_png()
	_check(shown != null and hidden != null and restored != null, "F12 saves each visibility state")
	if shown != null and hidden != null and restored != null:
		var header_pixel := Vector2i(shown.get_width() / 2, shown.get_height() / 16)
		var before := shown.get_pixelv(header_pixel).get_luminance()
		var after := hidden.get_pixelv(header_pixel).get_luminance()
		var again := restored.get_pixelv(header_pixel).get_luminance()
		_check(after > before + 0.3 and absf(again - before) < 0.05,
			"The F12 PNG shows the world in place of the header when hidden and the header when restored")

func _capture_png() -> Image:
	var before := DirAccess.get_files_at(C.SCREENSHOT_PATH)
	_key(KEY_F12)
	while app.screenshot_busy:
		await get_tree().process_frame
	for file in DirAccess.get_files_at(C.SCREENSHOT_PATH):
		if file.ends_with(".png") and not before.has(file):
			return Image.load_from_file(ProjectSettings.globalize_path(C.SCREENSHOT_PATH.path_join(file)))
	return null

func _has_visible_popup(branch: Node) -> bool:
	for child in branch.get_children(true):
		if child is Popup and child.visible:
			return true
		if _has_visible_popup(child):
			return true
	return false

func _graphics() -> void:
	var stress := arguments.has("stress")
	var aspect := "wide" if arguments.has("wide") else ("sixteen-nine" if arguments.has("sixteen-nine") else "sixteen-ten")
	app.dismiss_tutorial()
	_check(app.audio.clips.size() == app.audio.CUES.size(), "All synthesized audio clips initialized")
	if stress:
		app.load_level(4)
		app.placements = _full_kit()
		app.reset_attempt()
	else:
		_key(KEY_F9)
	_key(KEY_F1)
	await get_tree().create_timer(2.5).timeout
	_key(KEY_F12)
	await get_tree().create_timer(1.0).timeout
	var fps_until := Time.get_ticks_msec() + 10000
	while Time.get_ticks_msec() < fps_until:
		await get_tree().create_timer(0.75).timeout
		fx_fps.append(Engine.get_frames_per_second())
	fx_fps.sort()
	print("GRAPHICS ", aspect, " size=", DisplayServer.window_get_size(), " full_kit=", app.placements.size(),
		" FPS min/median/max=", fx_fps[0], "/", fx_fps[fx_fps.size() / 2], "/", fx_fps[fx_fps.size() - 1])
	if stress:
		_key(KEY_SPACE)
		await get_tree().process_frame
		var fans: AudioStreamPlayer = app.audio.ambiences.fan
		var belts: AudioStreamPlayer = app.audio.ambiences.conveyor
		_check(fans.playing and belts.playing, "Fan and conveyor audio loops started in simulation")
		var samples := PackedInt32Array()
		while app.mode == "run":
			await get_tree().create_timer(0.75).timeout
			samples.append(Engine.get_frames_per_second())
		samples.sort()
		_check(app.board.outcome == "Complete", "Graphical full-kit L5 cleared the receiver")
		if not samples.is_empty():
			print("FULL KIT RUN ", app.board.outcome, " FPS min/median/max=", samples[0], "/",
				samples[samples.size() / 2], "/", samples[samples.size() - 1], " stretch=", app.board.max_rope_stretch)
	elif arguments.has("play"):
		_key(KEY_SPACE)
		await get_tree().process_frame
		await _wait_run()
		_check(app.board.outcome == "Complete", "Graphical L1 reference completed")

func _full_kit() -> Array:
	return [
		C.placement("box_s", Vector2(3, 0.75), 0),
		C.placement("box_s", Vector2(-12, 5), 0),
		C.placement("plank", Vector2(-9.5, 5.5), 15),
		C.placement("plank", Vector2(9.7, 10.8), 15),
		C.placement("lever", Vector2(-8.5, 13.75), 0),
		C.placement("rope", Vector2(-7, 7), 0, {"rope_vector": Vector2(0, -1)}),
		C.placement("spring", Vector2(-6.75, 3.75), 15),
		C.placement("conveyor", Vector2(-2.75, 2.75), 0),
		C.placement("fan", Vector2(-11.75, 2.5), 0),
	]

func _wait_run() -> void:
	for tick in range(3300):
		if app.mode == "result":
			return
		await get_tree().physics_frame

func _wait_job() -> void:
	for tick in range(17000):
		if app.job.is_empty():
			return
		await get_tree().physics_frame

func _check(condition: bool, message: String) -> void:
	if not condition:
		errors += 1
	print("  ", "PASS" if condition else "FAIL", "  ", message)

func _key(key: Key, ctrl := false) -> void:
	var event := InputEventKey.new()
	event.keycode = key
	event.ctrl_pressed = ctrl
	event.pressed = true
	Input.parse_input_event(event)
	Input.flush_buffered_events()
	event = event.duplicate()
	event.pressed = false
	Input.parse_input_event(event)
	Input.flush_buffered_events()

func _click(point: Vector2) -> void:
	_mouse_button(MOUSE_BUTTON_LEFT, true, point)
	_mouse_button(MOUSE_BUTTON_LEFT, false, point)

func _mouse_button(button: MouseButton, pressed: bool, point: Vector2, ctrl := false) -> void:
	var event := InputEventMouseButton.new()
	event.ctrl_pressed = ctrl
	event.button_index = button
	event.position = get_viewport().get_stretch_transform() * app.camera.unproject_position(C.in_plane(point))
	event.global_position = event.position
	event.pressed = pressed
	Input.parse_input_event(event)
	Input.flush_buffered_events()

func _pointer(point: Vector2, mask := 0, relative_move := Vector2.ZERO) -> void:
	var event := InputEventMouseMotion.new()
	event.position = get_viewport().get_stretch_transform() * app.camera.unproject_position(C.in_plane(point))
	event.global_position = event.position
	event.button_mask = mask
	event.relative = relative_move
	Input.parse_input_event(event)
	Input.flush_buffered_events()
