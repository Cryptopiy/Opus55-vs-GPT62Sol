class_name DriftCar
extends RigidBody3D

const C = preload("res://game/config.gd")

var driver_id := 0
var paint := Color.WHITE
var throttle := 0.0
var brake := 0.0
var steer_input := 0.0
var handbrake := false
var assist := true
var steering := 0.0
var reversing := false
var gear := 1
var rpm := C.IDLE_RPM
var shift_left := 0.0
var speed := 0.0
var body_slip := 0.0
var wall_contact := false
var wall_impact := 0.0
var collision_impact := 0.0
var wall_point := Vector3.ZERO
var roof_contact := false
var roof_seconds := 0.0
var road_index := -1
var course_progress := 0.0
var last_checkpoint := 0
var next_checkpoint := 0
var completed_laps := 0
var lap_start := 0.0
var finish_time := 0.0
var best_lap := 0.0
var lap_times := PackedFloat32Array()
var wrong_way := 0.0
var stuck_time := 0.0
var recoveries := 0
var wall_seconds := 0.0

var mounts := PackedVector3Array()
var wheel_points := PackedVector3Array()
var wheel_normals := PackedVector3Array()
var wheel_length := PackedFloat32Array()
var wheel_spin := PackedFloat32Array()
var wheel_slip := PackedFloat32Array()
var wheel_contact := PackedByteArray()
var suspension_rays: Array[RayCast3D] = []

func build(id: int) -> void:
	driver_id = id
	paint = C.CAR_COLORS[id]
	mass = C.MASS
	center_of_mass_mode = RigidBody3D.CENTER_OF_MASS_MODE_CUSTOM
	center_of_mass = Vector3.DOWN * C.CENTER_OF_MASS_DROP
	angular_damp = 2.8
	linear_damp_mode = RigidBody3D.DAMP_MODE_REPLACE
	linear_damp = 0.0
	collision_layer = C.CAR_LAYER
	collision_mask = C.CAR_LAYER | C.ROAD_LAYER | C.WALL_LAYER
	continuous_cd = true
	can_sleep = false
	max_contacts_reported = 8
	var body_material := PhysicsMaterial.new()
	body_material.friction = 0.18
	body_material.bounce = 0.08
	physics_material_override = body_material
	_body_box(Vector3(1.84, 0.41, 4.12), Vector3(0.0, 0.04, 0.0))
	_body_box(Vector3(1.44, 0.35, 1.73), Vector3(0.0, 0.40, 0.16))
	wheel_points.resize(4)
	wheel_normals.resize(4)
	wheel_length.resize(4)
	wheel_spin.resize(4)
	wheel_slip.resize(4)
	wheel_contact.resize(4)
	for i in 4:
		var lateral := -1.0 if i % 2 == 0 else 1.0
		var longitudinal := -1.0 if i < 2 else 1.0
		mounts.append(Vector3(lateral * C.TRACK_WIDTH * 0.5, C.MOUNT_HEIGHT,
			longitudinal * C.WHEELBASE * 0.5))
		wheel_length[i] = C.SPRING_REST
		var ray := RayCast3D.new()
		ray.position = mounts[i]
		ray.target_position = Vector3.DOWN * (C.SPRING_REST + C.SPRING_TRAVEL + C.WHEEL_RADIUS)
		ray.collision_mask = C.ROAD_LAYER
		# Refresh on demand in the force solver so each wheel casts exactly once per tick.
		ray.enabled = false
		add_child(ray)
		suspension_rays.append(ray)

func _body_box(size: Vector3, offset: Vector3) -> void:
	var collider := CollisionShape3D.new()
	var shape := BoxShape3D.new()
	shape.size = size
	collider.shape = shape
	collider.position = offset
	add_child(collider)

