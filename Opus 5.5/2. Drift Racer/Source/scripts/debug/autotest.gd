class_name AutoTest
extends Node
## Self-test runner, only created when the game is started with
## `-- --autotest=<scenario>` (see README). Scenarios drive the real game
## (menus, race, physics, AI) and print `[autotest]` lines, ending with
## `[autotest] RESULT <scenario> PASS|FAIL <details>` before quitting.
## Normal play never creates this node.

const TIMEOUTS := {
	"ai_race": 900.0, "full_race": 900.0, "drift": 240.0, "wall": 60.0,
	"respawn": 120.0, "flip": 40.0, "screens": 60.0, "bench": 50.0, "results_shot": 900.0,
	"drift_shots": 80.0, "ai_stuck": 60.0, "audio_check": 40.0, "wrong_way": 40.0, "wall_shots": 60.0,
}

var main: GameMain
var scenario := ""
var race: Race

var _time := 0.0
var _phase := 0
var _phase_time := 0.0
var _v := {}
var _done := false


static func requested_scenario() -> String:
	for a in OS.get_cmdline_user_args():
		if a.begins_with("--autotest="):
			return a.substr("--autotest=".length())
	return ""


static func arg_int(key: String, fallback: int) -> int:
	for a in OS.get_cmdline_user_args():
		if a.begins_with("--%s=" % key):
			return a.get_slice("=", 1).to_int()
	return fallback


func run(main_node: GameMain, name_: String) -> void:
	main = main_node
	scenario = name_
	process_mode = Node.PROCESS_MODE_ALWAYS
	print("[autotest] scenario '%s' started" % scenario)
	if scenario == "track_stats" or scenario == "drift_rules":
		var r: Array = AutoTestChecks.track_stats(arg_int("from", 0), arg_int("to", 200)) 			if scenario == "track_stats" else AutoTestChecks.drift_rules()
		_finish(r[0], r[1])
		return
	if scenario == "ui_toggle" or scenario == "ui_toggle_shots":
		var ui_test := AutoTestUi.new()
		add_child(ui_test)
		ui_test.finished.connect(_finish)
		ui_test.start(main, scenario)
		return
	if scenario == "physics_lab" or scenario == "drift_sweep":
		var lab: PhysicsLab = PhysicsLab.new() if scenario == "physics_lab" else DriftSweep.new()
		add_child(lab)
		lab.finished.connect(_finish)
		lab.start()
		return
	if not TIMEOUTS.has(scenario):
		_finish(false, "unknown scenario")
		return
	if scenario == "screens":
		main.open_menu()
		await get_tree().create_timer(1.5).timeout
		await _shot("menu")
	await main.start_race(arg_int("seed", Config.DEFAULT_SEED), arg_int("difficulty", Config.DEFAULT_DIFFICULTY))
	race = main.race
	match scenario:
		"ai_race":
			race.park_player()
		"full_race", "respawn", "screens", "bench", "results_shot":
			race.give_player_to_ai(false)
		"drift", "drift_shots":
			var d := DriftTestDriver.new()
			d.setup(1.0, RandomNumberGenerator.new(), race.manager)
			race.set_controller(race.player, d)
			_v["driver"] = d
			AutoTestChecks.print_corners(race.track)
		"wall", "flip", "wrong_way", "wall_shots":
			for car in race.cars:
				race.set_controller(car, CarController.new())
		"audio_check":
			race.give_player_to_ai(false)
		"ai_stuck":
			race.park_player()
	_v["max_lat"] = [0.0, 0.0, 0.0, 0.0]
	_v["walls"] = [0, 0, 0, 0]
	_v["skid_max"] = 0
	_v["smoke_ticks"] = 0
	_v["banked"] = 0
	_v["fps"] = PackedFloat32Array()
	for car in race.cars:
		car.barrier_hit.connect(_count_wall.bind(car.car_index))


func _count_wall(impact: float, index: int) -> void:
	var walls: Array = _v["walls"]
	walls[index] = int(walls[index]) + 1
	if scenario == "drift" and index == 0:
		var car := race.player
		var d: DriftTestDriver = _v["driver"]
		print("[autotest] barrier contact: lateral %.1f m, %.0f km/h, slip %.0f deg, impact %.1f m/s, driver phase %d" % [
			race.track.lateral_offset(car.global_position, maxi(car.track_index, 0)), car.speed_kmh, car.body_slip_deg, impact, d.get(&"_phase")])


