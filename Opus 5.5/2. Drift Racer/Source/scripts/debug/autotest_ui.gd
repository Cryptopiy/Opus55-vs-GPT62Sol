class_name AutoTestUi
extends Node
## Autotests for the F8 "hide all UI" switch (U-01..U-06), started by AutoTest.
##  * ui_toggle (headless or GPU): presses F8 through the input system and
##    checks EVERY CanvasItem in the tree: while hidden nothing draws (except a
##    must-use screen that is open), all other hotkeys keep working, new UI and
##    a race restart stay hidden; after F8 again everything visible is drawn.
##  * ui_toggle_shots (GPU): freezes the world and compares real frames:
##    UI hidden == frame with every CanvasLayer switched off, the F12 file
##    == no UI, and UI shown again == the frame before hiding.

signal finished(passed: bool, details: String)

const WATCHDOG_SECONDS := 150.0

var main: GameMain
var _problems := PackedStringArray()
var _done := false


func start(main_node: GameMain, scenario: String) -> void:
	main = main_node
	name = "AutoTestUi"
	process_mode = Node.PROCESS_MODE_ALWAYS
	get_tree().create_timer(WATCHDOG_SECONDS, true).timeout.connect(_report.bind("watchdog timeout"))
	await main.start_race(Config.DEFAULT_SEED, Config.DEFAULT_DIFFICULTY)
	main.race.give_player_to_ai(false)
	if scenario == "ui_toggle":
		await _run_logic()
	else:
		await _run_shots()
	_report("")


func _report(fatal: String) -> void:
	if _done:
		return
	_done = true
	if not fatal.is_empty():
		_problems.append(fatal)
	for p in _problems:
		print("[autotest] UI problem: ", p)
	finished.emit(_problems.is_empty(), "; ".join(_problems) if not _problems.is_empty() else "all UI toggle checks passed")


# ------------------------------------------------------------- logic test

