class_name Car
extends RigidBody3D
## Arcade car on a custom raycast suspension (R-07..R-13) - not VehicleBody3D.
## Every 120 Hz physics tick each grounded wheel (RayCast3D) applies:
##  1. a spring + damper (+ bump stop, anti-roll) force along the contact
##     normal at the contact point,
##  2. separate longitudinal (drive / brake / rolling) and lateral
##     (slip-angle curve) tyre forces, limited by a friction circle whose size
##     follows the wheel load, so weight transfer changes grip.
## The same script drives player and AI cars; only the controller differs.

signal respawn_requested(car: Car)
signal respawned
signal barrier_hit(impact_speed: float)
signal car_hit(impact_speed: float)

const WHEEL_COUNT := 4

var car_index := 0
var display_name := ""
var paint_color := Color.WHITE
var is_player := false
var track: TrackData
var barrier_body: Node

# Controls (written by the controller every tick).
var throttle := 0.0
var brake := 0.0
var steer_input := 0.0
var handbrake := false
var controls_locked := true
var assist_enabled := true
var assist_gain: float = Config.ASSIST_GAIN
var assist_input_scale: float = Config.ASSIST_DRIFT_INPUT_SCALE
var controller: CarController
var drivetrain := Drivetrain.new()
var visuals: CarVisuals
var fx: CarFx
var audio: CarAudio

# Telemetry (read by HUD, AI, effects, audio and the drift scorer).
var speed := 0.0
var speed_kmh := 0.0
var forward_speed := 0.0
var lateral_speed := 0.0
var body_slip_deg := 0.0                  # angle between heading and velocity (0..180)
var steer_angle := 0.0                    # front wheel angle (rad, + = right)
var grounded_count := 0
var track_index := -1
var touching_barrier := false
var scrape_point := Vector3.ZERO
var scrape_normal := Vector3.UP
var respawn_count := 0
var flip_timer := 0.0
var wheel_grounded := PackedByteArray([0, 0, 0, 0])
var wheel_compression := PackedFloat32Array([0.0, 0.0, 0.0, 0.0])
var wheel_load := PackedFloat32Array([0.0, 0.0, 0.0, 0.0])
var wheel_slip := PackedFloat32Array([0.0, 0.0, 0.0, 0.0])      # contact-patch sliding speed (m/s)
var wheel_contact := PackedVector3Array([Vector3.ZERO, Vector3.ZERO, Vector3.ZERO, Vector3.ZERO])
var wheel_normal := PackedVector3Array([Vector3.UP, Vector3.UP, Vector3.UP, Vector3.UP])
var wheel_spin := PackedFloat32Array([0.0, 0.0, 0.0, 0.0])      # rad/s
var wheel_rotation := PackedFloat32Array([0.0, 0.0, 0.0, 0.0])  # visual angle (rad)

var _rays: Array[RayCast3D] = []
var _prev_compression := PackedFloat32Array([0.0, 0.0, 0.0, 0.0])
var _prev_velocity := Vector3.ZERO
var _com_local := Vector3.ZERO
var _barrier_contact := false
var _barrier_contact_prev := false
var _barrier_impact := 0.0
var _car_contact_speed := 0.0
var _car_contact_prev := false


## Local suspension mount of wheel w (0 FL, 1 FR, 2 RL, 3 RR). Forward is -Z.
static func mount_position(w: int) -> Vector3:
	var x := Config.TRACK_WIDTH * 0.5 * (-1.0 if w % 2 == 0 else 1.0)
	var z := Config.WHEELBASE * 0.5 * (-1.0 if w < 2 else 1.0)
	return Vector3(x, Config.MOUNT_HEIGHT, z)


