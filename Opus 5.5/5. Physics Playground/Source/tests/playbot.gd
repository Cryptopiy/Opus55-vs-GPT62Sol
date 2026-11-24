extends Node
## Windowed "normal play" bot (GPU run through tools/run_godot.ps1 -Gpu, <= 120 s per part): drives the real
## input paths with synthetic mouse / keyboard events (level select, tutorial, inventory hotkeys, ghost placement,
## rotate, select + drag, delete, undo/redo, camera orbit/pan/zoom/F, play/pause/reset/speeds, F1/F12/Esc/F9/F10,
## hints, rope drag, Alt handle drag). Started with `-- --autotest=play --part=1|2|3`. The run log is then checked
## for errors / warnings by the caller.

const C := preload("res://scripts/core/config.gd")
const Rope := preload("res://scripts/objects/obj_rope.gd")

var main: Node


func _ready() -> void:
	main = get_parent()
	Save.use_file("user://save_playbot.json")
	if _part() == 1:
		Save.reset_progress()
	_run.call_deferred()


func _part() -> int:
	for a in OS.get_cmdline_user_args():
		if a.begins_with("--part="):
			return int(a.substr(7))
	return 1


func _wait(sec: float) -> void:
	await get_tree().create_timer(sec, true, false, true).timeout


func _key(code: Key, ctrl: bool = false, shift: bool = false) -> void:
	for pressed in [true, false]:
		var e := InputEventKey.new()
		e.keycode = code
		e.physical_keycode = code
		e.ctrl_pressed = ctrl
		e.shift_pressed = shift
		e.pressed = pressed
		Input.parse_input_event(e)
		await get_tree().process_frame


func _move(p: Vector2, buttons: int = 0, shift: bool = false, alt: bool = false) -> void:
	var e := InputEventMouseMotion.new()
	e.position = p
	e.global_position = p
	e.button_mask = buttons
	e.shift_pressed = shift
	e.alt_pressed = alt
	Input.parse_input_event(e)
	await get_tree().process_frame


func _button(p: Vector2, index: MouseButton, pressed: bool, ctrl: bool = false, shift: bool = false, alt: bool = false) -> void:
	var e := InputEventMouseButton.new()
	e.position = p
	e.global_position = p
	e.button_index = index
	e.pressed = pressed
	e.ctrl_pressed = ctrl
	e.shift_pressed = shift
	e.alt_pressed = alt
	if pressed and index <= MOUSE_BUTTON_MIDDLE:
		e.button_mask = 1 << (index - 1)
	Input.parse_input_event(e)
	await get_tree().process_frame


func _click(p: Vector2) -> void:
	await _move(p)
	await _button(p, MOUSE_BUTTON_LEFT, true)
	await _button(p, MOUSE_BUTTON_LEFT, false)


func _drag(a: Vector2, b: Vector2, index: MouseButton = MOUSE_BUTTON_LEFT, shift: bool = false, alt: bool = false) -> void:
	await _move(a)
	await _button(a, index, true, false, shift, alt)
	var mask := 1 << (index - 1)
	for k in range(1, 11):
		await _move(a.lerp(b, float(k) / 10.0), mask, shift, alt)
	await _button(b, index, false, false, shift, alt)


func _click_control(c: Control) -> void:
	await _click(c.get_global_rect().get_center())


func _find_button(root: Node, text: String) -> Button:
	for n in root.find_children("*", "Button", true, false):
		var b := n as Button
		if b.text == text and b.is_visible_in_tree():
			return b
	return null


func _screen(world: Vector2) -> Vector2:
	var s: Node = main.session
	return s.camera_rig.cam.unproject_position(Vector3(world.x, world.y, 0.0))


func _wait_finished(limit: float) -> void:
	var t := 0.0
	while main.session.state < 3 and t < limit:
		await _wait(0.25)
		t += 0.25


func _log(text: String) -> void:
	print("PLAYBOT ", text)


func _run() -> void:
	DisplayServer.window_set_size(Vector2i(1600, 900))
	await _wait(1.0)
	match _part():
		1:
			await _part_one()
		2:
			await _part_two()
		3:
			await _part_three()
	_log("part %d done" % _part())
	if _part() == 3:
		Save.reset_progress()
		DirAccess.remove_absolute(ProjectSettings.globalize_path("user://save_playbot.json"))
	main.quit_game(0)