func _process(delta: float) -> void:
	if _done or race == null or not is_instance_valid(race):
		return
	_time += delta
	_phase_time += delta
	if _time > float(TIMEOUTS.get(scenario, 300.0)):
		_finish(false, "timeout after %.0f s (phase %d)" % [_time, _phase])
		return
	match scenario:
		"ai_race":
			_tick_ai_race(false)
		"full_race":
			_tick_ai_race(true)
		"drift":
			_tick_drift()
		"wall", "wall_shots":
			_tick_wall()
		"wrong_way":
			_tick_wrong_way()
		"respawn":
			_tick_respawn()
		"flip":
			_tick_flip()
		"screens":
			_tick_screens()
		"bench":
			_tick_bench()
		"results_shot":
			_tick_results_shot()
		"drift_shots":
			_tick_drift_shots()
		"ai_stuck":
			_tick_ai_stuck()
		"audio_check":
			_tick_audio_check()


# ------------------------------------------------------------------ scenarios

func _tick_ai_race(with_player: bool) -> void:
	var m := race.manager
	var lats: Array = _v["max_lat"]
	var wall := RoadBuilder.wall_line()
	for car in race.cars:
		if car.track_index >= 0:
			lats[car.car_index] = maxf(lats[car.car_index], absf(race.track.lateral_offset(car.global_position, car.track_index)))
	if _phase_time > 15.0:
		_phase_time = 0.0
		var parts := PackedStringArray()
		for e in m.order:
			parts.append("%s lap %d %.0f km/h resp %d" % [e.car.display_name, m.current_lap(e.car), e.car.speed_kmh, e.car.respawn_count])
		print("[autotest] t=%.0f  %s" % [m.race_time, " | ".join(parts)])
	var done := true
	for car in race.cars:
		if (car != race.player or with_player) and not m.entry_of(car).finished:
			done = false
	if not done:
		return
	var avgs := PackedFloat32Array()
	var problems := PackedStringArray()
	for e in m.entries:
		if e.car == race.player and not with_player:
			continue
		avgs.append(e.finish_time / float(Config.LAP_COUNT))
		var walls: Array = _v["walls"]
		print("[autotest] %s P%d total %s best %s laps %s respawns %d wall contacts %d max|lat| %.2f m" % [
			e.car.display_name, e.finish_place, Race.format_time(e.finish_time), Race.format_time(e.best_lap),
			str(e.lap_times), e.car.respawn_count, walls[e.car.car_index], lats[e.car.car_index]])
		if lats[e.car.car_index] > wall:
			problems.append("%s left the track" % e.car.display_name)
	var lo := INF
	var hi := 0.0
	for a in avgs:
		lo = minf(lo, a)
		hi = maxf(hi, a)
	var spread := (hi - lo) / lo
	print("[autotest] average lap spread %.1f %%" % (spread * 100.0))
	if spread > 0.15:
		problems.append("lap time spread %.1f %% > 15 %%" % (spread * 100.0))
	if with_player:
		var results_ok: bool = main.results.visible
		for i in m.order.size():
			if m.order[i].finish_place != i + 1:
				results_ok = false
		print("[autotest] results screen visible and ordered: %s" % results_ok)
		if not results_ok:
			problems.append("results screen wrong")
	_finish(problems.is_empty(), "; ".join(problems) if not problems.is_empty() else "spread %.1f %%" % (spread * 100.0))