func setup(index: int, player: bool, track_data: TrackData, barriers: Node, skid_marks: SkidMarks) -> void:
	car_index = index
	is_player = player
	paint_color = Config.CAR_COLORS[index]
	display_name = Config.CAR_NAMES[index]
	track = track_data
	barrier_body = barriers
	name = "Car%d" % index
	mass = Config.CAR_MASS
	collision_layer = Config.LAYER_CAR
	collision_mask = Config.LAYER_ROAD | Config.LAYER_BARRIER | Config.LAYER_CAR
	continuous_cd = true                       # R-13: no tunnelling at 190 km/h
	can_sleep = false
	contact_monitor = true
	max_contacts_reported = 8
	angular_damp_mode = RigidBody3D.DAMP_MODE_REPLACE
	angular_damp = Config.ANGULAR_DAMP
	linear_damp_mode = RigidBody3D.DAMP_MODE_REPLACE
	linear_damp = 0.0
	inertia = Config.CAR_INERTIA
	var pm := PhysicsMaterial.new()
	pm.friction = Config.BODY_FRICTION
	pm.bounce = Config.BODY_BOUNCE
	physics_material_override = pm
	var chassis_pos := Vector3(0.0, Config.CHASSIS_OFFSET_Y, 0.0)
	_add_box(Config.CHASSIS_SIZE, chassis_pos)
	_add_box(Config.CABIN_SIZE, Config.CABIN_OFFSET)
	# R-07: centre of mass = volume-weighted centre of the body shapes, 0.3 m lower.
	var v_chassis := Config.CHASSIS_SIZE.x * Config.CHASSIS_SIZE.y * Config.CHASSIS_SIZE.z
	var v_cabin := Config.CABIN_SIZE.x * Config.CABIN_SIZE.y * Config.CABIN_SIZE.z
	var geometric := (chassis_pos * v_chassis + Config.CABIN_OFFSET * v_cabin) / (v_chassis + v_cabin)
	_com_local = geometric + Vector3.DOWN * Config.COM_DROP
	center_of_mass_mode = RigidBody3D.CENTER_OF_MASS_MODE_CUSTOM
	center_of_mass = _com_local
	for w in WHEEL_COUNT:
		var ray := RayCast3D.new()
		ray.name = "WheelRay%d" % w
		ray.position = mount_position(w)
		ray.target_position = Vector3(0.0, -(Config.SUSPENSION_REST + Config.WHEEL_RADIUS), 0.0)
		ray.collision_mask = Config.LAYER_ROAD
		ray.enabled = false                     # updated manually each tick
		ray.hit_back_faces = true
		add_child(ray)
		_rays.append(ray)
	visuals = CarVisuals.new()
	add_child(visuals)
	visuals.build(self)
	fx = CarFx.new()
	add_child(fx)
	fx.setup(self, skid_marks)
	audio = CarAudio.new()
	add_child(audio)
	audio.setup(self)


func _add_box(size: Vector3, pos: Vector3) -> void:
	var shape := BoxShape3D.new()
	shape.size = size
	var cs := CollisionShape3D.new()
	cs.shape = shape
	cs.position = pos
	add_child(cs)


func _physics_process(delta: float) -> void:
	if controller != null:
		controller.update_controls(self, delta)
	var xf := global_transform
	_update_telemetry(xf.basis)
	_update_steering(delta)
	var rear_wheel_speed := (wheel_spin[2] + wheel_spin[3]) * 0.5 * Config.WHEEL_RADIUS
	drivetrain.update(delta, forward_speed, rear_wheel_speed, throttle, brake, controls_locked,
		handbrake and not controls_locked)
	var brake_cmd := 1.0 if controls_locked else drivetrain.brake_request
	_apply_wheels(delta, xf, brake_cmd)
	var v := linear_velocity
	var sp := v.length()
	if sp > 0.1:
		apply_central_force(-v * sp * Config.AERO_DRAG)
	_process_contacts()
	_update_flip(delta, xf)
	if track != null:
		track_index = track.find_closest_index(xf.origin, track_index, 12)
	_prev_velocity = linear_velocity
	visuals.update_from_car(self)
	fx.physics_update(delta)


func _update_telemetry(b: Basis) -> void:
	var v := linear_velocity
	speed = v.length()
	speed_kmh = speed * 3.6
	forward_speed = v.dot(-b.z)
	lateral_speed = v.dot(b.x)
	if speed > 1.0:
		body_slip_deg = rad_to_deg(atan2(absf(lateral_speed), forward_speed))
	else:
		body_slip_deg = 0.0


## R-11: speed-sensitive lock (32 deg -> 8 deg at 190 km/h), wheel angle
## moves at most 4 rad/s, optional counter-steer assist toward the velocity.
func _update_steering(delta: float) -> void:
	var t := clampf(speed_kmh / Config.STEER_SPEED_REF_KMH, 0.0, 1.0)
	var max_angle := deg_to_rad(lerpf(Config.STEER_MAX_LOW_DEG, Config.STEER_MAX_HIGH_DEG, t))
	var target := steer_input * max_angle
	if assist_enabled and forward_speed > Config.ASSIST_MIN_SPEED:
		# Body slip (+ = velocity points right of the nose). Steering by the same
		# angle aligns the front wheels with the direction of travel. While
		# sliding, the driver's input is scaled down so a held key (full lock)
		# holds the slide like half lock instead of killing it; releasing the
		# key lets the counter-steer straighten the car.
		var beta := atan2(lateral_speed, forward_speed)
		var weight := smoothstep(deg_to_rad(Config.ASSIST_START_DEG), deg_to_rad(Config.ASSIST_FULL_DEG), absf(beta))
		target = steer_input * max_angle * lerpf(1.0, assist_input_scale, weight) + beta * assist_gain * weight
	var lock := deg_to_rad(Config.STEER_MAX_LOW_DEG)
	steer_angle = move_toward(steer_angle, clampf(target, -lock, lock), Config.STEER_RATE * delta)


