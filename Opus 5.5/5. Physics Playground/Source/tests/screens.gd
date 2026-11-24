extends Node
## GPU visual check (only run through tools/run_godot.ps1 -Gpu: launch gate, monitoring, hard timeout):
## screenshots of the level select and every level (build / solution / running / result), the F1 overlay,
## three aspect ratios (16:9, 16:10, 21:9, all <= 1920x1080) and an FPS measurement with level 5 fully built.
## Started by main.gd with `-- --autotest=screens [--bench]`.

const C := preload("res://scripts/core/config.gd")

var main: Node
var out_dir := ""


func _ready() -> void:
	main = get_parent()
	out_dir = ProjectSettings.globalize_path("res://").path_join("../tools/screens").simplify_path()
	DirAccess.make_dir_recursive_absolute(out_dir)
	Save.use_file("user://save_screens.json")
	Save.reset_progress()
	_run.call_deferred()


func _shot(file: String) -> void:
	await RenderingServer.frame_post_draw
	var img := get_viewport().get_texture().get_image()
	img.save_png(out_dir.path_join(file + ".png"))
	print("SCREEN %s %dx%d" % [file, img.get_width(), img.get_height()])


func _wait(sec: float) -> void:
	await get_tree().create_timer(sec, true, false, true).timeout


func _measure(seconds: float) -> Dictionary:
	var frames := 0
	var t := 0.0
	var worst := 0.0
	var step_ms := 0.0
	while t < seconds:
		await get_tree().process_frame
		var dt := get_process_delta_time() / maxf(Engine.time_scale, 0.001)
		t += dt
		frames += 1
		if t > 0.5:
			worst = maxf(worst, dt)
		step_ms = maxf(step_ms, Performance.get_monitor(Performance.TIME_PHYSICS_PROCESS) * 1000.0)
	return {"avg_fps": frames / maxf(t, 0.001), "worst_ms": worst * 1000.0, "max_physics_ms": step_ms}


func _run() -> void:
	var bench := OS.get_cmdline_user_args().has("--bench")
	DisplayServer.window_set_size(Vector2i(1600, 900))
	await _wait(1.0)
	if bench:
		await _benchmark()
		_cleanup_save()
		main.quit_game(0)
		return
	await _shot("00_level_select")
	for i in C.LEVELS.size():
		var s: Node = main.open_level(i)
		await _wait(1.0)
		await _shot("L%d_1_build" % (i + 1))
		s.load_solution()
		await _wait(0.6)
		await _shot("L%d_2_solution" % (i + 1))
		s.toggle_play()
		await _wait(1.4)
		await _shot("L%d_3_running" % (i + 1))
		var t := 0.0
		while s.state < 3 and t < 26.0:
			await _wait(0.25)
			t += 0.25
		await _wait(1.8)
		await _shot("L%d_4_result" % (i + 1))
		print("SCREEN L%d state=%d time=%.2f" % [i + 1, s.state, s.last_time])
	# menus: Esc pause menu, F10 dev menu, F12 screenshot into <project>/Screenshots/
	var s1: Node = main.open_level(0)
	await _wait(0.8)
	main.call(&"_open_pause_menu")
	await _wait(0.5)
	await _shot("menu_pause")
	main.call(&"_close_pause_menu")
	main.get(&"_dev_menu").visible = true
	await _wait(0.4)
	await _shot("menu_dev")
	main.get(&"_dev_menu").visible = false
	var f12: String = await main.take_screenshot()
	print("SCREEN F12 file exists=%s %s" % [FileAccess.file_exists(f12), f12])
	if FileAccess.file_exists(f12):
		DirAccess.remove_absolute(f12)
	s1.reset()
	await _check_hidden_ui_screenshot()
	var s5: Node = main.open_level(4)
	s5.load_solution()
	main.get(&"_debug").visible = true
	for res: Vector2i in [Vector2i(1600, 900), Vector2i(1728, 1080), Vector2i(1920, 823)]:
		DisplayServer.window_set_size(res)
		await _wait(1.2)
		await _shot("aspect_%dx%d" % [res.x, res.y])
	DisplayServer.window_set_size(Vector2i(1600, 900))
	await _wait(1.0)
	s5.toggle_play()
	var m := await _measure(6.0)
	print("SCREEN FPS L5 running (vsync on): avg %.1f fps, worst frame %.2f ms, max physics frame %.3f ms" % [m["avg_fps"], m["worst_ms"], m["max_physics_ms"]])
	await _shot("L5_f1_overlay")
	_cleanup_save()
	main.quit_game(0)


