class_name DriverInput
extends RefCounted

static func read(car: DriftCar) -> void:
	car.throttle = 1.0 if Input.is_physical_key_pressed(KEY_W) else 0.0
	car.brake = 1.0 if Input.is_physical_key_pressed(KEY_S) else 0.0
	car.steer_input = float(Input.is_physical_key_pressed(KEY_D)) - float(Input.is_physical_key_pressed(KEY_A))
	car.handbrake = Input.is_physical_key_pressed(KEY_SPACE)