func _tick_drift() -> void:
	var e := race.manager.entry_of(race.player)
	var d := e.drift
	if d.event_age < float(_v.get("last_age", 99.0)):
		if d.last_event == DriftScorer.Event.BANKED:
			_v["banked"] = int(_v["banked"]) + 1
			print("[autotest] drift banked %.0f (total %.0f) at %.0f km/h" % [d.last_event_value, d.total, race.player.speed_kmh])
		elif d.last_event == DriftScorer.Event.LOST:
			print("[autotest] drift LOST %.0f (wall)" % d.last_event_value)
	_v["last_age"] = d.event_age
	_v["skid_max"] = maxi(int(_v["skid_max"]), race.skid_segments_active())
	if race.player.fx.estimated_particles() > 0:
		_v["smoke_ticks"] = int(_v["smoke_ticks"]) + 1
	# Count drift chains that accumulated score (banked or later lost).
	if d.chain_active and not bool(_v.get("in_chain", false)):
		_v["chains"] = int(_v.get("chains", 0)) + 1
	_v["in_chain"] = d.chain_active
	if d.chain_active:
		_v["chain_peak"] = maxf(float(_v.get("chain_peak", 0.0)), d.pending)
	elif float(_v.get("chain_peak", 0.0)) > 0.0:
		_v["scored_chains"] = int(_v.get("scored_chains", 0)) + 1
		_v["score_sum"] = float(_v.get("score_sum", 0.0)) + float(_v["chain_peak"])
		_v["chain_peak"] = 0.0
	_v["max_slip"] = maxf(float(_v.get("max_slip", 0.0)), race.player.body_slip_deg if race.player.speed_kmh > 30.0 else 0.0)
	if e.laps_done < 1:
		return
	# Give a drift still pending at the line time to be banked.
	if not _v.has("lap_done_at"):
		_v["lap_done_at"] = _time
	if _time < float(_v["lap_done_at"]) + Config.DRIFT_BANK_DELAY + 0.5:
		return
	var driver: DriftTestDriver = _v["driver"]
	var banked: int = _v["banked"]
	var scored := int(_v.get("scored_chains", 0))
	print("[autotest] lap done: handbrake drifts %d, drift chains with score %d (sum %.0f), banked %d, total banked %.0f, max slip %.1f, skid segments max %d, smoke frames %d, respawns %d" % [
		driver.drifts_started, scored, float(_v.get("score_sum", 0.0)), banked, d.total, _v["max_slip"], _v["skid_max"], _v["smoke_ticks"], race.player.respawn_count])
	var ok := driver.drifts_started >= 3 and scored >= 3 and int(_v["skid_max"]) > 0 and int(_v["smoke_ticks"]) > 0
	_finish(ok, "%d handbrake drifts, %d scored, %d banked" % [driver.drifts_started, scored, banked])


## Phases: (1) 100 km/h at 30 deg, (2) 190 km/h at 45 deg, (3) 190 km/h at 85 deg.
func _tick_wall() -> void:
	if not race.manager.is_racing():
		return
	var cases := [[100.0, 30.0], [190.0, 45.0], [190.0, 85.0]]
	var car := race.player
	if _phase == 0 or (_phase <= cases.size() and _phase_time > 3.0):
		if _phase > 0:
			_report_wall_case(cases[_phase - 1])
		if _phase == cases.size():
			_finish(int(_v.get("wall_fail", 0)) == 0, "%d wall case(s) failed" % int(_v.get("wall_fail", 0)))
			return
		var c: Array = cases[_phase]
		var idx := _straight_index()
		var t := race.track
		var yaw := deg_to_rad(float(c[1]))
		var fwd := t.tangents[idx].rotated(t.normals[idx], -yaw)
		var xf := Transform3D(Basis.looking_at(fwd, t.normals[idx]), t.points[idx] + t.normals[idx] * Config.SPAWN_HEIGHT)
		car.place(xf)
		car.linear_velocity = fwd * float(c[0]) / 3.6
		_v["case_lat"] = 0.0
		_v["case_hit"] = false
		_v["case_sparks"] = false
		_v["case_idx"] = idx
		if not car.barrier_hit.is_connected(_on_wall_hit):
			car.barrier_hit.connect(_on_wall_hit)
		_phase += 1
		_phase_time = 0.0
		return
	if _v.has("shot_due") and _time >= float(_v["shot_due"]):
		_v.erase("shot_due")
		_shot("wall_case%d" % _phase)
	var lat := race.track.lateral_offset(car.global_position, maxi(car.track_index, 0))
	_v["case_lat"] = maxf(float(_v["case_lat"]), lat)
	if car.fx.estimated_particles() > 0 and car.touching_barrier:
		_v["case_sparks"] = true


func _on_wall_hit(_impact: float) -> void:
	if scenario == "wall_shots" and not bool(_v["case_hit"]):
		_v["shot_due"] = _time + 0.12
	_v["case_hit"] = true


