class_name PhysicsLab
extends Node3D
## Autotest "physics_lab": one car on an endless flat pad running scripted
## manoeuvres (settle, full-throttle run, braking, skidpad, step steer,
## handbrake drift, power oversteer, reverse) and printing measurements used
## to tune and verify R-07..R-13. Only used by the autotest runner.

signal finished(passed: bool, details: String)


class LabDriver:
	extends CarController
	var throttle := 0.0
	var brake := 0.0
	var steer := 0.0
	var handbrake := false

	func update_controls(car: Car, _delta: float) -> void:
		car.throttle = throttle
		car.brake = brake
		car.steer_input = steer
		car.handbrake = handbrake


var car: Car
var driver: LabDriver
var _step := 0
var _t := 0.0
var _m := {}
var _min_up := 1.0
var _max_slip := 0.0
var _slip_time := 0.0
var _start_pos := Vector3.ZERO
var _start_speed := 0.0
var _phase_flag := false


func start() -> void:
	name = "PhysicsLab"
	var ground := StaticBody3D.new()
	ground.collision_layer = Config.LAYER_ROAD
	var shape := BoxShape3D.new()
	shape.size = Vector3(8000.0, 1.0, 8000.0)
	var cs := CollisionShape3D.new()
	cs.shape = shape
	cs.position = Vector3(0.0, -0.5, 0.0)
	ground.add_child(cs)
	add_child(ground)
	car = Car.new()
	add_child(car)
	car.setup(0, true, null, null, null)
	car.controls_locked = false
	driver = LabDriver.new()
	car.add_child(driver)
	car.controller = driver
	_reset_car()


func _reset_car() -> void:
	car.place(Transform3D(Basis.IDENTITY, Vector3(0.0, Config.SPAWN_HEIGHT, 0.0)))
	driver.throttle = 0.0
	driver.brake = 0.0
	driver.steer = 0.0
	driver.handbrake = false
	_t = 0.0
	_min_up = 1.0
	_max_slip = 0.0
	_slip_time = 0.0
	_phase_flag = false


func _next() -> void:
	_step += 1
	_reset_car()


func _physics_process(delta: float) -> void:
	if car == null:
		return
	_t += delta
	_min_up = minf(_min_up, car.global_transform.basis.y.dot(Vector3.UP))
	_max_slip = maxf(_max_slip, car.body_slip_deg)
	match _step:
		0:
			if _t > 1.5:
				var loads := 0.0
				for w in 4:
					loads += car.wheel_load[w]
				_m["rest_height"] = car.global_position.y
				_m["load_ratio"] = loads / (Config.CAR_MASS * Config.GRAVITY)
				_m["rest_compression"] = car.wheel_compression[0]
				print("[lab] settle: origin y %.3f m, wheel load sum / mg = %.3f, compression %.3f m" % [
					car.global_position.y, _m["load_ratio"], car.wheel_compression[0]])
				_next()
		1:
			driver.throttle = 1.0
			if not _m.has("t100") and car.speed_kmh >= 100.0:
				_m["t100"] = _t
			_m["vmax"] = maxf(_m.get("vmax", 0.0), car.speed_kmh)
			if _t > 45.0:
				print("[lab] accel: 0-100 %.2f s, top speed %.1f km/h, gear %d, rpm %.0f, min up %.2f" % [
					_m.get("t100", -1.0), _m["vmax"], car.drivetrain.gear, car.drivetrain.rpm, _min_up])
				_start_speed = car.speed_kmh
				_start_pos = car.global_position
				_step = 2
				_t = 0.0
		2:
			driver.throttle = 0.0
			driver.brake = 1.0
			if car.speed < 0.3:
				var dist := car.global_position.distance_to(_start_pos)
				_m["brake_dist"] = dist
				print("[lab] brake: from %.0f km/h stopped in %.1f m, %.2f s, max slip %.1f deg" % [_start_speed, dist, _t, _max_slip])
				_next()
		3:
			_speed_hold(60.0)
			driver.steer = 0.35
			if _t > 10.0:
				var yaw := car.angular_velocity.y
				var lat_g := absf(car.speed * yaw) / Config.GRAVITY
				_m["skidpad_g"] = lat_g
				print("[lab] skidpad 60 km/h steer 0.35: speed %.1f km/h, lateral %.2f g, body slip %.1f deg, radius %.1f m" % [
					car.speed_kmh, lat_g, car.body_slip_deg, car.speed / maxf(absf(yaw), 0.001)])
				_next()
		4:
			if not _phase_flag:
				driver.throttle = 1.0
				if car.speed_kmh >= 120.0:
					_phase_flag = true
					_t = 0.0
					_min_up = 1.0
					_max_slip = 0.0
			else:
				driver.throttle = 0.5
				driver.steer = 1.0
				if _t > 2.5:
					_m["step_min_up"] = _min_up
					print("[lab] step steer at 120 km/h: min up %.2f, max body slip %.1f deg, speed now %.0f km/h, lateral %.2f g" % [
						_min_up, _max_slip, car.speed_kmh, absf(car.speed * car.angular_velocity.y) / Config.GRAVITY])
					_next()
		5:
			_handbrake_step(delta)
		6:
			if not _phase_flag:
				_speed_hold(45.0)
				if _t > 6.0:
					_phase_flag = true
					_t = 0.0
					_max_slip = 0.0
			else:
				driver.throttle = 1.0
				driver.steer = 0.6
				if _t > 3.0:
					_m["power_slip"] = _max_slip
					print("[lab] power oversteer (gear %d, steer 0.6, full throttle): max body slip %.1f deg, min up %.2f" % [
						car.drivetrain.gear, _max_slip, _min_up])
					_next()
		7:
			driver.brake = 1.0
			if _t > 3.0:
				_m["reverse_speed"] = -car.forward_speed
				print("[lab] reverse after 3 s of S: %.1f km/h backwards, gear %d" % [-car.forward_speed * 3.6, car.drivetrain.gear])
				_next()
		8:
			_evaluate()
			_step = 9


