class_name RaceSession
extends Node3D

signal countdown_changed(value: int)
signal race_finished
signal respawned(car: DriftCar)

const C = preload("res://game/config.gd")

var track: RaceTrack
var cars: Array[DriftCar] = []
var player: DriftCar
var ai := RaceDriver.new()
var drift := DriftScore.new()
var difficulty := 1
var seed_value := C.DEFAULT_SEED
var remaining := C.COUNTDOWN
var count := -1
var elapsed := 0.0
var running := false
var complete := false
var player_autopilot := false
var _finished := 0

func build(course_seed: int, skill: int) -> void:
	seed_value = course_seed
	difficulty = skill
	track = RaceTrack.new()
	track.build(seed_value)
	add_child(track)
	track.gate_entered.connect(_gate)
	ai.setup(track.data, seed_value)
	for i in C.CAR_COUNT:
		var car := DriftCar.new()
		car.name = C.DRIVER_NAMES[i]
		car.build(i)
		car.freeze = true
		add_child(car)
		var behind := C.GRID_BACK + floorf(i * 0.5) * C.GRID_ROW + (i % 2) * C.GRID_STAGGER
		var lateral := -C.GRID_SIDE if i % 2 == 0 else C.GRID_SIDE
		car.reset_at(track.data.frame_at(-behind, lateral, C.RIDE_HEIGHT))
		car.road_index = track.data.nearest(car.global_position)
		car.course_progress = -behind
		cars.append(car)
	player = cars[0]

func _physics_process(delta: float) -> void:
	if complete or cars.is_empty():
		return
	if not running:
		remaining = maxf(0.0, remaining - delta)
		var next_count := ceili(remaining)
		if next_count != count:
			count = next_count
			countdown_changed.emit(count)
		if remaining > 0.0:
			return
		running = true
		for car in cars:
			car.freeze = false
		return
	elapsed += delta
	for car in cars:
		_update_progress(car, delta)
		if car == player and not player_autopilot and car.finish_time <= 0.0:
			DriverInput.read(car)
		else:
			ai.command(car, cars, difficulty, delta)
			car.stuck_time = car.stuck_time + delta if car.speed * C.KMH < C.STUCK_KMH else 0.0
			if car.stuck_time >= C.STUCK_TIME or car.wall_seconds > 2.1:
				reset_car(car)
		if car.roof_seconds >= C.ROOF_RESET:
			reset_car(car)
		# This also handles a rare out-of-bounds body after a hard car-to-car impact.
		var track_point := track.data.points[car.road_index]
		if car.global_position.distance_squared_to(track_point) > 19.0 * 19.0:
			reset_car(car)
	drift.update(player, delta, player.finish_time <= 0.0)
	if _finished == C.CAR_COUNT and drift.pending <= 0.0:
		complete = true
		_finish()

func _update_progress(car: DriftCar, delta: float) -> void:
	var data := track.data
	car.road_index = data.nearest(car.global_position, car.road_index)
	var along := data.progress(car.global_position, car.road_index)
	# Checkpoint 7 certifies the approach to the line, preventing a backwards crossing
	# from jumping a car a full lap in the position list.
	if car.next_checkpoint == 0 and along < data.length * 0.25:
		along += data.length
	if car.next_checkpoint == 1 and along > data.length * 0.75:
		along -= data.length
	if car.next_checkpoint == 0:
		along -= data.length if car.lap_times.is_empty() and car.last_checkpoint == 0 else 0.0
	car.course_progress = car.completed_laps * data.length + along
	var backwards := (-car.global_basis.z).dot(data.forward[car.road_index]) < -0.28
	car.wrong_way = car.wrong_way + delta if backwards else 0.0

func _gate(index: int, node: Node3D) -> void:
	var car := node as DriftCar
	if not running or complete or car == null or car.finish_time > 0.0 or index != car.next_checkpoint:
		return
	var gate_index := track.data.index_at(index * track.data.length / C.CHECKPOINTS)
	if car.linear_velocity.dot(track.data.forward[gate_index]) <= 0.5:
		return
	if index == 0 and car.last_checkpoint == C.CHECKPOINTS - 1:
		car.completed_laps += 1
		var duration := elapsed - car.lap_start
		car.lap_times.append(duration)
		car.best_lap = duration if car.best_lap <= 0.0 else minf(car.best_lap, duration)
		car.lap_start = elapsed
		if car.completed_laps == C.LAPS:
			car.finish_time = elapsed
			_finished += 1
	car.last_checkpoint = index
	car.next_checkpoint = (index + 1) % C.CHECKPOINTS

func _finish() -> void:
	for car in cars:
		car.freeze = true
	race_finished.emit()

func reset_car(car: DriftCar) -> void:
	var spacing := track.data.length / C.CHECKPOINTS
	var distance := car.last_checkpoint * spacing + 3.3
	if car.next_checkpoint == 0 and car.last_checkpoint == 0 and car.completed_laps == 0:
		distance = -C.GRID_BACK
	var lane := 0.0 if car == player else (1.15 if car.driver_id % 2 else -1.15)
	car.reset_at(track.data.frame_at(distance, lane, C.RIDE_HEIGHT))
	car.road_index = track.data.index_at(distance)
	car.recoveries += 1
	car.wrong_way = 0.0
	respawned.emit(car)

func standings() -> Array[DriftCar]:
	var order := cars.duplicate()
	order.sort_custom(_ahead)
	return order

func _ahead(a: DriftCar, b: DriftCar) -> bool:
	if a.finish_time > 0.0 and b.finish_time > 0.0:
		if is_equal_approx(a.finish_time, b.finish_time):
			return a.course_progress > b.course_progress
		return a.finish_time < b.finish_time
	if a.finish_time > 0.0 or b.finish_time > 0.0:
		return a.finish_time > 0.0
	return a.course_progress > b.course_progress
