extends SceneTree

const C = preload("res://game/config.gd")
const MAIN = preload("res://main.tscn")

var game: Node3D
var held := {}
var failures := 0

func _initialize() -> void:
	call_deferred("_run")

func _run() -> void:
	game = MAIN.instantiate()
	root.add_child(game)
	_check("menu opens with seed 2024, Normal and Start Race",
		game.screen == game.Screen.MENU and game.ui.seed_field.text == "2024"
		and game.ui.difficulty.selected == 1 and game.ui.start_button.visible)
	var track := game.race.track.data as TrackData
	_check("8 checkpoint areas and 120 Hz", game.race.track.find_children("Checkpoint *", "Area3D", true, false).size() == C.CHECKPOINTS
		and Engine.physics_ticks_per_second == 120)
	var repeat := TrackData.new()
	repeat.generate(C.DEFAULT_SEED)
	_check("same seed reproduces every sample and valid length", repeat.points == track.points
		and track.length >= C.LENGTH_RANGE.x and track.length <= C.LENGTH_RANGE.y)
	var different := TrackData.new()
	different.generate(-632)
	_check("different integer seed produces a different valid track", different.points != track.points
		and different.length >= C.LENGTH_RANGE.x and different.length <= C.LENGTH_RANGE.y)
	game.ui.seed_field.text = "text"
	game.ui.start_button.pressed.emit()
	_check("seed field rejects nonintegers", game.screen == game.Screen.MENU and not game.ui.seed_error.text.is_empty())
	game.ui.seed_field.text = "2024"
	game.ui.start_button.pressed.emit()
	var race := game.race as RaceSession
	var car := race.player
	var counts := PackedInt32Array()
	race.countdown_changed.connect(func(value: int) -> void: counts.append(value))
	# Use the normal key events and player controller throughout this acceptance lap.
	var grid := car.global_position
	_key(KEY_W, true)
	await _frames(240)
	_check("four shared ray cars locked into the staggered starting grid",
		race.cars.size() == C.CAR_COUNT and car.freeze and grid.is_equal_approx(car.global_position))
	_check("U-01 HUD is visible by default during the countdown", game.ui.hud.is_visible_in_tree())
	_tap(KEY_F8)
	await _frames(124)
	_check("3,2,1,GO unfreezes the cars", counts == PackedInt32Array([3, 2, 1, 0]) and race.running and not car.freeze)
	_check("U-01 F8 hides the countdown and the rest of the HUD", not game.ui.hud.is_visible_in_tree())
	_tap(KEY_ESCAPE)
	await _frames(2)
	var elapsed := race.elapsed
	await _frames(10)
	_check("Escape pause actually freezes race time", game.screen == game.Screen.PAUSE and race.elapsed == elapsed)
	_check("U-02 pause controls remain visible with gameplay UI hidden",
		game.ui.pause_menu.is_visible_in_tree())
	_tap(KEY_ESCAPE)
	_tap(KEY_F1)
	_tap(KEY_C)
	_tap(KEY_F3)
	await _frames(12)
	_check("camera, assist and debug keyboard toggles", game.camera.hood and not car.assist and game.ui.hud.debug)
	_check("U-03 throttle, camera, assist, F1 and race timing work with gameplay UI hidden",
		game.race == race and not paused and race.elapsed > elapsed and car.speed > 0.0
		and not game.ui.gameplay_overlay.is_visible_in_tree())
	_tap(KEY_C)
	_tap(KEY_F3)
	await _check_overlay_visibility()
	for rival in race.cars:
		# The parked-opponent run covers traffic separately. Give this lap a clean road.
		rival.collision_mask = C.ROAD_LAYER | C.WALL_LAYER
	var last_drift := -INF
	var drift_time := 0
	var drift_events := 0
	var scored_corners := 0
	var scored_here := true
	var drift_break := 0.0
	var peak_pending := 0.0
	var peak_skids := 0
	var smoke_seen := false
	var contacts := 0
	for i in 18000:
		await physics_frame
		if car.completed_laps > 0:
			break
		var curve := track.curvature[track.index_at(car.course_progress + 9.0)]
		if (drift_time == 0 and car.course_progress - last_drift > 160.0 and car.course_progress > 105.0
				and absf(curve) > 0.009 and car.speed > 15.0):
			last_drift = car.course_progress
			drift_time = 62
			drift_break = signf(curve)
			drift_events += 1
			scored_here = false
			print("HANDBRAKE at %.1f m; %.1f km/h; curvature %.4f" % [last_drift, car.speed * C.KMH, curve])
		race.ai.command(car, race.cars, 0, 1.0 / 120.0)
		var throttle_demand := car.throttle
		var brake_demand := car.brake
		var steering_demand := car.steer_input
		if drift_time > 0:
			drift_time -= 1
			steering_demand = drift_break * (0.86 if drift_time > 31 else 0.26)
			throttle_demand = 1.0
			brake_demand = 0.0
		var limit := deg_to_rad(lerpf(C.MAX_STEER, C.MIN_STEER, sqrt(clampf(car.speed / C.TOP_SPEED, 0.0, 1.0))))
		var assist := clampf(car.body_slip / 32.0, -0.85, 0.85) * smoothstep(8.0, 34.0, absf(car.body_slip)) * 0.72
		var steer_delta := clampf(steering_demand + assist, -1.0, 1.0) * limit - car.steering
		_key(KEY_W, float(i % 12) / 12.0 < throttle_demand)
		_key(KEY_S, float(i % 12) / 12.0 < brake_demand)
		_key(KEY_A, steer_delta < -0.017)
		_key(KEY_D, steer_delta > 0.017)
		_key(KEY_SPACE, drift_time > 0)
		peak_pending = maxf(peak_pending, race.drift.pending)
		if race.drift.active and not scored_here:
			scored_corners += 1
			scored_here = true
		peak_skids = maxi(peak_skids, game.skids.active_count())
		smoke_seen = smoke_seen or game.effects[0].smoke[0].emitting or game.effects[0].smoke[1].emitting
		contacts += int(car.wall_impact > 300.0)
	for key_code: int in [KEY_W, KEY_S, KEY_A, KEY_D, KEY_SPACE]:
		_key(key_code, false)
	print("PLAYER: laps=%d next=%d t=%.2f handbrakes=%d scored corners=%d peak=%.2f banked=%.2f skids=%d smoke=%s contacts=%d" %
		[car.completed_laps, car.next_checkpoint, race.elapsed, drift_events, scored_corners, peak_pending, race.drift.total, peak_skids, smoke_seen, contacts])
	_check("a complete lap with three handbrake corners, score, bank, smoke and pooled skids",
		car.completed_laps == 1 and scored_corners >= 3 and peak_pending > 0.0 and race.drift.total > 0.0
		and peak_skids > 0 and peak_skids <= C.SKID_LIMIT and smoke_seen)
	_check("U-03 the full keyboard drift lap, timing and scoring ran with gameplay UI hidden",
		car.completed_laps == 1 and car.best_lap > 0.0 and race.drift.total > 0.0
		and not game.ui.gameplay_overlay.is_visible_in_tree())
	var empty: Array[DriftCar] = []
	game.skids.drive(empty, C.SKID_LIFE + 0.01)
	_check("all existing skid segments expire at 20 seconds and the pool stays at 600",
		game.skids.active_count() == 0 and game.skids.multimesh.instance_count == C.SKID_LIMIT)
	await _barrier(race, 100.0)
	await _barrier(race, C.TOP_KMH)
	var old_checkpoint := car.last_checkpoint
	var old_pose := car.global_position
	_tap(KEY_R)
	await _frames(1)
	_check("R mid-corner returns to last checkpoint facing forward with zero speed",
		car.last_checkpoint == old_checkpoint and car.global_position.distance_to(old_pose) > 10.0
		and car.linear_velocity.length() < 1.0 and (-car.global_basis.z).dot(track.forward[car.road_index]) > 0.9)
	_check("U-04 respawn preserves hidden gameplay UI", not game.ui.hud.is_visible_in_tree())
	_key(KEY_S, true)
	await _frames(180)
	_check("S at rest engages reverse and lights the reversing lamp",
		car.reversing and car.linear_velocity.dot(car.global_basis.z) > 1.0 and game.models[0].reverse_light.visible)
	_key(KEY_S, false)
	await _finish(race)
	await _recoveries()
	await _check_seed_change()
	_score_boundaries()
	print("ACCEPTANCE FAILURES: %d" % failures)
	quit(0 if failures == 0 else 1)

