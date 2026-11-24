class_name RaceCamera
extends Camera3D

const C = preload("res://game/config.gd")

var hood := false
var _heading := Vector3.FORWARD
var _aim := Vector3.ZERO
var _cut := true

func cut() -> void:
	_cut = true

func follow(car: DriftCar, delta: float) -> void:
	var speed := clampf(car.speed / C.TOP_SPEED, 0.0, 1.0)
	fov = lerpf(C.FOV_LOW, C.FOV_HIGH, speed)
	if hood:
		global_transform = car.global_transform * Transform3D(Basis.IDENTITY.rotated(Vector3.RIGHT, -0.025),
			Vector3(0.0, 0.67, -1.24))
		_cut = true
		return
	var nose := (-car.global_basis.z).slide(Vector3.UP).normalized()
	if nose.length_squared() < 0.01:
		nose = _heading
	var direction := nose
	if car.speed > 12.0 and absf(car.body_slip) > C.DRIFT_MIN and absf(car.body_slip) < C.DRIFT_MAX:
		direction = nose.lerp(car.linear_velocity.slide(Vector3.UP).normalized(), 0.26).normalized()
	_heading = direction if _cut else _heading.lerp(direction, 1.0 - exp(-delta * 4.1)).normalized()
	var desired := car.global_position - _heading * C.CAMERA_DISTANCE + Vector3.UP * C.CAMERA_HEIGHT
	var focus := car.global_position + _heading * 3.6 + Vector3.UP * 0.30
	global_position = desired if _cut else global_position.lerp(desired, 1.0 - exp(-delta * 8.2))
	_aim = focus if _cut else _aim.lerp(focus, 1.0 - exp(-delta * 9.0))
	look_at(_aim)
	_cut = false

func showcase(data: TrackData) -> void:
	fov = 63.0
	global_position = data.frame_at(17.0, -5.9, 2.5).origin
	look_at(data.frame_at(-5.8, 2.1, 1.6).origin)