func reset_at(pose: Transform3D) -> void:
	global_transform = pose
	linear_velocity = Vector3.ZERO
	angular_velocity = Vector3.ZERO
	PhysicsServer3D.body_set_state(get_rid(), PhysicsServer3D.BODY_STATE_TRANSFORM, pose)
	PhysicsServer3D.body_set_state(get_rid(), PhysicsServer3D.BODY_STATE_LINEAR_VELOCITY, Vector3.ZERO)
	PhysicsServer3D.body_set_state(get_rid(), PhysicsServer3D.BODY_STATE_ANGULAR_VELOCITY, Vector3.ZERO)
	steering = 0.0
	speed = 0.0
	body_slip = 0.0
	roof_seconds = 0.0
	stuck_time = 0.0
	wall_seconds = 0.0
	wall_contact = false
	wall_impact = 0.0
	collision_impact = 0.0
	gear = 1
	shift_left = 0.0
	rpm = C.IDLE_RPM

func _physics_process(delta: float) -> void:
	if freeze:
		return
	var axes := global_basis.orthonormalized()
	var facing := -axes.z
	var forward_speed := linear_velocity.dot(facing)
	var lateral_speed := linear_velocity.dot(axes.x)
	speed = Vector2(forward_speed, lateral_speed).length()
	body_slip = rad_to_deg(atan2(lateral_speed, maxf(0.15, forward_speed)))
	roof_seconds = roof_seconds + delta if roof_contact else 0.0
	wall_seconds = wall_seconds + delta if wall_contact else 0.0
	_drivetrain(delta, forward_speed)
	var speed_fraction := clampf(speed / C.TOP_SPEED, 0.0, 1.0)
	var steering_limit := deg_to_rad(lerpf(C.MAX_STEER, C.MIN_STEER, sqrt(speed_fraction)))
	var demand := steer_input
	if assist and speed > 7.0 and forward_speed > 0.0:
		# Point the front tyres partly along travel during an oversteer slide.
		var recovery := clampf(body_slip / 32.0, -0.85, 0.85)
		demand = clampf(steer_input + recovery * smoothstep(8.0, 34.0, absf(body_slip)) * 0.72, -1.0, 1.0)
	steering = move_toward(steering, demand * steering_limit, C.STEER_RATE * delta)
	var drive := 0.0
	if shift_left <= 0.0:
		var limiter := 1.0 - smoothstep(C.TOP_SPEED - 3.5, C.TOP_SPEED + 0.35, speed)
		drive = throttle * C.ENGINE_FORCE * limiter
		if reversing:
			drive = -brake * C.ENGINE_FORCE * 0.46 * (1.0 - smoothstep(7.0, 10.0, speed))
	var braking := brake if not reversing else 0.0
	if forward_speed < -0.8:
		braking = maxf(braking, throttle)
		drive = minf(0.0, drive)
	for i in 4:
		_wheel(i, axes, drive * 0.5, braking * C.BRAKE_FORCE * 0.25, delta)
	var velocity := linear_velocity.slide(axes.y)
	apply_central_force(-velocity * (4.5 + speed * 0.095))

func _drivetrain(delta: float, forward_speed: float) -> void:
	if throttle > 0.03 or brake < 0.02 or forward_speed > 0.7:
		reversing = false
	elif forward_speed < 0.22:
		reversing = true
	shift_left = maxf(0.0, shift_left - delta)
	var target := C.IDLE_RPM + maxf(0.0, forward_speed) / float(C.GEAR_LIMITS[gear - 1]) * (C.UPSHIFT - C.IDLE_RPM)
	if reversing:
		target = C.IDLE_RPM + absf(forward_speed) * 390.0 + brake * 300.0
	elif shift_left <= 0.0:
		if target >= C.UPSHIFT and gear < C.GEAR_LIMITS.size():
			gear += 1
			shift_left = C.SHIFT_TIME
		elif target <= C.DOWNSHIFT and gear > 1:
			gear -= 1
			shift_left = C.SHIFT_TIME
	if gear == 1 and not reversing:
		target = maxf(target, C.IDLE_RPM + throttle * 900.0)
	rpm = lerpf(rpm, clampf(target, C.IDLE_RPM, C.MAX_RPM), 1.0 - exp(-delta * 16.0))

