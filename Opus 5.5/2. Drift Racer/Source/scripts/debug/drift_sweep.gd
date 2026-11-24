class_name DriftSweep
extends PhysicsLab
## Autotest "drift_sweep" (tuning aid): repeats a handbrake-initiated drift on
## the flat pad with different assist gains / angular damping / held throttle
## and steering, and prints how long the car stays in the drift window and
## whether it spins. Used to tune R-07 angular damping and R-11 assist.

var _cases: Array = []
var _case := 0
var _drift_time := 0.0
var _spun := false
var _trace := PackedStringArray()
var _trace_t := 0.0


func start() -> void:
	super.start()
	# [assist gain, input scale while sliding, throttle, steer]. Steer -1 =
	# keyboard style (key pressed below 24 deg slip, released above 32 deg);
	# steer 0 after the kick checks that releasing the key recovers the car.
	for gs in [[0.9, 0.5], [0.95, 0.55], [1.0, 0.65], [1.05, 0.75]]:
		for thr in [0.7, 1.0]:
			for st in [0.4, 0.7, 1.0, 0.0]:
				_cases.append([gs[0], gs[1], thr, st])
	_begin_case()


func _begin_case() -> void:
	car.place(Transform3D(Basis.IDENTITY, Vector3(0.0, Config.SPAWN_HEIGHT, 0.0)))
	var c: Array = _cases[_case]
	car.assist_gain = c[0]
	car.assist_input_scale = c[1]
	driver.handbrake = false
	driver.steer = 0.0
	_t = 0.0
	_phase_flag = false
	_max_slip = 0.0
	_drift_time = 0.0
	_spun = false
	_trace.clear()
	_trace_t = 0.0


func _physics_process(delta: float) -> void:
	if car == null or _case >= _cases.size():
		return
	_t += delta
	var c: Array = _cases[_case]
	if not _phase_flag:
		_speed_hold(75.0)
		if _t > 7.0:
			_phase_flag = true
			_t = 0.0
		return
	if _t < 0.5:
		driver.steer = 1.0
		driver.handbrake = true
		driver.throttle = 0.0
		driver.brake = 0.0
	else:
		driver.handbrake = false
		driver.throttle = c[2]
		if float(c[3]) >= 0.0:
			driver.steer = c[3]
		elif car.body_slip_deg < 24.0:
			driver.steer = 1.0
		elif car.body_slip_deg > 32.0:
			driver.steer = 0.0
	var slip := car.body_slip_deg
	_max_slip = maxf(_max_slip, slip)
	if slip > 100.0:
		_spun = true
	if slip >= Config.DRIFT_MIN_SLIP_DEG and slip <= Config.DRIFT_MAX_SLIP_DEG and car.speed_kmh > Config.DRIFT_MIN_SPEED_KMH:
		_drift_time += delta
	_trace_t += delta
	if _trace_t >= 0.5:
		_trace_t = 0.0
		_trace.append("%.0f" % slip)
	if _t > 6.0:
		print("[sweep] gain %.2f scale %.2f thr %.2f steer %.1f -> drift %.2f s, max slip %.0f, spun %s, end %.0f km/h, slip trace %s" % [
			c[0], c[1], c[2], c[3], _drift_time, _max_slip, _spun, car.speed_kmh, " ".join(_trace)])
		_case += 1
		if _case >= _cases.size():
			finished.emit(true, "sweep done")
			return
		_begin_case()