## Level 1 by hand: tutorial, placing via mouse, rotating, selecting, dragging, deleting, undo/redo, camera.
func _part_one() -> void:
	await _click_control(main.get(&"_level_select").find_child("Tile1", true, false))
	await _wait(1.2)
	var got := _find_button(main, "Got it")
	if got:
		await _click_control(got)
	var s: Node = main.session
	await _key(KEY_1)
	await _move(_screen(Vector2(-5.0, 4.25)))
	await _wait(0.2)
	# -30 deg ramp: Q (+15), E E E (-45), Ctrl+wheel up x3 (+15), Ctrl+wheel down x3 (-15) = -30
	await _key(KEY_Q)
	for k in 3:
		await _key(KEY_E)
	for k in 3:
		await _button(_screen(Vector2(-5.0, 4.25)), MOUSE_BUTTON_WHEEL_UP, true, true)
	for k in 3:
		await _button(_screen(Vector2(-5.0, 4.25)), MOUSE_BUTTON_WHEEL_DOWN, true, true)
	await _click(_screen(Vector2(-5.0, 4.25)))
	await _move(_screen(Vector2(-0.5, 0.0)))
	await _key(KEY_Q)
	await _key(KEY_Q)
	await _click(_screen(Vector2(-0.5, 0.0)))
	_log("placed %d planks (expect 2): %s" % [s.model.count(), str(s.model.records)])
	await _button(_screen(Vector2(0, 3)), MOUSE_BUTTON_RIGHT, true)
	await _button(_screen(Vector2(0, 3)), MOUSE_BUTTON_RIGHT, false)
	# select the bridge plank, drag it away and back, delete + undo, undo/redo the drag
	await _drag(_screen(Vector2(-0.5, 0.0)), _screen(Vector2(4.0, 2.0)))
	await _wait(0.2)
	await _key(KEY_Z, true)
	await _key(KEY_Y, true)
	await _key(KEY_Z, true)
	await _click(_screen(Vector2(-0.5, 0.0)))
	await _key(KEY_DELETE)
	await _key(KEY_Z, true)
	_log("after edit/undo: %d planks" % s.model.count())
	# camera: orbit, pan, zoom, reframe
	await _drag(Vector2(800, 450), Vector2(1000, 380), MOUSE_BUTTON_MIDDLE)
	await _drag(Vector2(800, 450), Vector2(700, 500), MOUSE_BUTTON_MIDDLE, true)
	for k in 3:
		await _button(Vector2(800, 450), MOUSE_BUTTON_WHEEL_UP, true)
	await _wait(0.5)
	await _key(KEY_F)
	await _wait(0.6)
	await _key(KEY_F1)
	# U-01/U-03: play this run with all UI hidden (F8), using hotkeys only
	await _key(KEY_F8)
	_log("F8 pressed: ui_visible=%s" % main.ui_visibility.ui_visible)
	await _key(KEY_SPACE)
	await _wait(2.0)
	await _key(KEY_SPACE)
	await _wait(0.5)
	await _key(KEY_SPACE)
	await _wait_finished(20.0)
	_log("L1 by hand state=%d time=%.2f stars=%d" % [s.state, s.last_time, s.last_stars])
	await _wait(1.6)
	await _key(KEY_F12)
	await _key(KEY_F8)
	_log("F8 pressed again: ui_visible=%s" % main.ui_visibility.ui_visible)
	await _key(KEY_R)
	await _key(KEY_BRACKETRIGHT)
	await _key(KEY_SPACE)
	await _wait_finished(15.0)
	_log("L1 at x2 state=%d time=%.2f" % [s.state, s.last_time])
	await _wait(1.6)
	var next := _find_button(main, "Next level")
	if next:
		await _click_control(next)
	await _wait(1.2)
	s = main.session
	if s.level["id"] != "L2":
		s = main.open_level(1)
		await _wait(1.0)
	# L2: rope by click + drag, then Alt+drag of the bob handle, then the reference solution
	await _key(KEY_1)
	await _drag(_screen(Vector2(-9.5, 12.5)), _screen(Vector2(-12.5, 11.5)))
	_log("L2 rope placed: %d objects" % s.model.count())
	await _click(_screen(Vector2(-9.5, 12.5)))
	if s.model.count() > 0 and s.model.records[0]["type"] == "rope":
		var bob: Vector2 = Rope.bail_point(s.model.records[0])
		await _drag(_screen(bob), _screen(bob + Vector2(0.6, 0.6)), MOUSE_BUTTON_LEFT, false, true)
		_log("L2 rope after Alt-drag: length %.2f rot %.0f" % [float(s.model.records[0]["length"]), float(s.model.records[0]["rot"])])
	await _key(KEY_F9)
	await _key(KEY_BRACKETLEFT)
	await _key(KEY_BRACKETLEFT)
	await _key(KEY_SPACE)
	await _wait(3.0)
	await _key(KEY_BRACKETRIGHT)
	await _wait_finished(15.0)
	_log("L2 solution at x0.25->x1 state=%d time=%.2f" % [s.state, s.last_time])
	await _wait(1.5)
	await _key(KEY_ESCAPE)
	await _wait(0.5)
	var resume := _find_button(main, "Resume")
	if resume:
		await _click_control(resume)
	await _key(KEY_F1)