func _run_logic() -> void:
	await _until_racing()
	await _frames(60)
	var drawn_before := _drawn_items()
	_check(drawn_before.size() > 10, "only %d UI items drawn before F8" % drawn_before.size())
	_expect_all_drawn("before F8")
	var t0 := main.race.manager.race_time
	_press(InputSetup.TOGGLE_UI)
	await _frames(2)
	_check(not main.ui_visibility.ui_visible, "F8 did not hide the UI")
	_check(not get_tree().paused, "F8 paused the game")
	_expect_nothing_drawn("after F8")
	await _frames(60)
	_check(main.race.manager.race_time > t0 + 0.3, "race clock did not advance while hidden")
	# U-03: every hotkey still works while hidden.
	var player := main.race.player
	var respawns := player.respawn_count
	_press(InputSetup.RESPAWN)
	await _frames(3)
	_check(player.respawn_count == respawns + 1, "R did not respawn while hidden")
	_expect_nothing_drawn("after R respawn")
	var cam_mode := main.race.camera.mode
	_press(InputSetup.CAMERA)
	await _frames(3)
	_check(main.race.camera.mode != cam_mode, "C did not switch camera while hidden")
	_press(InputSetup.CAMERA)
	var assist := player.assist_enabled
	_press(InputSetup.ASSIST)
	await _frames(3)
	_check(player.assist_enabled != assist, "F3 did not toggle the assist while hidden")
	_press(InputSetup.ASSIST)
	_press(InputSetup.DEBUG)
	await _frames(3)
	_check(main.debug_overlay.visible, "F1 did not switch the overlay on while hidden")
	_expect_nothing_drawn("with the F1 overlay switched on")
	# Must-use screens stay usable: Esc opens a drawn pause menu; F8 works there too.
	_press(InputSetup.PAUSE)
	await _frames(3)
	_check(get_tree().paused and _screen_drawn(main.pause_menu), "pause menu not drawn while UI hidden")
	_press(InputSetup.TOGGLE_UI)
	await _frames(2)
	_check(main.ui_visibility.ui_visible and _screen_drawn(main.pause_menu), "F8 while paused did not show the UI")
	_press(InputSetup.TOGGLE_UI)
	await _frames(2)
	_check(not main.ui_visibility.ui_visible and _screen_drawn(main.pause_menu), "F8 while paused hid the pause menu")
	_press(InputSetup.PAUSE)
	await _frames(3)
	_check(not get_tree().paused, "Esc did not resume")
	_expect_nothing_drawn("after resuming")
	# U-04: UI created while hidden is hidden (a new HUD text and a new layer).
	var extra := Label.new()
	extra.text = "spawned while hidden"
	main.hud.get_child(0).add_child(extra)
	var popup := CanvasLayer.new()
	var popup_label := Label.new()
	popup_label.text = "popup"
	popup.add_child(popup_label)
	main.add_child(popup)
	await _frames(2)
	_check(not UiVisibility.is_drawn(extra) and not UiVisibility.is_drawn(popup_label), "UI spawned while hidden is drawn")
	# U-04: restarting the race (new HUD binding, countdown) keeps it hidden.
	await main.start_race(Config.DEFAULT_SEED, Config.DEFAULT_DIFFICULTY)
	main.race.give_player_to_ai(false)
	_expect_nothing_drawn("during the restart countdown")
	await _until_racing()
	await _frames(30)
	_check(not main.ui_visibility.ui_visible, "restart showed the UI")
	_expect_nothing_drawn("after restart")
	# F8 again: everything that is visible draws again, incl. the spawned UI.
	_press(InputSetup.TOGGLE_UI)
	await _frames(2)
	_check(main.ui_visibility.ui_visible, "second F8 did not show the UI")
	_expect_all_drawn("after the second F8")
	_check(UiVisibility.is_drawn(main.debug_overlay.get_child(0)), "F1 overlay did not return")
	_check(UiVisibility.is_drawn(extra) and UiVisibility.is_drawn(popup_label), "spawned UI did not return")
	var missing := 0
	for item in drawn_before:
		if is_instance_valid(item) and _own_visible(item) and not UiVisibility.is_drawn(item):
			missing += 1
	_check(missing == 0, "%d UI items did not come back" % missing)
	print("[autotest] UI items drawn before F8: %d, after F8 twice: %d" % [drawn_before.size(), _drawn_items().size()])
	_press(InputSetup.DEBUG)
	popup.queue_free()
	extra.queue_free()
	# Results and main menu (must-use screens) stay drawn while hidden.
	_press(InputSetup.TOGGLE_UI)
	await _frames(2)
	main.results.open(main.race)
	await _frames(2)
	_check(_screen_drawn(main.results), "results screen hidden by F8")
	_expect_nothing_drawn("with the results screen open")
	main.open_menu()
	await _frames(2)
	_check(_screen_drawn(main.menu), "main menu hidden by F8")
	_check(not UiVisibility.is_drawn(main.hud.get_child(0) as CanvasItem), "HUD drawn in the menu while hidden")
	_press(InputSetup.TOGGLE_UI)
	await _frames(2)
	_check(main.ui_visibility.ui_visible and _screen_drawn(main.menu), "F8 in the main menu did not show the UI")


# --------------------------------------------------------------- GPU frames

func _run_shots() -> void:
	await _until_racing()
	await _frames(240)
	get_tree().paused = true                   # freeze the world (no menu shown)
	await _frames(8)
	var a := await _grab("uitoggle_visible")
	_press(InputSetup.TOGGLE_UI)
	await _frames(6)
	var b := await _grab("uitoggle_hidden")
	var dir := Screenshot.folder()
	var files_before := DirAccess.get_files_at(dir)
	_press(InputSetup.SCREENSHOT)
	await _frames(6)
	var f12: Image = null
	for f in DirAccess.get_files_at(dir):
		if not files_before.has(f) and f.ends_with(".png"):
			f12 = Image.load_from_file(dir.path_join(f))
	_check(f12 != null, "F12 did not save a screenshot")
	# Ground truth: every CanvasLayer off = a frame with no interface at all.
	var layers := get_tree().root.find_children("*", "CanvasLayer", true, false)
	var states := []
	for layer in layers:
		states.append((layer as CanvasLayer).visible)
		(layer as CanvasLayer).visible = false
	await _frames(6)
	var d := await _grab("")
	var d2 := await _grab("")
	for k in layers.size():
		(layers[k] as CanvasLayer).visible = states[k]
	_press(InputSetup.TOGGLE_UI)
	await _frames(6)
	var e := await _grab("uitoggle_back")
	get_tree().paused = false
	var noise := _diff(d, d2)
	var tolerance := maxf(0.002, noise * 3.0)
	var hidden_vs_none := _diff(b, d)
	var f12_vs_none := _diff(f12, d)
	var visible_vs_hidden := _diff(a, b)
	var back_vs_visible := _diff(a, e)
	print("[autotest] frame differences (share of sampled pixels): noise %.4f, hidden vs no-UI %.4f, F12 vs no-UI %.4f, visible vs hidden %.4f, shown-again vs visible %.4f" % [
		noise, hidden_vs_none, f12_vs_none, visible_vs_hidden, back_vs_visible])
	_check(hidden_vs_none <= tolerance, "hidden frame still contains UI pixels")
	_check(f12_vs_none <= tolerance, "F12 screenshot contains UI while hidden")
	_check(visible_vs_hidden >= 0.03, "UI was not visible before F8")
	_check(back_vs_visible <= tolerance, "UI did not fully return after F8")


