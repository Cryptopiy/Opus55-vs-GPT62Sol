class_name Drivetrain
extends RefCounted
## Rear-wheel-drive engine + automatic 6-speed gearbox (R-09): RPM 1000-7500,
## upshift at 7000, downshift at 3500, torque cut while shifting, reverse
## when stationary with brake held, rev limiter giving ~190 km/h top speed.

const GEAR_REVERSE := -1

var gear := 1
var rpm := Config.RPM_IDLE
var shift_timer := 0.0
var shift_event := 0          # +1 up / -1 down, cleared by the audio after use
var engine_load := 0.0        # 0..1 smoothed throttle load (sound timbre)
var drive_force := 0.0        # total force at the rear axle (+ = forward)
var engine_brake := 0.0       # resisting force when off throttle (N)
var brake_request := 0.0      # brake pedal after reverse logic (0..1)

var _first_ratio: float = Config.GEAR_RATIOS[0]


func reset() -> void:
	gear = 1
	rpm = Config.RPM_IDLE
	shift_timer = 0.0
	drive_force = 0.0
	engine_brake = 0.0
	brake_request = 0.0


func gear_ratio() -> float:
	if gear == GEAR_REVERSE:
		return Config.REVERSE_RATIO
	var r: float = Config.GEAR_RATIOS[gear - 1]
	return r


func is_reverse() -> bool:
	return gear == GEAR_REVERSE


## forward_speed: body speed along the heading (m/s). wheel_speed: average rear
## wheel surface speed (m/s, includes wheelspin). locked: countdown (engine may
## rev, no drive). clutch_in: handbrake pulled (drive disengaged).
func update(delta: float, forward_speed: float, wheel_speed: float, throttle: float,
		brake: float, locked: bool, clutch_in: bool) -> void:
	var drive_input := throttle
	brake_request = brake
	# Reverse: S engages it only when (nearly) stationary; W returns to 1st.
	if gear == GEAR_REVERSE:
		drive_input = brake
		brake_request = throttle
		if throttle > 0.1 and absf(forward_speed) < Config.STOP_SPEED:
			gear = 1
			drive_input = throttle
			brake_request = 0.0
	elif not locked and brake > 0.1 and throttle < 0.1 and absf(forward_speed) < Config.STOP_SPEED:
		gear = GEAR_REVERSE
		drive_input = brake
		brake_request = 0.0

	var ratio := gear_ratio() * Config.FINAL_DRIVE
	var free_rev := locked or clutch_in
	var shaft_speed := absf(forward_speed) if clutch_in else absf(wheel_speed)
	var wheel_rpm := shaft_speed / Config.WHEEL_RADIUS * 60.0 / TAU * ratio
	var target := wheel_rpm
	if locked:
		target = lerpf(Config.RPM_IDLE, Config.RPM_MAX * 0.92, drive_input)
	elif clutch_in:
		target = maxf(wheel_rpm, lerpf(Config.RPM_IDLE, Config.RPM_MAX * 0.85, drive_input))
	elif gear == 1 or gear == GEAR_REVERSE:
		# Simulated clutch slip so the car pulls away with usable torque.
		target = maxf(target, lerpf(Config.RPM_IDLE, Config.LAUNCH_RPM, drive_input))
	target = clampf(target, Config.RPM_IDLE, Config.RPM_MAX)
	var rate := Config.RPM_RISE_RATE if target > rpm else Config.RPM_FALL_RATE
	rpm = move_toward(rpm, target, rate * delta)

	if shift_timer > 0.0:
		shift_timer -= delta
	elif not locked and gear >= 1:
		var shift_rpm := absf(forward_speed if clutch_in else wheel_speed) / Config.WHEEL_RADIUS * 60.0 / TAU * ratio
		if shift_rpm > Config.RPM_UPSHIFT and gear < Config.GEAR_RATIOS.size() and drive_input > 0.05:
			gear += 1
			shift_timer = Config.SHIFT_TIME
			shift_event = 1
		elif shift_rpm < Config.RPM_DOWNSHIFT and gear > 1:
			gear -= 1
			shift_timer = Config.SHIFT_TIME * 0.5
			shift_event = -1

	engine_load = move_toward(engine_load, drive_input, delta * 6.0)
	drive_force = 0.0
	engine_brake = 0.0
	if free_rev or shift_timer > 0.0:
		return
	var torque := torque_at(rpm)
	var limiter := 1.0 - smoothstep(Config.RPM_LIMITER_SOFT, Config.RPM_MAX, rpm)
	if gear >= 1:
		var gear_scale := gear_ratio() / _first_ratio
		drive_force = Config.MAX_ENGINE_FORCE * torque * gear_scale * drive_input * limiter
		if drive_input < 0.05:
			engine_brake = Config.ENGINE_BRAKE_FORCE * (rpm / Config.RPM_MAX) * gear_scale
	elif forward_speed > -Config.REVERSE_MAX_SPEED_KMH / 3.6:
		drive_force = -Config.REVERSE_FORCE * torque * drive_input


## Normalised torque curve (piecewise linear over the config table).
func torque_at(engine_rpm: float) -> float:
	var n: int = Config.TORQUE_CURVE_RPM.size()
	var first_rpm: float = Config.TORQUE_CURVE_RPM[0]
	if engine_rpm <= first_rpm:
		var v0: float = Config.TORQUE_CURVE_VALUE[0]
		return v0
	for i in range(1, n):
		var r1: float = Config.TORQUE_CURVE_RPM[i]
		if engine_rpm <= r1:
			var r0: float = Config.TORQUE_CURVE_RPM[i - 1]
			var a: float = Config.TORQUE_CURVE_VALUE[i - 1]
			var b: float = Config.TORQUE_CURVE_VALUE[i]
			return lerpf(a, b, (engine_rpm - r0) / (r1 - r0))
	var last: float = Config.TORQUE_CURVE_VALUE[n - 1]
	return last
