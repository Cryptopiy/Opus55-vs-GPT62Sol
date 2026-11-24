class_name CarController
extends Node
## Base controller: releases every input (a parked / idle car). The car calls
## update_controls() at the start of each physics tick; subclasses override.


func update_controls(car: Car, _delta: float) -> void:
	car.throttle = 0.0
	car.brake = 0.0
	car.steer_input = 0.0
	car.handbrake = false