# ---------------------------------------------------------------- helpers

func _drawn_items() -> Array[CanvasItem]:
	var out: Array[CanvasItem] = []
	for n in get_tree().root.find_children("*", "CanvasItem", true, false):
		if UiVisibility.is_drawn(n as CanvasItem):
			out.append(n as CanvasItem)
	return out


## The element's own visibility (what it would show without the F8 switch).
func _own_visible(item: CanvasItem) -> bool:
	if not item.is_inside_tree() or not item.is_visible_in_tree():
		return false
	var n := item.get_parent()
	while n != null:
		if n is CanvasLayer and not (n as CanvasLayer).visible:
			return false
		n = n.get_parent()
	return true


func _in_screen(item: Node, screen: CanvasLayer) -> bool:
	return screen.is_ancestor_of(item)


func _screen_drawn(screen: CanvasLayer) -> bool:
	return screen.visible and UiVisibility.is_drawn(screen.get_child(0) as CanvasItem)


## While hidden: only an open must-use screen may draw.
func _expect_nothing_drawn(when: String) -> void:
	var bad := 0
	var example := ""
	for item in _drawn_items():
		if _in_screen(item, main.menu) or _in_screen(item, main.pause_menu) or _in_screen(item, main.results):
			continue
		bad += 1
		if example.is_empty():
			example = str(item.get_path())
	_check(bad == 0, "%d UI items drawn %s (e.g. %s)" % [bad, when, example])


## While shown: everything that is visible on its own must draw.
func _expect_all_drawn(when: String) -> void:
	var bad := 0
	for n in get_tree().root.find_children("*", "CanvasItem", true, false):
		var item := n as CanvasItem
		if _own_visible(item) and not UiVisibility.is_drawn(item):
			bad += 1
	_check(bad == 0, "%d visible UI items not drawn %s" % [bad, when])


func _check(ok: bool, problem: String) -> void:
	if not ok:
		_problems.append(problem)


func _press(action: StringName) -> void:
	var down := InputEventAction.new()
	down.action = action
	down.pressed = true
	Input.parse_input_event(down)
	var up := InputEventAction.new()
	up.action = action
	up.pressed = false
	Input.parse_input_event(up)


func _frames(n: int) -> void:
	for _i in n:
		await get_tree().process_frame


func _until_racing() -> void:
	for _i in 3000:
		if main.race != null and main.race.manager.is_racing():
			return
		await get_tree().process_frame
	_check(false, "race never started")


func _grab(tag: String) -> Image:
	await RenderingServer.frame_post_draw
	var img := get_viewport().get_texture().get_image()
	if not tag.is_empty():
		Screenshot.capture(get_viewport(), tag)
	return img


## Share of pixels (sampled every 4th row / column) that differ noticeably.
static func _diff(a: Image, b: Image) -> float:
	if a == null or b == null or a.get_size() != b.get_size():
		return 1.0
	var total := 0
	var bad := 0
	for y in range(0, a.get_height(), 4):
		for x in range(0, a.get_width(), 4):
			var ca := a.get_pixel(x, y)
			var cb := b.get_pixel(x, y)
			total += 1
			if absf(ca.r - cb.r) > 0.025 or absf(ca.g - cb.g) > 0.025 or absf(ca.b - cb.b) > 0.025:
				bad += 1
	return float(bad) / float(maxi(total, 1))
