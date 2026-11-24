class_name CarFx
extends Node3D
## Per-car effects: feeds skid-mark segments while a wheel slides (R-15),
## tyre smoke at both rear wheels scaled by slip (R-16) and sparks at the
## barrier contact while scraping above 20 km/h or right after an impact
## (R-17). Emitters are created once and only toggled (no spawning).

var is_scraping := false

var _car: Car
var _skids: SkidMarks
var _smoke: Array[GPUParticles3D] = []
var _sparks: GPUParticles3D
var _last_mark := PackedVector3Array([Vector3.ZERO, Vector3.ZERO, Vector3.ZERO, Vector3.ZERO])
var _has_mark := PackedByteArray([0, 0, 0, 0])
var _impact_timer := 0.0


func setup(car: Car, skids: SkidMarks) -> void:
	name = "Fx"
	_car = car
	_skids = skids
	for _k in 2:
		var smoke := FxFactory.make_smoke()
		add_child(smoke)
		_smoke.append(smoke)
	_sparks = FxFactory.make_sparks()
	add_child(_sparks)
	car.barrier_hit.connect(_on_barrier_hit)


func reset_trails() -> void:
	_has_mark.fill(0)
	_impact_timer = 0.0


func _on_barrier_hit(impact_speed: float) -> void:
	if impact_speed > Config.IMPACT_MIN_SPEED:
		_impact_timer = Config.IMPACT_BURST_TIME


func physics_update(delta: float) -> void:
	for w in Car.WHEEL_COUNT:
		var slip := _car.wheel_slip[w]
		if _car.wheel_grounded[w] == 1 and slip > Config.SKID_SLIP_THRESHOLD:
			var p := _car.wheel_contact[w]
			if _has_mark[w] == 0:
				_last_mark[w] = p
				_has_mark[w] = 1
			elif p.distance_squared_to(_last_mark[w]) >= Config.SKID_SEGMENT_LENGTH * Config.SKID_SEGMENT_LENGTH:
				var strength := clampf(0.45 + (slip - Config.SKID_SLIP_THRESHOLD) / 8.0, 0.45, 1.0)
				if _skids != null:
					_skids.add_segment(_last_mark[w], p, _car.wheel_normal[w], strength)
				_last_mark[w] = p
		else:
			_has_mark[w] = 0
	for k in 2:
		var w := 2 + k
		var slip := _car.wheel_slip[w]
		var emitter := _smoke[k]
		var on := _car.wheel_grounded[w] == 1 and slip > Config.SMOKE_SLIP_MIN
		if on:
			emitter.global_position = _car.wheel_contact[w] + Vector3.UP * 0.3
			emitter.amount_ratio = clampf(0.35 + (slip - Config.SMOKE_SLIP_MIN) / (Config.SMOKE_SLIP_FULL - Config.SMOKE_SLIP_MIN), 0.35, 1.0)
		if emitter.emitting != on:
			emitter.emitting = on
	is_scraping = _car.touching_barrier and _car.speed_kmh > Config.SPARK_MIN_SPEED_KMH
	if _impact_timer > 0.0:
		_impact_timer -= delta
	var sparks_on := is_scraping or (_impact_timer > 0.0 and _car.touching_barrier)
	if sparks_on:
		# Local -Z = backwards along the car, +Y = away from the barrier.
		var back := _car.global_transform.basis.z
		var n := _car.scrape_normal
		if absf(back.dot(n)) < 0.95:
			_sparks.global_transform = Transform3D(Basis.looking_at(back, n), _car.scrape_point + n * 0.05)
		_sparks.amount_ratio = 1.0 if _impact_timer > 0.0 else clampf(_car.speed_kmh / 120.0, 0.35, 1.0)
	if _sparks.emitting != sparks_on:
		_sparks.emitting = sparks_on


## Estimated live particles (GPU particles cannot be counted from the CPU).
func estimated_particles() -> int:
	var n := 0.0
	for s in _smoke:
		if s.emitting:
			n += float(s.amount) * s.amount_ratio
	if _sparks.emitting:
		n += float(_sparks.amount) * _sparks.amount_ratio
	return int(n)
