class_name RaceDriver
extends RefCounted

const C = preload("res://game/config.gd")

var line := PackedFloat32Array()
var pace := PackedFloat32Array()
var offsets := PackedFloat32Array()
var avoidance := PackedFloat32Array()
var course: TrackData

func setup(data: TrackData, seed_value: int) -> void:
	course = data
	line.resize(data.points.size())
	pace.resize(line.size())
	for i in line.size():
		var bend := 0.0
		for offset in range(-12, 13, 3):
			bend += data.curvature[data.wrap_index(i + offset)]
		line[i] = clampf(bend / (9.0 * 0.022), -1.0, 1.0) * C.LINE_OFFSET
		pace[i] = minf(C.TOP_SPEED, sqrt(C.LAT_ACCEL * data.radii[i]))
	# Back-propagate braking limits through the entire closed loop, including the seam.
	for i in range(line.size() * 2 - 1, -1, -1):
		var slot := data.wrap_index(i)
		var after := pace[data.wrap_index(slot + 1)]
		pace[slot] = minf(pace[slot], sqrt(after * after + 2.0 * 5.7 * data.step))
	var rng := RandomNumberGenerator.new()
	rng.seed = seed_value ^ 73127
	offsets.resize(C.CAR_COUNT)
	avoidance.resize(C.CAR_COUNT)
	for i in C.CAR_COUNT:
		offsets[i] = rng.randf_range(-0.68, 0.68)

func command(car: DriftCar, cars: Array[DriftCar], difficulty: int, delta: float) -> void:
	var i := car.road_index
	if i < 0:
		return
	var requested_side := 0.0
	var closing_brake := 0.0
	for other in cars:
		if car == other:
			continue
		var separation := other.global_position - car.global_position
		var ahead := separation.dot(-car.global_basis.z)
		var side := separation.dot(car.global_basis.x)
		if ahead > 0.0 and ahead < C.AVOID_RANGE and absf(side) < 2.25:
			var road_side := (car.global_position - course.points[i]).dot(course.right[i])
			if absf(road_side) > 3.4:
				requested_side = -signf(road_side) * 2.75
			else:
				requested_side = -signf(side + 0.06) * 2.75
		if ahead > 0.0 and ahead < 15.0 and absf(side) < 1.9:
			var closing := (car.linear_velocity - other.linear_velocity).dot(-car.global_basis.z)
			if closing > 1.8:
				var urgency := clampf((closing * 0.50 + 4.2 - ahead) * 0.28, 0.0, 1.0)
				closing_brake = maxf(closing_brake, urgency)
	avoidance[car.driver_id] = move_toward(avoidance[car.driver_id], requested_side, delta * 6.3)
	var look := clampf(6.0 + 0.42 * car.speed, 6.0, 29.0)
	var target_index := course.index_at(course.progress(car.global_position, i) + look)
	var offset := clampf(line[target_index] + offsets[car.driver_id] + avoidance[car.driver_id],
		-C.LINE_OFFSET - 1.0, C.LINE_OFFSET + 1.0)
	var target := course.points[target_index] + course.right[target_index] * offset
	var local_target := car.global_basis.transposed() * (target - car.global_position)
	var geometric_angle := atan2(2.0 * C.WHEELBASE * local_target.x, maxf(1.0, local_target.length_squared()))
	var limit := deg_to_rad(lerpf(C.MAX_STEER, C.MIN_STEER, sqrt(clampf(car.speed / C.TOP_SPEED, 0.0, 1.0))))
	car.steer_input = clampf(geometric_angle * 1.58 / limit, -1.0, 1.0)
	if local_target.z > 0.0:
		car.steer_input = signf(local_target.x)
	var target_speed := pace[course.wrap_index(i + 2)] * float(C.DIFFICULTIES[difficulty])
	var separation_to_player := car.course_progress - cars[0].course_progress
	if car.driver_id != 0 and absf(separation_to_player) > C.RUBBER_RANGE:
		target_speed *= 1.0 - signf(separation_to_player) * C.RUBBER_RATE
	target_speed = minf(target_speed, C.TOP_SPEED)
	if local_target.z > -1.0:
		target_speed = minf(target_speed, 8.0)
	var error := target_speed - car.speed
	car.throttle = clampf(error * 0.24 + 0.065, 0.0, 1.0) * (1.0 - closing_brake)
	car.brake = maxf(closing_brake, clampf(-error * 0.20 - 0.12, 0.0, 1.0))
	car.handbrake = false