func _check_overlay_visibility() -> void:
	var notification := Label.new()
	notification.text = "Gameplay notification"
	var floating_text := Label3D.new()
	floating_text.text = "Floating UI"
	game.ui.gameplay_overlay.add_child(notification)
	game.ui.world_overlay.add_child(floating_text)
	_check("U-04 newly mounted gameplay UI inherits hidden visibility",
		not notification.is_visible_in_tree() and not floating_text.is_visible_in_tree())
	var visible_items: Array[NodePath] = []
	for item in game.ui.shell.find_children("*", "CanvasItem", true, false):
		if item.is_visible_in_tree():
			visible_items.append(item.get_path())
	if not visible_items.is_empty():
		print("Visible canvas items: %s" % [visible_items])
	_check("U-02 no canvas item or floating text is visible during hidden gameplay",
		visible_items.is_empty() and not game.ui.world_overlay.is_visible_in_tree())
	_tap(KEY_F8)
	await _frames(2)
	_check("U-06 the same key restores the HUD, armed F1 overlay and both spawned elements",
		game.ui.hud.is_visible_in_tree() and game.ui.hud.debug
		and notification.is_visible_in_tree() and floating_text.is_visible_in_tree())
	notification.free()
	floating_text.free()
	_tap(KEY_F8)
	await _frames(2)