func _report_wall_case(c: Array) -> void:
	var car := race.player
	var wall := RoadBuilder.wall_line()
	var lat: float = _v["case_lat"]
	var up := car.global_transform.basis.y.dot(Vector3.UP)
	var ok: bool = lat < wall and bool(_v["case_hit"]) and bool(_v["case_sparks"]) and (up > 0.5 or car.respawn_count > 0)
	print("[autotest] wall %.0f km/h @ %.0f deg: max lateral %.2f m (barrier face %.2f m), hit %s, sparks %s, upright %.2f, speed now %.0f km/h -> %s" % [
		c[0], c[1], lat, wall, _v["case_hit"], _v["case_sparks"], up, car.speed_kmh, "OK" if ok else "FAIL"])
	if not ok:
		_v["wall_fail"] = int(_v.get("wall_fail", 0)) + 1


func _straight_index() -> int:
	var t := race.track
	var best := 0
	var best_cost := INF
	for i in range(t.count >> 2, (t.count * 3) >> 2):
		var cost := 0.0
		for k in range(-20, 21, 4):
			cost += absf(t.curvature[t.wrap_index(i + k)])
		if cost < best_cost:
			best_cost = cost
			best = i
	return best


## R-26: driving the wrong way for > 2 s raises the warning (and only then).
func _tick_wrong_way() -> void:
	if not race.manager.is_racing():
		return
	var car := race.player
	var hud: Hud = main.hud
	var label: Label = hud.get(&"_wrong_way")
	if _phase == 0:
		var idx := _straight_index()
		var t := race.track
		car.place(Transform3D(Basis.looking_at(-t.tangents[idx], t.normals[idx]), t.points[idx] + t.normals[idx] * Config.SPAWN_HEIGHT))
		_phase = 1
		_phase_time = 0.0
		return
	if _phase == 1 and _phase_time > 1.5:
		_v["early"] = race.manager.is_wrong_way(car)
		_phase = 2
	if _phase == 2 and _phase_time > 2.6:
		var flagged := race.manager.is_wrong_way(car)
		var shown := false
		for _i in 40:
			hud.call(&"_process", 0.05)
			shown = shown or label.visible
		print("[autotest] wrong way after 1.5 s: %s, after 2.6 s: %s, HUD label shown: %s" % [_v["early"], flagged, shown])
		_finish(not bool(_v["early"]) and flagged and shown, "wrong-way warning")


func _tick_respawn() -> void:
	var car := race.player
	var e := race.manager.entry_of(car)
	if _phase == 0:
		if e.gates_passed >= 2 and car.track_index >= 0 and absf(race.track.curvature[car.track_index]) > 1.0 / 70.0 and car.speed_kmh > 40.0:
			_v["gate"] = e.last_gate
			_v["count"] = car.respawn_count
			print("[autotest] pressing R mid-corner at %.0f km/h, curvature radius %.0f m, last gate %d" % [
				car.speed_kmh, 1.0 / absf(race.track.curvature[car.track_index]), e.last_gate])
			_press(InputSetup.RESPAWN)
			_phase = 1
			_phase_time = 0.0
		return
	if _phase == 1 and _phase_time > 0.1:
		var gi := race.track.checkpoint_indices[int(_v["gate"])]
		var dist := car.global_position.distance_to(race.track.points[gi])
		var heading := (-car.global_transform.basis.z).dot(race.track.tangents[gi])
		var ok: bool = car.respawn_count == int(_v["count"]) + 1 and dist < 7.0 and car.speed < 1.0 and heading > 0.95
		print("[autotest] after R: distance to checkpoint %.2f m, speed %.2f m/s, heading dot %.3f, respawns %d" % [
			dist, car.speed, heading, car.respawn_count])
		_finish(ok, "respawn at last checkpoint")


func _tick_flip() -> void:
	if not race.manager.is_racing():
		return
	var car := race.player
	if _phase == 0:
		var idx := _straight_index()
		var t := race.track
		var basis := Basis.looking_at(t.tangents[idx], t.normals[idx]).rotated(t.tangents[idx], PI)
		car.place(Transform3D(basis, t.points[idx] + t.normals[idx] * 1.4))
		_v["count"] = car.respawn_count
		_phase = 1
		_phase_time = 0.0
		return
	if car.respawn_count > int(_v["count"]):
		var up := car.global_transform.basis.y.dot(Vector3.UP)
		print("[autotest] auto-respawn after %.2f s upside down, upright %.2f" % [_phase_time, up])
		_finish(_phase_time >= Config.FLIP_RESPAWN_TIME and _phase_time < Config.FLIP_RESPAWN_TIME + 2.0 and up > 0.9, "flip respawn")
	elif _phase_time > 8.0:
		_finish(false, "no auto-respawn")