## Levels 3-5 with F9, hint after 60 s in level 5, lever fulcrum Alt-drag in level 3.
func _part_two() -> void:
	for i in [2, 3, 4]:
		Save.record_result(String(C.LEVELS[i - 1]["id"]), 1, 9.0)
	var s: Node = main.open_level(2)
	await _wait(1.0)
	await _key(KEY_1)
	await _click(_screen(Vector2(-4.0, 2.0)))
	await _click(_screen(Vector2(-4.0, 2.0)))
	await _drag(_screen(Vector2(-4.0, 2.0)), _screen(Vector2(-5.2, 2.0)), MOUSE_BUTTON_LEFT, false, true)
	_log("L3 lever fulcrum after Alt-drag: %.1f" % float(s.model.records[0].get("fulcrum", 0.0)))
	await _key(KEY_F9)
	await _key(KEY_SPACE)
	await _wait_finished(15.0)
	_log("L3 state=%d time=%.2f" % [s.state, s.last_time])
	await _wait(1.5)
	s = main.open_level(3)
	await _wait(0.8)
	await _key(KEY_F9)
	await _key(KEY_SPACE)
	await _wait_finished(15.0)
	_log("L4 state=%d time=%.2f" % [s.state, s.last_time])
	await _wait(1.5)
	s = main.open_level(4)
	await _wait(0.8)
	await _key(KEY_F9)
	await _key(KEY_SPACE)
	await _wait_finished(25.0)
	_log("L5 state=%d time=%.2f" % [s.state, s.last_time])
	await _key(KEY_R)
	while s.level_time < C.HINT_DELAY + 0.5:
		await _wait(0.5)
	var hint := _find_button(main, "Hint")
	if hint:
		await _click_control(hint)
		await _click_control(hint)
	_log("L5 hints shown: %d" % s.hints_shown)
	await _wait(1.0)


## F10 dev menu in the windowed game: determinism on the cloth level and level 5, stack test; pause menu restart.
func _part_three() -> void:
	for i in [1, 2, 3, 4]:
		Save.record_result(String(C.LEVELS[i - 1]["id"]), 1, 9.0)
	for level in [3, 4]:
		var s: Node = main.open_level(level)
		await _wait(1.0)
		await _key(KEY_F10)
		await _wait(0.3)
		var det := _find_button(main, "Determinism test: solution x5")
		if det:
			await _click_control(det)
		while not main.dev_tools.busy:
			await _wait(0.1)
		while main.dev_tools.busy:
			await _wait(0.25)
		await _wait(0.5)
		await _key(KEY_F10)
		_log("dev determinism done on %s" % s.level["id"])
	await _key(KEY_F10)
	var stack := _find_button(main, "Stack test: 3 boxes, 10 s")
	if stack:
		await _click_control(stack)
	while not main.dev_tools.busy:
		await _wait(0.1)
	while main.dev_tools.busy:
		await _wait(0.25)
	await _key(KEY_F10)
	await _key(KEY_ESCAPE)
	await _wait(0.4)
	var restart := _find_button(main, "Restart level")
	if restart:
		await _click_control(restart)
	await _wait(0.8)
	await _click_control(_find_button(main, "Levels"))
	await _wait(1.0)
