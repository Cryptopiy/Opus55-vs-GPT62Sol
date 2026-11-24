extends SceneTree

const C = preload("res://game/config.gd")
const MAIN = preload("res://main.tscn")

var game: Node3D
var launched := Time.get_ticks_msec()

func _initialize() -> void:
	call_deferred("_run")

func _run() -> void:
	var options := OS.get_cmdline_user_args()
	var duration := 18.0
	for value in options:
		if value.is_valid_float():
			duration = float(value)
	game = MAIN.instantiate()
	root.add_child(game)
	await create_timer(2.0 if options.has("shots") else 0.35).timeout
	if options.has("shots"):
		await _capture()
	game.ui.start_button.pressed.emit()
	game.race.player_autopilot = true
	if options.has("ui-capture"):
		if not await _check_ui_capture():
			quit(1)
			return
	if options.has("shots"):
		await create_timer(2.8).timeout
		await _capture()
		await create_timer(3.8).timeout
		_key(KEY_F1, true)
		_key(KEY_F1, false)
		await create_timer(0.12).timeout
		await _capture()
	else:
		await create_timer(4.25).timeout
	var frames := 0
	var slowest := 10000.0
	var fastest := 0.0
	var begun := Time.get_ticks_msec()
	var sample_at := begun + 1000
	var sample_frames := 0
	var low_hz := INF
	var high_hz := 0.0
	# Leave a margin for the parent watchdog even if renderer startup was slow.
	var deadline := mini(begun + int(duration * 1000.0), launched + 109000)
	while Time.get_ticks_msec() < deadline:
		await process_frame
		frames += 1
		var now := Time.get_ticks_msec()
		if now >= sample_at:
			var fps := float(frames - sample_frames) * 1000.0 / (now - sample_at + 1000.0)
			slowest = minf(slowest, fps)
			fastest = maxf(fastest, fps)
			sample_frames = frames
			sample_at = now + 1000
			low_hz = minf(low_hz, game.audio.motors[0].pitch)
			high_hz = maxf(high_hz, game.audio.motors[0].pitch)
	var measured := (Time.get_ticks_msec() - begun) * 0.001
	print("1080p VSync=%d size=%s sample: %.2f FPS, 1 s range %.2f-%.2f, %.2f measured seconds, %d frames, %.1f race seconds, draw calls=%d" %
		[DisplayServer.window_get_vsync_mode(), DisplayServer.window_get_size(),
		frames / measured, slowest, fastest, measured, frames, game.race.elapsed,
		Performance.get_monitor(Performance.RENDER_TOTAL_DRAW_CALLS_IN_FRAME)])
	print("GRAPHICS AUDIO: player engine %.1f–%.1f Hz, engines=%d, 3D Doppler=%d" %
		[low_hz, high_hz, game.audio.motors.size(), game.audio.motors[1].node.doppler_tracking])
	if options.has("effects"):
		await _effects()
	game.quit_game()

func _check_ui_capture() -> bool:
	# Hold the grid still so the F12 image can be compared pixel-for-pixel with the world.
	paused = true
	_tap(KEY_F1)
	var floating_text := Label3D.new()
	floating_text.text = "Floating UI"
	floating_text.billboard = BaseMaterial3D.BILLBOARD_ENABLED
	game.ui.world_overlay.add_child(floating_text)
	floating_text.global_position = game.camera.to_global(Vector3(0.0, 0.65, -4.0))
	await create_timer(0.12).timeout
	await RenderingServer.frame_post_draw
	var shown := root.get_texture().get_image()
	var directory := "res://Screenshots"
	var before := DirAccess.get_files_at(directory)
	_tap(KEY_F8)
	await _capture()
	var cursor_hidden := Input.mouse_mode == Input.MOUSE_MODE_HIDDEN
	var saved := Image.new()
	var capture_name := ""
	for file in DirAccess.get_files_at(directory):
		if file.ends_with(".png") and file not in before and saved.load(directory.path_join(file)) == OK:
			capture_name = file
			break
	game.ui.hide()
	game.ui.world_overlay.hide()
	await RenderingServer.frame_post_draw
	var world_only := root.get_texture().get_image()
	game.ui.show()
	_tap(KEY_F8)
	await create_timer(0.12).timeout
	await RenderingServer.frame_post_draw
	var restored := root.get_texture().get_image()
	floating_text.free()
	paused = false
	if not saved.is_empty():
		saved.convert(Image.FORMAT_RGB8)
		world_only.convert(Image.FORMAT_RGB8)
		shown.convert(Image.FORMAT_RGB8)
		restored.convert(Image.FORMAT_RGB8)
		var pixels := saved.get_data()
		if cursor_hidden and pixels == world_only.get_data() and pixels != shown.get_data() and pixels != restored.get_data():
			print("PASS U-05 F12 PNG matches the world-only frame pixel for pixel: %s" % capture_name)
			return true
	push_error("U-05 F12 capture did not match the world-only frame or the overlays failed to return.")
	return false

func _effects() -> void:
	var race := game.race as RaceSession
	var car := race.player
	var track := race.track.data
	race.player_autopilot = false
	var pose := track.frame_at(1163.0, -0.6, C.RIDE_HEIGHT)
	var travel := -pose.basis.z
	pose.basis = pose.basis.rotated(pose.basis.y, -0.53)
	car.reset_at(pose)
	car.road_index = track.nearest(pose.origin)
	car.linear_velocity = travel * (104.0 / C.KMH)
	game.camera.cut()
	_key(KEY_SPACE, true)
	await create_timer(0.58).timeout
	print("GRAPHICS DRIFT: marks=%d rear exhausts emitting=%s/%s, pending=%.2f" %
		[game.skids.active_count(), game.effects[0].smoke[0].emitting, game.effects[0].smoke[1].emitting, race.drift.pending])
	await _capture()
	_key(KEY_SPACE, false)
	pose = track.frame_at(track.length * 0.31, 2.9, C.RIDE_HEIGHT)
	pose.basis = pose.basis.rotated(Vector3.UP, -0.42)
	car.reset_at(pose)
	car.road_index = track.nearest(pose.origin)
	car.linear_velocity = -pose.basis.z * (100.0 / C.KMH)
	game.camera.cut()
	var contacted := false
	for i in 120:
		await physics_frame
		if game.effects[0].sparks.emitting:
			contacted = true
			break
	await create_timer(0.15).timeout
	await _capture()
	print("GRAPHICS BARRIER: running scrape emission=%s" % contacted)

func _capture() -> void:
	_tap(KEY_F12)
	await create_timer(0.24).timeout

func _tap(key_code: int) -> void:
	_key(key_code, true)
	_key(key_code, false)

func _key(key_code: int, pressed: bool) -> void:
	var event := InputEventKey.new()
	event.physical_keycode = key_code
	event.pressed = pressed
	Input.parse_input_event(event)