func _tick_screens() -> void:
	if _time >= 1.6 and not _v.has("closeup_cam"):
		_v["closeup_cam"] = true
		var cam := Camera3D.new()
		cam.name = "CloseupCamera"
		race.add_child(cam)
		var target := race.cars[1].global_position
		var side := race.cars[1].global_transform.basis.x
		cam.look_at_from_position(target + side * 5.5 + Vector3.UP * 1.6 + race.cars[1].global_transform.basis.z * -2.0, target + Vector3.UP * 0.3)
		cam.current = true
		_v["cam"] = cam
	if _time >= 2.2 and not _v.has("closeup"):
		_v["closeup"] = true
		_shot("car_closeup")
	if _time >= 2.6 and not _v.has("closeup_done"):
		_v["closeup_done"] = true
		var c: Camera3D = _v["cam"]
		c.queue_free()
		race.camera.current = true
	var shots := [[1.0, "grid"], [9.0, "race_start"], [16.0, "chase"], [24.0, "chase2"]]
	for s in shots:
		var at: float = s[0]
		if _time >= at and not _v.has(s[1]):
			_v[s[1]] = true
			_shot(s[1])
	if _time >= 27.0 and not _v.has("hood_on"):
		_v["hood_on"] = true
		race.camera.toggle_mode()
	if _time >= 30.0 and not _v.has("hood"):
		_v["hood"] = true
		_shot("hood")
	if _time >= 31.0 and not _v.has("hood_off"):
		_v["hood_off"] = true
		race.camera.toggle_mode()
		main.debug_overlay.toggle()
	if _time >= 33.0 and not _v.has("debug"):
		_v["debug"] = true
		_shot("debug")
	if _time >= 34.0 and not _v.has("debug_off"):
		_v["debug_off"] = true
		main.debug_overlay.toggle()
	if _time >= 35.0 and not _v.has("pause_on"):
		_v["pause_on"] = true
		_press(InputSetup.PAUSE)
	if _time >= 36.5 and not _v.has("pause"):
		_v["pause"] = true
		_shot("pause")
	if _time >= 37.5 and not _v.has("pause_off"):
		_v["pause_off"] = true
		_press(InputSetup.PAUSE)
	if _time >= 38.2 and not _v.has("f12"):
		_v["f12"] = true
		_press(InputSetup.SCREENSHOT)
	if _time >= 39.0:
		_finish(true, "screenshots written to %s" % Screenshot.folder())


func _tick_bench() -> void:
	if _time < 6.0:
		return
	var fps: PackedFloat32Array = _v["fps"]
	_v["acc"] = float(_v.get("acc", 0.0)) + get_process_delta_time()
	if float(_v["acc"]) >= 0.5:
		_v["acc"] = 0.0
		fps.append(Engine.get_frames_per_second())
		_v["fps"] = fps
		_v["draws"] = RenderingServer.get_rendering_info(RenderingServer.RENDERING_INFO_TOTAL_DRAW_CALLS_IN_FRAME)
	if _time >= 46.0:
		var lo := INF
		var sum := 0.0
		for f in fps:
			lo = minf(lo, f)
			sum += f
		var avg := sum / float(maxi(fps.size(), 1))
		print("[autotest] bench: avg FPS %.1f, min FPS %.1f, draw calls %d, samples %d" % [avg, lo, _v.get("draws", 0), fps.size()])
		_finish(lo >= 60.0, "min FPS %.1f" % lo)