func _check_seed_change() -> void:
	var seed_value := C.DEFAULT_SEED + 1
	game.go_to_menu(seed_value)
	await _frames(2)
	_check("U-02 main menu controls stay visible and the standalone F1 overlay stays hidden",
		game.ui.start_button.is_visible_in_tree() and game.ui.difficulty.is_visible_in_tree()
		and not game.ui.debug_back.is_visible_in_tree())
	game.ui.start_button.pressed.emit()
	await _frames(2)
	_check("U-04 hidden state survives loading a different track seed",
		game.race.seed_value == seed_value and not game.ui.hud.is_visible_in_tree())

func _barrier(race: RaceSession, kmh: float) -> void:
	var car := race.player
	var data := race.track.data
	var pose := data.frame_at(data.length * 0.31, 2.9, C.RIDE_HEIGHT)
	pose.basis = pose.basis.rotated(Vector3.UP, -0.42)
	car.reset_at(pose)
	car.road_index = data.nearest(car.global_position)
	car.linear_velocity = -pose.basis.z * (kmh / C.KMH)
	var hit := false
	var sparks := false
	var max_side := 0.0
	for i in 90:
		await physics_frame
		hit = hit or car.wall_contact
		sparks = sparks or game.effects[0].sparks.emitting
		max_side = maxf(max_side, absf((car.global_position - data.points[car.road_index]).dot(data.right[car.road_index])))
	print("BARRIER %.0f km/h: contact=%s sparks=%s max lateral=%.2f" % [kmh, hit, sparks, max_side])
	_check("barriers contain the car at %.0f km/h and trigger sparks" % kmh, hit and sparks and max_side < C.ROAD_WIDTH * 0.5)