func _cleanup_save() -> void:
	Save.reset_progress()
	DirAccess.remove_absolute(ProjectSettings.globalize_path("user://save_screens.json"))


func _press_f8() -> void:
	for pressed: bool in [true, false]:
		var e := InputEventKey.new()
		e.keycode = KEY_F8
		e.physical_keycode = KEY_F8
		e.pressed = pressed
		Input.parse_input_event(e)
		await get_tree().process_frame


func _grab() -> Image:
	await RenderingServer.frame_post_draw
	return get_viewport().get_texture().get_image()


## Average brightness of a screen rectangle (every 4th pixel).
static func _lum(img: Image, r: Rect2i) -> float:
	var sum := 0.0
	var n := 0
	for y in range(r.position.y, r.end.y, 4):
		for x in range(r.position.x, r.end.x, 4):
			var c := img.get_pixel(x, y)
			sum += (c.r + c.g + c.b) / 3.0
			n += 1
	return sum / maxf(n, 1.0)


## U-05: with the UI hidden (F8), the F12 screenshot contains no UI. The HUD panel and inventory regions are dark
## UI panels in a normal frame and must show the bright back wall in the hidden-UI frame and in the F12 file.
func _check_hidden_ui_screenshot() -> void:
	main.open_level(4)
	await _wait(1.2)
	var with_ui := await _grab()
	await _press_f8()
	await _wait(0.5)
	var no_ui := await _grab()
	var f12: String = await main.take_screenshot()
	var shot := Image.load_from_file(f12)
	var regions := [Rect2i(30, 25, 300, 80), Rect2i(700, 800, 200, 60)]
	var ok := shot != null
	var report := PackedStringArray()
	for r: Rect2i in regions:
		var a := _lum(with_ui, r)
		var b := _lum(no_ui, r)
		var c := _lum(shot, r) if shot else -1.0
		ok = ok and a < 0.45 and b > 0.6 and c > 0.6 and absf(b - c) < 0.05
		report.append("%s: with UI %.2f, hidden %.2f, F12 file %.2f" % [r, a, b, c])
	print("SCREEN U-05 %s %s" % ["PASS" if ok else "FAIL", " | ".join(report)])
	no_ui.save_png(out_dir.path_join("ui_hidden.png"))
	if shot:
		shot.save_png(out_dir.path_join("ui_hidden_f12.png"))
	DirAccess.remove_absolute(f12)
	await _press_f8()
	await _wait(0.3)


## One uncapped run (<= 60 s, monitored): L5 fully built and simulating, VSync off.
func _benchmark() -> void:
	var s: Node = main.open_level(4)
	s.load_solution()
	main.get(&"_debug").visible = true
	await _wait(1.0)
	DisplayServer.window_set_vsync_mode(DisplayServer.VSYNC_DISABLED)
	s.toggle_play()
	var m := await _measure(10.0)
	print("SCREEN BENCH L5 uncapped: avg %.1f fps, worst frame %.2f ms, max physics frame %.3f ms" % [m["avg_fps"], m["worst_ms"], m["max_physics_ms"]])
	await _shot("L5_bench_uncapped")
	DisplayServer.window_set_vsync_mode(DisplayServer.VSYNC_ENABLED)
