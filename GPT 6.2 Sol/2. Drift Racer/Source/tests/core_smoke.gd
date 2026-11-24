extends SceneTree

const C = preload("res://game/config.gd")

func _initialize() -> void:
	call_deferred("_run")

func _run() -> void:
	var pad := StaticBody3D.new()
	pad.collision_layer = C.ROAD_LAYER
	pad.collision_mask = C.CAR_LAYER
	var collider := CollisionShape3D.new()
	var shape := BoxShape3D.new()
	shape.size = Vector3(40.0, 1.0, 5000.0)
	collider.position.y = -0.5
	collider.shape = shape
	pad.add_child(collider)
	root.add_child(pad)
	var car := DriftCar.new()
	car.build(0)
	root.add_child(car)
	car.reset_at(Transform3D(Basis.IDENTITY, Vector3.UP * C.RIDE_HEIGHT))
	var shifts := PackedByteArray([0, 0, 0, 0, 0, 0])
	var peak := 0.0
	var peak_rpm := 0.0
	var minimum_up := 1.0
	var suspension_min := INF
	var suspension_max := 0.0
	var cuts := 0
	_key(KEY_W, true)
	for i in 2040:
		await physics_frame
		DriverInput.read(car)
		shifts[car.gear - 1] = 1
		peak = maxf(peak, car.speed * C.KMH)
		peak_rpm = maxf(peak_rpm, car.rpm)
		cuts += 1 if car.shift_left > 0.0 else 0
		minimum_up = minf(minimum_up, car.global_basis.y.y)
		for length in car.wheel_length:
			suspension_min = minf(suspension_min, length)
			suspension_max = maxf(suspension_max, length)
	_key(KEY_W, false)
	_key(KEY_S, true)
	var at_brake := car.speed * C.KMH
	for i in 240:
		await physics_frame
		DriverInput.read(car)
	print("DYNAMICS: mass=%.0f COM=%.2f wheelbase=%.2f width=%.2f travelled=%.1f m" %
		[car.mass, car.center_of_mass.y, car.mounts[0].distance_to(car.mounts[2]), car.mounts[0].distance_to(car.mounts[1]), -car.position.z])
	print("DYNAMICS: gears=%s torque cut ticks=%d RPM max=%.0f peak=%.1f km/h, braking 2 sec %.1f → %.1f km/h" %
		[shifts, cuts, peak_rpm, peak, at_brake, car.speed * C.KMH])
	print("DYNAMICS: suspension travel %.3f–%.3f m, minimum chassis up=%.3f" % [suspension_min, suspension_max, minimum_up])
	var good := (shifts == PackedByteArray([1, 1, 1, 1, 1, 1]) and cuts > 0 and absf(peak - C.TOP_KMH) < 3.0
		and car.speed * C.KMH < at_brake - 60.0 and minimum_up > 0.8 and suspension_max - suspension_min > 0.04)
	quit(0 if good else 1)

func _key(key_code: int, pressed: bool) -> void:
	var event := InputEventKey.new()
	event.physical_keycode = key_code
	event.pressed = pressed
	Input.parse_input_event(event)