func _apply_wheels(delta: float, xf: Transform3D, brake_cmd: float) -> void:
	var body_basis := xf.basis
	var up := body_basis.y
	var origin := xf.origin
	var com := xf * _com_local
	# Force that removes 1 m/s from a quarter of the car's mass in one tick:
	# caps friction so it can never reverse a velocity (no low-speed jitter).
	var stop_gain := mass * 0.25 / delta
	var hold := absf(drivetrain.drive_force) < 1.0 and speed < Config.STOP_SPEED
	grounded_count = 0
	for w in WHEEL_COUNT:
		var ray := _rays[w]
		ray.force_raycast_update()
		if ray.is_colliding():
			var hit := ray.get_collision_point()
			var dist := ray.global_position.distance_to(hit)
			wheel_compression[w] = clampf(Config.SUSPENSION_REST + Config.WHEEL_RADIUS - dist, 0.0, Config.SUSPENSION_REST)
			wheel_contact[w] = hit
			wheel_normal[w] = ray.get_collision_normal()
			wheel_grounded[w] = 1
			grounded_count += 1
		else:
			wheel_compression[w] = 0.0
			wheel_grounded[w] = 0
	for w in WHEEL_COUNT:
		var is_rear := w >= 2
		if wheel_grounded[w] == 0:
			_prev_compression[w] = 0.0
			wheel_load[w] = 0.0
			wheel_slip[w] = 0.0
			_spin_airborne_wheel(w, delta, is_rear)
			continue
		# --- suspension (R-08) ---
		var comp := wheel_compression[w]
		var comp_vel := (comp - _prev_compression[w]) / delta
		_prev_compression[w] = comp
		var partner := w ^ 1                     # other wheel on the same axle
		var roll := 0.0
		if wheel_grounded[partner] == 1:
			roll = Config.ANTI_ROLL_K * (comp - wheel_compression[partner])
		var bump := Config.BUMP_STOP_K * maxf(comp - Config.BUMP_STOP_START, 0.0)
		var fz := maxf(Config.SPRING_K * comp + Config.DAMPER_C * comp_vel + bump + roll, 0.0)
		wheel_load[w] = fz
		var n := wheel_normal[w]
		var p := wheel_contact[w]
		var offset := p - origin
		apply_force(n * fz, offset)
		# --- tyre frame at the contact patch ---
		var wheel_fwd := -body_basis.z
		if not is_rear:
			wheel_fwd = wheel_fwd.rotated(up, -steer_angle)
		var fwd := (wheel_fwd - n * wheel_fwd.dot(n)).normalized()
		var side := fwd.cross(n)
		var v := linear_velocity + angular_velocity.cross(p - com)
		var v_long := v.dot(fwd)
		var v_lat := v.dot(side)
		var grip := Config.TYRE_MU * TyreModel.load_factor(fz) * fz
		# --- longitudinal (R-09): drive, engine braking, brakes, rolling ---
		var stop_long := absf(v_long) * stop_gain
		var f_long := 0.0
		if is_rear:
			f_long += drivetrain.drive_force * 0.5
			if drivetrain.engine_brake > 0.0:
				f_long -= signf(v_long) * minf(drivetrain.engine_brake * 0.5, stop_long)
		var bias := (1.0 - Config.BRAKE_BIAS_FRONT) if is_rear else Config.BRAKE_BIAS_FRONT
		var brake_f := Config.BRAKE_FORCE * brake_cmd * bias * 0.5
		if hold:
			brake_f = maxf(brake_f, Config.HOLD_BRAKE_FORCE * 0.25)
		brake_f += Config.ROLLING_RESISTANCE * fz
		f_long -= signf(v_long) * minf(brake_f, stop_long)
		# --- lateral (R-10): slip-angle curve, peak 8 deg, 70 % beyond 25 deg ---
		var slip_angle := atan2(absf(v_lat), maxf(absf(v_long), Config.TYRE_LOW_SPEED))
		var f_lat := -signf(v_lat) * minf(TyreModel.lateral_coefficient(slip_angle) * grip, absf(v_lat) * stop_gain)
		var slide := absf(v_lat) * TyreModel.slide_fraction(slip_angle)
		var spin_target := v_long / Config.WHEEL_RADIUS
		if is_rear and handbrake:
			# Handbrake: rear grip x0.4 and the wheel is locked, so the friction
			# force opposes the whole sliding velocity of the contact patch.
			var slide_speed := sqrt(v_long * v_long + v_lat * v_lat)
			if slide_speed > 0.01:
				var mag := minf(grip * Config.HANDBRAKE_GRIP, slide_speed * stop_gain)
				f_long = -v_long / slide_speed * mag
				f_lat = -v_lat / slide_speed * mag
			slide = slide_speed
			spin_target = 0.0
		else:
			if absf(f_long) > grip:
				var excess := absf(f_long) / grip - 1.0
				if is_rear and drivetrain.drive_force * f_long > 0.0:
					var spin_slip := minf(excess * Config.SPIN_SLIP_SCALE, 30.0)
					spin_target += signf(f_long) * spin_slip / Config.WHEEL_RADIUS
					slide = maxf(slide, spin_slip)
				else:
					spin_target = 0.0
					slide = maxf(slide, absf(v_long))
				f_long = signf(f_long) * grip * Config.WHEELSPIN_LONG_RATIO
			# Friction circle: what the longitudinal force uses is not available
			# sideways (throttle on the rear = power oversteer).
			var lat_cap := sqrt(maxf(grip * grip - f_long * f_long, 0.0))
			f_lat = clampf(f_lat, -lat_cap, lat_cap)
		apply_force(fwd * f_long + side * f_lat, offset)
		wheel_slip[w] = slide
		wheel_spin[w] = lerpf(wheel_spin[w], spin_target, minf(1.0, delta * 30.0))
		wheel_rotation[w] = fmod(wheel_rotation[w] + wheel_spin[w] * delta, TAU)


