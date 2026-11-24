class_name DriftTestDriver
extends AIController
## Autotest driver for acceptance step 2 ("drive a lap with handbrake drifts
## in at least 3 corners"). It drives the racing line like the AI; entering a
## corner above 50 km/h it kicks the tail out with the handbrake while
## steering in, then holds the slide on the throttle while still steering
## along the track (counter-steer assist on), aiming for ~25 deg of slip -
## roughly what a player does.

enum Phase { CRUISE, KICK, HOLD }

const ENTRY_LOOKAHEAD := 25.0
const ENTRY_CURVATURE := 1.0 / 160.0
const KICK_TIME := 0.8                  # max; the kick ends once the slide builds
const HOLD_TIME := 3.0
const COOLDOWN := 1.0
const TARGET_SLIP := 25.0
const MAX_ENTRY_KMH := 82.0
const KICK_SLIP := 14.0
const DRIFT_LAT_ACCEL := 6.0               # m/s^2 a sliding car can sustain

var drifts_started := 0

var _phase := Phase.CRUISE
var _timer := 0.0
var _dir := 1.0
var _cool := 0.0
var _episode_slip := 0.0
var _entry_kmh := 0.0
var _window := 0.0


func update_controls(car: Car, delta: float) -> void:
	super.update_controls(car, delta)
	_cool -= delta
	var track := car.track
	var idx := maxi(car.track_index, 0)
	var s := track.distance_along(car.global_position, idx)
	var k_ahead := track.curvature[track.index_at_distance(s + ENTRY_LOOKAHEAD)]
	_episode_slip = maxf(_episode_slip, car.body_slip_deg)
	if car.body_slip_deg >= Config.DRIFT_MIN_SLIP_DEG and car.body_slip_deg <= Config.DRIFT_MAX_SLIP_DEG and car.speed_kmh > Config.DRIFT_MIN_SPEED_KMH:
		_window += delta
	# Entry speed a drifting car can carry through the coming corner.
	var k_max := 0.0
	for d in range(10, 70, 10):
		k_max = maxf(k_max, absf(track.curvature[track.index_at_distance(s + float(d))]))
	var entry_kmh := MAX_ENTRY_KMH
	if k_max > 0.0001:
		entry_kmh = minf(MAX_ENTRY_KMH, sqrt(DRIFT_LAT_ACCEL / k_max) * 3.6)
	match _phase:
		Phase.CRUISE:
			# Approach drift corners from the outside of the road.
			var k_far := track.curvature[track.index_at_distance(s + 60.0)]
			if absf(k_far) > ENTRY_CURVATURE or absf(k_ahead) > ENTRY_CURVATURE:
				var side := -signf(k_far if absf(k_far) > absf(k_ahead) else k_ahead) * 2.5
				_steer_to_offset(car, s, side)
			if car.speed_kmh > entry_kmh and absf(k_ahead) > ENTRY_CURVATURE:
				car.throttle = 0.0
				car.brake = 0.7
			elif absf(k_ahead) > ENTRY_CURVATURE and _cool <= 0.0 and car.speed_kmh > 50.0 					and car.speed_kmh <= entry_kmh + 3.0 and car.body_slip_deg < 8.0:
				_phase = Phase.KICK
				_timer = KICK_TIME
				_dir = signf(k_ahead)
				_episode_slip = 0.0
				_window = 0.0
				_entry_kmh = car.speed_kmh
				drifts_started += 1
		Phase.KICK:
			car.handbrake = true
			car.throttle = 0.0
			car.brake = 0.0
			car.steer_input = _dir
			_timer -= delta
			if _timer <= 0.0 or car.body_slip_deg > KICK_SLIP:
				_phase = Phase.HOLD
				_timer = HOLD_TIME
		Phase.HOLD:
			# Steering: the drift-holding input (half lock into the corner) plus
			# a PD correction that keeps the car near the racing line; the
			# assist adds the counter-steer, the throttle keeps the slide going.
			# With the assist the wheels already follow the travel direction,
			# so steer by the angle between the VELOCITY and a point ahead.
			var slip := car.body_slip_deg
			var lat := track.lateral_offset(car.global_position, idx)
			var target := track.racing_point_at(s + 9.0 + car.speed * 0.45, 0.0) - car.global_position
			var vel := car.linear_velocity
			vel.y = 0.0
			target.y = 0.0
			var ang := -vel.signed_angle_to(target, Vector3.UP)
			var kmh_t := clampf(car.speed_kmh / Config.STEER_SPEED_REF_KMH, 0.0, 1.0)
			var max_angle := deg_to_rad(lerpf(Config.STEER_MAX_LOW_DEG, Config.STEER_MAX_HIGH_DEG, kmh_t))
			# Hold the key into the corner, but ease off when the slide's path
			# is tighter than the corner (or the car nears the inside edge).
			var k_path := -car.angular_velocity.y / maxf(car.speed, 1.0)
			var k_track := track.curvature[track.index_at_distance(s + 10.0)]
			var turn_more := (k_track - k_path) * _dir * 15.0 - (lat * _dir) * 0.08
			var hold := clampf(0.65 + turn_more, 0.3, 1.0)
			if lat * _dir > 3.0:
				hold = 0.0
			var edge := clampf((absf(lat) - 5.0) / 1.2, 0.0, 1.0)
			car.steer_input = clampf(lerpf(_dir * hold, ang * 1.6 / max_angle, edge), -1.0, 1.0)
			car.handbrake = false
			car.brake = 0.0
			# Stay on the power (a lift scrubs speed and tightens the line);
			# back off only when the angle grows too large.
			car.throttle = clampf(0.85 + (TARGET_SLIP + 10.0 - slip) * 0.02, 0.5, 1.0)
			_timer -= delta
			var corner_left := absf(track.curvature[track.index_at_distance(s + 12.0)]) > ENTRY_CURVATURE * 0.6
			if _timer <= 0.0 or slip > 70.0 or car.speed_kmh < 35.0 or lat * _dir > 4.0 or absf(lat) > 5.8 or not corner_left:
				print("[drift-driver] episode %d: entry %.0f km/h, max slip %.0f deg, in drift window %.2f s, exit %.0f km/h" % [
					drifts_started, _entry_kmh, _episode_slip, _window, car.speed_kmh])
				_phase = Phase.CRUISE
				_cool = COOLDOWN


## Pure-pursuit steering toward a point `offset` metres from the centreline.
func _steer_to_offset(car: Car, s: float, offset: float) -> void:
	var track := car.track
	var look := 8.0 + car.speed * 0.5
	var p := track.racing_point_at(s + look, offset - track.racing_offset_at(s + look))
	var local := car.global_transform.affine_inverse() * p
	var alpha := atan2(local.x, -local.z)
	var wheel := atan(2.0 * Config.WHEELBASE * sin(alpha) / maxf(Vector2(local.x, local.z).length(), 1.0))
	var kmh_t := clampf(car.speed_kmh / Config.STEER_SPEED_REF_KMH, 0.0, 1.0)
	var max_angle := deg_to_rad(lerpf(Config.STEER_MAX_LOW_DEG, Config.STEER_MAX_HIGH_DEG, kmh_t))
	car.steer_input = clampf(wheel / max_angle, -1.0, 1.0)