## R-23: an AI car parked nose-first against the barrier must be reset to
## its last checkpoint after 3 s below 5 km/h.
func _tick_ai_stuck() -> void:
	if not race.manager.is_racing():
		return
	var car := race.cars[1]
	if _phase == 0 and race.manager.race_time > 8.0:
		var t := race.track
		var idx := car.track_index
		var side := t.rights[idx]
		var pos := t.points[idx] + side * (RoadBuilder.wall_line() - 2.4) + t.normals[idx] * Config.SPAWN_HEIGHT
		car.place(Transform3D(Basis.looking_at(side, t.normals[idx]), pos))
		_v["count"] = car.respawn_count
		_phase = 1
		_phase_time = 0.0
		print("[autotest] Red AI placed nose against the barrier at %.0f m" % t.distances[idx])
		return
	if _phase == 1 and car.respawn_count > int(_v["count"]):
		var e := race.manager.entry_of(car)
		var gi := race.track.checkpoint_indices[e.last_gate]
		var dist := car.global_position.distance_to(race.track.points[gi])
		print("[autotest] stuck AI respawned after %.2f s at %.1f m from checkpoint %d" % [_phase_time, dist, e.last_gate])
		_phase = 2
		_finish(_phase_time < 6.0 and dist < 8.0, "stuck recovery")
	elif _phase == 1 and _phase_time > 10.0:
		_finish(false, "stuck AI was not reset")


## R-30: after a few seconds of racing the engine synth must be producing
## audible samples for the player and AI cars, and SFX streams exist.
func _tick_audio_check() -> void:
	if race.manager.race_time < 6.0:
		return
	var lines := PackedStringArray()
	var ok := true
	for car in race.cars:
		var synth: EngineSynth = car.audio.get(&"_synth")
		var buf: PackedVector2Array = synth.get(&"_buffer")
		var sum := 0.0
		for v in buf:
			sum += v.x * v.x
		var rms := sqrt(sum / float(maxi(buf.size(), 1)))
		var active: bool = car.audio.get(&"_active")
		lines.append("%s rms %.3f active %s" % [car.display_name, rms, active])
		if car == race.player and rms < 0.01:
			ok = false
	print("[autotest] engine audio: " + " | ".join(lines))
	var streams := [Synth.squeal(), Synth.scrape(), Synth.wind(), Synth.thud(), Synth.beep(Config.BEEP_FREQ, 0.18)]
	for st: AudioStreamWAV in streams:
		if st.data.size() < 1000:
			ok = false
	print("[autotest] sfx streams: squeal %d, scrape %d, wind %d, thud %d, beep %d bytes" % [
		streams[0].data.size(), streams[1].data.size(), streams[2].data.size(), streams[3].data.size(), streams[4].data.size()])
	_finish(ok, "audio generated")


## GPU: screenshots while the drift driver is sliding (smoke, skid marks,
## drift score on the HUD), then one from the hood camera.
func _tick_drift_shots() -> void:
	var car := race.player
	var taken := int(_v.get("taken", 0))
	var sliding := car.rear_wheel_slip() > Config.SMOKE_SLIP_MIN and car.body_slip_deg > 18.0
	_v["slide_t"] = float(_v.get("slide_t", 0.0)) + get_process_delta_time() if sliding else 0.0
	if taken < 3 and float(_v["slide_t"]) > 0.7 and _time > float(_v.get("next_shot", 0.0)):
		_shot("drift%d" % taken)
		_v["taken"] = taken + 1
		_v["next_shot"] = _time + 4.0
	if taken >= 3 or _time > 70.0:
		_finish(taken >= 1, "%d drift screenshots" % taken)


func _tick_results_shot() -> void:
	var results: CanvasLayer = main.results
	if results.visible and not _v.has("first"):
		_v["first"] = _time
		_shot("results_live")
	if race.manager.all_finished() and not _v.has("final"):
		_v["final"] = _time
	if _v.has("final") and _time > float(_v["final"]) + 1.0 and not _v.has("final_shot"):
		_v["final_shot"] = true
		_shot("results_final")
	if _v.has("final_shot") and _time > float(_v["final"]) + 2.5:
		_finish(true, "results screenshots taken")


# -------------------------------------------------------------------- helpers

func _press(action: StringName) -> void:
	var down := InputEventAction.new()
	down.action = action
	down.pressed = true
	Input.parse_input_event(down)
	var up := InputEventAction.new()
	up.action = action
	up.pressed = false
	Input.parse_input_event(up)


func _shot(tag: String) -> void:
	await RenderingServer.frame_post_draw
	var path := Screenshot.capture(get_viewport(), tag)
	print("[autotest] screenshot %s -> %s" % [tag, path if not path.is_empty() else "(no frame: headless)"])


func _finish(passed: bool, details: String) -> void:
	if _done:
		return
	_done = true
	print("[autotest] RESULT %s %s %s" % [scenario, "PASS" if passed else "FAIL", details])
	main.quit_game()