func _spin_airborne_wheel(w: int, delta: float, is_rear: bool) -> void:
	var target := forward_speed / Config.WHEEL_RADIUS
	if is_rear and drivetrain.drive_force > 0.0:
		target += throttle * 25.0
	if is_rear and handbrake:
		target = 0.0
	wheel_spin[w] = lerpf(wheel_spin[w], target, minf(1.0, delta * 3.0))
	wheel_rotation[w] = fmod(wheel_rotation[w] + wheel_spin[w] * delta, TAU)


## Collects barrier / car contacts reported by the physics engine (read in the
## next _physics_process). Approach speed uses the velocity before the hit.
func _integrate_forces(state: PhysicsDirectBodyState3D) -> void:
	var touching := false
	var impact := 0.0
	var car_speed := 0.0
	for i in state.get_contact_count():
		var obj := state.get_contact_collider_object(i)
		var n := state.get_contact_local_normal(i)
		if obj == barrier_body:
			touching = true
			impact = maxf(impact, absf(_prev_velocity.dot(n)))
			scrape_point = state.get_contact_local_position(i)
			scrape_normal = n
		elif obj is Car:
			var other := obj as Car
			car_speed = maxf(car_speed, absf((_prev_velocity - other.linear_velocity).dot(n)))
	_barrier_contact = touching
	_barrier_impact = impact
	_car_contact_speed = car_speed


func _process_contacts() -> void:
	touching_barrier = _barrier_contact
	if _barrier_contact and not _barrier_contact_prev:
		barrier_hit.emit(_barrier_impact)
	_barrier_contact_prev = _barrier_contact
	var car_now := _car_contact_speed > 0.0
	if car_now and not _car_contact_prev and _car_contact_speed > Config.IMPACT_MIN_SPEED:
		car_hit.emit(_car_contact_speed)
	_car_contact_prev = car_now


## R-13: upside down / on the side with body contact for > 2 s -> respawn.
## Also respawns a car that somehow fell far below the circuit.
func _update_flip(delta: float, xf: Transform3D) -> void:
	if xf.basis.y.dot(Vector3.UP) < Config.FLIP_UP_DOT and grounded_count == 0 and get_contact_count() > 0:
		flip_timer += delta
		if flip_timer > Config.FLIP_RESPAWN_TIME:
			flip_timer = 0.0
			respawn_requested.emit(self)
	else:
		flip_timer = 0.0
	if track != null and xf.origin.y < track.min_height - Config.KILL_DEPTH:
		respawn_requested.emit(self)


func request_respawn() -> void:
	respawn_requested.emit(self)


## Teleports the car to `xf` with zero velocity (grid placement).
func place(xf: Transform3D) -> void:
	global_transform = xf
	linear_velocity = Vector3.ZERO
	angular_velocity = Vector3.ZERO
	_prev_velocity = Vector3.ZERO
	steer_angle = 0.0
	flip_timer = 0.0
	_prev_compression.fill(0.0)
	wheel_spin.fill(0.0)
	wheel_slip.fill(0.0)
	drivetrain.reset()
	track_index = -1
	reset_physics_interpolation()
	fx.reset_trails()


## Respawn at a checkpoint: on the track, facing forward, velocity zero.
func respawn(xf: Transform3D) -> void:
	place(xf)
	respawn_count += 1
	respawned.emit()


func max_wheel_slip() -> float:
	return maxf(maxf(wheel_slip[0], wheel_slip[1]), maxf(wheel_slip[2], wheel_slip[3]))


func rear_wheel_slip() -> float:
	return maxf(wheel_slip[2], wheel_slip[3])


func is_braking() -> bool:
	return drivetrain.brake_request > 0.05 or controls_locked