func _handbrake_step(delta: float) -> void:
	if not _phase_flag:
		_speed_hold(75.0)
		if _t > 7.0:
			_phase_flag = true
			_t = 0.0
			_max_slip = 0.0
			_slip_time = 0.0
		return
	if _t < 0.6:
		driver.steer = 1.0
		driver.handbrake = true
		driver.throttle = 0.0
	else:
		driver.handbrake = false
		driver.throttle = 0.75
		driver.steer = 0.45
	if car.body_slip_deg >= Config.DRIFT_MIN_SLIP_DEG and car.body_slip_deg <= Config.DRIFT_MAX_SLIP_DEG and car.speed_kmh > Config.DRIFT_MIN_SPEED_KMH:
		_slip_time += delta
	if _t > 5.0:
		_m["hb_slip"] = _max_slip
		_m["hb_drift_time"] = _slip_time
		print("[lab] handbrake drift from 75 km/h: max body slip %.1f deg, drift-qualifying time %.2f s, end slip %.1f deg, speed %.0f km/h" % [
			_max_slip, _slip_time, car.body_slip_deg, car.speed_kmh])
		_next()


func _speed_hold(kmh: float) -> void:
	var err := kmh - car.speed_kmh
	driver.throttle = clampf(err * 0.08 + 0.15, 0.0, 1.0)
	driver.brake = clampf(-err * 0.05, 0.0, 1.0)


func _evaluate() -> void:
	var problems := PackedStringArray()
	if absf(float(_m.get("load_ratio", 0.0)) - 1.0) > 0.05:
		problems.append("static wheel loads do not carry the weight")
	var vmax: float = _m.get("vmax", 0.0)
	if vmax < 180.0 or vmax > 200.0:
		problems.append("top speed %.0f km/h not ~190" % vmax)
	if float(_m.get("t100", 99.0)) > 9.0:
		problems.append("0-100 too slow")
	if float(_m.get("step_min_up", 0.0)) < 0.6:
		problems.append("car rolled during step steer")
	if float(_m.get("hb_slip", 0.0)) < Config.DRIFT_MIN_SLIP_DEG:
		problems.append("handbrake does not start a drift")
	if float(_m.get("reverse_speed", 0.0)) < 2.0:
		problems.append("reverse does not work")
	finished.emit(problems.is_empty(), "; ".join(problems) if not problems.is_empty() else "all lab checks passed")