func _wheel(i: int, axes: Basis, engine: float, service_brake: float, delta: float) -> void:
	var ray := suspension_rays[i]
	ray.force_raycast_update()
	if not ray.is_colliding():
		wheel_contact[i] = 0
		wheel_length[i] = move_toward(wheel_length[i], C.SPRING_REST + 0.08, delta * 2.8)
		wheel_slip[i] = 0.0
		return
	var point := ray.get_collision_point()
	var normal := ray.get_collision_normal()
	var offset := point - global_position
	var from_center := offset - axes * center_of_mass
	var velocity := linear_velocity + angular_velocity.cross(from_center)
	var spring_length := maxf(0.015, ray.global_position.distance_to(point) - C.WHEEL_RADIUS)
	var compression := C.SPRING_REST - spring_length
	var load := clampf(compression * C.SPRING_STIFFNESS - velocity.dot(normal) * C.SPRING_DAMPER, 0.0, 12000.0)
	wheel_contact[i] = 1
	wheel_points[i] = point
	wheel_normals[i] = normal
	wheel_length[i] = spring_length
	apply_force(normal * load, offset)
	var front := i < 2
	var locked := not front and handbrake
	var heading := -axes.z
	if front:
		heading = heading.rotated(axes.y, -steering)
	heading = heading.slide(normal).normalized()
	var side := heading.cross(normal).normalized()
	var along := velocity.dot(heading)
	var lateral := velocity.dot(side)
	var slip := rad_to_deg(atan2(absf(lateral), maxf(1.5, absf(along))))
	var curve := lerpf(1.0, C.TYRE_TAIL_GRIP, smoothstep(C.TYRE_PEAK, C.TYRE_TAIL, slip))
	var grip := load * (1.23 if front else 1.26)
	if locked:
		grip *= C.LOCKED_GRIP
	var lateral_force := -signf(lateral) * grip * curve * minf(slip / C.TYRE_PEAK, 1.0)
	var resistance := minf(absf(along) * 1400.0, grip if locked else service_brake + load * 0.016)
	if locked:
		resistance *= 0.76
	var drive := (0.0 if front or locked else engine) - signf(along) * resistance
	# A shared friction ellipse accounts for the grip spent accelerating or braking.
	var magnitude := Vector2(drive, lateral_force).length()
	var scale_force := minf(1.0, grip / maxf(1.0, magnitude))
	apply_force((heading * drive + side * lateral_force) * scale_force, offset)
	if not locked:
		wheel_spin[i] = fposmod(wheel_spin[i] - along * delta / C.WHEEL_RADIUS, TAU)
	wheel_slip[i] = clampf(maxf((slip - 3.0) / 29.0, (1.0 - scale_force) * 0.72), 0.0, 1.0)
	if locked and absf(along) > 3.0:
		wheel_slip[i] = maxf(wheel_slip[i], minf(1.0, absf(along) / 15.0))

func _integrate_forces(state: PhysicsDirectBodyState3D) -> void:
	wall_contact = false
	roof_contact = false
	wall_impact = 0.0
	collision_impact = 0.0
	for i in state.get_contact_count():
		var collider := state.get_contact_collider_object(i) as CollisionObject3D
		if collider == null:
			continue
		if collider.collision_layer & (C.WALL_LAYER | C.CAR_LAYER):
			collision_impact = maxf(collision_impact, state.get_contact_impulse(i).length())
		if collider.collision_layer & C.WALL_LAYER:
			wall_contact = true
			wall_point = state.get_contact_collider_position(i)
			wall_impact = maxf(wall_impact, state.get_contact_impulse(i).length())
		if collider.collision_layer & C.ROAD_LAYER and state.transform.basis.y.y < -0.30:
			roof_contact = true