func _finish(race: RaceSession) -> void:
	for rival in race.cars:
		rival.collision_layer = C.CAR_LAYER
		rival.collision_mask = C.CAR_LAYER | C.ROAD_LAYER | C.WALL_LAYER
	race.player_autopilot = true
	for i in 33000:
		await physics_frame
		if race.complete:
			break
	await process_frame
	await process_frame
	var sorted := true
	var previous := 0.0
	for rival in race.standings():
		print("RESULT %s %0.2fs best %.2fs laps %s" % [rival.name, rival.finish_time, rival.best_lap, rival.lap_times])
		sorted = sorted and rival.finish_time >= previous and rival.lap_times.size() == C.LAPS and rival.best_lap > 0.0
		previous = rival.finish_time
	_check("results show all four finishers in race-time order with three laps and best times",
		race.complete and game.screen == game.Screen.RESULTS and game.ui.results_rows.get_child_count() == 4 and sorted)
	var actions := PackedStringArray()
	for button in game.ui.results_menu.find_children("*", "Button", true, false):
		actions.append(button.text)
	_check("results expose restart, new seed and menu", actions == PackedStringArray(["Restart", "New seed", "Menu"]))
	_check("U-02 results and all three actions stay visible with gameplay UI hidden",
		game.ui.results_menu.is_visible_in_tree() and not game.ui.debug_back.is_visible_in_tree())

func _recoveries() -> void:
	game.restart_race()
	var race := game.race as RaceSession
	var data := race.track.data
	var car := race.player
	var rival := race.cars[3]
	await _frames(366)
	_check("U-04 hidden state survives a race restart",
		not game.ui.hud.is_visible_in_tree() and not game.ui.world_overlay.is_visible_in_tree())
	var wrong := data.frame_at(12.0, 4.8, C.RIDE_HEIGHT)
	wrong.basis = wrong.basis.rotated(wrong.basis.y, PI)
	car.reset_at(wrong)
	car.road_index = data.index_at(12.0)
	car.freeze = true
	rival.freeze = true
	await _frames(250)
	_check("wrong-way detection runs after two seconds and cannot award a lap", car.wrong_way > C.WRONG_WAY_TIME
		and car.completed_laps == 0)
	car.freeze = false
	var skip_distance := data.length * 2.0 / C.CHECKPOINTS - 4.6
	var skipped := data.frame_at(skip_distance, 3.5, C.RIDE_HEIGHT)
	car.reset_at(skipped)
	car.road_index = data.index_at(skip_distance)
	car.linear_velocity = -skipped.basis.z * 16.0
	await _frames(48)
	_check("crossing checkpoint two first cannot advance the checkpoint sequence",
		car.next_checkpoint == 0 and car.last_checkpoint == 0 and car.completed_laps == 0)
	var flipped := data.frame_at(12.0, 4.8, 0.83)
	flipped.basis = flipped.basis.rotated(-flipped.basis.z, PI)
	car.reset_at(flipped)
	car.road_index = data.index_at(12.0)
	await _frames(315)
	_check("real roof contact auto-respawns and an immobilized AI triggers checkpoint recovery",
		car.recoveries == 1 and car.global_basis.y.y > 0.8 and rival.recoveries >= 1)
	rival.freeze = false

func _score_boundaries() -> void:
	var car := DriftCar.new()
	var score := DriftScore.new()
	car.speed = 50.0 / C.KMH
	car.body_slip = 45.0
	for step in 1080:
		score.update(car, 1.0 / 120.0)
	_check("sustained legal slip reaches ×5", score.combo == C.COMBO_MAX and score.pending > 0.0)
	car.body_slip = 0.0
	score.update(car, 0.9)
	var unbanked := score.total == 0.0 and score.pending > 0.0
	score.update(car, 0.11)
	_check("drift banks only after one second below threshold", unbanked and score.total > 0.0 and score.pending == 0.0)
	car.body_slip = 45.0
	score.update(car, 0.5)
	car.wall_contact = true
	var previous := score.total
	score.update(car, 0.2)
	_check("wall contact cancels the active chain", score.pending == 0.0 and score.total == previous and score.combo == 1)
	car.free()

func _key(key_code: int, down: bool) -> void:
	if held.get(key_code, false) == down:
		return
	held[key_code] = down
	var event := InputEventKey.new()
	event.physical_keycode = key_code
	event.pressed = down
	Input.parse_input_event(event)

func _tap(key_code: int) -> void:
	_key(key_code, true)
	_key(key_code, false)

func _frames(count: int) -> void:
	for i in count:
		await physics_frame

func _check(label: String, passed: bool) -> void:
	print("%s %s" % ["PASS" if passed else "FAIL", label])
	if not passed:
		failures += 1
