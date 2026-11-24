class_name AIController
extends CarController
## AI driver (R-21..R-23) feeding the same Car physics as the player.
##  * Follows the precomputed racing line with a pure-pursuit look-ahead that
##    grows with speed.
##  * Speed target = curvature-based profile (already braked early) x skill
##    (Easy 85 % / Normal 95 % / Hard 100 %) x light rubber-banding.
##  * A slowly varying personal lane offset avoids single-file driving.
##  * Steers around a car ahead in its lane, brakes when closing fast.
##  * Stuck (< 5 km/h for 3 s) -> respawn at the last checkpoint.

const AVOID_TIME := 1.1                 # extra look-ahead per m/s of closing speed (s)
const TURNAROUND_SPEED := 8.0           # m/s when the target is far off the nose

var race: RaceManager
var skill := 1.0
var cooldown := false                   # true after the car has finished

var _lane_amp := 0.0
var _lane_period := 20.0
var _lane_phase := 0.0
var _avoid_offset := 0.0
var _car_brake := 0.0
var _stuck_time := 0.0
var _time := 0.0


func setup(skill_factor: float, rng: RandomNumberGenerator, race_manager: RaceManager) -> void:
	skill = skill_factor
	race = race_manager
	_lane_amp = rng.randf_range(0.5, 1.0) * Config.AI_LANE_OFFSET_MAX
	_lane_period = rng.randf_range(Config.AI_LANE_PERIOD_MIN, Config.AI_LANE_PERIOD_MAX)
	_lane_phase = rng.randf() * TAU


func update_controls(car: Car, delta: float) -> void:
	_time += delta
	var track := car.track
	var xf := car.global_transform
	var idx := car.track_index
	if idx < 0:
		idx = track.find_closest_index_full(xf.origin)
	var s := track.distance_along(xf.origin, idx)
	var v := car.speed

	_update_avoidance(car, xf, delta)
	var lane := _lane_amp * sin(TAU * _time / _lane_period + _lane_phase)

	# --- steering: pure pursuit toward a racing-line point ahead ---
	var look := Config.AI_LOOKAHEAD_BASE + v * Config.AI_LOOKAHEAD_PER_SPEED
	var s_target := s + look
	var base_off := track.racing_offset_at(s_target)
	var total_off := clampf(base_off + lane + _avoid_offset, -Config.AI_MAX_TOTAL_OFFSET, Config.AI_MAX_TOTAL_OFFSET)
	var target := track.racing_point_at(s_target, total_off - base_off)
	var local := xf.affine_inverse() * target
	var ahead := -local.z
	var alpha := atan2(local.x, ahead)
	var dist := maxf(Vector2(local.x, ahead).length(), 1.0)
	var wheel_angle := atan(2.0 * Config.WHEELBASE * sin(alpha) / dist)
	var t := clampf(car.speed_kmh / Config.STEER_SPEED_REF_KMH, 0.0, 1.0)
	var max_angle := deg_to_rad(lerpf(Config.STEER_MAX_LOW_DEG, Config.STEER_MAX_HIGH_DEG, t))
	car.steer_input = clampf(wheel_angle / max_angle, -1.0, 1.0)

	# --- speed: profile x skill x rubber band ---
	var target_speed := _profile_speed(track, s, v) * skill * _rubber_band(car)
	if cooldown:
		target_speed *= Config.AI_COOLDOWN_SPEED_SCALE
	if absf(alpha) > deg_to_rad(60.0):
		target_speed = minf(target_speed, TURNAROUND_SPEED)
	var err := target_speed - v
	var throttle := 0.0
	var brake := 0.0
	if err >= 0.0:
		throttle = clampf(err * Config.AI_THROTTLE_GAIN + 0.25, 0.0, 1.0)
	else:
		brake = clampf(-err * Config.AI_BRAKE_GAIN, 0.0, 1.0)
	# Driver traction control: ease off when the rear steps out or spins.
	var rear := car.rear_wheel_slip()
	throttle *= clampf(1.0 - (rear - 1.5) / 5.0, 0.25, 1.0)
	throttle *= clampf(1.0 - (car.body_slip_deg - 8.0) / 20.0, 0.2, 1.0)
	if _car_brake > 0.0:
		brake = maxf(brake, _car_brake)
		throttle = 0.0
	car.throttle = throttle
	car.brake = brake
	car.handbrake = false
	_update_stuck(car, delta)


## Lowest profile speed between here and where the car will be shortly.
func _profile_speed(track: TrackData, s: float, v: float) -> float:
	var reach := v * Config.AI_SPEED_LOOKAHEAD_TIME + 2.0
	var best := INF
	var steps := 4
	for k in steps + 1:
		var i := track.index_at_distance(s + reach * float(k) / float(steps))
		best = minf(best, track.target_speeds[i])
	return best


## R-22: +-5 % target speed when more than 80 m behind / ahead of the player.
func _rubber_band(car: Car) -> float:
	if race == null or race.player == null or race.player == car:
		return 1.0
	var gap := race.progress_of(car) - race.progress_of(race.player)
	if gap > Config.RUBBER_BAND_DISTANCE:
		return 1.0 - Config.RUBBER_BAND_FACTOR
	if gap < -Config.RUBBER_BAND_DISTANCE:
		return 1.0 + Config.RUBBER_BAND_FACTOR
	return 1.0


## R-23: shift the lane away from a car ahead in our lane (within 6 m gap,
## extended by closing speed) and brake when closing fast.
func _update_avoidance(car: Car, xf: Transform3D, delta: float) -> void:
	var desired := 0.0
	_car_brake = 0.0
	if race != null:
		var fwd := -xf.basis.z
		var right := xf.basis.x
		for other in race.cars:
			if other == car:
				continue
			var rel := other.global_position - xf.origin
			var ahead := rel.dot(fwd)
			var lateral := rel.dot(right)
			if ahead <= 0.0 or absf(lateral) > Config.AI_AVOID_LANE_HALF_WIDTH:
				continue
			var gap := ahead - Config.CHASSIS_SIZE.z
			var closing := (car.linear_velocity - other.linear_velocity).dot(fwd)
			if gap > Config.AI_AVOID_DISTANCE + maxf(closing, 0.0) * AVOID_TIME:
				continue
			# Dodge to the side the other car is not on; if dead ahead, toward
			# the road centre so we never squeeze into the barrier.
			var my_off := car.track.lateral_offset(xf.origin, maxi(car.track_index, 0))
			var dir := -signf(lateral)
			if absf(lateral) < 0.4:
				dir = -1.0 if my_off > 0.0 else 1.0
			desired = dir * Config.AI_AVOID_SHIFT
			if closing > Config.AI_CLOSING_BRAKE_SPEED:
				_car_brake = maxf(_car_brake, clampf(0.2 + (closing - Config.AI_CLOSING_BRAKE_SPEED) * 0.12, 0.0, 1.0))
	_avoid_offset = move_toward(_avoid_offset, desired, Config.AI_AVOID_RATE * delta)


func _update_stuck(car: Car, delta: float) -> void:
	if car.controls_locked or race == null or not race.is_racing():
		_stuck_time = 0.0
		return
	if car.speed_kmh < Config.AI_STUCK_SPEED_KMH:
		_stuck_time += delta
		if _stuck_time > Config.AI_STUCK_TIME:
			_stuck_time = 0.0
			car.request_respawn()
	else:
		_stuck_time = 0.0
