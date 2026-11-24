class_name DriftScore
extends RefCounted

const C = preload("res://game/config.gd")

var total := 0.0
var pending := 0.0
var combo := 1
var continuous := 0.0
var ended := 0.0
var active := false

func update(car: DriftCar, delta: float, scoring := true) -> void:
	var angle := absf(car.body_slip)
	var kmh := car.speed * C.KMH
	active = scoring and angle >= C.DRIFT_MIN and angle <= C.DRIFT_MAX and kmh > C.DRIFT_KMH and not car.reversing
	if car.wall_contact:
		active = false
		pending = 0.0
		continuous = 0.0
		ended = 0.0
		combo = 1
	elif active:
		continuous += delta
		ended = 0.0
		combo = mini(C.COMBO_MAX, 1 + int(continuous / C.COMBO_SECONDS))
		var angle_factor := lerpf(0.45, 1.0, inverse_lerp(C.DRIFT_MIN, C.DRIFT_MAX, angle))
		pending += kmh / C.DRIFT_DIVISOR * angle_factor * combo * delta
	else:
		continuous = 0.0
		ended += delta
		if ended >= C.BANK_DELAY and pending > 0.0:
			total += pending
			pending = 0.0
			combo = 1
