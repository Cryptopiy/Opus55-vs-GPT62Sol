extends SceneTree

const C = preload("res://game/config.gd")
const SESSION = preload("res://game/race_session.gd")

func _initialize() -> void:
	call_deferred("_run")

func _run() -> void:
	var seed_value := C.DEFAULT_SEED
	var autostart := false
	var difficulty := 1
	for arg in OS.get_cmdline_user_args():
		if arg.is_valid_int():
			seed_value = int(arg)
		if arg == "all":
			autostart = true
		if arg == "easy":
			difficulty = 0
		if arg == "hard":
			difficulty = 2
	var race := SESSION.new()
	root.add_child(race)
	race.build(seed_value, difficulty)
	race.player_autopilot = autostart
	var parked := false
	var avoiding := false
	var max_lateral := 0.0
	var impacts := 0
	var peak_speed := 0.0
	var fast := INF
	var slow := 0.0
	var passed := false
	for frame in 36000:
		await physics_frame
		if race.running and not autostart and not parked:
			race.player.reset_at(race.track.data.frame_at(43.0, 5.8, C.RIDE_HEIGHT))
			race.player.road_index = race.track.data.index_at(43.0)
			race.player.freeze = true
			parked = true
		for id in range(1, C.CAR_COUNT):
			avoiding = avoiding or absf(race.ai.avoidance[id]) > 0.1
		for car in race.cars:
			max_lateral = maxf(max_lateral, absf((car.global_position - race.track.data.points[car.road_index]).dot(race.track.data.right[car.road_index])))
			impacts += 1 if car.wall_impact > 300.0 else 0
			peak_speed = maxf(peak_speed, car.speed * C.KMH)
		if frame > 0 and frame % 7200 == 0:
			for car in race.cars:
				print("t=%.0f id=%d lap=%d gate=%d idx=%d speed=%.0f resets=%d" %
					[race.elapsed, car.driver_id, car.completed_laps, car.next_checkpoint, car.road_index, car.speed * C.KMH, car.recoveries])
		var finished := 0
		for car in race.cars:
			if car.finish_time > 0.0:
				finished += 1
		if finished == (C.CAR_COUNT if autostart else C.OPPONENTS):
			passed = true
			break
	for car in race.standings():
		print("%s: finish %.2fs, laps %s, recoveries %d" % [car.name, car.finish_time, car.lap_times, car.recoveries])
		if car.driver_id != 0:
			for lap in car.lap_times:
				fast = minf(fast, lap)
				slow = maxf(slow, lap)
	print("AI: seed=%d length=%.1f max lateral=%.2f m wall impulses=%d peak=%.1f km/h lap spread=%.2f%%" %
		[seed_value, race.track.data.length, max_lateral, impacts, peak_speed, (slow / fast - 1.0) * 100.0])
	print("AI: traffic avoidance applied=%s; player parked=%s" % [avoiding, parked])
	quit(0 if passed and (autostart or slow / fast < 1.15) and max_lateral < 7.8 else 1)
